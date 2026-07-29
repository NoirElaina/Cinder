using UnityEngine;

namespace Cinder.Game.Effects
{
    /// <summary>爆炸效果：命中世界时追加挖掘半径，演示 OnHitWorld 钩子。</summary>
    [CreateAssetMenu(menuName = "Cinder/Effects/Explosive Effect")]
    public sealed class ExplosiveEffect : ProjectileEffectDefinition
    {
        [Min(0)] public int RadiusAdd = 4;

        public override IProjectileBehavior Decorate(IProjectileBehavior inner) =>
            new Decorator(inner, RadiusAdd);

        sealed class Decorator : IProjectileBehavior
        {
            readonly IProjectileBehavior inner;
            readonly int radiusAdd;

            public Decorator(IProjectileBehavior inner, int radiusAdd)
            {
                this.inner = inner;
                this.radiusAdd = radiusAdd;
            }

            public void ModifySpec(ref ProjectileSpec spec) => inner.ModifySpec(ref spec);

            public void OnHitWorld(ref ProjectileSpec spec, int cellX, int cellY, ushort hitMaterial)
            {
                inner.OnHitWorld(ref spec, cellX, cellY, hitMaterial);
                spec.DigPower += radiusAdd;
            }
        }
    }
}
