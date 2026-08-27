using System;

namespace GateRush.Solver
{
    /// <summary>
    /// The limits one <see cref="ISearchStrategy.Search"/> call runs under, plus
    /// the <see cref="MoveGenMode"/> it branches on. Every limit is a hard stop
    /// that yields <see cref="SolveStatus.Indeterminate"/>; none of them changes
    /// the answer on a board the search can finish within them.
    /// </summary>
    public sealed class SearchBudget
    {
        /// <summary>
        /// The greatest solution length the search may return. A node at this
        /// depth is still tested for a solution but is not expanded further, so a
        /// solution of exactly this many moves is found while anything longer
        /// makes the search report <see cref="SolveStatus.Indeterminate"/>.
        /// </summary>
        public int MaxDepth { get; }

        /// <summary>
        /// The greatest number of states the search may expand — dequeue and
        /// generate the successors of. Reaching it stops the search with
        /// <see cref="SolveStatus.Indeterminate"/>.
        /// </summary>
        public int MaxExploredStates { get; }

        /// <summary>
        /// Wall-clock ceiling in milliseconds. Polled during the search rather
        /// than once per depth level, because a single stratum can exceed it on
        /// its own.
        /// <para>
        /// This is the one non-deterministic limit: the same board with the same
        /// budget can finish under the ceiling on a fast machine and trip it on a
        /// slow one, so a level can report <see cref="SolveStatus.Solvable"/> in
        /// one run and <see cref="SolveStatus.Indeterminate"/> in another. That is
        /// inherent to a wall-clock limit. When a verdict needs to be stable,
        /// bound the search with <see cref="MaxDepth"/> and
        /// <see cref="MaxExploredStates"/> and set this generously high.
        /// </para>
        /// </summary>
        public long MaxWallClockMs { get; }

        /// <summary>
        /// Which move set the search branches on. The editor runs
        /// <see cref="MoveGenMode.Canonical"/> first and falls back to
        /// <see cref="MoveGenMode.Exhaustive"/> with a larger budget only when the
        /// first pass finds nothing; the strategy itself does not decide this
        /// (see <c>DECISIONS.md</c> D5).
        /// </summary>
        public MoveGenMode Mode { get; }

        /// <exception cref="ArgumentOutOfRangeException">
        /// Any numeric limit is below 1, or <paramref name="mode"/> is not a
        /// defined <see cref="MoveGenMode"/>. A zero or negative budget is a
        /// caller bug, not a tiny-but-valid budget.
        /// </exception>
        public SearchBudget(int maxDepth, int maxExploredStates, long maxWallClockMs, MoveGenMode mode)
        {
            if (maxDepth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Must be at least 1.");
            }

            if (maxExploredStates < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExploredStates), maxExploredStates, "Must be at least 1.");
            }

            if (maxWallClockMs < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxWallClockMs), maxWallClockMs, "Must be at least 1.");
            }

            if (mode != MoveGenMode.Canonical && mode != MoveGenMode.Exhaustive)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a defined MoveGenMode.");
            }

            MaxDepth = maxDepth;
            MaxExploredStates = maxExploredStates;
            MaxWallClockMs = maxWallClockMs;
            Mode = mode;
        }
    }
}
