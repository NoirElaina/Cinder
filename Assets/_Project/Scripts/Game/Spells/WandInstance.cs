using System;
using System.Collections.Generic;
using Cinder.Core.Attributes;
using Cinder.Game.Effects;

namespace Cinder.Game.Spells
{
    /// <summary>一次施法产出的一个投射物：最终参数 + 命中行为链。</summary>
    public struct CastResult
    {
        public ProjectileSpec Spec;
        public IProjectileBehavior Behavior;

        /// <summary>相对瞄准方向的扇形偏转角（度），多重施法产生。</summary>
        public float AngleOffset;
    }

    /// <summary>
    /// 法杖运行时实例。施法管线（精简 Noita 模型）：
    /// 按法术槽顺序折叠——修饰符累积，投射物法术把当前累积的修饰符
    /// 装饰链应用到自身参数上并发射；一次施法消耗全部法术的法力。
    /// 法术槽 SetSpell/RemoveSpell 即热插拔。
    /// </summary>
    public sealed class WandInstance
    {
        readonly List<SpellDefinition> slots = new List<SpellDefinition>();

        float cooldownLeft;
        float rechargeLeft;

        public WandInstance(WandDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Attributes = new AttributeSet();
            Attributes.GetOrAdd(WandAttributes.CastDelay).SetBase(definition.CastDelay);
            Attributes.GetOrAdd(WandAttributes.RechargeTime).SetBase(definition.RechargeTime);
            Attributes.GetOrAdd(WandAttributes.ManaMax).SetBase(definition.ManaMax);
            Attributes.GetOrAdd(WandAttributes.ManaRegen).SetBase(definition.ManaRegen);
            CurrentMana = definition.ManaMax;
            foreach (SpellDefinition spell in definition.DefaultSpells)
                if (spell != null && slots.Count < definition.Capacity) slots.Add(spell);
        }

        public WandDefinition Definition { get; }

        /// <summary>法杖属性（施法延迟/充能/法力），可被修饰符热改。</summary>
        public AttributeSet Attributes { get; }

        public IReadOnlyList<SpellDefinition> Spells => slots;

        public float CurrentMana { get; private set; }

        public bool CanCast => cooldownLeft <= 0f && rechargeLeft <= 0f;

        /// <summary>法术槽变化（热插拔）时触发。</summary>
        public event Action SpellsChanged;

        /// <summary>每帧推进冷却/充能/法力回复。</summary>
        public void Tick(float deltaTime)
        {
            if (cooldownLeft > 0f) cooldownLeft -= deltaTime;
            if (rechargeLeft > 0f) rechargeLeft -= deltaTime;
            float max = Attributes.GetValue(WandAttributes.ManaMax, Definition.ManaMax);
            float regen = Attributes.GetValue(WandAttributes.ManaRegen, Definition.ManaRegen);
            CurrentMana = Math.Min(max, CurrentMana + regen * deltaTime);
        }

        /// <summary>热插拔：替换指定槽位的法术（越界时追加，满员返回 false）。</summary>
        public bool SetSpell(int index, SpellDefinition spell)
        {
            if (index < 0) return false;
            if (index < slots.Count)
            {
                slots[index] = spell;
                SpellsChanged?.Invoke();
                return true;
            }
            if (slots.Count >= Definition.Capacity) return false;
            slots.Add(spell);
            SpellsChanged?.Invoke();
            return true;
        }

        /// <summary>热插拔：移除指定槽位的法术。</summary>
        public bool RemoveSpell(int index)
        {
            if (index < 0 || index >= slots.Count) return false;
            slots.RemoveAt(index);
            SpellsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 尝试施法。成功时 results 填入本次全部投射物并扣法力/进入冷却；
        /// 失败（冷却中/法力不足/无投射物法术）返回 false 且无副作用。
        /// </summary>
        public bool TryCast(List<CastResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (!CanCast) return false;

            float manaCost = 0f;
            IProjectileBehavior chain = BaseProjectileBehavior.Instance;
            var pending = new List<CastResult>(slots.Count);
            int multicast = 0;
            float multicastSpread = 0f;

            foreach (SpellDefinition spell in slots)
            {
                if (spell == null) continue;
                manaCost += spell.ManaCost;
                if (spell is MulticastSpellDefinition multi)
                {
                    multicast += multi.Count;
                    multicastSpread = UnityEngine.Mathf.Max(multicastSpread, multi.SpreadStep);
                }
                else if (spell is ModifierSpellDefinition modifier)
                {
                    chain = modifier.Decorate(chain);
                }
                else if (spell is TriggerSpellDefinition trigger && trigger.Payload != null)
                {
                    ProjectileSpec carrier = trigger.CarrierSpec;
                    chain.ModifySpec(ref carrier);
                    carrier.TriggerPayload = trigger.Payload;
                    pending.Add(new CastResult { Spec = carrier, Behavior = chain });
                }
                else if (spell is ProjectileSpellDefinition projectile)
                {
                    ProjectileSpec spec = projectile.BaseSpec;
                    chain.ModifySpec(ref spec);
                    int copies = System.Math.Max(1, multicast);
                    multicast = 0; // 只作用于紧随的投射物法术
                    for (int i = 0; i < copies; i++)
                    {
                        float offset = copies == 1
                            ? 0f
                            : (i - (copies - 1) * 0.5f) * multicastSpread;
                        pending.Add(new CastResult
                        {
                            Spec = spec,
                            Behavior = chain,
                            AngleOffset = offset,
                        });
                    }
                }
            }

            if (pending.Count == 0) return false;
            if (CurrentMana < manaCost) return false;

            CurrentMana -= manaCost;
            cooldownLeft = Attributes.GetValue(WandAttributes.CastDelay, Definition.CastDelay);
            rechargeLeft = Attributes.GetValue(WandAttributes.RechargeTime, Definition.RechargeTime);
            results.AddRange(pending);
            return true;
        }
    }

    public static class WandAttributes
    {
        public const string CastDelay = "wand.cast_delay";
        public const string RechargeTime = "wand.recharge_time";
        public const string ManaMax = "wand.mana_max";
        public const string ManaRegen = "wand.mana_regen";
    }
}
