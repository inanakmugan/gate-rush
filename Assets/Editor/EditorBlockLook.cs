using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;

namespace GateRush.Editor
{
    /// <summary>
    /// What the Level Editor shows of one block beyond its outer colour
    /// (Module 20): whether it starts frozen (M3), the colour beneath a
    /// layered block's outer one (M4), and the one number it carries. Pure
    /// decisions, no drawing, so the window only paints what this returns —
    /// on the main grid, in a wave's scope and in a generator's queue alike.
    /// </summary>
    /// <remarks>
    /// <para>A frozen block shows as ice with its count and nothing else, as
    /// on the board, where M3 hides its colours: it gets no colour beneath
    /// and no depth. Its colours stay editable in the properties panel.</para>
    /// <para>Neither rule is decided here. "Frozen at level start" is
    /// <see cref="UnlockConditions.IsThresholdMet"/> at zero clears, the
    /// predicate the initial state and the resolver use, and the depth shows
    /// from <see cref="VisibilityLayer.ColoursShownWithoutNumeral"/> colours
    /// up, as the board's badge does.</para>
    /// </remarks>
    public readonly struct EditorBlockLook
    {
        private EditorBlockLook(bool isFrozen, BlockColor? beneathColor, int? number)
        {
            IsFrozen = isFrozen;
            BeneathColor = beneathColor;
            Number = number;
        }

        /// <summary>True when the block starts the level frozen: drawn in the ice colour, whatever its own.</summary>
        public bool IsFrozen { get; }

        /// <summary>The second colour of a layered block that is not frozen, drawn inset as one piece; otherwise null.</summary>
        public BlockColor? BeneathColor { get; }

        /// <summary>
        /// The number on the block: the clears that unfreeze a frozen block,
        /// or the depth of a stack too deep for its two shown colours to tell;
        /// otherwise null.
        /// </summary>
        public int? Number { get; }

        /// <summary>
        /// The look of a block with colour stack <paramref name="colorStack"/>
        /// and unfreeze threshold <paramref name="unfreezeAtClearCount"/>, as
        /// it stands when the level starts.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="colorStack"/> is null.</exception>
        public static EditorBlockLook Of(IReadOnlyList<BlockColor> colorStack, int? unfreezeAtClearCount)
        {
            if (colorStack == null)
            {
                throw new ArgumentNullException(nameof(colorStack));
            }

            // The per-colour counts are never read: IsThresholdMet indexes
            // them only for a colour-bound unlock, and a block's freeze
            // counts every clear (requiredColor is null).
            if (unfreezeAtClearCount.HasValue
                && !UnlockConditions.IsThresholdMet(0, Array.Empty<int>(), unfreezeAtClearCount.Value, null))
            {
                return new EditorBlockLook(true, null, unfreezeAtClearCount.Value);
            }

            var beneath = colorStack.Count > 1 ? colorStack[1] : (BlockColor?)null;
            var depth = colorStack.Count > VisibilityLayer.ColoursShownWithoutNumeral ? colorStack.Count : (int?)null;
            return new EditorBlockLook(false, beneath, depth);
        }
    }
}
