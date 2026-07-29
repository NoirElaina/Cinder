using Cinder.Simulation.Jobs;
using Unity.Jobs;

namespace Cinder.Simulation.Channels
{
    /// <summary>
    /// 化学反应通道：查对称反应表触发物质替换（岩浆淬水、酸腐蚀等）。
    /// 反应数据全部来自 MaterialTable（由 MaterialDatabase 烘焙），无自持数组。
    /// </summary>
    public sealed class ReactionChannel : ISimChannel
    {
        public string Name => "化学反应";
        public bool Enabled { get; set; } = true;

        public void Allocate(int width, int height) { }

        public void OnWindowShifted() { }

        public void Step(in SimChannelContext ctx)
        {
            new ReactionJob
            {
                Cells = ctx.Cells,
                Reactions = ctx.Reactions,
                Mats = ctx.Mats,
                Moved = ctx.Moved,
                Width = ctx.Width,
                Height = ctx.Height,
                ChunksX = ctx.ChunksX,
                TableCapacity = MaterialTable.Capacity,
                Tick = ctx.Tick,
                Seed = ctx.Seed,
            }.Run();
        }

        public void Dispose() { }
    }
}
