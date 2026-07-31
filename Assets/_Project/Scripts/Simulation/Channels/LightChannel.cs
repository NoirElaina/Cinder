using Cinder.Simulation.Jobs;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Channels
{
    /// <summary>
    /// 光照通道：维护逐格光照场（0..255），供 Cell Surface 渲染做暗环境 +
    /// 火光/岩浆局部照明 + 天光。每 tick 由 LightJob 从零重建（O(N) 扫掠，
    /// 无逐光源开销），因此窗口移位无需搬移数据。
    /// 光照是纯表现场：不回写 Cells，不影响模拟确定性。
    /// </summary>
    public sealed class LightChannel : ISimChannel, ISimProbe
    {
        NativeArray<byte> light;

        public string Name => "光照";
        public bool Enabled { get; set; } = true;

        /// <summary>当前光照场，与模拟窗口同布局。渲染打包 Job 直接读。</summary>
        public NativeArray<byte> Light => light;

        public void Allocate(int width, int height)
        {
            Dispose();
            light = new NativeArray<byte>(width * height, Allocator.Persistent);
        }

        /// <summary>每 tick 全量重建，移位后无需处理。</summary>
        public void OnWindowShifted() { }

        public void Step(in SimChannelContext ctx)
        {
            // 光照是纯表现场：每 2 tick 重建一次（15Hz）就足够平滑，
            // 窗口平移后的即时重建由上层调 Rebuild 兑底。
            if ((ctx.Tick & 1u) != 0u) return;
            Rebuild(ctx.Cells, ctx.Mats, ctx.Width, ctx.Height);
        }

        /// <summary>立即全量重建。窗口平移后光照场是旧布局数据，
        /// 上层应在平移后、渲染前调用一次，避免闪烁 1-2 帧。</summary>
        public void Rebuild(NativeArray<Cell> cells, NativeArray<MaterialProps> mats,
            int width, int height)
        {
            new LightJob
            {
                Cells = cells,
                Mats = mats,
                Light = light,
                Width = width,
                Height = height,
            }.Run();
        }

        public string ProbeLine(int flatIndex)
        {
            if (!light.IsCreated || flatIndex < 0 || flatIndex >= light.Length) return "-";
            return $"{light[flatIndex] * 100 / 255}%";
        }

        public void Dispose()
        {
            if (light.IsCreated) light.Dispose();
        }
    }
}
