using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The board frame as quarter tiles (Module 15): a ring of cells just
    /// outside the grid plus every static wall cell, tiled with the same rule
    /// as a block, so walls join the frame where they touch it and a notch in
    /// the board's outline turns into a concave corner.
    /// </summary>
    /// <remarks>
    /// <para><b>The ring.</b> Frame cells lie on columns <c>−1</c> and
    /// <c>Width</c> and rows <c>−1</c> and <c>Height</c>, corners included. The
    /// ring cells under a gate's span are left out: a gate replaces the frame
    /// segment it sits on and is drawn as its own piece
    /// (<see cref="GateCells"/>). Gate positions never change, so a closed
    /// gate leaves the same hole. Generators do not: the frame runs on under
    /// them.</para>
    /// <para><b>Thickness.</b> The ring is drawn thinner than a cell.
    /// <see cref="QuarterRect"/> squeezes column <c>−1</c> onto <c>[−t, 0]</c>
    /// and column <c>Width</c> onto <c>[Width, Width + t]</c>, and the same for
    /// rows, while every cell inside the grid keeps its full size. Two quarters
    /// meeting at a seam always share their scale along it, so boundary lines
    /// still meet.</para>
    /// </remarks>
    public static class FrameTiling
    {
        /// <summary>
        /// The frame's quarters: the ring cells not under a gate, row by row
        /// from the bottom, then every static wall cell in level order.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is null.</exception>
        public static IReadOnlyList<QuarterTile> Compute(LevelContext ctx)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            var underGates = new HashSet<Coord>();
            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                underGates.UnionWith(GateCells(ctx, g));
            }

            var cells = new List<Coord>();
            for (var y = -1; y <= ctx.Height; y++)
            {
                for (var x = -1; x <= ctx.Width; x++)
                {
                    var cell = new Coord(x, y);
                    if (IsRing(ctx, cell) && !underGates.Contains(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            cells.AddRange(ctx.StaticWalls);
            var members = new HashSet<Coord>(cells);
            return QuarterTiling.For(cells, members.Contains);
        }

        /// <summary>
        /// The ring cells gate <paramref name="gateIndex"/> covers, from the low
        /// end of its span: its piece of the frame, tiled like a block.
        /// </summary>
        public static IReadOnlyList<Coord> GateCells(LevelContext ctx, int gateIndex)
        {
            var gate = ctx.Gates[gateIndex];
            var cells = new Coord[gate.Width];
            for (var k = 0; k < gate.Width; k++)
            {
                cells[k] = RingCell(ctx.Width, ctx.Height, gate.Edge, gate.Offset + k);
            }

            return cells;
        }

        /// <summary>
        /// The rectangle <paramref name="tile"/> covers, in grid units, with the
        /// ring squeezed to <paramref name="thicknessCells"/>. A tile inside the
        /// grid covers the same rectangle as <see cref="BlockTiling.QuarterRect"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="thicknessCells"/> is not in <c>(0, 1]</c>, or the tile's
        /// cell lies beyond the ring.
        /// </exception>
        public static Rect QuarterRect(QuarterTile tile, int width, int height, float thicknessCells)
        {
            CheckThickness(thicknessCells);
            var (xMin, xMax) = QuarterSpan(tile.Cell.X, width, tile.SignX, thicknessCells);
            var (yMin, yMax) = QuarterSpan(tile.Cell.Y, height, tile.SignY, thicknessCells);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// The rectangle, in grid units, of the ring segment
        /// <paramref name="span"/> cells long starting at <paramref name="offset"/>
        /// along <paramref name="edge"/>, at the ring's squeezed thickness: where
        /// an edge feature's bar is drawn. It covers exactly the quarters of
        /// those ring cells.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="thicknessCells"/> is not in <c>(0, 1]</c>.</exception>
        public static Rect EdgeSpanRect(int width, int height, BoardEdge edge, int offset, int span, float thicknessCells)
        {
            CheckThickness(thicknessCells);
            switch (edge)
            {
                case BoardEdge.Bottom:
                    return new Rect(offset, -thicknessCells, span, thicknessCells);
                case BoardEdge.Top:
                    return new Rect(offset, height, span, thicknessCells);
                case BoardEdge.Left:
                    return new Rect(-thicknessCells, offset, thicknessCells, span);
                default:
                    return new Rect(width, offset, thicknessCells, span);
            }
        }

        private static bool IsRing(LevelContext ctx, Coord cell) =>
            cell.X == -1 || cell.X == ctx.Width || cell.Y == -1 || cell.Y == ctx.Height;

        /// <summary>The ring cell just outside <paramref name="edge"/> at position <paramref name="along"/> along it.</summary>
        private static Coord RingCell(int width, int height, BoardEdge edge, int along)
        {
            switch (edge)
            {
                case BoardEdge.Bottom:
                    return new Coord(along, -1);
                case BoardEdge.Top:
                    return new Coord(along, height);
                case BoardEdge.Left:
                    return new Coord(-1, along);
                default:
                    return new Coord(width, along);
            }
        }

        /// <summary>
        /// One axis of a quarter's rectangle: the cell's extent on that axis —
        /// squeezed to the thickness on the ring — and the half of it the
        /// quarter's sign names.
        /// </summary>
        private static (float min, float max) QuarterSpan(int cell, int count, int sign, float thicknessCells)
        {
            float low;
            float high;
            if (cell == -1)
            {
                low = -thicknessCells;
                high = 0f;
            }
            else if (cell == count)
            {
                low = count;
                high = count + thicknessCells;
            }
            else if (cell >= 0 && cell < count)
            {
                low = cell;
                high = cell + 1;
            }
            else
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cell), cell, $"A frame cell lies on the ring or inside the grid, from -1 to {count}.");
            }

            var mid = (low + high) * 0.5f;
            return sign > 0 ? (mid, high) : (low, mid);
        }

        private static void CheckThickness(float thicknessCells)
        {
            if (!(thicknessCells > 0f && thicknessCells <= 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(thicknessCells), thicknessCells, "The frame thickness must be above 0 and at most 1 cell.");
            }
        }
    }
}
