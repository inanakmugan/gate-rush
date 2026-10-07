using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// What an open gate is pulling a dragged block toward (Module 22, D49):
    /// the whole-cell origin where the block would arrive flush and aligned
    /// with a gate it can leave through, that gate, and how strongly. Found by
    /// <see cref="DragController"/> each update; the view only draws it.
    /// </summary>
    public readonly struct PullTarget
    {
        /// <summary>A pull toward <paramref name="origin"/> through gate <paramref name="gateIndex"/>.</summary>
        public PullTarget(Coord origin, int gateIndex, float strength, Vector2 nudge)
        {
            Origin = origin;
            GateIndex = gateIndex;
            Strength = strength;
            Nudge = nudge;
        }

        /// <summary>
        /// Where the block would arrive flush and aligned with the gate. The
        /// block's start only when it is being pushed in place into its gate
        /// (D43): the start is never pulled toward and never captures.
        /// </summary>
        public Coord Origin { get; }

        /// <summary>The gate's index in <see cref="LevelContext.Gates"/>: the one the block would exit through.</summary>
        public int GateIndex { get; }

        /// <summary>
        /// From 0 at the edge of the pull range, eased, to 1 from the capture
        /// range inward, where the block is latched onto <see cref="Origin"/>.
        /// </summary>
        public float Strength { get; }

        /// <summary>
        /// How far into the gate's mouth to draw the block, in cells, on top
        /// of <see cref="DragController.Position"/>: zero unless the block
        /// stands on <see cref="Origin"/> and the pointer pushes on toward the
        /// gate. Presentation only — it leaves the board, so it is never part
        /// of the position, which stays legal.
        /// </summary>
        public Vector2 Nudge { get; }
    }
}
