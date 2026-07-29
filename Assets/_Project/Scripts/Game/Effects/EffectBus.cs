using System.Collections.Generic;
using Cinder.Simulation;

namespace Cinder.Game.Effects
{
    /// <summary>效果种类。处理器按种类订阅，新增种类不影响既有代码。</summary>
    public enum EffectKind : byte
    {
        /// <summary>球形挖除地形（基岩级物质除外）。</summary>
        Dig = 0,

        /// <summary>爆炸：挖除外环 + 心区点燃可燃物 + 全场加热。</summary>
        Explosion = 1,

        /// <summary>加热：球形升温，驱动温度通道相变（煮水/点木/熔冰）。</summary>
        Heat = 2,

        /// <summary>冰冻：球形降温，驱动温度通道相变（水结冰等）。</summary>
        Freeze = 3,

        /// <summary>点燃：球形内可燃物直接变为燃烧产物。</summary>
        Ignite = 4,
    }

    /// <summary>
    /// 一次效果请求。任何系统（投射物、环境、未来的敌人）都可入队，
    /// 由效果总线在 tick 间隙统一派发给处理器执行。
    /// </summary>
    public readonly struct EffectRequest
    {
        public readonly EffectKind Kind;

        /// <summary>效果中心（世界格坐标）。</summary>
        public readonly int CellX;
        public readonly int CellY;

        /// <summary>影响半径（格）。</summary>
        public readonly int Radius;

        /// <summary>强度参数，含义按种类：Heat/Freeze = 中心温度变化 K。</summary>
        public readonly int Amount;

        EffectRequest(EffectKind kind, int cellX, int cellY, int radius, int amount)
        {
            Kind = kind;
            CellX = cellX;
            CellY = cellY;
            Radius = radius;
            Amount = amount;
        }

        public static EffectRequest Dig(int cellX, int cellY, int radius) =>
            new EffectRequest(EffectKind.Dig, cellX, cellY, radius, 0);

        public static EffectRequest Explosion(int cellX, int cellY, int radius) =>
            new EffectRequest(EffectKind.Explosion, cellX, cellY, radius, 0);

        public static EffectRequest Heat(int cellX, int cellY, int radius, int kelvin) =>
            new EffectRequest(EffectKind.Heat, cellX, cellY, radius, kelvin);

        public static EffectRequest Freeze(int cellX, int cellY, int radius, int kelvin) =>
            new EffectRequest(EffectKind.Freeze, cellX, cellY, radius, kelvin);

        public static EffectRequest Ignite(int cellX, int cellY, int radius) =>
            new EffectRequest(EffectKind.Ignite, cellX, cellY, radius, 0);
    }

    /// <summary>
    /// 效果处理器看到的世界写入接口。把效果系统与具体模拟实现解耦：
    /// 处理器不直接碰 SimulationWindow，测试可用任意实现喂给它。
    /// </summary>
    public interface IEffectWorld
    {
        bool ContainsCell(int worldX, int worldY);

        /// <summary>读取物质 Id；界外返回 Empty。</summary>
        ushort GetMaterial(int worldX, int worldY);

        /// <summary>按物质表规则放置（自动带颜色变体与 BaseLife）。</summary>
        void SetMaterial(int worldX, int worldY, ushort materialId);

        /// <summary>查询物质属性（可燃性/密度/热学字段等）。</summary>
        MaterialProps PropsOf(ushort materialId);

        /// <summary>给一格施加温度变化（K），可负。未挂温度通道时静默无效。</summary>
        void AddHeat(int worldX, int worldY, int deltaK);

        /// <summary>不可破坏约定：密度 255（基岩级）的物质免疫挖掘与爆炸。</summary>
        bool IsIndestructible(ushort materialId);
    }

    /// <summary>
    /// 效果处理器。热插拔单位：运行时 AddHandler/RemoveHandler 即可
    /// 增删一种效果行为，总线与其他处理器零改动。
    /// </summary>
    public interface IEffectHandler
    {
        EffectKind Kind { get; }
        void Handle(in EffectRequest request, IEffectWorld world);
    }

    /// <summary>
    /// 效果总线：请求队列 + 处理器表。生产者只入队不执行，
    /// 由宿主（WorldController）在模拟 tick 间隙 Flush，保证写世界时
    /// 没有 Job 在飞，时序确定。
    /// </summary>
    public sealed class EffectBus
    {
        readonly Queue<EffectRequest> queue = new Queue<EffectRequest>();
        readonly List<IEffectHandler> handlers = new List<IEffectHandler>();

        public IReadOnlyList<IEffectHandler> Handlers => handlers;
        public int PendingCount => queue.Count;

        /// <summary>入队一个效果请求（本帧 Flush 时执行）。</summary>
        public void Emit(in EffectRequest request) => queue.Enqueue(request);

        public void AddHandler(IEffectHandler handler)
        {
            if (handler != null && !handlers.Contains(handler)) handlers.Add(handler);
        }

        public bool RemoveHandler(IEffectHandler handler) => handlers.Remove(handler);

        /// <summary>清空队列并把所有请求派发给匹配种类的处理器。</summary>
        public void Flush(IEffectWorld world)
        {
            while (queue.Count > 0)
            {
                EffectRequest request = queue.Dequeue();
                for (int i = 0; i < handlers.Count; i++)
                    if (handlers[i].Kind == request.Kind)
                        handlers[i].Handle(request, world);
            }
        }
    }
}
