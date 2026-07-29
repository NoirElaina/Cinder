using System.Collections.Generic;
using Cinder.Core.Attributes;
using Cinder.Core.Modules;
using Cinder.Core.StateMachine;
using Cinder.Game.Characters;
using Cinder.Game.Effects;
using Cinder.Game.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace Cinder.Tests
{
    public class ModuleRegistryTests
    {
        sealed class FakeModule : IModule
        {
            public string ModuleId { get; set; }
        }

        [Test]
        public void RegisterUnregister_EventsAndLookup()
        {
            var registry = new ModuleRegistry<FakeModule>();
            var events = new List<string>();
            registry.Registered += m => events.Add("+" + m.ModuleId);
            registry.Unregistered += m => events.Add("-" + m.ModuleId);

            var a = new FakeModule { ModuleId = "test.a" };
            var b = new FakeModule { ModuleId = "test.b" };

            Assert.IsTrue(registry.Register(a));
            Assert.IsTrue(registry.Register(b));
            Assert.IsFalse(registry.Register(a), "重复 Id 应被拒绝");
            Assert.IsFalse(registry.Register(new FakeModule { ModuleId = null }));

            Assert.AreEqual(2, registry.Count);
            Assert.AreSame(a, registry.Get("test.a"));
            Assert.IsTrue(registry.Contains("test.b"));

            Assert.IsTrue(registry.Unregister("test.a"));
            Assert.IsFalse(registry.Unregister("test.a"));
            Assert.IsNull(registry.Get("test.a"));
            Assert.AreEqual(1, registry.Count);

            CollectionAssert.AreEqual(new[] { "+test.a", "+test.b", "-test.a" }, events);
        }

        [Test]
        public void Clear_UnregistersAll()
        {
            var registry = new ModuleRegistry<FakeModule>();
            int removed = 0;
            registry.Unregistered += _ => removed++;
            registry.Register(new FakeModule { ModuleId = "x" });
            registry.Register(new FakeModule { ModuleId = "y" });
            registry.Clear();
            Assert.AreEqual(0, registry.Count);
            Assert.AreEqual(2, removed);
        }
    }

    public class AttributeSetTests
    {
        [Test]
        public void EvaluationOrder_AddThenMultiplyThenOverride()
        {
            var attribute = new Attribute(10f);
            var buff = new object();
            attribute.AddModifier(new AttributeModifier(ModifierOp.Multiply, 2f, buff));
            attribute.AddModifier(new AttributeModifier(ModifierOp.Add, 5f, buff));
            Assert.AreEqual(30f, attribute.Value, "应为 (10+5)*2，与注册顺序无关");

            attribute.AddModifier(new AttributeModifier(ModifierOp.Override, 99f, buff));
            Assert.AreEqual(99f, attribute.Value);

            attribute.RemoveModifiersFromSource(buff);
            Assert.AreEqual(10f, attribute.Value, "按来源移除后应回到基础值");
        }

        [Test]
        public void Changed_FiresWithNewValue()
        {
            var attribute = new Attribute(1f);
            var seen = new List<float>();
            attribute.Changed += v => seen.Add(v);
            attribute.SetBase(2f);
            attribute.AddModifier(new AttributeModifier(ModifierOp.Add, 3f));
            CollectionAssert.AreEqual(new[] { 2f, 5f }, seen);
        }

        [Test]
        public void AttributeSet_RemoveBySourceAcrossAttributes()
        {
            var set = new AttributeSet();
            var ring = new object();
            set.GetOrAdd("hp").SetBase(100f);
            set.GetOrAdd("speed").SetBase(8f);
            set.GetOrAdd("hp").AddModifier(new AttributeModifier(ModifierOp.Add, 50f, ring));
            set.GetOrAdd("speed").AddModifier(new AttributeModifier(ModifierOp.Multiply, 1.5f, ring));

            Assert.AreEqual(150f, set.GetValue("hp"));
            Assert.AreEqual(12f, set.GetValue("speed"));

            Assert.AreEqual(2, set.RemoveModifiersFromSource(ring));
            Assert.AreEqual(100f, set.GetValue("hp"));
            Assert.AreEqual(8f, set.GetValue("speed"));
        }
    }

    public class StateMachineTests
    {
        sealed class RecordingState : IState<int>
        {
            public string Name { get; set; }
            public List<string> Log { get; } = new List<string>();
            public void Enter(int context) => Log.Add("enter" + context);
            public void Exit(int context) => Log.Add("exit" + context);
            public void Tick(int context, float dt) => Log.Add("tick");
        }

        [Test]
        public void Transitions_EnterExitPaired()
        {
            var fsm = new StateMachine<int>(7);
            var a = new RecordingState { Name = "A" };
            var b = new RecordingState { Name = "B" };
            fsm.Register(a);
            fsm.Register(b);

            Assert.IsTrue(fsm.ChangeTo("A"));
            Assert.IsTrue(fsm.IsIn("A"));
            Assert.IsTrue(fsm.ChangeTo("B"));
            fsm.Tick(0.1f);

            CollectionAssert.AreEqual(new[] { "enter7", "exit7" }, a.Log);
            CollectionAssert.AreEqual(new[] { "enter7", "tick" }, b.Log);
        }

        [Test]
        public void ChangeTo_UnknownOrSame_BehavesSanely()
        {
            var fsm = new StateMachine<int>(0);
            var a = new RecordingState { Name = "A" };
            fsm.Register(a);

            Assert.IsFalse(fsm.ChangeTo("nope"), "未注册状态应切换失败");
            Assert.IsNull(fsm.Current);

            fsm.ChangeTo("A");
            Assert.IsTrue(fsm.ChangeTo("A"), "切换到当前状态应为空操作");
            Assert.AreEqual(1, a.Log.Count, "空操作不应重复 Enter/Exit");
        }
    }

    public class EffectChainTests
    {
        StatModifierEffect plusFive;
        StatModifierEffect timesTwo;

        [SetUp]
        public void SetUp()
        {
            plusFive = ScriptableObject.CreateInstance<StatModifierEffect>();
            plusFive.ModuleId = "effect.plus_five";
            plusFive.DamageAdd = 5f;
            timesTwo = ScriptableObject.CreateInstance<StatModifierEffect>();
            timesTwo.ModuleId = "effect.times_two";
            timesTwo.DamageMultiply = 2f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(plusFive);
            Object.DestroyImmediate(timesTwo);
        }

        [Test]
        public void Chain_AppliesInListOrder()
        {
            IProjectileBehavior chain = BaseProjectileBehavior.Instance;
            chain = plusFive.Decorate(chain);
            chain = timesTwo.Decorate(chain);

            var spec = new ProjectileSpec { Damage = 10f };
            chain.ModifySpec(ref spec);
            Assert.AreEqual(30f, spec.Damage, "先 +5 再 x2：(10+5)*2");
        }

        [Test]
        public void OnHitWorld_PropagatesThroughChain()
        {
            var bomb = ScriptableObject.CreateInstance<ExplosiveEffect>();
            bomb.RadiusAdd = 4;
            try
            {
                IProjectileBehavior chain = BaseProjectileBehavior.Instance;
                chain = bomb.Decorate(chain);
                chain = plusFive.Decorate(chain); // 命中钩子应穿透外层

                var spec = new ProjectileSpec { DigPower = 1 };
                chain.OnHitWorld(ref spec, 0, 0, 0);
                Assert.AreEqual(5, spec.DigPower);
            }
            finally { Object.DestroyImmediate(bomb); }
        }
    }

    public class WeaponHotPlugTests
    {
        WeaponDefinition definition;
        StatModifierEffect plusFive;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.ModuleId = "weapon.test";
            definition.BaseSpec = new ProjectileSpec { Damage = 10f, Speed = 30f };
            plusFive = ScriptableObject.CreateInstance<StatModifierEffect>();
            plusFive.ModuleId = "effect.plus_five";
            plusFive.DamageAdd = 5f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(plusFive);
        }

        [Test]
        public void AddRemoveEffect_RecomposesSpec()
        {
            WeaponInstance weapon = WeaponFactory.Create(definition);
            Assert.AreEqual(10f, weapon.ComposeSpec().Damage);

            int changes = 0;
            weapon.EffectsChanged += () => changes++;

            Assert.IsTrue(weapon.AddEffect(plusFive));
            Assert.AreEqual(15f, weapon.ComposeSpec().Damage, "热挂载后应立刻生效");
            Assert.IsFalse(weapon.AddEffect(plusFive), "重复挂载应被拒绝");

            Assert.IsTrue(weapon.RemoveEffect(plusFive));
            Assert.AreEqual(10f, weapon.ComposeSpec().Damage, "热卸载后应还原");
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Factory_CreatesFromRegistry()
        {
            var registry = new ModuleRegistry<WeaponDefinition>();
            Assert.IsNull(WeaponFactory.Create(registry, "weapon.test"), "未注册应返回 null");
            registry.Register(definition);
            WeaponInstance weapon = WeaponFactory.Create(registry, "weapon.test");
            Assert.IsNotNull(weapon);
            Assert.AreSame(definition, weapon.Definition);
        }

        [Test]
        public void Attributes_InitializedFromDefinition()
        {
            definition.FireRate = 6f;
            definition.ManaCost = 3f;
            WeaponInstance weapon = WeaponFactory.Create(definition);
            Assert.AreEqual(6f, weapon.Attributes.GetValue(WeaponAttributes.FireRate));
            Assert.AreEqual(3f, weapon.Attributes.GetValue(WeaponAttributes.ManaCost));
        }
    }

    public class CharacterTests
    {
        CharacterDefinition definition;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definition.ModuleId = "character.test";
            definition.MaxHealth = 100f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(definition);

        [Test]
        public void NewCharacter_StartsIdleWithFullHealth()
        {
            var character = new Character(definition);
            Assert.IsTrue(character.Fsm.IsIn(CharacterStates.Idle));
            Assert.AreEqual(100f, character.CurrentHealth);
            Assert.AreEqual(100f, character.Attributes.GetValue(CharacterAttributes.MaxHealth));
        }

        [Test]
        public void Damage_ToZero_SwitchesToDead()
        {
            var character = new Character(definition);
            int died = 0;
            character.Died += () => died++;

            character.ApplyDamage(30f);
            Assert.AreEqual(70f, character.CurrentHealth);
            Assert.IsFalse(character.IsDead);

            character.ApplyDamage(100f);
            Assert.AreEqual(0f, character.CurrentHealth);
            Assert.IsTrue(character.IsDead);
            Assert.AreEqual(1, died);

            character.ApplyDamage(10f);
            character.Heal(50f);
            Assert.AreEqual(0f, character.CurrentHealth, "死亡后伤害/治疗均无效");
            Assert.AreEqual(1, died, "Died 不应重复触发");
        }

        [Test]
        public void Heal_ClampsToMaxHealth()
        {
            var character = new Character(definition);
            character.ApplyDamage(40f);
            character.Heal(999f);
            Assert.AreEqual(100f, character.CurrentHealth);
        }
    }
}
