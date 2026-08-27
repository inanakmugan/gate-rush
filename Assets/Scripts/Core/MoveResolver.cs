using System;
using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// Applies an action to a board and resolves every consequence that follows,
    /// until the board stops changing. Every rule interaction in the game passes
    /// through here: a player move, a rocket (<see cref="TryClearBlock"/>), and a
    /// broom (<see cref="TrySweepColor"/>) all feed the same pipeline — there is
    /// no second removal path (see <c>DECISIONS.md</c> D9).
    /// </summary>
    /// <remarks>
    /// <para><b>Scope.</b> This class currently implements M1 (movement and gate
    /// exit) and M7 (axis restriction) only. The fixpoint loop
    /// (<c>DECISIONS.md</c> D8) is fully built, but its condition-re-evaluation,
    /// spawn-trigger, and key-effect steps are extension points that do nothing
    /// yet — see <see cref="ReevaluateConditions"/>,
    /// <see cref="CheckSpawnTriggers"/>, and <see cref="ApplyKeyEffects"/>. Until
    /// those are filled in, no action produces a chain longer than one pass.</para>
    ///
    /// <para><b>Exit is a property of the move, not the position</b>
    /// (<c>DECISIONS.md</c> D25). A move — zero-distance or not — that leaves a
    /// block flush and aligned with a compatible open gate clears it; there is no
    /// parking on a usable gate. A block may sit flush against a compatible gate
    /// <em>without</em> clearing only when it did not arrive there by a move
    /// (authored that way, or exposed later by an unfreeze / gate-open /
    /// shutter-open); the player then clears it with a zero-distance move.</para>
    ///
    /// <para><b>Instance, not static.</b> The resolver holds reusable buffers —
    /// an event queue, the successor builder, and a private
    /// <see cref="BlockReachability"/> that owns the flood-fill working set — so
    /// a search that applies millions of moves does not re-allocate them per
    /// call. That <see cref="BlockReachability"/> is private and never shared, for
    /// the reason its own documentation gives: its scan buffers cannot be reused
    /// by a second caller mid-scan. The resolver is therefore not thread-safe;
    /// that is acceptable because the solver is single-threaded and WebGL has no
    /// threads anyway. Construct one per search, not one per move.</para>
    ///
    /// <para><b>Not sealed.</b> <see cref="ReevaluateConditions"/> and
    /// <see cref="CheckSpawnTriggers"/> are <c>internal virtual</c> so this
    /// assembly's tests can subclass the resolver and script the fixpoint loop —
    /// proving it runs multiple passes and honours its iteration bound while both
    /// hooks are still no-ops. Production code never subclasses this type.</para>
    /// </remarks>
    public class MoveResolver
    {
        private readonly Queue<ColorClearedEvent> events = new Queue<ColorClearedEvent>();
        private readonly SuccessorBuilder successor = new SuccessorBuilder();
        private readonly BlockReachability reachability = new BlockReachability();

        /// <summary>
        /// Applies a player move. Returns <c>false</c> — leaving
        /// <paramref name="result"/> null — when the move is not legal: the block
        /// cannot move (dead, frozen, locked, shuttered, or axis-forbidden), the
        /// target is not reachable by a corner-turning flood fill of fully-legal
        /// intermediate positions, or the move is zero-distance and the block is
        /// not flush against a compatible open gate. Never throws for player
        /// error.
        /// </summary>
        public bool TryApplyMove(LevelContext ctx, BoardState state, Move move, out BoardState result)
        {
            result = null;

            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var blockIndex = move.BlockIndex;
            if (blockIndex < 0 || blockIndex >= ctx.TotalBlockCapacity)
            {
                return false;
            }

            if (!state.CanMove(ctx, blockIndex))
            {
                return false;
            }

            var currentOrigin = state.Origins[blockIndex];
            var isZeroDistance = move.TargetOrigin == currentOrigin;

            if (!isZeroDistance &&
                !reachability.IsReachable(ctx, state, blockIndex, currentOrigin, move.TargetOrigin))
            {
                return false;
            }

            var clearsAtGate =
                BlockReachability.IsAtCompatibleExitGate(ctx, state, blockIndex, move.TargetOrigin);

            // A zero-distance move is only ever legal as the push that clears a
            // block already sitting at a compatible open gate. Anything else is a
            // no-op and must be rejected (D25).
            if (isZeroDistance && !clearsAtGate)
            {
                return false;
            }

            successor.Reset(state);
            events.Clear();

            successor.SetOrigin(blockIndex, move.TargetOrigin);

            if (clearsAtGate)
            {
                ClearOuterColor(ctx, successor, blockIndex);
            }

            result = ResolveToFixpoint(ctx, successor);
            return true;
        }

        /// <summary>
        /// Clears one block's current colour with no movement and no gate
        /// requirement — the rocket joker and the <see cref="KeyEffect.ClearOuterColor"/>
        /// key effect both enter here. Returns <c>false</c> when the block cannot
        /// be targeted (dead, or under a closed shutter); frozen and locked
        /// blocks <em>can</em> be targeted (see <c>DECISIONS.md</c> D11).
        /// </summary>
        public bool TryClearBlock(LevelContext ctx, BoardState state, int blockIndex, out BoardState result)
        {
            result = null;

            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (blockIndex < 0 || blockIndex >= ctx.TotalBlockCapacity)
            {
                return false;
            }

            if (!state.CanBeTargeted(ctx, blockIndex))
            {
                return false;
            }

            successor.Reset(state);
            events.Clear();

            ClearOuterColor(ctx, successor, blockIndex);

            result = ResolveToFixpoint(ctx, successor);
            return true;
        }

        /// <summary>
        /// Clears <paramref name="color"/> from every targetable block currently
        /// showing it — the broom joker. Returns <c>false</c> when no targetable
        /// block matches (the caller can then decline to consume the joker).
        /// Because adjacent colour-stack layers differ (<c>DECISIONS.md</c> D26),
        /// no block matches twice in one sweep.
        /// </summary>
        public bool TrySweepColor(LevelContext ctx, BoardState state, BlockColor color, out BoardState result)
        {
            result = null;

            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            successor.Reset(state);
            events.Clear();

            var sweptAny = false;
            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                if (!state.CanBeTargeted(ctx, i))
                {
                    continue;
                }

                if (state.CurrentColorOf(ctx, i) != color)
                {
                    continue;
                }

                ClearOuterColor(ctx, successor, i);
                sweptAny = true;
            }

            if (!sweptAny)
            {
                return false;
            }

            result = ResolveToFixpoint(ctx, successor);
            return true;
        }

        /// <summary>
        /// Removes block <paramref name="blockIndex"/>'s current outer colour on
        /// the successor being built. The colour removed is derived here from the
        /// block's stack and the count already cleared — it is never passed in,
        /// so a caller cannot credit the wrong colour when a block is cleared
        /// more than once in one resolution (a broom clear followed by a key
        /// effect, once phase 1.8 lands). Bumps the cleared-colour count,
        /// enqueues the <see cref="ColorClearedEvent"/> every counter listens to,
        /// and — when that was the block's last colour — marks it dead so its
        /// cells read free. The block's origin is left untouched: a cleared block
        /// stays at the gate mouth and keeps obstructing it (M1, D25).
        /// </summary>
        private void ClearOuterColor(LevelContext ctx, SuccessorBuilder builder, int blockIndex)
        {
            var colorStack = ctx.SpecAt(blockIndex).ColorStack;
            var alreadyCleared = builder.GetClearedColors(blockIndex);
            var color = colorStack[alreadyCleared];

            builder.SetClearedColors(blockIndex, (byte)(alreadyCleared + 1));
            events.Enqueue(new ColorClearedEvent(blockIndex, color));

            if (alreadyCleared + 1 >= colorStack.Count)
            {
                builder.SetAlive(blockIndex, false);

                // EXTENSION POINT — phase 1.7 (M10 time-bonus blocks). A block
                // dying here contributes its TimeBonusSeconds to the level's
                // effective time budget, reported to the caller as an output
                // rather than stored in BoardState (Core has no countdown).
                // Not computed this phase, and BlockSpec does not yet expose
                // TimeBonusSeconds.
            }
        }

        /// <summary>
        /// The fixpoint loop (<c>DECISIONS.md</c> D8). Drains the event queue,
        /// then re-evaluates unlock conditions and spawn triggers; if either
        /// changed anything, it drains and re-evaluates again. Terminates when a
        /// full pass changes nothing.
        /// </summary>
        private BoardState ResolveToFixpoint(LevelContext ctx, SuccessorBuilder builder)
        {
            var iterationLimit = ctx.MaxResolutionPasses;
            var pass = 0;

            while (true)
            {
                DrainEvents(ctx, builder);

                // Both hooks must run on every pass — a shutter opening and a
                // wave arriving can happen in the same pass — so this is |=,
                // not ||. || would short-circuit past CheckSpawnTriggers
                // whenever ReevaluateConditions already returned true.
                var changed = ReevaluateConditions(ctx, builder);
                changed |= CheckSpawnTriggers(ctx, builder);

                if (!changed)
                {
                    break;
                }

                pass++;
                if (pass > iterationLimit)
                {
                    throw new InvalidOperationException(
                        $"Move resolution for level {ctx.LevelId} exceeded its fixpoint bound of " +
                        $"{iterationLimit} passes. The level data contains a resolution cycle " +
                        "(see DECISIONS.md D8).");
                }
            }

            return builder.Build();
        }

        private void DrainEvents(LevelContext ctx, SuccessorBuilder builder)
        {
            while (events.Count > 0)
            {
                var cleared = events.Dequeue();
                builder.IncrementTotalClearCount();
                builder.IncrementClearCountByColor((int)cleared.Color);
                ApplyKeyEffects(ctx, builder, cleared);
            }
        }

        /// <summary>
        /// EXTENSION POINT — phase 1.8 (M8 locks and keys). When a block that
        /// carried a key has just died (final colour cleared), this must mark
        /// that key consumed on the successor and apply its
        /// <see cref="KeyEffect"/> to the target lock:
        /// <see cref="KeyEffect.UnlockMovement"/> flips <c>Unlocked</c> once
        /// enough keys are consumed; <see cref="KeyEffect.ClearOuterColor"/>
        /// calls <see cref="ClearOuterColor"/> on the target, which enqueues a
        /// fresh event that this same drain loop then processes. No-op while only
        /// M1 and M7 are implemented.
        /// </summary>
        private void ApplyKeyEffects(LevelContext ctx, SuccessorBuilder builder, ColorClearedEvent cleared)
        {
        }

        /// <summary>
        /// EXTENSION POINT — phase 1.7 (M2 count-gated gates, M3 count-gated
        /// blocks) and phase 1.8 (M5 shutters). Re-evaluates every unlock
        /// condition against the counters the drain loop has just advanced and
        /// <b>opens</b> gates, <b>unfreezes</b> blocks, and <b>opens</b> shutters
        /// whose thresholds are now met.
        /// <para>
        /// It must do only that and then stop. In particular it must <b>not</b>
        /// clear a block left flush against a gate it opens — exit is
        /// move-triggered, and that block waits for the player's next (possibly
        /// zero-distance) move (D25). Clearing here is a plausible-looking
        /// mistake and is wrong.
        /// </para>
        /// Returns true iff it changed any field. No-op — returns false — while
        /// only M1 and M7 are implemented. <c>internal virtual</c> so tests can
        /// drive the fixpoint loop; see the class remarks.
        /// </summary>
        internal virtual bool ReevaluateConditions(LevelContext ctx, SuccessorBuilder builder) => false;

        /// <summary>
        /// EXTENSION POINT — phase 1.13 (M6 generators, M9 elevators). Spawns the
        /// next generator block when every cell it would occupy is empty, and
        /// places the next elevator wave when its region holds no blocks. Both
        /// advance a monotonic progress index, which is why
        /// <see cref="LevelContext.MaxResolutionPasses"/> counts them. Returns
        /// true iff it spawned anything. No-op — returns false — while only M1
        /// and M7 are implemented. <c>internal virtual</c> so tests can drive the
        /// fixpoint loop; see the class remarks.
        /// </summary>
        internal virtual bool CheckSpawnTriggers(LevelContext ctx, SuccessorBuilder builder) => false;

        /// <summary>
        /// Accumulates the changes one resolution makes to a
        /// <see cref="BoardState"/> and emits the successor. Reused across every
        /// call on the owning resolver: <see cref="Reset"/> rebinds it to a new
        /// source state and discards pending changes. Each of the source state's
        /// arrays is left shared until the first write touches it, then copied
        /// once (copy-on-write); arrays never written are handed to the successor
        /// by reference, which is the structural sharing
        /// <see cref="BoardState"/>'s constructor explicitly permits.
        /// <para><c>internal</c> rather than <c>private</c> only because the
        /// <c>internal virtual</c> extension points name it in their signatures
        /// and a test subclass must be able to reference the type.</para>
        /// </summary>
        internal sealed class SuccessorBuilder
        {
            private BoardState source;

            private Coord[] origins;
            private byte[] clearedColors;
            private bool[] alive;
            private int[] clearCountByColor;
            private int totalClearCount;

            public void Reset(BoardState newSource)
            {
                source = newSource;
                origins = null;
                clearedColors = null;
                alive = null;
                clearCountByColor = null;
                totalClearCount = newSource.TotalClearCount;
            }

            public byte GetClearedColors(int index) =>
                clearedColors != null ? clearedColors[index] : source.ClearedColors[index];

            public void SetOrigin(int index, Coord value) =>
                Materialize(ref origins, source.Origins)[index] = value;

            public void SetClearedColors(int index, byte value) =>
                Materialize(ref clearedColors, source.ClearedColors)[index] = value;

            public void SetAlive(int index, bool value) =>
                Materialize(ref alive, source.Alive)[index] = value;

            public void IncrementTotalClearCount() => totalClearCount++;

            public void IncrementClearCountByColor(int colorIndex) =>
                Materialize(ref clearCountByColor, source.ClearCountByColor)[colorIndex]++;

            public BoardState Build() =>
                new BoardState(
                    origins ?? source.Origins,
                    clearedColors ?? source.ClearedColors,
                    alive ?? source.Alive,
                    source.Unfrozen,
                    source.Unlocked,
                    source.GateOpen,
                    source.ShutterOpen,
                    source.GeneratorIndex,
                    source.ElevatorWaveIndex,
                    source.ElevatorWaveActive,
                    totalClearCount,
                    clearCountByColor ?? source.ClearCountByColor,
                    source.KeyConsumed);

            private static T[] Materialize<T>(ref T[] slot, IReadOnlyList<T> original)
            {
                if (slot == null)
                {
                    var copy = new T[original.Count];
                    for (var i = 0; i < original.Count; i++)
                    {
                        copy[i] = original[i];
                    }

                    slot = copy;
                }

                return slot;
            }
        }
    }
}
