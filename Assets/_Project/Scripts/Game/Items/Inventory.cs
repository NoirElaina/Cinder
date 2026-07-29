using System;
using System.Collections.Generic;

namespace Cinder.Game.Items
{
    /// <summary>背包：持有物品模块，增删即热插拔，带事件。</summary>
    public sealed class Inventory
    {
        readonly List<ItemDefinition> items = new List<ItemDefinition>();

        public Inventory(int capacity = 12)
        {
            Capacity = capacity;
        }

        public int Capacity { get; set; }

        public int Count => items.Count;

        public IReadOnlyList<ItemDefinition> All => items;

        public event Action<ItemDefinition> Added;
        public event Action<ItemDefinition> Removed;

        public bool Add(ItemDefinition item)
        {
            if (item == null || items.Count >= Capacity || items.Contains(item)) return false;
            items.Add(item);
            Added?.Invoke(item);
            return true;
        }

        public bool Remove(ItemDefinition item)
        {
            if (item == null || !items.Remove(item)) return false;
            Removed?.Invoke(item);
            return true;
        }

        public bool Contains(ItemDefinition item) => items.Contains(item);
    }
}
