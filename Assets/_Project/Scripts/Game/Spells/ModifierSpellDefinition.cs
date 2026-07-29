using Cinder.Game.Effects;
using UnityEngine;

namespace Cinder.Game.Spells
{
    /// <summary>
    /// 修饰符法术：包装一个投射物效果，施法时叠加到其后所有投射物上。
    /// 直接复用效果装饰链，效果本身也可独立热插拔。
    /// </summary>
    [CreateAssetMenu(menuName = "Cinder/Spells/Modifier Spell")]
    public sealed class ModifierSpellDefinition : SpellDefinition
    {
        public override SpellKind Kind => SpellKind.Modifier;

        public ProjectileEffectDefinition Effect;

        public IProjectileBehavior Decorate(IProjectileBehavior inner) =>
            Effect != null ? Effect.Decorate(inner) : inner;
    }
}
