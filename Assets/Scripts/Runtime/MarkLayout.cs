using System;
using System.Collections.Generic;
using System.Globalization;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where the marks on a block go (Module 16): the point its icons and
    /// count sit on, where two or three marks sharing a block go (Modules 19
    /// and 20), the chains across a locked block, how wide a count's badge is
    /// and how much width a time bonus has. Plain rules in cell units, so they are tested
    /// without a scene.
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
        /// How far either side of the anchor the outer two of three crowded
        /// marks sit, in cells: a third, so each has a third of a cell and
        /// none leaves the anchor's cells. Geometry, not a tunable.
        /// </summary>
        public const float CrowdedTripleOffsetCells = 1f / 3f;

        /// <summary>The most marks <see cref="Row"/> places: a padlock or key, a layer count and a time bonus.</summary>
        public const int MostMarksInARow = 3;

        /// <summary>
        /// The width a mark sharing its block has, in cells: one cell, as two
        /// marks with room sit a cell apart, and crowded ones share a cell at
        /// a smaller scale. Geometry, not a tunable.
        /// </summary>
        public const float SharedMarkRoomCells = 2f * RoomyOffsetCells;

        /// <summary>
        /// The centre of <paramref name="cells"/>, in cell units relative to the
        /// footprint's origin, where a block's marks sit. On an L or a T, the
        /// middle of the cell where its arms meet (<see cref="TryJunction"/>),
        /// so the mark reads as on the block, not at one of its tips. Otherwise
        /// the centre of its bounding box when that point lies on the
        /// footprint — on a cell, on a seam between two of its cells, or where
        /// four of its cells meet — so a 1×2 is marked at its middle; and
        /// failing that the centre of the footprint cell nearest it, the lowest
        /// row and then the leftmost cell winning a tie, so a mark never sits
        /// over a neighbouring block.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        public static Vector2 Anchor(IReadOnlyList<Coord> cells)
        {
            DoubledAnchor(cells, out var doubleX, out var doubleY, out _);
            return new Vector2(doubleX * 0.5f, doubleY * 0.5f);
        }

        /// <summary>
        /// Where a block's count badge goes on the board (Module 46): the
        /// outer bottom-right corner of the footprint's bottom-right cell —
        /// the right-most cell of its lowest row, so an L's is its foot's end
        /// — pulled in by <paramref name="insetCells"/> on both axes, so the
        /// badge sits over the corner and overlaps the block's edge, clear of
        /// the window of a layered block. In cell units relative to the
        /// footprint's origin.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        public static Vector2 CornerBadge(IReadOnlyList<Coord> cells, float insetCells)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A footprint has at least one cell.", nameof(cells));
            }

            var corner = cells[0];
            for (var i = 1; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.Y < corner.Y || (cell.Y == corner.Y && cell.X > corner.X))
                {
                    corner = cell;
                }
            }

            return new Vector2(corner.X + 1 - insetCells, corner.Y + insetCells);
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
            var row = Row(cells, 2);
            return new PairedMarkLayout(row[0], row[1], row.IsCrowded);
        }

        /// <summary>
        /// Where <paramref name="count"/> marks sharing one block go — a
        /// padlock or a key, the layer count (M4) and the time-bonus mark
        /// (M10), in that order — in cell units relative to the footprint's
        /// origin, from left to right or from the lowest up.
        /// <list type="bullet">
        /// <item>One mark sits on <see cref="Anchor"/>.</item>
        /// <item>Two follow <see cref="PairedMarks"/>' rule: half a cell
        /// either side of the anchor along the longer side of the bounding
        /// box where both points lie on the footprint, otherwise a quarter
        /// cell either side of it, horizontally, crowded.</item>
        /// <item>Three sit on the anchor and a whole cell either side of it,
        /// along that longer side (horizontal on a tie), when both of those
        /// points lie on the footprint: a 1×3 carries one mark in each cell,
        /// at full size. Otherwise they sit on the anchor and a third of a
        /// cell either side of it, horizontally, and are
        /// <see cref="MarkRowLayout.IsCrowded"/>: the caller draws them
        /// smaller.</item>
        /// </list>
        /// Every slot lies on the footprint.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not from 1 to <see cref="MostMarksInARow"/>.</exception>
        public static MarkRowLayout Row(IReadOnlyList<Coord> cells, int count)
        {
            if (count < 1 || count > MostMarksInARow)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, $"A block carries 1 to {MostMarksInARow} marks in a row.");
            }

            DoubledAnchor(cells, out var doubleX, out var doubleY, out var members);
            var anchor = new Vector2(doubleX * 0.5f, doubleY * 0.5f);
            if (count == 1)
            {
                return new MarkRowLayout(new[] { anchor }, isCrowded: false);
            }

            Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);

            // One step in doubled coordinates is half a cell: two marks sit
            // one step either side of the anchor, the outer two of three sit
            // two steps either side of it.
            var isHorizontal = maxX - minX >= maxY - minY;
            var reach = count == 2 ? 1 : 2;
            var stepX = isHorizontal ? reach : 0;
            var stepY = isHorizontal ? 0 : reach;
            if (AreAllTouchingCellsMembers(members, doubleX - stepX, doubleY - stepY)
                && AreAllTouchingCellsMembers(members, doubleX + stepX, doubleY + stepY))
            {
                var low = new Vector2((doubleX - stepX) * 0.5f, (doubleY - stepY) * 0.5f);
                var high = new Vector2((doubleX + stepX) * 0.5f, (doubleY + stepY) * 0.5f);
                return new MarkRowLayout(count == 2 ? new[] { low, high } : new[] { low, anchor, high }, isCrowded: false);
            }

            // Less than half a cell along x stays inside the anchor's own cell
            // when the anchor is a cell's middle on x, and inside the two
            // cells either side of it — both the block's — when it is on a
            // seam.
            var offset = new Vector2(count == 2 ? CrowdedOffsetCells : CrowdedTripleOffsetCells, 0f);
            return new MarkRowLayout(
                count == 2 ? new[] { anchor - offset, anchor + offset } : new[] { anchor - offset, anchor, anchor + offset },
                isCrowded: true);
        }

        /// <summary>
        /// Where a block's padlock or key, its layer count and its time-bonus
        /// mark sit, in cell units relative to the footprint's origin, and how
        /// crowded they are (Module 24). One of them alone sits on
        /// <see cref="Anchor"/>, uncrowded; two or three take
        /// <see cref="Row"/>'s slots in that order. On a frozen block every
        /// slot is raised by <paramref name="frozenRaiseCells"/>, above the
        /// frozen count, which stays on the anchor. A mark the block has not
        /// keeps the (raised) anchor and is never drawn. The one rule for the
        /// board and the Level Editor: each multiplies by its own cell size.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        public static BlockMarkSlots Slots(
            IReadOnlyList<Coord> cells, bool isFrozen, bool hasIcon, bool hasLayers, bool hasBonus, float frozenRaiseCells)
        {
            var raise = isFrozen ? new Vector2(0f, frozenRaiseCells) : Vector2.zero;
            var anchor = Anchor(cells) + raise;
            var count = (hasIcon ? 1 : 0) + (hasLayers ? 1 : 0) + (hasBonus ? 1 : 0);
            if (count < 2)
            {
                return new BlockMarkSlots(anchor, anchor, anchor, MarkCrowding.Roomy);
            }

            var row = Row(cells, count);
            var crowding = !row.IsCrowded ? MarkCrowding.Roomy
                : count == 2 ? MarkCrowding.CrowdedPair
                : MarkCrowding.CrowdedTriple;

            // The row's slots go to the marks the block has, in the row's
            // order.
            var slot = 0;
            var icon = hasIcon ? row[slot++] + raise : anchor;
            var layers = hasLayers ? row[slot++] + raise : anchor;
            var bonus = hasBonus ? row[slot] + raise : anchor;
            return new BlockMarkSlots(icon, layers, bonus, crowding);
        }

        /// <summary>
        /// The M7 arrow of an axis-restricted block, in cell units relative to
        /// the footprint's origin: a rectangle centred on the centre of the
        /// footprint's bounding box, running along <paramref name="axis"/> for
        /// the bounding box's length less <paramref name="endInsetCells"/> at
        /// each end, <paramref name="thicknessCells"/> across. The one rule for
        /// the board and the Level Editor (Module 24).
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="axis"/> is <see cref="MovementAxis.Free"/>: a free block has no arrow.</exception>
        public static Rect AxisArrowRect(IReadOnlyList<Coord> cells, MovementAxis axis, float thicknessCells, float endInsetCells)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A footprint has at least one cell.", nameof(cells));
            }

            if (axis != MovementAxis.HorizontalOnly && axis != MovementAxis.VerticalOnly)
            {
                throw new ArgumentOutOfRangeException(nameof(axis), axis, "Only an axis-restricted block has an arrow.");
            }

            Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            var isHorizontal = axis == MovementAxis.HorizontalOnly;
            var alongCells = isHorizontal ? maxX + 1 - minX : maxY + 1 - minY;
            var length = alongCells - 2f * endInsetCells;
            var size = isHorizontal ? new Vector2(length, thicknessCells) : new Vector2(thicknessCells, length);
            var center = new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);
            return new Rect(center - size * 0.5f, size);
        }

        /// <summary>
        /// The cell where an L's or a T's arms meet, if <paramref name="cells"/>
        /// is one, in any rotation or mirror and with any arm lengths: the
        /// footprint is two straight runs of cells — one along a row, one along
        /// a column — crossing at exactly one cell, which is the only cell with
        /// a neighbour in the block both across and up or down. With two
        /// neighbours it is an L's corner; with three, a T's junction. Any other
        /// shape — a 1×1, a bar, a rectangle, an S or a Z, a plus (four
        /// neighbours), or any polyomino with a cell off those two runs — has
        /// none, and its marks keep <see cref="Anchor"/>'s other rule.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        public static bool TryJunction(IReadOnlyList<Coord> cells, out Coord junction)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A footprint has at least one cell.", nameof(cells));
            }

            return TryJunction(cells, new HashSet<Coord>(cells), out junction);
        }

        /// <summary><see cref="TryJunction(IReadOnlyList{Coord}, out Coord)"/> on the footprint's <paramref name="members"/>.</summary>
        private static bool TryJunction(IReadOnlyList<Coord> cells, HashSet<Coord> members, out Coord junction)
        {
            junction = default;
            var candidates = 0;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var isAcross = members.Contains(new Coord(cell.X - 1, cell.Y)) || members.Contains(new Coord(cell.X + 1, cell.Y));
                var isUpOrDown = members.Contains(new Coord(cell.X, cell.Y - 1)) || members.Contains(new Coord(cell.X, cell.Y + 1));
                if (isAcross && isUpOrDown)
                {
                    junction = cell;
                    candidates++;
                }
            }

            if (candidates != 1)
            {
                return false;
            }

            // Every cell on the junction's row or column, and those on each
            // reached from the junction without a gap: two straight runs.
            var row = RunLength(members, junction, 1, 0) + RunLength(members, junction, -1, 0);
            var column = RunLength(members, junction, 0, 1) + RunLength(members, junction, 0, -1);
            if (row + column + 1 != cells.Count)
            {
                return false;
            }

            var neighbours = Math.Min(1, RunLength(members, junction, 1, 0)) + Math.Min(1, RunLength(members, junction, -1, 0))
                             + Math.Min(1, RunLength(members, junction, 0, 1)) + Math.Min(1, RunLength(members, junction, 0, -1));
            return neighbours == CornerNeighbours || neighbours == JunctionNeighbours;
        }

        /// <summary>An L's corner has two neighbours in the block, a T's junction three; a plus's four is neither. Geometry, not a tunable.</summary>
        private const int CornerNeighbours = 2;

        private const int JunctionNeighbours = 3;

        /// <summary>How many of the block's cells follow <paramref name="from"/> without a gap, one step of (<paramref name="dx"/>, <paramref name="dy"/>) at a time.</summary>
        private static int RunLength(HashSet<Coord> members, Coord from, int dx, int dy)
        {
            var length = 0;
            while (members.Contains(new Coord(from.X + dx * (length + 1), from.Y + dy * (length + 1))))
            {
                length++;
            }

            return length;
        }

        /// <summary>
        /// The anchor of <paramref name="cells"/> in doubled coordinates, which
        /// keep it whole: an odd value is a cell's middle on that axis, an even
        /// one a line between cells. Every cell touching the anchor is in the
        /// footprint. The junction of an L or a T (<see cref="TryJunction"/>)
        /// first; otherwise the rule <see cref="Anchor"/> describes.
        /// </summary>
        private static void DoubledAnchor(
            IReadOnlyList<Coord> cells, out int doubleX, out int doubleY, out HashSet<Coord> members)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A footprint has at least one cell.", nameof(cells));
            }

            members = new HashSet<Coord>(cells);
            if (TryJunction(cells, members, out var junction))
            {
                doubleX = 2 * junction.X + 1;
                doubleY = 2 * junction.Y + 1;
                return;
            }

            Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);

            doubleX = minX + maxX + 1;
            doubleY = minY + maxY + 1;
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
        /// The width, in cells, the time-bonus mark (M10) of a block with
        /// footprint <paramref name="cells"/> has, centred on its slot, before
        /// it is fitted (<see cref="RuntimeConfig.TimeBonusMarkScale"/>). Shared
        /// with a padlock, a key or a layer count
        /// (<paramref name="isShared"/>), it has
        /// <see cref="SharedMarkRoomCells"/>. Alone it sits on
        /// <see cref="Anchor"/>, and has twice the distance from the anchor to
        /// the nearer end of the run of cells it lies on, in the row it lies
        /// in — or, when it lies on the seam between two rows, the narrower of
        /// the two: two cells on a 2×1 or a 2×2, one on a 1×1, a vertical 1×2
        /// or an L. Only the width counts: the mark is shorter than a cell.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        public static float BonusRoomCells(IReadOnlyList<Coord> cells, bool isShared)
        {
            DoubledAnchor(cells, out var doubleX, out var doubleY, out var members);
            if (isShared)
            {
                return SharedMarkRoomCells;
            }

            // A cell x spans doubled x from 2x to 2x + 2; the anchor touches
            // every cell around it (DoubledAnchor), so each run below exists.
            var columns = TouchingIndices(doubleX);
            var narrowest = int.MaxValue;
            foreach (var row in TouchingIndices(doubleY))
            {
                var first = columns[0];
                while (members.Contains(new Coord(first - 1, row)))
                {
                    first--;
                }

                var last = columns[columns.Length - 1];
                while (members.Contains(new Coord(last + 1, row)))
                {
                    last++;
                }

                var nearerEnd = Math.Min(doubleX - 2 * first, 2 * last + 2 - doubleX);
                narrowest = Math.Min(narrowest, nearerEnd);
            }

            // Twice the doubled distance over two: the doubled distance itself.
            return narrowest;
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

    /// <summary>How much room the marks sharing one block have (<see cref="MarkLayout.Slots"/>).</summary>
    public enum MarkCrowding
    {
        /// <summary>Each mark has a cell, or there is only one: full size.</summary>
        Roomy,

        /// <summary>Two marks share the anchor's cells and are drawn smaller.</summary>
        CrowdedPair,

        /// <summary>Three marks share the anchor's cells and are drawn smaller still.</summary>
        CrowdedTriple
    }

    /// <summary>
    /// Where a block's padlock or key, its layer count and its time-bonus mark
    /// sit, in cell units relative to the footprint's origin
    /// (<see cref="MarkLayout.Slots"/>).
    /// </summary>
    public readonly struct BlockMarkSlots
    {
        /// <summary>The slots of a block's marks.</summary>
        public BlockMarkSlots(Vector2 icon, Vector2 layers, Vector2 bonus, MarkCrowding crowding)
        {
            Icon = icon;
            Layers = layers;
            Bonus = bonus;
            Crowding = crowding;
        }

        /// <summary>The centre of the padlock or key.</summary>
        public Vector2 Icon { get; }

        /// <summary>The centre of the layer count (M4).</summary>
        public Vector2 Layers { get; }

        /// <summary>The centre of the time-bonus mark (M10).</summary>
        public Vector2 Bonus { get; }

        /// <summary>Whether the marks have room for full size, or share the anchor's cells as a pair or a triple.</summary>
        public MarkCrowding Crowding { get; }
    }

    /// <summary>Where the marks sharing one block go (<see cref="MarkLayout.Row"/>).</summary>
    public readonly struct MarkRowLayout
    {
        private readonly Vector2[] slots;

        /// <summary>A layout for the marks at <paramref name="slots"/>, in order.</summary>
        public MarkRowLayout(Vector2[] slots, bool isCrowded)
        {
            this.slots = slots;
            IsCrowded = isCrowded;
        }

        /// <summary>How many marks the row holds.</summary>
        public int Count => slots.Length;

        /// <summary>The centre of mark <paramref name="index"/>, counted from the left, or from the lowest of a vertical row.</summary>
        public Vector2 this[int index] => slots[index];

        /// <summary>True when the marks share the anchor's cells and must be drawn smaller.</summary>
        public bool IsCrowded { get; }
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
