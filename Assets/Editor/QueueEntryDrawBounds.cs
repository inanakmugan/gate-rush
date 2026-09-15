using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// The size of a generator queue entry's Free-draw grid, and which of its
    /// cells a click may add to the shape. Derived from the owning generator
    /// rather than a fixed square (docs/Modules/09a follow-up, D34): the axis
    /// running along the generator's edge — X for Top/Bottom, Y for Left/Right,
    /// the same split <see cref="BlockShape.ProjectionOnto"/> uses — is capped
    /// at the generator's width, and the axis running into the board at a
    /// configured depth.
    /// </summary>
    /// <remarks>
    /// The caps bound new clicks only. A cell the entry already holds outside
    /// them — left over from before the generator's width or edge changed —
    /// grows the grid on that axis so it stays visible and removable, and is
    /// never deleted here. <c>DraftValidator</c>'s
    /// <c>GeneratorTooNarrowForQueuedBlock</c> is what flags such a stale shape.
    /// </remarks>
    public readonly struct QueueEntryDrawBounds
    {
        /// <summary>Grid columns: the X cap, grown to include every existing cell.</summary>
        public int Columns { get; }

        /// <summary>Grid rows: the Y cap, grown to include every existing cell.</summary>
        public int Rows { get; }

        /// <summary>The X extent a new cell must fall within.</summary>
        public int CapColumns { get; }

        /// <summary>The Y extent a new cell must fall within.</summary>
        public int CapRows { get; }

        private QueueEntryDrawBounds(int columns, int rows, int capColumns, int capRows)
        {
            Columns = columns;
            Rows = rows;
            CapColumns = capColumns;
            CapRows = capRows;
        }

        /// <summary>
        /// The bounds for a queue entry holding <paramref name="cells"/> on a
        /// generator at <paramref name="edge"/> with <paramref name="width"/>.
        /// The width is clamped to <c>[1, GeneratorDefinition.MaxWidth]</c> for
        /// sizing only: a typo must not produce a huge draw surface, and the
        /// invalid width itself is already reported by the validator. A
        /// non-positive <paramref name="maxDepth"/> is treated as 1.
        /// </summary>
        public static QueueEntryDrawBounds For(BoardEdge edge, int width, int maxDepth, IReadOnlyList<Coord> cells)
        {
            var edgeCap = Math.Min(Math.Max(width, 1), GeneratorDefinition.MaxWidth);
            var depthCap = Math.Max(maxDepth, 1);

            var alongX = edge == BoardEdge.Top || edge == BoardEdge.Bottom;
            var capColumns = alongX ? edgeCap : depthCap;
            var capRows = alongX ? depthCap : edgeCap;

            var columns = capColumns;
            var rows = capRows;
            if (cells != null)
            {
                foreach (var cell in cells)
                {
                    columns = Math.Max(columns, cell.X + 1);
                    rows = Math.Max(rows, cell.Y + 1);
                }
            }

            return new QueueEntryDrawBounds(columns, rows, capColumns, capRows);
        }

        /// <summary>
        /// Whether a click on <paramref name="cell"/> may add it to the shape:
        /// true only inside the caps. A cell outside them, on a grid grown for
        /// a stale shape, is remove-only.
        /// </summary>
        public bool AllowsNewCell(Coord cell)
        {
            return cell.X >= 0 && cell.X < CapColumns && cell.Y >= 0 && cell.Y < CapRows;
        }
    }
}
