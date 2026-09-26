using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Fixture;
using static GateRush.Tests.SearchCorpus;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ValidationPipeline"/>: the quick exhaustive attempt
    /// answers easy levels with a proof, anything it cannot settle falls through
    /// to nearest-next-clear and its cross-check, stages are announced in order,
    /// a solver disagreement or a rejected generated move propagates rather
    /// than becoming a verdict, a level with generators or elevators is declined
    /// before any search with a reason rather than a verdict, and
    /// cancellation — before the run or in the middle of a search — ends it
    /// with <see cref="OperationCanceledException"/>.
    /// </summary>
    public class ValidationPipelineTests
    {
        /// <summary>Reports every board unsolvable — a stand-in for a broken nearest-next-clear.</summary>
        private sealed class AlwaysUnsolvableStrategy : ISearchStrategy
        {
            public SolveResult Search(LevelContext ctx, BoardState initial, SearchBudget budget) =>
                new SolveResult(SolveStatus.Unsolvable, null, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// Blocks until its budget's cancellation fires, then stops the way a real
        /// search does. Signals when it has started, so a test can cancel while it
        /// is genuinely mid-search.
        /// </summary>
        private sealed class BlockUntilCancelledStrategy : ISearchStrategy
        {
            public readonly ManualResetEventSlim Started = new ManualResetEventSlim();

            public SolveResult Search(LevelContext ctx, BoardState initial, SearchBudget budget)
            {
                Started.Set();
                budget.Cancellation.WaitHandle.WaitOne(TimeSpan.FromSeconds(TestTimeoutSeconds));
                budget.Cancellation.ThrowIfCancellationRequested();
                throw new InvalidOperationException("The search was never cancelled.");
            }
        }

        private const int TestTimeoutSeconds = 10;

        private static SearchBudget Quick(int maxExplored = 100_000) =>
            new SearchBudget(200, maxExplored, 10_000, MoveGenMode.Exhaustive);

        private static SearchBudget Normal() => new SearchBudget(200, 500_000, 10_000, MoveGenMode.Exhaustive);

        private static SearchBudget Canonical() => new SearchBudget(64, 200_000, 10_000, MoveGenMode.Canonical);

        /// <summary>A red block on an open 3x3 board with only a blue gate: unsolvable, with room to move.</summary>
        private static LevelContext NoGateForItsColour() =>
            Ctx(3, 3, new[] { Block(1, new Coord(1, 1)) }, new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Blue) });

        private static ValidationOutcome Outcome(
            LevelContext ctx, SearchBudget quick, List<ValidationStage> stages = null, ValidationPipeline pipeline = null) =>
            (pipeline ?? new ValidationPipeline()).Run(ctx, quick, Normal(), Canonical(), Normal(), stages == null ? (Action<ValidationStage>)null : stages.Add);

        /// <summary>
        /// The result of a run on a level every search can judge. Asserts the
        /// run did not decline the level, so no test here reads a missing verdict.
        /// </summary>
        private static ValidationResult Run(
            LevelContext ctx, SearchBudget quick, List<ValidationStage> stages = null, ValidationPipeline pipeline = null)
        {
            var outcome = Outcome(ctx, quick, stages, pipeline);
            Assert.IsNull(outcome.NotValidatableReason, "a level without generators or elevators must get a verdict");
            return outcome.Result;
        }

        /// <summary>A quick-stage factory that records whether any search was ever started.</summary>
        private sealed class SearchSpy
        {
            public bool Searched { get; private set; }

            public ISearchStrategy Create()
            {
                Searched = true;
                return new AStarStrategy();
            }
        }

        // ----- Which stage answers --------------------------------------------

        [Test]
        public void Run_QuickAttemptSolves_AnswersProvenShortestAlone()
        {
            var stages = new List<ValidationStage>();

            var result = Run(StepAsideBeforeFirstClearBoard(), Quick(), stages);

            Assert.AreEqual(ValidationStage.QuickOptimal, result.AnsweredBy);
            Assert.AreEqual(LevelSolveVerdict.Solvable, result.Verdict);
            Assert.AreEqual(3, result.ProvenShortestLength);
            Assert.IsNull(result.NextClear);
            CollectionAssert.AreEqual(new[] { ValidationStage.QuickOptimal }, stages);
        }

        [Test]
        public void Run_QuickAttemptProvesUnsolvable_AnswersWithoutACrossCheck()
        {
            var stages = new List<ValidationStage>();

            var result = Run(NoGateForItsColour(), Quick(), stages);

            Assert.AreEqual(ValidationStage.QuickOptimal, result.AnsweredBy);
            Assert.AreEqual(LevelSolveVerdict.Unsolvable, result.Verdict);
            Assert.AreEqual(UnsolvableCrossCheck.NotRun, result.CrossCheckOutcome);
            CollectionAssert.AreEqual(new[] { ValidationStage.QuickOptimal }, stages);
        }

        [Test]
        public void Run_QuickAttemptOutOfBudget_NearestNextClearAnswers()
        {
            var stages = new List<ValidationStage>();

            var result = Run(StepAsideBeforeFirstClearBoard(), Quick(maxExplored: 1), stages);

            Assert.AreEqual(ValidationStage.NearestNextClear, result.AnsweredBy);
            Assert.AreEqual(LevelSolveVerdict.Solvable, result.Verdict);
            Assert.AreEqual(3, result.Solution.Count);
            Assert.AreEqual(2, result.LengthLowerBound);
            Assert.IsNull(result.ProvenShortestLength);
            Assert.AreEqual(SolveStatus.Indeterminate, result.Quick.Status);
            CollectionAssert.AreEqual(new[] { ValidationStage.QuickOptimal, ValidationStage.NearestNextClear }, stages);
        }

        [Test]
        public void Run_NearestNextClearUnsolvable_IsCrossCheckedAndReportsTheOutcome()
        {
            var stages = new List<ValidationStage>();

            var result = Run(NoGateForItsColour(), Quick(maxExplored: 1), stages);

            Assert.AreEqual(ValidationStage.NearestNextClear, result.AnsweredBy);
            Assert.AreEqual(LevelSolveVerdict.Unsolvable, result.Verdict);
            Assert.AreEqual(UnsolvableCrossCheck.Confirmed, result.CrossCheckOutcome);
            Assert.AreEqual(0, result.LengthLowerBound);
            CollectionAssert.AreEqual(
                new[] { ValidationStage.QuickOptimal, ValidationStage.NearestNextClear, ValidationStage.CrossCheck },
                stages);
        }

        // ----- Errors are not verdicts ------------------------------------------

        [Test]
        public void Run_SolverDisagreement_Propagates()
        {
            var pipeline = new ValidationPipeline(
                nextClearRunner: new NextClearRunner(nextClearFactory: () => new AlwaysUnsolvableStrategy()));

            Assert.Throws<SolverDisagreementException>(
                () => Run(StepAsideBeforeFirstClearBoard(), Quick(maxExplored: 1), pipeline: pipeline));
        }

        [Test]
        public void Run_SearchGeneratesAMoveTheResolverRejects_Propagates()
        {
            var pipeline = new ValidationPipeline(quickFactory: () => new AStarStrategy(() => new IllegalMoveGenerator()));

            Assert.Throws<InvalidOperationException>(
                () => Run(StepAsideBeforeFirstClearBoard(), Quick(), pipeline: pipeline));
        }

        // ----- Levels no search can judge yet -----------------------------------

        [Test]
        public void Run_LevelWithAGenerator_IsNotValidatableAndRunsNoSearch()
        {
            var spy = new SearchSpy();
            var stages = new List<ValidationStage>();
            var ctx = Ctx(
                3, 3,
                new[] { Block(1, new Coord(1, 1)) },
                new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) },
                generators: new[] { Spawner(1, BoardEdge.Top, 0, 1, Spawned()) });

            var outcome = Outcome(ctx, Quick(), stages, new ValidationPipeline(quickFactory: spy.Create));

            Assert.IsNull(outcome.Result);
            Assert.AreEqual(ValidationPipeline.SpawnersNotYetSupportedReason, outcome.NotValidatableReason);
            Assert.IsFalse(spy.Searched);
            CollectionAssert.IsEmpty(stages);
        }

        [Test]
        public void Run_LevelWithAnElevator_IsNotValidatableAndRunsNoSearch()
        {
            var spy = new SearchSpy();
            var stages = new List<ValidationStage>();
            var ctx = Ctx(
                3, 3,
                new[] { Block(1, new Coord(1, 1)) },
                new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) },
                elevators: new[] { Elevator(1, new Coord(2, 2), new Coord(2, 2), new[] { Spawned(regionOrigin: new Coord(0, 0)) }) });

            var outcome = Outcome(ctx, Quick(), stages, new ValidationPipeline(quickFactory: spy.Create));

            Assert.IsNull(outcome.Result);
            Assert.AreEqual(ValidationPipeline.SpawnersNotYetSupportedReason, outcome.NotValidatableReason);
            Assert.IsFalse(spy.Searched);
            CollectionAssert.IsEmpty(stages);
        }

        [Test]
        public void Run_CanonicalQuickBudget_Throws()
        {
            var canonicalQuick = new SearchBudget(200, 100_000, 10_000, MoveGenMode.Canonical);

            Assert.Throws<ArgumentException>(() => Run(StepAsideBeforeFirstClearBoard(), canonicalQuick));
        }

        // ----- Cancellation -------------------------------------------------------

        [Test]
        public void Run_CancelledBeforeItStarts_ThrowsOperationCanceled()
        {
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();

                Assert.Throws<OperationCanceledException>(() => new ValidationPipeline().Run(
                    StepAsideBeforeFirstClearBoard(), Quick(), Normal(), Canonical(), Normal(),
                    cancellation: source.Token));
            }
        }

        [Test]
        public void Run_CancelledInTheMiddleOfASearch_StopsWithOperationCanceled()
        {
            var blocking = new BlockUntilCancelledStrategy();
            var pipeline = new ValidationPipeline(quickFactory: () => blocking);

            using (var source = new CancellationTokenSource())
            {
                var run = Task.Run(() => pipeline.Run(
                    StepAsideBeforeFirstClearBoard(), Quick(), Normal(), Canonical(), Normal(),
                    cancellation: source.Token));

                Assert.IsTrue(blocking.Started.Wait(TimeSpan.FromSeconds(TestTimeoutSeconds)), "the search never started");
                source.Cancel();

                var failure = Assert.Throws<AggregateException>(
                    () => run.Wait(TimeSpan.FromSeconds(TestTimeoutSeconds)));
                Assert.IsInstanceOf<OperationCanceledException>(failure.GetBaseException());
            }
        }
    }
}
