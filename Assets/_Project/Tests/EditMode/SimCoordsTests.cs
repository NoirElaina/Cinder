using Cinder.Simulation;
using NUnit.Framework;

namespace Cinder.Tests
{
    public class SimCoordsTests
    {
        [Test]
        public void CellToChunk_NegativeCoords_FloorSemantics()
        {
            Assert.AreEqual(0, SimCoords.CellToChunk(0));
            Assert.AreEqual(0, SimCoords.CellToChunk(127));
            Assert.AreEqual(1, SimCoords.CellToChunk(128));
            Assert.AreEqual(-1, SimCoords.CellToChunk(-1));
            Assert.AreEqual(-1, SimCoords.CellToChunk(-128));
            Assert.AreEqual(-2, SimCoords.CellToChunk(-129));
        }

        [Test]
        public void CellToLocal_NegativeCoords_WrapsIntoChunk()
        {
            Assert.AreEqual(0, SimCoords.CellToLocal(0));
            Assert.AreEqual(127, SimCoords.CellToLocal(-1));
            Assert.AreEqual(0, SimCoords.CellToLocal(-128));
            Assert.AreEqual(127, SimCoords.CellToLocal(127));
        }

        [Test]
        public void PackKey_RoundTrip_IncludingNegatives()
        {
            (int x, int y)[] cases = { (0, 0), (1, 2), (-1, -1), (-300, 7), (int.MaxValue, int.MinValue) };
            foreach ((int x, int y) in cases)
            {
                long key = SimCoords.PackKey(x, y);
                Assert.AreEqual(x, SimCoords.UnpackX(key));
                Assert.AreEqual(y, SimCoords.UnpackY(key));
            }
        }

        [Test]
        public void PackKey_DistinctForDistinctCoords()
        {
            Assert.AreNotEqual(SimCoords.PackKey(1, 0), SimCoords.PackKey(0, 1));
            Assert.AreNotEqual(SimCoords.PackKey(-1, 0), SimCoords.PackKey(1, 0));
        }
    }
}
