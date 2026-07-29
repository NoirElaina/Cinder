using System;
using System.Collections.Generic;
using Cinder.Runtime.Materials;
using Cinder.Simulation;

namespace Cinder.Runtime.World
{
    /// <summary>
    /// 世界流式加载：以焦点为中心维护两级区域——
    /// 模拟窗口（4x3 区块，检出到 SimulationWindow）与驻留环
    /// （窗口外扩一圈，留在 WorldGrid 供渲染）。环外区块卸载：
    /// 改过的落盘，未改的丢弃（确定性生成可重建）。
    /// </summary>
    public sealed class WorldStreamer : IDisposable
    {
        public const int WindowChunksX = 4;
        public const int WindowChunksY = 3;

        /// <summary>驻留环在窗口四周外扩的区块数。</summary>
        public int ResidentRadiusX = 3;
        public int ResidentRadiusY = 2;

        readonly MaterialDatabase db;
        readonly ChunkStore store;
        bool initialized;

        /// <summary>待加载的驻留区块（分帧摊销，避免移位卡顿）。</summary>
        readonly Queue<long> pendingResidents = new Queue<long>();
        readonly HashSet<long> pendingSet = new HashSet<long>();

        public WorldGrid Grid { get; }
        public SimulationWindow Window { get; }

        public WorldStreamer(int seed, MaterialDatabase database)
        {
            db = database;
            Grid = new WorldGrid(seed);
            store = new ChunkStore(seed);
            Window = new SimulationWindow(WindowChunksX, WindowChunksY, 0, 0);
        }

        /// <summary>焦点（格坐标）变化时调用，按需移位窗口并同步驻留环。</summary>
        public void SetFocus(int cellX, int cellY)
        {
            int desiredX = SimCoords.CellToChunk(cellX) - WindowChunksX / 2;
            int desiredY = SimCoords.CellToChunk(cellY) - WindowChunksY / 2;

            if (!initialized)
            {
                Window.FillFrom(desiredX, desiredY, Grid, LoadBytes);
                initialized = true;
            }
            else if (desiredX != Window.OriginChunkX || desiredY != Window.OriginChunkY)
            {
                Window.Shift(desiredX, desiredY, Grid, LoadBytes);
            }

            SyncResidents();
        }

        /// <summary>圆形笔刷编辑（挖掘/放置），仅作用于模拟窗口内。</summary>
        public void EditSphere(int worldX, int worldY, int radius, ushort materialId)
        {
            byte baseLife = db.Table[materialId].BaseLife;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;
                    int x = worldX + dx;
                    int y = worldY + dy;
                    if (!Window.ContainsCell(x, y)) continue;
                    Window.SetCell(x, y, new Cell
                    {
                        MaterialId = materialId,
                        Variant = SimHash.Variant(x, y, (uint)Grid.Seed),
                        State = baseLife,
                    });
                }
            }
        }

        byte[] LoadBytes(int chunkX, int chunkY) => store.TryLoad(chunkX, chunkY);

        void SyncResidents()
        {
            int minCx = Window.OriginChunkX - ResidentRadiusX;
            int maxCx = Window.OriginChunkX + WindowChunksX - 1 + ResidentRadiusX;
            int minCy = Window.OriginChunkY - ResidentRadiusY;
            int maxCy = Window.OriginChunkY + WindowChunksY - 1 + ResidentRadiusY;

            // 缺失的驻留区块进入分帧加载队列，而不是当帧同步生成
            for (int cy = minCy; cy <= maxCy; cy++)
            {
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    if (Window.ContainsChunk(cx, cy)) continue;
                    if (!Grid.ContainsY(cy)) continue;
                    if (Grid.TryGet(cx, cy, out _)) continue;
                    long key = SimCoords.PackKey(cx, cy);
                    if (pendingSet.Add(key)) pendingResidents.Enqueue(key);
                }
            }

            var unload = new List<ChunkData>();
            foreach (ChunkData chunk in Grid.Loaded)
            {
                if (chunk.ChunkX >= minCx && chunk.ChunkX <= maxCx
                    && chunk.ChunkY >= minCy && chunk.ChunkY <= maxCy) continue;
                unload.Add(chunk);
            }
            foreach (ChunkData chunk in unload)
            {
                Grid.Remove(chunk.ChunkX, chunk.ChunkY);
                if (chunk.Modified) store.SaveAsync(chunk);
                chunk.Dispose();
            }
        }

        /// <summary>
        /// 每帧调用，按预算加载驻留区块。过期的条目（焦点已移远）直接丢弃。
        /// </summary>
        public void ProcessPendingLoads(int budget)
        {
            int minCx = Window.OriginChunkX - ResidentRadiusX;
            int maxCx = Window.OriginChunkX + WindowChunksX - 1 + ResidentRadiusX;
            int minCy = Window.OriginChunkY - ResidentRadiusY;
            int maxCy = Window.OriginChunkY + WindowChunksY - 1 + ResidentRadiusY;

            while (budget > 0 && pendingResidents.Count > 0)
            {
                long key = pendingResidents.Dequeue();
                pendingSet.Remove(key);
                int cx = SimCoords.UnpackX(key);
                int cy = SimCoords.UnpackY(key);
                if (cx < minCx || cx > maxCx || cy < minCy || cy > maxCy) continue;
                if (Window.ContainsChunk(cx, cy)) continue;
                Grid.GetOrCreate(cx, cy, LoadBytes);
                budget--;
            }
        }

        /// <summary>退出前把窗口区块与驻留区块全部落盘（仅修改过的）。</summary>
        public void SaveAll()
        {
            for (int cy = 0; cy < Window.ChunksY; cy++)
            {
                for (int cx = 0; cx < Window.ChunksX; cx++)
                {
                    int gcx = Window.OriginChunkX + cx;
                    int gcy = Window.OriginChunkY + cy;
                    if (!Grid.ContainsY(gcy)) continue;
                    var chunk = new ChunkData(gcx, gcy) { Modified = true };
                    int srcStart = (cy * SimCoords.ChunkSize) * Window.Width + cx * SimCoords.ChunkSize;
                    for (int ly = 0; ly < SimCoords.ChunkSize; ly++)
                    {
                        Unity.Collections.NativeArray<Cell>.Copy(
                            Window.ReadArray, srcStart + ly * Window.Width,
                            chunk.Cells, ly * SimCoords.ChunkSize,
                            SimCoords.ChunkSize);
                    }
                    store.SaveSync(chunk);
                    chunk.Dispose();
                }
            }
            foreach (ChunkData chunk in Grid.Loaded)
                if (chunk.Modified) store.SaveSync(chunk);
        }

        public void Dispose()
        {
            Window.Dispose();
            Grid.Dispose();
        }
    }
}
