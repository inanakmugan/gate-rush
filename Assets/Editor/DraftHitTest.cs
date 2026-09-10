using System.Linq;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>What occupies a grid cell, in the order the editor selects it.</summary>
    public enum DraftHitKind
    {
        None,
        Block,
        Shutter,
        Elevator,
        Wall,
    }

    /// <summary>
    /// The result of resolving a grid cell to the thing a click there should act
    /// on. <see cref="Target"/> is the draft object for a block or region and
    /// <c>null</c> for a wall (there is nothing to select) or an empty cell.
    /// </summary>
    public readonly struct DraftHit
    {
        public DraftHitKind Kind { get; }
        public object Target { get; }

        public DraftHit(DraftHitKind kind, object target)
        {
            Kind = kind;
            Target = target;
        }

        public static readonly DraftHit None = new DraftHit(DraftHitKind.None, null);

        public bool IsEmpty => Kind == DraftHitKind.None;
    }

    /// <summary>
    /// Resolves a grid cell to what occupies it. A pure function over a draft —
    /// no <c>UnityEditor</c> — so the window never has to decide "did I click
    /// something" in <c>OnGUI</c>; it asks here (revision A1).
    /// </summary>
    /// <remarks>
    /// Precedence, highest first: a block's footprint, then a shutter region,
    /// then an elevator region, then a static wall. Regions legitimately cover
    /// blocks, so a click on a region cell that also holds a block lands on the
    /// block. Gates and generators sit on edge markers outside the grid and are
    /// not resolved here.
    /// </remarks>
    public static class DraftHitTest
    {
        public static DraftHit PickAt(LevelDraft draft, Coord cell)
        {
            var block = draft.Blocks.FirstOrDefault(b => Covers(b.StartOrigin, b.Cells, cell));
            if (block != null)
            {
                return new DraftHit(DraftHitKind.Block, block);
            }

            var shutter = draft.Shutters.FirstOrDefault(s => InRegion(s.Min, s.Max, cell));
            if (shutter != null)
            {
                return new DraftHit(DraftHitKind.Shutter, shutter);
            }

            var elevator = draft.Elevators.FirstOrDefault(e => InRegion(e.Min, e.Max, cell));
            if (elevator != null)
            {
                return new DraftHit(DraftHitKind.Elevator, elevator);
            }

            if (draft.StaticWalls.Contains(cell))
            {
                return new DraftHit(DraftHitKind.Wall, null);
            }

            return DraftHit.None;
        }

        /// <summary>Whether a footprint of <paramref name="cells"/> anchored at <paramref name="origin"/> covers <paramref name="target"/>. Geometry shared by every caller that needs to know what a block or region occupies.</summary>
        public static bool Covers(Coord origin, System.Collections.Generic.IReadOnlyList<Coord> cells, Coord target)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (origin + cells[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether <paramref name="c"/> lies within the inclusive rectangle <paramref name="min"/>..<paramref name="max"/>.</summary>
        public static bool InRegion(Coord min, Coord max, Coord c) =>
            c.X >= min.X && c.X <= max.X && c.Y >= min.Y && c.Y <= max.Y;
    }
}
