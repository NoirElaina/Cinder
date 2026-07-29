using System;
using System.Collections.Generic;
using Cinder.Simulation.Jobs;
using Unity.Jobs;

namespace Cinder.Simulation
{
    /// <summary>
    /// 模拟引擎：每个 tick 先跑移动求解（FallingSandJob），再按挂载顺序执行
    /// 各物理场通道（ISimChannel，热插拔），最后收尾。
    /// 物质表可被热替换（热插拔物质时由上层重新赋值 Table）。
    /// </summary>
    public sealed class SimulationEngine : IDisposable
    {
        readonly SimulationWindow window;
        readonly int seed;
        readonly List<ISimChannel> channels = new List<ISimChannel>();
        uint tick;
        int lastOriginX = int.MinValue;
        int lastOriginY;

        public SimulationEngine(SimulationWindow window, MaterialTable table, int seed)
        {
            this.window = window;
            this.seed = seed;
            Table = table;
        }

        public MaterialTable Table { get; set; }

        public uint Tick => tick;

        public IReadOnlyList<ISimChannel> Channels => channels;

        /// <summary>挂载一个物理场通道。</summary>
        public void AddChannel(ISimChannel channel)
        {
            channel.Allocate(window.Width, window.Height);
            channels.Add(channel);
        }

        /// <summary>卸载一个物理场通道并释放其自持资源。</summary>
        public bool RemoveChannel(ISimChannel channel)
        {
            if (!channels.Remove(channel)) return false;
            channel.Dispose();
            return true;
        }

        public void Step()
        {
            if (window.OriginChunkX != lastOriginX || window.OriginChunkY != lastOriginY)
            {
                lastOriginX = window.OriginChunkX;
                lastOriginY = window.OriginChunkY;
                for (int i = 0; i < channels.Count; i++) channels[i].OnWindowShifted();
            }

            uint current = tick++;
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
                Tick = current,
                Seed = (uint)seed,
                FireId = BuiltinMaterials.Fire,
            };
            job.Run();

            for (int i = 0; i < channels.Count; i++)
            {
                ISimChannel channel = channels[i];
                if (!channel.Enabled) continue;
                channel.Step(new SimChannelContext
                {
                    Cells = window.WriteArray,
                    Mats = Table.Native,
                    Reactions = Table.Reactions,
                    Moved = window.ChunkMoved,
                    Width = window.Width,
                    Height = window.Height,
                    ChunksX = window.ChunksX,
                    Tick = current,
                    Seed = (uint)seed,
                });
            }

            window.EndTick();
        }

        public void Dispose()
        {
            for (int i = 0; i < channels.Count; i++) channels[i].Dispose();
            channels.Clear();
        }
    }
}
