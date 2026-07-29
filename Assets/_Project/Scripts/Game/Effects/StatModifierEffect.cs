using UnityEngine;

namespace Cinder.Game.Effects
{
    /// <summary>通用数值修饰效果：加算/乘算投射物各项参数。</summary>
    [CreateAssetMenu(menuName = "Cinder/Effects/Stat Modifier Effect")]
    public sealed class StatModifierEffect : ProjectileEffectDefinition
    {
        public float DamageAdd;
        public float DamageMultiply = 1f;
        public float SpeedMultiply = 1f;
        public float GravityAdd;
        public float LifetimeMultiply = 1f;
        public int PierceAdd;
        public int DigPowerAdd;

        public override IProjectileBehavior Decorate(IProjectileBehavior inner) =>
            new Decorator(inner, this);

        sealed class Decorator : IProjectileBehavior
        {
            readonly IProjectileBehavior inner;
            readonly StatModifierEffect def;

            public Decorator(IProjectileBehavior inner, StatModifierEffect def)
            {
                this.inner = inner;
                this.def = def;
            }

            public void ModifySpec(ref ProjectileSpec spec)
            {
                inner.ModifySpec(ref spec);
                spec.Damage = spec.Damage * def.DamageMultiply + def.DamageAdd;
                spec.Speed *= def.SpeedMultiply;
                spec.Gravity += def.GravityAdd;
                spec.Lifetime *= def.LifetimeMultiply;
                spec.Pierce += def.PierceAdd;
                spec.DigPower += def.DigPowerAdd;
            }

            public void OnHitWorld(ref ProjectileSpec spec, in ProjectileHit hit) =>
                inner.OnHitWorld(ref spec, hit);
        }
    }
}
