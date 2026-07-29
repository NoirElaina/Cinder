namespace Cinder.Simulation
{
    /// <summary>
    /// 双格反应规则（blittable）：A 遇 B 以 Chance/255 概率反应，双方各自变为
    /// OutA/OutB（与自身 Id 相同 = 保持不变）。反应表对称写入，
    /// 查询 reactions[a * Capacity + b] 与 reactions[b * Capacity + a] 等价。
    /// </summary>
    public struct ReactionRule
    {
        /// <summary>1 = 此槽有反应，0 = 无反应（判空必须用这个字段）。</summary>
        public byte Exists;

        /// <summary>0..255 的反应概率权重。</summary>
        public byte Chance;

        /// <summary>A 格反应后变成的物质 Id。</summary>
        public ushort OutA;

        /// <summary>B 格反应后变成的物质 Id。</summary>
        public ushort OutB;
    }
}
