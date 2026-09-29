using System;
using System.Collections.Generic;
using System.Globalization;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where the marks on a block go (Module 16): the point its icons and
    /// count sit on, the chains across a locked block, and how wide a count's
    /// badge is. Plain rules in cell units, so they are tested without a scene.
    /// </summary>
    public static class MarkLayout
    {
        /// <summary>
        /// The centre of <paramref name="cells"/>, in cell units relative to the
        /// footprint's origin: the centre of its bounding box when that point
        /// lies on the footprint — on a cell, on a seam between two of its
        /// cells, or where four of its cells meet — so a 1×2 or a T is marked
        /// at its middle. Otherwise, as at the inside corner of an L, the centre
        /// of the footprint cell nearest it, the lowest row and then the
        /// leftmost cell winning a tie, so a mark never sits over a
        /// neighbouring block.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        public static Vector2 Anchor(IReadOnlyList<Coord> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A footprint has at least one cell.", nameof(cells));
            }

            Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);

            // Doubled coordinates keep the centre whole: an odd value is a
            // cell's middle on that axis, an even one a line between cells.
            var doubleX = minX + maxX + 1;
            var doubleY = minY + maxY + 1;
            var members = new HashSet<Coord>(cells);
            if (AreAllTouchingCellsMembers(members, doubleX, doubleY))
            {
                return new Vector2(doubleX * 0.5f, doubleY * 0.5f);
            }

            var best = cells[0];
            var bestDistance = int.MaxValue;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var dx = 2 * cell.X + 1 - doubleX;
                var dy = 2 * cell.Y + 1 - doubleY;
                var distance = dx * dx + dy * dy;
                if (distance < bestDistance
                    || (distance == bestDistance && (cell.Y < best.Y || (cell.Y == best.Y && cell.X < best.X))))
                {
                    best = cell;
                    bestDistance = distance;
                }
            }

            return new Vector2(best.X + 0.5f, best.Y + 0.5f);
        }

        /// <summary>
        /// The chains across a locked block: one horizontal strip for every run
        /// of neighbouring cells in each row of <paramref name="cells"/>,
        /// through the middle of that row, <paramref name="thicknessCells"/>
        /// thick, spanning only that run less <paramref name="endInsetCells"/> at
        /// each end. Every strip stays on its own cells, so a chain never draws
        /// over a neighbouring block in the empty corner of an L or a T. In cell
        /// units relative to the footprint's origin, by row and then from left
        /// to right.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="thicknessCells"/> is not in <c>(0, 1]</c>, or
        /// <paramref name="endInsetCells"/> is not in <c>[0, 0.5)</c>.
        /// </exception>
        public static IReadOnlyList<Rect> ChainStrips(IReadOnlyList<Coord> cells, float thicknessCells, float endInsetCells)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A footprint has at least one cell.", nameof(cells));
            }

            if (!(thicknessCells > 0f && thicknessCells <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(thicknessCells), thicknessCells, "A chain is above 0 and at most 1 cell thick.");
            }

            if (!(endInsetCells >= 0f && endInsetCells < 0.5f))
            {
                throw new ArgumentOutOfRangeException(nameof(endInsetCells), endInsetCells, "A chain's end inset is at least 0 and below half a cell.");
            }

            var sorted = new List<Coord>(cells);
            sorted.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

            var strips = new List<Rect>();
            var runStart = 0;
            for (var i = 1; i <= sorted.Count; i++)
            {
                var isRunOver = i == sorted.Count
                                || sorted[i].Y != sorted[i - 1].Y
                                || sorted[i].X != sorted[i - 1].X + 1;
                if (!isRunOver)
                {
                    continue;
                }

                var first = sorted[runStart];
                var last = sorted[i - 1];
                strips.Add(new Rect(
                    first.X + endInsetCells,
                    first.Y + 0.5f - thicknessCells * 0.5f,
                    last.X + 1 - first.X - 2f * endInsetCells,
                    thicknessCells));
                runStart = i;
            }

            return strips;
        }

        /// <summary>
        /// The width of the badge showing <paramref name="value"/>: its digits
        /// at <paramref name="digitWidth"/> each plus <paramref name="padding"/>
        /// on each side, and never narrower than it is tall, so a one-digit
        /// badge is square and a longer number widens it.
        /// </summary>
        public static float BadgeWidth(int value, float height, float digitWidth, float padding)
        {
            var digits = value.ToString(CultureInfo.InvariantCulture).Length;
            return Math.Max(height, digits * digitWidth + 2f * padding);
        }

        /// <summary>The lowest and highest cell coordinates of a footprint on each axis.</summary>
        internal static void Bounds(IReadOnlyList<Coord> cells, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;
            for (var i = 0; i < cells.Count; i++)
            {
                minX = Math.Min(minX, cells[i].X);
                maxX = Math.Max(maxX, cells[i].X);
                minY = Math.Min(minY, cells[i].Y);
                maxY = Math.Max(maxY, cells[i].Y);
            }
        }

        /// <summary>
        /// True when every cell touching the point at doubled coordinates
        /// (<paramref name="doubleX"/>, <paramref name="doubleY"/>) is in the
        /// footprint: the one cell it is the middle of, the two either side of
        /// a seam it lies on, or the four around a corner.
        /// </summary>
        private static bool AreAllTouchingCellsMembers(HashSet<Coord> members, int doubleX, int doubleY)
        {
            var xs = TouchingIndices(doubleX);
            var ys = TouchingIndices(doubleY);
            foreach (var x in xs)
            {
                foreach (var y in ys)
                {
                    if (!members.Contains(new Coord(x, y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>The cell indices on one axis a doubled coordinate touches: its own when odd, both neighbours when even.</summary>
        private static int[] TouchingIndices(int doubled) =>
            doubled % 2 != 0 ? new[] { (doubled - 1) / 2 } : new[] { doubled / 2 - 1, doubled / 2 };
    }
}
