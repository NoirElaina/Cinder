using Cinder.Game.Effects;
using UnityEngine;

namespace Cinder.Game.Spells
{
    /// <summary>投射物法术：法杖施法管线的终点，产出一个投射物。</summary>
    [CreateAssetMenu(menuName = "Cinder/Spells/Projectile Spell")]
    public sealed class ProjectileSpellDefinition : SpellDefinition
    {
        public override SpellKind Kind => SpellKind.Projectile;

        public ProjectileSpec BaseSpec = new ProjectileSpec
        {
            Damage = 10f,
            Speed = 80f,
            Gravity = 20f,
            Lifetime = 2f,
            DigPower = 0,
            Pierce = 0,
            TrailMaterial = 0,
            Tint = new Color32(255, 230, 120, 255),
        };
    }
}
