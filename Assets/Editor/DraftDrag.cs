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
        /// Whether <paramref name="cell"/> lies within the board's own grid. The
        /// single definition of "on the board", shared by drag validation and by
        /// the window's placement check — a preset footprint reaching past the
        /// right edge and a dragged block reaching past it are the same question
        /// and must not be answered twice.
        /// </summary>
        public static bool IsInsideBoard(LevelDraft draft, Coord cell) =>
            cell.X >= 0 && cell.X < draft.Width && cell.Y >= 0 && cell.Y < draft.Height;

        /// <summary>
        /// The wave-scope equivalent of <see cref="IsInsideBoard"/>.
        /// <paramref name="cell"/> is relative to the region's own <c>Min</c>
        /// corner, never a board position, so the bounds are the region's width
        /// and height measured from (0, 0) — the same convention
        /// <c>SpawnedBlock.RegionOrigin</c> documents.
        /// </summary>
        public static bool IsInsideRegion(ElevatorDraft elevator, Coord cell)
        {
            var width = elevator.Max.X - elevator.Min.X + 1;
            var height = elevator.Max.Y - elevator.Min.Y + 1;
            return cell.X >= 0 && cell.X < width && cell.Y >= 0 && cell.Y < height;
        }

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
                if (!IsInsideBoard(draft, cell))
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
            foreach (var relative in dragged.Cells)
            {
                var cell = candidateOrigin + relative;
                if (!IsInsideRegion(elevator, cell))
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
        /// The offset a gate or generator drag should land at, given how far
        /// along the candidate edge the pointer now is and which cell of the
        /// marker was grabbed — the edge-feature counterpart of
        /// <see cref="CandidateOrigin"/>, and the same reasoning: the grabbed
        /// cell stays under the pointer instead of the feature's offset snapping
        /// to it.
        /// </summary>
        /// <remarks>
        /// <paramref name="grabOffset"/> is an index into the marker's own span
        /// — "the second cell of this three-wide gate" — not a board
        /// coordinate. That is why a drag that crosses onto another edge needs
        /// no special case here: the index means the same thing on the new edge,
        /// so the grabbed part of the marker stays under the pointer even though
        /// the axis being measured has changed from X to Y or back.
        /// </remarks>
        public static int CandidateEdgeOffset(int pointerAlongEdge, int grabOffset) =>
            pointerAlongEdge - grabOffset;

        /// <summary>
        /// Whether <paramref name="feature"/> — a <see cref="GateDraft"/> or a
        /// <see cref="GeneratorDraft"/> — could legally sit at
        /// <paramref name="candidateOffset"/> on <paramref name="candidateEdge"/>:
        /// its whole span inside that edge, and clear of every other edge
        /// feature's span on it (M6). The feature's own current span is
        /// excluded, without which a feature always collides with itself and no
        /// drag is ever legal — the same exclusion
        /// <see cref="IsLegalOnBoard"/> makes for a block's own cells.
        /// </summary>
        /// <remarks>
        /// A drag may carry a feature onto a different edge, which is why the
        /// edge is a parameter rather than read from the feature: the candidate
        /// is a whole placement, not an offset along a fixed edge. What governs
        /// it is the destination — the span has to fit the edge it is landing
        /// on, whose length differs between the horizontal and vertical pairs,
        /// and has to be clear of that edge's own features rather than the
        /// departed one's.
        /// </remarks>
        public static bool IsLegalEdgeFeature(
            LevelDraft draft, object feature, BoardEdge candidateEdge, int candidateOffset)
        {
            var span = EdgeFeatures.Of(feature);
            if (!span.HasValue)
            {
                return false;
            }

            return EdgeFeatures.FitsOnEdge(draft, candidateEdge, candidateOffset, span.Value.Width)
                   && EdgeFeatures.Blocking(draft, candidateEdge, candidateOffset, span.Value.Width, feature) == null;
        }

        /// <summary>
        /// Applies an edge-feature drag if legal, leaving the draft untouched
        /// otherwise. One method for both kinds rather than a gate copy and a
        /// generator copy: M6 governs them identically, and two near-identical
        /// paths here would be two places for that rule to drift. Both the edge
        /// and the offset are written, since a drag can move a feature from one
        /// edge to another.
        /// </summary>
        public static bool TryApplyEdgeFeature(
            LevelDraft draft, object feature, BoardEdge candidateEdge, int candidateOffset)
        {
            if (!IsLegalEdgeFeature(draft, feature, candidateEdge, candidateOffset))
            {
                return false;
            }

            switch (feature)
            {
                case GateDraft gate:
                    gate.Edge = candidateEdge;
                    gate.Offset = candidateOffset;
                    return true;
                case GeneratorDraft generator:
                    generator.Edge = candidateEdge;
                    generator.Offset = candidateOffset;
                    return true;
                default:
                    return false;
            }
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

            var deltaX = ClampDelta(rawDelta.X, -originalMin.X, (width - 1) - originalMax.X);
            var deltaY = ClampDelta(rawDelta.Y, -originalMin.Y, (height - 1) - originalMax.Y);
            var delta = new Coord(deltaX, deltaY);

            return (originalMin + delta, originalMax + delta);
        }

        /// <summary>
        /// Clamps a move's delta, treating an inverted range as "no movement
        /// allowed on this axis". The range inverts when the region is already
        /// larger than the grid on that axis — no delta can put it wholly
        /// inside, so both bounds are unsatisfiable at once and a plain clamp
        /// would honour whichever one it tested second, shoving the region out
        /// of bounds instead of leaving it where it is. Only hand-edited JSON
        /// can produce such a region, which is precisely the file this editor
        /// exists to repair (docs/Modules/09a).
        /// </summary>
        private static int ClampDelta(int value, int lo, int hi) => lo > hi ? 0 : Clamp(value, lo, hi);

        private static int Clamp(int value, int lo, int hi) => value < lo ? lo : value > hi ? hi : value;
    }
}
