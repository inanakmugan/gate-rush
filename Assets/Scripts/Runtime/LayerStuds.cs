using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where a layered block's inner block, in the colour beneath, puts its
    /// studs (Module 46): on its own grid, centred, as many as fit its face,
    /// at the normal stud size. A 2×2 block's inner block holds 3×3; a 1×1's,
    /// a 1×2's and an L's, whose inner arms are narrower than a stud, hold none.
    /// </summary>
    /// <remarks>
    /// <para><b>The grid.</b> Along each axis, as many points as fit the
    /// inner block's bounding box less the clearance on both sides, spaced
    /// evenly and centred on that box.</para>
    /// <para><b>The fit.</b> A point is kept when the square of the
    /// clearance around it lies inside the inner block, which is the footprint
    /// shrunk by the inset on every outward side (<see cref="FaceShape"/>).
    /// That holds exactly when the square, grown by the inset, covers only
    /// the footprint's own cells, which works for any shape. The rounding of the
    /// inner block's corners is left to the clearance, which StudFitTests
    /// checks against the art recipe's stud.</para>
    /// </remarks>
    public static class LayerStuds
    {
        /// <summary>
        /// The centres of the inner studs, in cells in the frame of
        /// <paramref name="cells"/>, row by row from the bottom and left to right.
        /// </summary>
        /// <param name="cells">The block's footprint.</param>
        /// <param name="innerInsetCells">How far the inner block sits inside the footprint (<see cref="RuntimeConfig.LayerInnerInsetCells"/>).</param>
        /// <param name="spacingCells">The inner grid's spacing (<see cref="RuntimeConfig.LayerStudSpacingCells"/>).</param>
        /// <param name="clearanceCells">How far a stud's centre stays from the inner block's edge (<see cref="RuntimeConfig.LayerStudClearanceCells"/>).</param>
        /// <exception cref="ArgumentNullException"><paramref name="cells"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="spacingCells"/> is not positive.</exception>
        public static IReadOnlyList<Vector2> Centers(
            IReadOnlyList<Coord> cells, float innerInsetCells, float spacingCells, float clearanceCells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (!(spacingCells > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(spacingCells), spacingCells, "The inner grid's spacing must be positive.");
            }

            var centers = new List<Vector2>();
            if (cells.Count == 0)
            {
                return centers;
            }

            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            var left = minX + innerInsetCells;
            var right = maxX + 1 - innerInsetCells;
            var bottom = minY + innerInsetCells;
            var top = maxY + 1 - innerInsetCells;

            var columns = CountAlong(right - left, spacingCells, clearanceCells);
            var rows = CountAlong(top - bottom, spacingCells, clearanceCells);
            var members = new HashSet<Coord>(cells);
            var reach = clearanceCells + innerInsetCells;

            for (var row = 0; row < rows; row++)
            {
                var y = (bottom + top) * 0.5f + (row - (rows - 1) * 0.5f) * spacingCells;
                for (var column = 0; column < columns; column++)
                {
                    var x = (left + right) * 0.5f + (column - (columns - 1) * 0.5f) * spacingCells;
                    if (CoversOnlyMembers(members, x - reach, x + reach, y - reach, y + reach))
                    {
                        centers.Add(new Vector2(x, y));
                    }
                }
            }

            return centers;
        }

        /// <summary>How many points, spaced by <paramref name="spacing"/>, fit an extent once the clearance is taken from both ends.</summary>
        private static int CountAlong(float extent, float spacing, float clearance)
        {
            var usable = extent - 2f * clearance;
            return usable < 0f ? 0 : (int)Math.Floor(usable / spacing) + 1;
        }

        /// <summary>True when every cell the rectangle overlaps belongs to the footprint.</summary>
        private static bool CoversOnlyMembers(HashSet<Coord> members, float xMin, float xMax, float yMin, float yMax)
        {
            var firstColumn = (int)Math.Floor(xMin);
            var lastColumn = (int)Math.Ceiling(xMax) - 1;
            var firstRow = (int)Math.Floor(yMin);
            var lastRow = (int)Math.Ceiling(yMax) - 1;
            for (var x = firstColumn; x <= lastColumn; x++)
            {
                for (var y = firstRow; y <= lastRow; y++)
                {
                    if (!members.Contains(new Coord(x, y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
