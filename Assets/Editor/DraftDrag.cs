using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// Drag validation and application for Session B's central gesture — moving a
    /// block, or a shutter/elevator region, by dragging it (docs/Modules/09a,
    /// Session B, Part 2). A pure function over a draft — no <c>UnityEditor</c> or
    /// <c>UnityEngine</c> type in any signature — so the window only tracks where
    /// the pointer is and defers every rule to here.
    /// </summary>
    /// <remarks>
    /// The draft is never touched mid-drag: the window holds the candidate
    /// position locally and calls <see cref="TryApplyBoard"/> or
    /// <see cref="TryApplyWave"/> once, on a legal mouse-up. That is what makes a
    /// rejected or cancelled drag leave the draft untouched by construction, and
    /// what makes a completed drag exactly one mutation — relevant to Session C's
    /// undo, which does not need to special-case drags as a result.
    /// </remarks>
    public static class DraftDrag
    {
        /// <summary>
        /// The origin a drag should land the footprint at, given the pointer's
        /// current cell and which relative cell of the footprint was grabbed. The
        /// offset stays constant for the whole drag, so the grabbed cell always
        /// stays under the pointer rather than the block's origin snapping to it.
        /// </summary>
        public static Coord CandidateOrigin(Coord pointerCell, Coord grabOffset) => pointerCell - grabOffset;

        /// <summary>
        /// Whether <paramref name="dragged"/> could legally sit with its
        /// footprint at <paramref name="candidateOrigin"/> on the board: every
        /// cell inside the grid, clear of static walls, and clear of every other
        /// living block. <paramref name="dragged"/>'s own current cells are
        /// excluded from the block-overlap check — without that a block always
        /// collides with the cells it currently occupies and no drag is ever
        /// legal.
        /// </summary>
        public static bool IsLegalOnBoard(LevelDraft draft, BlockDraft dragged, Coord candidateOrigin)
        {
            foreach (var relative in dragged.Cells)
            {
                var cell = candidateOrigin + relative;
                if (cell.X < 0 || cell.X >= draft.Width || cell.Y < 0 || cell.Y >= draft.Height)
                {
                    return false;
                }

                if (draft.StaticWalls.Contains(cell))
                {
                    return false;
                }

                foreach (var other in draft.Blocks)
                {
                    if (!ReferenceEquals(other, dragged) && DraftHitTest.Covers(other.StartOrigin, other.Cells, cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// The wave-scope equivalent of <see cref="IsLegalOnBoard"/>: every cell
        /// inside <paramref name="elevator"/>'s region and clear of every other
        /// block already in <paramref name="wave"/>, <paramref name="dragged"/>
        /// excluded. <c>RegionOrigin</c> — and so <paramref name="candidateOrigin"/>
        /// — is relative to the region's own <c>Min</c> corner, not an absolute
        /// board position (<c>SpawnedBlock</c>'s doc: the absolute footprint is
        /// <c>regionMin + RegionOrigin + cell</c>), so the bounds check here is
        /// against the region's own width and height starting at (0, 0) — never
        /// against <paramref name="elevator"/>'s <c>Min</c>/<c>Max</c> directly.
        /// </summary>
        public static bool IsLegalInWave(ElevatorDraft elevator, WaveDraft wave, SpawnedBlockDraft dragged, Coord candidateOrigin)
        {
            var width = elevator.Max.X - elevator.Min.X + 1;
            var height = elevator.Max.Y - elevator.Min.Y + 1;

            foreach (var relative in dragged.Cells)
            {
                var cell = candidateOrigin + relative;
                if (cell.X < 0 || cell.X >= width || cell.Y < 0 || cell.Y >= height)
                {
                    return false;
                }

                foreach (var other in wave.Blocks)
                {
                    if (ReferenceEquals(other, dragged) || !other.RegionOrigin.HasValue)
                    {
                        continue;
                    }

                    if (DraftHitTest.Covers(other.RegionOrigin.Value, other.Cells, cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Applies a board drag if legal, leaving the draft untouched otherwise.</summary>
        public static bool TryApplyBoard(LevelDraft draft, BlockDraft dragged, Coord candidateOrigin)
        {
            if (!IsLegalOnBoard(draft, dragged, candidateOrigin))
            {
                return false;
            }

            dragged.StartOrigin = candidateOrigin;
            return true;
        }

        /// <summary>Applies a wave drag if legal, leaving the draft untouched otherwise.</summary>
        public static bool TryApplyWave(ElevatorDraft elevator, WaveDraft wave, SpawnedBlockDraft dragged, Coord candidateOrigin)
        {
            if (!IsLegalInWave(elevator, wave, dragged, candidateOrigin))
            {
                return false;
            }

            dragged.RegionOrigin = candidateOrigin;
            return true;
        }

        /// <summary>
        /// The rectangle a region drag-*create* describes between the press cell
        /// and the pointer's current cell, in either direction, clamped into the
        /// grid through <see cref="RegionBounds"/>.
        /// </summary>
        public static (Coord Min, Coord Max) RegionCreateRect(Coord anchorCell, Coord currentCell, int width, int height) =>
            RegionBounds.Clamped(anchorCell, currentCell, width, height);

        /// <summary>
        /// The rectangle a region drag-*move* describes: <paramref name="originalMin"/>/<paramref name="originalMax"/>
        /// shifted by the pointer's movement since the press cell, with the
        /// <em>delta itself</em> clamped so the region stops at the grid's edge
        /// without changing size.
        /// </summary>
        /// <remarks>
        /// This deliberately does not reuse <see cref="RegionBounds.Clamped"/>,
        /// which clamps each corner independently — correct for the A2 numeric
        /// fields it was written for, wrong for a move: a corner clamped to the
        /// edge while its opposite corner keeps sliding shrinks the region. That
        /// is also lossy (the extent does not come back on a drag away from the
        /// edge, and there is no undo yet to recover it), and worse on an
        /// elevator, whose every wave's tiling A2 already notes is invalidated by
        /// a region change. Resizing by dragging is out of scope for this round
        /// (docs/Modules/09a, Session B) regardless.
        /// </remarks>
        public static (Coord Min, Coord Max) RegionMoveRect(
            Coord originalMin, Coord originalMax, Coord anchorCell, Coord currentCell, int width, int height)
        {
            var rawDelta = currentCell - anchorCell;

            var deltaX = Clamp(rawDelta.X, -originalMin.X, (width - 1) - originalMax.X);
            var deltaY = Clamp(rawDelta.Y, -originalMin.Y, (height - 1) - originalMax.Y);
            var delta = new Coord(deltaX, deltaY);

            return (originalMin + delta, originalMax + delta);
        }

        private static int Clamp(int value, int lo, int hi) => value < lo ? lo : value > hi ? hi : value;
    }
}
