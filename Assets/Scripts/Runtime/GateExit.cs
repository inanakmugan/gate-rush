using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The geometry of a destroyed block passing through its gate (Module 18):
    /// how far it travels, the area that clips it at the gate's inner line,
    /// and where the gate's inward glow sits. Plain and in grid units — cells,
    /// with <c>(0, 0)</c> the grid's lower-left corner — so it is tested
    /// without a scene.
    /// </summary>
    public static class GateExit
    {
        /// <summary>The direction out of the board through <paramref name="edge"/>.</summary>
        public static Vector2 Outward(BoardEdge edge)
        {
            switch (edge)
            {
                case BoardEdge.Top:
                    return Vector2.up;
                case BoardEdge.Bottom:
                    return Vector2.down;
                case BoardEdge.Left:
                    return Vector2.left;
                default:
                    return Vector2.right;
            }
        }

        /// <summary>
        /// The block's depth along the exit direction: the span of its
        /// footprint's bounding box on that axis, in cells. Also how far it
        /// travels to be wholly through the gate's inner line, on every edge:
        /// nothing of a block is drawn outside its footprint's cells.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="cells"/> is null.</exception>
        public static int DepthCells(IReadOnlyList<Coord> cells, BoardEdge edge)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            return edge == BoardEdge.Left || edge == BoardEdge.Right ? maxX + 1 - minX : maxY + 1 - minY;
        }

        /// <summary>
        /// How far past the footprint a lifted block can reach on any side, in
        /// cells: its outline's width (<see cref="LiftOutline"/>) grown by the
        /// lift scale, plus how far the lift scale pushes the footprint's own
        /// edge out from its centre, <c>(scale − 1) × half its larger
        /// extent</c>. A block at rest reaches nowhere past its footprint: its
        /// lip is inside its cells (<see cref="FaceShape"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="cells"/> is null.</exception>
        public static float OverhangCells(IReadOnlyList<Coord> cells, float liftScale, float outlineWidthCells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            var halfExtent = Math.Max(maxX + 1 - minX, maxY + 1 - minY) * 0.5f;
            return liftScale * outlineWidthCells + (liftScale - 1f) * halfExtent;
        }

        /// <summary>
        /// The area a passing block stays visible in: the grid, with its side
        /// on <paramref name="exitEdge"/> exactly the gate's inner line — where
        /// the block is cut — and its other three sides pushed out by
        /// <paramref name="marginCells"/>, so a lifted block on a border row
        /// keeps the outline that reaches past the grid there.
        /// </summary>
        public static Rect MaskRect(int width, int height, BoardEdge exitEdge, float marginCells)
        {
            var xMin = exitEdge == BoardEdge.Left ? 0f : -marginCells;
            var xMax = exitEdge == BoardEdge.Right ? width : width + marginCells;
            var yMin = exitEdge == BoardEdge.Bottom ? 0f : -marginCells;
            var yMax = exitEdge == BoardEdge.Top ? height : height + marginCells;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// Whether a block's own footprint, drawn with its origin at
        /// <paramref name="drawnOrigin"/>, reaches past the inner line of
        /// <paramref name="edge"/> — the grid's edge on that side: a dragged
        /// block nudged into its gate's mouth (Module 22). The footprint only:
        /// a lifted block's outline reaching a little over the frame does not
        /// count, so a block sliding along an edge keeps its outline there. A
        /// block exactly flush against the edge does not cross it.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="cells"/> is null.</exception>
        public static bool FootprintCrossesInnerLine(
            int width, int height, BoardEdge edge, IReadOnlyList<Coord> cells, Vector2 drawnOrigin)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            switch (edge)
            {
                case BoardEdge.Top:
                    return drawnOrigin.y + maxY + 1 > height;
                case BoardEdge.Bottom:
                    return drawnOrigin.y + minY < 0f;
                case BoardEdge.Left:
                    return drawnOrigin.x + minX < 0f;
                default:
                    return drawnOrigin.x + maxX + 1 > width;
            }
        }

        /// <summary>
        /// The band just inside a gate the glow covers: the gate's span along
        /// its edge, <paramref name="depthCells"/> into the board from its inner
        /// line.
        /// </summary>
        public static Rect GlowRect(int width, int height, BoardEdge edge, int offset, int span, float depthCells)
        {
            switch (edge)
            {
                case BoardEdge.Top:
                    return new Rect(offset, height - depthCells, span, depthCells);
                case BoardEdge.Bottom:
                    return new Rect(offset, 0f, span, depthCells);
                case BoardEdge.Left:
                    return new Rect(0f, offset, depthCells, span);
                default:
                    return new Rect(width - depthCells, offset, depthCells, span);
            }
        }

        /// <summary>
        /// The gate's outer line: the grid's edge pushed outward by the frame's
        /// thickness, across the gate's span, from its low end to its high end.
        /// Cubes stream out from here, beneath the gate and outside the board.
        /// </summary>
        public static void OuterLine(
            int width, int height, BoardEdge edge, int offset, int span, float frameThicknessCells,
            out Vector2 from, out Vector2 to)
        {
            switch (edge)
            {
                case BoardEdge.Top:
                    from = new Vector2(offset, height + frameThicknessCells);
                    to = new Vector2(offset + span, height + frameThicknessCells);
                    return;
                case BoardEdge.Bottom:
                    from = new Vector2(offset, -frameThicknessCells);
                    to = new Vector2(offset + span, -frameThicknessCells);
                    return;
                case BoardEdge.Left:
                    from = new Vector2(-frameThicknessCells, offset);
                    to = new Vector2(-frameThicknessCells, offset + span);
                    return;
                default:
                    from = new Vector2(width + frameThicknessCells, offset);
                    to = new Vector2(width + frameThicknessCells, offset + span);
                    return;
            }
        }
    }
}
