namespace Cinder.Simulation
{
    /// <summary>
    /// 细格坐标与区块坐标换算。格是细物理像素（一个世界单位 = WorldScale.CellsPerUnit 格），
    /// 世界单位与格的互换只走 WorldScale。X 方向无限，Y 方向有限
    /// （见 WorldGrid.MinChunkY/MaxChunkY）。负数坐标的换算全部走位运算，语义为 floor。
    /// </summary>
    public static class SimCoords
    {
        public const int ChunkShift = 7;
        public const int ChunkSize = 1 << ChunkShift; // 128
        public const int ChunkMask = ChunkSize - 1;

        /// <summary>格坐标 -> 区块坐标（floor 语义，-1 -> -1）。</summary>
        public static int CellToChunk(int cell) => cell >> ChunkShift;

        /// <summary>格坐标 -> 区块内局部坐标（0..127）。</summary>
        public static int CellToLocal(int cell) => cell & ChunkMask;

        /// <summary>区块坐标 -> 该区块原点（左下角）的格坐标。</summary>
        public static int ChunkToCellOrigin(int chunk) => chunk << ChunkShift;

        /// <summary>打包区块坐标为字典键，支持负坐标。</summary>
        public static long PackKey(int chunkX, int chunkY) =>
            ((long)chunkX << 32) | (uint)chunkY;

        public static int UnpackX(long key) => (int)(key >> 32);

        public static int UnpackY(long key) => (int)(uint)(key & 0xFFFFFFFFL);
    }
}
