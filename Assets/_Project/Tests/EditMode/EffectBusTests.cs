using Cinder.Game.Effects;
using Cinder.Simulation;
using Cinder.Simulation.Channels;
using NUnit.Framework;

namespace Cinder.Tests
{
    /// <summary>
    /// 效果总线测试：请求 -> 处理器 -> 世界写入的完整链路，
    /// 以及处理器的热插拔语义。
    /// </summary>
    public class EffectBusTests
    {
        const uint Seed = 42;

        static SimEffectWorld WorldOf(SimRig rig, ThermalChannel thermal) =>
            new SimEffectWorld(rig.Window, thermal, rig.Table, Seed);

        [Test]
        public void Dig_RemovesTerrain_BedrockSurvives()
        {
            using var rig = new SimRig(2, 2);
            rig.FillRect(100, 1, 155, 10, BuiltinMaterials.Rock);
            rig.FillRect(100, 0, 155, 0, BuiltinMaterials.Bedrock);

            var bus = new EffectBus();
            bus.AddHandler(new DigHandler());
            bus.Emit(EffectRequest.Dig(128, 3, 4)); // 球下缘扫到基岩行
            bus.Flush(WorldOf(rig, null));

            Assert.AreEqual(56, rig.Count(BuiltinMaterials.Bedrock), "基岩免疫挖掘");
            Assert.AreEqual(0, rig.Window.GetCell(128, 3).MaterialId, "挖掘中心应被清空");
            Assert.Less(rig.Count(BuiltinMaterials.Rock), 56 * 10 - 40, "岩层应被挖出球坑");
        }

        [Test]
        public void Explosion_DigsRing_IgnitesCore_SparesBedrock()
        {
            using var rig = new SimRig(2, 2);
            rig.FillRect(108, 0, 147, 3, BuiltinMaterials.Bedrock);
            rig.FillRect(108, 4, 147, 13, BuiltinMaterials.Wood); // 40x10 木块

            var bus = new EffectBus();
            bus.AddHandler(new ExplosionHandler());
            bus.Emit(EffectRequest.Explosion(128, 9, 6));
            bus.Flush(WorldOf(rig, null));

            Assert.AreEqual(40 * 4, rig.Count(BuiltinMaterials.Bedrock), "基岩免疫爆炸");
            Assert.Greater(rig.Count(BuiltinMaterials.Fire), 0, "心区木头应被点燃");
            Assert.Less(rig.Count(BuiltinMaterials.Wood), 40 * 10 - 50, "爆风应挖除一片木头");
        }

        [Test]
        public void Heat_BoilsWater_ThroughThermalChannel()
        {
            using var rig = new SimRig(2, 2);
            var thermal = new ThermalChannel();
            rig.Engine.AddChannel(thermal);
            rig.FillRect(96, 0, 159, 3, BuiltinMaterials.Water);

            var bus = new EffectBus();
            bus.AddHandler(new HeatHandler());
            bus.Emit(EffectRequest.Heat(128, 2, 5, 800));
            bus.Flush(WorldOf(rig, thermal));
            Assert.AreEqual(0, rig.Count(BuiltinMaterials.Steam), "加热在 tick 结算前不生效");

            rig.Ticks(60);
            Assert.Greater(rig.Count(BuiltinMaterials.Steam), 0, "加热点的水应沸腾成蒸汽");
            Assert.Less(rig.Count(BuiltinMaterials.Water), 64 * 4);
        }

        [Test]
        public void Freeze_TurnsWaterIntoIce_ThroughThermalChannel()
        {
            using var rig = new SimRig(2, 2);
            var thermal = new ThermalChannel();
            rig.Engine.AddChannel(thermal);
            rig.FillRect(96, 0, 159, 3, BuiltinMaterials.Water);

            var bus = new EffectBus();
            bus.AddHandler(new FreezeHandler());
            bus.Emit(EffectRequest.Freeze(128, 2, 5, 600));
            bus.Flush(WorldOf(rig, thermal));

            rig.Ticks(10);
            Assert.Greater(rig.Count(BuiltinMaterials.Ice), 20, "冰冻点的水应结成冰");
            Assert.Less(rig.Count(BuiltinMaterials.Water), 64 * 4 - 20);
        }

        [Test]
        public void Ignite_SetsFlammable_LeavesRock()
        {
            using var rig = new SimRig(2, 2);
            rig.FillRect(108, 4, 123, 13, BuiltinMaterials.Wood);
            rig.FillRect(132, 4, 147, 13, BuiltinMaterials.Rock);

            var bus = new EffectBus();
            bus.AddHandler(new IgniteHandler());
            bus.Emit(EffectRequest.Ignite(128, 9, 6));
            bus.Flush(WorldOf(rig, null));

            Assert.Greater(rig.Count(BuiltinMaterials.Fire), 0, "木头应被点燃");
            Assert.AreEqual(16 * 10, rig.Count(BuiltinMaterials.Rock), "岩石不可燃");
        }

        [Test]
        public void HotPlug_RemoveHandler_SilencesKind()
        {
            using var rig = new SimRig(2, 2);
            rig.FillRect(100, 1, 155, 10, BuiltinMaterials.Rock);

            var bus = new EffectBus();
            var dig = new DigHandler();
            bus.AddHandler(dig);
            SimEffectWorld world = WorldOf(rig, null);

            Assert.IsTrue(bus.RemoveHandler(dig));
            bus.Emit(EffectRequest.Dig(128, 5, 4));
            bus.Flush(world);
            Assert.AreEqual(56 * 10, rig.Count(BuiltinMaterials.Rock), "卸载处理器后挖掘应无效");

            bus.AddHandler(dig);
            bus.Emit(EffectRequest.Dig(128, 5, 4));
            bus.Flush(world);
            Assert.Less(rig.Count(BuiltinMaterials.Rock), 56 * 10 - 40, "重新挂载后挖掘恢复");
        }
    }
}
