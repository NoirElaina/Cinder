using System;
using Unity.Collections;

namespace Cinder.Simulation
{
    /// <summary>
    /// 模拟窗口：覆盖 W x H 个区块的平坦双缓冲数组，是模拟唯一发生的地方。
    /// 窗口"检出"WorldGrid 的区块，移位时把离开区域的区块写回存储，
    /// 因此跨区块边界的物质移动天然无竞态。
    /// 每帧只有移位发生时有拷贝，稳态零拷贝。
    /// </summary>
    public sealed class SimulationWindow : IDisposable
    {
        public readonly int ChunksX;
        public readonly int ChunksY;

        /// <summary>窗口左下角区块的世界区块坐标。</summary>
        public int OriginChunkX { get; private set; }
        public int OriginChunkY { get; private set; }

        NativeArray<Cell> read;
        NativeArray<Cell> write;

        /// <summary>每区块：本 tick 是否参与模拟。</summary>
        public NativeArray<byte> ChunkAwake;

        /// <summary>每区块：本 tick 移动计数（Job 写入，EndTick 消费后清零）。</summary>
        public NativeArray<int> ChunkMoved;

        /// <summary>每区块：渲染脏标记（EndTick 闩锁，渲染方清除）。</summary>
        public NativeArray<byte> ChunkDirty;

        readonly bool[] movedScratch;

        public SimulationWindow(int chunksX, int chunksY, int originChunkX, int originChunkY)
        {
            ChunksX = chunksX;
            ChunksY = chunksY;
            OriginChunkX = originChunkX;
            OriginChunkY = originChunkY;
            int cellCount = Width * Height;
            read = new NativeArray<Cell>(cellCount, Allocator.Persistent);
            write = new NativeArray<Cell>(cellCount, Allocator.Persistent);
            int chunkCount = ChunkCount;
            ChunkAwake = new NativeArray<byte>(chunkCount, Allocator.Persistent);
            ChunkMoved = new NativeArray<int>(chunkCount, Allocator.Persistent);
            ChunkDirty = new NativeArray<byte>(chunkCount, Allocator.Persistent);
            movedScratch = new bool[chunkCount];
            for (int i = 0; i < chunkCount; i++)
            {
                ChunkAwake[i] = 1;
                ChunkDirty[i] = 1;
            }
        }

        public int Width => ChunksX * SimCoords.ChunkSize;
        public int Height => ChunksY * SimCoords.ChunkSize;
        public int ChunkCount => ChunksX * ChunksY;

        /// <summary>仅供 SimulationEngine/渲染层访问的当前状态缓冲。</summary>
        public NativeArray<Cell> ReadArray => read;

        /// <summary>仅供 SimulationEngine 访问的写入缓冲。</summary>
        public NativeArray<Cell> WriteArray => write;

        public bool ContainsCell(int worldX, int worldY)
        {
            int lx = worldX - SimCoords.ChunkToCellOrigin(OriginChunkX);
            int ly = worldY - SimCoords.ChunkToCellOrigin(OriginChunkY);
            return lx >= 0 && lx < Width && ly >= 0 && ly < Height;
        }

        public bool ContainsChunk(int chunkX, int chunkY) =>
            chunkX >= OriginChunkX && chunkX < OriginChunkX + ChunksX
            && chunkY >= OriginChunkY && chunkY < OriginChunkY + ChunksY;

        int IndexOf(int worldX, int worldY)
        {
            int lx = worldX - SimCoords.ChunkToCellOrigin(OriginChunkX);
            int ly = worldY - SimCoords.ChunkToCellOrigin(OriginChunkY);
            return ly * Width + lx;
        }

        public Cell GetCell(int worldX, int worldY) => read[IndexOf(worldX, worldY)];

