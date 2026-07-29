using Cinder.Game.Effects;
using UnityEngine;

namespace Cinder.Game.Spells
{
    /// <summary>
    /// 触发法术：作为投射物发射，命中世界时在命中点释放 Payload 法术。
    /// Payload 只释放一层，不递归触发。
    /// </summary>
    [CreateAssetMenu(menuName = "Cinder/Spells/Trigger Spell")]
    public sealed class TriggerSpellDefinition : SpellDefinition
    {
        public override SpellKind Kind => SpellKind.Projectile;

        public ProjectileSpellDefinition Payload;

        [Tooltip("触发弹自身的飞行参数（伤害通常为 0，靠 Payload 输出）")]
        public ProjectileSpec CarrierSpec = new ProjectileSpec
        {
            Damage = 0f,
            Speed = 60f,
            Gravity = 20f,
            Lifetime = 3f,
            DigPower = 0,
            Pierce = 0,
            TrailMaterial = 0,
            Tint = new Color32(180, 220, 255, 255),
        };
    }
}
