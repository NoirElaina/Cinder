using System;
using System.Collections.Generic;
using Cinder.Core.Attributes;
using Cinder.Game.Effects;

namespace Cinder.Game.Weapons
{
    /// <summary>
    /// 武器运行时实例：属性集 + 可变效果列表。AddEffect/RemoveEffect 即热插拔，
    /// 下一次 ComposeSpec/BuildBehaviorChain 自动反映新组合（装饰链按需重建，
    /// 无缓存失效问题）。
    /// </summary>
    public sealed class WeaponInstance
    {
        readonly List<ProjectileEffectDefinition> effects =
            new List<ProjectileEffectDefinition>();

        public WeaponInstance(WeaponDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Attributes = new AttributeSet();
            Attributes.GetOrAdd(WeaponAttributes.FireRate).SetBase(definition.FireRate);
            Attributes.GetOrAdd(WeaponAttributes.ManaCost).SetBase(definition.ManaCost);
            foreach (ProjectileEffectDefinition effect in definition.DefaultEffects)
                if (effect != null) effects.Add(effect);
        }

        public WeaponDefinition Definition { get; }

        /// <summary>武器属性（射速/耗魔…），可被增益/装备修饰。</summary>
        public AttributeSet Attributes { get; }

        public IReadOnlyList<ProjectileEffectDefinition> Effects => effects;

        /// <summary>效果列表发生变化（热插拔）时触发。</summary>
        public event Action EffectsChanged;

        /// <summary>运行时挂载一个效果（热插拔）。</summary>
        public bool AddEffect(ProjectileEffectDefinition effect)
        {
            if (effect == null || effects.Contains(effect)) return false;
            effects.Add(effect);
            EffectsChanged?.Invoke();
            return true;
        }

        /// <summary>运行时卸载一个效果（热插拔）。</summary>
        public bool RemoveEffect(ProjectileEffectDefinition effect)
        {
            if (effect == null || !effects.Remove(effect)) return false;
            EffectsChanged?.Invoke();
            return true;
        }

        /// <summary>按当前效果列表重建装饰链：列表顺序 = 生效顺序。</summary>
        public IProjectileBehavior BuildBehaviorChain()
        {
            IProjectileBehavior behavior = BaseProjectileBehavior.Instance;
            foreach (ProjectileEffectDefinition effect in effects)
                behavior = effect.Decorate(behavior);
            return behavior;
        }

        /// <summary>计算当前组合下的最终投射物参数。</summary>
        public ProjectileSpec ComposeSpec()
        {
            ProjectileSpec spec = Definition.BaseSpec;
            BuildBehaviorChain().ModifySpec(ref spec);
            return spec;
        }
    }

    public static class WeaponAttributes
    {
        public const string FireRate = "weapon.fire_rate";
        public const string ManaCost = "weapon.mana_cost";
    }
}
