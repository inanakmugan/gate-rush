using System;
using System.Collections.Generic;
using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    public class BoardStateTests
    {
        // Thin forwarders onto the shared Fixture builder. Kept because this
        // file's helpers use a Create* prefix and CreateContext pins a 6x6 grid;
        // forwarding leaves the 34 call sites and the fixed size untouched.
        private static BlockDefinition CreateBlock(
            int id,
            Coord startOrigin,
            IReadOnlyList<BlockColor> colorStack = null,
            int? unfreezeAtClearCount = null,
            int? lockId = null,
            int requiredKeyCount = 0,
            int? keyTargetLockId = null) =>
            Block(
                id, startOrigin,
                colors: colorStack,
                unfreezeAt: unfreezeAtClearCount,
                lockId: lockId,
                requiredKeys: requiredKeyCount,
                keyTarget: keyTargetLockId);

        private static SpawnedBlock CreateSpawnedBlock(IReadOnlyList<BlockColor> colorStack = null) =>
            Spawned(colors: colorStack ?? new[] { BlockColor.Purple });

        private static LevelContext CreateContext(
            IReadOnlyList<BlockDefinition> blocks = null,
            IReadOnlyList<GateDefinition> gates = null,
            IReadOnlyList<ShutterDefinition> shutters = null,
            IReadOnlyList<GeneratorDefinition> generators = null,
            IReadOnlyList<ElevatorDefinition> elevators = null) =>
            Ctx(6, 6, blocks, gates, shutters, generators, elevators);

        /// <summary>
        /// Populates every field group at least once: 3 top-level blocks (one
        /// two-layer, one locked, one carrying its key), 1 gate, 1 shutter, 1
        /// generator with one queued block, 1 elevator with one wave of one
        /// block. Index 0/1/2 are the top-level blocks, 3 is the generator's
        /// spawn slot, 4 is the elevator's.
        /// </summary>
        private static LevelContext CreateFullContext()
        {
            var blockA = CreateBlock(1, new Coord(0, 0));
            var blockB = CreateBlock(
                2, new Coord(1, 0),
                colorStack: new[] { BlockColor.Green, BlockColor.Blue },
                unfreezeAtClearCount: 2,
                lockId: 5,
                requiredKeyCount: 1);
            var blockC = CreateBlock(3, new Coord(2, 0), keyTargetLockId: 5);

            var gate = new GateDefinition(10, BoardEdge.Top, 0, 1, BlockColor.Red, openAtClearCount: 2);
            var shutter = new ShutterDefinition(20, new Coord(4, 4), new Coord(5, 5), 3, null);
            var generator = new GeneratorDefinition(30, BoardEdge.Bottom, 3, new[] { CreateSpawnedBlock() });
            var elevator = new ElevatorDefinition(
                40, new Coord(0, 3), new Coord(1, 3),
                new IReadOnlyList<SpawnedBlock>[] { new[] { CreateSpawnedBlock(new[] { BlockColor.Cyan }) } });

            return CreateContext(
                blocks: new[] { blockA, blockB, blockC },
                gates: new[] { gate },
                shutters: new[] { shutter },
                generators: new[] { generator },
                elevators: new[] { elevator });
        }

        /// <summary>
        /// One block per <c>CanMove</c>/<c>CanBeTargeted</c> truth-table row:
        /// 0 normal, 1 frozen, 2 locked, 3 the key for 2, 4 under a closed
        /// shutter, 5 normal (flipped to dead via <see cref="With"/> by the
        /// tests that need it).
        /// </summary>
        private static LevelContext CreateTargetingContext()
        {
            var normal = CreateBlock(1, new Coord(0, 0));
            var frozen = CreateBlock(2, new Coord(1, 0), unfreezeAtClearCount: 5);
            var locked = CreateBlock(3, new Coord(2, 0), lockId: 7, requiredKeyCount: 1);
            var key = CreateBlock(4, new Coord(3, 0), keyTargetLockId: 7);
            var underShutter = CreateBlock(5, new Coord(0, 1));
            var willBeDead = CreateBlock(6, new Coord(1, 1));

            var shutter = new ShutterDefinition(1, new Coord(0, 1), new Coord(0, 1), 3, null);

            return CreateContext(
                blocks: new[] { normal, frozen, locked, key, underShutter, willBeDead },
                shutters: new[] { shutter });
        }

        private static BoardState With(
            BoardState baseline,
            IReadOnlyList<Coord> origins = null,
            IReadOnlyList<byte> clearedColors = null,
            IReadOnlyList<bool> alive = null,
            IReadOnlyList<bool> unfrozen = null,
            IReadOnlyList<bool> unlocked = null,
            IReadOnlyList<bool> gateOpen = null,
            IReadOnlyList<bool> shutterOpen = null,
            IReadOnlyList<int> generatorIndex = null,
            IReadOnlyList<int> elevatorWaveIndex = null,
            IReadOnlyList<bool> elevatorWaveActive = null,
            int? totalClearCount = null,
            IReadOnlyList<int> clearCountByColor = null,
            IReadOnlyList<bool> keyConsumed = null)
        {
            return new BoardState(
                origins ?? baseline.Origins,
                clearedColors ?? baseline.ClearedColors,
                alive ?? baseline.Alive,
                unfrozen ?? baseline.Unfrozen,
                unlocked ?? baseline.Unlocked,
                gateOpen ?? baseline.GateOpen,
                shutterOpen ?? baseline.ShutterOpen,
                generatorIndex ?? baseline.GeneratorIndex,
                elevatorWaveIndex ?? baseline.ElevatorWaveIndex,
                elevatorWaveActive ?? baseline.ElevatorWaveActive,
                totalClearCount ?? baseline.TotalClearCount,
                clearCountByColor ?? baseline.ClearCountByColor,
                keyConsumed ?? baseline.KeyConsumed);
        }

        private static T[] ReplaceAt<T>(IReadOnlyList<T> source, int index, T value)
        {
            var copy = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            copy[index] = value;
            return copy;
        }

        [Test]
        public void Equals_TwoStatesFromIdenticalData_AreEqualAndShareHashCode()
        {
            var a = BoardState.CreateInitial(CreateFullContext());
            var b = BoardState.CreateInitial(CreateFullContext());

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingOrigins_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, origins: ReplaceAt(baseline.Origins, 0, new Coord(5, 5)));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingClearedColors_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, clearedColors: ReplaceAt(baseline.ClearedColors, 1, (byte)1));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingAlive_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, alive: ReplaceAt(baseline.Alive, 0, false));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingUnfrozen_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, unfrozen: ReplaceAt(baseline.Unfrozen, 1, true));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingUnlocked_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, unlocked: ReplaceAt(baseline.Unlocked, 1, true));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingKeyConsumed_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, keyConsumed: ReplaceAt(baseline.KeyConsumed, 2, true));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingGateOpen_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, gateOpen: ReplaceAt(baseline.GateOpen, 0, true));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingShutterOpen_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, shutterOpen: ReplaceAt(baseline.ShutterOpen, 0, true));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingGeneratorIndex_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, generatorIndex: ReplaceAt(baseline.GeneratorIndex, 0, 1));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingElevatorWaveIndex_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, elevatorWaveIndex: ReplaceAt(baseline.ElevatorWaveIndex, 0, 1));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingElevatorWaveActive_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, elevatorWaveActive: ReplaceAt(baseline.ElevatorWaveActive, 0, true));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingTotalClearCount_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, totalClearCount: baseline.TotalClearCount + 1);

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingClearCountByColor_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, clearCountByColor: ReplaceAt(baseline.ClearCountByColor, 0, 1));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void CanMove_NormalAliveBlock_ReturnsTrue()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsTrue(state.CanMove(ctx, 0));
        }

        [Test]
        public void CanBeTargeted_NormalAliveBlock_ReturnsTrue()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsTrue(state.CanBeTargeted(ctx, 0));
        }

        [Test]
        public void CanMove_FrozenBlock_ReturnsFalse()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsFalse(state.CanMove(ctx, 1));
        }

        [Test]
        public void CanBeTargeted_FrozenBlock_ReturnsTrue()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsTrue(state.CanBeTargeted(ctx, 1));
        }

        [Test]
        public void CanMove_LockedBlock_ReturnsFalse()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsFalse(state.CanMove(ctx, 2));
        }

        [Test]
        public void CanBeTargeted_LockedBlock_ReturnsTrue()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsTrue(state.CanBeTargeted(ctx, 2));
        }

        [Test]
        public void CanMove_BlockUnderClosedShutter_ReturnsFalse()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsFalse(state.CanMove(ctx, 4));
        }

        [Test]
        public void CanBeTargeted_BlockUnderClosedShutter_ReturnsFalse()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsFalse(state.CanBeTargeted(ctx, 4));
        }

        [Test]
        public void CanMove_DeadBlock_ReturnsFalse()
        {
            var ctx = CreateTargetingContext();
            var baseline = BoardState.CreateInitial(ctx);
            var dead = With(baseline, alive: ReplaceAt(baseline.Alive, 5, false));

            Assert.IsFalse(dead.CanMove(ctx, 5));
        }

        [Test]
        public void CanBeTargeted_DeadBlock_ReturnsFalse()
        {
            var ctx = CreateTargetingContext();
            var baseline = BoardState.CreateInitial(ctx);
            var dead = With(baseline, alive: ReplaceAt(baseline.Alive, 5, false));

            Assert.IsFalse(dead.CanBeTargeted(ctx, 5));
        }

        [Test]
        public void IsSolved_InitialStateWithOnlyAGeneratorQueue_ReturnsFalse()
        {
            var generator = new GeneratorDefinition(1, BoardEdge.Top, 0, new[] { CreateSpawnedBlock() });
            var ctx = CreateContext(generators: new[] { generator });

            var state = BoardState.CreateInitial(ctx);

            // No blocks were ever placed, so every "Alive" entry is false from
            // the start — indistinguishable from a finished level unless the
            // generator's own exhaustion is checked too.
            Assert.IsFalse(state.IsSolved(ctx));
        }

        [Test]
        public void IsSolved_InitialStateWithOnlyElevatorWaves_ReturnsFalse()
        {
            var elevator = new ElevatorDefinition(
                1, new Coord(0, 0), new Coord(0, 0),
                new IReadOnlyList<SpawnedBlock>[] { new[] { CreateSpawnedBlock() } });
            var ctx = CreateContext(elevators: new[] { elevator });

            var state = BoardState.CreateInitial(ctx);

            Assert.IsFalse(state.IsSolved(ctx));
        }

        [Test]
        public void IsSolved_EmptyLevelWithNoSpawnersPending_ReturnsTrue()
        {
            var ctx = CreateContext();

            var state = BoardState.CreateInitial(ctx);

            Assert.IsTrue(state.IsSolved(ctx));
        }

        [Test]
        public void CurrentColorOf_AfterPartialClear_ReturnsNextColorInStack()
        {
            var ctx = CreateFullContext();
            var baseline = BoardState.CreateInitial(ctx);
            var partiallyCleared = With(baseline, clearedColors: ReplaceAt(baseline.ClearedColors, 1, (byte)1));

            Assert.AreEqual(BlockColor.Blue, partiallyCleared.CurrentColorOf(ctx, 1));
        }

        [Test]
        public void CurrentColorOf_GeneratorSpawnSlot_ResolvesGeneratorsQueuedColor()
        {
            var ctx = CreateFullContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(BlockColor.Purple, state.CurrentColorOf(ctx, 3));
        }

        [Test]
        public void CurrentColorOf_ElevatorSpawnSlot_ResolvesElevatorsWaveColor()
        {
            var ctx = CreateFullContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(BlockColor.Cyan, state.CurrentColorOf(ctx, 4));
        }

        [Test]
        public void CreateInitial_GeneratorSpawnSlot_StartsInactiveWithUnspawnedOrigin()
        {
            var ctx = CreateFullContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsFalse(state.Alive[3]);
            Assert.AreEqual(BoardState.UnspawnedOrigin, state.Origins[3]);
        }

        [Test]
        public void IsCellFree_ClosedShutterRegion_TreatedAsOccupied()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsFalse(state.IsCellFree(ctx, new Coord(0, 1)));
        }

        [Test]
        public void IsCellFree_OpenShutterRegion_TreatedAsFree()
        {
            var ctx = CreateTargetingContext();
            var baseline = BoardState.CreateInitial(ctx);
            var opened = With(baseline, shutterOpen: ReplaceAt(baseline.ShutterOpen, 0, true));

            // The block parked there (index 4) still occupies the cell in its
            // own right; ignore it to isolate the shutter-open behaviour.
            Assert.IsTrue(opened.IsCellFree(ctx, new Coord(0, 1), ignoreBlockIndex: 4));
        }

        [Test]
        public void IsCellFree_CellOccupiedOnlyBySelf_ReturnsTrueWhenIgnored()
        {
            var ctx = CreateTargetingContext();
            var state = BoardState.CreateInitial(ctx);

            Assert.IsFalse(state.IsCellFree(ctx, new Coord(0, 0)));
            Assert.IsTrue(state.IsCellFree(ctx, new Coord(0, 0), ignoreBlockIndex: 0));
        }

        [Test]
        public void IsCellFree_CalledWithDifferentContextThanFirstBuild_Throws()
        {
            var ctxA = CreateFullContext();
            var ctxB = CreateFullContext();
            var state = BoardState.CreateInitial(ctxA);

            state.IsCellFree(ctxA, new Coord(0, 0));

            Assert.Throws<ArgumentException>(() => state.IsCellFree(ctxB, new Coord(0, 0)));
        }

        [Test]
        public void EnsureOccupancyMap_TwoLivingBlocksShareACell_Throws()
        {
            var ctx = CreateFullContext();
            var baseline = BoardState.CreateInitial(ctx);
            var overlapping = With(baseline, origins: ReplaceAt(baseline.Origins, 1, baseline.Origins[0]));

            // The cell must be one IsCellFree does not short-circuit on: the grid,
            // static-wall and shutter checks all return before the map is ever built.
            // (5,5) sits inside this fixture's closed shutter, so the throw would
            // never fire there.
            Assert.Throws<InvalidOperationException>(() => overlapping.IsCellFree(ctx, new Coord(3, 3)));
        }

        [Test]
        public void IsSolved_EveryWavePlacedButElevatorStillMarkedActive_ReturnsFalse()
        {
            var elevator = new ElevatorDefinition(
                1, new Coord(0, 0), new Coord(0, 0),
                new IReadOnlyList<SpawnedBlock>[] { new[] { CreateSpawnedBlock() } });
            var ctx = CreateContext(elevators: new[] { elevator });
            var baseline = BoardState.CreateInitial(ctx);

            // Every wave placed, nothing alive anywhere, but Active still true
            // — an internally contradictory state a correct resolver would
            // never produce.
            var contradictory = With(
                baseline,
                elevatorWaveIndex: ReplaceAt(baseline.ElevatorWaveIndex, 0, 1),
                elevatorWaveActive: ReplaceAt(baseline.ElevatorWaveActive, 0, true));

            Assert.IsFalse(contradictory.IsSolved(ctx));
        }
    }
}