using System;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>
    /// Decides what the player may see of a board (D13). <see cref="BoardState"/>
    /// always holds the whole truth; this layer hides from the player what the
    /// rules say is hidden — a frozen block's colour (M3), every block under a
    /// closed shutter (M5) — and derives the counts shown on labels. It reads
    /// state and never changes it.
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
        /// numeral only once more colours remain than those two.
        /// </summary>
        private const int ColoursShownWithoutNumeral = 2;

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
        /// its colours or layer numeral. Lock and key badges show on any shown
        /// block, frozen included: they identify a pairing, not a colour.
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

            int? lockId = null;
            var keysStillRequired = 0;
            if (spec.LockId.HasValue && !state.Unlocked[blockIndex])
            {
                lockId = spec.LockId.Value;
                keysStillRequired = spec.RequiredKeyCount - ConsumedKeyCount(state, lockId.Value);
            }

            return new BlockVisual(
                isShown: true,
                isFrozen: isFrozen,
                frozenRemaining: frozenRemaining,
                outerColor: outer,
                beneathColor: beneath,
                layerNumeral: layerNumeral,
                lockId: lockId,
                keysStillRequired: keysStillRequired,
                keyTargetLockId: spec.KeyTargetLockId);
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

        /// <summary>How many blocks generator <paramref name="generatorIndex"/> still has queued.</summary>
        public int GeneratorQueued(BoardState state, int generatorIndex) =>
            ctx.Generators[generatorIndex].Queue.Count - state.GeneratorIndex[generatorIndex];

        /// <summary>How many of elevator <paramref name="elevatorIndex"/>'s waves have not arrived yet.</summary>
        public int ElevatorWavesToCome(BoardState state, int elevatorIndex) =>
            ctx.Elevators[elevatorIndex].Waves.Count - state.ElevatorWaveIndex[elevatorIndex];

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
            int? lockId, int keysStillRequired, int? keyTargetLockId)
        {
            IsShown = isShown;
            IsFrozen = isFrozen;
            FrozenRemaining = frozenRemaining;
            OuterColor = outerColor;
            BeneathColor = beneathColor;
            LayerNumeral = layerNumeral;
            LockId = lockId;
            KeysStillRequired = keysStillRequired;
            KeyTargetLockId = keyTargetLockId;
        }

        /// <summary>False for a dead or unspawned block, or one under a closed shutter.</summary>
        public bool IsShown { get; }

        /// <summary>True while the block is frozen (M3): drawn in the frozen tint, colours hidden.</summary>
        public bool IsFrozen { get; }

        /// <summary>Clears still needed to unfreeze; 0 when not frozen.</summary>
        public int FrozenRemaining { get; }

        /// <summary>The block's current colour; null when hidden by a freeze.</summary>
        public BlockColor? OuterColor { get; }

        /// <summary>The colour beneath the outer one, when there is one and it is not hidden.</summary>
        public BlockColor? BeneathColor { get; }

        /// <summary>Colours remaining, shown only when more than two remain (M4); otherwise null.</summary>
        public int? LayerNumeral { get; }

        /// <summary>The lock id for the badge while the block is still locked; otherwise null.</summary>
        public int? LockId { get; }

        /// <summary>Keys still needed to open the lock; 0 when there is no lock showing.</summary>
        public int KeysStillRequired { get; }

        /// <summary>The lock this block's key opens, for the key badge; null when it carries no key.</summary>
        public int? KeyTargetLockId { get; }
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
