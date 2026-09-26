using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Solver;

namespace GateRush.Editor
{
    /// <summary>The verdict a two-stage solve reaches for the editor (D4, D5).</summary>
    public enum LevelSolveVerdict
    {
        /// <summary>A solution was found — canonical or exhaustive.</summary>
        Solvable,

        /// <summary>
        /// The exhaustive search covered the whole reachable state space without
        /// a solution and without hitting a budget limit. Only the exhaustive
        /// stage can conclude this: a canonical <see cref="SolveStatus.Unsolvable"/>
        /// means only "not found in the pruned move set" (D5).
        /// </summary>
        Unsolvable,

        /// <summary>A budget limit stopped a stage before it could prove anything.</summary>
        Indeterminate,
    }

    /// <summary>
    /// The outcome of <see cref="LevelSolveRunner.Run"/>: the verdict, a verified
    /// solution when there is one and what is known about its length, which
    /// stage found it, and both raw <see cref="SolveResult"/>s for the metrics
    /// panel.
    /// </summary>
    /// <remarks>
    /// Existence (<see cref="Verdict"/>) and quality
    /// (<see cref="LengthLowerBound"/>, <see cref="ProvenShortestLength"/>) are
    /// separate, as on <see cref="SolveResult"/>: a canonical-stage solution is
    /// playable, but proven shortest only when its length meets the lower
    /// bound — a shorter one can use moves canonical pruning left out. Time-budget and difficulty
    /// code must read <see cref="ProvenShortestLength"/> and handle its null
    /// case explicitly rather than take <see cref="Solution"/>'s length.
    /// </remarks>
    public sealed class LevelSolveResult
    {
        public LevelSolveVerdict Verdict { get; }

        /// <summary>A verified solution; not necessarily the shortest. Empty unless <see cref="Verdict"/> is Solvable.</summary>
        public IReadOnlyList<Move> Solution { get; }

        /// <summary>
        /// No solution is shorter than this. From the solving stage when
        /// Solvable; the larger of the two stages' bounds when Indeterminate —
        /// both are valid, so the tighter one wins; zero when Unsolvable.
        /// </summary>
        public int LengthLowerBound { get; }

        /// <summary>
        /// The shortest solution's length when the solving stage proved it, else
        /// null. Always null unless <see cref="Verdict"/> is Solvable.
        /// </summary>
        public int? ProvenShortestLength { get; }

        /// <summary>Which move set produced the solution. Meaningful only when <see cref="Verdict"/> is Solvable.</summary>
        public MoveGenMode SolvedBy { get; }

        /// <summary>The canonical stage's raw result. Always present.</summary>
        public SolveResult Canonical { get; }

        /// <summary>The exhaustive stage's raw result, or null when the canonical stage already solved the level.</summary>
        public SolveResult Exhaustive { get; }

        internal LevelSolveResult(
            LevelSolveVerdict verdict,
            IReadOnlyList<Move> solution,
            MoveGenMode solvedBy,
            SolveResult canonical,
            SolveResult exhaustive)
        {
            Verdict = verdict;
            Solution = solution ?? Array.Empty<Move>();
            SolvedBy = solvedBy;
            Canonical = canonical;
            Exhaustive = exhaustive;

            switch (verdict)
            {
                case LevelSolveVerdict.Solvable:
                    var solving = solvedBy == MoveGenMode.Exhaustive ? exhaustive : canonical;
                    LengthLowerBound = solving.LengthLowerBound;
                    ProvenShortestLength = solving.ProvenShortestLength;
                    break;
                case LevelSolveVerdict.Indeterminate:
                    LengthLowerBound = Math.Max(canonical.LengthLowerBound, exhaustive?.LengthLowerBound ?? 0);
                    ProvenShortestLength = null;
                    break;
                default:
                    LengthLowerBound = 0;
                    ProvenShortestLength = null;
                    break;
            }
        }
    }

    /// <summary>
    /// The two-stage solver invocation the editor's <c>[ Validate ]</c> button
    /// runs (D5). Canonical first; if it does not find a solution, exhaustive
    /// with a larger budget. The strategy honours a budget — this class owns the
    /// <em>policy</em>: what to try next and how to read the two results.
    /// </summary>
    /// <remarks>
    /// <para><b>Verdict rules.</b> A canonical <see cref="SolveStatus.Solvable"/>
    /// ends it — canonical pruning is a subset of the player's moves, so a
    /// solution it finds is genuinely playable (D5). Anything else falls through
    /// to exhaustive, and the verdict comes from there: exhaustive
    /// <see cref="SolveStatus.Solvable"/> is Solvable, exhaustive
    /// <see cref="SolveStatus.Unsolvable"/> is Unsolvable, and any
    /// <see cref="SolveStatus.Indeterminate"/> with no solution is Indeterminate.
    /// A canonical <see cref="SolveStatus.Unsolvable"/> never concludes anything
    /// on its own.</para>
    /// <para><b>Test seam.</b> The strategy is supplied by a factory so a test
    /// can record which budgets were searched. The default builds a fresh
    /// <see cref="BreadthFirstStrategy"/> per stage, matching its
    /// construct-one-per-search contract.</para>
    /// </remarks>
    public sealed class LevelSolveRunner
    {
        private readonly Func<ISearchStrategy> strategyFactory;

        public LevelSolveRunner(Func<ISearchStrategy> strategyFactory = null)
        {
            this.strategyFactory = strategyFactory ?? (() => new BreadthFirstStrategy());
        }

        /// <param name="stageStarting">
        /// Invoked with <see cref="MoveGenMode.Canonical"/> just before the
        /// canonical search and, if it runs, with <see cref="MoveGenMode.Exhaustive"/>
        /// just before the exhaustive one. The search is synchronous and opaque,
        /// so this is the only progress signal a caller gets — the editor uses it
        /// to raise a progress bar.
        /// </param>
        public LevelSolveResult Run(
            LevelContext ctx,
            SearchBudget canonicalBudget,
            SearchBudget exhaustiveBudget,
            Action<MoveGenMode> stageStarting = null)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (canonicalBudget == null)
            {
                throw new ArgumentNullException(nameof(canonicalBudget));
            }

            if (exhaustiveBudget == null)
            {
                throw new ArgumentNullException(nameof(exhaustiveBudget));
            }

            var initial = BoardState.CreateInitial(ctx);

            stageStarting?.Invoke(MoveGenMode.Canonical);
            var canonical = strategyFactory().Search(ctx, initial, canonicalBudget);
            if (canonical.Status == SolveStatus.Solvable)
            {
                return new LevelSolveResult(
                    LevelSolveVerdict.Solvable, canonical.Solution, MoveGenMode.Canonical, canonical, null);
            }

            stageStarting?.Invoke(MoveGenMode.Exhaustive);
            var exhaustive = strategyFactory().Search(ctx, initial, exhaustiveBudget);
            switch (exhaustive.Status)
            {
                case SolveStatus.Solvable:
                    return new LevelSolveResult(
                        LevelSolveVerdict.Solvable, exhaustive.Solution, MoveGenMode.Exhaustive, canonical, exhaustive);
                case SolveStatus.Unsolvable:
                    return new LevelSolveResult(
                        LevelSolveVerdict.Unsolvable, Array.Empty<Move>(), MoveGenMode.Exhaustive, canonical, exhaustive);
                default:
                    return new LevelSolveResult(
                        LevelSolveVerdict.Indeterminate, Array.Empty<Move>(), MoveGenMode.Exhaustive, canonical, exhaustive);
            }
        }
    }
}
