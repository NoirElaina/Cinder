using Cinder.Core.Modules;
using UnityEngine;

namespace Cinder.Game.Characters
{
    /// <summary>角色模块：基础属性模板。运行时是 Character（FSM + 属性集）。</summary>
    [CreateAssetMenu(menuName = "Cinder/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject, IModule
    {
        [SerializeField] string moduleId = "character.unnamed";
        [SerializeField] string displayName = "Unnamed Character";

        public string ModuleId { get => moduleId; set => moduleId = value; }
        public string DisplayName { get => displayName; set => displayName = value; }

        [Min(1f)] public float MaxHealth = 100f;
        [Min(0f)] public float MoveSpeed = 8f;
        [Min(0f)] public float JumpStrength = 12f;
    }
}
