using UnityEngine;

namespace Cinder.Game.Effects
{
    /// <summary>
    /// 投射物静态参数。发射时由武器基础值出发，依次穿过效果装饰链被逐级改写。
    /// </summary>
    public struct ProjectileSpec
    {
        public float Damage;
        public float Speed;
        public float Gravity;
        public float Lifetime;

        /// <summary>命中世界时的挖掘半径（格），0 = 不破坏地形。</summary>
        public int DigPower;

        /// <summary>可穿透的实体/格数。</summary>
        public int Pierce;

        /// <summary>飞行拖尾放置的物质 Id（0 = 无拖尾）。</summary>
        public ushort TrailMaterial;

        public Color32 Tint;

        /// <summary>命中后释放的载荷法术（触发弹），运行时清空防递归。</summary>
        public Spells.ProjectileSpellDefinition TriggerPayload;
    }

    /// <summary>
    /// 投射物命中世界的上下文。装饰器通过 Emit 往效果总线入队效果请求
    /// （爆炸/点燃/冰冻…），而不是自己直接改世界——世界写入统一由
    /// 效果处理器在 tick 间隙执行。
    /// </summary>
    public readonly struct ProjectileHit
    {
        public readonly int CellX;
        public readonly int CellY;

        /// <summary>被命中格的物质 Id。</summary>
        public readonly ushort Material;

        readonly EffectBus bus;

        public ProjectileHit(int cellX, int cellY, ushort material, EffectBus bus)
        {
            CellX = cellX;
            CellY = cellY;
            Material = material;
            this.bus = bus;
        }

        /// <summary>入队效果请求；无总线时静默丢弃（纯数据测试场景）。</summary>
        public void Emit(in EffectRequest request) => bus?.Emit(request);
    }

    /// <summary>
    /// 投射物行为装饰器接口。每一级效果包装内层行为，形成装饰链。
    /// 约定：外层先调用内层，再应用自己的修改（列表顺序 = 生效顺序）。
    /// </summary>
    public interface IProjectileBehavior
    {
        void ModifySpec(ref ProjectileSpec spec);

        /// <summary>命中世界时的二次改写与效果发射（爆炸/点燃/冰冻等）。</summary>
        void OnHitWorld(ref ProjectileSpec spec, in ProjectileHit hit);
    }

    /// <summary>装饰链底：什么都不做。</summary>
    public sealed class BaseProjectileBehavior : IProjectileBehavior
    {
        public static readonly BaseProjectileBehavior Instance = new BaseProjectileBehavior();

        public void ModifySpec(ref ProjectileSpec spec) { }

        public void OnHitWorld(ref ProjectileSpec spec, in ProjectileHit hit) { }
    }
}