        /// <summary>外部编辑（挖掘/放置）。写当前缓冲并唤醒所在区块。</summary>
        public void SetCell(int worldX, int worldY, in Cell cell)
        {
            if (!ContainsCell(worldX, worldY)) return;
            int index = IndexOf(worldX, worldY);
            read[index] = cell;
            int chunkIndex = ChunkIndexOf(index);
            ChunkAwake[chunkIndex] = 1;
            ChunkDirty[chunkIndex] = 1;
        }

        public int ChunkIndexOf(int cellIndex)
        {
            int lx = cellIndex % Width;
            int ly = cellIndex / Width;
            return (ly >> SimCoords.ChunkShift) * ChunksX + (lx >> SimCoords.ChunkShift);
        }

        /// <summary>世界区块坐标 -> 窗口内区块序号；不在窗口内返回 -1。</summary>
        public int WindowChunkIndex(int chunkX, int chunkY)
        {
            if (!ContainsChunk(chunkX, chunkY)) return -1;
            return (chunkY - OriginChunkY) * ChunksX + (chunkX - OriginChunkX);
        }

        /// <summary>tick 收尾：交换双缓冲、闩锁脏标记、按移动情况更新休眠状态。</summary>
        public void EndTick()
        {
            (read, write) = (write, read);

            for (int i = 0; i < ChunkCount; i++)
            {
                movedScratch[i] = ChunkMoved[i] > 0;
                if (movedScratch[i]) ChunkDirty[i] = 1;
            }

            for (int cy = 0; cy < ChunksY; cy++)
            {
                for (int cx = 0; cx < ChunksX; cx++)
                {
                    int i = cy * ChunksX + cx;
                    bool active = movedScratch[i]
                        || (cx > 0 && movedScratch[i - 1])
                        || (cx < ChunksX - 1 && movedScratch[i + 1])
                        || (cy > 0 && movedScratch[i - ChunksX])
                        || (cy < ChunksY - 1 && movedScratch[i + ChunksX]);
                    ChunkAwake[i] = (byte)(active ? 1 : 0);
                    ChunkMoved[i] = 0;
                }
            }
        }

        /// <summary>
        /// 把窗口平移到新的原点（以区块为单位）。离开区域的区块写回 WorldGrid
        /// （标记 Modified），进入区域的区块从 WorldGrid 检出；
        /// 世界 Y 范围之外填基岩（下方）或空（上方）。
        /// </summary>
        public void Shift(int newOriginX, int newOriginY, WorldGrid grid,
            Func<int, int, byte[]> diskLoader)
        {
            int cellCount = Width * Height;
            var newRead = new NativeArray<Cell>(cellCount, Allocator.Persistent);
            var newWrite = new NativeArray<Cell>(cellCount, Allocator.Persistent);

            // 1. 填充新窗口
            for (int ncy = 0; ncy < ChunksY; ncy++)
            {
                for (int ncx = 0; ncx < ChunksX; ncx++)
                {
                    int gcx = newOriginX + ncx;
                    int gcy = newOriginY + ncy;
                    int dstStart = (ncy * SimCoords.ChunkSize) * Width + ncx * SimCoords.ChunkSize;

                    if (ContainsChunk(gcx, gcy))
                    {
                        // 旧窗口内平移拷贝
                        int srcStart = ((gcy - OriginChunkY) * SimCoords.ChunkSize) * Width
                            + (gcx - OriginChunkX) * SimCoords.ChunkSize;
                        CopyChunkStrided(read, srcStart, Width, newRead, dstStart, Width);
                    }
                    else
                    {
                        LoadChunkInto(newRead, dstStart, gcx, gcy, grid, diskLoader);
                    }
                }
            }

            // 2. 换出离开区域的区块
            for (int ocy = 0; ocy < ChunksY; ocy++)
            {
                for (int ocx = 0; ocx < ChunksX; ocx++)
                {
                    int gcx = OriginChunkX + ocx;
                    int gcy = OriginChunkY + ocy;
                    bool stillInside =
                        gcx >= newOriginX && gcx < newOriginX + ChunksX
                        && gcy >= newOriginY && gcy < newOriginY + ChunksY;
                    if (stillInside || !grid.ContainsY(gcy)) continue;

                    var evicted = new ChunkData(gcx, gcy) { Modified = true };
                    int srcStart = (ocy * SimCoords.ChunkSize) * Width + ocx * SimCoords.ChunkSize;
                    CopyChunkStrided(read, srcStart, Width, evicted.Cells, 0, SimCoords.ChunkSize);
                    grid.Attach(evicted);
                }
            }

            // 3. 替换缓冲并复位标记
            read.Dispose();
            write.Dispose();
            read = newRead;
            write = newWrite;
            OriginChunkX = newOriginX;
            OriginChunkY = newOriginY;
            for (int i = 0; i < ChunkCount; i++)
            {
                ChunkAwake[i] = 1;
                ChunkMoved[i] = 0;
                ChunkDirty[i] = 1;
            }
        }

