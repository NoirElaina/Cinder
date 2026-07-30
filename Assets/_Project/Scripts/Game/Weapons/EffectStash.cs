using System;
using System.Collections.Generic;
using Cinder.Game.Effects;

namespace Cinder.Game.Weapons
{
    /// <summary>
    /// 效果背包：玩家在世界中拾取到的投射物效果（ProjectileEffectDefinition）。
    /// 与 Inventory（物品）分离，是画布节点图的拖拽源、拾取的目标。增删即热插拔，带事件。
    /// </summary>
    public sealed class EffectStash
    {
        readonly List<ProjectileEffectDefinition> effects = new List<ProjectileEffectDefinition>();

        public int Capacity { get; set; } = 32;

        public int Count => effects.Count;

        public IReadOnlyList<ProjectileEffectDefinition> All => effects;

        public event Action<ProjectileEffectDefinition> Added;
        public event Action<ProjectileEffectDefinition> Removed;

        /// <summary>放入一个效果（允许重复拾取同一效果资产）。</summary>
        public bool Add(ProjectileEffectDefinition effect)
        {
            if (effect == null || effects.Count >= Capacity) return false;
            effects.Add(effect);
            Added?.Invoke(effect);
            return true;
        }

        /// <summary>取出一个效果（画布拖成节点时消耗；删除节点时通常还回）。</summary>
        public bool Remove(ProjectileEffectDefinition effect)
        {
            if (effect == null || !effects.Remove(effect)) return false;
            Removed?.Invoke(effect);
            return true;
        }

        public bool Contains(ProjectileEffectDefinition effect) => effects.Contains(effect);
    }
}
