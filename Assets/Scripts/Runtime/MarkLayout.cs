using System;
using System.Collections.Generic;
using System.Globalization;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where the marks on a block go (Module 16): the point its icons and
    /// count sit on, where two marks sharing a block go (Module 19), the
    /// chains across a locked block, and how wide a count's or a time bonus's
    /// badge is. Plain rules in cell units, so they are tested without a scene.
    /// </summary>
    public static class MarkLayout
    {
        /// <summary>
        /// How far either side of the anchor two crowded marks sit, in cells: a
        /// quarter, so each has half a cell and neither leaves the anchor's
        /// cells. Geometry, not a tunable.
        /// </summary>
        public const float CrowdedOffsetCells = 0.25f;

        /// <summary>
        /// How far either side of the anchor two marks with room sit, in cells:
        /// half a cell, so each has a whole cell's width.
        /// </summary>
        public const float RoomyOffsetCells = 0.5f;

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
            DoubledAnchor(cells, out var doubleX, out var doubleY, out _);
            return new Vector2(doubleX * 0.5f, doubleY * 0.5f);
        }

        /// <summary>
        /// Where two marks sharing one block go — a padlock or a key, and the
        /// time-bonus mark (M10) — in cell units relative to the footprint's
        /// origin. They sit half a cell either side of <see cref="Anchor"/>,
        /// along the longer side of the footprint's bounding box (horizontal on
        /// a tie), when both of those points lie on the footprint: a 1×2 then
        /// carries one mark in each cell, at full size. Otherwise — a 1×1, an
        /// L, a T — they sit a quarter cell either side of the anchor,
        /// horizontally, which is always on the footprint, and are
        /// <see cref="PairedMarkLayout.IsCrowded"/>: the caller draws them
        /// smaller.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        public static PairedMarkLayout PairedMarks(IReadOnlyList<Coord> cells)
        {
            DoubledAnchor(cells, out var doubleX, out var doubleY, out var members);
            Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);

            // One step in doubled coordinates is half a cell.
            var isHorizontal = maxX - minX >= maxY - minY;
            var stepX = isHorizontal ? 1 : 0;
            var stepY = isHorizontal ? 0 : 1;
            if (AreAllTouchingCellsMembers(members, doubleX - stepX, doubleY - stepY)
                && AreAllTouchingCellsMembers(members, doubleX + stepX, doubleY + stepY))
            {
                return new PairedMarkLayout(
                    new Vector2((doubleX - stepX) * 0.5f, (doubleY - stepY) * 0.5f),
                    new Vector2((doubleX + stepX) * 0.5f, (doubleY + stepY) * 0.5f),
                    isCrowded: false);
            }

            // A quarter cell along x stays inside the anchor's own cell when
            // the anchor is a cell's middle on x, and inside the two cells
            // either side of it — both the block's — when it is on a seam.
            var anchor = new Vector2(doubleX * 0.5f, doubleY * 0.5f);
            var quarter = new Vector2(CrowdedOffsetCells, 0f);
            return new PairedMarkLayout(anchor - quarter, anchor + quarter, isCrowded: true);
        }

        /// <summary>
        /// The anchor of <paramref name="cells"/> in doubled coordinates, which
        /// keep it whole: an odd value is a cell's middle on that axis, an even
        /// one a line between cells. Every cell touching the anchor is in the
        /// footprint.
        /// </summary>
        private static void DoubledAnchor(
            IReadOnlyList<Coord> cells, out int doubleX, out int doubleY, out HashSet<Coord> members)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A footprint has at least one cell.", nameof(cells));
            }

            Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);

            doubleX = minX + maxX + 1;
            doubleY = minY + maxY + 1;
            members = new HashSet<Coord>(cells);
            if (AreAllTouchingCellsMembers(members, doubleX, doubleY))
            {
                return;
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

            doubleX = 2 * best.X + 1;
            doubleY = 2 * best.Y + 1;
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

        /// <summary>
        /// The width of a time-bonus mark (M10) showing a text of
        /// <paramref name="characters"/> characters: the clock icon, the gap
        /// after it, the characters at <paramref name="digitWidth"/> each, and
        /// <paramref name="padding"/> on each side; never narrower than it is
        /// tall.
        /// </summary>
        public static float BonusBadgeWidth(
            int characters, float height, float digitWidth, float padding, float iconWidth, float iconGap) =>
            Math.Max(height, iconWidth + iconGap + characters * digitWidth + 2f * padding);

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

    /// <summary>Where two marks sharing one block go (<see cref="MarkLayout.PairedMarks"/>).</summary>
    public readonly struct PairedMarkLayout
    {
        /// <summary>A layout for two marks.</summary>
        public PairedMarkLayout(Vector2 first, Vector2 second, bool isCrowded)
        {
            First = first;
            Second = second;
            IsCrowded = isCrowded;
        }

        /// <summary>The mark on the left, or the lower one of a vertical pair: the padlock or key.</summary>
        public Vector2 First { get; }

        /// <summary>The mark on the right, or the upper one of a vertical pair: the time bonus.</summary>
        public Vector2 Second { get; }

        /// <summary>True when the two share half a cell each and must be drawn smaller.</summary>
        public bool IsCrowded { get; }
    }
}
