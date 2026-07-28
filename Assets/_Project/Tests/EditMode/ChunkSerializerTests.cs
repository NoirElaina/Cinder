using Cinder.Simulation;
using NUnit.Framework;

namespace Cinder.Tests
{
    public class ChunkSerializerTests
    {
        [Test]
        public void RoundTrip_PreservesCoordsAndCells()
        {
            var source = new ChunkData(-3, 7);
            try
            {
                source.Set(0, 0, Cell.Of(BuiltinMaterials.Rock, 1, 2));
                source.Set(127, 127, Cell.Of(BuiltinMaterials.Water, 3, 0));
                source.Set(64, 12, Cell.Of(BuiltinMaterials.Fire, 2, 39));

                byte[] bytes = ChunkSerializer.Serialize(source);
                Assert.AreEqual(ChunkSerializer.TotalSize, bytes.Length);
                Assert.IsTrue(ChunkSerializer.TryDeserialize(bytes, out ChunkData restored));
                try
                {
                    Assert.AreEqual(-3, restored.ChunkX);
                    Assert.AreEqual(7, restored.ChunkY);
                    Assert.AreEqual(BuiltinMaterials.Rock, restored.Get(0, 0).MaterialId);
                    Assert.AreEqual(1, restored.Get(0, 0).Variant);
                    Assert.AreEqual(2, restored.Get(0, 0).State);
                    Assert.AreEqual(BuiltinMaterials.Water, restored.Get(127, 127).MaterialId);
                    Assert.AreEqual(39, restored.Get(64, 12).State);
                }
                finally { restored.Dispose(); }
            }
            finally { source.Dispose(); }
        }

        [Test]
        public void TryDeserialize_Garbage_ReturnsFalse()
        {
            Assert.IsFalse(ChunkSerializer.TryDeserialize(null, out _));
            Assert.IsFalse(ChunkSerializer.TryDeserialize(new byte[10], out _));
            Assert.IsFalse(ChunkSerializer.TryDeserialize(
                new byte[ChunkSerializer.TotalSize], out _)); // magic 不对
        }
    }
}
