using System;
using Unity.Collections;

namespace Cinder.Simulation
{
    /// <summary>
    /// 驻留区块：128x128 格的单缓冲存储。模拟只发生在 SimulationWindow 内，
    /// 存储中的区块是静止的，仅用于渲染与跨窗口迁移。
    /// </summary>
    public sealed class ChunkData : IDisposable
    {
        public const int CellCount = SimCoords.ChunkSize * SimCoords.ChunkSize;

        public readonly int ChunkX;
        public readonly int ChunkY;

        public NativeArray<Cell> Cells;

        /// <summary>是否相对程序化生成结果被修改过（决定卸载时是否落盘）。</summary>
        public bool Modified;

        public ChunkData(int chunkX, int chunkY)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            Cells = new NativeArray<Cell>(CellCount, Allocator.Persistent);
        }

        public long Key => SimCoords.PackKey(ChunkX, ChunkY);

        public Cell Get(int localX, int localY) =>
            Cells[localY * SimCoords.ChunkSize + localX];

        public void Set(int localX, int localY, in Cell cell)
        {
            Cells[localY * SimCoords.ChunkSize + localX] = cell;
            Modified = true;
        }

        public void Dispose()
        {
            if (Cells.IsCreated) Cells.Dispose();
        }
    }
}
