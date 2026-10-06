using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 11's <see cref="VisibilityLayer"/> (D13): remaining counts
    /// are <c>threshold − current count</c> and vanish once met; a frozen block
    /// hides its colours and a closed shutter hides its blocks, while
    /// <see cref="BoardState"/> keeps the whole truth. Module 19: a shown
    /// block reports its time bonus.
    /// </summary>
    public class VisibilityLayerTests
    {
        private const int FrozenSlot = 1;
        private const int GlobalShutteredSlot = 2;
        private const int RedShutteredSlot = 3;
        private const int ThresholdGateIndex = 1;
        private const int GlobalShutterIndex = 0;
        private const int RedShutterIndex = 1;

        private static readonly BlockColor[] ThreeLayers = { BlockColor.Blue, BlockColor.Green, BlockColor.Yellow };

        /// <summary>
        /// A 4x4 board whose slot 0 is a red block pre-aligned with a red gate,
        /// so one push gives one clear (total 1, red 1). Slot 1 is a three-layer
        /// block frozen until <paramref name="unfreezeAt"/>; a blue top gate
        /// opens at <paramref name="gateOpensAt"/>; slot 2 sits under a global
        /// shutter and slot 3 under a red-bound one.
        /// </summary>
        private static LevelContext CountsBoard(int unfreezeAt, int gateOpensAt, int globalShutterAt, int redShutterAt) =>
            Ctx(
                4, 4,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(3, 3), colors: ThreeLayers, unfreezeAt: unfreezeAt),
                    Block(3, new Coord(3, 0), colors: new[] { BlockColor.Green }),
                    Block(4, new Coord(2, 0), colors: new[] { BlockColor.Yellow })
                },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 1, 1, BlockColor.Blue, openAt: gateOpensAt)
                },
                shutters: new[]
                {
                    Shutter(1, new Coord(3, 0), new Coord(3, 0), threshold: globalShutterAt),
                    Shutter(2, new Coord(2, 0), new Coord(2, 0), threshold: redShutterAt, requiredColor: BlockColor.Red)
                });

        private static BoardState AfterOneRedClear(LevelContext ctx)
        {
            var initial = BoardState.CreateInitial(ctx);
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, initial, new Move(0, new Coord(0, 0)), out var after, out _));
            return after;
        }

        [Test]
        public void Counts_AfterOneClear_AreThresholdMinusCurrentCount()
        {
            var ctx = CountsBoard(unfreezeAt: 3, gateOpensAt: 4, globalShutterAt: 2, redShutterAt: 3);
            var state = AfterOneRedClear(ctx);
            var visibility = new VisibilityLayer(ctx);

            var frozen = visibility.Block(state, FrozenSlot);
            var gate = visibility.Gate(state, ThresholdGateIndex);
            var globalShutter = visibility.Shutter(state, GlobalShutterIndex);
            var redShutter = visibility.Shutter(state, RedShutterIndex);

            Assert.AreEqual(2, frozen.FrozenRemaining);
            Assert.IsFalse(gate.IsOpen);
            Assert.AreEqual(3, gate.OpensInClears);
            Assert.IsNull(gate.Color);
            Assert.AreEqual(1, globalShutter.OpensInClears);
            Assert.IsNull(globalShutter.CountsColor);
            Assert.AreEqual(2, redShutter.OpensInClears);
            Assert.AreEqual(BlockColor.Red, redShutter.CountsColor);
        }

        [Test]
        public void Counts_WhenEveryThresholdIsMet_ShowNothing()
        {
            var ctx = CountsBoard(unfreezeAt: 1, gateOpensAt: 1, globalShutterAt: 1, redShutterAt: 1);
            var state = AfterOneRedClear(ctx);
            var visibility = new VisibilityLayer(ctx);

            var unfrozen = visibility.Block(state, FrozenSlot);
            var gate = visibility.Gate(state, ThresholdGateIndex);
            var globalShutter = visibility.Shutter(state, GlobalShutterIndex);
            var redShutter = visibility.Shutter(state, RedShutterIndex);

            Assert.IsFalse(unfrozen.IsFrozen);
            Assert.AreEqual(0, unfrozen.FrozenRemaining);
            Assert.AreEqual(BlockColor.Blue, unfrozen.OuterColor);
            Assert.IsTrue(gate.IsOpen);
            Assert.AreEqual(0, gate.OpensInClears);
            Assert.AreEqual(BlockColor.Blue, gate.Color);
            Assert.IsFalse(globalShutter.IsClosed);
            Assert.AreEqual(0, globalShutter.OpensInClears);
            Assert.IsFalse(redShutter.IsClosed);
            Assert.AreEqual(0, redShutter.OpensInClears);
        }

        [Test]
        public void Block_Frozen_ShowsShapeButHidesEveryColourAndLeavesStateUnchanged()
        {
            var ctx = CountsBoard(unfreezeAt: 3, gateOpensAt: 4, globalShutterAt: 2, redShutterAt: 3);
            var state = BoardState.CreateInitial(ctx);
            var visibility = new VisibilityLayer(ctx);

            var visual = visibility.Block(state, FrozenSlot);

            Assert.IsTrue(visual.IsShown);
            Assert.IsTrue(visual.IsFrozen);
            Assert.IsNull(visual.OuterColor);
            Assert.IsNull(visual.BeneathColor);
            Assert.IsNull(visual.LayerNumeral);
            Assert.AreEqual(BoardState.CreateInitial(ctx), state);
            Assert.AreEqual(BlockColor.Blue, state.CurrentColorOf(ctx, FrozenSlot), "the truth keeps the colour");
        }

        [Test]
        public void Block_FrozenAndLayered_ReportsNoColourBeneathAndNoLayerCount()
        {
            // Module 20: the board draws an inner shape and a layer badge from
            // these two, so a frozen layered block is ice and nothing else.
            // One clear thaws the same block, and both then show.
            var ctx = CountsBoard(unfreezeAt: 1, gateOpensAt: 4, globalShutterAt: 2, redShutterAt: 3);
            var frozenState = BoardState.CreateInitial(ctx);
            var thawedState = AfterOneRedClear(ctx);
            var visibility = new VisibilityLayer(ctx);

            var frozen = visibility.Block(frozenState, FrozenSlot);
            var thawed = visibility.Block(thawedState, FrozenSlot);

            Assert.IsTrue(frozen.IsFrozen);
            Assert.IsNull(frozen.BeneathColor);
            Assert.IsNull(frozen.LayerNumeral);
            Assert.IsFalse(thawed.IsFrozen);
            Assert.AreEqual(ThreeLayers[1], thawed.BeneathColor);
            Assert.AreEqual(ThreeLayers.Length, thawed.LayerNumeral);
        }

        [Test]
        public void Block_UnderClosedShutter_IsNotShownAndLeavesStateUnchanged()
        {
            var ctx = CountsBoard(unfreezeAt: 3, gateOpensAt: 4, globalShutterAt: 2, redShutterAt: 3);
            var state = BoardState.CreateInitial(ctx);
            var visibility = new VisibilityLayer(ctx);

            var global = visibility.Block(state, GlobalShutteredSlot);
            var red = visibility.Block(state, RedShutteredSlot);

            Assert.IsFalse(global.IsShown);
            Assert.IsFalse(red.IsShown);
            Assert.AreEqual(BoardState.CreateInitial(ctx), state);
            Assert.IsTrue(state.Alive[GlobalShutteredSlot], "the truth keeps the block");
            Assert.IsTrue(state.Alive[RedShutteredSlot], "the truth keeps the block");
        }

        [Test]
        public void Block_LayeredAndUnfrozen_ShowsOuterAndBeneathWithANumeralOnlyAboveTwo()
        {
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: ThreeLayers),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Red, BlockColor.Blue })
                },
                new[] { Gate(1, BoardEdge.Top, 1, 1, BlockColor.Green) });
            var state = BoardState.CreateInitial(ctx);
            var visibility = new VisibilityLayer(ctx);

            var three = visibility.Block(state, 0);
            var two = visibility.Block(state, 1);

            Assert.AreEqual(BlockColor.Blue, three.OuterColor);
            Assert.AreEqual(BlockColor.Green, three.BeneathColor);
            Assert.AreEqual(3, three.LayerNumeral);
            Assert.AreEqual(BlockColor.Red, two.OuterColor);
            Assert.AreEqual(BlockColor.Blue, two.BeneathColor);
            Assert.IsNull(two.LayerNumeral);
        }

        /// <summary>
        /// 4x4: slot 0 is a blue-over-green block locked by lock 5, which needs
        /// both keys; slots 1 and 2 are red key carriers, each pre-aligned with a
        /// red left gate; slot 3 carries nothing.
        /// </summary>
        private static LevelContext LockBoard() =>
            Ctx(
                4, 4,
                new[]
                {
                    Block(1, new Coord(2, 2), colors: new[] { BlockColor.Blue, BlockColor.Green }, lockId: 5, requiredKeys: 2),
                    Block(2, new Coord(0, 0), keyTarget: 5),
                    Block(3, new Coord(0, 3), keyTarget: 5),
                    Block(4, new Coord(3, 0), colors: new[] { BlockColor.Yellow })
                },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Left, 3, 1, BlockColor.Red)
                });

        [Test]
        public void Block_LockedAfterOneOfTwoKeys_LockColorIsItsOuterColourAtLevelStartWithOneKeyStillRequired()
        {
            var ctx = LockBoard();
            var initial = BoardState.CreateInitial(ctx);
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, initial, new Move(1, new Coord(0, 0)), out var state, out _));
            var visibility = new VisibilityLayer(ctx);

            var locked = visibility.Block(state, 0);
            var plain = visibility.Block(state, 3);

            Assert.IsTrue(locked.IsLocked);
            Assert.AreEqual(BlockColor.Blue, locked.LockColor);
            Assert.AreEqual(1, locked.KeysStillRequired);
            Assert.IsFalse(plain.IsLocked);
            Assert.IsNull(plain.LockColor);
        }

        [Test]
        public void Block_AfterItsLastKey_IsNoLongerLocked()
        {
            var ctx = LockBoard();
            var resolver = new MoveResolver();
            Assert.IsTrue(resolver.TryApplyMove(ctx, BoardState.CreateInitial(ctx), new Move(1, new Coord(0, 0)), out var first, out _));
            Assert.IsTrue(resolver.TryApplyMove(ctx, first, new Move(2, new Coord(0, 3)), out var second, out _));
            var visibility = new VisibilityLayer(ctx);

            var opened = visibility.Block(second, 0);

            Assert.IsFalse(opened.IsLocked);
            Assert.IsNull(opened.LockColor);
            Assert.AreEqual(0, opened.KeysStillRequired);
        }

        [Test]
        public void Block_KeyCarrier_IsMarkedWithItsLocksColourNotItsOwn_AndABlockWithoutAKeyHasNoMark()
        {
            // D47: the red carriers' keys are marked blue, the lock's colour.
            var ctx = LockBoard();
            var state = BoardState.CreateInitial(ctx);
            var visibility = new VisibilityLayer(ctx);

            var key = visibility.Block(state, 1);
            var plain = visibility.Block(state, 3);
            var locked = visibility.Block(state, 0);

            Assert.AreEqual(BlockColor.Red, key.OuterColor);
            Assert.AreEqual(BlockColor.Blue, key.KeyMarkColor);
            Assert.IsNull(plain.KeyMarkColor);
            Assert.IsNull(locked.KeyMarkColor);
        }

        [Test]
        public void Block_WithATimeBonus_ReportsItsSeconds()
        {
            const int bonus = 7;
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0), timeBonusSeconds: bonus) });
            var state = BoardState.CreateInitial(ctx);
            var visibility = new VisibilityLayer(ctx);

            var visual = visibility.Block(state, 0);

            Assert.IsTrue(visual.IsShown);
            Assert.AreEqual(bonus, visual.TimeBonusSeconds);
        }

        [Test]
        public void Block_WithoutATimeBonus_ReportsNone()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) });
            var state = BoardState.CreateInitial(ctx);
            var visibility = new VisibilityLayer(ctx);

            var visual = visibility.Block(state, 0);

            Assert.IsTrue(visual.IsShown);
            Assert.AreEqual(0, visual.TimeBonusSeconds);
        }

        /// <summary>
        /// 2x1: a left-edge generator queues a red 1x1, which spawns at level
        /// start over the red bottom gate, then a blue horizontal 1x2 frozen
        /// until <paramref name="unfreezeAt"/> clears.
        /// </summary>
        private static LevelContext GeneratorBoard(int? unfreezeAt) =>
            Ctx(
                2, 1,
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                generators: new[]
                {
                    Spawner(1, BoardEdge.Left, 0, 1,
                        Spawned(),
                        Spawned(
                            colors: new[] { BlockColor.Blue },
                            cells: new[] { new Coord(0, 0), new Coord(1, 0) },
                            unfreezeAt: unfreezeAt))
                });

        [Test]
        public void Generator_WithABlockQueued_ReportsTheCountAndTheNextBlocksCellsAndColour_NothingOnceExhausted()
        {
            var ctx = GeneratorBoard(unfreezeAt: null);
            var initial = BoardState.CreateInitial(ctx);
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, initial, new Move(0, new Coord(0, 0)), out var exhausted, out _));
            var visibility = new VisibilityLayer(ctx);

            var before = visibility.Generator(initial, 0);
            var after = visibility.Generator(exhausted, 0);

            Assert.IsTrue(before.IsShown);
            Assert.AreEqual(1, before.Queued, "the red block spawned at level start; the blue one waits");
            CollectionAssert.AreEqual(new[] { new Coord(0, 0), new Coord(1, 0) }, before.NextCells);
            Assert.AreEqual(BlockColor.Blue, before.NextColor);
            Assert.IsFalse(before.IsNextFrozen);
            Assert.IsFalse(after.IsShown);
            Assert.AreEqual(0, after.Queued);
            Assert.IsNull(after.NextCells);
        }

        [Test]
        public void Generator_NextBlockThatWouldSpawnFrozenAtTheCurrentCounts_HidesItsColour()
        {
            var ctx = GeneratorBoard(unfreezeAt: 1);
            var state = BoardState.CreateInitial(ctx);
            var visibility = new VisibilityLayer(ctx);

            var generator = visibility.Generator(state, 0);

            Assert.IsTrue(generator.IsNextFrozen);
            Assert.IsNull(generator.NextColor);
            Assert.AreEqual(2, generator.NextCells.Count, "its shape still shows");
        }

        [Test]
        public void ElevatorPresent_WhileWavesRemainOrAWaveIsOnTheBoard_TrueAndFalseAfterTheFinalWaveIsCleared()
        {
            // 2x1, region (1,0) over a blue gate: two one-block blue waves.
            var ctx = Ctx(
                2, 1,
                gates: new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Blue) },
                elevators: new[]
                {
                    Elevator(1, new Coord(1, 0), new Coord(1, 0),
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) },
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) })
                });
            var resolver = new MoveResolver();
            var initial = BoardState.CreateInitial(ctx);
            Assert.IsTrue(resolver.TryApplyMove(ctx, initial, new Move(0, new Coord(1, 0)), out var lastWaveOnBoard, out _));
            Assert.IsTrue(resolver.TryApplyMove(ctx, lastWaveOnBoard, new Move(1, new Coord(1, 0)), out var cleared, out _));
            var visibility = new VisibilityLayer(ctx);

            var withWavesToCome = visibility.ElevatorPresent(initial, 0);
            var withTheLastWaveOnTheBoard = visibility.ElevatorPresent(lastWaveOnBoard, 0);
            var afterTheLastWave = visibility.ElevatorPresent(cleared, 0);

            Assert.IsTrue(withWavesToCome);
            Assert.IsTrue(withTheLastWaveOnTheBoard);
            Assert.IsFalse(afterTheLastWave);
        }
    }
}
