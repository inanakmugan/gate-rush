using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>
    /// Decides what the player may see of a board (D13). <see cref="BoardState"/>
    /// always holds the whole truth; this layer hides from the player what the
    /// rules say is hidden — a frozen block's colour (M3), every block under a
    /// closed shutter (M5), an elevator's waves still to come (D48) — and
    /// derives the counts shown on badges. It reads state and never changes it.
    /// </summary>
    /// <remarks>
    /// Every remaining count is the number of clears still needed:
    /// <c>threshold − current count</c> on the counter the unlock watches. It is
    /// reported only while the thing is still shut, so a satisfied threshold
    /// shows nothing.
    /// </remarks>
    public sealed class VisibilityLayer
    {
        /// <summary>
        /// M4: the player sees the outer colour and the one beneath it, and a
        /// numeral only once more colours remain than those two. Public so
        /// the Level Editor marks a block's depth by the same rule.
        /// </summary>
        public const int ColoursShownWithoutNumeral = 2;

        private readonly LevelContext ctx;

        /// <summary>A layer for one level.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is null.</exception>
        public VisibilityLayer(LevelContext ctx)
        {
            this.ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        /// <summary>
        /// What the player sees of block <paramref name="blockIndex"/>. A dead or
        /// not-yet-spawned block, or one under a closed shutter, is not shown at
        /// all. A frozen block shows its shape and its remaining count, never
        /// its colours or layer numeral. Locks, keys and a time bonus (M10)
        /// show on any shown block, frozen included: a lock is known by its
        /// block's colour at level start and a key is marked with that colour
        /// (D47), whatever the colour of the block carrying it.
        /// </summary>
        public BlockVisual Block(BoardState state, int blockIndex)
        {
            // Alive and outside every closed shutter — exactly what
            // CanBeTargeted tests, since a shutter is the only thing that makes
            // a living block untargetable (M5).
            if (!state.CanBeTargeted(ctx, blockIndex))
            {
                return default;
            }

            var spec = ctx.SpecAt(blockIndex);
            var isFrozen = !state.Unfrozen[blockIndex];

            var frozenRemaining = 0;
            if (isFrozen)
            {
                // .Value is safe: a block without a threshold starts unfrozen
                // (BoardState.CreateInitial), so a frozen one always has one.
                frozenRemaining = ClearsStillNeeded(state, spec.UnfreezeAtClearCount.Value, null);
            }

            BlockColor? outer = null;
            BlockColor? beneath = null;
            int? layerNumeral = null;
            if (!isFrozen)
            {
                var cleared = state.ClearedColors[blockIndex];
                var remaining = spec.ColorStack.Count - cleared;
                outer = spec.ColorStack[cleared];
                if (remaining > 1)
                {
                    beneath = spec.ColorStack[cleared + 1];
                }

                if (remaining > ColoursShownWithoutNumeral)
                {
                    layerNumeral = remaining;
                }
            }

            BlockColor? lockColor = null;
            var keysStillRequired = 0;
            if (spec.LockId.HasValue && !state.Unlocked[blockIndex])
            {
                lockColor = LockColorOf(spec);
                keysStillRequired = spec.RequiredKeyCount - ConsumedKeyCount(state, spec.LockId.Value);
            }

            BlockColor? keyMarkColor = null;
            if (spec.KeyTargetLockId.HasValue)
            {
                keyMarkColor = LockColorOf(ctx.SpecAt(ctx.LockOwnerIndex(spec.KeyTargetLockId.Value)));
            }

            return new BlockVisual(
                isShown: true,
                isFrozen: isFrozen,
                frozenRemaining: frozenRemaining,
                outerColor: outer,
                beneathColor: beneath,
                layerNumeral: layerNumeral,
                lockColor: lockColor,
                keysStillRequired: keysStillRequired,
                keyMarkColor: keyMarkColor,
                timeBonusSeconds: spec.TimeBonusSeconds);
        }

        /// <summary>
        /// What the player sees of gate <paramref name="gateIndex"/>: its colour
        /// while open; while closed, no colour (drawn frozen, M2) and the clears
        /// still needed to open it.
        /// </summary>
        public GateVisual Gate(BoardState state, int gateIndex)
        {
            var gate = ctx.Gates[gateIndex];
            if (state.GateOpen[gateIndex])
            {
                return new GateVisual(isOpen: true, color: gate.Color, opensInClears: 0);
            }

            // .Value is safe: a gate without a threshold is open from the
            // start (GateDefinition.IsOpenAtZeroClears) and opening is
            // permanent, so a closed one always has one.
            return new GateVisual(
                isOpen: false,
                color: null,
                opensInClears: ClearsStillNeeded(state, gate.OpenAtClearCount.Value, null));
        }

        /// <summary>
        /// What the player sees of shutter <paramref name="shutterIndex"/>: while
        /// closed, an opaque cover with the clears still needed — of any colour
        /// for a global shutter, of <see cref="ShutterVisual.CountsColor"/> for a
        /// colour-bound one.
        /// </summary>
        public ShutterVisual Shutter(BoardState state, int shutterIndex)
        {
            var shutter = ctx.Shutters[shutterIndex];
            if (state.ShutterOpen[shutterIndex])
            {
                return default;
            }

            return new ShutterVisual(
                isClosed: true,
                opensInClears: ClearsStillNeeded(state, shutter.Threshold, shutter.RequiredColor),
                countsColor: shutter.RequiredColor);
        }

        /// <summary>
        /// What the player sees of generator <paramref name="generatorIndex"/>:
        /// while it still has blocks queued, how many, and the next one on its
        /// screen (M6, D48). An exhausted generator is destroyed and shows
        /// nothing. The next block's colour is hidden, like a frozen block's
        /// (M3), when it would spawn frozen at the current counts.
        /// </summary>
        public GeneratorVisual Generator(BoardState state, int generatorIndex)
        {
            var queue = ctx.Generators[generatorIndex].Queue;
            var spawned = state.GeneratorIndex[generatorIndex];
            if (spawned >= queue.Count)
            {
                return default;
            }

            var next = queue[spawned];

            // The same test the resolver makes when the block spawns: frozen
            // while its threshold is unmet by the running total.
            var isNextFrozen = next.UnfreezeAtClearCount.HasValue
                               && !UnlockConditions.IsThresholdMet(
                                   state.TotalClearCount, state.ClearCountByColor, next.UnfreezeAtClearCount.Value, null);

            return new GeneratorVisual(
                isShown: true,
                queued: queue.Count - spawned,
                nextCells: next.Cells,
                nextColor: isNextFrozen ? (BlockColor?)null : next.ColorStack[0],
                isNextFrozen: isNextFrozen);
        }

        /// <summary>
        /// True while elevator <paramref name="elevatorIndex"/> is drawn: until
        /// its final wave has been cleared (M9). How many waves remain is never
        /// shown (D48).
        /// </summary>
        public bool ElevatorPresent(BoardState state, int elevatorIndex) =>
            state.ElevatorWaveIndex[elevatorIndex] < ctx.Elevators[elevatorIndex].Waves.Count
            || state.ElevatorWaveActive[elevatorIndex];

        /// <summary>A lock's colour: its block's outer colour at level start (D47).</summary>
        private static BlockColor LockColorOf(BlockSpec spec) => spec.ColorStack[0];

        /// <summary>
        /// <c>threshold − counter</c>, where the counter is the total clear count,
        /// or one colour's count when <paramref name="countsColor"/> is given —
        /// the same counter choice <see cref="UnlockConditions.IsThresholdMet"/>
        /// makes.
        /// </summary>
        private static int ClearsStillNeeded(BoardState state, int threshold, BlockColor? countsColor)
        {
            var counter = countsColor.HasValue
                ? state.ClearCountByColor[(int)countsColor.Value]
                : state.TotalClearCount;

            return threshold - counter;
        }

        private int ConsumedKeyCount(BoardState state, int lockId)
        {
            var keys = ctx.KeyIndicesForLock(lockId);
            var consumed = 0;
            for (var i = 0; i < keys.Count; i++)
            {
                if (state.KeyConsumed[keys[i]])
                {
                    consumed++;
                }
            }

            return consumed;
        }
    }

    /// <summary>
    /// What the player sees of one block. The default value is a block that is
    /// not shown.
    /// </summary>
    public readonly struct BlockVisual
    {
        /// <summary>A block visual with every field given.</summary>
        public BlockVisual(
            bool isShown, bool isFrozen, int frozenRemaining,
            BlockColor? outerColor, BlockColor? beneathColor, int? layerNumeral,
            BlockColor? lockColor, int keysStillRequired, BlockColor? keyMarkColor, int timeBonusSeconds)
        {
            IsShown = isShown;
            IsFrozen = isFrozen;
            FrozenRemaining = frozenRemaining;
            OuterColor = outerColor;
            BeneathColor = beneathColor;
            LayerNumeral = layerNumeral;
            LockColor = lockColor;
            KeysStillRequired = keysStillRequired;
            KeyMarkColor = keyMarkColor;
            TimeBonusSeconds = timeBonusSeconds;
        }

        /// <summary>False for a dead or unspawned block, or one under a closed shutter.</summary>
        public bool IsShown { get; }

        /// <summary>True while the block is frozen (M3): drawn in ice, colours hidden.</summary>
        public bool IsFrozen { get; }

        /// <summary>Clears still needed to unfreeze; 0 when not frozen.</summary>
        public int FrozenRemaining { get; }

        /// <summary>The block's current colour; null when hidden by a freeze.</summary>
        public BlockColor? OuterColor { get; }

        /// <summary>The colour beneath the outer one, when there is one and it is not hidden.</summary>
        public BlockColor? BeneathColor { get; }

        /// <summary>Colours remaining, shown only when more than two remain (M4); otherwise null.</summary>
        public int? LayerNumeral { get; }

        /// <summary>
        /// While the block is still locked, the lock's colour: the block's outer
        /// colour at level start (D47). Null when it carries no lock or the lock
        /// has opened.
        /// </summary>
        public BlockColor? LockColor { get; }

        /// <summary>True while the block is drawn locked: chains and a padlock.</summary>
        public bool IsLocked => LockColor.HasValue;

        /// <summary>Keys still needed to open the lock; 0 when there is no lock showing.</summary>
        public int KeysStillRequired { get; }

        /// <summary>
        /// The colour this block's key is marked with — its lock's colour
        /// (D47), whatever this block's own colour. Null when it carries no key.
        /// </summary>
        public BlockColor? KeyMarkColor { get; }

        /// <summary>
        /// The seconds this block adds to the countdown when it is destroyed
        /// (M10), shown on its time-bonus mark; 0 when it carries no bonus.
        /// </summary>
        public int TimeBonusSeconds { get; }
    }

    /// <summary>
    /// What the player sees of one generator (M6, D48). The default value is an
    /// exhausted generator, which draws nothing.
    /// </summary>
    public readonly struct GeneratorVisual
    {
        /// <summary>A generator visual with every field given.</summary>
        public GeneratorVisual(
            bool isShown, int queued, IReadOnlyList<Coord> nextCells, BlockColor? nextColor, bool isNextFrozen)
        {
            IsShown = isShown;
            Queued = queued;
            NextCells = nextCells;
            NextColor = nextColor;
            IsNextFrozen = isNextFrozen;
        }

        /// <summary>False once every queued block has spawned: the generator is destroyed.</summary>
        public bool IsShown { get; }

        /// <summary>Blocks still queued, the next one included; 0 when not shown.</summary>
        public int Queued { get; }

        /// <summary>The next block's footprint, normalised to a (0, 0) minimum; null when not shown.</summary>
        public IReadOnlyList<Coord> NextCells { get; }

        /// <summary>The next block's colour; null when it would spawn frozen (M3), or when not shown.</summary>
        public BlockColor? NextColor { get; }

        /// <summary>True when the next block would spawn frozen at the current counts: drawn in ice.</summary>
        public bool IsNextFrozen { get; }
    }

    /// <summary>What the player sees of one gate.</summary>
    public readonly struct GateVisual
    {
        /// <summary>A gate visual with every field given.</summary>
        public GateVisual(bool isOpen, BlockColor? color, int opensInClears)
        {
            IsOpen = isOpen;
            Color = color;
            OpensInClears = opensInClears;
        }

        /// <summary>True once the gate has opened (M2).</summary>
        public bool IsOpen { get; }

        /// <summary>The gate's colour while open; null while closed, when it is drawn colourless.</summary>
        public BlockColor? Color { get; }

        /// <summary>Clears still needed to open; 0 when open.</summary>
        public int OpensInClears { get; }
    }

    /// <summary>What the player sees of one shutter. The default value is an open shutter, which draws nothing.</summary>
    public readonly struct ShutterVisual
    {
        /// <summary>A shutter visual with every field given.</summary>
        public ShutterVisual(bool isClosed, int opensInClears, BlockColor? countsColor)
        {
            IsClosed = isClosed;
            OpensInClears = opensInClears;
            CountsColor = countsColor;
        }

        /// <summary>True while the shutter is closed and covers its region.</summary>
        public bool IsClosed { get; }

        /// <summary>Clears still needed to open; 0 when open.</summary>
        public int OpensInClears { get; }

        /// <summary>The colour whose clears count, for a colour-bound shutter; null for a global one.</summary>
        public BlockColor? CountsColor { get; }
    }
}
