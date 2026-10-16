using System;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>
    /// One level being played: the immutable <see cref="LevelContext"/> and the
    /// current <see cref="BoardState"/>. The drawn board is always
    /// <see cref="State"/>, and the only way it changes is a resolution through
    /// <see cref="MoveResolver"/> — a move, a Rocket or a Broom (D9) — or
    /// <see cref="Restart"/>, so the runtime can never hold a board the rules
    /// did not produce. The Clock needs nothing here: its seconds go to the
    /// run's countdown.
    /// </summary>
    /// <remarks>
    /// Owns its own <see cref="MoveResolver"/>: a resolver keeps reusable
    /// buffers and must never be shared (Module 03). The time bonus a
    /// resolution reports is passed on through <see cref="TimeBonusEarned"/>;
    /// the countdown it feeds lives outside the session, as time lives outside
    /// <c>Core</c> (D12).
    /// </remarks>
    public sealed class LevelSession
    {
        private readonly MoveResolver resolver = new MoveResolver();

        /// <summary>
        /// Starts the level at <see cref="BoardState.CreateInitial(LevelContext)"/>,
        /// which has already placed any generator output or elevator wave whose
        /// cells are empty at level start (D42).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is null.</exception>
        public LevelSession(LevelContext ctx)
        {
            Context = ctx ?? throw new ArgumentNullException(nameof(ctx));
            State = BoardState.CreateInitial(ctx);
        }

        /// <summary>Raised once every time <see cref="State"/> is replaced.</summary>
        public event Action StateChanged;

        /// <summary>
        /// Raised with the seconds a resolution earned when it destroyed one or
        /// more time-bonus blocks (M10), and only when those seconds are
        /// positive; a clear that leaves its block alive earns nothing. Raised
        /// after <see cref="State"/> is replaced and before
        /// <see cref="StateChanged"/>.
        /// </summary>
        public event Action<int> TimeBonusEarned;

        /// <summary>The level being played. Never changes.</summary>
        public LevelContext Context { get; }

        /// <summary>The board as the rules currently have it — the whole truth (D13).</summary>
        public BoardState State { get; private set; }

        /// <summary>True once no living block remains and every spawner is exhausted.</summary>
        public bool IsSolved => State.IsSolved(Context);

        /// <summary>
        /// Applies <paramref name="move"/> through
        /// <see cref="MoveResolver.TryApplyMove"/>. On success replaces
        /// <see cref="State"/> with the fully resolved successor, raises
        /// <see cref="TimeBonusEarned"/> if the resolution earned seconds, and
        /// raises <see cref="StateChanged"/> once; on rejection changes nothing,
        /// raises nothing and returns false. The input layer only produces moves
        /// that are legal by construction, so a false here is a bug for the
        /// caller to report.
        /// </summary>
        public bool TryApply(Move move)
        {
            if (!resolver.TryApplyMove(Context, State, move, out var next, out var timeBonusSeconds))
            {
                return false;
            }

            Commit(next, timeBonusSeconds);
            return true;
        }

        /// <summary>
        /// True when a Rocket may hit block <paramref name="blockIndex"/>: it
        /// is within the level and <see cref="BoardState.CanBeTargeted"/> —
        /// alive and not under a closed shutter; frozen and locked blocks can
        /// be hit (D11). Exactly the check <see cref="MoveResolver.TryClearBlock"/>
        /// refuses on, so after a true, <see cref="TryRocket"/> on the same
        /// <see cref="State"/> succeeds. Changes nothing.
        /// </summary>
        public bool CanRocket(int blockIndex) =>
            blockIndex >= 0
            && blockIndex < Context.TotalBlockCapacity
            && State.CanBeTargeted(Context, blockIndex);

        /// <summary>
        /// True when a Broom of <paramref name="color"/> would clear something:
        /// some block that <see cref="BoardState.CanBeTargeted"/> currently
        /// shows that colour. Exactly the selection
        /// <see cref="MoveResolver.TrySweepColor"/> refuses on, so after a
        /// true, <see cref="TrySweep"/> on the same <see cref="State"/>
        /// succeeds. Changes nothing.
        /// </summary>
        public bool CanSweep(BlockColor color)
        {
            for (var i = 0; i < Context.TotalBlockCapacity; i++)
            {
                if (State.CanBeTargeted(Context, i) && State.CurrentColorOf(Context, i) == color)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A Rocket on block <paramref name="blockIndex"/>, through
        /// <see cref="MoveResolver.TryClearBlock"/>: on success replaces
        /// <see cref="State"/>, raises <see cref="TimeBonusEarned"/> with the
        /// seconds of every block the resolution destroyed when positive, and
        /// raises <see cref="StateChanged"/> once, as <see cref="TryApply"/>
        /// does. False, changing and raising nothing, exactly when
        /// <see cref="CanRocket"/> is false. Spend the joker only after
        /// <see cref="CanRocket"/> (D65).
        /// </summary>
        public bool TryRocket(int blockIndex)
        {
            if (!resolver.TryClearBlock(Context, State, blockIndex, out var next, out var timeBonusSeconds))
            {
                return false;
            }

            Commit(next, timeBonusSeconds);
            return true;
        }

        /// <summary>
        /// A Broom of <paramref name="color"/>, through
        /// <see cref="MoveResolver.TrySweepColor"/>: on success replaces
        /// <see cref="State"/>, raises <see cref="TimeBonusEarned"/> once with
        /// the summed seconds when positive, and raises
        /// <see cref="StateChanged"/> once, as <see cref="TryApply"/> does.
        /// False, changing and raising nothing, exactly when
        /// <see cref="CanSweep"/> is false. Spend the joker only after
        /// <see cref="CanSweep"/> (D65).
        /// </summary>
        public bool TrySweep(BlockColor color)
        {
            if (!resolver.TrySweepColor(Context, State, color, out var next, out var timeBonusSeconds))
            {
                return false;
            }

            Commit(next, timeBonusSeconds);
            return true;
        }

        /// <summary>
        /// Returns to the level's initial state, spawned blocks included, and
        /// raises <see cref="StateChanged"/>. There is no undo (D14): this is the
        /// only way back.
        /// </summary>
        public void Restart()
        {
            State = BoardState.CreateInitial(Context);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// The one way a resolution's result lands, for moves and jokers
        /// alike: replace <see cref="State"/>, raise <see cref="TimeBonusEarned"/>
        /// when the seconds are positive, then <see cref="StateChanged"/>.
        /// </summary>
        private void Commit(BoardState next, int timeBonusSeconds)
        {
            State = next;
            if (timeBonusSeconds > 0)
            {
                TimeBonusEarned?.Invoke(timeBonusSeconds);
            }

            StateChanged?.Invoke();
        }
    }
}
