using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Module 10's part of <see cref="MoveResolverTests"/>: generators (M6) and
    /// elevators (M9) spawning inside the fixpoint loop, closed shutters not
    /// stopping them (D42), keys for a lock whose block has not spawned yet
    /// (D42), spawning never clearing (D25), the chain ARCHITECTURE's core
    /// concept 3 describes, generator/elevator contention, and the tightest
    /// case of the resolution-pass bound. Level start itself is covered in
    /// <c>BoardStateTests</c>, since it is <see cref="BoardState.CreateInitial(LevelContext)"/>'s.
    /// </summary>
    /// <remarks>
    /// Grid convention as elsewhere: y = 0 is the bottom row, so in a one-row
    /// board every block is flush against the bottom edge and a zero-distance
    /// move pushes it into the bottom gate under it.
    /// </remarks>
    public partial class MoveResolverTests
    {
        private static readonly Coord[] SpawnCellsHorizontal1x2 = { new Coord(0, 0), new Coord(1, 0) };

        // ----- Generator trigger (M6) ----------------------------------------

        /// <summary>
        /// 3x1: a red block (index 0) on the cell a left-edge generator spawns
        /// into, over a red gate; the generator's one queued block (index 1) is
        /// blue. Nothing else.
        /// </summary>
        private static LevelContext GeneratorBehindARedBlock() =>
            Ctx(
                3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned(colors: new[] { BlockColor.Blue })) });

        [Test]
        public void CheckSpawnTriggers_GeneratorCellOccupied_WaitsAndSpawnsInTheResolutionThatFreesIt()
        {
            var ctx = GeneratorBehindARedBlock();
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsFalse(state.Alive[1], "the red block holds the spawn back");
            Assert.AreEqual(0, state.GeneratorIndex[0]);
            Assert.IsTrue(moved);
            Assert.IsFalse(result.Alive[0]);
            Assert.IsTrue(result.Alive[1]);
            Assert.AreEqual(new Coord(0, 0), result.Origins[1]);
            Assert.AreEqual(1, result.GeneratorIndex[0]);
        }

        [Test]
        public void CheckSpawnTriggers_MoveVacatesTheGeneratorsCellWithoutClearing_Spawns()
        {
            var ctx = GeneratorBehindARedBlock();

            Resolver().TryApplyMove(ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(1, 0)), out var result, out _);

            Assert.AreEqual(0, result.TotalClearCount, "nothing was cleared");
            Assert.IsTrue(result.Alive[1]);
            Assert.AreEqual(new Coord(0, 0), result.Origins[1]);
            Assert.AreEqual(1, result.GeneratorIndex[0]);
        }

        /// <summary>
        /// 2x1: a left-edge generator queues two red blocks (indices 0 and 1)
        /// onto a cell over a red gate. Its cell is empty at level start.
        /// </summary>
        private static LevelContext GeneratorOntoItsOwnGate() =>
            Ctx(
                2, 1,
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned(), Spawned()) });

        [Test]
        public void CheckSpawnTriggers_BlockSpawnsFlushAgainstACompatibleGate_IsNotClearedAndAPushClearsIt()
        {
            // D25: a spawned block did not arrive by a move. It waits, at level
            // start and mid-resolution alike, and a zero-distance push clears it.
            var ctx = GeneratorOntoItsOwnGate();
            var initial = BoardState.CreateInitial(ctx);

            var pushed = Resolver().TryApplyMove(ctx, initial, new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsTrue(initial.Alive[0], "spawned at level start");
            Assert.AreEqual(0, initial.TotalClearCount, "and not cleared there");
            Assert.IsTrue(pushed);
            Assert.IsFalse(result.Alive[0], "the push clears it");
            Assert.IsTrue(result.Alive[1], "the second block spawns onto the freed cell");
            Assert.AreEqual(0, result.ClearedColors[1], "and is not cleared either");
            Assert.AreEqual(1, result.TotalClearCount);
        }

        [Test]
        public void CheckSpawnTriggers_AfterTheLastQueuedBlock_NeverSpawnsAgainAndTheLevelSolves()
        {
            var ctx = GeneratorOntoItsOwnGate();
            var resolver = Resolver();
            resolver.TryApplyMove(ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var first, out _);

            resolver.TryApplyMove(ctx, first, new Move(1, new Coord(0, 0)), out var second, out _);

            Assert.AreEqual(2, second.GeneratorIndex[0]);
            Assert.IsFalse(second.Alive[0] || second.Alive[1]);
            Assert.IsTrue(second.IsSolved(ctx));
        }

        [TestCase(1, true)]
        [TestCase(2, false)]
        public void CheckSpawnTriggers_SpawnedBlocksFrozenFlag_ReadsTheRunningClearCount(
            int unfreezeAt, bool expectUnfrozen)
        {
            // The source state has no clears; the push that frees the cell makes
            // one. A spawned block's freeze threshold is judged against that
            // running count, through the shared predicate (Module 06).
            var ctx = Ctx(
                2, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                generators: new[]
                {
                    Spawner(1, BoardEdge.Left, 0, 1, Spawned(colors: new[] { BlockColor.Blue }, unfreezeAt: unfreezeAt))
                });

            Resolver().TryApplyMove(ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsTrue(result.Alive[1]);
            Assert.AreEqual(expectUnfrozen, result.Unfrozen[1]);
        }

        // ----- Elevator trigger (M9) -----------------------------------------

        [Test]
        public void CheckSpawnTriggers_TopLevelBlockInTheRegion_HoldsTheFirstWaveUntilItLeaves()
        {
            // No gates at all: the wave arrives on a move that clears nothing.
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(1, 0)) },
                elevators: new[]
                {
                    Elevator(1, new Coord(1, 0), new Coord(1, 0),
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) })
                });
            var initial = BoardState.CreateInitial(ctx);

            Resolver().TryApplyMove(ctx, initial, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.AreEqual(0, initial.ElevatorWaveIndex[0]);
            Assert.IsFalse(initial.ElevatorWaveActive[0]);
            Assert.IsFalse(initial.Alive[1]);
            Assert.AreEqual(1, result.ElevatorWaveIndex[0]);
            Assert.IsTrue(result.ElevatorWaveActive[0]);
            Assert.IsTrue(result.Alive[1]);
            Assert.AreEqual(new Coord(1, 0), result.Origins[1]);
        }

        [Test]
        public void CheckSpawnTriggers_ResolutionThatEmptiesTheRegion_PlacesTheNextWaveAtMinPlusRegionOrigin()
        {
            // 2x1, region (1,0). Wave 0 is a blue block over the blue gate; its
            // clear empties the region, and wave 1's green block arrives.
            var ctx = Ctx(
                2, 1,
                gates: new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Blue) },
                elevators: new[]
                {
                    Elevator(1, new Coord(1, 0), new Coord(1, 0),
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) },
                        new[] { Spawned(colors: new[] { BlockColor.Green }, regionOrigin: new Coord(0, 0)) })
                });
            var initial = BoardState.CreateInitial(ctx);

            Resolver().TryApplyMove(ctx, initial, new Move(0, new Coord(1, 0)), out var result, out _);

            Assert.IsFalse(initial.Alive[1], "the wave's own block holds the next wave back");
            Assert.AreEqual(2, result.ElevatorWaveIndex[0]);
            Assert.IsTrue(result.ElevatorWaveActive[0]);
            Assert.IsTrue(result.Alive[1]);
            Assert.AreEqual(new Coord(1, 0), result.Origins[1]);
        }

        [Test]
        public void ElevatorWaveActive_TrueWhileAWaveOccupiesTheRegion_FalseOnceItReadsEmpty()
        {
            var ctx = Ctx(
                2, 1,
                gates: new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Blue) },
                elevators: new[]
                {
                    Elevator(1, new Coord(1, 0), new Coord(1, 0),
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) },
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) })
                });
            var resolver = Resolver();
            var initial = BoardState.CreateInitial(ctx);
            resolver.TryApplyMove(ctx, initial, new Move(0, new Coord(1, 0)), out var afterFirst, out _);

            resolver.TryApplyMove(ctx, afterFirst, new Move(1, new Coord(1, 0)), out var afterLast, out _);

            Assert.IsTrue(initial.ElevatorWaveActive[0]);
            Assert.IsTrue(afterFirst.ElevatorWaveActive[0], "the second wave arrived as the first left");
            Assert.IsFalse(afterFirst.IsSolved(ctx), "a wave is still on the board");
            Assert.AreEqual(2, afterLast.ElevatorWaveIndex[0]);
            Assert.IsFalse(afterLast.ElevatorWaveActive[0], "the region read empty with no wave left");
            Assert.IsTrue(afterLast.IsSolved(ctx));
        }

        [Test]
        public void ElevatorWaveActive_ForeignBlockEntersBeforeTheRegionEmpties_StaysSetAndHoldsTheNextWave()
        {
            // 4x1, region (1,0)..(2,0) over a two-wide blue gate. Wave 0 is two
            // blue blocks (indices 1, 2), wave 1 one green 1x2 (index 3). The red
            // block (index 0) steps into the region between the two blue clears,
            // so the region never reads empty: the flag stays set until red
            // leaves, and only then does wave 1 arrive.
            var ctx = Ctx(
                4, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 1, 2, BlockColor.Blue) },
                elevators: new[]
                {
                    Elevator(1, new Coord(1, 0), new Coord(2, 0),
                        new[]
                        {
                            Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)),
                            Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(1, 0))
                        },
                        new[]
                        {
                            Spawned(colors: new[] { BlockColor.Green }, cells: SpawnCellsHorizontal1x2,
                                regionOrigin: new Coord(0, 0))
                        })
                });
            var resolver = Resolver();
            var initial = BoardState.CreateInitial(ctx);
            resolver.TryApplyMove(ctx, initial, new Move(1, new Coord(1, 0)), out var oneBlueLeft, out _);
            resolver.TryApplyMove(ctx, oneBlueLeft, new Move(0, new Coord(1, 0)), out var redInside, out _);

            resolver.TryApplyMove(ctx, redInside, new Move(2, new Coord(2, 0)), out var waveGone, out _);
            resolver.TryApplyMove(ctx, waveGone, new Move(0, new Coord(0, 0)), out var redLeft, out _);

            Assert.AreEqual(new Coord(2, 0), initial.Origins[2], "wave blocks land at Min + RegionOrigin");
            Assert.IsFalse(waveGone.Alive[1] || waveGone.Alive[2], "every wave-0 block is gone");
            Assert.IsTrue(waveGone.ElevatorWaveActive[0], "the region never read empty");
            Assert.AreEqual(1, waveGone.ElevatorWaveIndex[0]);
            Assert.IsFalse(waveGone.Alive[3]);
            Assert.AreEqual(2, redLeft.ElevatorWaveIndex[0]);
            Assert.IsTrue(redLeft.ElevatorWaveActive[0]);
            Assert.IsTrue(redLeft.Alive[3]);
            Assert.AreEqual(new Coord(1, 0), redLeft.Origins[3]);
        }

        [Test]
        public void TryApplyMove_LastWavesOnlyBlockSlidOutWithoutClearing_ClearsTheFlagWithinTheFixpointBound()
        {
            // The tightest level for MaxResolutionPasses: one colour and one
            // wave, so the bound is 2. The move clears nothing and spawns
            // nothing; its one changing pass only clears ElevatorWaveActive —
            // the pass charged to the wave placed at level start (see
            // MoveResolver.ResolveToFixpoint).
            var ctx = Ctx(
                2, 1,
                elevators: new[]
                {
                    Elevator(1, new Coord(0, 0), new Coord(0, 0), new[] { Spawned(regionOrigin: new Coord(0, 0)) })
                });
            var initial = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, initial, new Move(0, new Coord(1, 0)), out var result, out _);

            Assert.AreEqual(2, ctx.MaxResolutionPasses);
            Assert.IsTrue(initial.ElevatorWaveActive[0]);
            Assert.IsTrue(moved);
            Assert.AreEqual(0, result.TotalClearCount);
            Assert.AreEqual(1, result.ElevatorWaveIndex[0]);
            Assert.IsFalse(result.ElevatorWaveActive[0]);
            Assert.IsTrue(result.Alive[0], "the block left the region; it was not cleared");
        }

        // ----- Generator and elevator contending for cells ---------------------

        [Test]
        public void CheckSpawnTriggers_GeneratorBlockInAnElevatorRegion_HoldsTheWaveBack()
        {
            // Both target (0,0) at level start; generators are checked first, so
            // the generator's red block (index 0) takes it and the blue wave
            // (index 1) waits until red is cleared.
            var ctx = Ctx(
                2, 1,
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned()) },
                elevators: new[]
                {
                    Elevator(1, new Coord(0, 0), new Coord(0, 0),
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) })
                });
            var initial = BoardState.CreateInitial(ctx);

            Resolver().TryApplyMove(ctx, initial, new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsTrue(initial.Alive[0]);
            Assert.AreEqual(0, initial.ElevatorWaveIndex[0]);
            Assert.IsFalse(initial.Alive[1]);
            Assert.IsFalse(result.Alive[0]);
            Assert.AreEqual(1, result.ElevatorWaveIndex[0]);
            Assert.IsTrue(result.Alive[1]);
            Assert.AreEqual(new Coord(0, 0), result.Origins[1]);
        }

        [Test]
        public void CheckSpawnTriggers_PlacedWaveInTheGeneratorsCells_HoldsTheGeneratorBack()
        {
            // 3x1. The generator's green 1x2 (index 1) needs (0,0) and (1,0);
            // the red block (index 0) on (1,0) holds it at level start, so the
            // elevator's blue wave (index 2) takes the empty region (0,0).
            // Moving red away is then not enough: the wave holds (0,0). Only
            // the blue clear frees the generator — and since generators are
            // checked first in that pass, its block fills the region before the
            // elevator looks, so the region never reads empty and the flag stays.
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(1, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Blue) },
                generators: new[]
                {
                    Spawner(1, BoardEdge.Left, 0, 1,
                        Spawned(colors: new[] { BlockColor.Green }, cells: SpawnCellsHorizontal1x2))
                },
                elevators: new[]
                {
                    Elevator(1, new Coord(0, 0), new Coord(0, 0),
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) })
                });
            var resolver = Resolver();
            var initial = BoardState.CreateInitial(ctx);
            resolver.TryApplyMove(ctx, initial, new Move(0, new Coord(2, 0)), out var redAway, out _);

            resolver.TryApplyMove(ctx, redAway, new Move(2, new Coord(0, 0)), out var blueCleared, out _);

            Assert.IsTrue(initial.Alive[2], "the wave took the empty region");
            Assert.AreEqual(0, initial.GeneratorIndex[0]);
            Assert.AreEqual(0, redAway.GeneratorIndex[0], "the placed wave holds the generator back");
            Assert.IsFalse(redAway.Alive[1]);
            Assert.AreEqual(1, blueCleared.GeneratorIndex[0]);
            Assert.IsTrue(blueCleared.Alive[1]);
            Assert.AreEqual(new Coord(0, 0), blueCleared.Origins[1]);
            Assert.IsTrue(blueCleared.ElevatorWaveActive[0], "the generator refilled the region before it read empty");
        }

        // ----- A closed shutter does not stop a spawn (D42) -----------------------

        [Test]
        public void CheckSpawnTriggers_EmptyRegionUnderAClosedShutter_ReceivesItsWaveHiddenAndUnreachable()
        {
            // 3x1. The region (2,0) is under a shutter that opens on the first
            // clear; the red block (index 0) supplies it. The blue wave block
            // (index 1) is over its blue gate.
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 2, 1, BlockColor.Blue)
                },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0), threshold: 1) },
                elevators: new[]
                {
                    Elevator(1, new Coord(2, 0), new Coord(2, 0),
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) })
                });
            var resolver = Resolver();
            var initial = BoardState.CreateInitial(ctx);

            var pushedHidden = resolver.TryApplyMove(ctx, initial, new Move(1, new Coord(2, 0)), out _, out _);
            var rocketedHidden = resolver.TryClearBlock(ctx, initial, 1, out _, out _);
            resolver.TryApplyMove(ctx, initial, new Move(0, new Coord(0, 0)), out var opened, out _);

            Assert.IsTrue(initial.Alive[1], "the wave arrived under the closed shutter");
            Assert.AreEqual(1, initial.ElevatorWaveIndex[0]);
            Assert.IsFalse(initial.CanMove(ctx, 1));
            Assert.IsFalse(initial.CanBeTargeted(ctx, 1));
            Assert.IsFalse(pushedHidden);
            Assert.IsFalse(rocketedHidden);
            Assert.IsTrue(opened.ShutterOpen[0]);
            Assert.IsTrue(opened.CanMove(ctx, 1));
        }

        [Test]
        public void CheckSpawnTriggers_GeneratorCellUnderAClosedShutter_StillSpawns()
        {
            var ctx = Ctx(
                2, 1,
                shutters: new[] { Shutter(1, new Coord(0, 0), new Coord(0, 0), threshold: 1) },
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned()) });

            var initial = BoardState.CreateInitial(ctx);

            Assert.IsTrue(initial.Alive[0]);
            Assert.AreEqual(1, initial.GeneratorIndex[0]);
            Assert.IsFalse(initial.CanMove(ctx, 0), "it spawned hidden, and stays unreachable");
        }

        // ----- Keys for a lock that has not spawned (D42) ----------------------

        /// <summary>
        /// 4x2. Row 0: red key block (index 0, a <paramref name="keyEffect"/>
        /// key for lock 1) over a red gate; yellow block (index 1) over a yellow
        /// gate; blue block (index 2) on (2,0). A right-edge generator queues one
        /// horizontal 1x2 (index 3) that owns lock 1 and needs (2,0) and (3,0),
        /// so the blue block holds it back until it steps up to (2,1), which
        /// clears nothing. With <paramref name="shuttered"/>, (3,0) is under a
        /// shutter that opens on the second clear — the yellow push after the
        /// red — so the owner spawns under it.
        /// </summary>
        private static LevelContext UnspawnedLockBoard(KeyEffect keyEffect, bool shuttered) =>
            Ctx(
                4, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: keyEffect),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Yellow }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Blue })
                },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 1, 1, BlockColor.Yellow)
                },
                shutters: shuttered
                    ? new[] { Shutter(1, new Coord(3, 0), new Coord(3, 0), threshold: 2) }
                    : null,
                generators: new[]
                {
                    Spawner(1, BoardEdge.Right, 0, 1,
                        Spawned(
                            colors: new[] { BlockColor.Green, BlockColor.Pink },
                            cells: SpawnCellsHorizontal1x2,
                            lockId: 1,
                            requiredKeys: 1))
                });

        private static readonly Move PushRedKey = new Move(0, new Coord(0, 0));
        private static readonly Move PushYellow = new Move(1, new Coord(1, 0));
        private static readonly Move StepBlueAside = new Move(2, new Coord(2, 1));

        [TestCase(KeyEffect.UnlockMovement)]
        [TestCase(KeyEffect.ClearOuterColor)]
        public void TryApplyMove_KeyCompletesALockNotYetSpawned_ConsumesTheKeyAndTheEffectWaitsInTheOwnersSlot(
            KeyEffect keyEffect)
        {
            var ctx = UnspawnedLockBoard(keyEffect, shuttered: false);

            Resolver().TryApplyMove(ctx, BoardState.CreateInitial(ctx), PushRedKey, out var result, out _);

            Assert.IsTrue(result.KeyConsumed[0], "the key is spent when its carrier dies");
            Assert.AreEqual(keyEffect, result.WaitingKeyEffect[3]);
            Assert.IsFalse(result.Alive[3]);
            Assert.AreEqual(BoardState.UnspawnedOrigin, result.Origins[3]);
            Assert.IsFalse(result.Unlocked[3]);
            Assert.AreEqual(0, result.ClearedColors[3]);
            Assert.AreEqual(1, result.TotalClearCount);
        }

        [Test]
        public void TryApplyMove_OwnerSpawnsUncoveredWithUnlockMovementWaiting_ArrivesUnlocked()
        {
            var ctx = UnspawnedLockBoard(KeyEffect.UnlockMovement, shuttered: false);
            var resolver = Resolver();
            resolver.TryApplyMove(ctx, BoardState.CreateInitial(ctx), PushRedKey, out var waiting, out _);

            resolver.TryApplyMove(ctx, waiting, StepBlueAside, out var result, out _);

            Assert.IsTrue(result.Alive[3]);
            Assert.AreEqual(new Coord(2, 0), result.Origins[3]);
            Assert.IsTrue(result.Unlocked[3]);
            Assert.IsNull(result.WaitingKeyEffect[3]);
            Assert.AreEqual(0, result.ClearedColors[3]);
            Assert.AreEqual(1, result.TotalClearCount);
            Assert.IsTrue(result.CanMove(ctx, 3));
        }

        [Test]
        public void TryApplyMove_OwnerSpawnsUncoveredWithClearOuterColorWaiting_ArrivesUnlockedAndCleared()
        {
            var ctx = UnspawnedLockBoard(KeyEffect.ClearOuterColor, shuttered: false);
            var resolver = Resolver();
            resolver.TryApplyMove(ctx, BoardState.CreateInitial(ctx), PushRedKey, out var waiting, out _);

            resolver.TryApplyMove(ctx, waiting, StepBlueAside, out var result, out _);

            Assert.IsTrue(result.Alive[3]);
            Assert.IsTrue(result.Unlocked[3]);
            Assert.IsNull(result.WaitingKeyEffect[3]);
            Assert.AreEqual(1, result.ClearedColors[3]);
            Assert.AreEqual(BlockColor.Pink, result.CurrentColorOf(ctx, 3));
            Assert.AreEqual(2, result.TotalClearCount, "the key's clear, drained in the same resolution");
            Assert.AreEqual(1, result.ClearCountByColor[(int)BlockColor.Green]);
        }

        [Test]
        public void TryApplyMove_OwnerSpawnsUnderAClosedShutter_KeepsWaitingUntilTheOpeningAppliesIt()
        {
            var ctx = UnspawnedLockBoard(KeyEffect.ClearOuterColor, shuttered: true);
            var resolver = Resolver();
            resolver.TryApplyMove(ctx, BoardState.CreateInitial(ctx), PushRedKey, out var waiting, out _);
            resolver.TryApplyMove(ctx, waiting, StepBlueAside, out var spawnedHidden, out _);

            resolver.TryApplyMove(ctx, spawnedHidden, PushYellow, out var opened, out _);

            Assert.IsTrue(spawnedHidden.Alive[3]);
            Assert.AreEqual(KeyEffect.ClearOuterColor, spawnedHidden.WaitingKeyEffect[3], "nothing reaches it yet");
            Assert.IsFalse(spawnedHidden.Unlocked[3]);
            Assert.AreEqual(0, spawnedHidden.ClearedColors[3]);
            Assert.IsTrue(opened.ShutterOpen[0]);
            Assert.IsNull(opened.WaitingKeyEffect[3]);
            Assert.IsTrue(opened.Unlocked[3]);
            Assert.AreEqual(1, opened.ClearedColors[3]);
            Assert.AreEqual(3, opened.TotalClearCount);
        }

        [TestCase(KeyEffect.UnlockMovement)]
        [TestCase(KeyEffect.ClearOuterColor)]
        public void TryApplyMove_ShutterOpensWhileTheWaitingOwnerIsStillUnspawned_TheEffectWaitsForTheSpawn(
            KeyEffect keyEffect)
        {
            // Regression: the opening's release pass must skip an owner that has
            // not spawned. Its sentinel origin lies under no shutter, so a shutter
            // test alone reads "uncovered" and would apply the effect to a block
            // that is not on the board.
            var ctx = UnspawnedLockBoard(keyEffect, shuttered: true);
            var resolver = Resolver();
            resolver.TryApplyMove(ctx, BoardState.CreateInitial(ctx), PushRedKey, out var waiting, out _);
            resolver.TryApplyMove(ctx, waiting, PushYellow, out var opened, out _);

            resolver.TryApplyMove(ctx, opened, StepBlueAside, out var spawned, out _);

            Assert.IsTrue(opened.ShutterOpen[0]);
            Assert.IsFalse(opened.Alive[3]);
            Assert.AreEqual(BoardState.UnspawnedOrigin, opened.Origins[3]);
            Assert.AreEqual(keyEffect, opened.WaitingKeyEffect[3]);
            Assert.IsFalse(opened.Unlocked[3]);
            Assert.AreEqual(0, opened.ClearedColors[3]);
            Assert.AreEqual(2, opened.TotalClearCount);
            Assert.IsTrue(spawned.Alive[3]);
            Assert.IsNull(spawned.WaitingKeyEffect[3]);
            Assert.IsTrue(spawned.Unlocked[3]);
            Assert.AreEqual(keyEffect == KeyEffect.ClearOuterColor ? 1 : 0, spawned.ClearedColors[3]);
        }

        // ----- The chain (ARCHITECTURE, core concept 3) ------------------------

        [Test]
        public void TryApplyMove_LastWaveBlockCarriesAClearKey_ClearsTheLockCrossesAShutterAndTheNextWaveArrives()
        {
            // 3x1. Region (0,0) over a red gate; wave 0 is the red key block
            // (index 2, a ClearOuterColor key for lock 1), wave 1 a green block
            // (index 3). Index 0 is the blue/yellow lock owner at (1,0); index 1 a
            // yellow block under a blue-bound shutter at (2,0). One push: the key
            // block clears and dies, its key clears the owner's blue, that blue
            // clear opens the shutter, and the emptied region takes wave 1.
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(1, 0), colors: new[] { BlockColor.Blue, BlockColor.Yellow }, lockId: 1, requiredKeys: 1),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Yellow })
                },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0), threshold: 1, requiredColor: BlockColor.Blue) },
                elevators: new[]
                {
                    Elevator(1, new Coord(0, 0), new Coord(0, 0),
                        new[] { Spawned(keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor, regionOrigin: new Coord(0, 0)) },
                        new[] { Spawned(colors: new[] { BlockColor.Green }, regionOrigin: new Coord(0, 0)) })
                });
            var initial = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, initial, new Move(2, new Coord(0, 0)), out var result, out _);

            Assert.IsTrue(initial.Alive[2], "wave 0 arrived at level start");
            Assert.IsTrue(moved);
            Assert.IsFalse(result.Alive[2]);
            Assert.IsTrue(result.KeyConsumed[2]);
            Assert.IsTrue(result.Unlocked[0]);
            Assert.AreEqual(1, result.ClearedColors[0]);
            Assert.IsTrue(result.ShutterOpen[0], "the key's blue clear crossed the shutter's threshold");
            Assert.AreEqual(2, result.ElevatorWaveIndex[0]);
            Assert.IsTrue(result.ElevatorWaveActive[0]);
            Assert.IsTrue(result.Alive[3]);
            Assert.AreEqual(new Coord(0, 0), result.Origins[3]);
            Assert.AreEqual(2, result.TotalClearCount);
        }
    }
}
