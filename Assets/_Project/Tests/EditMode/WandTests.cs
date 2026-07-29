using System.Collections.Generic;
using Cinder.Game.Effects;
using Cinder.Game.Spells;
using NUnit.Framework;
using UnityEngine;

namespace Cinder.Tests
{
    public class WandTests
    {
        StatModifierEffect plusFive;
        ModifierSpellDefinition modifier;
        ProjectileSpellDefinition bolt;
        ProjectileSpellDefinition bolt2;
        WandDefinition wandDef;
        readonly List<CastResult> results = new List<CastResult>();

        [SetUp]
        public void SetUp()
        {
            plusFive = ScriptableObject.CreateInstance<StatModifierEffect>();
            plusFive.ModuleId = "effect.plus_five";
            plusFive.DamageAdd = 5f;

            modifier = ScriptableObject.CreateInstance<ModifierSpellDefinition>();
            modifier.ModuleId = "spell.mod_plus_five";
            modifier.ManaCost = 2f;
            modifier.Effect = plusFive;

            bolt = ScriptableObject.CreateInstance<ProjectileSpellDefinition>();
            bolt.ModuleId = "spell.bolt";
            bolt.ManaCost = 5f;
            bolt.BaseSpec = new ProjectileSpec { Damage = 10f, Speed = 80f };

            bolt2 = ScriptableObject.CreateInstance<ProjectileSpellDefinition>();
            bolt2.ModuleId = "spell.bolt2";
            bolt2.ManaCost = 3f;
            bolt2.BaseSpec = new ProjectileSpec { Damage = 20f, Speed = 60f };

            wandDef = ScriptableObject.CreateInstance<WandDefinition>();
            wandDef.ModuleId = "wand.test";
            wandDef.CastDelay = 0.2f;
            wandDef.RechargeTime = 0.5f;
            wandDef.ManaMax = 100f;
            wandDef.ManaRegen = 0f; // 测试里关掉回复，数值可预测
            wandDef.Capacity = 6;
            wandDef.DefaultSpells.Add(modifier);
            wandDef.DefaultSpells.Add(bolt);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(plusFive);
            Object.DestroyImmediate(modifier);
            Object.DestroyImmediate(bolt);
            Object.DestroyImmediate(bolt2);
            Object.DestroyImmediate(wandDef);
        }

        [Test]
        public void Cast_AppliesModifiersAndConsumesMana()
        {
            var wand = new WandInstance(wandDef);
            Assert.IsTrue(wand.TryCast(results));
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(15f, results[0].Spec.Damage, "修饰符应叠加到投射物上");
            Assert.AreEqual(93f, wand.CurrentMana, "应扣除全部法术法力 2+5");
        }

        [Test]
        public void Cast_BlockedByCooldownAndRecharge()
        {
            var wand = new WandInstance(wandDef);
            Assert.IsTrue(wand.TryCast(results));
            Assert.IsFalse(wand.CanCast);
            Assert.IsFalse(wand.TryCast(results), "冷却/充能中不能再施法");

            wand.Tick(0.25f); // 过了施法延迟但还在充能
            Assert.IsFalse(wand.CanCast);
            wand.Tick(0.3f); // 充能结束
            Assert.IsTrue(wand.CanCast);
            Assert.IsTrue(wand.TryCast(results));
        }

        [Test]
        public void Cast_FailsWhenManaInsufficient()
        {
            wandDef.ManaMax = 6f; // 总耗 7 > 6
            var wand = new WandInstance(wandDef);
            Assert.IsFalse(wand.TryCast(results));
            Assert.AreEqual(0, results.Count);
            Assert.IsTrue(wand.CanCast, "施法失败不应进入冷却");
        }

        [Test]
        public void Cast_FailsWithoutProjectileSpell()
        {
            wandDef.DefaultSpells.Clear();
            wandDef.DefaultSpells.Add(modifier);
            var wand = new WandInstance(wandDef);
            Assert.IsFalse(wand.TryCast(results), "只有修饰符没有投射物时不算施法");
        }

        [Test]
        public void Modifiers_ApplyToAllFollowingProjectiles()
        {
            wandDef.DefaultSpells.Add(bolt2);
            var wand = new WandInstance(wandDef);
            Assert.IsTrue(wand.TryCast(results));
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(15f, results[0].Spec.Damage);
            Assert.AreEqual(25f, results[1].Spec.Damage, "同一施法块内修饰符作用于全部后续投射物");
        }

        [Test]
        public void HotSwap_ChangesNextCast()
        {
            var wand = new WandInstance(wandDef);
            int changes = 0;
            wand.SpellsChanged += () => changes++;

            Assert.IsTrue(wand.RemoveSpell(0)); // 移除修饰符
            Assert.IsTrue(wand.TryCast(results));
            Assert.AreEqual(10f, results[0].Spec.Damage, "移除修饰符后应回到基础伤害");

            wand.Tick(0.6f); // 推进冷却与充能，准备下一次施法
            // SetSpell 是替换语义：先替换 0 槽为修饰符，再把投射物追加回 1 槽
            Assert.IsTrue(wand.SetSpell(0, modifier));
            Assert.IsTrue(wand.SetSpell(1, bolt));
            Assert.IsTrue(wand.TryCast(results));
            Assert.AreEqual(15f, results[0].Spec.Damage);
            Assert.AreEqual(3, changes);
        }

        [Test]
        public void SetSpell_RespectsCapacity()
        {
            var wand = new WandInstance(wandDef);
            Assert.IsTrue(wand.SetSpell(2, bolt2)); // 追加到空槽
            Assert.IsTrue(wand.SetSpell(3, bolt2));
            Assert.IsTrue(wand.SetSpell(4, bolt2));
            Assert.IsTrue(wand.SetSpell(5, bolt2)); // 满员 (Capacity=6)
            Assert.IsFalse(wand.SetSpell(6, bolt2), "超出容量应被拒绝");
        }
    }
}
