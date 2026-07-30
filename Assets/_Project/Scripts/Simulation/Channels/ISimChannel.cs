using System;
using Unity.Collections;

namespace Cinder.Simulation
{
    /// <summary>
    /// 通道调度上下文：引擎每 tick 在移动求解之后传入。
    /// 通道可改写 Cells；改写物质时必须递增 Moved 以唤醒区块并标脏渲染。
    /// </summary>
    public struct SimChannelContext
    {
        public NativeArray<Cell> Cells;
        public NativeArray<MaterialProps> Mats;
        public NativeArray<ReactionRule> Reactions;
        public NativeArray<int> Moved;
        public int Width;
        public int Height;
        public int ChunksX;
        public uint Tick;
        public uint Seed;

        public int ChunkIndexOf(int x, int y) =>
            (y >> SimCoords.ChunkShift) * ChunksX + (x >> SimCoords.ChunkShift);
    }

    /// <summary>
    /// 模拟通道（物理场）：温度、化学反应、湿度、电荷等每场一个实现，独立增删。
    /// 新增一个物理场 = 新建一个实现类 + SimulationEngine.AddChannel 一行，内核零改动。
    /// 通道本体是托管类（一帧调用几次，虚调用可忽略），重活在 Burst Job 里。
    /// </summary>
    public interface ISimChannel : IDisposable
    {
        string Name { get; }

        /// <summary>运行时可关，用于排查与低配模式。</summary>
        bool Enabled { get; set; }

        /// <summary>按窗口尺寸分配自持平行数组（无自持数组的通道空实现）。</summary>
        void Allocate(int width, int height);

        /// <summary>窗口移位后调用：通道重置或重建自持数组内容。</summary>
        void OnWindowShifted();

        /// <summary>执行一个 tick 的场求解（主线程顺序执行，保证确定性）。</summary>
        void Step(in SimChannelContext ctx);
    }

    /// <summary>
    /// 通道调试探针（可选实现）：返回该通道在指定格的一行物理数据，用于 HUD。
    /// 新通道实现本接口即自动出现在调试面板，表现层零改动。
    /// </summary>
    public interface ISimProbe
    {
        string ProbeLine(int flatIndex);
    }
}
