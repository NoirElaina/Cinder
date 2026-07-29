using System;
using System.Collections.Generic;
using Cinder.Core.Attributes;

namespace Cinder.Game.Items
{
    /// <summary>
    /// 装备系统：槽位 -> 物品。装备时把物品的修饰符挂到目标属性集
    /// （Source = 物品本身），卸下时按 Source 移除，同槽互斥自动换卸。
    /// 属性路由由外部解析器决定（如 "wand." 前缀路由到法杖）。
    /// </summary>
    public sealed class Equipment
    {
        readonly Dictionary<string, ItemDefinition> slots =
            new Dictionary<string, ItemDefinition>();
        readonly Func<string, AttributeSet> attributeSetResolver;

        public Equipment(Func<string, AttributeSet> attributeSetResolver)
        {
            this.attributeSetResolver = attributeSetResolver
                ?? throw new ArgumentNullException(nameof(attributeSetResolver));
        }

        /// <summary>参数为 (槽位, 物品)，卸下时物品为 null。</summary>
        public event Action<string, ItemDefinition> SlotChanged;

        public ItemDefinition Get(string slot) =>
            slot != null && slots.TryGetValue(slot, out ItemDefinition item) ? item : null;

        public IEnumerable<string> Slots => slots.Keys;

        public bool Equip(ItemDefinition item)
        {
            if (item == null || string.IsNullOrEmpty(item.EquipSlot)) return false;
            string slot = item.EquipSlot;
            if (slots.TryGetValue(slot, out ItemDefinition current) && ReferenceEquals(current, item))
                return false;
            if (current != null) Unequip(slot);

            slots[slot] = item;
            foreach (AttributeModifierEntry entry in item.Modifiers)
                attributeSetResolver(entry.Attribute)
                    ?.GetOrAdd(entry.Attribute).AddModifier(entry.ToModifier(item));
            SlotChanged?.Invoke(slot, item);
            return true;
        }

        public bool Unequip(string slot)
        {
            if (slot == null || !slots.TryGetValue(slot, out ItemDefinition item)) return false;
            slots.Remove(slot);
            foreach (AttributeModifierEntry entry in item.Modifiers)
                attributeSetResolver(entry.Attribute)?.GetOrAdd(entry.Attribute)
                    .RemoveModifiersFromSource(item);
            SlotChanged?.Invoke(slot, null);
            return true;
        }
    }
}
