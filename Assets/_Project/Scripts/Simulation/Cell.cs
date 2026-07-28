namespace Cinder.Simulation
{
    /// <summary>
    /// 世界最小单元（4 字节，blittable）。MaterialId 索引 MaterialTable。
    /// </summary>
    public struct Cell
    {
        /// <summary>本帧已被移动过，防止一次 tick 内二次移动。</summary>
        public const byte FlagMoved = 1;

        public ushort MaterialId;

        /// <summary>颜色变体（调色板索引抖动），仅渲染使用。</summary>
        public byte Variant;

        /// <summary>物质状态，含义由物质类型决定（火焰 = 剩余寿命）。</summary>
        public byte State;

        public byte Flags;

        public bool IsEmpty => MaterialId == BuiltinMaterials.Empty;

        public static Cell Of(ushort materialId, byte variant = 0, byte state = 0) =>
            new Cell { MaterialId = materialId, Variant = variant, State = state };
    }
}
