using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Fixture;
using static GateRush.Tests.Solve;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 05: three-valued status, shortest-solution guarantee against
    /// a hand-verified corpus, canonical/exhaustive agreement, the
    /// stratified/non-stratified equivalence that protects the memory
    /// optimisation, budget behaviour, and reproducibility.
    /// </summary>
    public class BreadthFirstStrategyTests
    {
        // ----- Corpus -------------------------------------------------------

        /// <summary>
        /// Boards with a hand-verified shortest solution length. Each is chosen so
        /// canonical pruning does not lengthen the optimum, so both modes agree.
        /// </summary>
        private static IEnumerable<(string name, LevelContext ctx, BoardState initial, int optimum)> SolvableCorpus()
        {
            var slide = Ctx(5, 1, new[] { Block(1, new Coord(2, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            yield return ("lone block slides to its gate", slide, BoardState.CreateInitial(slide), 1);

            var twoInLine = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            yield return ("far block waits for the near one", twoInLine, BoardState.CreateInitial(twoInLine), 2);

            var threeInLine = Ctx(
                7, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0)), Block(3, new Coord(2, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            yield return ("three in a row, forced order", threeInLine, BoardState.CreateInitial(threeInLine), 3);

            var fourInLine = Ctx(
                8, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0)),
                    Block(3, new Coord(2, 0)), Block(4, new Coord(3, 0))
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            yield return ("four in a row, forced order", fourInLine, BoardState.CreateInitial(fourInLine), 4);

            var layered = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 2, 1, BlockColor.Blue)
                });
            yield return ("layered block, one gate per colour", layered, BoardState.CreateInitial(layered), 2);

            var packed = PackedFourColourBoard();
            yield return ("fully packed, every block pre-aligned", packed, BoardState.CreateInitial(packed), 4);
        }

        private static LevelContext PackedFourColourBoard()
        {
            return Ctx(
                2, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }),
                    Block(3, new Coord(0, 1), colors: new[] { BlockColor.Green }),
                    Block(4, new Coord(1, 1), colors: new[] { BlockColor.Yellow })
                },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 1, 1, BlockColor.Blue),
                    Gate(3, BoardEdge.Top, 0, 1, BlockColor.Green),
                    Gate(4, BoardEdge.Top, 1, 1, BlockColor.Yellow)
                });
        }

        private static IEnumerable<(string name, LevelContext ctx, BoardState initial)> UnsolvableCorpus()
        {
            var noGate = Ctx(3, 3, new[] { Block(1, new Coord(1, 1)) }, new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Blue) });
            yield return ("block colour has no matching gate", noGate, BoardState.CreateInitial(noGate));

            var obstructed = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }, axis: MovementAxis.VerticalOnly)
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            yield return ("only exit parked shut by an immovable block", obstructed, BoardState.CreateInitial(obstructed));

            var deadLayer = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            yield return ("layered block's second colour has no gate", deadLayer, BoardState.CreateInitial(deadLayer));
        }

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

            var boards = SolvableCorpus()
                .Select(b => (b.name, b.ctx, b.initial))
                .Concat(UnsolvableCorpus());

            foreach (var (name, ctx, initial) in boards)
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
