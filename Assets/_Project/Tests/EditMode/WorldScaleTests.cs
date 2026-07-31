using Cinder.Simulation;
using NUnit.Framework;

namespace Cinder.Tests
{
    public class WorldScaleTests
    {
        [Test]
        public void Constants_AreConsistent()
        {
            Assert.AreEqual(4, WorldScale.CellsPerUnit);
            Assert.AreEqual(1f, WorldScale.CellsPerUnitF * WorldScale.UnitsPerCell, 1e-6f);
            Assert.AreEqual(6f, WorldScale.PlayerWidthCells * 0.5f);
            Assert.AreEqual(20, WorldScale.PlayerHeightCells);
            Assert.LessOrEqual(WorldScale.StepHeightCells, WorldScale.PlayerHeightCells);
        }

        [Test]
        public void WorldToCell_FloorSemantics()
        {
            Assert.AreEqual(0, WorldScale.WorldToCell(0f));
            Assert.AreEqual(0, WorldScale.WorldToCell(0.24f));
            Assert.AreEqual(1, WorldScale.WorldToCell(0.25f));
            Assert.AreEqual(3, WorldScale.WorldToCell(0.99f));
            Assert.AreEqual(4, WorldScale.WorldToCell(1f));

            // 负坐标必须向下取整，不能向零截断
            Assert.AreEqual(-1, WorldScale.WorldToCell(-0.1f));
            Assert.AreEqual(-1, WorldScale.WorldToCell(-0.25f),
                "-0.25 恰在格 -1 的左边界上");
            Assert.AreEqual(-2, WorldScale.WorldToCell(-0.26f));
            Assert.AreEqual(-4, WorldScale.WorldToCell(-1f));
        }

        [Test]
        public void RoundTrip_CellToWorldToCell()
        {
            for (int cell = -10; cell <= 10; cell++)
            {
                float center = WorldScale.CellCenterToWorld(cell);
                Assert.AreEqual(cell, WorldScale.WorldToCell(center), $"cell {cell}");
            }
        }

        [Test]
        public void ContinuousConversion_IsInverse()
        {
            float world = 3.6875f;
            float cells = WorldScale.WorldToCellF(world);
            Assert.AreEqual(world, WorldScale.CellToWorld(cells), 1e-6f);
        }
    }
}
