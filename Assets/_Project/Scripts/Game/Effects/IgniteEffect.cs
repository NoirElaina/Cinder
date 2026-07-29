using UnityEngine;

namespace Cinder.Game.Effects
{
    /// <summary>点燃效果：命中世界时入队一个球形点燃请求。</summary>
    [CreateAssetMenu(menuName = "Cinder/Effects/Ignite Effect")]
    public sealed class IgniteEffect : ProjectileEffectDefinition
    {
        [Min(0)] public int Radius = 2;

        public override IProjectileBehavior Decorate(IProjectileBehavior inner) =>
            new Decorator(inner, Radius);

        sealed class Decorator : IProjectileBehavior
        {
            readonly IProjectileBehavior inner;
            readonly int radius;

            public Decorator(IProjectileBehavior inner, int radius)
            {
                this.inner = inner;
                this.radius = radius;
            }

            public void ModifySpec(ref ProjectileSpec spec) => inner.ModifySpec(ref spec);

            public void OnHitWorld(ref ProjectileSpec spec, in ProjectileHit hit)
            {
                inner.OnHitWorld(ref spec, hit);
                hit.Emit(EffectRequest.Ignite(hit.CellX, hit.CellY, radius));
            }
        }
    }
}
