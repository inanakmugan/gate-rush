using System;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>
    /// One level being played: the immutable <see cref="LevelContext"/> and the
    /// current <see cref="BoardState"/>. The drawn board is always
    /// <see cref="State"/>, and the only way it changes is a resolution through
    /// <see cref="MoveResolver"/> (or <see cref="Restart"/>), so the runtime can
    /// never hold a board the rules did not produce.
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

            State = next;
            if (timeBonusSeconds > 0)
            {
                TimeBonusEarned?.Invoke(timeBonusSeconds);
            }

            StateChanged?.Invoke();
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
    }
}
