using Cinder.Simulation;
using Cinder.Simulation.Channels;
using NUnit.Framework;

namespace Cinder.Tests
{
    /// <summary>物理场通道测试：化学反应表（ReactionChannel）与温度相变（ThermalChannel）。</summary>
    public class ChannelTests
    {
        [Test]
        public void Lava_WaterContact_ProducesRockAndSteam()
        {
            using var rig = new SimRig(2, 2);
            rig.Engine.AddChannel(new ReactionChannel());
            rig.FillRect(96, 0, 159, 3, BuiltinMaterials.Lava);
            rig.FillRect(96, 4, 159, 7, BuiltinMaterials.Water);
            rig.Ticks(120);

            Assert.Greater(rig.Count(BuiltinMaterials.Rock), 0, "岩浆遇水应生成岩石");
            Assert.Less(rig.Count(BuiltinMaterials.Water), 64 * 4, "水应被蒸发消耗");
        }

        [Test]
        public void Acid_Corrodes_Rock()
        {
            using var rig = new SimRig(2, 2);
            rig.Engine.AddChannel(new ReactionChannel());
            rig.FillRect(96, 0, 159, 19, BuiltinMaterials.Rock);
            rig.FillRect(112, 20, 143, 27, BuiltinMaterials.Acid);
            rig.Ticks(400);

            Assert.Less(rig.Count(BuiltinMaterials.Rock), 64 * 20 - 30, "酸应腐蚀掉一片岩石");
        }

        [Test]
        public void Wood_Ignites_NearLava()
        {
            using var rig = new SimRig(2, 2);
            rig.Engine.AddChannel(new ThermalChannel());
            rig.FillRect(96, 0, 159, 3, BuiltinMaterials.Lava);
            rig.FillRect(108, 4, 147, 9, BuiltinMaterials.Wood);
            rig.Ticks(300);

            Assert.Less(rig.Count(BuiltinMaterials.Wood), 40 * 6, "木应被岩浆加热点燃");
        }

        [Test]
        public void Water_Boils_ToSteam_OnLava()
        {
            using var rig = new SimRig(2, 2);
            rig.Engine.AddChannel(new ThermalChannel());
            rig.FillRect(96, 0, 159, 3, BuiltinMaterials.Lava);
            rig.FillRect(96, 4, 159, 7, BuiltinMaterials.Water);
            rig.Ticks(300);

            Assert.Greater(rig.Count(BuiltinMaterials.Steam), 0, "水应被煮沸腾为蒸汽");
            Assert.Less(rig.Count(BuiltinMaterials.Water), 64 * 4);
        }

        [Test]
        public void Ice_Melts_NearFire()
        {
            using var rig = new SimRig(2, 2);
            rig.Engine.AddChannel(new ThermalChannel());
            rig.FillRect(96, 0, 159, 0, BuiltinMaterials.Rock);
            rig.FillRect(108, 1, 127, 6, BuiltinMaterials.Ice);
            rig.Window.SetCell(130, 3, Cell.Of(BuiltinMaterials.Fire, 0,
                rig.Table[BuiltinMaterials.Fire].BaseLife));
            rig.Ticks(80);

            Assert.Greater(rig.Count(BuiltinMaterials.Water), 0, "冰靠近火应熔化成水");
        }
    }
}
