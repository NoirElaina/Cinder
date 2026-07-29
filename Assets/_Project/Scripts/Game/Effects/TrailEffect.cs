using UnityEngine;

namespace Cinder.Game.Effects
{
    /// <summary>拖尾效果：投射物飞行路径上放置指定物质（火/酸/水…）。</summary>
    [CreateAssetMenu(menuName = "Cinder/Effects/Trail Effect")]
    public sealed class TrailEffect : ProjectileEffectDefinition
    {
        [Tooltip("拖尾放置的物质 Id，0 = 无")]
        public int TrailMaterial;

        public override IProjectileBehavior Decorate(IProjectileBehavior inner) =>
            new Decorator(inner, (ushort)Mathf.Clamp(TrailMaterial, 0, 255));

        sealed class Decorator : IProjectileBehavior
        {
            readonly IProjectileBehavior inner;
            readonly ushort material;

            public Decorator(IProjectileBehavior inner, ushort material)
            {
                this.inner = inner;
                this.material = material;
            }

            public void ModifySpec(ref ProjectileSpec spec)
            {
                inner.ModifySpec(ref spec);
                if (material != 0) spec.TrailMaterial = material;
            }

            public void OnHitWorld(ref ProjectileSpec spec, in ProjectileHit hit) =>
                inner.OnHitWorld(ref spec, hit);
        }
    }
}
