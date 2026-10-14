using System;
using System.Threading;

namespace GateRush.Solver
{
    /// <summary>
    /// The limits one <see cref="ISearchStrategy.Search"/> call runs under, plus
    /// the <see cref="MoveGenMode"/> it branches on. A search that a limit cut
    /// short and that found no solution reports
    /// <see cref="SolveStatus.Indeterminate"/>, never
    /// <see cref="SolveStatus.Unsolvable"/>; none of the limits changes the
    /// answer on a board the search can finish within them.
    /// </summary>
    public sealed class SearchBudget
    {
        /// <summary>
        /// The greatest solution length the search may return: a solution of
        /// exactly this many moves is found, and one that would be longer is
        /// not. <see cref="BreadthFirstStrategy"/> and
        /// <see cref="AStarStrategy"/> do not expand a node at this depth and
        /// go on with the shallower ones, and A\* also drops a successor whose
        /// moves so far plus its heuristic exceed it.
        /// <see cref="NearestNextClearStrategy"/> holds the total length of the
        /// routes it has committed to against it. A search that this limit cut
        /// and that found no solution reports
        /// <see cref="SolveStatus.Indeterminate"/>.
        /// </summary>
        public int MaxDepth { get; }

        /// <summary>
        /// The greatest number of states the search may take from its frontier
        /// to expand, over the whole search — across every stratum for
        /// <see cref="NearestNextClearStrategy"/>. Reaching it stops the search
        /// with <see cref="SolveStatus.Indeterminate"/>.
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
        /// How often a strategy polls <see cref="MaxWallClockMs"/> — once every
        /// this many expansions rather than every one.
        /// <c>Stopwatch.ElapsedMilliseconds</c> queries the OS timer on each read,
        /// and the expansion loop is the hottest in the project; the budget is a
        /// coarse safety ceiling, so a few hundred extra expansions before it
        /// trips do not matter. Shared here so every strategy honours the limit
        /// at the same cadence.
        /// </summary>
        internal const int WallClockPollInterval = 1024;

        /// <summary>
        /// Which move set the search branches on. The caller decides it, not
        /// the strategy (see <c>DECISIONS.md</c> D5), except that
        /// <see cref="NearestNextClearStrategy"/> rejects
        /// <see cref="MoveGenMode.Canonical"/>. The editor's Validate runs A\*
        /// under <see cref="MoveGenMode.Exhaustive"/> at a small budget, then
        /// nearest-next-clear, and cross-checks an unsolvable verdict of that
        /// with A\* under <see cref="MoveGenMode.Canonical"/> first and
        /// <see cref="MoveGenMode.Exhaustive"/> after (D37, D38).
        /// </summary>
        public MoveGenMode Mode { get; }

        /// <summary>
        /// Lets the caller stop a running search from another thread. Unlike the
        /// limits above it does not yield <see cref="SolveStatus.Indeterminate"/>:
        /// a cancelled search has no answer to report, so every strategy checks
        /// this once per expansion — a single flag read, far finer than the
        /// wall-clock poll — and throws <see cref="OperationCanceledException"/>.
        /// <see cref="CancellationToken.None"/> by default, which never cancels.
        /// </summary>
        public CancellationToken Cancellation { get; }

        /// <exception cref="ArgumentOutOfRangeException">
        /// Any numeric limit is below 1, or <paramref name="mode"/> is not a
        /// defined <see cref="MoveGenMode"/>. A zero or negative budget is a
        /// caller bug, not a tiny-but-valid budget.
        /// </exception>
        public SearchBudget(
            int maxDepth,
            int maxExploredStates,
            long maxWallClockMs,
            MoveGenMode mode,
            CancellationToken cancellation = default)
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
            Cancellation = cancellation;
        }

        /// <summary>This budget with every limit unchanged, cancellable through <paramref name="cancellation"/>.</summary>
        public SearchBudget WithCancellation(CancellationToken cancellation) =>
            new SearchBudget(MaxDepth, MaxExploredStates, MaxWallClockMs, Mode, cancellation);
    }
}
