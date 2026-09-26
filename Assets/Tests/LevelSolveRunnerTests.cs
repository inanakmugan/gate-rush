using System.Collections.Generic;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Fixture;
using static GateRush.Tests.SearchCorpus;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="LevelSolveRunner"/>'s two-stage policy (D5): canonical
    /// first, exhaustive only if canonical did not solve, and the verdict comes
    /// from the exhaustive stage — a canonical <see cref="SolveStatus.Unsolvable"/>
    /// never concludes anything.
    /// </summary>
    public class LevelSolveRunnerTests
    {
        /// <summary>Records the mode of every search the runner asks for; delegates to a fresh BFS each time.</summary>
        private sealed class RecordingStrategy : ISearchStrategy
        {
            public readonly List<MoveGenMode> Searches = new List<MoveGenMode>();

            public SolveResult Search(LevelContext ctx, BoardState initial, SearchBudget budget)
            {
                Searches.Add(budget.Mode);
                return new BreadthFirstStrategy().Search(ctx, initial, budget);
            }
        }

        private static SearchBudget Canonical(int maxExplored = 200_000) =>
            new SearchBudget(64, maxExplored, 10_000, MoveGenMode.Canonical);

        private static SearchBudget Exhaustive(int maxExplored = 500_000) =>
            new SearchBudget(64, maxExplored, 10_000, MoveGenMode.Exhaustive);

        /// <summary>
        /// A 2x1 gate with two 1x1 blocks flush against it: solvable in two
        /// zero-distance clears. Needs more than one expansion, so a
        /// one-expansion budget gives up on it.
        /// </summary>
        private static LevelContext TwoZeroDistanceClears()
        {
            var blocks = new[]
            {
                Block(1, new Coord(0, 0)),
                Block(2, new Coord(1, 0)),
            };
            var gates = new[] { Gate(1, BoardEdge.Bottom, 0, 2, BlockColor.Red) };
            return Ctx(2, 1, blocks: blocks, gates: gates);
        }

        /// <summary>One block flush against its gate: solved by a single zero-distance clear.</summary>
        private static LevelContext OneZeroDistanceClear()
        {
            var blocks = new[] { Block(1, new Coord(1, 0)) };
            var gates = new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) };
            return Ctx(3, 3, blocks: blocks, gates: gates);
        }

        /// <summary>A single block boxed into a 1x1 grid with no gate: no move exists.</summary>
        private static LevelContext Unsolvable()
        {
            return Ctx(1, 1, blocks: new[] { Block(1, new Coord(0, 0)) });
        }

        [Test]
        public void Run_CanonicalSolves_ExhaustiveIsNotRun()
        {
            var spy = new RecordingStrategy();

            var result = new LevelSolveRunner(() => spy).Run(OneZeroDistanceClear(), Canonical(), Exhaustive());

            Assert.AreEqual(LevelSolveVerdict.Solvable, result.Verdict);
            Assert.AreEqual(MoveGenMode.Canonical, result.SolvedBy);
            CollectionAssert.AreEqual(new[] { MoveGenMode.Canonical }, spy.Searches);
            Assert.IsNull(result.Exhaustive);
        }

        [Test]
        public void Run_CanonicalDoesNotSolve_RetriedExhaustivelyAndSolved()
        {
            var spy = new RecordingStrategy();
            var stages = new List<MoveGenMode>();

            // Canonical budget too small to finish; exhaustive budget generous.
            var result = new LevelSolveRunner(() => spy)
                .Run(TwoZeroDistanceClears(), Canonical(maxExplored: 1), Exhaustive(), stages.Add);

            Assert.AreEqual(LevelSolveVerdict.Solvable, result.Verdict);
            Assert.AreEqual(MoveGenMode.Exhaustive, result.SolvedBy);
            CollectionAssert.AreEqual(new[] { MoveGenMode.Canonical, MoveGenMode.Exhaustive }, spy.Searches);
            CollectionAssert.AreEqual(new[] { MoveGenMode.Canonical, MoveGenMode.Exhaustive }, stages);
            Assert.AreEqual(2, result.Solution.Count);
        }

        [Test]
        public void Run_CanonicalSolves_TheExhaustiveStageIsNeverAnnounced()
        {
            var stages = new List<MoveGenMode>();

            new LevelSolveRunner().Run(OneZeroDistanceClear(), Canonical(), Exhaustive(), stages.Add);

            CollectionAssert.AreEqual(new[] { MoveGenMode.Canonical }, stages);
        }

        [Test]
        public void Run_NeitherStageSolvesWithinBudget_IsIndeterminateNotUnsolvable()
        {
            var result = new LevelSolveRunner()
                .Run(TwoZeroDistanceClears(), Canonical(maxExplored: 1), Exhaustive(maxExplored: 1));

            Assert.AreEqual(LevelSolveVerdict.Indeterminate, result.Verdict);
        }

        [Test]
        public void Run_GenuinelyUnsolvable_IsUnsolvableFromTheExhaustiveStage()
        {
            var spy = new RecordingStrategy();

            var result = new LevelSolveRunner(() => spy).Run(Unsolvable(), Canonical(), Exhaustive());

            Assert.AreEqual(LevelSolveVerdict.Unsolvable, result.Verdict);
            CollectionAssert.AreEqual(new[] { MoveGenMode.Canonical, MoveGenMode.Exhaustive }, spy.Searches);
        }

        // ----- Existence versus quality --------------------------------------

        [Test]
        public void Run_CanonicalSolutionLongerThanTheBound_IsSolvableButNotProvenShortest()
        {
            var result = new LevelSolveRunner().Run(StepAsideBeforeFirstClearBoard(), Canonical(), Exhaustive());

            Assert.AreEqual(LevelSolveVerdict.Solvable, result.Verdict);
            Assert.AreEqual(MoveGenMode.Canonical, result.SolvedBy);
            Assert.AreEqual(3, result.Solution.Count);
            Assert.AreEqual(2, result.LengthLowerBound);
            Assert.IsNull(result.ProvenShortestLength);
        }

        [Test]
        public void Run_CanonicalSolutionMeetingTheBound_IsProvenShortest()
        {
            var result = new LevelSolveRunner().Run(OneZeroDistanceClear(), Canonical(), Exhaustive());

            Assert.AreEqual(MoveGenMode.Canonical, result.SolvedBy);
            Assert.AreEqual(1, result.ProvenShortestLength);
        }

        [Test]
        public void Run_ExhaustiveStageSolves_IsProvenShortest()
        {
            var result = new LevelSolveRunner()
                .Run(TwoZeroDistanceClears(), Canonical(maxExplored: 1), Exhaustive());

            Assert.AreEqual(MoveGenMode.Exhaustive, result.SolvedBy);
            Assert.AreEqual(2, result.ProvenShortestLength);
            Assert.AreEqual(2, result.LengthLowerBound);
        }

        [Test]
        public void Run_Indeterminate_CarriesALowerBoundButNoProvenLength()
        {
            var result = new LevelSolveRunner()
                .Run(TwoZeroDistanceClears(), Canonical(maxExplored: 1), Exhaustive(maxExplored: 1));

            Assert.AreEqual(LevelSolveVerdict.Indeterminate, result.Verdict);
            Assert.AreEqual(2, result.LengthLowerBound);
            Assert.IsNull(result.ProvenShortestLength);
        }

        [Test]
        public void Run_Unsolvable_HasNoLowerBoundAndNoProvenLength()
        {
            var result = new LevelSolveRunner().Run(Unsolvable(), Canonical(), Exhaustive());

            Assert.AreEqual(LevelSolveVerdict.Unsolvable, result.Verdict);
            Assert.AreEqual(0, result.LengthLowerBound);
            Assert.IsNull(result.ProvenShortestLength);
        }

        // ----- The combination the Level Editor ships -----------------------

        [Test]
        public void Run_DrivenByAStar_ReachesTheSameVerdictAndOptimumAsBreadthFirst()
        {
            // The editor's Validate button supplies AStarStrategy rather than
            // taking this runner's breadth-first default, so the pairing itself
            // needs coverage — every other case here exercises the default or a
            // spy. D3's own tests already pin that the two strategies agree on
            // the optimum; this pins that the two-stage policy around them does
            // too.
            foreach (var ctx in new[] { OneZeroDistanceClear(), TwoZeroDistanceClears(), Unsolvable() })
            {
                var expected = new LevelSolveRunner().Run(ctx, Canonical(), Exhaustive());
                var actual = new LevelSolveRunner(() => new AStarStrategy()).Run(ctx, Canonical(), Exhaustive());

                Assert.AreEqual(expected.Verdict, actual.Verdict);
                Assert.AreEqual(expected.SolvedBy, actual.SolvedBy);
                Assert.AreEqual(expected.Solution.Count, actual.Solution.Count);
            }
        }
    }
}
