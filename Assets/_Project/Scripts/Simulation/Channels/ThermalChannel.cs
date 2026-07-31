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
    public sealed class ThermalChannel : ISimChannel, ISimProbe
    {
        /// <summary>环境温度（约 17 摄氏度）。</summary>
        public const short AmbientK = 290;

        /// <summary>温度场上限（K），外部加热与扩散都钳制在此范围内。</summary>
        public const short MaxK = 6000;

        NativeArray<short> tempRead;
        NativeArray<short> tempWrite;

        /// <summary>全环境温度模板，重置时整块拷贝（避免百万次逐格托管写入）。</summary>
        NativeArray<short> ambientTemplate;

        /// <summary>外部热源（效果总线/编辑器等）累积的温度变化，下个 tick 生效。</summary>
        NativeArray<int> pendingDelta;

        /// <summary>有待结算的外部热量才扫 pendingDelta（绝大多数 tick 为假）。</summary>
        bool hasPending;

        public string Name => "温度";
        public bool Enabled { get; set; } = true;

        public void Allocate(int width, int height)
        {
            Dispose();
            int count = width * height;
            tempRead = new NativeArray<short>(count, Allocator.Persistent);
            tempWrite = new NativeArray<short>(count, Allocator.Persistent);
            pendingDelta = new NativeArray<int>(count, Allocator.Persistent);
            ambientTemplate = new NativeArray<short>(count, Allocator.Persistent);
            for (int i = 0; i < count; i++) ambientTemplate[i] = AmbientK;
            ResetToAmbient();
        }

        /// <summary>移位后重置：自热物质下一 tick 会自行回到 SelfTempK。</summary>
        public void OnWindowShifted()
        {
            ResetToAmbient();
            if (!hasPending) return;
            for (int i = 0; i < pendingDelta.Length; i++) pendingDelta[i] = 0;
            hasPending = false;
        }

        /// <summary>
        /// 外部施加温度变化（K，可负）。只累积不立即写场，
        /// 下个 tick 扩散前统一结算，保证模拟时序确定。
        /// </summary>
        public void AddHeat(int flatIndex, int deltaK)
        {
            pendingDelta[flatIndex] += deltaK;
            hasPending = true;
        }

        /// <summary>读取某格当前温度（K）。供调试视图与探针使用。</summary>
        public short GetTempK(int flatIndex)
        {
            if (!tempWrite.IsCreated || flatIndex < 0 || flatIndex >= tempWrite.Length) return AmbientK;
            return tempWrite[flatIndex];
        }

        /// <summary>当前温度场（最近一次 Step 的结果），渲染调试热力图直读。</summary>
        public NativeArray<short> CurrentTemps => tempWrite;

        public string ProbeLine(int flatIndex)
        {
            short k = GetTempK(flatIndex);
            return $"{k}K ({k - 273}℃)";
        }

        void ResetToAmbient()
        {
            NativeArray<short>.Copy(ambientTemplate, tempRead);
            NativeArray<short>.Copy(ambientTemplate, tempWrite);
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
                Awake = ctx.Awake,
                Width = ctx.Width,
                Height = ctx.Height,
                ChunksX = ctx.ChunksX,
                Tick = ctx.Tick,
                Seed = ctx.Seed,
                AmbientK = AmbientK,
                // 休眠块每 8 tick 全量求解一次，与反应通道的全量拍错开半相
                FullPass = (byte)((ctx.Tick & 7u) == 4u ? 1 : 0),
            }.Run();
        }

        void ApplyPending()
        {
            if (!hasPending) return;
            hasPending = false;
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
            if (ambientTemplate.IsCreated) ambientTemplate.Dispose();
        }
    }
}
