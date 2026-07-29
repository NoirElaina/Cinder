using UnityEngine;

namespace Cinder.Game.Effects
{
    /// <summary>
    /// 爆炸效果：命中世界时入队爆炸请求（挖除外环 + 心区点燃 + 加热）。
    /// 基础挖掘半径并入爆炸半径，爆炸处理器自带地形破坏。
    /// </summary>
    [CreateAssetMenu(menuName = "Cinder/Effects/Explosive Effect")]
    public sealed class ExplosiveEffect : ProjectileEffectDefinition
    {
        [Min(1)] public int RadiusAdd = 4;

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

            public void OnHitWorld(ref ProjectileSpec spec, in ProjectileHit hit)
            {
                inner.OnHitWorld(ref spec, hit);
                hit.Emit(EffectRequest.Explosion(hit.CellX, hit.CellY,
                    spec.DigPower + radiusAdd));
                spec.DigPower = 0; // 爆炸自带地形破坏，避免与基础挖掘重复
            }
        }
    }
}
