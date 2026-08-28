using System.Collections.Generic;
using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    public class ElevatorDefinitionTests
    {
        private static SpawnedBlock Cell(int regionX, int regionY, IReadOnlyList<Coord> cells = null) =>
            Spawned(
                colors: new[] { BlockColor.Blue },
                cells: cells,
                regionOrigin: new Coord(regionX, regionY));

        [Test]
        public void Constructor_MutatingCallerWaveListAfterConstruction_DoesNotAffectStoredWaves()
        {
            var wave = new List<SpawnedBlock> { Cell(0, 0) };
            var waves = new List<IReadOnlyList<SpawnedBlock>> { wave };

            var elevator = new ElevatorDefinition(1, new Coord(0, 0), new Coord(0, 0), waves);

            wave.Add(Cell(0, 0));

            Assert.AreEqual(1, elevator.Waves[0].Count);
        }

        [Test]
        public void Constructor_WaveTilesRegionExactly_Succeeds()
        {
            // 2x2 region tiled by an L-tromino plus a 1x1.
            var lShape = new[] { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

            Assert.DoesNotThrow(() => new ElevatorDefinition(
                1, new Coord(0, 0), new Coord(1, 1),
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { Cell(0, 0, lShape), Cell(1, 1) }
                }));
        }

        [Test]
        public void Constructor_WaveLeavesACellUncovered_ThrowsNamingElevatorWaveAndCount()
        {
            var ex = Assert.Throws<System.ArgumentException>(() => new ElevatorDefinition(
                9, new Coord(0, 0), new Coord(1, 1),
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { Cell(0, 0), Cell(1, 0), Cell(0, 1) }
                }));

            StringAssert.Contains("Elevator 9", ex.Message);
            StringAssert.Contains("wave 0", ex.Message);
            StringAssert.Contains("1 cell", ex.Message);
        }

        [Test]
        public void Constructor_WaveHasTwoBlocksOverlapping_Throws()
        {
            var ex = Assert.Throws<System.ArgumentException>(() => new ElevatorDefinition(
                3, new Coord(0, 0), new Coord(1, 0),
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { Cell(0, 0), Cell(0, 0) }
                }));

            StringAssert.Contains("overlapping", ex.Message);
        }

        [Test]
        public void Constructor_WaveBlockExtendsOutsideRegion_Throws()
        {
            var ex = Assert.Throws<System.ArgumentException>(() => new ElevatorDefinition(
                4, new Coord(0, 0), new Coord(0, 0),
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { Cell(0, 0, new[] { new Coord(0, 0), new Coord(1, 0) }) }
                }));

            StringAssert.Contains("outside the region", ex.Message);
        }

        [Test]
        public void Constructor_WaveBlockHasNoRegionOrigin_ThrowsNamingElevatorAndWave()
        {
            var ex = Assert.Throws<System.ArgumentException>(() => new ElevatorDefinition(
                7, new Coord(0, 0), new Coord(0, 0),
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { Spawned(colors: new[] { BlockColor.Blue }) }
                }));

            StringAssert.Contains("Elevator 7", ex.Message);
            StringAssert.Contains("wave 0", ex.Message);
            StringAssert.Contains("RegionOrigin", ex.Message);
        }

        [Test]
        public void Constructor_EmptyWaveList_Succeeds()
        {
            Assert.DoesNotThrow(() => new ElevatorDefinition(
                1, new Coord(0, 0), new Coord(2, 2),
                System.Array.Empty<IReadOnlyList<SpawnedBlock>>()));
        }
    }
}
