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

        [Test]
        public void RegionMoveRect_RegionWiderThanTheGrid_DoesNotMoveOnThatAxis()
        {
            // A 8-wide region on a 6-wide grid: no delta can put it wholly
            // inside, so its clamp bounds invert (lower 0, upper -2). Before the
            // fix the clamp honoured the upper bound and shoved the region
            // further out of the grid on every drag. Only hand-edited JSON
            // produces such a region — the file this editor exists to repair.
            var (min, max) = DraftDrag.RegionMoveRect(
                originalMin: new Coord(0, 1), originalMax: new Coord(7, 2),
                anchorCell: new Coord(0, 1), currentCell: new Coord(3, 1),
                width: 6, height: 6);

            Assert.AreEqual(new Coord(0, 1), min);
            Assert.AreEqual(new Coord(7, 2), max);
        }

        [Test]
        public void RegionMoveRect_RegionTallerThanTheGrid_StillMovesOnTheAxisThatFits()
        {
            // The two axes are clamped independently, so an oversized height
            // must not freeze a horizontal drag as well.
            var (min, max) = DraftDrag.RegionMoveRect(
                originalMin: new Coord(0, 0), originalMax: new Coord(1, 7),
                anchorCell: new Coord(0, 0), currentCell: new Coord(2, 3),
                width: 6, height: 6);

            Assert.AreEqual(new Coord(2, 0), min);
            Assert.AreEqual(new Coord(3, 7), max);
        }

        // -- shared bounds helpers (item 6) ------------------------------

        [Test]
        public void IsInsideBoard_CellsInsideAndOutsideTheGrid_AnswersForEach()
        {
            var draft = LevelDraft.NewEmpty(6, 4);

            Assert.IsTrue(DraftDrag.IsInsideBoard(draft, new Coord(0, 0)));
            Assert.IsTrue(DraftDrag.IsInsideBoard(draft, new Coord(5, 3)));
            Assert.IsFalse(DraftDrag.IsInsideBoard(draft, new Coord(6, 0)));
            Assert.IsFalse(DraftDrag.IsInsideBoard(draft, new Coord(0, 4)));
            Assert.IsFalse(DraftDrag.IsInsideBoard(draft, new Coord(-1, 0)));
        }

        [Test]
        public void IsInsideRegion_CellsAreRelativeToTheRegionsMinCorner_NotAbsoluteBoardPositions()
        {
            var elevator = new ElevatorDraft { Id = 1, Min = new Coord(2, 2), Max = new Coord(4, 3) };

            Assert.IsTrue(DraftDrag.IsInsideRegion(elevator, new Coord(0, 0)));
            Assert.IsTrue(DraftDrag.IsInsideRegion(elevator, new Coord(2, 1)));
            Assert.IsFalse(DraftDrag.IsInsideRegion(elevator, new Coord(3, 0)));
            Assert.IsFalse(DraftDrag.IsInsideRegion(elevator, new Coord(0, 2)));
        }

        // -- edge feature drag (item 8) ----------------------------------

        private static LevelDraft EdgeFeatureDraft()
        {
            var draft = LevelDraft.NewEmpty(6, 6);
            draft.Gates.Add(new GateDraft
            {
                Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 2, Color = BlockColor.Red,
            });
            draft.Generators.Add(new GeneratorDraft
            {
                Id = 1, Edge = BoardEdge.Bottom, Offset = 4, Width = 1,
            });
            draft.Generators.Add(new GeneratorDraft
            {
                Id = 2, Edge = BoardEdge.Left, Offset = 0, Width = 2,
            });
            return draft;
        }

        [Test]
        public void CandidateEdgeOffset_KeepsTheGrabbedCellUnderThePointer()
        {
            Assert.AreEqual(3, DraftDrag.CandidateEdgeOffset(pointerAlongEdge: 4, grabOffset: 1));
        }
        [Test]
        public void IsLegalEdgeFeature_SpanInsideTheEdgeAndClearOfOtherFeatures_IsLegal()
        {
            var draft = EdgeFeatureDraft();

            Assert.IsTrue(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Bottom, 2));
        }

        [Test]
        public void IsLegalEdgeFeature_SpanWouldRunPastTheEndOfTheEdge_IsIllegal()
        {
            var draft = EdgeFeatureDraft();

            Assert.IsFalse(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Bottom, 5));
            Assert.IsFalse(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Bottom, -1));
        }

        [Test]
        public void IsLegalEdgeFeature_SpanWouldOverlapAnotherFeatureOnTheSameEdge_IsIllegal()
        {
            var draft = EdgeFeatureDraft();

            // The bottom generator holds cell 4; a 2-wide gate at 3 reaches it.
            Assert.IsFalse(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Bottom, 3));
        }

        [Test]
        public void IsLegalEdgeFeature_AFeatureOnAnotherEdgeHoldsThoseOffsets_IsLegal()
        {
            var draft = EdgeFeatureDraft();

            // Generator 2 occupies offsets 0-1 of the left edge. The gate is
            // landing on the bottom edge, so those offsets are not its to
            // contend for.
            Assert.IsTrue(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Bottom, 0));
        }

        [Test]
        public void IsLegalEdgeFeature_FeatureStaysWhereItIs_IsLegalRatherThanCollidingWithItself()
        {
            var draft = EdgeFeatureDraft();

            Assert.IsTrue(DraftDrag.IsLegalEdgeFeature(
                draft, draft.Gates[0], draft.Gates[0].Edge, draft.Gates[0].Offset));
        }

        // -- crossing onto another edge ----------------------------------

        [Test]
        public void IsLegalEdgeFeature_CandidateOnAnEmptyDifferentEdge_IsLegal()
        {
            var draft = EdgeFeatureDraft();

            Assert.IsTrue(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Top, 0));
        }

        [Test]
        public void IsLegalEdgeFeature_CandidateLandsOnAFeatureOfTheDestinationEdge_IsIllegal()
        {
            // Overlap is judged against the edge being landed on, not the one
            // being left: generator 2 holds offsets 0-1 of the left edge.
            var draft = EdgeFeatureDraft();

            Assert.IsFalse(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Left, 1));
            Assert.IsTrue(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Left, 2));
        }

        [Test]
        public void IsLegalEdgeFeature_SpanTooWideForTheDestinationEdge_IsIllegal()
        {
            // A 4-wide gate fits the bottom edge of a 6x2 board and cannot fit
            // either side edge, which is only 2 cells long. The two edge pairs
            // have different lengths, so the fit has to be checked against the
            // destination rather than assumed from where the feature started.
            var draft = LevelDraft.NewEmpty(6, 2);
            draft.Gates.Add(new GateDraft
            {
                Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 4, Color = BlockColor.Red,
            });

            Assert.IsTrue(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Bottom, 1));
            Assert.IsFalse(DraftDrag.IsLegalEdgeFeature(draft, draft.Gates[0], BoardEdge.Left, 0));
        }

        [Test]
        public void TryApplyEdgeFeature_LegalOffset_MovesTheGate()
        {
            var draft = EdgeFeatureDraft();

            var applied = DraftDrag.TryApplyEdgeFeature(draft, draft.Gates[0], BoardEdge.Bottom, 2);

            Assert.IsTrue(applied);
            Assert.AreEqual(BoardEdge.Bottom, draft.Gates[0].Edge);
            Assert.AreEqual(2, draft.Gates[0].Offset);
        }

        [Test]
        public void TryApplyEdgeFeature_LegalOffset_MovesTheGeneratorThroughTheSamePath()
        {
            var draft = EdgeFeatureDraft();

            var applied = DraftDrag.TryApplyEdgeFeature(draft, draft.Generators[0], BoardEdge.Bottom, 3);

            Assert.IsTrue(applied);
            Assert.AreEqual(3, draft.Generators[0].Offset);
        }

        [Test]
        public void TryApplyEdgeFeature_CandidateOnAnotherEdge_WritesBothEdgeAndOffset()
        {
            var draft = EdgeFeatureDraft();

            var applied = DraftDrag.TryApplyEdgeFeature(draft, draft.Gates[0], BoardEdge.Right, 3);

            Assert.IsTrue(applied);
            Assert.AreEqual(BoardEdge.Right, draft.Gates[0].Edge);
            Assert.AreEqual(3, draft.Gates[0].Offset);
        }

        [Test]
        public void TryApplyEdgeFeature_IllegalOffset_LeavesTheDraftUntouched()
        {
            var draft = EdgeFeatureDraft();

            var applied = DraftDrag.TryApplyEdgeFeature(draft, draft.Gates[0], BoardEdge.Bottom, 3);

            Assert.IsFalse(applied);
            Assert.AreEqual(BoardEdge.Bottom, draft.Gates[0].Edge);
            Assert.AreEqual(0, draft.Gates[0].Offset);
        }

        [Test]
        public void TryApplyEdgeFeature_IllegalCandidateOnAnotherEdge_LeavesBothFieldsUntouched()
        {
            // The edge must not be written when the offset is refused, or a
            // rejected drag would still have moved the feature halfway.
            var draft = EdgeFeatureDraft();

            var applied = DraftDrag.TryApplyEdgeFeature(draft, draft.Gates[0], BoardEdge.Left, 1);

            Assert.IsFalse(applied);
            Assert.AreEqual(BoardEdge.Bottom, draft.Gates[0].Edge);
            Assert.AreEqual(0, draft.Gates[0].Offset);
        }

        [Test]
        public void TryApplyEdgeFeature_SomethingThatIsNotAnEdgeFeature_IsRefused()
        {
            var draft = EdgeFeatureDraft();

            Assert.IsFalse(DraftDrag.TryApplyEdgeFeature(
                draft, new ShutterDraft { Id = 1, Threshold = 1 }, BoardEdge.Bottom, 0));
        }
    }
}
