using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// The outcome of one <see cref="ISearchStrategy.Search"/> call: whether a
    /// solution exists (<see cref="Status"/>), how good the returned one is
    /// known to be (<see cref="LengthLowerBound"/> and
    /// <see cref="ProvenShortestLength"/>), and the counters the Level Editor
    /// displays.
    /// </summary>
    /// <remarks>
    /// <para><b>Existence and quality are separate questions.</b>
    /// <see cref="SolveStatus.Solvable"/> says a verified solution exists; it
    /// does not say that solution is the shortest. Only
    /// <see cref="ProvenShortestLength"/> says that, and it is null unless the
    /// search actually proved it. Difficulty and time-budget code must read that
    /// field rather than <see cref="Solution"/>'s length, so an unproven length
    /// can never be treated as an optimum by accident.</para>
    /// <para><b>Proof is derived, not asserted.</b> A strategy supplies a lower
    /// bound; the solution is proven shortest exactly when the bound reaches the
    /// solution's length. A search that is optimal over the player's full move
    /// set proves it by passing the length itself as the bound. There is no way
    /// to mark a solution shortest without a bound that justifies it.</para>
    /// </remarks>
    public sealed class SolveResult
    {
        /// <summary>Which of the three outcomes the search reached.</summary>
        public SolveStatus Status { get; }

        /// <summary>
        /// A move sequence, verified against the rules, that drives the initial
        /// state to <see cref="BoardState.IsSolved"/>, in order. Not necessarily
        /// the shortest — see <see cref="ProvenShortestLength"/>. Empty for every
        /// status other than <see cref="SolveStatus.Solvable"/>, and also empty —
        /// length zero — when the initial state is already solved.
        /// </summary>
        public IReadOnlyList<Move> Solution { get; }

        /// <summary>
        /// No solution over the player's full move set is shorter than this.
        /// Meaningful for <see cref="SolveStatus.Solvable"/> and
        /// <see cref="SolveStatus.Indeterminate"/>; zero for
        /// <see cref="SolveStatus.Unsolvable"/>, where there is no solution to
        /// bound. Never exceeds <see cref="Solution"/>'s length.
        /// </summary>
        public int LengthLowerBound { get; }

        /// <summary>
        /// The length of the shortest solution over the player's full move set,
        /// when the search proved it — equal to <see cref="Solution"/>'s length —
        /// and null otherwise. A solution found under
        /// <see cref="MoveGenMode.Canonical"/> is usually not proven: canonical
        /// moves are a subset of the player's, so a shorter solution can exist
        /// outside them.
        /// </summary>
        public int? ProvenShortestLength =>
            Status == SolveStatus.Solvable && LengthLowerBound == Solution.Count
                ? Solution.Count
                : (int?)null;

        /// <summary>How many states the search expanded before it stopped.</summary>
        public int ExploredStateCount { get; }

        /// <summary>
        /// The largest the strategy's frontier grew: the breadth-first queue, or
        /// the A\* open set including superseded entries not yet popped (they
        /// occupy memory until they are). For <see cref="BreadthFirstStrategy"/>,
        /// stratification scopes the visited set, not the queue, so a stratified
        /// and a non-stratified run of the same board report the same value here
        /// — a divergence would be a bug. <see cref="PeakRetainedStateCount"/> is
        /// the number that legitimately differs between the two.
        /// </summary>
        public int PeakFrontierSize { get; }

        /// <summary>
        /// The high-water mark of states held in the visited set(s) at one time.
        /// For <see cref="BreadthFirstStrategy"/> with <c>stratifyVisitedSet</c>
        /// this drops below <see cref="ExploredStateCount"/> as retired strata are
        /// released; without it — and always for <see cref="AStarStrategy"/>,
        /// which does not stratify — the set only ever grows, so this equals the
        /// number of distinct states reached. The editor can show the gap as the
        /// memory the stratification saved.
        /// </summary>
        public int PeakRetainedStateCount { get; }

        /// <summary>Wall-clock time the search took, in milliseconds.</summary>
        public long ElapsedMs { get; }

        /// <exception cref="ArgumentException">
        /// <paramref name="lengthLowerBound"/> is negative, exceeds a solvable
        /// result's solution length, or is non-zero on an unsolvable result — a
        /// bug in the strategy that produced it, never a property of the level.
        /// </exception>
        internal SolveResult(
            SolveStatus status,
            IReadOnlyList<Move> solution,
            int lengthLowerBound,
            int exploredStateCount,
            int peakFrontierSize,
            int peakRetainedStateCount,
            long elapsedMs)
        {
            Status = status;
            Solution = solution ?? Array.Empty<Move>();

            if (lengthLowerBound < 0
                || (status == SolveStatus.Solvable && lengthLowerBound > Solution.Count)
                || (status == SolveStatus.Unsolvable && lengthLowerBound != 0))
            {
                throw new ArgumentException(
                    $"A {status} result cannot carry a length lower bound of {lengthLowerBound} " +
                    $"with a {Solution.Count}-move solution.",
                    nameof(lengthLowerBound));
            }

            LengthLowerBound = lengthLowerBound;
            ExploredStateCount = exploredStateCount;
            PeakFrontierSize = peakFrontierSize;
            PeakRetainedStateCount = peakRetainedStateCount;
            ElapsedMs = elapsedMs;
        }

        /// <summary>
        /// The result of a search that returns a shortest solution within its own
        /// move set — <see cref="BreadthFirstStrategy"/> and
        /// <see cref="AStarStrategy"/> — with the lower bound
        /// <see cref="LowerBoundForModeOptimalSearch"/> gives it.
        /// </summary>
        internal static SolveResult FromModeOptimalSearch(
            SolveStatus status,
            IReadOnlyList<Move> solution,
            MoveGenMode mode,
            LevelContext ctx,
            BoardState initial,
            int exploredStateCount,
            int peakFrontierSize,
            int peakRetainedStateCount,
            long elapsedMs)
        {
            var solutionLength = solution?.Count ?? 0;

            return new SolveResult(
                status,
                solution,
                LowerBoundForModeOptimalSearch(status, solutionLength, mode, ctx, initial),
                exploredStateCount,
                peakFrontierSize,
                peakRetainedStateCount,
                elapsedMs);
        }

        /// <summary>
        /// The lower bound a search that returns a shortest solution within its
        /// own move set can claim for <see cref="LengthLowerBound"/>.
        /// </summary>
        /// <remarks>
        /// Under <see cref="MoveGenMode.Exhaustive"/> the move set is the
        /// player's, so a solvable result's own length is the bound and the
        /// solution is proven shortest. Under <see cref="MoveGenMode.Canonical"/>
        /// that length only bounds canonical solutions, so the bound falls back to
        /// <see cref="AStarStrategy.EstimateRemainingMoves"/> at the initial state,
        /// which is admissible over the full move set. The same fallback covers an
        /// indeterminate result in either mode. A canonical solution whose length
        /// meets that estimate is still proven shortest — nothing can be shorter
        /// than an admissible bound.
        /// </remarks>
        internal static int LowerBoundForModeOptimalSearch(
            SolveStatus status,
            int solutionLength,
            MoveGenMode mode,
            LevelContext ctx,
            BoardState initial)
        {
            switch (status)
            {
                case SolveStatus.Unsolvable:
                    return 0;
                case SolveStatus.Solvable when mode == MoveGenMode.Exhaustive:
                    return solutionLength;
                default:
                    return AStarStrategy.EstimateRemainingMoves(ctx, initial);
            }
        }
    }
}
