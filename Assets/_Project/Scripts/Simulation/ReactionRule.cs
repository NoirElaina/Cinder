namespace Cinder.Simulation
{
    /// <summary>
    /// 双格反应规则（blittable）。参与者用 MatA/MatB 标识，与格子的左右/上下
    /// 方位无关——ReactionJob 按"格子实际物质"匹配谁是 MatA、谁是 MatB，
    /// 因此石头在酸的左/右/上/下，产物都落在正确的格子上（修复方位错配 bug）。
    /// 反应表对称写入，[a * Capacity + b] 与 [b * Capacity + a] 等价。
    /// </summary>
    public struct ReactionRule
    {
        /// <summary>1 = 此槽有反应，0 = 无反应（判空必须用这个字段）。</summary>
        public byte Exists;

        /// <summary>0..255 的反应概率权重。</summary>
        public byte Chance;

        /// <summary>参与者 A 的物质 Id。</summary>
        public ushort MatA;

        /// <summary>参与者 B 的物质 Id。</summary>
        public ushort MatB;

        /// <summary>A 反应后（或预算耗尽后）变成的物质 Id；与 MatA 相同 = 不变。</summary>
        public ushort OutA;

        /// <summary>B 反应后（或预算耗尽后）变成的物质 Id；与 MatB 相同 = 不变。</summary>
        public ushort OutB;

        /// <summary>
        /// A 每次反应消耗的 State 预算。0 = 立即按 OutA 转变；
        /// &gt;0 = 渐进消耗（State 视为剩余预算，耗尽即 &lt;=0 时 A 才变成 OutA）。
        /// 用于"酸有腐蚀预算，腐蚀几格后耗尽消失"这类有上限的反应物。
        /// </summary>
        public byte CostA;

        /// <summary>B 每次反应消耗的 State 预算，语义同 CostA。</summary>
        public byte CostB;
    }
}
