using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="DraftTiling"/> — the bridge from draft types to
    /// <c>Core</c>'s <see cref="ElevatorTiling"/>, shared by the live warning and
    /// the window's wave-list status so the two are one computation.
    /// </summary>
    public class DraftTilingTests
    {
        private static SpawnedBlockDraft Cell(Coord regionOrigin) =>
            new SpawnedBlockDraft
            {
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Blue },
                RegionOrigin = regionOrigin,
            };

        [Test]
        public void Check_WaveTilesTheRegion_ReturnsAnExactResult()
        {
            var elevator = new ElevatorDraft { Min = new Coord(0, 0), Max = new Coord(1, 0) };
            var wave = new WaveDraft { Blocks = { Cell(new Coord(0, 0)), Cell(new Coord(1, 0)) } };

            var result = DraftTiling.Check(elevator, wave);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsExact);
        }

        [Test]
        public void Check_WaveLeavesACellUncovered_ReturnsANonExactResult()
        {
            var elevator = new ElevatorDraft { Min = new Coord(0, 0), Max = new Coord(1, 0) };
            var wave = new WaveDraft { Blocks = { Cell(new Coord(0, 0)) } };

            var result = DraftTiling.Check(elevator, wave);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsExact);
            Assert.AreEqual(1, result.UncoveredCells.Count);
        }

        [Test]
        public void Check_ABlockShapeIsInvalid_ReturnsNull()
        {
            var elevator = new ElevatorDraft { Min = new Coord(0, 0), Max = new Coord(0, 0) };
            var broken = new SpawnedBlockDraft
            {
                Cells = { new Coord(0, 0), new Coord(2, 0) }, // gap: not orthogonally connected
                ColorStack = { BlockColor.Blue },
                RegionOrigin = new Coord(0, 0),
            };

            Assert.IsNull(DraftTiling.Check(elevator, new WaveDraft { Blocks = { broken } }));
        }
    }
}
