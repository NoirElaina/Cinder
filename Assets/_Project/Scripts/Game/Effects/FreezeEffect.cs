using UnityEngine;

namespace Cinder.Game.Effects
{
    /// <summary>冰冻效果：命中世界时入队一个球形降温请求（水结冰靠温度通道相变）。</summary>
    [CreateAssetMenu(menuName = "Cinder/Effects/Freeze Effect")]
    public sealed class FreezeEffect : ProjectileEffectDefinition
    {
        [Min(0)] public int Radius = 3;

        [Tooltip("中心降温幅度（K），按距离线性衰减")]
        [Min(0)] public int Kelvin = 600;

        public override IProjectileBehavior Decorate(IProjectileBehavior inner) =>
            new Decorator(inner, Radius, Kelvin);

        sealed class Decorator : IProjectileBehavior
        {
            readonly IProjectileBehavior inner;
            readonly int radius;
            readonly int kelvin;

            public Decorator(IProjectileBehavior inner, int radius, int kelvin)
            {
                this.inner = inner;
                this.radius = radius;
                this.kelvin = kelvin;
            }

            public void ModifySpec(ref ProjectileSpec spec) => inner.ModifySpec(ref spec);

            public void OnHitWorld(ref ProjectileSpec spec, in ProjectileHit hit)
            {
                inner.OnHitWorld(ref spec, hit);
                hit.Emit(EffectRequest.Freeze(hit.CellX, hit.CellY, radius, kelvin));
            }
        }
    }
}
