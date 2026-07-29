using System.Collections.Generic;
using Cinder.Game.Effects;
using Cinder.Game.Spells;
using NUnit.Framework;
using UnityEngine;

namespace Cinder.Tests
{
    public class AdvancedSpellTests
    {
        MulticastSpellDefinition multicast;
        ProjectileSpellDefinition bolt;
        ProjectileSpellDefinition payload;
        TriggerSpellDefinition trigger;
        WandDefinition wandDef;
        readonly List<CastResult> results = new List<CastResult>();

        [SetUp]
        public void SetUp()
        {
            multicast = ScriptableObject.CreateInstance<MulticastSpellDefinition>();
            multicast.ModuleId = "spell.multi2";
            multicast.ManaCost = 4f;
            multicast.Count = 2;
            multicast.SpreadStep = 10f;

            bolt = ScriptableObject.CreateInstance<ProjectileSpellDefinition>();
            bolt.ModuleId = "spell.bolt";
            bolt.ManaCost = 5f;
            bolt.BaseSpec = new ProjectileSpec { Damage = 10f, Speed = 80f };

            payload = ScriptableObject.CreateInstance<ProjectileSpellDefinition>();
            payload.ModuleId = "spell.payload";
            payload.ManaCost = 99f; // 载荷不单独收费
            payload.BaseSpec = new ProjectileSpec { Damage = 30f, Speed = 40f, DigPower = 3 };

            trigger = ScriptableObject.CreateInstance<TriggerSpellDefinition>();
            trigger.ModuleId = "spell.trigger";
            trigger.ManaCost = 8f;
            trigger.Payload = payload;
            trigger.CarrierSpec = new ProjectileSpec { Damage = 0f, Speed = 60f };

            wandDef = ScriptableObject.CreateInstance<WandDefinition>();
            wandDef.ModuleId = "wand.adv";
            wandDef.ManaMax = 200f;
            wandDef.ManaRegen = 0f;
            wandDef.Capacity = 8;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(multicast);
            Object.DestroyImmediate(bolt);
            Object.DestroyImmediate(payload);
            Object.DestroyImmediate(trigger);
            Object.DestroyImmediate(wandDef);
        }

        [Test]
        public void Multicast_DuplicatesProjectileWithFanOffsets()
        {
            wandDef.DefaultSpells.Add(multicast);
            wandDef.DefaultSpells.Add(bolt);
            var wand = new WandInstance(wandDef);

            Assert.IsTrue(wand.TryCast(results));
            Assert.AreEqual(2, results.Count, "双重施法应产出 2 份投射物");
            Assert.AreEqual(-5f, results[0].AngleOffset);
            Assert.AreEqual(5f, results[1].AngleOffset);
            Assert.AreEqual(10f, results[0].Spec.Damage);
            Assert.AreEqual(191f, wand.CurrentMana, "法力消耗 4+5");
        }

        [Test]
        public void Multicast_OnlyAffectsNextProjectile()
        {
            wandDef.DefaultSpells.Add(multicast);
            wandDef.DefaultSpells.Add(bolt);
            wandDef.DefaultSpells.Add(bolt); // 第二个投射物不受多重影响
            var wand = new WandInstance(wandDef);

            Assert.IsTrue(wand.TryCast(results));
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(0f, results[2].AngleOffset);
        }

        [Test]
        public void Trigger_CarrierCarriesPayloadWithModifiers()
        {
            wandDef.DefaultSpells.Add(multicast); // 多重只被投射物法术消费，不影响触发弹
            wandDef.DefaultSpells.Add(trigger);
            var wand = new WandInstance(wandDef);

            Assert.IsTrue(wand.TryCast(results));
            Assert.AreEqual(1, results.Count);
            Assert.AreSame(payload, results[0].Spec.TriggerPayload, "触发弹应携带载荷法术");
            Assert.AreEqual(60f, results[0].Spec.Speed, "用载体的飞行参数");
            Assert.AreEqual(188f, wand.CurrentMana, "法力消耗 4+8，载荷不计费");
        }
    }

    public class GasAndOilTests
    {
        [Test]
        public void Smoke_RisesAndDecays()
        {
            using var rig = new SimRig(2, 2);
            byte life = rig.Table[Cinder.Simulation.BuiltinMaterials.Smoke].BaseLife;
            rig.Window.SetCell(128, 50,
                Cinder.Simulation.Cell.Of(Cinder.Simulation.BuiltinMaterials.Smoke, 0, life));
            rig.Ticks(20);
            rig.Bounds(Cinder.Simulation.BuiltinMaterials.Smoke, out _, out _, out int minY, out _);
            Assert.Greater(minY, 50, "烟应上升");
            rig.Ticks((int)life);
            Assert.AreEqual(0, rig.Count(Cinder.Simulation.BuiltinMaterials.Smoke), "寿命耗尽应消散");
        }

        [Test]
        public void Oil_IgnitesFromAdjacentFire()
        {
            using var rig = new SimRig(2, 2);
            ushort oil = Cinder.Simulation.BuiltinMaterials.Oil;
            rig.FillRect(96, 100, 159, 107, oil); // 64x8 油池
            rig.Window.SetCell(128, 104, Cinder.Simulation.Cell.Of(
                Cinder.Simulation.BuiltinMaterials.Fire, 0,
                rig.Table[Cinder.Simulation.BuiltinMaterials.Fire].BaseLife));
            rig.Ticks(400);

            Assert.Less(rig.Count(oil), 64 * 8 - 100, "油应被点燃并烧掉一大片");
        }
    }
}
