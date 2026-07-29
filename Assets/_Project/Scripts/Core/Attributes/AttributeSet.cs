using System;
using System.Collections.Generic;

namespace Cinder.Core.Attributes
{
    public enum ModifierOp : byte
    {
        /// <summary>加算：Value += 值。</summary>
        Add = 0,
        /// <summary>乘算：Value *= 值。</summary>
        Multiply = 1,
        /// <summary>覆盖：Value = 值（多个覆盖时后注册者胜）。</summary>
        Override = 2,
    }

    /// <summary>
    /// 属性修饰符（装饰器思路的数据版）。Source 用于按来源整体移除，
    /// 例如卸下一件装备时移除它贡献的全部修饰。
    /// </summary>
    public readonly struct AttributeModifier
    {
        public readonly ModifierOp Op;
        public readonly float Value;
        public readonly object Source;

        public AttributeModifier(ModifierOp op, float value, object source = null)
        {
            Op = op;
            Value = value;
            Source = source;
        }
    }

    /// <summary>
    /// 单个属性：基础值 + 修饰符栈。求值顺序固定为 加算 -> 乘算 -> 覆盖，
    /// 与修饰符注册先后无关，结果可预测。
    /// </summary>
    public sealed class Attribute
    {
        float baseValue;
        readonly List<AttributeModifier> modifiers = new List<AttributeModifier>();

        public float Value { get; private set; }

        /// <summary>参数为新的最终值。</summary>
        public event Action<float> Changed;

        public Attribute(float baseValue = 0f)
        {
            this.baseValue = baseValue;
            Value = baseValue;
        }

        public float BaseValue => baseValue;

        public IReadOnlyList<AttributeModifier> Modifiers => modifiers;

        public void SetBase(float value)
        {
            baseValue = value;
            Recalculate();
        }

        public void AddModifier(in AttributeModifier modifier)
        {
            modifiers.Add(modifier);
            Recalculate();
        }

        /// <summary>移除指定来源的全部修饰符，返回移除数量。</summary>
        public int RemoveModifiersFromSource(object source)
        {
            if (source == null) return 0;
            int removed = modifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
            if (removed > 0) Recalculate();
            return removed;
        }

        void Recalculate()
        {
            float add = 0f;
            float mul = 1f;
            float? over = null;
            foreach (AttributeModifier m in modifiers)
            {
                switch (m.Op)
                {
                    case ModifierOp.Add: add += m.Value; break;
                    case ModifierOp.Multiply: mul *= m.Value; break;
                    case ModifierOp.Override: over = m.Value; break;
                }
            }

            float next = over ?? (baseValue + add) * mul;
            if (Math.Abs(next - Value) < 1e-6f) return;
            Value = next;
            Changed?.Invoke(next);
        }
    }

    /// <summary>按字符串 Id 管理的属性集合，角色/武器/物品通用。</summary>
    public sealed class AttributeSet
    {
        readonly Dictionary<string, Attribute> attributes = new Dictionary<string, Attribute>();

        public int Count => attributes.Count;

        public IEnumerable<string> Ids => attributes.Keys;

        public Attribute GetOrAdd(string id)
        {
            if (!attributes.TryGetValue(id, out Attribute attribute))
            {
                attribute = new Attribute();
                attributes.Add(id, attribute);
            }
            return attribute;
        }

        public Attribute Get(string id) =>
            id != null && attributes.TryGetValue(id, out Attribute attribute) ? attribute : null;

        public float GetValue(string id, float fallback = 0f)
        {
            Attribute attribute = Get(id);
            return attribute != null ? attribute.Value : fallback;
        }

        /// <summary>按来源移除所有属性上的修饰符（卸装备/清增益用），返回总移除数。</summary>
        public int RemoveModifiersFromSource(object source)
        {
            int total = 0;
            foreach (Attribute attribute in attributes.Values)
                total += attribute.RemoveModifiersFromSource(source);
            return total;
        }
    }
}
