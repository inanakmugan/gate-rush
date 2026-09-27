using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The rectangle each cell of a block's footprint is drawn as, so a
    /// multi-cell block reads as one shape rather than as separate squares. A
    /// cell is inset by half the cell gap only on the sides that face outside
    /// its own footprint; a side shared with another cell of the same block
    /// runs to the cell edge, so the two cells touch. Two different blocks side
    /// by side still show the full gap between them, half from each.
    /// </summary>
    /// <remarks>
    /// Rectangles are in cell units relative to the block's origin, the same
    /// frame as the footprint's cells: cell <c>(x, y)</c> covers
    /// <c>[x, x + 1) × [y, y + 1)</c> before insetting. The inner corner of an L
    /// needs no extra rectangle: the full-length shared sides of its cells
    /// already cover it.
    /// </remarks>
    public static class BlockCellRects
    {
        /// <summary>
        /// One rectangle per footprint cell, in the order of <paramref name="cells"/>.
        /// </summary>
        /// <param name="cells">The block's footprint.</param>
        /// <param name="gap">The gap between neighbouring cells, as a fraction of a cell (<c>RuntimeConfig.CellGap</c>).</param>
        /// <exception cref="ArgumentNullException"><paramref name="cells"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="gap"/> is negative or leaves no cell to draw.
        /// </exception>
        public static IReadOnlyList<Rect> Compute(IReadOnlyList<Coord> cells, float gap)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (gap < 0f || gap >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(gap), gap, "The cell gap must be in [0, 1).");
            }

            var half = gap * 0.5f;
            var rects = new Rect[cells.Count];

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var xMin = cell.X + (Contains(cells, cell + new Coord(-1, 0)) ? 0f : half);
                var xMax = cell.X + 1 - (Contains(cells, cell + new Coord(1, 0)) ? 0f : half);
                var yMin = cell.Y + (Contains(cells, cell + new Coord(0, -1)) ? 0f : half);
                var yMax = cell.Y + 1 - (Contains(cells, cell + new Coord(0, 1)) ? 0f : half);
                rects[i] = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }

            return rects;
        }

        /// <summary>Linear membership test; footprints are a handful of cells.</summary>
        private static bool Contains(IReadOnlyList<Coord> cells, Coord cell)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
