using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;

namespace GateRush.Editor
{
    /// <summary>
    /// What the Level Editor shows of one block beyond its outer colour
    /// (Modules 20 and 24): whether it starts frozen (M3), the colour beneath
    /// a layered block's outer one (M4), the one number it carries, its
    /// padlock or key (M8, D47), its time bonus (M10), its axis (M7), and
    /// where those marks sit. Pure decisions, no drawing, so the window only
    /// paints what this returns — on the main grid, in a wave's scope and in
    /// a generator's queue alike.
    /// </summary>
    /// <remarks>
    /// <para>A frozen block shows as ice with its count, as on the board,
    /// where M3 hides its colours: it gets no colour beneath and no depth.
    /// It keeps its padlock, key and time bonus, raised above the count, and
    /// its arrow. Its colours stay editable in the properties panel.</para>
    /// <para>No rule is decided here. "Frozen at level start" is
    /// <see cref="UnlockConditions.IsThresholdMet"/> at zero clears, the
    /// predicate the initial state and the resolver use; the depth shows
    /// from <see cref="VisibilityLayer.ColoursShownWithoutNumeral"/> colours
    /// up, as the board's badge does; and the marks sit where
    /// <see cref="MarkLayout.Slots"/> puts them, the board's own rule.</para>
    /// </remarks>
    public readonly struct EditorBlockLook
    {
        private EditorBlockLook(
            bool isFrozen, BlockColor? beneathColor, int? number, EditorMarkIcon icon, int? timeBonusSeconds,
            MovementAxis axis, BlockMarkSlots slots)
        {
            IsFrozen = isFrozen;
            BeneathColor = beneathColor;
            Number = number;
            Icon = icon;
            TimeBonusSeconds = timeBonusSeconds;
            Axis = axis;
            Slots = slots;
        }

        /// <summary>True when the block starts the level frozen: drawn in the ice colour, whatever its own.</summary>
        public bool IsFrozen { get; }

        /// <summary>The second colour of a layered block that is not frozen, drawn inset as one piece; otherwise null.</summary>
        public BlockColor? BeneathColor { get; }

        /// <summary>
        /// The number on the block: the clears that unfreeze a frozen block,
        /// or the depth of a stack too deep for its two shown colours to tell;
        /// otherwise null. A frozen block's count sits on the footprint's
        /// anchor; a depth sits on <see cref="Slots"/>' layer slot, which is
        /// the anchor when the block has no other mark.
        /// </summary>
        public int? Number { get; }

        /// <summary>The block's padlock or key, or none.</summary>
        public EditorMarkIcon Icon { get; }

        /// <summary>The seconds the block adds to the countdown (M10), or null when it adds none.</summary>
        public int? TimeBonusSeconds { get; }

        /// <summary>The block's movement axis (M7): an arrow is drawn unless it is <see cref="MovementAxis.Free"/>.</summary>
        public MovementAxis Axis { get; }

        /// <summary>Where the padlock or key, the depth and the time bonus sit, in the block's cell units, and how crowded they are.</summary>
        public BlockMarkSlots Slots { get; }

        /// <summary>
        /// True when a block with unfreeze threshold
        /// <paramref name="unfreezeAtClearCount"/> starts the level frozen.
        /// Core's predicate at zero clears; the one question for a block's
        /// fill and for its look.
        /// </summary>
        public static bool IsFrozenAtStart(int? unfreezeAtClearCount)
        {
            // The per-colour counts are never read: IsThresholdMet indexes
            // them only for a colour-bound unlock, and a block's freeze
            // counts every clear (requiredColor is null).
            return unfreezeAtClearCount.HasValue
                   && !UnlockConditions.IsThresholdMet(0, Array.Empty<int>(), unfreezeAtClearCount.Value, null);
        }

        /// <summary>
        /// The look of a block as it stands when the level starts.
        /// </summary>
        /// <param name="cells">The block's footprint, relative to its origin; not empty.</param>
        /// <param name="colorStack">The block's colours, outermost first.</param>
        /// <param name="unfreezeAtClearCount">The block's unfreeze threshold, or null.</param>
        /// <param name="axis">The block's movement axis.</param>
        /// <param name="timeBonusSeconds">The block's time bonus; zero or less shows no mark.</param>
        /// <param name="lockId">
        /// The block's lock id, or null. A padlock shows only when this is
        /// set, so a stale <paramref name="requiredKeyCount"/> on a lock-free
        /// block never shows.
        /// </param>
        /// <param name="requiredKeyCount">The keys the block's lock requires; read only with a lock id.</param>
        /// <param name="hasKey">True when the block carries a key. A block with a lock as well — a level data error — shows the padlock only.</param>
        /// <param name="keyMarkColor">The colour of the lock the key targets (D47), or null when no block has that lock id.</param>
        /// <param name="frozenRaiseCells">How far a frozen block's marks sit above its count, in cells: the board's value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="colorStack"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="cells"/> is null or empty.</exception>
        public static EditorBlockLook Of(
            IReadOnlyList<Coord> cells, IReadOnlyList<BlockColor> colorStack, int? unfreezeAtClearCount, MovementAxis axis,
            int timeBonusSeconds, int? lockId, int requiredKeyCount, bool hasKey, BlockColor? keyMarkColor,
            float frozenRaiseCells)
        {
            if (colorStack == null)
            {
                throw new ArgumentNullException(nameof(colorStack));
            }

            var icon = lockId.HasValue ? EditorMarkIcon.Padlock(requiredKeyCount)
                : hasKey ? EditorMarkIcon.Key(keyMarkColor)
                : EditorMarkIcon.None;
            var bonus = timeBonusSeconds > 0 ? timeBonusSeconds : (int?)null;
            var hasIcon = icon.Kind != EditorMarkIconKind.None;

            if (IsFrozenAtStart(unfreezeAtClearCount))
            {
                var raised = MarkLayout.Slots(cells, true, hasIcon, false, bonus.HasValue, frozenRaiseCells);
                return new EditorBlockLook(true, null, unfreezeAtClearCount.Value, icon, bonus, axis, raised);
            }

            var beneath = colorStack.Count > 1 ? colorStack[1] : (BlockColor?)null;
            var depth = colorStack.Count > VisibilityLayer.ColoursShownWithoutNumeral ? colorStack.Count : (int?)null;
            var slots = MarkLayout.Slots(cells, false, hasIcon, depth.HasValue, bonus.HasValue, frozenRaiseCells);
            return new EditorBlockLook(false, beneath, depth, icon, bonus, axis, slots);
        }
    }

