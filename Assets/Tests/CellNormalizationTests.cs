using System.Collections.Generic;
using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="CellNormalization"/> (docs/Modules/09a follow-up): the
    /// shift-to-(0,0) arithmetic a queue entry's free draw needs, since it has
    /// no <c>Core</c> constructor downstream to apply D30's normalisation for it.
    /// </summary>
    public class CellNormalizationTests
    {
        [Test]
        public void Normalize_CellsAlreadyAtOrigin_AreUnchanged()
        {
            var cells = new List<Coord> { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

            var result = CellNormalization.Normalize(cells);

            CollectionAssert.AreEqual(cells, result);
        }

        [Test]
        public void Normalize_CellsShiftedAwayFromOrigin_AreShiftedBackToIt()
        {
            var cells = new List<Coord> { new Coord(2, 2), new Coord(3, 2), new Coord(2, 3), new Coord(3, 3) };

            var result = CellNormalization.Normalize(cells);

            CollectionAssert.AreEqual(
                new[] { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) }, result);
        }

        [Test]
        public void Normalize_NegativeCoordinates_AreShiftedToTheOrigin()
        {
            var cells = new List<Coord> { new Coord(-1, -1), new Coord(0, -1) };

            var result = CellNormalization.Normalize(cells);

            CollectionAssert.AreEqual(new[] { new Coord(0, 0), new Coord(1, 0) }, result);
        }

        [Test]
        public void Normalize_RelativeShapeIsPreserved()
        {
            // An L: (2,5),(2,6),(3,6) — same relative shape as ShapePresets' LNorthEast.
            var cells = new List<Coord> { new Coord(2, 5), new Coord(2, 6), new Coord(3, 6) };

            var result = CellNormalization.Normalize(cells);

            CollectionAssert.AreEqual(
                new[] { new Coord(0, 0), new Coord(0, 1), new Coord(1, 1) }, result);
        }

        [Test]
        public void Normalize_EmptyList_ReturnsEmpty()
        {
            var result = CellNormalization.Normalize(new List<Coord>());

            Assert.IsEmpty(result);
        }
    }
}
