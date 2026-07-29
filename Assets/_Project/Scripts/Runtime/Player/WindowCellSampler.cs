using Cinder.Game.Physics;
using Cinder.Runtime.World;

namespace Cinder.Runtime.Player
{
    /// <summary>用模拟窗口实现的固体采样器（玩家像素碰撞用）。</summary>
    public sealed class WindowCellSampler : ICellSampler
    {
        readonly WorldStreamer streamer;

        public WindowCellSampler(WorldStreamer streamer)
        {
            this.streamer = streamer;
        }

        public bool IsSolid(int cellX, int cellY) => streamer.IsSolidCell(cellX, cellY);
    }
}
