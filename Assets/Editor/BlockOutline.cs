using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// Which cell sides of a block's footprint face something outside the block
    /// (<see cref="Edges"/>) and which face the block itself
    /// (<see cref="InteriorEdges"/>). Pure geometry over a cell list, no
    /// <c>UnityEditor</c>, so the window turns the result into rectangles and
    /// decides nothing about where a block ends.
    /// </summary>
    /// <remarks>
    /// The boundary is computed from the footprint itself rather than from grid
    /// lines, which is the whole point: the editor fills cells by colour, so two
    /// same-coloured blocks sitting side by side are otherwise indistinguishable
    /// from one bigger block. A grid-line stroke cannot tell them apart — it
    /// draws between every pair of cells, including the two cells inside one
    /// block — while a footprint boundary draws only at a block's own outer
    /// edge (docs/Modules/09a).
    /// </remarks>
    public static class BlockOutline
    {
        /// <summary>One side of one cell, lying on a block's outer edge.</summary>
        public readonly struct BoundaryEdge
        {
            /// <summary>The cell, in the same space as the footprint handed to <see cref="Edges"/> — relative, not board-absolute.</summary>
            public Coord Cell { get; }

            /// <summary>Which of the cell's four sides the boundary runs along.</summary>
            public Direction Side { get; }

            public BoundaryEdge(Coord cell, Direction side)
            {
                Cell = cell;
                Side = side;
            }

            public override string ToString() => $"{Cell} {Side}";
        }

        /// <summary>
        /// Every boundary side of <paramref name="cells"/>, in a fixed order:
        /// the footprint's own cell order, and within each cell
        /// <see cref="Direction.Up"/>, <see cref="Direction.Down"/>,
        /// <see cref="Direction.Left"/>, <see cref="Direction.Right"/>. Fixed so
        /// the same footprint always strokes identically between repaints.
        /// </summary>
        public static IReadOnlyList<BoundaryEdge> Edges(IReadOnlyList<Coord> cells) =>
            Sides(cells, neighbourIsSameBlock: false);

        /// <summary>
        /// Every side of <paramref name="cells"/> whose neighbour belongs to the
        /// same block — the exact complement of <see cref="Edges"/>, in the same
        /// fixed order. These are the sides where the editor's grid line falls
        /// inside one block and has to be painted out, since a line there reads
        /// as a division that is not real.
        /// </summary>
        public static IReadOnlyList<BoundaryEdge> InteriorEdges(IReadOnlyList<Coord> cells) =>
            Sides(cells, neighbourIsSameBlock: true);

        /// <summary>
        /// The one walk both <see cref="Edges"/> and <see cref="InteriorEdges"/>
        /// run, with the membership test inverted between them. Written once so
        /// the boundary set and the interior set cannot drift into disagreeing
        /// about a side — which would show up as either a missing stroke or a
        /// stroke painted over by an erasure.
        /// </summary>
        private static IReadOnlyList<BoundaryEdge> Sides(IReadOnlyList<Coord> cells, bool neighbourIsSameBlock)
        {
            var edges = new List<BoundaryEdge>();
            if (cells == null || cells.Count == 0)
            {
                return edges;
            }

            var occupied = new HashSet<Coord>(cells);

            foreach (var cell in cells)
            {
                foreach (var side in AllSides)
                {
                    if (occupied.Contains(cell + Step(side)) == neighbourIsSameBlock)
                    {
                        edges.Add(new BoundaryEdge(cell, side));
                    }
                }
            }

            return edges;
        }

        private static readonly Direction[] AllSides =
        {
            Direction.Up,
            Direction.Down,
            Direction.Left,
            Direction.Right,
        };

        /// <summary>
        /// The one-cell step a direction represents. The grid's origin is
        /// bottom-left with +Y up — <see cref="EditorGridLayout.CellRect"/> is
        /// the only place that flips to GUI space — so Up is +Y here.
        /// </summary>
        private static Coord Step(Direction side)
        {
            switch (side)
            {
                case Direction.Up:
                    return new Coord(0, 1);
                case Direction.Down:
                    return new Coord(0, -1);
                case Direction.Left:
                    return new Coord(-1, 0);
                default:
                    return new Coord(1, 0);
            }
        }
    }
}
