using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="RegionBounds"/> (revision A2): the numeric region-bound
    /// fields clamp to the grid and correct <c>Min &gt; Max</c> rather than
    /// storing it.
    /// </summary>
    public class RegionBoundsTests
    {
        [Test]
        public void Clamped_ValuesInsideTheGrid_PassThroughUnchanged()
        {
            var (min, max) = RegionBounds.Clamped(new Coord(1, 2), new Coord(4, 5), 8, 8);

            Assert.AreEqual(new Coord(1, 2), min);
            Assert.AreEqual(new Coord(4, 5), max);
        }

        [Test]
        public void Clamped_ValuesOutsideTheGrid_AreClampedToIt()
        {
            var (min, max) = RegionBounds.Clamped(new Coord(-3, -1), new Coord(20, 12), 6, 5);

            Assert.AreEqual(new Coord(0, 0), min);
            Assert.AreEqual(new Coord(5, 4), max);
        }

        [Test]
        public void Clamped_MinGreaterThanMax_IsCorrectedNotStored()
        {
            var (min, max) = RegionBounds.Clamped(new Coord(5, 6), new Coord(2, 1), 10, 10);

            Assert.AreEqual(new Coord(2, 1), min);
            Assert.AreEqual(new Coord(5, 6), max);
        }

        [Test]
        public void Clamped_MixedAxisInversion_IsCorrectedPerAxis()
        {
            var (min, max) = RegionBounds.Clamped(new Coord(5, 1), new Coord(2, 6), 10, 10);

            Assert.AreEqual(new Coord(2, 1), min);
            Assert.AreEqual(new Coord(5, 6), max);
        }
    }
}
