using Cinder.Game.Effects;
using UnityEngine;

namespace Cinder.Game.Spells
{
    public enum SpellKind : byte
    {
        Projectile = 0,
        Modifier = 1,
    }

    /// <summary>
    /// 法术模块基类。法术也是 IModule，与武器/效果/角色共用注册表机制。
    /// </summary>
    public abstract class SpellDefinition : ScriptableObject, Core.Modules.IModule
    {
        [SerializeField] string moduleId = "spell.unnamed";
        [SerializeField] string displayName = "Unnamed Spell";

        public string ModuleId { get => moduleId; set => moduleId = value; }
        public string DisplayName { get => displayName; set => displayName = value; }

        [Min(0f)] public float ManaCost = 5f;

        public abstract SpellKind Kind { get; }
    }

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
