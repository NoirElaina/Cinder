namespace Cinder.Simulation
{
    /// <summary>
    /// 世界单位与细物理格的唯一换算服务。一个世界单位 = 4 个细物理格；
    /// 相机、角色 Transform、投射物速度等都用世界单位，模拟、碰撞、挖掘
    /// 全部用细格整数坐标。禁止在其他文件散落 * 4 / 4 的手写换算。
    /// </summary>
    public static class WorldScale
    {
        /// <summary>每世界单位的细物理格数。存档版本与此耦合（见 ChunkSerializer）。</summary>
        public const int CellsPerUnit = 4;

        public const float CellsPerUnitF = CellsPerUnit;

        /// <summary>一个细格的世界尺寸。</summary>
        public const float UnitsPerCell = 1f / CellsPerUnitF;

        /// <summary>角色标准碰撞盒（细格）。</summary>
        public const int PlayerWidthCells = 12;
        public const int PlayerHeightCells = 20;

        /// <summary>普通行走可跨越的台阶高度（细格）。</summary>
        public const int StepHeightCells = 2;

        /// <summary>世界坐标 -> 细格坐标（floor 语义，负坐标正确）。</summary>
        public static int WorldToCell(float world)
        {
            int i = (int)(world * CellsPerUnitF);
            return world * CellsPerUnitF < i ? i - 1 : i;
        }

        /// <summary>世界坐标 -> 细格连续坐标（不取整，供碰撞体等浮点细格系统用）。</summary>
        public static float WorldToCellF(float world) => world * CellsPerUnitF;

        /// <summary>细格坐标 -> 该格左下角的世界坐标。</summary>
        public static float CellToWorld(int cell) => cell * UnitsPerCell;

        /// <summary>细格连续坐标 -> 世界坐标。</summary>
        public static float CellToWorld(float cell) => cell * UnitsPerCell;

        /// <summary>细格坐标 -> 该格中心的世界坐标。</summary>
        public static float CellCenterToWorld(int cell) => (cell + 0.5f) * UnitsPerCell;
    }
}
