using System.Collections.Generic;
using Cinder.Core.Attributes;
using Cinder.Game.Characters;
using Cinder.Game.Items;
using Cinder.Game.Spells;
using NUnit.Framework;

namespace Cinder.Tests
{
    public class EquipmentTests
    {
        AttributeSet characterSet;
        AttributeSet wandSet;
        Equipment equipment;
        ItemDefinition ring;
        ItemDefinition core;

        [SetUp]
        public void SetUp()
        {
            characterSet = new AttributeSet();
            characterSet.GetOrAdd(CharacterAttributes.MoveSpeed).SetBase(22f);
            wandSet = new AttributeSet();
            wandSet.GetOrAdd(WandAttributes.ManaMax).SetBase(100f);
            equipment = new Equipment(id =>
                id != null && id.StartsWith("wand.") ? wandSet : characterSet);
            ring = ItemFactory.CreateDemoRing();
            core = ItemFactory.CreateDemoCore();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(ring);
            UnityEngine.Object.DestroyImmediate(core);
        }

        [Test]
        public void Equip_AppliesModifiers_Unequip_RemovesThem()
        {
            Assert.AreEqual(22f, characterSet.GetValue(CharacterAttributes.MoveSpeed));
            Assert.IsTrue(equipment.Equip(ring));
            Assert.AreEqual(33f, characterSet.GetValue(CharacterAttributes.MoveSpeed), "22 x1.5");

            Assert.IsTrue(equipment.Unequip(ring.EquipSlot));
            Assert.AreEqual(22f, characterSet.GetValue(CharacterAttributes.MoveSpeed), "卸下应还原");
        }

        [Test]
        public void Equip_RoutesWandAttributesToWandSet()
        {
            Assert.IsTrue(equipment.Equip(core));
            Assert.AreEqual(150f, wandSet.GetValue(WandAttributes.ManaMax), "100 x1.5");
            Assert.AreEqual(22f, characterSet.GetValue(CharacterAttributes.MoveSpeed), "角色属性不受影响");
        }

        [Test]
        public void Equip_SameSlot_SwapsAndCleansOldSource()
        {
            var ring2 = ItemFactory.CreateDemoRing();
            try
            {
                equipment.Equip(ring);
                Assert.IsTrue(equipment.Equip(ring2), "同槽应换卸");
                Assert.AreSame(ring2, equipment.Get("charm1"));
                Assert.AreEqual(1, characterSet.Get(CharacterAttributes.MoveSpeed).Modifiers.Count,
                    "旧物品的修饰应被移除，不应叠加");
            }
            finally { UnityEngine.Object.DestroyImmediate(ring2); }
        }

        [Test]
        public void SlotChanged_Fires()
        {
            var log = new List<string>();
            equipment.SlotChanged += (slot, item) => log.Add(slot + ":" + (item == null ? "off" : "on"));
            equipment.Equip(ring);
            equipment.Unequip(ring.EquipSlot);
            CollectionAssert.AreEqual(new[] { "charm1:on", "charm1:off" }, log);
        }
    }

    public class InventoryTests
    {
        [Test]
        public void AddRemove_EventsAndCapacity()
        {
            var inventory = new Inventory(1);
            var a = ItemFactory.CreateDemoRing();
            var b = ItemFactory.CreateDemoCore();
            try
            {
                var events = new List<string>();
                inventory.Added += i => events.Add("+" + i.ModuleId);
                inventory.Removed += i => events.Add("-" + i.ModuleId);

                Assert.IsTrue(inventory.Add(a));
                Assert.IsFalse(inventory.Add(b), "容量满应拒绝");
                Assert.IsFalse(inventory.Add(a), "重复应拒绝");
                Assert.IsTrue(inventory.Remove(a));
                Assert.IsTrue(inventory.Add(b));

                CollectionAssert.AreEqual(
                    new[] { "+item.swift_ring", "-item.swift_ring", "+item.mana_core" }, events);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }
    }
}
