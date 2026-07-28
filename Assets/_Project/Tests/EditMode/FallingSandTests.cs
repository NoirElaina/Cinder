using System;
using Cinder.Simulation;
using NUnit.Framework;

namespace Cinder.Tests
{
    /// <summary>模拟规则测试的通用脚手架：一块小窗口 + 内置物质表。</summary>
    sealed class SimRig : IDisposable
    {
        public readonly MaterialTable Table;
        public readonly SimulationWindow Window;
        public readonly SimulationEngine Engine;

        public SimRig(int chunksX = 2, int chunksY = 2, int seed = 42)
        {
            Table = MaterialTable.CreateBuiltin();
            Window = new SimulationWindow(chunksX, chunksY, 0, 0);
            Engine = new SimulationEngine(Window, Table, seed);
        }

        public void Ticks(int n)
        {
            for (int i = 0; i < n; i++) Engine.Step();
        }

        public void FillRect(int x0, int y0, int x1, int y1, ushort materialId)
        {
            byte life = Table[materialId].BaseLife;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Window.SetCell(x, y, Cell.Of(materialId, 0, life));
        }

        public int Count(ushort materialId)
        {
            int n = 0;
            var cells = Window.ReadArray;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].MaterialId == materialId) n++;
            return n;
        }

        public void Bounds(ushort materialId, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue; minY = int.MaxValue;
            maxX = int.MinValue; maxY = int.MinValue;
            for (int y = 0; y < Window.Height; y++)
                for (int x = 0; x < Window.Width; x++)
                    if (Window.GetCell(x, y).MaterialId == materialId)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
        }

        public void Dispose()
        {
            Window.Dispose();
            Table.Dispose();
        }
    }

    public class FallingSandTests
    {
        [Test]
        public void Sand_FallsToFloor()
        {
            using var rig = new SimRig(1, 2);
            rig.Window.SetCell(64, 200, Cell.Of(BuiltinMaterials.Sand));
            rig.Ticks(250);
            Assert.AreEqual(BuiltinMaterials.Sand, rig.Window.GetCell(64, 0).MaterialId);
            Assert.AreEqual(1, rig.Count(BuiltinMaterials.Sand));
        }

        [Test]
        public void Sand_PouredColumn_SpreadsIntoPile()
        {
            using var rig = new SimRig(2, 2);
            for (int t = 0; t < 60; t++)
            {
                rig.Window.SetCell(128, 380, Cell.Of(BuiltinMaterials.Sand));
                rig.Engine.Step();
            }
            rig.Ticks(300);

            Assert.AreEqual(60, rig.Count(BuiltinMaterials.Sand), "沙子数量应守恒");
            rig.Bounds(BuiltinMaterials.Sand, out int minX, out int maxX, out int minY, out _);
            Assert.AreEqual(0, minY, "沙堆应落在底部");
            Assert.Greater(maxX - minX, 10, "沙堆应向两侧摊开成堆而非细柱");
        }

        [Test]
        public void Water_SpreadsHorizontally()
        {
            using var rig = new SimRig(2, 2);
            rig.FillRect(120, 300, 136, 316, BuiltinMaterials.Water); // 17x17 = 289
            rig.Ticks(400);

            Assert.AreEqual(289, rig.Count(BuiltinMaterials.Water), "水量应守恒");
            rig.Bounds(BuiltinMaterials.Water, out int minX, out int maxX, out _, out int maxY);
            Assert.Greater(maxX - minX, 60, "水应在底部摊开");
            Assert.Less(maxY, 20, "水应沉降到底部");
        }

        [Test]
        public void Sand_SinksThroughWater()
        {
            using var rig = new SimRig(2, 2);
            rig.FillRect(0, 0, 255, 19, BuiltinMaterials.Water);   // 底部 20 行水
            rig.FillRect(120, 100, 135, 115, BuiltinMaterials.Sand); // 16x16 = 256 沙
            rig.Ticks(400);

            Assert.AreEqual(256, rig.Count(BuiltinMaterials.Sand), "沙子数量应守恒");
            rig.Bounds(BuiltinMaterials.Sand, out _, out _, out int sandMinY, out _);
            Assert.Less(sandMinY, 10, "沙应穿过水沉到底部");

            // 水被置换后应有一部分高于原始水面
            rig.Bounds(BuiltinMaterials.Water, out _, out _, out _, out int waterMaxY);
            Assert.Greater(waterMaxY, 19, "沙沉入后应把水置换到更高处");
        }
    }

    public class FireTests
    {
        [Test]
        public void Fire_SpreadsIntoWood()
        {
            using var rig = new SimRig(2, 2);
            rig.FillRect(112, 100, 143, 131, BuiltinMaterials.Wood); // 32x32 = 1024
            rig.Window.SetCell(128, 116, Cell.Of(BuiltinMaterials.Fire, 0,
                rig.Table[BuiltinMaterials.Fire].BaseLife));
            rig.Ticks(400);

            int woodLeft = rig.Count(BuiltinMaterials.Wood);
            Assert.Less(woodLeft, 1024 - 100, "火应蔓延烧掉大片木头");
        }

        [Test]
        public void Fire_BurnsOutAlone()
        {
            using var rig = new SimRig(2, 2);
            byte life = rig.Table[BuiltinMaterials.Fire].BaseLife;
            rig.Window.SetCell(128, 128, Cell.Of(BuiltinMaterials.Fire, 0, life));
            rig.Ticks(life + 20);
            Assert.AreEqual(0, rig.Count(BuiltinMaterials.Fire), "无燃料时火应自行熄灭");
        }
    }
}
