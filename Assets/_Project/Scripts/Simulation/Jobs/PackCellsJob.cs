using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Jobs
{
    /// <summary>
    /// 渲染打包 Job：把 Cell + 光照（调试模式为温度）压进每格一个 uint，
    /// 直接写入 GraphicsBuffer.LockBufferForWrite 锁定的显存。位布局与 CellSurface.shader 严格对应：
    ///   普通模式  [0..9] 物质 Id | [10..15] Variant 高 6 位 | [16..23] 光照 |
    ///             [24..31] 气体寿命分数 0..255（shader 据此淡出，其余物质为原始 State）
    ///   温度模式  [0..9] 物质 Id | [16..28] 温度 K（0..8191 钳制）
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct PackCellsJob : IJob
    {
        [ReadOnly] public NativeArray<Cell> Cells;
        [ReadOnly] public NativeArray<byte> Light;
        [ReadOnly] public NativeArray<short> Temps;
        [ReadOnly] public NativeArray<MaterialProps> Mats;
        [WriteOnly] public NativeArray<uint> Packed;

        /// <summary>0 = 普通渲染，1 = 温度热力图。</summary>
        public int Mode;

        public void Execute()
        {
            if (Mode == 1)
            {
                for (int i = 0; i < Cells.Length; i++)
                {
                    Cell c = Cells[i];
                    int t = Temps[i];
                    if (t < 0) t = 0; else if (t > 8191) t = 8191;
                    Packed[i] = (uint)(c.MaterialId & 0x3FF) | ((uint)t << 16);
                }
                return;
            }

            for (int i = 0; i < Cells.Length; i++)
            {
                Cell c = Cells[i];
                MaterialProps p = Mats[c.MaterialId];
                // 气体把剩余寿命归一化到 0..255 供 shader 淡出；无寿命气体（蒸汽）恒浓
                uint state = c.State;
                if (p.Type == MatterType.Gas)
                    state = p.BaseLife > 0 ? (uint)(c.State * 255 / p.BaseLife) : 255u;
                Packed[i] = (uint)(c.MaterialId & 0x3FF)
                    | ((uint)(c.Variant >> 2) << 10)
                    | ((uint)Light[i] << 16)
                    | (state << 24);
            }
        }
    }
}
