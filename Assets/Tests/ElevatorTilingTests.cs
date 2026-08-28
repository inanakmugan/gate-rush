using System.Collections.Generic;
using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ElevatorTiling"/>, the shared checker that reports how a
    /// wave covers its region. <see cref="ElevatorDefinition"/> throws on a
    /// non-exact result; the Level Editor warns on the same one.
    /// </summary>
    public class ElevatorTilingTests
    {
        private static SpawnedBlock At(int x, int y, IReadOnlyList<Coord> cells = null) =>
            Spawned(colors: new[] { BlockColor.Blue }, cells: cells, regionOrigin: new Coord(x, y));

        [Test]
        public void Check_WaveCoversEveryCellOnce_IsExact()
        {
            var wave = new[] { At(0, 0), At(1, 0), At(0, 1), At(1, 1) };

            var result = ElevatorTiling.Check(new Coord(0, 0), new Coord(1, 1), wave);

            Assert.IsTrue(result.IsExact);
        }

        [Test]
        public void Check_RegionOffsetFromOrigin_MeasuresRelativeToMin()
        {
            // Region well away from (0,0); RegionOrigin is still region-relative.
            var wave = new[] { At(0, 0), At(1, 0) };

            var result = ElevatorTiling.Check(new Coord(4, 3), new Coord(5, 3), wave);

            Assert.IsTrue(result.IsExact);
        }

        [Test]
        public void Check_OneCellUncovered_ListsThatCellOnly()
        {
            var wave = new[] { At(0, 0), At(1, 0), At(0, 1) };

            var result = ElevatorTiling.Check(new Coord(0, 0), new Coord(1, 1), wave);

            Assert.IsFalse(result.IsExact);
            CollectionAssert.AreEqual(new[] { new Coord(1, 1) }, result.UncoveredCells);
            Assert.AreEqual(0, result.OverlappingCells.Count);
        }

        [Test]
        public void Check_TwoBlocksOnTheSameCell_ReportsTheOverlap()
        {
            var wave = new[] { At(0, 0), At(0, 0), At(1, 0), At(1, 1), At(0, 1) };

            var result = ElevatorTiling.Check(new Coord(0, 0), new Coord(1, 1), wave);

            CollectionAssert.Contains(result.OverlappingCells, new Coord(0, 0));
        }

        [Test]
        public void Check_BlockExtendsBeyondRegion_ReportsTheOutsideCell()
        {
            var wave = new[] { At(0, 0, new[] { new Coord(0, 0), new Coord(1, 0) }) };

            var result = ElevatorTiling.Check(new Coord(0, 0), new Coord(0, 0), wave);

            CollectionAssert.Contains(result.OutsideRegionCells, new Coord(1, 0));
        }

        [Test]
        public void Check_BlockWithoutRegionOrigin_ReportsItsWaveIndexAndCoversNothing()
        {
            var wave = new[] { At(0, 0), Spawned(colors: new[] { BlockColor.Blue }) };

            var result = ElevatorTiling.Check(new Coord(0, 0), new Coord(1, 0), wave);

            CollectionAssert.AreEqual(new[] { 1 }, result.BlocksWithoutRegionOrigin);
            Assert.IsFalse(result.IsExact);
        }

        [Test]
        public void Check_GeneratorStyleBlockListIsUnaffected_ExactTilingStillDetected()
        {
            // A generator queue never calls this; the point is only that a wave
            // built the ordinary way still reads as exact.
            var wave = new[] { At(0, 0, new[] { new Coord(0, 0), new Coord(0, 1) }) };

            var result = ElevatorTiling.Check(new Coord(0, 0), new Coord(0, 1), wave);

            Assert.IsTrue(result.IsExact);
        }
    }
}
