using GateRush.Core;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// How a gate's edge marker reads on the Level Editor canvas: whether it
    /// starts closed behind a clear-count threshold (M2), and the fill that
    /// says so. Pure decisions, no drawing, so the window only paints what this
    /// returns.
    /// </summary>
    /// <remarks>
    /// A threshold-gated gate is drawn in a single fixed <see cref="Frost"/>
    /// colour that hides its own colour entirely, matching M2's closed gate
    /// being "colourless (rendered frozen)": a locked gate's colour is only
    /// revealed once it opens. A gate open from the start keeps its flat
    /// palette colour. Functional, not final art.
    /// </remarks>
    public static class GateMarker
    {
        /// <summary>
        /// The icy near-white blue every threshold-gated gate is filled with,
        /// whatever its colour.
        /// </summary>
        public static readonly Color Frost = new Color(0.86f, 0.93f, 0.98f);

        /// <summary>
        /// Whether a gate with this threshold starts closed. The negation of
        /// <see cref="GateDefinition.IsOpenAtZeroClears"/>, so the editor and
        /// the game's initial state cannot disagree about which gates are closed.
        /// </summary>
        public static bool IsThresholdGated(int? openAtClearCount) =>
            !GateDefinition.IsOpenAtZeroClears(openAtClearCount);

        /// <summary>
        /// The marker fill for a gate of <paramref name="baseColor"/>:
        /// <paramref name="baseColor"/> itself when the gate starts open, and
        /// <see cref="Frost"/> — ignoring <paramref name="baseColor"/> — when
        /// <see cref="IsThresholdGated"/>.
        /// </summary>
        public static Color Fill(Color baseColor, int? openAtClearCount) =>
            IsThresholdGated(openAtClearCount) ? Frost : baseColor;
    }
}
