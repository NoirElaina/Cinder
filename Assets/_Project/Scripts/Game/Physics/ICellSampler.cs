namespace Cinder.Game.Physics
{
    /// <summary>
    /// 世界固体采样接口：像素碰撞体通过它查询某格是否可站立。
    /// 由 Runtime 层用模拟窗口实现；测试用假实现。界外约定为固体。
    /// </summary>
    public interface ICellSampler
    {
        bool IsSolid(int cellX, int cellY);
    }
}
