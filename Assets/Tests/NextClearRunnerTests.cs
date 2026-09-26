using GateRush.Core;
using GateRush.Editor;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Fixture;
using static GateRush.Tests.SearchCorpus;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="NextClearRunner"/>'s cross-check policy: a
    /// nearest-next-clear Unsolvable is corroborated by the A\* two-stage search
    /// before it is reported, confirmed when A\* agrees, labelled inconclusive
    /// when A\* runs out of budget, and a loud
    /// <see cref="SolverDisagreementException"/> when A\* finds a solution.
    /// Other verdicts pass through without a cross-check.
    /// </summary>
    public class NextClearRunnerTests
    {
        /// <summary>Reports every board unsolvable — a stand-in for a broken strategy.</summary>
        private sealed class AlwaysUnsolvableStrategy : ISearchStrategy
        {
            public SolveResult Search(LevelContext ctx, BoardState initial, SearchBudget budget) =>
                new SolveResult(SolveStatus.Unsolvable, null, 0, 0, 0, 0, 0);
        }

        /// <summary>Counts searches, delegating to a fresh A\* each time.</summary>
        private sealed class CountingStrategy : ISearchStrategy
        {
            public int Searches;

            public SolveResult Search(LevelContext ctx, BoardState initial, SearchBudget budget)
            {
                Searches++;
                return new AStarStrategy().Search(ctx, initial, budget);
            }
        }

        private static SearchBudget NextClear(int maxExplored = 200_000) =>
            new SearchBudget(200, maxExplored, 60_000, MoveGenMode.Exhaustive);

        private static SearchBudget Canonical(int maxExplored = 200_000) =>
            new SearchBudget(64, maxExplored, 10_000, MoveGenMode.Canonical);

        private static SearchBudget Exhaustive(int maxExplored = 500_000) =>
            new SearchBudget(64, maxExplored, 10_000, MoveGenMode.Exhaustive);

        /// <summary>A red block on an open 3x3 board with only a blue gate: unsolvable, with room to move.</summary>
        private static LevelContext NoGateForItsColour() =>
            Ctx(3, 3, new[] { Block(1, new Coord(1, 1)) }, new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Blue) });

        [Test]
        public void Run_Solvable_PassesThroughWithoutACrossCheck()
        {
            var crossCheck = new CountingStrategy();

            var result = new NextClearRunner(crossCheckFactory: () => crossCheck)
                .Run(StepAsideBeforeFirstClearBoard(), NextClear(), Canonical(), Exhaustive());

            Assert.AreEqual(SolveStatus.Solvable, result.NextClear.Status);
            Assert.AreEqual(UnsolvableCrossCheck.NotRun, result.CrossCheckOutcome);
            Assert.IsNull(result.CrossCheck);
            Assert.AreEqual(0, crossCheck.Searches);
        }

        [Test]
        public void Run_Indeterminate_PassesThroughWithoutACrossCheck()
        {
            var crossCheck = new CountingStrategy();

            var result = new NextClearRunner(crossCheckFactory: () => crossCheck)
                .Run(StepAsideBeforeFirstClearBoard(), NextClear(maxExplored: 1), Canonical(), Exhaustive());

            Assert.AreEqual(SolveStatus.Indeterminate, result.NextClear.Status);
            Assert.AreEqual(UnsolvableCrossCheck.NotRun, result.CrossCheckOutcome);
            Assert.AreEqual(0, crossCheck.Searches);
        }

        [Test]
        public void Run_UnsolvableAndAStarAgrees_IsConfirmed()
        {
            var result = new NextClearRunner().Run(NoGateForItsColour(), NextClear(), Canonical(), Exhaustive());

            Assert.AreEqual(SolveStatus.Unsolvable, result.NextClear.Status);
            Assert.AreEqual(UnsolvableCrossCheck.Confirmed, result.CrossCheckOutcome);
            Assert.AreEqual(LevelSolveVerdict.Unsolvable, result.CrossCheck.Verdict);
        }

        [Test]
        public void Run_AxisBlockBoxedInAboveACrossAxisGate_BothSearchesAgreeItIsUnsolvable()
        {
            // End to end for D39: nearest-next-clear proves it on clear
            // monotonicity, A* by exhaustion. If only one of them still allowed
            // the push across the axis they would disagree and this would throw;
            // if both did, the level would read as solvable.
            var ctx = AxisBlockBoxedAboveCrossAxisGateBoard();
            Assert.IsTrue(ctx.IsClearMonotone, "the proof needs a clear-monotone board");

            var result = new NextClearRunner().Run(ctx, NextClear(), Canonical(), Exhaustive());

            Assert.AreEqual(SolveStatus.Unsolvable, result.NextClear.Status);
            Assert.AreEqual(UnsolvableCrossCheck.Confirmed, result.CrossCheckOutcome);
            Assert.AreEqual(LevelSolveVerdict.Unsolvable, result.CrossCheck.Verdict);
        }

        [Test]
        public void Run_UnsolvableAndAStarRunsOutOfBudget_StandsButIsInconclusive()
        {
            var result = new NextClearRunner()
                .Run(NoGateForItsColour(), NextClear(), Canonical(maxExplored: 1), Exhaustive(maxExplored: 1));

            Assert.AreEqual(SolveStatus.Unsolvable, result.NextClear.Status);
            Assert.AreEqual(UnsolvableCrossCheck.Inconclusive, result.CrossCheckOutcome);
            Assert.AreEqual(LevelSolveVerdict.Indeterminate, result.CrossCheck.Verdict);
        }

        [Test]
        public void Run_CrossCheckStarting_IsAnnouncedOnlyWhenACrossCheckRuns()
        {
            var announcements = 0;
            var runner = new NextClearRunner();

            runner.Run(StepAsideBeforeFirstClearBoard(), NextClear(), Canonical(), Exhaustive(), () => announcements++);
            Assert.AreEqual(0, announcements, "solvable: no cross-check");

            runner.Run(NoGateForItsColour(), NextClear(), Canonical(), Exhaustive(), () => announcements++);
            Assert.AreEqual(1, announcements, "unsolvable: one cross-check");
        }

        [Test]
        public void Run_UnsolvableButAStarFindsASolution_ThrowsSolverDisagreement()
        {
            var runner = new NextClearRunner(nextClearFactory: () => new AlwaysUnsolvableStrategy());

            var exception = Assert.Throws<SolverDisagreementException>(
                () => runner.Run(StepAsideBeforeFirstClearBoard(), NextClear(), Canonical(), Exhaustive()));

            Assert.AreEqual(SolveStatus.Unsolvable, exception.NextClear.Status);
            Assert.AreEqual(LevelSolveVerdict.Solvable, exception.CrossCheck.Verdict);
        }
    }
}
