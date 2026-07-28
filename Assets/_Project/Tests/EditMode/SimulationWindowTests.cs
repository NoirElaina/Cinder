using Cinder.Simulation;
using NUnit.Framework;

namespace Cinder.Tests
{
    public class SimulationWindowTests
    {
        [Test]
        public void Shift_PreservesOverlappingCells_AndEvictsLeavingChunks()
        {
            using var grid = new WorldGrid(seed: 99);
            using var window = new SimulationWindow(2, 2, 0, 0);
            window.FillFrom(0, 0, grid, null);

            window.SetCell(10, 10, Cell.Of(BuiltinMaterials.Water));   // 区块 (0,0)
            window.SetCell(140, 10, Cell.Of(BuiltinMaterials.Rock));   // 区块 (1,0)

            window.Shift(1, 0, grid, null); // 右移一格，覆盖区块 (1..2, 0..1)

            // 保留区数据原样平移
            Assert.AreEqual(BuiltinMaterials.Rock, window.GetCell(140, 10).MaterialId);
            // 离开区的区块被写回存储
            Assert.IsTrue(grid.TryGet(0, 0, out ChunkData evicted));
            Assert.AreEqual(BuiltinMaterials.Water, evicted.Get(10, 10).MaterialId);
            Assert.IsTrue(evicted.Modified);
        }

        [Test]
        public void Shift_RoundTrip_RestoresEvictedCells()
        {
            using var grid = new WorldGrid(seed: 99);
            using var window = new SimulationWindow(2, 2, 0, 0);
            window.FillFrom(0, 0, grid, null);

            window.SetCell(10, 10, Cell.Of(BuiltinMaterials.Water));
            window.Shift(1, 0, grid, null);
            window.Shift(0, 0, grid, null); // 移回

            Assert.AreEqual(BuiltinMaterials.Water, window.GetCell(10, 10).MaterialId,
                "移出的区块再次进入窗口时应从存储恢复");
        }

        [Test]
        public void FillFrom_LoadsGeneratedWorld()
        {
            using var grid = new WorldGrid(seed: 7);
            using var window = new SimulationWindow(2, 1, 0, -1);
            window.FillFrom(0, -1, grid, null);

            // 区块 (0,-1) 位于地下（地表最低 -34），应含有固体
            bool anySolid = false;
            for (int y = 0; y < window.Height && !anySolid; y++)
                for (int x = 0; x < window.Width && !anySolid; x++)
                    if (window.GetCell(x, y - SimCoords.ChunkSize).MaterialId != BuiltinMaterials.Empty)
                        anySolid = true;
            Assert.IsTrue(anySolid);
        }
    }
}