    /// <summary>Which icon a block carries in the Level Editor (<see cref="EditorMarkIcon"/>).</summary>
    public enum EditorMarkIconKind
    {
        /// <summary>No icon.</summary>
        None,

        /// <summary>A locked block's padlock (M8).</summary>
        Padlock,

        /// <summary>A key-carrying block's key (M8).</summary>
        Key
    }

    /// <summary>
    /// A block's padlock or key as the Level Editor shows it (Module 24): a
    /// padlock with the keys its lock requires, or a key with its lock's
    /// colour (D47).
    /// </summary>
    public readonly struct EditorMarkIcon
    {
        private EditorMarkIcon(EditorMarkIconKind kind, int? requiredKeyCount, BlockColor? keyColor)
        {
            Kind = kind;
            RequiredKeyCount = requiredKeyCount;
            KeyColor = keyColor;
        }

        /// <summary>No icon.</summary>
        public static EditorMarkIcon None => default;

        /// <summary>A padlock showing <paramref name="requiredKeyCount"/>.</summary>
        public static EditorMarkIcon Padlock(int requiredKeyCount) =>
            new EditorMarkIcon(EditorMarkIconKind.Padlock, requiredKeyCount, null);

        /// <summary>A key whose gem is <paramref name="lockColor"/>, or which has no gem when that is null.</summary>
        public static EditorMarkIcon Key(BlockColor? lockColor) =>
            new EditorMarkIcon(EditorMarkIconKind.Key, null, lockColor);

        /// <summary>Which icon this is.</summary>
        public EditorMarkIconKind Kind { get; }

        /// <summary>The keys a padlock's lock requires; null for anything but a padlock.</summary>
        public int? RequiredKeyCount { get; }

        /// <summary>The colour of a key's gem: its lock's colour, or null when its lock was not found or this is not a key.</summary>
        public BlockColor? KeyColor { get; }
    }
}
