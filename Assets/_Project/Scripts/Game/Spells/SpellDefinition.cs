using UnityEngine;

namespace Cinder.Game.Spells
{
    public enum SpellKind : byte
    {
        Projectile = 0,
        Modifier = 1,
        Trigger = 2,
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
}
