using System;
using System.Linq;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Fixture;
using static GateRush.Tests.SearchCorpus;
using static GateRush.Tests.Solve;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 05: three-valued status, shortest-solution guarantee against
    /// a hand-verified corpus, canonical/exhaustive agreement, the
    /// stratified/non-stratified equivalence that protects the memory
    /// optimisation, the collapsed/plain-identity equivalence that protects the
    /// symmetry optimisation (D35), budget behaviour, and reproducibility.
    /// </summary>
    public class BreadthFirstStrategyTests
    {
        // ----- Basic outcomes --------------------------------------------

        [Test]
        public void Search_TriviallySolvableBoard_ReturnsSolvableWithMinimalMoveCount()
        {
            var ctx = Ctx(5, 1, new[] { Block(1, new Coord(2, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var initial = BoardState.CreateInitial(ctx);

            var result = new BreadthFirstStrategy().Search(ctx, initial, Budget(MoveGenMode.Canonical));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(1, result.Solution.Count);
            Assert.IsTrue(Replay(ctx, initial, result.Solution).IsSolved(ctx));
        }

        [Test]
        public void Search_FullyPackedBoardWithPreAlignedBlocks_SolvesStartingFromAZeroDistanceMove()
        {
            var ctx = PackedFourColourBoard();
            var initial = BoardState.CreateInitial(ctx);

            var result = new BreadthFirstStrategy().Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            var first = result.Solution[0];
            Assert.AreEqual(
                initial.Origins[first.BlockIndex], first.TargetOrigin,
                "the opening move must be a zero-distance gate clear");
            Assert.IsTrue(Replay(ctx, initial, result.Solution).IsSolved(ctx));
        }

        [Test]
        public void Search_BlockWhoseColourHasNoGate_ReturnsUnsolvable()
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(1, 1)) }, new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Blue) });
            var initial = BoardState.CreateInitial(ctx);

            var result = new BreadthFirstStrategy().Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Unsolvable, result.Status);
            CollectionAssert.IsEmpty(result.Solution);
        }

        [Test]
        public void Search_OnlyExitPermanentlyObstructed_ReturnsUnsolvable()
        {
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }, axis: MovementAxis.VerticalOnly)
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });

            var result = new BreadthFirstStrategy().Search(
                ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Unsolvable, result.Status);
        }

        [Test]
        public void Search_LayeredBlockSecondColourHasNoGate_ReturnsUnsolvable()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });

            var result = new BreadthFirstStrategy().Search(
                ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Unsolvable, result.Status);
        }

        [Test]
        public void Search_InitialStateAlreadySolved_ReturnsSolvableWithEmptySolution()
        {
            var ctx = Ctx(3, 3, Array.Empty<BlockDefinition>());

            var result = new BreadthFirstStrategy().Search(
                ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Canonical));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            CollectionAssert.IsEmpty(result.Solution);
        }

        // ----- Found versus proven shortest ------------------------------

        [Test]
        public void Search_ExhaustiveSolution_IsProvenShortest()
        {
            var ctx = StepAsideBeforeFirstClearBoard();

            var result = new BreadthFirstStrategy().Search(ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(3, result.ProvenShortestLength);
            Assert.AreEqual(3, result.LengthLowerBound);
        }

        [Test]
        public void Search_CanonicalSolutionLongerThanTheHeuristicBound_IsNotProvenShortest()
        {
            var ctx = StepAsideBeforeFirstClearBoard();
            var initial = BoardState.CreateInitial(ctx);

            var result = new BreadthFirstStrategy().Search(ctx, initial, Budget(MoveGenMode.Canonical));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(3, result.Solution.Count);
            Assert.AreEqual(2, result.LengthLowerBound);
            Assert.IsNull(result.ProvenShortestLength);
            Assert.IsTrue(Replay(ctx, initial, result.Solution).IsSolved(ctx));
        }

        // ----- Cancellation ------------------------------------------------

        [Test]
        public void Search_CancelledToken_ThrowsOperationCanceled()
        {
            var ctx = StepAsideBeforeFirstClearBoard();
            using (var source = new System.Threading.CancellationTokenSource())
            {
                source.Cancel();

                Assert.Throws<OperationCanceledException>(() => new BreadthFirstStrategy().Search(
                    ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive).WithCancellation(source.Token)));
            }
        }

        // ----- Budget: Indeterminate is not Unsolvable -------------------

        [Test]
        public void Search_SolvableBoardWithADepthBudgetBelowTheOptimum_ReturnsIndeterminate()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 2, 1, BlockColor.Blue)
                });
            var initial = BoardState.CreateInitial(ctx);

            var result = new BreadthFirstStrategy().Search(
                ctx, initial, new SearchBudget(maxDepth: 1, maxExploredStates: 500_000, maxWallClockMs: 10_000, MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Indeterminate, result.Status);
            Assert.AreNotEqual(SolveStatus.Unsolvable, result.Status);
        }

        [Test]
        public void Search_SolvableBoardWithAnExploredStateBudgetTooSmall_ReturnsIndeterminate()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 2, 1, BlockColor.Blue)
                });

            var result = new BreadthFirstStrategy().Search(
                ctx, BoardState.CreateInitial(ctx),
                new SearchBudget(maxDepth: 64, maxExploredStates: 1, maxWallClockMs: 10_000, MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Indeterminate, result.Status);
        }

        [Test]
        public void Search_DepthBudgetExactlyEqualToTheOptimum_FindsTheSolution()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 2, 1, BlockColor.Blue)
                });
            var initial = BoardState.CreateInitial(ctx);

            var result = new BreadthFirstStrategy().Search(
                ctx, initial, new SearchBudget(maxDepth: 2, maxExploredStates: 500_000, maxWallClockMs: 10_000, MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(2, result.Solution.Count);
        }

        // ----- Shortest solution over the corpus ------------------------

        [Test]
        public void Search_ReturnsTheHandVerifiedOptimum_ForEveryCorpusBoard()
        {
            var strategy = new BreadthFirstStrategy();

            foreach (var (name, ctx, initial, optimum) in SolvableCorpus())
            {
                var result = strategy.Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

                Assert.AreEqual(SolveStatus.Solvable, result.Status, $"[{name}]");
                Assert.AreEqual(optimum, result.Solution.Count, $"[{name}] wrong solution length");
                Assert.IsTrue(Replay(ctx, initial, result.Solution).IsSolved(ctx), $"[{name}] solution does not solve");
            }
        }

        [Test]
        public void Search_TimeBonusOnABoard_ExploresAnIdenticalStateSpace()
        {
            // Time is outside the search space (D12): an M10 bonus is not a
            // BoardState field, so the two boards must explore the *same* states,
            // not merely reach the same answer. A weaker move-count check would
            // pass even if the bonus had leaked into BoardState.
            var plain = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 2, 1, BlockColor.Blue)
                });
            var withBonus = Ctx(
                5, 5,
                new[]
                {
                    Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }, timeBonusSeconds: 30)
                },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 2, 1, BlockColor.Blue)
                });
            var strategy = new BreadthFirstStrategy();

            var plainResult = strategy.Search(plain, BoardState.CreateInitial(plain), Budget(MoveGenMode.Exhaustive));
            var bonusResult = strategy.Search(withBonus, BoardState.CreateInitial(withBonus), Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, plainResult.Status);
            Assert.AreEqual(plainResult.Status, bonusResult.Status);
            Assert.AreEqual(plainResult.Solution.Count, bonusResult.Solution.Count);
            Assert.AreEqual(plainResult.ExploredStateCount, bonusResult.ExploredStateCount);
        }

        [Test]
        public void Search_EveryReturnedMoveReplaysThroughTheResolver_AndReachesSolved()
        {
            var strategy = new BreadthFirstStrategy();

            foreach (var (name, ctx, initial, _) in SolvableCorpus())
            {
                var result = strategy.Search(ctx, initial, Budget(MoveGenMode.Canonical));

                Assert.AreEqual(SolveStatus.Solvable, result.Status, $"[{name}]");
                var final = Replay(ctx, initial, result.Solution);
                Assert.IsTrue(final.IsSolved(ctx), $"[{name}] replayed solution left the board unsolved");
            }
        }

        // ----- Canonical vs exhaustive --------------------------------

        [Test]
        public void Search_CanonicalAndExhaustive_ReturnTheSameMoveCount_OverTheCorpus()
        {
            var strategy = new BreadthFirstStrategy();

            foreach (var (name, ctx, initial, _) in SolvableCorpus())
            {
                var canonical = strategy.Search(ctx, initial, Budget(MoveGenMode.Canonical));
                var exhaustive = strategy.Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

                Assert.AreEqual(SolveStatus.Solvable, canonical.Status, $"[{name}] canonical");
                Assert.AreEqual(SolveStatus.Solvable, exhaustive.Status, $"[{name}] exhaustive");
                Assert.AreEqual(
                    exhaustive.Solution.Count, canonical.Solution.Count,
                    $"[{name}] canonical pruning changed the optimum");
            }
        }

        // ----- Stratified vs non-stratified: the memory-optimisation guard ---

        [Test]
        public void Search_StratifiedAndNonStratified_ReturnIdenticalResults_OverTheWholeCorpus()
        {
            var stratified = new BreadthFirstStrategy(stratifyVisitedSet: true);
            var plain = new BreadthFirstStrategy(stratifyVisitedSet: false);

            foreach (var (name, ctx, initial) in WholeCorpus())
            {
                foreach (var mode in new[] { MoveGenMode.Canonical, MoveGenMode.Exhaustive })
                {
                    var a = stratified.Search(ctx, initial, Budget(mode));
                    var b = plain.Search(ctx, initial, Budget(mode));

                    Assert.AreEqual(b.Status, a.Status, $"[{name}/{mode}] status");
                    CollectionAssert.AreEqual(b.Solution, a.Solution, $"[{name}/{mode}] solution");
                    Assert.AreEqual(b.ExploredStateCount, a.ExploredStateCount, $"[{name}/{mode}] explored count");
                    Assert.AreEqual(b.PeakFrontierSize, a.PeakFrontierSize, $"[{name}/{mode}] frontier peak");
                }
            }
        }

        // ----- Symmetry collapse: the state-identity guard (D35) ------------

        [Test]
        public void Search_SymmetryCollapsedAndPlainIdentity_ReturnTheSameVerdictAndOptimum_OverTheWholeCorpus()
        {
            // The collapse changes what counts as "the same state", so this is
            // the test that has to hold: treating interchangeable blocks as one
            // may not change a verdict or lengthen an optimum on any board,
            // symmetric or not. Solutions are compared by length, not element for
            // element — collapsing prunes one of two equivalent branches, so the
            // search may legitimately return the other shortest path.
            var strategy = new BreadthFirstStrategy();

            foreach (var (name, ctx, collapsed) in WholeCorpus())
            {
                var plain = BoardState.CreateInitial(ctx, BlockSymmetry.None);

                foreach (var mode in new[] { MoveGenMode.Canonical, MoveGenMode.Exhaustive })
                {
                    var expected = strategy.Search(ctx, plain, Budget(mode));
                    var actual = strategy.Search(ctx, collapsed, Budget(mode));

                    Assert.AreEqual(expected.Status, actual.Status, $"[{name}/{mode}] status");
                    Assert.AreEqual(
                        expected.Solution.Count, actual.Solution.Count,
                        $"[{name}/{mode}] the symmetry collapse changed the optimum");
                }
            }
        }

        [Test]
        public void Search_InterchangeableBlocks_ExploresFewerStatesThanPlainIdentity()
        {
            // Four identical blocks on an open grid: 4! labellings of every
            // configuration, all but one of them pure duplication. The same
            // search over the same board, differing only in whether that
            // duplication is recognised.
            var ctx = InterchangeableBlocksBoard();
            var strategy = new BreadthFirstStrategy();

            var collapsed = strategy.Search(ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive));
            var plain = strategy.Search(
                ctx, BoardState.CreateInitial(ctx, BlockSymmetry.None), Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, collapsed.Status);
            Assert.AreEqual(plain.Solution.Count, collapsed.Solution.Count);
            Assert.Less(
                collapsed.ExploredStateCount, plain.ExploredStateCount,
                "the symmetry collapse did not shrink the explored state space");
            Assert.Less(
                collapsed.PeakRetainedStateCount, plain.PeakRetainedStateCount,
                "the symmetry collapse did not shrink the visited set");
        }

        [Test]
        public void Search_Stratified_RetainsFewerStatesThanNonStratified_OnAMultiStratumBoard()
        {
            // Seven blocks packed into an eight-wide corridor: each clear is its
            // own stratum, and every stratum drains before the next fills, so the
            // stratified run releases each stratum's visited set while the plain
            // run keeps all seven. If the flag were ignored the two counts would
            // match and this fails.
            var ctx = Ctx(
                8, 1,
                Enumerable.Range(0, 7).Select(x => Block(x + 1, new Coord(x, 0))).ToArray(),
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var initial = BoardState.CreateInitial(ctx);

            var stratified = new BreadthFirstStrategy(stratifyVisitedSet: true)
                .Search(ctx, initial, Budget(MoveGenMode.Exhaustive));
            var plain = new BreadthFirstStrategy(stratifyVisitedSet: false)
                .Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, stratified.Status);
            Assert.AreEqual(7, stratified.Solution.Count);
            Assert.AreEqual(7, plain.Solution.Count);
            Assert.Less(
                stratified.PeakRetainedStateCount, plain.PeakRetainedStateCount,
                "stratification did not shrink the retained visited set");
        }

        // ----- Reproducibility --------------------------------------

        [Test]
        public void Search_IsReproducible_AcrossRuns()
        {
            var ctx = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var initial = BoardState.CreateInitial(ctx);
            var strategy = new BreadthFirstStrategy();

            var first = strategy.Search(ctx, initial, Budget(MoveGenMode.Exhaustive));
            var second = strategy.Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

            CollectionAssert.AreEqual(first.Solution, second.Solution);
        }

        // ----- Argument and budget validation -----------------------

        [Test]
        public void Search_NullArguments_Throw()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) });
            var initial = BoardState.CreateInitial(ctx);
            var strategy = new BreadthFirstStrategy();
            var budget = Budget(MoveGenMode.Canonical);

            Assert.Throws<ArgumentNullException>(() => strategy.Search(null, initial, budget));
            Assert.Throws<ArgumentNullException>(() => strategy.Search(ctx, null, budget));
            Assert.Throws<ArgumentNullException>(() => strategy.Search(ctx, initial, null));
        }

        [TestCase(0, 1, 1)]
        [TestCase(1, 0, 1)]
        [TestCase(1, 1, 0)]
        [TestCase(-3, 1, 1)]
        public void SearchBudget_RejectsNonPositiveLimits(int maxDepth, int maxExplored, long maxMs)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SearchBudget(maxDepth, maxExplored, maxMs, MoveGenMode.Canonical));
        }
    }
}
