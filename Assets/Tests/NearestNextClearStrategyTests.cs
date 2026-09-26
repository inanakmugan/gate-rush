using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Fixture;
using static GateRush.Tests.SearchCorpus;
using static GateRush.Tests.Solve;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="NearestNextClearStrategy"/>: its verdicts on the
    /// hand-verified corpus, that it reports Unsolvable only on clear-monotone
    /// levels, its budget behaviour and determinism, and — against ground-truth
    /// A\* over seeded random boards covering every runtime mechanic — that its
    /// verdict matches exactly on clear-monotone boards and is never Unsolvable
    /// on the rest, and that on a clear-monotone board every clear it commits to
    /// leaves a solvable board solvable.
    /// </summary>
    public class NearestNextClearStrategyTests
    {
        /// <summary>Each mechanic must appear on at least one compared board in this many.</summary>
        private const int CoverageDivisor = 15;

        /// <summary>The looser floor for non-monotone boards, which need a rarer two-key mixed-effect lock.</summary>
        private const int NonMonotoneCoverageDivisor = 60;

        /// <summary>At least one confirmed-solvable commit point per this many boards drawn.</summary>
        private const int CommitCoverageDivisor = 5;

        /// <summary>Generous enough that no test board's search is ever cut short by it.</summary>
        private static SearchBudget Generous() => Budget(MoveGenMode.Exhaustive, maxDepth: 200, maxExplored: 200_000, maxMs: 60_000);

        /// <summary>
        /// Ground truth: exhaustive A\*, whose optimum and unsolvability proofs are
        /// covered by its own suite. Boards it cannot settle within this budget are
        /// skipped, never counted.
        /// </summary>
        private static SearchBudget GroundTruth() => Budget(MoveGenMode.Exhaustive, maxDepth: 200, maxExplored: 10_000, maxMs: 60_000);

        private static SolveResult Search(LevelContext ctx, SearchBudget budget = null) =>
            new NearestNextClearStrategy().Search(ctx, BoardState.CreateInitial(ctx), budget ?? Generous());

        // ----- Corpus ------------------------------------------------------

        [Test]
        public void Search_EverySolvableCorpusBoard_FindsAReplayableSolutionNoShorterThanTheOptimum()
        {
            foreach (var (name, ctx, initial, optimum) in SolvableCorpus())
            {
                var result = new NearestNextClearStrategy().Search(ctx, initial, Generous());

                Assert.AreEqual(SolveStatus.Solvable, result.Status, name);
                Assert.IsTrue(Replay(ctx, initial, result.Solution).IsSolved(ctx), name);
                Assert.GreaterOrEqual(result.Solution.Count, optimum, name);
                Assert.LessOrEqual(result.LengthLowerBound, optimum, name);
            }
        }

        [Test]
        public void Search_EveryUnsolvableCorpusBoard_IsUnsolvable()
        {
            foreach (var (name, ctx, initial) in UnsolvableCorpus())
            {
                var result = new NearestNextClearStrategy().Search(ctx, initial, Generous());

                Assert.AreEqual(SolveStatus.Unsolvable, result.Status, name);
                Assert.AreEqual(0, result.LengthLowerBound, name);
            }
        }

        [Test]
        public void Search_SolutionLongerThanTheBound_IsSolvableButNotProvenShortest()
        {
            var result = Search(StepAsideBeforeFirstClearBoard());

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(3, result.Solution.Count);
            Assert.AreEqual(2, result.LengthLowerBound);
            Assert.IsNull(result.ProvenShortestLength);
        }

        [Test]
        public void Search_InitialStateAlreadySolved_ReturnsAnEmptyProvenSolution()
        {
            var result = Search(Ctx(2, 2));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            CollectionAssert.IsEmpty(result.Solution);
            Assert.AreEqual(0, result.ProvenShortestLength);
        }

        // ----- Unsolvable only where a dead end proves it -------------------

        [Test]
        public void Search_DeadEndOnALevelWithAGenerator_IsIndeterminateNotUnsolvable()
        {
            // No red gate exists, so no clear is ever reachable — but a level
            // with a generator is not clear-monotone, and a dead end on it proves
            // nothing.
            var ctx = Ctx(
                3, 3,
                new[] { Block(1, new Coord(1, 1)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) },
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned()) });
            Assert.IsFalse(ctx.IsClearMonotone);

            var result = Search(ctx);

            Assert.AreEqual(SolveStatus.Indeterminate, result.Status);
        }

        [Test]
        public void Search_MixedEffectLockTrap_IsIndeterminateNeverUnsolvable()
        {
            // The nearest clear wastes the ClearOuterColor key and strands the
            // lock's owner; the level is solvable (A* finds 3 moves). Because a
            // mixed-effect lock makes the level non-monotone, the dead end the
            // commitment leads to must not be reported as a proof.
            var ctx = WastedClearKeyTrapBoard();

            var result = Search(ctx);

            Assert.AreEqual(SolveStatus.Indeterminate, result.Status);
        }

        // ----- Budget --------------------------------------------------------

        [Test]
        public void Search_ExploredStateBudgetTooSmall_IsIndeterminate()
        {
            var result = Search(StepAsideBeforeFirstClearBoard(), Budget(MoveGenMode.Exhaustive, maxExplored: 1));

            Assert.AreEqual(SolveStatus.Indeterminate, result.Status);
            Assert.AreEqual(2, result.LengthLowerBound);
        }

        [Test]
        public void Search_DepthBudgetBelowTheSolutionLength_IsIndeterminate()
        {
            var result = Search(StepAsideBeforeFirstClearBoard(), Budget(MoveGenMode.Exhaustive, maxDepth: 2));

            Assert.AreEqual(SolveStatus.Indeterminate, result.Status);
        }

        [Test]
        public void Search_DepthBudgetExactlyTheSolutionLength_Solves()
        {
            var result = Search(StepAsideBeforeFirstClearBoard(), Budget(MoveGenMode.Exhaustive, maxDepth: 3));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
        }

        [Test]
        public void Search_CancelledToken_ThrowsOperationCanceled()
        {
            using (var source = new System.Threading.CancellationTokenSource())
            {
                source.Cancel();

                Assert.Throws<OperationCanceledException>(
                    () => Search(StepAsideBeforeFirstClearBoard(), Generous().WithCancellation(source.Token)));
            }
        }

        [Test]
        public void Search_CanonicalBudget_Throws()
        {
            Assert.Throws<ArgumentException>(() => Search(StepAsideBeforeFirstClearBoard(), Budget(MoveGenMode.Canonical)));
        }

        // ----- Determinism ---------------------------------------------------

        [Test]
        public void Search_StepAsideBoard_ReturnsThisExactSequence()
        {
            var result = Search(StepAsideBeforeFirstClearBoard());

            CollectionAssert.AreEqual(
                new[]
                {
                    new Move(1, new Coord(1, 1)),
                    new Move(0, new Coord(2, 0)),
                    new Move(1, new Coord(1, 1))
                },
                result.Solution);
        }

        // ----- Against ground truth over random boards -----------------------

        [TestCase(3, 300, 20260925)]
        [TestCase(4, 100, 20260926)]
        public void Search_VerdictAgreesWithAStar_OverSeededRandomBoards(int maxSide, int boardCount, int seed)
        {
            var rng = new Random(seed);
            var coverage = new Dictionary<string, int>();
            void Count(string key) => coverage[key] = coverage.TryGetValue(key, out var n) ? n + 1 : 1;

            for (var b = 0; b < boardCount; b++)
            {
                var ctx = RandomBoards.Next(rng, maxSide);
                var initial = BoardState.CreateInitial(ctx);
                var truth = new AStarStrategy().Search(ctx, initial, GroundTruth());
                if (truth.Status == SolveStatus.Indeterminate)
                {
                    continue;
                }

                var result = new NearestNextClearStrategy().Search(ctx, initial, Generous());
                var label = $"board {b} (seed {seed}, side {maxSide})";

                if (result.Status == SolveStatus.Solvable)
                {
                    Assert.IsTrue(Replay(ctx, initial, result.Solution).IsSolved(ctx), label);
                    Assert.AreEqual(SolveStatus.Solvable, truth.Status, $"{label}: solved a board A* proved unsolvable");
                    Assert.GreaterOrEqual(result.Solution.Count, truth.Solution.Count, $"{label}: shorter than A*'s optimum");
                    Assert.LessOrEqual(result.LengthLowerBound, truth.Solution.Count, $"{label}: bound above the optimum");
                }

                // On a non-monotone level a commitment can dead-end on a solvable
                // board, so only Unsolvable is ruled out there; everywhere else the
                // verdicts must match exactly.
                if (!ctx.IsClearMonotone)
                {
                    Assert.AreNotEqual(SolveStatus.Unsolvable, result.Status, $"{label}: not clear-monotone");
                    if (truth.Status == SolveStatus.Unsolvable)
                    {
                        Assert.AreNotEqual(SolveStatus.Solvable, result.Status, $"{label}: A* proved it unsolvable");
                    }

                    Count("nonMonotone");
                    continue;
                }

                if (truth.Status == SolveStatus.Solvable)
                {
                    Assert.AreEqual(SolveStatus.Solvable, result.Status, $"{label}: A* solved it");
                    Count("solvable");
                }
                else
                {
                    Assert.AreEqual(SolveStatus.Unsolvable, result.Status, $"{label}: A* proved it unsolvable");
                    Count("unsolvable");
                }

                CountMechanics(ctx, Count);
            }

            var minimum = boardCount / CoverageDivisor;
            foreach (var key in new[] { "solvable", "unsolvable", "lock", "shutter", "frozen", "layered" })
            {
                Assert.GreaterOrEqual(coverage.TryGetValue(key, out var n) ? n : 0, minimum, $"boards compared with: {key}");
            }

            // Mixed-effect locks need a two-key lock whose keys differ, so they
            // are rarer in the corpus; they get their own, lower floor.
            var nonMonotoneMinimum = boardCount / NonMonotoneCoverageDivisor;
            Assert.GreaterOrEqual(
                coverage.TryGetValue("nonMonotone", out var nonMonotone) ? nonMonotone : 0, nonMonotoneMinimum,
                "boards compared that are not clear-monotone");
        }

        [TestCase(3, 300, 20260927)]
        [TestCase(4, 60, 20260928)]
        public void Search_EveryClearItCommitsTo_LeavesASolvableBoardSolvable_OverSeededRandomBoards(
            int maxSide, int boardCount, int seed)
        {
            var rng = new Random(seed);
            var resolver = new MoveResolver();
            var checkedCommits = 0;

            for (var b = 0; b < boardCount; b++)
            {
                var ctx = RandomBoards.Next(rng, maxSide);
                var initial = BoardState.CreateInitial(ctx);
                if (!ctx.IsClearMonotone || new AStarStrategy().Search(ctx, initial, GroundTruth()).Status != SolveStatus.Solvable)
                {
                    continue;
                }

                var result = new NearestNextClearStrategy().Search(ctx, initial, Generous());
                var state = initial;
                for (var i = 0; i < result.Solution.Count; i++)
                {
                    Assert.IsTrue(resolver.TryApplyMove(ctx, state, result.Solution[i], out var next, out _));
                    if (next.TotalClearCount > state.TotalClearCount && !next.IsSolved(ctx))
                    {
                        var truth = new AStarStrategy().Search(ctx, next, GroundTruth());
                        Assert.AreNotEqual(
                            SolveStatus.Unsolvable, truth.Status,
                            $"board {b} (seed {seed}, side {maxSide}): the clear at step {i + 1} left a solvable board unsolvable");
                        checkedCommits += truth.Status == SolveStatus.Solvable ? 1 : 0;
                    }

                    state = next;
                }
            }

            Assert.GreaterOrEqual(checkedCommits, boardCount / CommitCoverageDivisor, "commit points confirmed solvable");
        }

        /// <summary>Counts which mechanics a board exercises, for the coverage floor.</summary>
        private static void CountMechanics(LevelContext ctx, Action<string> count)
        {
            bool hasLock = false, hasFrozen = false, hasLayered = false;
            foreach (var block in ctx.Blocks)
            {
                hasLock |= block.LockId.HasValue;
                hasFrozen |= block.UnfreezeAtClearCount.HasValue;
                hasLayered |= block.ColorStack.Count > 1;
            }

            if (hasLock)
            {
                count("lock");
            }

            if (hasFrozen)
            {
                count("frozen");
            }

            if (hasLayered)
            {
                count("layered");
            }

            if (ctx.Shutters.Count > 0)
            {
                count("shutter");
            }
        }
    }
}
