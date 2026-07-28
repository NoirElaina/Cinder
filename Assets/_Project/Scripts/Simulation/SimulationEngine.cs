using Cinder.Simulation.Jobs;
using Unity.Jobs;

namespace Cinder.Simulation
{
    /// <summary>
    /// 模拟引擎：每个 tick 调度 FallingSandJob 并收尾。
    /// 物质表可被热替换（热插拔物质时由上层重新赋值 Table）。
    /// </summary>
    public sealed class SimulationEngine
    {
        readonly SimulationWindow window;
        readonly int seed;
        uint tick;

        public SimulationEngine(SimulationWindow window, MaterialTable table, int seed)
        {
            this.window = window;
            this.seed = seed;
            Table = table;
        }

        public MaterialTable Table { get; set; }

        public uint Tick => tick;

        public void Step()
        {
            var job = new FallingSandJob
            {
                Read = window.ReadArray,
                Write = window.WriteArray,
                Mats = Table.Native,
                Awake = window.ChunkAwake,
                Moved = window.ChunkMoved,
                Width = window.Width,
                Height = window.Height,
                ChunksX = window.ChunksX,
                Tick = tick++,
                Seed = (uint)seed,
                FireId = BuiltinMaterials.Fire,
            };
            job.Run();
            window.EndTick();
        }
    }
}
