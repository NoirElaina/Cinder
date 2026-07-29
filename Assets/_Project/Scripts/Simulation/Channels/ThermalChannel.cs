using Cinder.Simulation.Jobs;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Channels
{
    /// <summary>
    /// 温度通道：逐格温度场（开尔文），扩散 + 环境回落 + 数据驱动相变
    /// （点燃/熔化/沸腾/凝固，全部由 MaterialProps 热学字段声明）。
    /// 温度存自持平行数组，双缓冲读写保证确定性。
    /// </summary>
    public sealed class ThermalChannel : ISimChannel
    {
        /// <summary>环境温度（约 17 摄氏度）。</summary>
        public const short AmbientK = 290;

        /// <summary>温度场上限（K），外部加热与扩散都钳制在此范围内。</summary>
        public const short MaxK = 6000;

        NativeArray<short> tempRead;
        NativeArray<short> tempWrite;

        /// <summary>外部热源（效果总线/编辑器等）累积的温度变化，下个 tick 生效。</summary>
        NativeArray<int> pendingDelta;

        public string Name => "温度";
        public bool Enabled { get; set; } = true;

        public void Allocate(int width, int height)
        {
            Dispose();
            int count = width * height;
            tempRead = new NativeArray<short>(count, Allocator.Persistent);
            tempWrite = new NativeArray<short>(count, Allocator.Persistent);
            pendingDelta = new NativeArray<int>(count, Allocator.Persistent);
            ResetToAmbient();
        }

        /// <summary>移位后重置：自热物质下一 tick 会自行回到 SelfTempK。</summary>
        public void OnWindowShifted()
        {
            ResetToAmbient();
            for (int i = 0; i < pendingDelta.Length; i++) pendingDelta[i] = 0;
        }

        /// <summary>
        /// 外部施加温度变化（K，可负）。只累积不立即写场，
        /// 下个 tick 扩散前统一结算，保证模拟时序确定。
        /// </summary>
        public void AddHeat(int flatIndex, int deltaK) => pendingDelta[flatIndex] += deltaK;

        void ResetToAmbient()
        {
            for (int i = 0; i < tempRead.Length; i++)
            {
                tempRead[i] = AmbientK;
                tempWrite[i] = AmbientK;
            }
        }

        public void Step(in SimChannelContext ctx)
        {
            (tempRead, tempWrite) = (tempWrite, tempRead);
            ApplyPending();
            new ThermalJob
            {
                Cells = ctx.Cells,
                Mats = ctx.Mats,
                Moved = ctx.Moved,
                TempRead = tempRead,
                TempWrite = tempWrite,
                Width = ctx.Width,
                Height = ctx.Height,
                ChunksX = ctx.ChunksX,
                Tick = ctx.Tick,
                Seed = ctx.Seed,
                AmbientK = AmbientK,
            }.Run();
        }

        void ApplyPending()
        {
            for (int i = 0; i < pendingDelta.Length; i++)
            {
                int delta = pendingDelta[i];
                if (delta == 0) continue;
                pendingDelta[i] = 0;
                int t = tempRead[i] + delta;
                tempRead[i] = (short)(t < 0 ? 0 : t > MaxK ? MaxK : t);
            }
        }

        public void Dispose()
        {
            if (tempRead.IsCreated) tempRead.Dispose();
            if (tempWrite.IsCreated) tempWrite.Dispose();
            if (pendingDelta.IsCreated) pendingDelta.Dispose();
        }
    }
}
