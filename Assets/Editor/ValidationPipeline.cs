using System;
using System.Collections.Generic;
using System.Threading;
using GateRush.Core;
using GateRush.Solver;

namespace GateRush.Editor
{
    /// <summary>The step of <see cref="ValidationPipeline.Run"/> that is running, or that produced the answer.</summary>
    public enum ValidationStage
    {
        /// <summary>Exhaustive A\* at a small budget: a proven answer for easy levels.</summary>
        QuickOptimal,

        /// <summary><see cref="NearestNextClearStrategy"/>, run when the quick attempt ran out of budget.</summary>
        NearestNextClear,

        /// <summary>The A\* cross-check <see cref="NextClearRunner"/> runs on a nearest-next-clear Unsolvable.</summary>
        CrossCheck,
    }

    /// <summary>
    /// What Validate concluded: the verdict, a verified solution and what is
    /// known about its length, which stage answered, and the raw stage results
    /// behind it.
    /// </summary>
    /// <remarks>
    /// Existence (<see cref="Verdict"/>) and quality
    /// (<see cref="LengthLowerBound"/>, <see cref="ProvenShortestLength"/>) are
    /// separate, as on <see cref="SolveResult"/>. Time-budget and difficulty
    /// code must read <see cref="ProvenShortestLength"/> and handle its null
    /// case explicitly.
    /// </remarks>
    public sealed class ValidationResult
    {
        internal ValidationResult(ValidationStage answeredBy, SolveResult quick, NextClearRunResult nextClear)
        {
            AnsweredBy = answeredBy;
            Quick = quick;
            NextClear = nextClear;

            var answering = Answering;
            Verdict = ToVerdict(answering.Status);
            Solution = answering.Solution;

            // Both stages' bounds are valid for the level; the tighter one wins.
            LengthLowerBound = Verdict == LevelSolveVerdict.Unsolvable
                ? 0
                : Math.Max(quick.LengthLowerBound, answering.LengthLowerBound);
            ProvenShortestLength = Verdict == LevelSolveVerdict.Solvable && LengthLowerBound == Solution.Count
                ? Solution.Count
                : (int?)null;
        }

        /// <summary><see cref="ValidationStage.QuickOptimal"/> or <see cref="ValidationStage.NearestNextClear"/>.</summary>
        public ValidationStage AnsweredBy { get; }

        public LevelSolveVerdict Verdict { get; }

        /// <summary>A verified solution; not necessarily the shortest. Empty unless <see cref="Verdict"/> is Solvable.</summary>
        public IReadOnlyList<Move> Solution { get; }

        /// <summary>No solution is shorter than this. Zero when Unsolvable.</summary>
        public int LengthLowerBound { get; }

        /// <summary>The shortest solution's length when proven, else null. Null unless <see cref="Verdict"/> is Solvable.</summary>
        public int? ProvenShortestLength { get; }

        /// <summary>The quick optimal attempt's result. Always present.</summary>
        public SolveResult Quick { get; }

        /// <summary>The nearest-next-clear run, with its cross-check; null when the quick attempt answered.</summary>
        public NextClearRunResult NextClear { get; }

        /// <summary>What the A\* cross-check made of a nearest-next-clear Unsolvable; <see cref="UnsolvableCrossCheck.NotRun"/> otherwise.</summary>
        public UnsolvableCrossCheck CrossCheckOutcome => NextClear?.CrossCheckOutcome ?? UnsolvableCrossCheck.NotRun;

        /// <summary>The raw result of the stage that answered.</summary>
        public SolveResult Answering => NextClear?.NextClear ?? Quick;

        private static LevelSolveVerdict ToVerdict(SolveStatus status)
        {
            switch (status)
            {
                case SolveStatus.Solvable:
                    return LevelSolveVerdict.Solvable;
                case SolveStatus.Unsolvable:
                    return LevelSolveVerdict.Unsolvable;
                default:
                    return LevelSolveVerdict.Indeterminate;
            }
        }
    }

    /// <summary>
    /// What <see cref="ValidationPipeline.Run"/> returns: either a
    /// <see cref="ValidationResult"/>, or the reason the level could not be
    /// validated at all. Exactly one of <see cref="Result"/> and
    /// <see cref="NotValidatableReason"/> is non-null.
    /// </summary>
    /// <remarks>
    /// A separate wrapper, not a flag on <see cref="ValidationResult"/>, so that
    /// "no verdict" can never be read as a verdict: a flagged result would
    /// still expose a <see cref="ValidationResult.Verdict"/>, and its default,
    /// <see cref="LevelSolveVerdict.Solvable"/>, is the worst possible thing to
    /// read by mistake. Here the verdict is reachable only through a
    /// <see cref="Result"/> that is null.
    /// </remarks>
    public sealed class ValidationOutcome
    {
        private ValidationOutcome(ValidationResult result, string notValidatableReason)
        {
            Result = result;
            NotValidatableReason = notValidatableReason;
        }

        /// <summary>The pipeline's answer; null when the level could not be validated.</summary>
        public ValidationResult Result { get; }

        /// <summary>Why no search ran; null when <see cref="Result"/> is present. Not an error — the level may be fine.</summary>
        public string NotValidatableReason { get; }

        internal static ValidationOutcome Of(ValidationResult result) =>
            new ValidationOutcome(result ?? throw new ArgumentNullException(nameof(result)), null);

        internal static ValidationOutcome NotValidatable(string reason) =>
            new ValidationOutcome(null, reason ?? throw new ArgumentNullException(nameof(reason)));
    }

