using System;
using GateRush.Core;
using GateRush.Solver;

namespace GateRush.Editor
{
    /// <summary>What the A\* cross-check made of a nearest-next-clear Unsolvable verdict.</summary>
    public enum UnsolvableCrossCheck
    {
        /// <summary>The verdict was not Unsolvable, so no cross-check ran.</summary>
        NotRun,

        /// <summary>A\* also proved the level unsolvable within its budgets.</summary>
        Confirmed,

        /// <summary>
        /// A\* ran out of budget without finding a solution: it did not
        /// contradict the verdict, but did not confirm it either. The verdict
        /// stands on the clear-monotonicity argument alone.
        /// </summary>
        Inconclusive,
    }

    /// <summary>
    /// The outcome of <see cref="NextClearRunner.Run"/>: the
    /// <see cref="NearestNextClearStrategy"/> result, and — when that result is
    /// Unsolvable — what the A\* cross-check made of it.
    /// </summary>
    public sealed class NextClearRunResult
    {
        internal NextClearRunResult(SolveResult nextClear, LevelSolveResult crossCheck, UnsolvableCrossCheck crossCheckOutcome)
        {
            NextClear = nextClear;
            CrossCheck = crossCheck;
            CrossCheckOutcome = crossCheckOutcome;
        }

        /// <summary>The nearest-next-clear search's own result. Its status is the verdict.</summary>
        public SolveResult NextClear { get; }

        /// <summary>The A\* two-stage result used as corroboration; null unless the cross-check ran.</summary>
        public LevelSolveResult CrossCheck { get; }

        public UnsolvableCrossCheck CrossCheckOutcome { get; }
    }

    /// <summary>
    /// Thrown when <see cref="NearestNextClearStrategy"/> reports a level
    /// unsolvable and the A\* cross-check finds a solution. One of the two is
    /// wrong — most likely the clear-monotonicity argument or the code relying on
    /// it — so neither verdict is reported.
    /// </summary>
    public sealed class SolverDisagreementException : Exception
    {
        internal SolverDisagreementException(LevelContext ctx, SolveResult nextClear, LevelSolveResult crossCheck)
            : base(
                $"Solver disagreement on level {ctx.LevelId}: nearest-next-clear reported it unsolvable, but A* " +
                $"found a {crossCheck.Solution.Count}-move solution. Neither verdict can be trusted; this is a solver " +
                "bug, not a property of the level.")
        {
            NextClear = nextClear;
            CrossCheck = crossCheck;
        }

        public SolveResult NextClear { get; }

        public LevelSolveResult CrossCheck { get; }
    }

    /// <summary>
    /// Runs <see cref="NearestNextClearStrategy"/> and, whenever it would report
    /// a level unsolvable, corroborates that with the existing A\* two-stage
    /// search (<see cref="LevelSolveRunner"/>) at the editor's ordinary canonical
    /// and exhaustive budgets before the verdict is surfaced.
    /// </summary>
    /// <remarks>
    /// <para><b>Cross-check rules.</b> A\* Unsolvable:
    /// <see cref="UnsolvableCrossCheck.Confirmed"/>. A\* Indeterminate:
    /// <see cref="UnsolvableCrossCheck.Inconclusive"/> — the verdict stands,
    /// labelled, because on exactly the levels this strategy exists for A\*
    /// runs out of budget. A\* Solvable: the two disagree, and
    /// <see cref="SolverDisagreementException"/> is thrown rather than either
    /// answer being reported.</para>
    /// <para><b>Test seam.</b> Both strategies come from factories so a test can
    /// substitute a strategy that reports a wrong verdict and prove the
    /// disagreement path fires.</para>
    /// </remarks>
    public sealed class NextClearRunner
    {
        private readonly Func<ISearchStrategy> nextClearFactory;
        private readonly Func<ISearchStrategy> crossCheckFactory;

        public NextClearRunner(
            Func<ISearchStrategy> nextClearFactory = null,
            Func<ISearchStrategy> crossCheckFactory = null)
        {
            this.nextClearFactory = nextClearFactory ?? (() => new NearestNextClearStrategy());
            this.crossCheckFactory = crossCheckFactory ?? (() => new AStarStrategy());
        }

        /// <param name="crossCheckStarting">
        /// Invoked just before the A\* cross-check, when one runs — the only
        /// progress signal a caller gets for it.
        /// </param>
        /// <exception cref="SolverDisagreementException">
        /// Nearest-next-clear reported Unsolvable and the A\* cross-check found a
        /// solution.
        /// </exception>
        public NextClearRunResult Run(
            LevelContext ctx,
            SearchBudget nextClearBudget,
            SearchBudget canonicalBudget,
            SearchBudget exhaustiveBudget,
            Action crossCheckStarting = null)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (nextClearBudget == null)
            {
                throw new ArgumentNullException(nameof(nextClearBudget));
            }

            if (canonicalBudget == null)
            {
                throw new ArgumentNullException(nameof(canonicalBudget));
            }

            if (exhaustiveBudget == null)
            {
                throw new ArgumentNullException(nameof(exhaustiveBudget));
            }

            var nextClear = nextClearFactory().Search(ctx, BoardState.CreateInitial(ctx), nextClearBudget);
            if (nextClear.Status != SolveStatus.Unsolvable)
            {
                return new NextClearRunResult(nextClear, crossCheck: null, UnsolvableCrossCheck.NotRun);
            }

            crossCheckStarting?.Invoke();
            var crossCheck = new LevelSolveRunner(crossCheckFactory).Run(ctx, canonicalBudget, exhaustiveBudget);
            switch (crossCheck.Verdict)
            {
                case LevelSolveVerdict.Solvable:
                    throw new SolverDisagreementException(ctx, nextClear, crossCheck);
                case LevelSolveVerdict.Unsolvable:
                    return new NextClearRunResult(nextClear, crossCheck, UnsolvableCrossCheck.Confirmed);
                default:
                    return new NextClearRunResult(nextClear, crossCheck, UnsolvableCrossCheck.Inconclusive);
            }
        }
    }
}
