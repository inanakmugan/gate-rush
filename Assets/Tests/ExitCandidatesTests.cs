using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Fixture;
using static GateRush.Tests.SearchCorpus;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ExitCandidates"/>: which exits count as possible for a
    /// stratum — the dead-end signal the nearest-next-clear strategy acts on —
    /// and the blocker count it orders its search by, which counts blocks on the
    /// route to an exit as well as on the exit.
    /// </summary>
    public class ExitCandidatesTests
    {
        // ----- The start origin under an axis restriction (D39) ---------------

        [Test]
        public void IsEmpty_AxisBlockBoxedInAboveACrossAxisGate_IsTrue()
        {
            var ctx = AxisBlockBoxedAboveCrossAxisGateBoard();

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void IsEmpty_AxisBlockThatCanStepOffItsCrossAxisGate_IsFalse()
        {
            var ctx = AxisBlockReturnsToCrossAxisGateBoard();

            Assert.IsFalse(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void FewestBlockers_AxisBlockMustStepOffPastABlockerToArriveBack_CountsTheBlocker()
        {
            // The red block cannot be pushed down in place; stepping off either
            // way runs into a blue block, and arriving back meets nobody.
            var ctx = Ctx(
                3, 2,
                new[]
                {
                    Block(1, new Coord(1, 0), axis: MovementAxis.HorizontalOnly),
                    Block(2, new Coord(0, 0), colors: new[] { BlockColor.Blue }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Blue })
                },
                new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var fewest = ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state);

            Assert.AreEqual(1, fewest);
        }

        [Test]
        public void FewestBlockers_AxisBlockBoxedInAgainstAnAxisEndGate_IsZero()
        {
            // Pushing right, along its axis, is allowed in place.
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(2, 0), axis: MovementAxis.HorizontalOnly) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) },
                staticWalls: new[] { new Coord(1, 0) });
            var state = BoardState.CreateInitial(ctx);

            var candidates = ExitCandidates.Of(ctx, state);

            Assert.IsFalse(candidates.IsEmpty);
            Assert.AreEqual(0, candidates.FewestBlockers(ctx, state));
        }

        // ----- Exits in general ------------------------------------------------

        [Test]
        public void IsEmpty_ColourHasNoOpenGate_IsTrue()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void IsEmpty_OnlyExitCoveredByAFrozenBlock_IsTrue()
        {
            // The blue block sits frozen in the red gate's mouth; no clear can
            // thaw it before a clear, so the red block has nowhere to exit.
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }, unfreezeAt: 1)
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void IsEmpty_OnlyExitUnderAClosedShutter_IsTrue()
        {
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0), threshold: 1) });

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void IsEmpty_OnlyRouteToTheExitPassesAFrozenBlock_IsTrue()
        {
            // The exit cell itself is free, but the frozen blue block stands on
            // the only way there and cannot step aside before a clear.
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }, unfreezeAt: 1)
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void FewestBlockers_RouteAroundTheBlocker_IsZero()
        {
            // Same layout without the wall: red can go over blue through row 1.
            var ctx = Ctx(
                3, 2,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue })
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(0, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_ExitAlreadyFree_IsZero()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(0, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_BlockAlreadyAtItsExit_DoesNotCountItself()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(2, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(0, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_StepAsideBoard_CountsTheOneBlockInTheWay()
        {
            // Red needs (2, 0); blue at (1, 0) stands on the way, not on the exit
            // itself, and the wall at (2, 1) closes the route around it.
            var ctx = StepAsideBeforeFirstClearBoard();
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(1, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_MultiCellBlockerOverSeveralFootprintCells_CountsOnce()
        {
            // A 2x2 red block must reach the 2-wide gate at the right of rows 0-1;
            // one vertical 1x2 blue block covers both cells of the column it needs.
            var square = new[] { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };
            var tall = new[] { new Coord(0, 0), new Coord(0, 1) };
            var ctx = Ctx(
                4, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), cells: square),
                    Block(2, new Coord(3, 0), cells: tall, colors: new[] { BlockColor.Blue })
                },
                new[] { Gate(1, BoardEdge.Right, 0, 2, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(1, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_EmptyStratum_Throws()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);

            Assert.Throws<InvalidOperationException>(() => ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        // ----- Against the search it replaced (Module 28) ----------------------

        [Test]
        public void RouteCosts_MatchTheScanningSearch_OverTheSearchCorpus()
        {
            var tally = new Tally();

            foreach (var (name, ctx, _) in WholeCorpus())
            {
                CompareEveryStratum(ctx, name, tally);
            }

            CompareEveryStratum(StepAsideBeforeFirstClearBoard(), "step aside before the first clear", tally);
            CompareEveryStratum(AxisBlockReturnsToCrossAxisGateBoard(), "axis block returns to its gate", tally);
            CompareEveryStratum(AxisBlockBoxedAboveCrossAxisGateBoard(), "axis block boxed in", tally);

            // Fifteen solvable corpus boards have no generator or elevator, so
            // each is compared at its starting state at least; and the
            // step-aside board starts with one blocker in the way.
            Assert.GreaterOrEqual(tally.States, MinCorpusStates, "states compared");
            Assert.GreaterOrEqual(tally.CostsAboveZero, 1, "route costs above zero compared");
        }

        [TestCase(4, 60, 20261013)]
        [TestCase(5, 30, 20261014)]
        public void RouteCosts_MatchTheScanningSearch_OverSeededRandomBoards(int maxSide, int boardCount, int seed)
        {
            var rng = new Random(seed);
            var tally = new Tally();

            for (var b = 0; b < boardCount; b++)
            {
                CompareEveryStratum(RandomBoards.Next(rng, maxSide), $"board {b} (seed {seed}, side {maxSide})", tally);
            }

            Assert.GreaterOrEqual(tally.States, boardCount, "states compared");
            Assert.GreaterOrEqual(
                tally.CostsAboveZero, boardCount / RandomBoardsPerCostAboveZero, "route costs above zero compared");
        }

        /// <summary>The solvable corpus boards without a generator or an elevator: each is modelled from its first state.</summary>
        private const int MinCorpusStates = 15;

        /// <summary>At least one route cost above zero is compared per this many random boards.</summary>
        private const int RandomBoardsPerCostAboveZero = 4;

        /// <summary>How many strata of one board are compared; keeps a large random board cheap.</summary>
        private const int MaxStrataPerBoard = 12;

        /// <summary>How many states of one stratum are visited, compared or walked.</summary>
        private const int MaxStatesPerStratum = 120;

        /// <summary>What a comparison covered, for its floors.</summary>
        private sealed class Tally
        {
            public int States { get; set; }

            public int CostsAboveZero { get; set; }
        }

        /// <summary>
        /// Walks <paramref name="ctx"/> stratum by stratum from its first
        /// state. Wherever the nearest-next-clear strategy would build an
        /// <see cref="ExitCandidates"/> — a stratum with no spawn pending, on
        /// its <see cref="NextClearAbstraction"/> — builds the scanning copy
        /// beside it and holds every state of the stratum to the same answers.
        /// </summary>
        private static void CompareEveryStratum(LevelContext ctx, string label, Tally tally)
        {
            var generator = new MoveGenerator();
            var resolver = new MoveResolver();

            var roots = new Queue<BoardState>();
            var seenRoots = new HashSet<BoardState>();
            var first = BoardState.CreateInitial(ctx);
            roots.Enqueue(first);
            seenRoots.Add(first);

            for (var strata = 0; roots.Count > 0 && strata < MaxStrataPerBoard; strata++)
            {
                var root = roots.Dequeue();
                if (NextClearAbstraction.CanModel(ctx, root))
                {
                    CompareStratum(NextClearAbstraction.Of(ctx, root), label, tally, generator, resolver);
                }

                // The real board finds the strata that follow: every state a
                // clear or a spawn leads to starts one.
                var vector = root.ProgressVector;
                var frontier = new Queue<BoardState>();
                var seen = new HashSet<BoardState> { root };
                frontier.Enqueue(root);
                while (frontier.Count > 0)
                {
                    var state = frontier.Dequeue();
                    foreach (var move in generator.Generate(ctx, state, MoveGenMode.Exhaustive))
                    {
                        Assert.IsTrue(resolver.TryApplyMove(ctx, state, move, out var next, out _), $"{label}: {move}");
                        if (next.ProgressVector != vector)
                        {
                            if (seenRoots.Add(next))
                            {
                                roots.Enqueue(next);
                            }
                        }
                        else if (seen.Count < MaxStatesPerStratum && seen.Add(next))
                        {
                            frontier.Enqueue(next);
                        }
                    }
                }
            }
        }

        private static void CompareStratum(
            NextClearAbstraction abstraction, string label, Tally tally, MoveGenerator generator, MoveResolver resolver)
        {
            var ctx = abstraction.Context;
            var root = abstraction.Initial;
            var candidates = ExitCandidates.Of(ctx, root);
            var legacy = LegacyExitCandidates.Of(ctx, root);

            Assert.AreEqual(legacy.IsEmpty, candidates.IsEmpty, $"{label}: IsEmpty");
            Assert.AreEqual(legacy.CandidateCount, candidates.CandidateCount, $"{label}: candidate count");
            if (candidates.IsEmpty)
            {
                return;
            }

            var frontier = new Queue<BoardState>();
            var seen = new HashSet<BoardState> { root };
            frontier.Enqueue(root);
            while (frontier.Count > 0)
            {
                var state = frontier.Dequeue();
                tally.States++;

                var expectedFewest = legacy.FewestBlockers(ctx, state);
                var fewest = candidates.FewestBlockers(ctx, state);
                Assert.AreEqual(expectedFewest, fewest, $"{label}: FewestBlockers");

                for (var k = 0; k < candidates.CandidateCount; k++)
                {
                    var expected = legacy.RouteCost(ctx, state, k);
                    var actual = candidates.RouteCost(ctx, state, k);
                    Assert.AreEqual(expected, actual, $"{label}: route cost of candidate {k}");
                    if (expected > 0 && expected != int.MaxValue)
                    {
                        tally.CostsAboveZero++;
                    }
                }

                foreach (var move in generator.Generate(ctx, state, MoveGenMode.Exhaustive))
                {
                    Assert.IsTrue(resolver.TryApplyMove(ctx, state, move, out var next, out _), $"{label}: {move}");

                    // A clear ends the stratum; the copy is not valid past it.
                    if (next.TotalClearCount == root.TotalClearCount
                        && seen.Count < MaxStatesPerStratum && seen.Add(next))
                    {
                        frontier.Enqueue(next);
                    }
                }
            }
        }
    }
}
