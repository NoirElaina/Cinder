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
    }

    /// <summary>
    /// 投射物行为装饰器接口。每一级效果包装内层行为，形成装饰链。
    /// 约定：外层先调用内层，再应用自己的修改（列表顺序 = 生效顺序）。
    /// </summary>
    public interface IProjectileBehavior
    {
        void ModifySpec(ref ProjectileSpec spec);

        /// <summary>命中世界时的二次改写（如爆炸物此时才决定挖掘半径）。</summary>
        void OnHitWorld(ref ProjectileSpec spec, int cellX, int cellY, ushort hitMaterial);
    }

    /// <summary>装饰链底：什么都不做。</summary>
    public sealed class BaseProjectileBehavior : IProjectileBehavior
    {
        public static readonly BaseProjectileBehavior Instance = new BaseProjectileBehavior();

        public void ModifySpec(ref ProjectileSpec spec) { }

        public void OnHitWorld(ref ProjectileSpec spec, int cellX, int cellY, ushort hitMaterial) { }
    }
}
