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
    /// Covers phase 1.11: A\* behaves like breadth-first search on every outcome
    /// (three-valued status, budgets, shortest solutions, reproducibility), returns
    /// the same move count as <see cref="BreadthFirstStrategy"/> over the whole
    /// corpus, and expands fewer states doing it. The heuristic's admissibility
    /// and consistency are checked directly as well, not only through the
    /// answers they produce.
    /// </summary>
    public class AStarStrategyTests
    {
        // ----- Basic outcomes --------------------------------------------

        [Test]
        public void Search_TriviallySolvableBoard_ReturnsSolvableWithMinimalMoveCount()
        {
            var ctx = Ctx(5, 1, new[] { Block(1, new Coord(2, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var initial = BoardState.CreateInitial(ctx);

            var result = new AStarStrategy().Search(ctx, initial, Budget(MoveGenMode.Canonical));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(1, result.Solution.Count);
            Assert.IsTrue(Replay(ctx, initial, result.Solution).IsSolved(ctx));
        }

        [Test]
        public void Search_FullyPackedBoardWithPreAlignedBlocks_SolvesStartingFromAZeroDistanceMove()
        {
            var ctx = PackedFourColourBoard();
            var initial = BoardState.CreateInitial(ctx);

            var result = new AStarStrategy().Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            var first = result.Solution[0];
            Assert.AreEqual(
                initial.Origins[first.BlockIndex], first.TargetOrigin,
                "the opening move must be a zero-distance gate clear");
            Assert.IsTrue(Replay(ctx, initial, result.Solution).IsSolved(ctx));
        }

        [TestCase(MoveGenMode.Canonical)]
        [TestCase(MoveGenMode.Exhaustive)]
        public void Search_AxisBlockFlushAboveACrossAxisGate_SlidesAwayAndArrivesBack(MoveGenMode mode)
        {
            var ctx = AxisBlockReturnsToCrossAxisGateBoard();
            var initial = BoardState.CreateInitial(ctx);

            var result = new AStarStrategy().Search(ctx, initial, Budget(mode));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(2, result.Solution.Count);
            Assert.AreNotEqual(initial.Origins[0], result.Solution[0].TargetOrigin, "the first move must slide away");
            Assert.AreEqual(initial.Origins[0], result.Solution[1].TargetOrigin, "the second must arrive back at the gate");
            Assert.IsTrue(Replay(ctx, initial, result.Solution).IsSolved(ctx));
        }

        [Test]
        public void Search_AxisBlockBoxedInAboveACrossAxisGate_ReturnsUnsolvable()
        {
            var ctx = AxisBlockBoxedAboveCrossAxisGateBoard();

            var result = new AStarStrategy().Search(ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Unsolvable, result.Status);
        }

        [Test]
        public void Search_GeneratedMoveTheResolverRejects_ThrowsNamingTheMove()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });

            var error = Assert.Throws<InvalidOperationException>(
                () => new AStarStrategy(() => new IllegalMoveGenerator())
                    .Search(ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive)));

            StringAssert.Contains("block 0", error.Message);
            StringAssert.Contains(IllegalMoveGenerator.Target.ToString(), error.Message);
        }

        [Test]
        public void Search_BlockWhoseColourHasNoGate_ReturnsUnsolvable()
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(1, 1)) }, new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Blue) });
            var initial = BoardState.CreateInitial(ctx);

            var result = new AStarStrategy().Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

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

            var result = new AStarStrategy().Search(
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

            var result = new AStarStrategy().Search(
                ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Unsolvable, result.Status);
        }

        [Test]
        public void Search_InitialStateAlreadySolved_ReturnsSolvableWithEmptySolution()
        {
            var ctx = Ctx(3, 3, Array.Empty<BlockDefinition>());

            var result = new AStarStrategy().Search(
                ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Canonical));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            CollectionAssert.IsEmpty(result.Solution);
        }

        // ----- Found versus proven shortest ------------------------------

        [Test]
        public void Search_ExhaustiveSolution_IsProvenShortest()
        {
            var ctx = StepAsideBeforeFirstClearBoard();

            var result = new AStarStrategy().Search(ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(3, result.ProvenShortestLength);
            Assert.AreEqual(3, result.LengthLowerBound);
        }

        [Test]
        public void Search_CanonicalSolutionLongerThanTheHeuristicBound_IsNotProvenShortest()
        {
            var ctx = StepAsideBeforeFirstClearBoard();
            var initial = BoardState.CreateInitial(ctx);

            var result = new AStarStrategy().Search(ctx, initial, Budget(MoveGenMode.Canonical));

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

                Assert.Throws<OperationCanceledException>(() => new AStarStrategy().Search(
                    ctx, BoardState.CreateInitial(ctx), Budget(MoveGenMode.Exhaustive).WithCancellation(source.Token)));
            }
        }

        // ----- Budget: Indeterminate is not Unsolvable -------------------

        [Test]
        public void Search_SolvableBoardWithADepthBudgetBelowTheOptimum_ReturnsIndeterminate()
        {
            var ctx = LayeredTwoGateBoard();

            var result = new AStarStrategy().Search(
                ctx, BoardState.CreateInitial(ctx),
                new SearchBudget(maxDepth: 1, maxExploredStates: 500_000, maxWallClockMs: 10_000, MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Indeterminate, result.Status);
        }

        [Test]
        public void Search_SolvableBoardWithAnExploredStateBudgetTooSmall_ReturnsIndeterminate()
        {
            var ctx = LayeredTwoGateBoard();

            var result = new AStarStrategy().Search(
                ctx, BoardState.CreateInitial(ctx),
                new SearchBudget(maxDepth: 64, maxExploredStates: 1, maxWallClockMs: 10_000, MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Indeterminate, result.Status);
        }

        [Test]
        public void Search_DepthBudgetExactlyEqualToTheOptimum_FindsTheSolution()
        {
            var ctx = LayeredTwoGateBoard();

            var result = new AStarStrategy().Search(
                ctx, BoardState.CreateInitial(ctx),
                new SearchBudget(maxDepth: 2, maxExploredStates: 500_000, maxWallClockMs: 10_000, MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(2, result.Solution.Count);
        }

        // ----- Shortest solution over the corpus ------------------------

        [Test]
        public void Search_ReturnsTheHandVerifiedOptimum_ForEveryCorpusBoard()
        {
            var strategy = new AStarStrategy();

            foreach (var (name, ctx, initial, optimum) in SolvableCorpus())
            {
                foreach (var mode in new[] { MoveGenMode.Canonical, MoveGenMode.Exhaustive })
                {
                    var result = strategy.Search(ctx, initial, Budget(mode));

                    Assert.AreEqual(SolveStatus.Solvable, result.Status, $"[{name}/{mode}]");
                    Assert.AreEqual(optimum, result.Solution.Count, $"[{name}/{mode}] wrong solution length");
                    Assert.IsTrue(
                        Replay(ctx, initial, result.Solution).IsSolved(ctx),
                        $"[{name}/{mode}] replayed solution left the board unsolved");
                }
            }
        }

        [Test]
        public void Search_TimeBonusOnABoard_ExploresAnIdenticalStateSpace()
        {
            // Time is outside the search space (D12), and the heuristic must not
            // read it either: the two boards must explore the *same* states, not
            // merely reach the same answer.
            var plain = LayeredTwoGateBoard();
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
            var strategy = new AStarStrategy();

            var plainResult = strategy.Search(plain, BoardState.CreateInitial(plain), Budget(MoveGenMode.Exhaustive));
            var bonusResult = strategy.Search(withBonus, BoardState.CreateInitial(withBonus), Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, plainResult.Status);
            Assert.AreEqual(plainResult.Status, bonusResult.Status);
            Assert.AreEqual(plainResult.Solution.Count, bonusResult.Solution.Count);
            Assert.AreEqual(plainResult.ExploredStateCount, bonusResult.ExploredStateCount);
        }

        // ----- Equivalence with breadth-first search ---------------------

        [Test]
        public void Search_MatchesBreadthFirstStatusAndMoveCount_OverTheWholeCorpus()
        {
            // The proof the heuristic is sound enough to ship (D3). Solutions are
            // compared by length only: the two strategies break ties differently,
            // so they may legitimately pick different shortest paths.
            var aStar = new AStarStrategy();
            var breadthFirst = new BreadthFirstStrategy();

            foreach (var (name, ctx, initial) in WholeCorpus())
            {
                foreach (var mode in new[] { MoveGenMode.Canonical, MoveGenMode.Exhaustive })
                {
                    var expected = breadthFirst.Search(ctx, initial, Budget(mode));
                    var actual = aStar.Search(ctx, initial, Budget(mode));

                    Assert.AreEqual(expected.Status, actual.Status, $"[{name}/{mode}] status");
                    Assert.AreEqual(
                        expected.Solution.Count, actual.Solution.Count,
                        $"[{name}/{mode}] A* and breadth-first disagree on the optimum");
                }
            }
        }

        [Test]
        public void Search_SymmetryCollapsedAndPlainIdentity_ReturnTheSameVerdictAndOptimum_OverTheWholeCorpus()
        {
            // The collapse quotients the state graph, and A* trusts a closed set
            // to be final. That is only safe because the heuristic is a sum of
            // per-index terms over (spec, row) pairs and so cannot tell two
            // interchangeable blocks apart — h is identical on states the
            // collapse merges. If that ever stops being true, the search throws
            // on an inconsistent heuristic rather than quietly returning a
            // non-optimum, so this test covers both failures at once.
            var strategy = new AStarStrategy();

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
        public void Search_ExploresFewerStatesThanBreadthFirst_OnAnOpenBoard()
        {
            // Every block on the open grid has dozens of pointless destinations.
            // Breadth-first search expands all of them one level at a time; A*
            // follows the moves that lower h, so it should expand little more
            // than one state per move of the solution.
            var ctx = LooseBlocksOneGateBoard();
            var initial = BoardState.CreateInitial(ctx);

            var aStar = new AStarStrategy().Search(ctx, initial, Budget(MoveGenMode.Exhaustive));
            var breadthFirst = new BreadthFirstStrategy().Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(SolveStatus.Solvable, aStar.Status);
            Assert.AreEqual(breadthFirst.Solution.Count, aStar.Solution.Count);
            Assert.Less(
                aStar.ExploredStateCount, breadthFirst.ExploredStateCount,
                "A* expanded at least as many states as breadth-first search");
        }

        // ----- Heuristic --------------------------------------------------

        [Test]
        public void EstimateRemainingMoves_InitialState_DoesNotExceedTheOptimum_ForEveryCorpusBoard()
        {
            foreach (var (name, ctx, initial, optimum) in SolvableCorpus())
            {
                var estimate = AStarStrategy.EstimateRemainingMoves(ctx, initial);

                Assert.LessOrEqual(estimate, optimum, $"[{name}] heuristic overestimates");
            }
        }

        [Test]
        public void EstimateRemainingMoves_EveryReachableState_DropsByAtMostOnePerMoveAndIsZeroWhenSolved()
        {
            // Consistency over every edge of every corpus board's full (exhaustive)
            // state graph — the property that makes closed-set A* safe without
            // reopening. With h = 0 on solved states it also implies admissibility
            // everywhere, not only at the initial state.
            var generator = new MoveGenerator();
            var resolver = new MoveResolver();

            foreach (var (name, ctx, initial) in WholeCorpus())
            {
                var visited = new HashSet<BoardState> { initial };
                var frontier = new Queue<BoardState>();
                frontier.Enqueue(initial);

                while (frontier.Count > 0)
                {
                    var state = frontier.Dequeue();
                    var h = AStarStrategy.EstimateRemainingMoves(ctx, state);

                    Assert.GreaterOrEqual(h, 0, $"[{name}] negative estimate");
                    if (state.IsSolved(ctx))
                    {
                        Assert.AreEqual(0, h, $"[{name}] non-zero estimate on a solved state");
                        continue;
                    }

                    foreach (var move in generator.Generate(ctx, state, MoveGenMode.Exhaustive))
                    {
                        Assert.IsTrue(resolver.TryApplyMove(ctx, state, move, out var successor, out _));

                        Assert.LessOrEqual(
                            h, 1 + AStarStrategy.EstimateRemainingMoves(ctx, successor),
                            $"[{name}] estimate dropped by more than one on {move}");

                        if (visited.Add(successor))
                        {
                            frontier.Enqueue(successor);
                        }
                    }
                }
            }
        }

        [Test]
        public void EstimateRemainingMoves_LockWithMixedKeyEffects_CountsThePossibleFreeClear()
        {
            // Three colours, but one lock still has an unconsumed ClearOuterColor
            // key among its keys, so one clear may come free. Requiring *every*
            // unconsumed key to be ClearOuterColor would give 3 against a 2-move
            // optimum.
            var ctx = MixedKeyEffectLockBoard();

            var estimate = AStarStrategy.EstimateRemainingMoves(ctx, BoardState.CreateInitial(ctx));

            Assert.AreEqual(2, estimate);
        }

        [Test]
        public void EstimateRemainingMoves_ClearOuterColorWaitingForAShutter_CountsTheFreeClear()
        {
            // D41: after the key's push its lock has no unconsumed key left, but
            // ClearOuterColor waits on the owner. Two colours remain and one
            // move — the green push — clears both.
            var ctx = ShutteredClearOuterColorLockBoard();
            new MoveResolver().TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var waiting, out _);

            var estimate = AStarStrategy.EstimateRemainingMoves(ctx, waiting);

            Assert.AreEqual(KeyEffect.ClearOuterColor, waiting.WaitingKeyEffect[2]);
            Assert.AreEqual(1, estimate);
        }

        [Test]
        public void EstimateRemainingMoves_UnlockMovementWaiting_DoesNotCountALaterClearOuterColorKey()
        {
            // Lock 1 needs one key. The UnlockMovement key completed it under
            // the shutter, so the ClearOuterColor key still unconsumed can no
            // longer fire (M8): all three remaining colours cost a move each.
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: KeyEffect.UnlockMovement),
                    Block(2, new Coord(1, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green }),
                    Block(4, new Coord(3, 0), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 1)
                },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 1, 1, BlockColor.Red),
                    Gate(3, BoardEdge.Bottom, 2, 1, BlockColor.Green),
                    Gate(4, BoardEdge.Bottom, 3, 1, BlockColor.Blue)
                },
                shutters: new[] { Shutter(1, new Coord(3, 0), new Coord(3, 0), 1, BlockColor.Green) });
            new MoveResolver().TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var waiting, out _);

            var estimate = AStarStrategy.EstimateRemainingMoves(ctx, waiting);
            var remaining = new BreadthFirstStrategy().Search(ctx, waiting, Budget(MoveGenMode.Exhaustive));

            Assert.AreEqual(KeyEffect.UnlockMovement, waiting.WaitingKeyEffect[3]);
            Assert.AreEqual(3, estimate);
            Assert.AreEqual(remaining.Solution.Count, estimate, "the estimate is exact here, not merely admissible");
        }

        [Test]
        public void EstimateRemainingMoves_OpeningThatReleasesTwoWaitingClears_DropsByExactlyOne()
        {
            // The move that opens the shutter clears three colours: its own and
            // both released owners'. Both locks leave F with it, so h drops by
            // one — without counting waiting clears in F it would drop by three.
            var ctx = TwoWaitingClearsUnderOneShutterBoard();
            var resolver = new MoveResolver();
            resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var firstKey, out _);
            resolver.TryApplyMove(ctx, firstKey, new Move(1, new Coord(1, 0)), out var bothWaiting, out _);
            resolver.TryApplyMove(ctx, bothWaiting, new Move(2, new Coord(2, 0)), out var solved, out _);

            var before = AStarStrategy.EstimateRemainingMoves(ctx, bothWaiting);
            var after = AStarStrategy.EstimateRemainingMoves(ctx, solved);

            Assert.IsTrue(solved.IsSolved(ctx));
            Assert.AreEqual(1, before);
            Assert.AreEqual(0, after);
        }

        [Test]
        public void Search_TwoWaitingClearsUnderOneShutter_ReturnsTheBreadthFirstOptimum()
        {
            var ctx = TwoWaitingClearsUnderOneShutterBoard();
            var initial = BoardState.CreateInitial(ctx);

            foreach (var mode in new[] { MoveGenMode.Canonical, MoveGenMode.Exhaustive })
            {
                var expected = new BreadthFirstStrategy().Search(ctx, initial, Budget(mode));
                var actual = new AStarStrategy().Search(ctx, initial, Budget(mode));

                Assert.AreEqual(SolveStatus.Solvable, actual.Status, $"[{mode}]");
                Assert.AreEqual(3, expected.Solution.Count, $"[{mode}] breadth-first optimum");
                Assert.AreEqual(expected.Solution.Count, actual.Solution.Count, $"[{mode}] A* optimum");
            }
        }

        [Test]
        public void EstimateRemainingMoves_WaitingClearAppliedWhenItsBlockSpawns_NeverDropsByMoreThanOne()
        {
            // D42 on the corpus board built for it. The red push leaves
            // ClearOuterColor waiting in the unspawned slot: the lock is now
            // counted through what waits instead of through its key. Moving
            // green aside spawns the lock's block and the effect clears it at
            // once: one colour and one free clear go together, so h holds.
            var ctx = GeneratorReleasesAWaitingKeyEffectBoard();
            var resolver = new MoveResolver();
            var initial = BoardState.CreateInitial(ctx);
            resolver.TryApplyMove(ctx, initial, new Move(0, new Coord(0, 0)), out var waiting, out _);
            resolver.TryApplyMove(ctx, waiting, new Move(1, new Coord(1, 1)), out var spawned, out _);

            var before = AStarStrategy.EstimateRemainingMoves(ctx, initial);
            var afterKey = AStarStrategy.EstimateRemainingMoves(ctx, waiting);
            var afterSpawn = AStarStrategy.EstimateRemainingMoves(ctx, spawned);

            Assert.AreEqual(KeyEffect.ClearOuterColor, waiting.WaitingKeyEffect[2]);
            Assert.IsFalse(spawned.Alive[2], "the released clear destroyed the spawned block");
            Assert.AreEqual(new[] { 2, 1, 1 }, new[] { before, afterKey, afterSpawn });
        }

        [Test]
        public void EstimateRemainingMoves_PendingGeneratorAndElevatorOutput_CountsEveryColour()
        {
            // Two top-level red blocks stand on the generator's spawn cell and in
            // the elevator's region, so neither spawns at level start (D42) and
            // both slots are still unspawned. One colour each for the blockers,
            // two in the generator's layered block, three in the wave's.
            var ctx = Ctx(
                3, 3,
                blocks: new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 2)) },
                generators: new[]
                {
                    Spawner(1, BoardEdge.Left, 0, 1, Spawned(colors: new[] { BlockColor.Red, BlockColor.Blue }))
                },
                elevators: new[]
                {
                    Elevator(1, new Coord(2, 2), new Coord(2, 2),
                        new[]
                        {
                            Spawned(
                                colors: new[] { BlockColor.Green, BlockColor.Yellow, BlockColor.Green },
                                regionOrigin: new Coord(0, 0))
                        })
                });

            var initial = BoardState.CreateInitial(ctx);
            var estimate = AStarStrategy.EstimateRemainingMoves(ctx, initial);

            Assert.IsFalse(initial.Alive[2] || initial.Alive[3], "the fixture's spawner output must still be pending");
            Assert.AreEqual(7, estimate);
        }

        // ----- Reproducibility --------------------------------------

        [Test]
        public void Search_IsReproducible_AcrossRuns()
        {
            // Pins the concrete sequence, not only agreement between two runs: the
            // near block (index 1) slides to the gate and clears, then the far
            // block (index 0) follows it to the same cell.
            var ctx = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var initial = BoardState.CreateInitial(ctx);
            var strategy = new AStarStrategy();
            var expected = new[] { new Move(1, new Coord(5, 0)), new Move(0, new Coord(5, 0)) };

            var first = strategy.Search(ctx, initial, Budget(MoveGenMode.Exhaustive));
            var second = strategy.Search(ctx, initial, Budget(MoveGenMode.Exhaustive));

            CollectionAssert.AreEqual(expected, first.Solution);
            CollectionAssert.AreEqual(expected, second.Solution);
        }

        // ----- Argument validation -----------------------------------

        [Test]
        public void Search_NullArguments_Throw()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) });
            var initial = BoardState.CreateInitial(ctx);
            var strategy = new AStarStrategy();
            var budget = Budget(MoveGenMode.Canonical);

            Assert.Throws<ArgumentNullException>(() => strategy.Search(null, initial, budget));
            Assert.Throws<ArgumentNullException>(() => strategy.Search(ctx, null, budget));
            Assert.Throws<ArgumentNullException>(() => strategy.Search(ctx, initial, null));
        }

        /// <summary>
        /// A two-colour block that must visit a gate on each side of a 5x5 board:
        /// optimum 2. The board the budget tests tighten around.
        /// </summary>
        private static LevelContext LayeredTwoGateBoard()
        {
            return Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 2, 1, BlockColor.Blue)
                });
        }
    }
}
