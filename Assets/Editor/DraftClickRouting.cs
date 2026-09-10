using System;
using System.Linq;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>The window's tools. Shared with <see cref="DraftClickRouting"/> so the routing table below is defined once and is directly testable.</summary>
    public enum EditorTool
    {
        Select,
        Block,
        Gate,
        Shutter,
        Wall,
        Generator,
        Elevator,
    }

    /// <summary>
    /// What a click on a cell resolves to for a given tool: either an existing
    /// thing the tool should select, or nothing — meaning the tool should
    /// proceed with its own placement action instead.
    /// </summary>
    public readonly struct ClickRouting
    {
        /// <summary>True when the click lands on something the tool treats as a selection candidate.</summary>
        public bool SelectsExisting { get; }

        /// <summary>The thing to select. Meaningful only when <see cref="SelectsExisting"/> is true; may still be null there (Select tool, empty cell — deselect).</summary>
        public object Target { get; }

        private ClickRouting(bool selectsExisting, object target)
        {
            SelectsExisting = selectsExisting;
            Target = target;
        }

        public static ClickRouting Select(object target) => new ClickRouting(true, target);

        /// <summary>The tool found nothing to select and should carry out its own action.</summary>
        public static readonly ClickRouting Proceed = new ClickRouting(false, null);
    }

    /// <summary>
    /// Scopes the "existing thing wins" rule (docs/Modules/09a, Session B, Part 1)
    /// by tool. <see cref="DraftHitTest"/> answers "what is at this cell" — pure
    /// geometry, stable across rounds. This is policy: which tools consult that
    /// answer for a selection candidate, and it is expected to keep changing as
    /// 09a's queue of rounds continues, so it lives apart from the geometry it
    /// calls into.
    /// </summary>
    /// <remarks>
    /// Select, Block and Wall treat a block on the cell as a selection candidate,
    /// with the same block-first precedence <see cref="DraftHitTest.PickAt"/>
    /// already applies. Gate and Generator never do — they sit on their own edge
    /// markers, outside the grid, and always proceed to place there. Shutter and
    /// Elevator never do either: a region legitimately covers a block, so a click
    /// should reach the region and not the block beneath it, and only a region of
    /// the tool's own kind counts — never the other kind, never a block.
    /// </remarks>
    public static class DraftClickRouting
    {
        public static ClickRouting Route(LevelDraft draft, Coord cell, EditorTool tool)
        {
            switch (tool)
            {
                case EditorTool.Select:
                    return ClickRouting.Select(DraftHitTest.PickAt(draft, cell).Target);

                case EditorTool.Block:
                case EditorTool.Wall:
                {
                    var hit = DraftHitTest.PickAt(draft, cell);
                    return hit.Kind == DraftHitKind.Block ? ClickRouting.Select(hit.Target) : ClickRouting.Proceed;
                }

                case EditorTool.Shutter:
                {
                    var shutter = draft.Shutters.FirstOrDefault(s => DraftHitTest.InRegion(s.Min, s.Max, cell));
                    return shutter != null ? ClickRouting.Select(shutter) : ClickRouting.Proceed;
                }

                case EditorTool.Elevator:
                {
                    var elevator = draft.Elevators.FirstOrDefault(e => DraftHitTest.InRegion(e.Min, e.Max, cell));
                    return elevator != null ? ClickRouting.Select(elevator) : ClickRouting.Proceed;
                }

                case EditorTool.Gate:
                case EditorTool.Generator:
                    return ClickRouting.Proceed;

                default:
                    // A tool added in a later 09a round must decide its own
                    // routing rather than silently falling into "proceed" —
                    // this project makes that kind of drift a compile error
                    // where it can (D28, D31, noEngineReferences); a switch
                    // over a closed set of tools is the one place it can only
                    // be a loud runtime failure instead.
                    throw new ArgumentOutOfRangeException(
                        nameof(tool), tool, $"{nameof(DraftClickRouting)}.{nameof(Route)} has no routing rule for this tool.");
            }
        }
    }
}
