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
