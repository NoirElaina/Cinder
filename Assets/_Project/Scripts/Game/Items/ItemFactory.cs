using Cinder.Core.Attributes;
using Cinder.Game.Characters;
using Cinder.Game.Spells;
using UnityEngine;

namespace Cinder.Game.Items
{
    /// <summary>纯代码构建的演示物品，零资产验证装备热插拔。</summary>
    public static class ItemFactory
    {
        /// <summary>迅捷戒指：移速 x1.5、跳跃 +6。</summary>
        public static ItemDefinition CreateDemoRing()
        {
            var ring = ScriptableObject.CreateInstance<ItemDefinition>();
            ring.ModuleId = "item.swift_ring";
            ring.DisplayName = "迅捷戒指";
            ring.EquipSlot = "charm1";
            ring.Modifiers = new[]
            {
                new AttributeModifierEntry
                {
                    Attribute = CharacterAttributes.MoveSpeed,
                    Op = ModifierOp.Multiply,
                    Value = 1.5f,
                },
                new AttributeModifierEntry
                {
                    Attribute = CharacterAttributes.JumpStrength,
                    Op = ModifierOp.Add,
                    Value = 6f,
                },
            };
            return ring;
        }

        /// <summary>聚能核心：法力上限 x1.5、法力回复 +30。</summary>
        public static ItemDefinition CreateDemoCore()
        {
            var core = ScriptableObject.CreateInstance<ItemDefinition>();
            core.ModuleId = "item.mana_core";
            core.DisplayName = "聚能核心";
            core.EquipSlot = "charm2";
            core.Modifiers = new[]
            {
                new AttributeModifierEntry
                {
                    Attribute = WandAttributes.ManaMax,
                    Op = ModifierOp.Multiply,
                    Value = 1.5f,
                },
                new AttributeModifierEntry
                {
                    Attribute = WandAttributes.ManaRegen,
                    Op = ModifierOp.Add,
                    Value = 30f,
                },
            };
            return core;
        }
    }
}
