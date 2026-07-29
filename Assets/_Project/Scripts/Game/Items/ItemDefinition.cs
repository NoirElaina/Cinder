using Cinder.Core.Attributes;
using Cinder.Core.Modules;
using UnityEngine;

namespace Cinder.Game.Items
{
    /// <summary>物品属性修饰条目（可在 Inspector 里编辑）。</summary>
    [System.Serializable]
    public struct AttributeModifierEntry
    {
        [Tooltip("目标属性 Id，wand. 前缀路由到法杖属性，其余路由到角色属性")]
        public string Attribute;
        public ModifierOp Op;
        public float Value;

        public AttributeModifier ToModifier(object source) =>
            new AttributeModifier(Op, Value, source);
    }

    /// <summary>
    /// 物品模块：装备后向角色/法杖属性集挂修饰符（Source = 本物品），
    /// 卸下时按 Source 整体移除——物品属性热插拔即装备/卸下。
    /// </summary>
    [CreateAssetMenu(menuName = "Cinder/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject, IModule
    {
        [SerializeField] string moduleId = "item.unnamed";
        [SerializeField] string displayName = "Unnamed Item";

        public string ModuleId { get => moduleId; set => moduleId = value; }
        public string DisplayName { get => displayName; set => displayName = value; }

        [Tooltip("装备槽名，同槽物品互斥")]
        public string EquipSlot = "charm1";

        public AttributeModifierEntry[] Modifiers = new AttributeModifierEntry[0];
    }
}
