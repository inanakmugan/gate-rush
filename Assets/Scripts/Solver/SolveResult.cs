using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// The outcome of one <see cref="ISearchStrategy.Search"/> call: the status,
    /// the shortest solution when there is one, and the counters the Level Editor
    /// displays.
    /// </summary>
    public sealed class SolveResult
    {
        /// <summary>Which of the three outcomes the search reached.</summary>
        public SolveStatus Status { get; }

        /// <summary>
        /// The shortest move sequence that drives the initial state to
        /// <see cref="BoardState.IsSolved"/>, in order. Empty for every status
        /// other than <see cref="SolveStatus.Solvable"/>, and also empty — length
        /// zero — when the initial state is already solved.
        /// </summary>
        public IReadOnlyList<Move> Solution { get; }

        /// <summary>How many states the search expanded before it stopped.</summary>
        public int ExploredStateCount { get; }

        /// <summary>
        /// The largest the breadth-first frontier queue grew. Stratification
        /// scopes the visited set, not the queue, so a stratified and a
        /// non-stratified run of the same board report the same value here — a
        /// divergence would be a bug. <see cref="PeakRetainedStateCount"/> is the
        /// number that legitimately differs between the two.
        /// </summary>
        public int PeakFrontierSize { get; }

        /// <summary>
        /// The high-water mark of states held in the visited set(s) at one time.
        /// With <c>stratifyVisitedSet</c> this drops below
        /// <see cref="ExploredStateCount"/> as retired strata are released;
        /// without it the visited set only ever grows, so this equals the number
        /// of distinct states visited. The editor can show the gap as the memory
        /// the stratification saved.
        /// </summary>
        public int PeakRetainedStateCount { get; }

        /// <summary>Wall-clock time the search took, in milliseconds.</summary>
        public long ElapsedMs { get; }

        internal SolveResult(
            SolveStatus status,
            IReadOnlyList<Move> solution,
            int exploredStateCount,
            int peakFrontierSize,
            int peakRetainedStateCount,
            long elapsedMs)
        {
            Status = status;
            Solution = solution ?? Array.Empty<Move>();
            ExploredStateCount = exploredStateCount;
            PeakFrontierSize = peakFrontierSize;
            PeakRetainedStateCount = peakRetainedStateCount;
            ElapsedMs = elapsedMs;
        }
    }
}
