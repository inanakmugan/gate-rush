using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="NextClearAbstraction"/>: what the copy keeps, merges and
    /// drops, that it refuses states it cannot model, that its moves map back to
    /// the source board, and — the property the solver relies on — that over
    /// many seeded random boards and mid-level states, the shortest route to the
    /// next clear has the same length on the copy as on the source, and the
    /// copy's route replays on the source board.
    /// </summary>
    public class NextClearAbstractionTests
    {
        private const int BfsStateCap = 20_000;
        private const int RandomBoardCount = 250;
        private const int MaxWalkMoves = 6;
        private const int CorpusSeed = 20260925;

        private static BlockColor ColorOf(NextClearAbstraction abstraction, int abstractIndex) =>
            abstraction.Initial.CurrentColorOf(abstraction.Context, abstractIndex);

        // ----- What the copy keeps, merges and drops ----------------------

        [Test]
        public void Of_ColoursWithoutAnOpenGate_ShareOneColourAndTheOpenGateColourIsKept()
        {
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green })
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });

            var abstraction = NextClearAbstraction.Of(ctx, BoardState.CreateInitial(ctx));

            Assert.AreEqual(BlockColor.Red, ColorOf(abstraction, 0));
            Assert.AreEqual(ColorOf(abstraction, 1), ColorOf(abstraction, 2));
            Assert.AreNotEqual(BlockColor.Red, ColorOf(abstraction, 1));
            Assert.AreEqual(1, abstraction.Context.BlockSymmetry.Groups.Count, "the merged blocks should be interchangeable");
        }

        [Test]
        public void Of_GateStillClosed_IsDroppedAndItsColourMerged()
        {
            // Blue's gate is still closed; green has no gate at all. Neither can
            // clear next, so both should share the merged colour. The test checks
            // that they share it rather than which colour it is: the merged colour
            // is whichever colour has no open gate, and that may be one of the
            // blocks' own.
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green })
                },
                new[]
                {
                    Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Left, 0, 1, BlockColor.Blue, openAt: 1)
                });

            var abstraction = NextClearAbstraction.Of(ctx, BoardState.CreateInitial(ctx));

            Assert.AreEqual(1, abstraction.Context.Gates.Count);
            Assert.AreEqual(BlockColor.Red, abstraction.Context.Gates[0].Color);
            Assert.AreEqual(ColorOf(abstraction, 1), ColorOf(abstraction, 2));
            Assert.AreEqual(1, abstraction.Context.BlockSymmetry.Groups.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, abstraction.Context.BlockSymmetry.Groups[0]);
        }

        [Test]
        public void Of_FrozenAndLockedBlocks_CannotMoveInTheCopyAndTheirColourIsMerged()
        {
            var ctx = Ctx(
                5, 3,
                new[]
                {
                    Block(1, new Coord(0, 0), unfreezeAt: 1),
                    Block(2, new Coord(2, 0), lockId: 1, requiredKeys: 1),
                    Block(3, new Coord(4, 2), keyTarget: 1)
                },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });

            var abstraction = NextClearAbstraction.Of(ctx, BoardState.CreateInitial(ctx));

            Assert.IsFalse(abstraction.Initial.CanMove(abstraction.Context, 0), "frozen");
            Assert.IsFalse(abstraction.Initial.CanMove(abstraction.Context, 1), "locked");
            Assert.IsTrue(abstraction.Initial.CanMove(abstraction.Context, 2), "free");
            Assert.AreNotEqual(BlockColor.Red, ColorOf(abstraction, 0));
            Assert.AreNotEqual(BlockColor.Red, ColorOf(abstraction, 1));
            Assert.AreEqual(BlockColor.Red, ColorOf(abstraction, 2));
        }

        [Test]
        public void Of_ClosedShutter_IsKeptClosedAndHoldsItsBlock_OpenShutterIsDropped()
        {
            var ctx = Ctx(
                4, 3,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(3, 2))
                },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) },
                shutters: new[]
                {
                    Shutter(1, new Coord(3, 2), new Coord(3, 2), threshold: 2),
                    Shutter(2, new Coord(1, 1), new Coord(1, 1), threshold: 0)
                });

            var abstraction = NextClearAbstraction.Of(ctx, BoardState.CreateInitial(ctx));

            Assert.AreEqual(1, abstraction.Context.Shutters.Count);
            Assert.AreEqual(new Coord(3, 2), abstraction.Context.Shutters[0].Min);
            Assert.IsFalse(abstraction.Initial.ShutterOpen[0]);
            Assert.IsFalse(abstraction.Initial.CanMove(abstraction.Context, 1));
        }

        [Test]
        public void Of_LayeredBlock_KeepsOnlyItsCurrentColour()
        {
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });

            var abstraction = NextClearAbstraction.Of(ctx, BoardState.CreateInitial(ctx));

            CollectionAssert.AreEqual(new[] { BlockColor.Red }, abstraction.Context.SpecAt(0).ColorStack);
        }

        [Test]
        public void Of_StateAfterAClear_LeavesTheDestroyedBlockOutAndMapsTheRestBack()
        {
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(2, 0)),
                    Block(2, new Coord(0, 0))
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var initial = BoardState.CreateInitial(ctx);
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, initial, new Move(0, new Coord(2, 0)), out var afterClear, out _));

            var abstraction = NextClearAbstraction.Of(ctx, afterClear);

            Assert.AreEqual(1, abstraction.Context.Blocks.Count);
            Assert.AreEqual(new Coord(0, 0), abstraction.Initial.Origins[0]);
            Assert.AreEqual(new Move(1, new Coord(2, 0)), abstraction.ToSourceMove(new Move(0, new Coord(2, 0))));
        }

        // ----- What it refuses --------------------------------------------

        [Test]
        public void Of_GeneratorOutputPending_Throws()
        {
            var ctx = Ctx(
                3, 3,
                new[] { Block(1, new Coord(2, 2)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) },
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned()) });

            Assert.Throws<InvalidOperationException>(() => NextClearAbstraction.Of(ctx, BoardState.CreateInitial(ctx)));
        }

        [Test]
        public void CanModel_FalseExactlyWhenGeneratorOutputIsPending()
        {
            var withGenerator = Ctx(
                3, 3,
                new[] { Block(1, new Coord(2, 2)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) },
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned()) });
            var plain = Ctx(3, 3, new[] { Block(1, new Coord(2, 2)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });

            Assert.IsFalse(NextClearAbstraction.CanModel(withGenerator, BoardState.CreateInitial(withGenerator)));
            Assert.IsTrue(NextClearAbstraction.CanModel(plain, BoardState.CreateInitial(plain)));
        }

        [Test]
        public void ToSourceMove_IndexOutsideTheCopy_Throws()
        {
            var ctx = Ctx(2, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var abstraction = NextClearAbstraction.Of(ctx, BoardState.CreateInitial(ctx));

            Assert.Throws<ArgumentOutOfRangeException>(() => abstraction.ToSourceMove(new Move(1, new Coord(1, 0))));
        }

        // ----- The property the solver relies on ---------------------------

        [Test]
        public void NextClearDistance_OnTheCopy_MatchesTheSourceBoard_OverSeededRandomBoards()
        {
            var coverage = new Coverage();

            foreach (var (ctx, state) in SampleStates())
            {
                var source = ShortestRouteToNextClear(ctx, state);
                var abstraction = NextClearAbstraction.Of(ctx, state);
                var copy = ShortestRouteToNextClear(abstraction.Context, abstraction.Initial);
                if (source.Capped || copy.Capped)
                {
                    continue;
                }

                Assert.AreEqual(
                    source.Length, copy.Length,
                    $"level {ctx.LevelId}: next-clear distance differs (source {Describe(source)}, copy {Describe(copy)})");
                coverage.Record(ctx, state, source.Length.HasValue);
            }

            coverage.AssertBroadEnough();
        }

        [Test]
        public void ToSourceMove_ReplayingTheCopysShortestRoute_ClearsOnTheSourceAtTheSameLength()
        {
            var resolver = new MoveResolver();
            var replayed = 0;

            foreach (var (ctx, state) in SampleStates())
            {
                var abstraction = NextClearAbstraction.Of(ctx, state);
                var copy = ShortestRouteToNextClear(abstraction.Context, abstraction.Initial);
                if (copy.Capped || !copy.Length.HasValue)
                {
                    continue;
                }

                var current = state;
                for (var i = 0; i < copy.Moves.Count; i++)
                {
                    var move = abstraction.ToSourceMove(copy.Moves[i]);
                    Assert.IsTrue(
                        resolver.TryApplyMove(ctx, current, move, out var next, out _),
                        $"level {ctx.LevelId}: source board rejected {move} at step {i}");

                    var cleared = next.TotalClearCount > state.TotalClearCount;
                    Assert.AreEqual(
                        i == copy.Moves.Count - 1, cleared,
                        $"level {ctx.LevelId}: step {i} of {copy.Moves.Count} {(cleared ? "cleared early" : "did not clear")}");
                    current = next;
                }

                replayed++;
            }

            Assert.Greater(replayed, RandomBoardCount / 4, "too few routes replayed to mean anything");
        }

        // ----- Sampling and search helpers ---------------------------------

        /// <summary>
        /// One state per random board: the initial state, or where a short
        /// seeded random walk of legal moves (clears included) leaves it, so
        /// states with open gates, thawed blocks, open shutters and partly
        /// cleared stacks are sampled too. Solved states are skipped.
        /// </summary>
        private static IEnumerable<(LevelContext ctx, BoardState state)> SampleStates()
        {
            var rng = new Random(CorpusSeed);
            var generator = new MoveGenerator();
            var resolver = new MoveResolver();

            for (var b = 0; b < RandomBoardCount; b++)
            {
                var ctx = RandomBoards.Next(rng);
                var state = BoardState.CreateInitial(ctx);
                var walk = rng.Next(MaxWalkMoves + 1);
                for (var step = 0; step < walk && !state.IsSolved(ctx); step++)
                {
                    var moves = new List<Move>(generator.Generate(ctx, state, MoveGenMode.Exhaustive));
                    if (moves.Count == 0)
                    {
                        break;
                    }

                    resolver.TryApplyMove(ctx, state, moves[rng.Next(moves.Count)], out state, out _);
                }

                if (!state.IsSolved(ctx))
                {
                    yield return (ctx, state);
                }
            }
        }

        private readonly struct Route
        {
            public Route(int? length, IReadOnlyList<Move> moves, bool capped)
            {
                Length = length;
                Moves = moves;
                Capped = capped;
            }

            /// <summary>Moves to the nearest clear, including the clearing move; null when no clear is reachable.</summary>
            public int? Length { get; }

            public IReadOnlyList<Move> Moves { get; }

            /// <summary>The search hit <see cref="BfsStateCap"/> before it could answer.</summary>
            public bool Capped { get; }
        }

        /// <summary>
        /// Breadth-first search over exhaustive moves to the nearest state with
        /// more clears than <paramref name="start"/>. An independent reference —
        /// it shares nothing with the solver beyond the move generator and
        /// resolver the rules live in.
        /// </summary>
        private static Route ShortestRouteToNextClear(LevelContext ctx, BoardState start)
        {
            var generator = new MoveGenerator();
            var resolver = new MoveResolver();
            var parent = new Dictionary<BoardState, (BoardState from, Move move)> { [start] = (null, default) };
            var queue = new Queue<BoardState>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                foreach (var move in generator.Generate(ctx, state, MoveGenMode.Exhaustive))
                {
                    if (!resolver.TryApplyMove(ctx, state, move, out var next, out _))
                    {
                        continue;
                    }

                    if (next.TotalClearCount > start.TotalClearCount)
                    {
                        var route = new List<Move> { move };
                        for (var at = state; parent[at].from != null; at = parent[at].from)
                        {
                            route.Add(parent[at].move);
                        }

                        route.Reverse();
                        return new Route(route.Count, route, capped: false);
                    }

                    if (parent.ContainsKey(next))
                    {
                        continue;
                    }

                    if (parent.Count >= BfsStateCap)
                    {
                        return new Route(null, Array.Empty<Move>(), capped: true);
                    }

                    parent[next] = (state, move);
                    queue.Enqueue(next);
                }
            }

            return new Route(null, Array.Empty<Move>(), capped: false);
        }

        private static string Describe(Route route) =>
            route.Length.HasValue ? $"{route.Length} moves" : "no clear reachable";

        /// <summary>
        /// Counts how many compared states exercised each mechanic, so the
        /// property test fails loudly if the random corpus ever stops covering
        /// one — a pass over boards that never contain a shutter proves nothing
        /// about shutters.
        /// </summary>
        private sealed class Coverage
        {
            private const int MinCompared = RandomBoardCount / 2;
            private const int MinPerMechanic = 25;

            private int compared;
            private int withClearReachable;
            private int withNoClearReachable;
            private int withLock;
            private int withClosedShutter;
            private int withFrozenBlock;
            private int withLayeredBlock;
            private int midLevel;

            public void Record(LevelContext ctx, BoardState state, bool clearReachable)
            {
                compared++;
                if (clearReachable)
                {
                    withClearReachable++;
                }
                else
                {
                    withNoClearReachable++;
                }

                if (state.TotalClearCount > 0)
                {
                    midLevel++;
                }

                var hasLock = false;
                var hasFrozen = false;
                var hasLayered = false;
                for (var i = 0; i < ctx.TotalBlockCapacity; i++)
                {
                    if (!state.Alive[i])
                    {
                        continue;
                    }

                    hasLock |= !state.Unlocked[i];
                    hasFrozen |= !state.Unfrozen[i];
                    hasLayered |= ctx.SpecAt(i).ColorStack.Count - state.ClearedColors[i] > 1;
                }

                var hasClosedShutter = false;
                for (var s = 0; s < ctx.Shutters.Count; s++)
                {
                    hasClosedShutter |= !state.ShutterOpen[s];
                }

                withLock += hasLock ? 1 : 0;
                withFrozenBlock += hasFrozen ? 1 : 0;
                withLayeredBlock += hasLayered ? 1 : 0;
                withClosedShutter += hasClosedShutter ? 1 : 0;
            }

            public void AssertBroadEnough()
            {
                Assert.GreaterOrEqual(compared, MinCompared, "states compared");
                Assert.GreaterOrEqual(withClearReachable, MinPerMechanic, "states with a reachable clear");
                Assert.GreaterOrEqual(withNoClearReachable, MinPerMechanic, "states with no reachable clear");
                Assert.GreaterOrEqual(withLock, MinPerMechanic, "states with a locked block");
                Assert.GreaterOrEqual(withClosedShutter, MinPerMechanic, "states with a closed shutter");
                Assert.GreaterOrEqual(withFrozenBlock, MinPerMechanic, "states with a frozen block");
                Assert.GreaterOrEqual(withLayeredBlock, MinPerMechanic, "states with a layered block");
                Assert.GreaterOrEqual(midLevel, MinPerMechanic, "states after at least one clear");
            }
        }
    }
}
