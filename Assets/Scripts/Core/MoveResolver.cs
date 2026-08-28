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
    /// <para><b>Scope.</b> This class implements M1 (movement and gate exit),
    /// M7 (axis restriction), the count-based unlocks M2 (gates), M3 (frozen
    /// blocks) and M5's threshold evaluation (shutters), M8 (locks and keys),
    /// and M10 (time-bonus blocks report their seconds when they die). The
    /// fixpoint loop (<c>DECISIONS.md</c> D8) drives real work through
    /// <see cref="ReevaluateConditions"/> and <see cref="ApplyKeyEffects"/> —
    /// the latter is where the loop first closes a cycle, since a key's
    /// <see cref="KeyEffect.ClearOuterColor"/> emits a fresh event the same
    /// drain then processes. Its spawn-trigger step remains an extension point
    /// that does nothing yet — see <see cref="CheckSpawnTriggers"/> (M6/M9,
    /// phase 1.13).</para>
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
    /// proving it runs multiple passes and honours its iteration bound
    /// independently of what either hook does for real. Production code never
    /// subclasses this type.</para>
    /// </remarks>
    public class MoveResolver
    {
        private readonly Queue<ColorClearedEvent> events = new Queue<ColorClearedEvent>();
        private readonly SuccessorBuilder successor = new SuccessorBuilder();
        private readonly BlockReachability reachability = new BlockReachability();

        /// <summary>
        /// Seconds contributed by every time-bonus block (M10) that died during
        /// the resolution currently in progress. Reset by <see cref="BeginResolution"/>
        /// at the start of each entry point and surfaced to the caller as an
        /// <c>out</c> parameter — never stored in <see cref="BoardState"/>,
        /// because <c>Core</c> has no countdown (<c>DECISIONS.md</c> D12).
        /// </summary>
        private int accumulatedTimeBonusSeconds;

        /// <summary>
        /// Set whenever something an unlock threshold reads has changed since
        /// <see cref="ReevaluateConditions"/> last scanned: a clear drained from
        /// the event queue (<see cref="DrainEvents"/>), or a block spawned by
        /// <see cref="CheckSpawnTriggers"/>. <see cref="ReevaluateConditions"/>
        /// clears it when it scans and returns immediately when it is not set —
        /// the fixpoint loop calls the hook on every pass and most passes change
        /// nothing. <see cref="BeginResolution"/> lowers it per resolution; a
        /// plain reposition raises it never and is never scanned.
        /// </summary>
        private bool conditionsDirty;

        /// <summary>
        /// Applies a player move. Returns <c>false</c> — leaving
        /// <paramref name="result"/> null and <paramref name="timeBonusSeconds"/>
        /// zero — when the move is not legal: the block cannot move (dead,
        /// frozen, locked, shuttered, or axis-forbidden), the target is not
        /// reachable by a corner-turning flood fill of fully-legal intermediate
        /// positions, or the move is zero-distance and the block is not flush
        /// against a compatible open gate. Never throws for player error.
        /// <para><paramref name="timeBonusSeconds"/> is the sum of every
        /// time-bonus block (M10) destroyed anywhere in this resolution — a
        /// single clear, or a chain — for the caller to add to its countdown
        /// (<c>DECISIONS.md</c> D12).</para>
        /// </summary>
        public bool TryApplyMove(
            LevelContext ctx, BoardState state, Move move,
            out BoardState result, out int timeBonusSeconds)
        {
            result = null;
            timeBonusSeconds = 0;

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
            BeginResolution();

            successor.SetOrigin(blockIndex, move.TargetOrigin);

            if (clearsAtGate)
            {
                ClearOuterColor(ctx, successor, blockIndex);
            }

            result = ResolveToFixpoint(ctx, successor);
            timeBonusSeconds = accumulatedTimeBonusSeconds;
            return true;
        }

        /// <summary>
        /// Clears one block's current colour with no movement and no gate
        /// requirement — the rocket joker and the <see cref="KeyEffect.ClearOuterColor"/>
        /// key effect both enter here. Returns <c>false</c> — leaving
        /// <paramref name="timeBonusSeconds"/> zero — when the block cannot be
        /// targeted (dead, or under a closed shutter); frozen and locked blocks
        /// <em>can</em> be targeted (see <c>DECISIONS.md</c> D11).
        /// <paramref name="timeBonusSeconds"/> sums the M10 bonuses of every
        /// block destroyed in the resulting resolution.
        /// </summary>
        public bool TryClearBlock(
            LevelContext ctx, BoardState state, int blockIndex,
            out BoardState result, out int timeBonusSeconds)
        {
            result = null;
            timeBonusSeconds = 0;

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
            BeginResolution();

            ClearOuterColor(ctx, successor, blockIndex);

            result = ResolveToFixpoint(ctx, successor);
            timeBonusSeconds = accumulatedTimeBonusSeconds;
            return true;
        }

        /// <summary>
        /// Clears <paramref name="color"/> from every targetable block currently
        /// showing it — the broom joker. Returns <c>false</c> — leaving
        /// <paramref name="timeBonusSeconds"/> zero — when no targetable block
        /// matches (the caller can then decline to consume the joker). Because
        /// adjacent colour-stack layers differ (<c>DECISIONS.md</c> D26), no
        /// block matches twice in one sweep. <paramref name="timeBonusSeconds"/>
        /// sums the M10 bonuses of every block destroyed in the resulting
        /// resolution — a broom can kill several bonus-carrying blocks at once.
        /// </summary>
        public bool TrySweepColor(
            LevelContext ctx, BoardState state, BlockColor color,
            out BoardState result, out int timeBonusSeconds)
        {
            result = null;
            timeBonusSeconds = 0;

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
            BeginResolution();

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
            timeBonusSeconds = accumulatedTimeBonusSeconds;
            return true;
        }

        /// <summary>
        /// Resets the per-resolution accumulators the three entry points share:
        /// the M10 time-bonus sum and <see cref="ReevaluateConditions"/>'s
        /// last-scanned marker. Call once per action, after
        /// <see cref="SuccessorBuilder.Reset"/> and before the first clear.
        /// </summary>
        private void BeginResolution()
        {
            accumulatedTimeBonusSeconds = 0;
            conditionsDirty = false;
        }

        /// <summary>
        /// Removes block <paramref name="blockIndex"/>'s current outer colour on
        /// the successor being built. The colour removed is derived here from the
        /// block's stack and the count already cleared — it is never passed in,
        /// so a caller cannot credit the wrong colour when a block is cleared
        /// more than once in one resolution (a broom clear followed by a key
        /// effect on the same block, say). Bumps the cleared-colour count,
        /// enqueues the <see cref="ColorClearedEvent"/> every counter listens to,
        /// and — when that was the block's last colour — marks it dead so its
        /// cells read free and adds its <see cref="BlockSpec.TimeBonusSeconds"/>
        /// (M10) to <see cref="accumulatedTimeBonusSeconds"/>. The bonus lands
        /// only on the death, not on each clear of a layered stack. The block's
        /// origin is left untouched: a cleared block stays at the gate mouth and
        /// keeps obstructing it (M1, D25).
        /// </summary>
        private void ClearOuterColor(LevelContext ctx, SuccessorBuilder builder, int blockIndex)
        {
            var spec = ctx.SpecAt(blockIndex);
            var colorStack = spec.ColorStack;
            var alreadyCleared = builder.GetClearedColors(blockIndex);
            var color = colorStack[alreadyCleared];

            builder.SetClearedColors(blockIndex, (byte)(alreadyCleared + 1));
            events.Enqueue(new ColorClearedEvent(blockIndex, color));

            if (alreadyCleared + 1 >= colorStack.Count)
            {
                builder.SetAlive(blockIndex, false);
                accumulatedTimeBonusSeconds += spec.TimeBonusSeconds;
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
                conditionsDirty = true;
                ApplyKeyEffects(ctx, builder, cleared);
            }
        }

        /// <summary>
        /// M8. Called once per dequeued <see cref="ColorClearedEvent"/> inside
        /// <see cref="DrainEvents"/>. When the cleared block has just <em>died</em>
        /// carrying a key (a shed layer delivers nothing), marks that key consumed
        /// and, once its target lock has collected <see cref="BlockSpec.RequiredKeyCount"/>
        /// consumed keys, applies the effect exactly once:
        /// <see cref="KeyEffect.UnlockMovement"/> flips the owner's <c>Unlocked</c>
        /// flag and stops; <see cref="KeyEffect.ClearOuterColor"/> flips it
        /// <em>and</em> calls <see cref="ClearOuterColor"/> on the owner, whose
        /// fresh event this same drain loop then processes — the one place the
        /// fixpoint loop feeds itself (<c>DECISIONS.md</c> D8).
        /// <para>
        /// A key consumed against an owner that is already dead (destroyed by a
        /// joker before its key arrived, D11) or already unlocked applies
        /// nothing; the key-carrying block stays on the board as an ordinary
        /// block. The lock-or-key rule (a block carries one, never both) bounds
        /// this: a key's effect can clear its target, but that target holds a
        /// lock and so cannot itself carry a key, so one key produces at most one
        /// extra clear and <see cref="LevelContext.MaxResolutionPasses"/> is not
        /// threatened.
        /// </para>
        /// </summary>
        private void ApplyKeyEffects(LevelContext ctx, SuccessorBuilder builder, ColorClearedEvent cleared)
        {
            var keyIndex = cleared.BlockIndex;

            // A shed layer delivers nothing: only the clear that empties a
            // key-carrying block's stack — leaving it dead — delivers its key.
            if (builder.IsAlive(keyIndex))
            {
                return;
            }

            var keyTargetLockId = ctx.SpecAt(keyIndex).KeyTargetLockId;
            if (!keyTargetLockId.HasValue)
            {
                return;
            }

            // A key delivers at most once. The death that delivers it is drained
            // once, but guard rather than assume.
            if (builder.IsKeyConsumed(keyIndex))
            {
                return;
            }

            var lockId = keyTargetLockId.Value;
            var keyEffect = ctx.SpecAt(keyIndex).KeyEffect;
            var ownerIndex = ctx.LockOwnerIndex(lockId);

            // EXTENSION POINT — phase 1.13 (M6/M9). The lock's owner is a
            // generator/elevator slot that has not spawned yet: Alive is false
            // but for a different reason than "destroyed", and BoardState's index
            // scheme says so explicitly (an unspawned slot keeps
            // UnspawnedOrigin; a destroyed block keeps the grid cell it died on).
            // Such a key must be HELD, not consumed — consuming it now would
            // leave the block permanently locked once it lands, with no visible
            // cause and no way for a designer to see why the solver calls the
            // level unsolvable. The pass that spawns the owner
            // (CheckSpawnTriggers) will raise conditionsDirty; phase 1.13 must
            // re-evaluate held keys there. Behaviour today is unchanged — no
            // level has spawners yet — but this branch is named, not folded into
            // the dead-owner case below.
            if (!builder.IsAlive(ownerIndex) &&
                builder.GetOrigin(ownerIndex) == BoardState.UnspawnedOrigin)
            {
                return;
            }

            builder.ConsumeKey(keyIndex);

            var requiredKeys = ctx.SpecAt(ownerIndex).RequiredKeyCount;
            var consumedKeys = 0;
            var keyIndices = ctx.KeyIndicesForLock(lockId);
            for (var i = 0; i < keyIndices.Count; i++)
            {
                if (builder.IsKeyConsumed(keyIndices[i]))
                {
                    consumedKeys++;
                }
            }

            if (consumedKeys < requiredKeys)
            {
                return;
            }

            if (!builder.IsAlive(ownerIndex))
            {
                // Destroyed by a rocket or broom before its key arrived (locked
                // blocks are targetable, D11). The key is spent; nothing to
                // apply.
                return;
            }

            if (builder.IsUnlocked(ownerIndex))
            {
                // Cannot happen with a once-only trigger, but guard rather than
                // assume.
                return;
            }

            builder.Unlock(ownerIndex);

            if (keyEffect == KeyEffect.ClearOuterColor)
            {
                ClearOuterColor(ctx, builder, ownerIndex);
            }
        }

        /// <summary>
        /// Re-evaluates every count-based unlock against the counters the drain
        /// loop has just advanced and <b>opens</b> count-gated gates (M2),
        /// <b>unfreezes</b> count-gated blocks (M3), and <b>opens</b> shutters
        /// (M5) whose thresholds are now met. All three go through the one
        /// predicate <see cref="UnlockConditions.IsThresholdMet"/>.
        /// <para>
        /// It does only that and then stops. In particular it does <b>not</b>
        /// clear a block left flush against a gate it opens — exit is
        /// move-triggered, and that block waits for the player's next (possibly
        /// zero-distance) move (D25). Clearing here is a plausible-looking
        /// mistake and is wrong.
        /// </para>
        /// <para>
        /// The M3 scan visits only blocks that are currently alive. A
        /// not-yet-spawned generator/elevator slot is skipped; the pass that
        /// spawns it raises <see cref="conditionsDirty"/>, so the next pass
        /// scans it once it exists. A full sweep of
        /// <see cref="LevelContext.TotalBlockCapacity"/> would re-check
        /// non-existent blocks on every pass of every move.
        /// </para>
        /// <para>
        /// Skips the whole scan when <see cref="conditionsDirty"/> is not set —
        /// nothing a threshold reads has changed since the last scan — and
        /// clears the flag when it does scan. Returns true iff it changed any
        /// field. <c>internal virtual</c> so tests can drive the fixpoint loop;
        /// see the class remarks.
        /// </para>
        /// </summary>
        internal virtual bool ReevaluateConditions(LevelContext ctx, SuccessorBuilder builder)
        {
            if (!conditionsDirty)
            {
                // Nothing a threshold depends on has changed since the last
                // scan, so no gate, block or shutter can have newly unlocked.
                return false;
            }

            conditionsDirty = false;

            var totalClearCount = builder.TotalClearCount;
            var clearCountByColor = builder.ClearCountByColor;
            var changed = false;

            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                if (builder.IsGateOpen(g))
                {
                    continue;
                }

                var openAt = ctx.Gates[g].OpenAtClearCount;
                if (openAt.HasValue &&
                    UnlockConditions.IsThresholdMet(totalClearCount, clearCountByColor, openAt.Value, null))
                {
                    builder.OpenGate(g);
                    changed = true;
                }
            }

            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                if (!builder.IsAlive(i) || builder.IsUnfrozen(i))
                {
                    continue;
                }

                var unfreezeAt = ctx.SpecAt(i).UnfreezeAtClearCount;
                if (unfreezeAt.HasValue &&
                    UnlockConditions.IsThresholdMet(totalClearCount, clearCountByColor, unfreezeAt.Value, null))
                {
                    builder.Unfreeze(i);
                    changed = true;
                }
            }

            for (var s = 0; s < ctx.Shutters.Count; s++)
            {
                if (builder.IsShutterOpen(s))
                {
                    continue;
                }

                var shutter = ctx.Shutters[s];
                if (UnlockConditions.IsThresholdMet(
                        totalClearCount, clearCountByColor, shutter.Threshold, shutter.RequiredColor))
                {
                    builder.OpenShutter(s);
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// EXTENSION POINT — phase 1.13 (M6 generators, M9 elevators). Spawns the
        /// next generator block when every cell it would occupy is empty, and
        /// places the next elevator wave when its region holds no blocks. Both
        /// advance a monotonic progress index, which is why
        /// <see cref="LevelContext.MaxResolutionPasses"/> counts them. Returns
        /// true iff it spawned anything, and must raise
        /// <see cref="conditionsDirty"/> when it does so a spawned block's
        /// unlock threshold is re-evaluated on the next pass. No-op — returns
        /// false — while only M1 and M7 are implemented. <c>internal virtual</c>
        /// so tests can drive the fixpoint loop; see the class remarks.
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
            private bool[] unfrozen;
            private bool[] unlocked;
            private bool[] keyConsumed;
            private bool[] gateOpen;
            private bool[] shutterOpen;
            private int[] clearCountByColor;
            private int totalClearCount;

            public void Reset(BoardState newSource)
            {
                source = newSource;
                origins = null;
                clearedColors = null;
                alive = null;
                unfrozen = null;
                unlocked = null;
                keyConsumed = null;
                gateOpen = null;
                shutterOpen = null;
                clearCountByColor = null;
                totalClearCount = newSource.TotalClearCount;
            }

            /// <summary>The running total clear count, advanced by the drain
            /// loop — the counter every non-colour-bound unlock threshold
            /// (M2, M3, and global M5 shutters) is compared against.</summary>
            public int TotalClearCount => totalClearCount;

            /// <summary>The running per-colour clear counts. Returns the source
            /// array by reference until the first <see cref="IncrementClearCountByColor"/>
            /// copies it — safe for the read-only use
            /// <see cref="UnlockConditions.IsThresholdMet"/> makes of it.</summary>
            public IReadOnlyList<int> ClearCountByColor => clearCountByColor ?? source.ClearCountByColor;

            public byte GetClearedColors(int index) =>
                clearedColors != null ? clearedColors[index] : source.ClearedColors[index];

            public bool IsAlive(int index) =>
                alive != null ? alive[index] : source.Alive[index];

            public bool IsUnfrozen(int index) =>
                unfrozen != null ? unfrozen[index] : source.Unfrozen[index];

            public bool IsUnlocked(int index) =>
                unlocked != null ? unlocked[index] : source.Unlocked[index];

            public bool IsKeyConsumed(int index) =>
                keyConsumed != null ? keyConsumed[index] : source.KeyConsumed[index];

            /// <summary>
            /// The block's origin as this successor currently has it — the
            /// pending value if <see cref="SetOrigin"/> has touched this index,
            /// otherwise the source's. Lets <c>ApplyKeyEffects</c> tell a
            /// destroyed lock owner (a real grid origin) from a not-yet-spawned
            /// one (<see cref="BoardState.UnspawnedOrigin"/>).
            /// </summary>
            public Coord GetOrigin(int index) =>
                origins != null ? origins[index] : source.Origins[index];

            public bool IsGateOpen(int index) =>
                gateOpen != null ? gateOpen[index] : source.GateOpen[index];

            public bool IsShutterOpen(int index) =>
                shutterOpen != null ? shutterOpen[index] : source.ShutterOpen[index];

            public void SetOrigin(int index, Coord value) =>
                Materialize(ref origins, source.Origins)[index] = value;

            public void SetClearedColors(int index, byte value) =>
                Materialize(ref clearedColors, source.ClearedColors)[index] = value;

            public void SetAlive(int index, bool value) =>
                Materialize(ref alive, source.Alive)[index] = value;

            /// <summary>Unfreezes block <paramref name="index"/> (M3). Permanent —
            /// nothing in the game re-freezes a block (D6).</summary>
            public void Unfreeze(int index) =>
                Materialize(ref unfrozen, source.Unfrozen)[index] = true;

            /// <summary>Unlocks block <paramref name="index"/> (M8). Permanent —
            /// a newly exposed colour is never locked (<c>MECHANICS.md</c> M8)
            /// and nothing re-locks a block.</summary>
            public void Unlock(int index) =>
                Materialize(ref unlocked, source.Unlocked)[index] = true;

            /// <summary>Marks the key carried by block <paramref name="index"/>
            /// consumed (M8). Permanent — a key delivers at most once, on the
            /// clear that empties its carrier's stack.</summary>
            public void ConsumeKey(int index) =>
                Materialize(ref keyConsumed, source.KeyConsumed)[index] = true;

            /// <summary>Opens gate <paramref name="index"/> (M2). Permanent.</summary>
            public void OpenGate(int index) =>
                Materialize(ref gateOpen, source.GateOpen)[index] = true;

            /// <summary>Opens shutter <paramref name="index"/> (M5). Permanent.</summary>
            public void OpenShutter(int index) =>
                Materialize(ref shutterOpen, source.ShutterOpen)[index] = true;

            public void IncrementTotalClearCount() => totalClearCount++;

            public void IncrementClearCountByColor(int colorIndex) =>
                Materialize(ref clearCountByColor, source.ClearCountByColor)[colorIndex]++;

            public BoardState Build() =>
                new BoardState(
                    origins ?? source.Origins,
                    clearedColors ?? source.ClearedColors,
                    alive ?? source.Alive,
                    unfrozen ?? source.Unfrozen,
                    unlocked ?? source.Unlocked,
                    gateOpen ?? source.GateOpen,
                    shutterOpen ?? source.ShutterOpen,
                    source.GeneratorIndex,
                    source.ElevatorWaveIndex,
                    source.ElevatorWaveActive,
                    totalClearCount,
                    clearCountByColor ?? source.ClearCountByColor,
                    keyConsumed ?? source.KeyConsumed);

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