        /// <summary>
        /// 首次填充：直接把窗口放到指定原点并从 WorldGrid 检出全部区块，
        /// 不做换出（旧缓冲内容无意义）。
        /// </summary>
        public void FillFrom(int originX, int originY, WorldGrid grid,
            Func<int, int, byte[]> diskLoader)
        {
            OriginChunkX = originX;
            OriginChunkY = originY;
            for (int ncy = 0; ncy < ChunksY; ncy++)
            {
                for (int ncx = 0; ncx < ChunksX; ncx++)
                {
                    int dstStart = (ncy * SimCoords.ChunkSize) * Width + ncx * SimCoords.ChunkSize;
                    LoadChunkInto(read, dstStart, originX + ncx, originY + ncy, grid, diskLoader);
                }
            }
            for (int i = 0; i < ChunkCount; i++)
            {
                ChunkAwake[i] = 1;
                ChunkMoved[i] = 0;
                ChunkDirty[i] = 1;
            }
        }

        void LoadChunkInto(NativeArray<Cell> dst, int dstStart, int gcx, int gcy,
            WorldGrid grid, Func<int, int, byte[]> diskLoader)
        {
            if (grid.ContainsY(gcy))
            {
                ChunkData chunk = grid.GetOrCreate(gcx, gcy, diskLoader);
                CopyChunkStrided(chunk.Cells, 0, SimCoords.ChunkSize, dst, dstStart, Width);
                grid.Remove(gcx, gcy);
                chunk.Dispose();
            }
            else
            {
                FillChunk(dst, dstStart,
                    gcy < grid.MinChunkY ? BuiltinMaterials.Bedrock : BuiltinMaterials.Empty);
            }
        }

        static void CopyChunkStrided(NativeArray<Cell> src, int srcStart, int srcStride,
            NativeArray<Cell> dst, int dstStart, int dstStride)
        {
            for (int ly = 0; ly < SimCoords.ChunkSize; ly++)
            {
                NativeArray<Cell>.Copy(
                    src, srcStart + ly * srcStride,
                    dst, dstStart + ly * dstStride,
                    SimCoords.ChunkSize);
            }
        }

        void FillChunk(NativeArray<Cell> dst, int dstStart, ushort materialId)
        {
            for (int ly = 0; ly < SimCoords.ChunkSize; ly++)
            {
                int rowStart = dstStart + ly * Width;
                for (int lx = 0; lx < SimCoords.ChunkSize; lx++)
                    dst[rowStart + lx] = Cell.Of(materialId);
            }
        }

        public void Dispose()
        {
            if (read.IsCreated) read.Dispose();
            if (write.IsCreated) write.Dispose();
            if (ChunkAwake.IsCreated) ChunkAwake.Dispose();
            if (ChunkMoved.IsCreated) ChunkMoved.Dispose();
            if (ChunkDirty.IsCreated) ChunkDirty.Dispose();
        }
    }
}
