using Cinder.Simulation;
using NUnit.Framework;

namespace Cinder.Tests
{
    public class WorldGeneratorTests
    {
        const int Seed = 12345;
        const int MinY = -31;

        [Test]
        public void Generate_SameSeedSameChunk_Deterministic()
        {
            var a = new ChunkData(3, 0);
            var b = new ChunkData(3, 0);
            try
            {
                WorldGenerator.Generate(3, 0, Seed, MinY, a.Cells);
                WorldGenerator.Generate(3, 0, Seed, MinY, b.Cells);
                CollectionAssert.AreEqual(
                    ChunkSerializer.Serialize(a), ChunkSerializer.Serialize(b));
            }
            finally { a.Dispose(); b.Dispose(); }
        }

        [Test]
        public void Generate_DifferentChunkX_Differs()
        {
            var a = new ChunkData(3, 0);
            var b = new ChunkData(4, 0);
            try
            {
                WorldGenerator.Generate(3, 0, Seed, MinY, a.Cells);
                WorldGenerator.Generate(4, 0, Seed, MinY, b.Cells);
                CollectionAssert.AreNotEqual(
                    ChunkSerializer.Serialize(a), ChunkSerializer.Serialize(b));
            }
            finally { a.Dispose(); b.Dispose(); }
        }

        [Test]
        public void Generate_BottomChunk_AllBedrock()
        {
            var c = new ChunkData(0, MinY);
            try
            {
                WorldGenerator.Generate(0, MinY, Seed, MinY, c.Cells);
                for (int i = 0; i < c.Cells.Length; i++)
                    Assert.AreEqual(BuiltinMaterials.Bedrock, c.Cells[i].MaterialId, $"index {i}");
            }
            finally { c.Dispose(); }
        }

        [Test]
        public void Generate_SkyChunk_AllEmpty()
        {
            // 地表最高约 50 格，区块 1 覆盖 y 128..255，必然全空
            var c = new ChunkData(5, 1);
            try
            {
                WorldGenerator.Generate(5, 1, Seed, MinY, c.Cells);
                for (int i = 0; i < c.Cells.Length; i++)
                    Assert.AreEqual(BuiltinMaterials.Empty, c.Cells[i].MaterialId, $"index {i}");
            }
            finally { c.Dispose(); }
        }

        [Test]
        public void Generate_BelowSurfaceChunk_HasSolid()
        {
            // 地表最低约 -34，区块 -1 覆盖 y -128..-1，必然含固体
            var c = new ChunkData(3, -1);
            try
            {
                WorldGenerator.Generate(3, -1, Seed, MinY, c.Cells);
                bool anySolid = false;
                for (int i = 0; i < c.Cells.Length; i++)
                    if (c.Cells[i].MaterialId != BuiltinMaterials.Empty) { anySolid = true; break; }
                Assert.IsTrue(anySolid);
            }
            finally { c.Dispose(); }
        }
    }
}
