using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// Shifts a cell set so its component-wise minimum is <c>(0, 0)</c> — the
    /// same invariant <c>Core</c>'s <c>BlockDefinition</c> and <c>SpawnedBlock</c>
    /// establish at construction (D30). <c>BlockValidation.MinCorner</c>, the
    /// function that does this inside <c>Core</c>, is internal to that assembly,
    /// so this is a separate, equally small copy of the same trivial arithmetic
    /// rather than a request to widen <c>Core</c>'s visibility for it.
    /// </summary>
    /// <remarks>
    /// A generator queue entry's free draw (docs/Modules/09a follow-up) calls
    /// this on a <em>copy</em> of a queue entry's cells, only to compare that
    /// copy against <c>ShapePresets.Cells(...)</c> — the entry's own <c>Cells</c>
    /// stays exactly where the designer drew it. Normalising the entry itself on
    /// every click was tried first and pinned the shape to a fixed corner after
    /// the first cell, making most of the grid unreachable; comparing against a
    /// normalised copy avoids that while still letting the popup recognise a
    /// hand-drawn 2x2 started away from the origin as the 2x2 preset it is.
    /// </remarks>
    public static class CellNormalization
    {
        public static List<Coord> Normalize(IReadOnlyList<Coord> cells)
        {
            if (cells.Count == 0)
            {
                return new List<Coord>();
            }

            var minX = cells[0].X;
            var minY = cells[0].Y;
            for (var i = 1; i < cells.Count; i++)
            {
                if (cells[i].X < minX)
                {
                    minX = cells[i].X;
                }

                if (cells[i].Y < minY)
                {
                    minY = cells[i].Y;
                }
            }

            var min = new Coord(minX, minY);
            var result = new List<Coord>(cells.Count);
            foreach (var c in cells)
            {
                result.Add(c - min);
            }

            return result;
        }
    }
}
