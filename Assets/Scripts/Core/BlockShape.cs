using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// Geometry questions about a block's cell set that callers want answered
    /// rather than enforced. <see cref="BlockValidation"/> throws when a shape is
    /// wrong; this reports, so the Level Editor can refuse a free-draw stroke
    /// that would disconnect the shape before it ever reaches <c>Core</c> — the
    /// same split as <see cref="ElevatorTiling"/> beside <see cref="ElevatorDefinition"/>.
    /// </summary>
    public static class BlockShape
    {
        private static readonly Coord[] Orthogonal =
        {
            new Coord(1, 0),
            new Coord(-1, 0),
            new Coord(0, 1),
            new Coord(0, -1),
        };

        /// <summary>
        /// True when every distinct cell in <paramref name="cells"/> is reachable
        /// from the first by a path of orthogonal steps through other cells in
        /// the set. Empty and single-cell sets are connected. Diagonal-only
        /// contact does not count — a diagonally joined shape has no well-defined
        /// projection span onto a board edge (M1).
        /// </summary>
        public static bool IsOrthogonallyConnected(IReadOnlyList<Coord> cells)
        {
            if (cells == null || cells.Count <= 1)
            {
                return true;
            }

            var set = new HashSet<Coord>(cells);
            var reached = new HashSet<Coord> { cells[0] };
            var frontier = new Queue<Coord>();
            frontier.Enqueue(cells[0]);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var step in Orthogonal)
                {
                    var neighbour = current + step;
                    if (set.Contains(neighbour) && reached.Add(neighbour))
                    {
                        frontier.Enqueue(neighbour);
                    }
                }
            }

            return reached.Count == set.Count;
        }

        /// <summary>
        /// The extent of <paramref name="cells"/> projected onto
        /// <paramref name="edge"/> — the x-extent for a top or bottom edge, the
        /// y-extent for a left or right edge (a vertical 1x2 projects 1 onto
        /// top/bottom, 2 onto left/right). Zero for an empty set. The whole
        /// footprint projects, not just the cells touching the wall (M1).
        /// </summary>
        public static int ProjectionOnto(IReadOnlyList<Coord> cells, BoardEdge edge)
        {
            if (cells == null || cells.Count == 0)
            {
                return 0;
            }

            var horizontal = edge == BoardEdge.Top || edge == BoardEdge.Bottom;
            var min = int.MaxValue;
            var max = int.MinValue;
            foreach (var cell in cells)
            {
                var v = horizontal ? cell.X : cell.Y;
                if (v < min)
                {
                    min = v;
                }

                if (v > max)
                {
                    max = v;
                }
            }

            return (max - min) + 1;
        }

        /// <summary>True when <paramref name="a"/> and <paramref name="b"/> share an edge (not a corner, not the same cell).</summary>
        public static bool AreOrthogonallyAdjacent(Coord a, Coord b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            if (dx < 0)
            {
                dx = -dx;
            }

            if (dy < 0)
            {
                dy = -dy;
            }

            return dx + dy == 1;
        }
    }
}
