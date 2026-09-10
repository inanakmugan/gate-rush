using GateRush.Core;
using GateRush.Editor;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="DraftDrag"/> (docs/Modules/09a, Session B, Part 2):
    /// board and wave drag validation and application, the grab-offset math, and
    /// the region drag-draw rectangle.
    /// </summary>
    public class DraftDragTests
    {
        // -- board block drag -------------------------------------------

        private static LevelDraft BoardDraft()
        {
            var draft = LevelDraft.NewEmpty(6, 6);
            draft.StaticWalls.Add(new Coord(3, 3));
            draft.Blocks.Add(new BlockDraft
            {
                Id = 1,
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Red },
                StartOrigin = new Coord(0, 0),
            });
            draft.Blocks.Add(new BlockDraft
            {
                Id = 2,
                Cells = { new Coord(0, 0), new Coord(1, 0) },
                ColorStack = { BlockColor.Blue },
                StartOrigin = new Coord(2, 0),
            });
            return draft;
        }

        [Test]
        public void IsLegalOnBoard_CandidateInsideGridClearOfWallsAndBlocks_IsLegal()
        {
            var draft = BoardDraft();

            Assert.IsTrue(DraftDrag.IsLegalOnBoard(draft, draft.Blocks[0], new Coord(4, 4)));
        }

        [Test]
        public void IsLegalOnBoard_CandidateOutsideGrid_IsIllegal()
        {
            var draft = BoardDraft();

            Assert.IsFalse(DraftDrag.IsLegalOnBoard(draft, draft.Blocks[0], new Coord(6, 0)));
            Assert.IsFalse(DraftDrag.IsLegalOnBoard(draft, draft.Blocks[0], new Coord(-1, 0)));
        }

        [Test]
        public void IsLegalOnBoard_CandidateOnAStaticWall_IsIllegal()
        {
            var draft = BoardDraft();

            Assert.IsFalse(DraftDrag.IsLegalOnBoard(draft, draft.Blocks[0], new Coord(3, 3)));
        }

        [Test]
        public void IsLegalOnBoard_CandidateOverlappingAnotherBlock_IsIllegal()
        {
            var draft = BoardDraft();

            Assert.IsFalse(DraftDrag.IsLegalOnBoard(draft, draft.Blocks[0], new Coord(2, 0)));
        }

        [Test]
        public void IsLegalOnBoard_CandidateAtTheDraggedBlocksOwnCurrentCells_IsLegal()
        {
            var draft = BoardDraft();

            // Block 1 currently occupies (0, 0). Without excluding its own cells
            // from the overlap check, this would always report illegal.
            Assert.IsTrue(DraftDrag.IsLegalOnBoard(draft, draft.Blocks[0], new Coord(0, 0)));
        }

        [Test]
        public void TryApplyBoard_LegalCandidate_MovesTheBlockAndReturnsTrue()
        {
            var draft = BoardDraft();

            var applied = DraftDrag.TryApplyBoard(draft, draft.Blocks[0], new Coord(4, 4));

            Assert.IsTrue(applied);
            Assert.AreEqual(new Coord(4, 4), draft.Blocks[0].StartOrigin);
        }

        [Test]
        public void TryApplyBoard_IllegalCandidate_LeavesTheDraftByteForByteUnchangedAndReturnsFalse()
        {
            var draft = BoardDraft();
            var before = LevelSerializer.ToJson(draft.ToDto());

            var applied = DraftDrag.TryApplyBoard(draft, draft.Blocks[0], new Coord(2, 0)); // overlaps block 2

            Assert.IsFalse(applied);
            Assert.AreEqual(before, LevelSerializer.ToJson(draft.ToDto()));
        }

        // -- wave block drag ---------------------------------------------

        private static (ElevatorDraft Elevator, WaveDraft Wave) WaveDraftFixture()
        {
            var elevator = new ElevatorDraft { Id = 1, Min = new Coord(2, 2), Max = new Coord(4, 3) }; // 3x2 region
            var wave = new WaveDraft();
            var dragged = new SpawnedBlockDraft
            {
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Red },
                RegionOrigin = new Coord(0, 0),
            };
            var other = new SpawnedBlockDraft
            {
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Blue },
                RegionOrigin = new Coord(1, 0),
            };
            wave.Blocks.Add(dragged);
            wave.Blocks.Add(other);
            elevator.Waves.Add(wave);
            return (elevator, wave);
        }

        [Test]
        public void IsLegalInWave_CandidateInsideRegionClearOfOtherBlocks_IsLegal()
        {
            var (elevator, wave) = WaveDraftFixture();

            Assert.IsTrue(DraftDrag.IsLegalInWave(elevator, wave, wave.Blocks[0], new Coord(2, 1)));
        }

        [Test]
        public void IsLegalInWave_CandidateOutsideTheRegionsOwnExtent_IsIllegal()
        {
            var (elevator, wave) = WaveDraftFixture(); // region is 3 wide, 2 tall — local x in [0,2], y in [0,1]

            Assert.IsFalse(DraftDrag.IsLegalInWave(elevator, wave, wave.Blocks[0], new Coord(3, 0)));
            Assert.IsFalse(DraftDrag.IsLegalInWave(elevator, wave, wave.Blocks[0], new Coord(0, 2)));
        }

        [Test]
        public void IsLegalInWave_CandidateOverlappingAnotherWaveBlock_IsIllegal()
        {
            var (elevator, wave) = WaveDraftFixture();

            Assert.IsFalse(DraftDrag.IsLegalInWave(elevator, wave, wave.Blocks[0], new Coord(1, 0)));
        }

        [Test]
        public void IsLegalInWave_CandidateAtTheDraggedBlocksOwnCurrentCells_IsLegal()
        {
            var (elevator, wave) = WaveDraftFixture();

            Assert.IsTrue(DraftDrag.IsLegalInWave(elevator, wave, wave.Blocks[0], new Coord(0, 0)));
        }

        [Test]
        public void TryApplyWave_IllegalCandidate_LeavesTheDraftUnchangedAndReturnsFalse()
        {
            var (elevator, wave) = WaveDraftFixture();
            var originalOrigin = wave.Blocks[0].RegionOrigin;

            var applied = DraftDrag.TryApplyWave(elevator, wave, wave.Blocks[0], new Coord(1, 0)); // overlaps the other block

            Assert.IsFalse(applied);
            Assert.AreEqual(originalOrigin, wave.Blocks[0].RegionOrigin);
        }

        [Test]
        public void TryApplyWave_LegalCandidate_MovesTheBlockAndReturnsTrue()
        {
            var (elevator, wave) = WaveDraftFixture();

            var applied = DraftDrag.TryApplyWave(elevator, wave, wave.Blocks[0], new Coord(2, 1));

            Assert.IsTrue(applied);
            Assert.AreEqual(new Coord(2, 1), wave.Blocks[0].RegionOrigin);
        }

        // -- grab offset ---------------------------------------------------

        [Test]
        public void CandidateOrigin_BlockGrabbedByTopRightCellAndDroppedTwoCellsRight_MovesExactlyTwoCellsRight()
        {
            // A 2x1 block starting at (0, 0) is grabbed by its top-right cell,
            // (1, 0) — grab offset (1, 0) relative to its origin.
            var grabOffset = new Coord(1, 0);
            var pressPointerCell = new Coord(1, 0);
            var startOrigin = DraftDrag.CandidateOrigin(pressPointerCell, grabOffset);

            var releasePointerCell = pressPointerCell + new Coord(2, 0);
            var candidateOrigin = DraftDrag.CandidateOrigin(releasePointerCell, grabOffset);

            Assert.AreEqual(new Coord(0, 0), startOrigin);
            Assert.AreEqual(new Coord(2, 0), candidateOrigin);
        }

        // -- region drag-draw ------------------------------------------------

        [Test]
        public void RegionCreateRect_PressBelowRightOfRelease_UsesTheCellsNotRawPositions()
        {
            var (min, max) = DraftDrag.RegionCreateRect(new Coord(5, 5), new Coord(2, 1), 8, 8);

            Assert.AreEqual(new Coord(2, 1), min);
            Assert.AreEqual(new Coord(5, 5), max);
        }

        [Test]
        public void RegionCreateRect_PressAboveLeftOfRelease_UsesTheCellsNotRawPositions()
        {
            var (min, max) = DraftDrag.RegionCreateRect(new Coord(1, 1), new Coord(4, 3), 8, 8);

            Assert.AreEqual(new Coord(1, 1), min);
            Assert.AreEqual(new Coord(4, 3), max);
        }

        [Test]
        public void RegionCreateRect_ReleaseOutsideTheGrid_IsClampedThroughRegionBounds()
        {
            var (min, max) = DraftDrag.RegionCreateRect(new Coord(1, 1), new Coord(20, 20), 6, 6);

            Assert.AreEqual(new Coord(1, 1), min);
            Assert.AreEqual(new Coord(5, 5), max);
        }

        [Test]
        public void RegionMoveRect_PointerMovesByADelta_ShiftsBothCornersBySameDelta()
        {
            var (min, max) = DraftDrag.RegionMoveRect(
                originalMin: new Coord(1, 1), originalMax: new Coord(2, 2),
                anchorCell: new Coord(1, 1), currentCell: new Coord(3, 2),
                width: 8, height: 8);

            Assert.AreEqual(new Coord(3, 2), min);
            Assert.AreEqual(new Coord(4, 3), max);
        }

        [Test]
        public void RegionMoveRect_DraggedPastTheGridEdge_StopsAtTheEdgeWithSizeIntact()
        {
            // A 2x2 region at (3,3)-(4,4) is dragged 7 cells right in a 6x6 grid
            // (max index 5) — far more than fits. It should stop flush against
            // the edge at (4,3)-(5,4), one cell over, still 2x2 — not clamp each
            // corner independently, which would collapse it to a sliver.
            var (min, max) = DraftDrag.RegionMoveRect(
                originalMin: new Coord(3, 3), originalMax: new Coord(4, 4),
                anchorCell: new Coord(3, 3), currentCell: new Coord(10, 3),
                width: 6, height: 6);

            Assert.AreEqual(new Coord(4, 3), min);
            Assert.AreEqual(new Coord(5, 4), max);
        }

        [Test]
        public void RegionMoveRect_DraggedPastTheGridEdgeOnBothAxes_StopsAtEachEdgeIndependentlyWithSizeIntact()
        {
            var (min, max) = DraftDrag.RegionMoveRect(
                originalMin: new Coord(0, 0), originalMax: new Coord(1, 1),
                anchorCell: new Coord(0, 0), currentCell: new Coord(20, -20),
                width: 6, height: 6);

            Assert.AreEqual(new Coord(4, 0), min);
            Assert.AreEqual(new Coord(5, 1), max);
        }
    }
}
