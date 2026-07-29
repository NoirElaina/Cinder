using System.Collections.Generic;
using Cinder.Core.Modules;
using UnityEngine;

namespace Cinder.Game.Spells
{
    /// <summary>
    /// 法杖模块（精简版 Noita 法杖）：施法延迟、充能时间、法力、容量、
    /// 默认法术列表。法术槽运行时可热插拔（见 WandInstance）。
    /// </summary>
    [CreateAssetMenu(menuName = "Cinder/Wand Definition")]
    public sealed class WandDefinition : ScriptableObject, IModule
    {
        [SerializeField] string moduleId = "wand.unnamed";
        [SerializeField] string displayName = "Unnamed Wand";

        public string ModuleId { get => moduleId; set => moduleId = value; }
        public string DisplayName { get => displayName; set => displayName = value; }

        [Tooltip("两次施法之间的间隔（秒）")]
        [Min(0f)] public float CastDelay = 0.2f;

        [Tooltip("一次施法后的充能时间（秒），与施法延迟并行计时")]
        [Min(0f)] public float RechargeTime = 0.4f;

        [Min(1f)] public float ManaMax = 100f;
        [Min(0f)] public float ManaRegen = 20f;

        [Min(1)] public int Capacity = 6;

        public List<SpellDefinition> DefaultSpells = new List<SpellDefinition>();
    }
}