    /// <summary>
    /// The Level Editor's single Validate: a quick optimal attempt, then
    /// nearest-next-clear with its A\* cross-check. Synchronous and free of Unity
    /// APIs, so the editor can run it on a worker thread; everything it reads is
    /// passed in.
    /// </summary>
    /// <remarks>
    /// <para><b>Pipeline.</b> First, exhaustive A\* at the quick budget. On an
    /// exhaustive move set its Solvable is proven shortest and its Unsolvable is
    /// a proof, so either ends the run. Indeterminate falls through to
    /// <see cref="NextClearRunner"/> at the normal budgets.</para>
    /// <para><b>Cancellation.</b> <paramref name="cancellation"/> is attached
    /// to every budget, so each search checks it before every expansion, and it
    /// is checked again between stages. A cancelled run throws
    /// <see cref="OperationCanceledException"/>; it has no result.</para>
    /// <para><b>Errors are not results.</b> A
    /// <see cref="SolverDisagreementException"/>, or any exception a search
    /// throws on a bug, propagates to the caller unchanged.</para>
    /// <para><b>Not yet every level.</b> A level with a generator or an
    /// elevator gets <see cref="ValidationOutcome.NotValidatableReason"/>
    /// before any search runs — neither a verdict nor an error — until spawning
    /// lands in phase 1.13 (<see cref="SpawnersNotYetSupportedReason"/>).</para>
    /// </remarks>
    public sealed class ValidationPipeline
    {
        private readonly Func<ISearchStrategy> quickFactory;
        private readonly NextClearRunner nextClearRunner;

        public ValidationPipeline(Func<ISearchStrategy> quickFactory = null, NextClearRunner nextClearRunner = null)
        {
            this.quickFactory = quickFactory ?? (() => new AStarStrategy());
            this.nextClearRunner = nextClearRunner ?? new NextClearRunner();
        }

        /// <summary>
        /// Why no level with a generator or an elevator can be validated yet.
        /// Spawning does not exist until phase 1.13, so on such a level
        /// <see cref="BoardState.IsSolved"/> can never become true and every
        /// search would report <see cref="SolveStatus.Unsolvable"/> as if proven.
        /// </summary>
        public const string SpawnersNotYetSupportedReason =
            "Levels with generators or elevators cannot be validated until spawning is implemented " +
            "(phase 1.13). Until then every search would report such a level unsolvable, which proves nothing.";

        /// <summary>
        /// Validates <paramref name="ctx"/>: a verdict in
        /// <see cref="ValidationOutcome.Result"/>, or — for a level no search
        /// can judge yet — the reason in
        /// <see cref="ValidationOutcome.NotValidatableReason"/>, with no search
        /// run and no stage announced.
        /// </summary>
        /// <param name="quickBudget">Must be exhaustive: only then is a quick answer a proof.</param>
        /// <param name="nextClearBudget">Nearest-next-clear's own budget; exhaustive.</param>
        /// <param name="canonicalBudget">The cross-check's first stage.</param>
        /// <param name="exhaustiveBudget">The cross-check's second stage.</param>
        /// <param name="stageStarting">
        /// Invoked, on the calling thread, just before each stage starts. The
        /// searches are opaque, so this is the only progress signal.
        /// </param>
        /// <exception cref="ArgumentException"><paramref name="quickBudget"/> is not exhaustive.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled.</exception>
        /// <exception cref="SolverDisagreementException">See <see cref="NextClearRunner"/>.</exception>
        public ValidationOutcome Run(
            LevelContext ctx,
            SearchBudget quickBudget,
            SearchBudget nextClearBudget,
            SearchBudget canonicalBudget,
            SearchBudget exhaustiveBudget,
            Action<ValidationStage> stageStarting = null,
            CancellationToken cancellation = default)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (quickBudget == null)
            {
                throw new ArgumentNullException(nameof(quickBudget));
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

            if (quickBudget.Mode != MoveGenMode.Exhaustive)
            {
                throw new ArgumentException(
                    "The quick attempt must be exhaustive: only then are its answers proofs.", nameof(quickBudget));
            }

            cancellation.ThrowIfCancellationRequested();

            // TODO(1.13): remove once MoveResolver.CheckSpawnTriggers spawns
            // generator output and elevator waves.
            if (ctx.Generators.Count > 0 || ctx.Elevators.Count > 0)
            {
                return ValidationOutcome.NotValidatable(SpawnersNotYetSupportedReason);
            }

            stageStarting?.Invoke(ValidationStage.QuickOptimal);
            var quick = quickFactory().Search(ctx, BoardState.CreateInitial(ctx), quickBudget.WithCancellation(cancellation));
            if (quick.Status != SolveStatus.Indeterminate)
            {
                return ValidationOutcome.Of(new ValidationResult(ValidationStage.QuickOptimal, quick, nextClear: null));
            }

            cancellation.ThrowIfCancellationRequested();
            stageStarting?.Invoke(ValidationStage.NearestNextClear);
            var nextClear = nextClearRunner.Run(
                ctx,
                nextClearBudget.WithCancellation(cancellation),
                canonicalBudget.WithCancellation(cancellation),
                exhaustiveBudget.WithCancellation(cancellation),
                () =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    stageStarting?.Invoke(ValidationStage.CrossCheck);
                });

            return ValidationOutcome.Of(new ValidationResult(ValidationStage.NearestNextClear, quick, nextClear));
        }
    }
}
