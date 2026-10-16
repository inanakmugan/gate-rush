using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 18's <see cref="MoveChanges"/>: what a move changed, as the
    /// player sees it — thaws, openings, unlocks, count pops, spawns with their
    /// source — and its clears, exactly as <see cref="ResolutionDiff"/>
    /// reports them. Grid y grows upward; in a one-row board every block is
    /// flush against the bottom edge, and a push down in place clears it into
    /// the bottom gate under it.
    /// </summary>
    public class MoveChangesTests
    {
        private static readonly Coord[] Horizontal1x2 = { new Coord(0, 0), new Coord(1, 0) };

        // The one count the chain board's gate, shutter and frozen block all
        // wait for: exactly the number of reds the sweep reaches (Module 33).
        private const int ChainThreshold = 3;

        // A threshold no clear in its test reaches, so what it guards stays shut.
        private const int UnreachedThreshold = 5;

        [Test]
        public void Between_PlainMoveThatClearsNothing_ReportsNothing()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(2, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, push: null);

            Assert.IsEmpty(changes.Clears);
            Assert.IsEmpty(changes.ThawedBlocks);
            Assert.IsEmpty(changes.OpenedGates);
            Assert.IsEmpty(changes.OpenedShutters);
            Assert.IsEmpty(changes.UnlockedBlocks);
            Assert.IsEmpty(changes.CountPops);
            Assert.IsEmpty(changes.Spawns);
            Assert.IsFalse(changes.HasArrivals);
        }

        [Test]
        public void Between_ClearThatThawsAFrozenBlock_ReportsTheThawAndNoCountPopForIt()
        {
            var ctx = RedBlockOverItsGateBeside(Block(2, new Coord(2, 0), unfreezeAt: 1));
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            CollectionAssert.AreEqual(new[] { 1 }, changes.ThawedBlocks);
            CollectionAssert.DoesNotContain(changes.CountPops, new CountPop(CountKind.Frozen, 1));
        }

        [Test]
        public void Between_ClearThatOnlyLowersAFrozenCount_ReportsAFrozenCountPop()
        {
            var ctx = RedBlockOverItsGateBeside(Block(2, new Coord(2, 0), unfreezeAt: 2));
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            CollectionAssert.AreEqual(new[] { new CountPop(CountKind.Frozen, 1) }, changes.CountPops);
            Assert.IsEmpty(changes.ThawedBlocks);
        }

        [Test]
        public void Between_ClearThatOpensAClosedGate_ReportsTheOpenedGate()
        {
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Bottom, 2, 1, BlockColor.Blue, openAt: 1) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            CollectionAssert.AreEqual(new[] { 1 }, changes.OpenedGates);
            CollectionAssert.DoesNotContain(changes.CountPops, new CountPop(CountKind.Gate, 1));
        }

        [Test]
        public void Between_ClearThatLowersAGateCount_ReportsAGateCountPop()
        {
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Bottom, 2, 1, BlockColor.Blue, openAt: 2) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            CollectionAssert.AreEqual(new[] { new CountPop(CountKind.Gate, 1) }, changes.CountPops);
            Assert.IsEmpty(changes.OpenedGates);
        }

        [Test]
        public void Between_ClearThatOpensAShutter_ReportsTheShutterAndNoSpawnsUnderIt()
        {
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0)) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            CollectionAssert.AreEqual(new[] { 0 }, changes.OpenedShutters);
            Assert.IsEmpty(changes.Spawns);
        }

        [Test]
        public void Between_ClearThatOpensAShutterOverAnElevatorsHiddenWave_RevealsTheWaveRatherThanSpawningIt()
        {
            // D42: the empty region under the closed shutter received its wave
            // at level start, hidden. The block is a spawner's, and newly
            // shown — but the shutter's lift reveals it; it did not spawn now.
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0)) },
                elevators: new[]
                {
                    Elevator(1, new Coord(2, 0), new Coord(2, 0),
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) })
                });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            Assert.IsTrue(before.Alive[1], "the wave arrived hidden at level start");
            CollectionAssert.AreEqual(new[] { 0 }, changes.OpenedShutters);
            Assert.IsEmpty(changes.Spawns);
        }

        [Test]
        public void Between_ClearThatCompletesALock_ReportsTheUnlock()
        {
            var ctx = Ctx(3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 7),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }, lockId: 7, requiredKeys: 1)
                },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            CollectionAssert.AreEqual(new[] { 1 }, changes.UnlockedBlocks);
            CollectionAssert.DoesNotContain(changes.CountPops, new CountPop(CountKind.Padlock, 1));
        }

        [Test]
        public void Between_ClearThatConsumesAKeyWithoutCompletingTheLock_ReportsAPadlockCountPop()
        {
            var ctx = Ctx(4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 7),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }, lockId: 7, requiredKeys: 2),
                    Block(3, new Coord(3, 0), colors: new[] { BlockColor.Green }, keyTarget: 7)
                },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            CollectionAssert.AreEqual(new[] { new CountPop(CountKind.Padlock, 1) }, changes.CountPops);
            Assert.IsEmpty(changes.UnlockedBlocks);
        }

        [Test]
        public void Between_ClearThatMakesAGeneratorSpawn_ReportsTheBlockWithThatGeneratorAsSource()
        {
            // The red block holds the generator's cell; clearing it lets the
            // first of two queued blocks (slot 1) in, and the badge counts down.
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                generators: new[]
                {
                    Spawner(1, BoardEdge.Left, 0, 1,
                        Spawned(colors: new[] { BlockColor.Blue }), Spawned(colors: new[] { BlockColor.Green }))
                });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            CollectionAssert.AreEqual(new[] { new BlockSpawn(1, SpawnerKind.Generator, 0) }, changes.Spawns);
            CollectionAssert.AreEqual(new[] { new CountPop(CountKind.Generator, 0) }, changes.CountPops);
        }

        [Test]
        public void Between_ClearThatBringsAnElevatorsNextWave_ReportsEveryWaveBlockWithThatElevatorAsSource()
        {
            // Wave 0 (slot 0) is one red 1x2 over a red gate as wide as the
            // region; clearing it empties the region and wave 1's two blocks
            // (slots 1 and 2) arrive.
            var ctx = Ctx(2, 1,
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 2, BlockColor.Red) },
                elevators: new[]
                {
                    Elevator(1, new Coord(0, 0), new Coord(1, 0),
                        new[] { Spawned(cells: Horizontal1x2, regionOrigin: new Coord(0, 0)) },
                        new[]
                        {
                            Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)),
                            Spawned(colors: new[] { BlockColor.Green }, regionOrigin: new Coord(1, 0))
                        })
                });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            CollectionAssert.AreEqual(
                new[] { new BlockSpawn(1, SpawnerKind.Elevator, 0), new BlockSpawn(2, SpawnerKind.Elevator, 0) },
                changes.Spawns);
        }

        [Test]
        public void Between_DestroyedArrival_ReportsClearsExactlyAsResolutionDiff()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(2, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));
            var expected = ResolutionDiff.Between(ctx, before, after, move, push: null);

            var changes = MoveChanges.Between(ctx, before, after, move, push: null);

            Assert.AreEqual(1, expected.Count, "the fixture clears the block");
            CollectionAssert.AreEqual(expected, changes.Clears);
        }

        [Test]
        public void Between_LayeredPush_ReportsClearsExactlyAsResolutionDiff()
        {
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));
            var expected = ResolutionDiff.Between(ctx, before, after, move, Direction.Left);

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Left);

            Assert.AreEqual(1, expected.Count, "the fixture peels the block");
            CollectionAssert.AreEqual(expected, changes.Clears);
        }

        [TestCase(Direction.Left)]
        [TestCase(Direction.Down)]
        public void Between_PushInACorner_ReportsClearsExactlyAsResolutionDiff(Direction push)
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 2), colors: new[] { BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Bottom, 0, 1, BlockColor.Red) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));
            var expected = ResolutionDiff.Between(ctx, before, after, move, push);

            var changes = MoveChanges.Between(ctx, before, after, move, push);

            Assert.AreEqual(1, expected.Count, "the fixture clears the block");
            CollectionAssert.AreEqual(expected, changes.Clears);
        }

        [Test]
        public void WithoutClears_SameMove_ReportsTheSameOtherChangesAndNoClears()
        {
            var ctx = RedBlockOverItsGateBeside(Block(2, new Coord(2, 0), unfreezeAt: 1));
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));
            var full = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            var withoutClears = MoveChanges.WithoutClears(ctx, before, after);

            Assert.IsEmpty(withoutClears.Clears);
            Assert.IsNotEmpty(full.Clears, "the fixture clears the block");
            CollectionAssert.AreEqual(full.ThawedBlocks, withoutClears.ThawedBlocks);
            CollectionAssert.AreEqual(full.CountPops, withoutClears.CountPops);
            CollectionAssert.AreEqual(full.Spawns, withoutClears.Spawns);
        }

        [Test]
        public void Between_GateClear_IsNotInPlaceAndNamesItsGate()
        {
            var ctx = RedBlockOverItsGateBeside(Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }));
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var changes = MoveChanges.Between(ctx, before, after, move, Direction.Down);

            Assert.AreEqual(1, changes.Clears.Count);
            Assert.IsFalse(changes.Clears[0].IsInPlace);
            Assert.AreEqual(0, changes.Clears[0].GateIndex);
            Assert.AreEqual(BoardEdge.Bottom, changes.Clears[0].GateEdge);
        }

        // ----- Jokers (Module 34) --------------------------------------------

        [Test]
        public void ForJoker_RocketDestroysABlock_ReportsOneInPlaceClearWithNoExposedColour()
        {
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var before = BoardState.CreateInitial(ctx);
            Assert.IsTrue(new MoveResolver().TryClearBlock(ctx, before, 0, out var after, out _));

            var changes = MoveChanges.ForJoker(ctx, before, after);

            Assert.AreEqual(1, changes.Clears.Count);
            var clear = changes.Clears[0];
            Assert.IsTrue(clear.IsInPlace);
            Assert.AreEqual(0, clear.BlockIndex);
            Assert.AreEqual(BlockColor.Red, clear.RemovedColor);
            Assert.IsNull(clear.ExposedColor);
            Assert.IsTrue(clear.IsDestroyed);
        }

        [Test]
        public void ForJoker_InPlaceClear_HasNoGate()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }) });
            var before = BoardState.CreateInitial(ctx);
            Assert.IsTrue(new MoveResolver().TryClearBlock(ctx, before, 0, out var after, out _));
            var clear = MoveChanges.ForJoker(ctx, before, after).Clears[0];

            Assert.Throws<System.InvalidOperationException>(() => _ = clear.GateIndex);
            Assert.Throws<System.InvalidOperationException>(() => _ = clear.GateEdge);
        }

        [Test]
        public void ForJoker_RocketOnALayeredBlock_ReportsOneInPlaceClearWithTheExposedColour()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }) });
            var before = BoardState.CreateInitial(ctx);
            Assert.IsTrue(new MoveResolver().TryClearBlock(ctx, before, 0, out var after, out _));

            var changes = MoveChanges.ForJoker(ctx, before, after);

            Assert.AreEqual(1, changes.Clears.Count);
            Assert.IsTrue(changes.Clears[0].IsInPlace);
            Assert.AreEqual(BlockColor.Red, changes.Clears[0].RemovedColor);
            Assert.AreEqual(BlockColor.Blue, changes.Clears[0].ExposedColor);
            Assert.IsTrue(after.Alive[0]);
        }

        /// <summary>
        /// A frozen layered block can only lose a colour to a joker; it stays
        /// alive and frozen. The clear carries the exposed colour for the
        /// rules, and the board must not show it (M3, D13): that is the view's
        /// part, checked by hand.
        /// </summary>
        [Test]
        public void ForJoker_RocketOnAFrozenLayeredBlock_ReportsOneInPlaceClearAndTheBlockStaysFrozen()
        {
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }, unfreezeAt: UnreachedThreshold) });
            var before = BoardState.CreateInitial(ctx);
            Assert.IsTrue(new MoveResolver().TryClearBlock(ctx, before, 0, out var after, out _));

            var changes = MoveChanges.ForJoker(ctx, before, after);

            Assert.AreEqual(1, changes.Clears.Count);
            Assert.IsTrue(changes.Clears[0].IsInPlace);
            Assert.AreEqual(BlockColor.Blue, changes.Clears[0].ExposedColor);
            Assert.IsTrue(after.Alive[0]);
            Assert.IsFalse(after.Unfrozen[0]);
            Assert.IsEmpty(changes.ThawedBlocks);
        }

        /// <summary>
        /// 3x2: red blocks at (0, 0) and (1, 0), and a red block at (2, 1)
        /// under a shutter no sweep here opens.
        /// </summary>
        [Test]
        public void ForJoker_Broom_ReportsOneInPlaceClearPerSweptBlockAndNoneUnderAClosedShutter()
        {
            var ctx = Ctx(3, 2,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0)), Block(3, new Coord(2, 1)) },
                shutters: new[] { Shutter(1, new Coord(2, 1), new Coord(2, 1), UnreachedThreshold) });
            var before = BoardState.CreateInitial(ctx);
            Assert.IsTrue(new MoveResolver().TrySweepColor(ctx, before, BlockColor.Red, out var after, out _));

            var changes = MoveChanges.ForJoker(ctx, before, after);

            CollectionAssert.AreEqual(new[] { 0, 1 }, BlockIndices(changes.Clears));
            Assert.That(changes.Clears, Has.All.Property(nameof(ClearedBlock.IsInPlace)).True);
            Assert.IsTrue(after.Alive[2], "the shuttered block is not swept");
        }

        /// <summary>
        /// Module 33's chain board
        /// (<c>MoveResolverTests.TrySweepColor_CrossesSeveralThresholdsAndBringsAnElevatorsNextWaveInOneResolution</c>):
        /// 6x3, a red block at (0,0), a blue block at (1,0) frozen until
        /// <see cref="ChainThreshold"/>, a red block under a global shutter at
        /// (0,2), a green bottom gate at x = 3, and an elevator over
        /// (4,2)-(5,2) whose first wave is two red 1x1s and whose second is a
        /// blue 1x2. The sweep's three reds open everything at once.
        /// </summary>
        [Test]
        public void ForJoker_BroomThatCrossesSeveralThresholds_ReportsTheChainAsForAMove()
        {
            var ctx = Ctx(
                6, 3,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }, unfreezeAt: ChainThreshold),
                    Block(3, new Coord(0, 2))
                },
                gates: new[] { Gate(1, BoardEdge.Bottom, 3, 1, BlockColor.Green, openAt: ChainThreshold) },
                shutters: new[] { Shutter(1, new Coord(0, 2), new Coord(0, 2), ChainThreshold) },
                elevators: new[]
                {
                    Elevator(1, new Coord(4, 2), new Coord(5, 2),
                        new[]
                        {
                            Spawned(regionOrigin: new Coord(0, 0)),
                            Spawned(regionOrigin: new Coord(1, 0))
                        },
                        new[]
                        {
                            Spawned(colors: new[] { BlockColor.Blue }, cells: Horizontal1x2, regionOrigin: new Coord(0, 0))
                        })
                });
            var before = BoardState.CreateInitial(ctx);
            var firstWave = ctx.ElevatorWaveFirstSlot(0, 0);
            var secondWave = ctx.ElevatorWaveFirstSlot(0, 1);
            Assert.IsTrue(new MoveResolver().TrySweepColor(ctx, before, BlockColor.Red, out var after, out _));

            var changes = MoveChanges.ForJoker(ctx, before, after);

            CollectionAssert.AreEqual(new[] { 0, firstWave, firstWave + 1 }, BlockIndices(changes.Clears));
            CollectionAssert.AreEqual(new[] { 1 }, changes.ThawedBlocks);
            CollectionAssert.AreEqual(new[] { 0 }, changes.OpenedGates);
            CollectionAssert.AreEqual(new[] { 0 }, changes.OpenedShutters);
            CollectionAssert.AreEqual(new[] { new BlockSpawn(secondWave, SpawnerKind.Elevator, 0) }, changes.Spawns);
            Assert.IsTrue(after.CanBeTargeted(ctx, 2), "the opened shutter shows its block");
            CollectionAssert.DoesNotContain(BlockIndices(changes.Spawns), 2, "revealed, not spawned");
        }

        private static List<int> BlockIndices(IReadOnlyList<ClearedBlock> clears)
        {
            var indices = new List<int>();
            for (var i = 0; i < clears.Count; i++)
            {
                indices.Add(clears[i].BlockIndex);
            }

            return indices;
        }

        private static List<int> BlockIndices(IReadOnlyList<BlockSpawn> spawns)
        {
            var indices = new List<int>();
            for (var i = 0; i < spawns.Count; i++)
            {
                indices.Add(spawns[i].BlockIndex);
            }

            return indices;
        }

        /// <summary>
        /// 3x1: a red block (slot 0) at (0, 0) over a red bottom gate, and
        /// <paramref name="other"/> as slot 1.
        /// </summary>
        private static LevelContext RedBlockOverItsGateBeside(BlockDefinition other) =>
            Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0)), other },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) });
    }
}
