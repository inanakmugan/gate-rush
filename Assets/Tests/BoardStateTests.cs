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

        private static SpawnedBlock CreateSpawnedBlock(
            IReadOnlyList<BlockColor> colorStack = null, Coord? regionOrigin = null) =>
            Spawned(colors: colorStack ?? new[] { BlockColor.Purple }, regionOrigin: regionOrigin);

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
        /// spawn slot, 4 is the elevator's. Both targets are empty, so both have
        /// spawned in the state <c>CreateInitial</c> returns (D42): 3 at (3, 0),
        /// 4 at (0, 3).
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
            var generator = new GeneratorDefinition(30, BoardEdge.Bottom, 3, 1, new[] { CreateSpawnedBlock() });
            var elevator = new ElevatorDefinition(
                40, new Coord(0, 3), new Coord(0, 3),
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { CreateSpawnedBlock(new[] { BlockColor.Cyan }, regionOrigin: new Coord(0, 0)) }
                });

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
            IReadOnlyList<bool> keyConsumed = null,
            IReadOnlyList<KeyEffect?> waitingKeyEffect = null)
        {
            // The baseline's symmetry, so a perturbed fixture stays a state of
            // the same level — the same inheritance MoveResolver's successor
            // builder relies on.
            return new BoardState(
                baseline.Symmetry,
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
                keyConsumed ?? baseline.KeyConsumed,
                waitingKeyEffect ?? baseline.WaitingKeyEffect);
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
        public void GetHashCode_ChangingWaitingKeyEffect_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(
                baseline, waitingKeyEffect: ReplaceAt(baseline.WaitingKeyEffect, 1, (KeyEffect?)KeyEffect.UnlockMovement));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void Equals_StatesDifferingOnlyInWhichEffectWaits_AreDifferentStates()
        {
            // D41: with mixed-effect keys, two completion orders leave the same
            // keys consumed but different effects waiting. The visited set must
            // tell them apart, including UnlockMovement — enum value 0 — from
            // nothing waiting at all.
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var unlock = With(
                baseline, waitingKeyEffect: ReplaceAt(baseline.WaitingKeyEffect, 1, (KeyEffect?)KeyEffect.UnlockMovement));
            var clear = With(
                baseline, waitingKeyEffect: ReplaceAt(baseline.WaitingKeyEffect, 1, (KeyEffect?)KeyEffect.ClearOuterColor));

            Assert.AreNotEqual(baseline, unlock);
            Assert.AreNotEqual(unlock, clear);
            Assert.AreNotEqual(unlock.GetHashCode(), clear.GetHashCode());
        }

        [Test]
        public void CreateInitial_EveryBlockSlot_HasNothingWaiting()
        {
            var ctx = CreateFullContext();

            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(ctx.TotalBlockCapacity, state.WaitingKeyEffect.Count);
            foreach (var waiting in state.WaitingKeyEffect)
            {
                Assert.IsNull(waiting);
            }
        }

        [Test]
        public void CreateInitialWithWaitingKeyEffects_CarriesTheGivenValuesAndCopiesThem()
        {
            var ctx = CreateFullContext();
            var given = new KeyEffect?[ctx.TotalBlockCapacity];
            given[1] = KeyEffect.ClearOuterColor;

            var state = BoardState.CreateInitialWithWaitingKeyEffects(ctx, given);
            given[1] = null;

            Assert.AreEqual(KeyEffect.ClearOuterColor, state.WaitingKeyEffect[1]);
            Assert.AreEqual(
                With(BoardState.CreateInitial(ctx), waitingKeyEffect: ReplaceAt(
                    BoardState.CreateInitial(ctx).WaitingKeyEffect, 1, (KeyEffect?)KeyEffect.ClearOuterColor)),
                state);
        }

        [Test]
        public void CreateInitialWithWaitingKeyEffects_WrongLength_Throws()
        {
            var ctx = CreateFullContext();

            Assert.Throws<ArgumentException>(() =>
                BoardState.CreateInitialWithWaitingKeyEffects(ctx, new KeyEffect?[ctx.TotalBlockCapacity - 1]));
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
            var mutated = With(baseline, generatorIndex: ReplaceAt(baseline.GeneratorIndex, 0, baseline.GeneratorIndex[0] + 1));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingElevatorWaveIndex_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, elevatorWaveIndex: ReplaceAt(baseline.ElevatorWaveIndex, 0, baseline.ElevatorWaveIndex[0] + 1));

            Assert.AreNotEqual(baseline.GetHashCode(), mutated.GetHashCode());
        }

        [Test]
        public void GetHashCode_ChangingElevatorWaveActive_ChangesHash()
        {
            var baseline = BoardState.CreateInitial(CreateFullContext());
            var mutated = With(baseline, elevatorWaveActive: ReplaceAt(baseline.ElevatorWaveActive, 0, !baseline.ElevatorWaveActive[0]));

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

        // ----- Interchangeable blocks (D35) --------------------------------

        /// <summary>
        /// Three interchangeable red-over-blue blocks on an open grid, plus one
        /// green block that shares no spec with them. Indices 0, 1, 2 form the
        /// one group; index 3 stands alone. The two-colour stack is what lets a
        /// test give one group member a shed colour the others have not.
        /// </summary>
        private static LevelContext CreateSymmetricContext() =>
            Ctx(
                4, 4,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }),
                    Block(4, new Coord(3, 0), colors: new[] { BlockColor.Green })
                });

        /// <summary>Returns <paramref name="source"/> with the values at the two indices exchanged.</summary>
        private static T[] Swap<T>(IReadOnlyList<T> source, int first, int second)
        {
            var copy = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            copy[first] = source[second];
            copy[second] = source[first];
            return copy;
        }

        [Test]
        public void Equals_TwoInterchangeableBlocksSwapped_IsTheSameStateWithTheSameHash()
        {
            var baseline = BoardState.CreateInitial(CreateSymmetricContext());

            var swapped = With(baseline, origins: Swap(baseline.Origins, 0, 2));

            Assert.AreEqual(baseline, swapped);
            Assert.AreEqual(baseline.GetHashCode(), swapped.GetHashCode());
        }

        [Test]
        public void Equals_InterchangeableBlocksSwapped_LeavesOriginsLiterallyAddressed()
        {
            // The collapse is the visited set's notion of "seen before" and
            // nothing more: a Move a search returns names a literal block index,
            // so the arrays it replays against must keep reporting literal
            // positions.
            var baseline = BoardState.CreateInitial(CreateSymmetricContext());

            var swapped = With(baseline, origins: Swap(baseline.Origins, 0, 2));

            Assert.AreEqual(new Coord(2, 0), swapped.Origins[0]);
            Assert.AreEqual(new Coord(0, 0), swapped.Origins[2]);
        }

        [Test]
        public void Equals_SwappingAcrossTwoDifferentSpecs_IsADifferentState()
        {
            // Index 3 is the green block: exchanging its position with a
            // red-over-blue one's genuinely changes the board, and must not
            // collapse.
            var baseline = BoardState.CreateInitial(CreateSymmetricContext());

            var swapped = With(baseline, origins: Swap(baseline.Origins, 0, 3));

            Assert.AreNotEqual(baseline, swapped);
        }

        [Test]
        public void Equals_InterchangeableBlocksSwappedWithTheirWholeRow_IsTheSameState()
        {
            // Relabelling means the whole row travels: block 0 at (0, 0) having
            // shed a colour is the same board as block 2 sitting there having
            // shed one, with block 0 at (2, 0) intact.
            var baseline = BoardState.CreateInitial(CreateSymmetricContext());
            var oneCleared = With(baseline, clearedColors: ReplaceAt(baseline.ClearedColors, 0, (byte)1));

            var relabelled = With(
                oneCleared,
                origins: Swap(oneCleared.Origins, 0, 2),
                clearedColors: Swap(oneCleared.ClearedColors, 0, 2));

            Assert.AreEqual(oneCleared, relabelled);
            Assert.AreEqual(oneCleared.GetHashCode(), relabelled.GetHashCode());
        }

        [Test]
        public void Equals_InterchangeableBlocksSwappedByOriginAlone_IsADifferentState()
        {
            // Moving only the origins leaves the shed colour behind: the block
            // at (0, 0) is now intact and the one at (2, 0) is not, which is a
            // genuinely different board rather than a relabelling of this one.
            var baseline = BoardState.CreateInitial(CreateSymmetricContext());
            var oneCleared = With(baseline, clearedColors: ReplaceAt(baseline.ClearedColors, 0, (byte)1));

            var originsOnly = With(oneCleared, origins: Swap(oneCleared.Origins, 0, 2));

            Assert.AreNotEqual(oneCleared, originsOnly);
        }

        [Test]
        public void Equals_LevelWithNoInterchangeableBlocks_StillComparesByLiteralIndex()
        {
            // CreateFullContext has no repeated spec, so the identity order
            // applies and swapping two blocks' origins is a different state.
            var baseline = BoardState.CreateInitial(CreateFullContext());

            var swapped = With(baseline, origins: Swap(baseline.Origins, 0, 1));

            Assert.AreNotEqual(baseline, swapped);
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
            var generator = new GeneratorDefinition(1, BoardEdge.Top, 0, 1, new[] { CreateSpawnedBlock() });
            var ctx = CreateContext(generators: new[] { generator });

            // The unresolved state: settled, the generator would already have
            // spawned (D42) and a living block would decide this on its own.
            var state = BoardState.CreateUnresolved(ctx, ctx.BlockSymmetry, null);

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
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { CreateSpawnedBlock(regionOrigin: new Coord(0, 0)) }
                });
            var ctx = CreateContext(elevators: new[] { elevator });

            // Unresolved, for the same reason as the generator case above.
            var state = BoardState.CreateUnresolved(ctx, ctx.BlockSymmetry, null);

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

        // ----- Level start is settled (D42) ---------------------------------

        [Test]
        public void CreateInitial_GeneratorWhoseCellIsOccupied_LeavesItsSlotUnspawned()
        {
            var generator = new GeneratorDefinition(1, BoardEdge.Bottom, 3, 1, new[] { CreateSpawnedBlock() });
            var ctx = CreateContext(blocks: new[] { CreateBlock(1, new Coord(3, 0)) }, generators: new[] { generator });

            var state = BoardState.CreateInitial(ctx);

            Assert.IsFalse(state.Alive[1]);
            Assert.AreEqual(BoardState.UnspawnedOrigin, state.Origins[1]);
            Assert.AreEqual(0, state.GeneratorIndex[0]);
        }

        [Test]
        public void CreateInitial_GeneratorWhoseCellIsEmpty_HasSpawnedBeforeTheFirstMove()
        {
            var ctx = CreateFullContext();

            var state = BoardState.CreateInitial(ctx);

            Assert.IsTrue(state.Alive[3]);
            Assert.AreEqual(new Coord(3, 0), state.Origins[3]);
            Assert.AreEqual(ctx.GeneratorSpawnOrigin(0, 0), state.Origins[3]);
            Assert.AreEqual(1, state.GeneratorIndex[0]);
            Assert.AreEqual(0, state.TotalClearCount, "spawning is not a clear");
        }

        [Test]
        public void CreateInitial_ElevatorWhoseRegionIsEmpty_HasPlacedItsFirstWave()
        {
            var ctx = CreateFullContext();

            var state = BoardState.CreateInitial(ctx);

            Assert.IsTrue(state.Alive[4]);
            Assert.AreEqual(new Coord(0, 3), state.Origins[4]);
            Assert.AreEqual(1, state.ElevatorWaveIndex[0]);
            Assert.IsTrue(state.ElevatorWaveActive[0]);
        }

        [Test]
        public void CreateInitial_LevelWithoutSpawners_IsTheUnresolvedStateFieldForField()
        {
            var ctx = CreateTargetingContext();
            var unresolved = BoardState.CreateUnresolved(ctx, ctx.BlockSymmetry, null);

            var settled = BoardState.CreateInitial(ctx);

            Assert.AreEqual(unresolved, settled);
            Assert.AreEqual(unresolved.GetHashCode(), settled.GetHashCode());
            AssertEveryArrayEqual(unresolved, settled);
        }

        [Test]
        public void CreateInitial_EveryOverload_SettlesTheSameWay()
        {
            // The public overload, the internal symmetry one BFS's plain-identity
            // baseline uses, and the waiting-effects one all settle through the
            // same resolution, so all three start with both spawners fired.
            var ctx = CreateFullContext();
            var viaPublic = BoardState.CreateInitial(ctx);

            var viaSymmetry = BoardState.CreateInitial(ctx, BlockSymmetry.None);
            var viaWaiting = BoardState.CreateInitialWithWaitingKeyEffects(ctx, new KeyEffect?[ctx.TotalBlockCapacity]);

            Assert.IsTrue(viaPublic.Alive[3] && viaPublic.Alive[4]);
            AssertEveryArrayEqual(viaPublic, viaSymmetry);
            AssertEveryArrayEqual(viaPublic, viaWaiting);
        }

        /// <summary>
        /// Compares every public array literally, index by index — stricter
        /// than <see cref="BoardState.Equals(BoardState)"/>, which reads
        /// interchangeable blocks in canonical order (D35).
        /// </summary>
        private static void AssertEveryArrayEqual(BoardState expected, BoardState actual)
        {
            CollectionAssert.AreEqual(expected.Origins, actual.Origins, "Origins");
            CollectionAssert.AreEqual(expected.ClearedColors, actual.ClearedColors, "ClearedColors");
            CollectionAssert.AreEqual(expected.Alive, actual.Alive, "Alive");
            CollectionAssert.AreEqual(expected.Unfrozen, actual.Unfrozen, "Unfrozen");
            CollectionAssert.AreEqual(expected.Unlocked, actual.Unlocked, "Unlocked");
            CollectionAssert.AreEqual(expected.KeyConsumed, actual.KeyConsumed, "KeyConsumed");
            CollectionAssert.AreEqual(expected.WaitingKeyEffect, actual.WaitingKeyEffect, "WaitingKeyEffect");
            CollectionAssert.AreEqual(expected.GateOpen, actual.GateOpen, "GateOpen");
            CollectionAssert.AreEqual(expected.ShutterOpen, actual.ShutterOpen, "ShutterOpen");
            CollectionAssert.AreEqual(expected.GeneratorIndex, actual.GeneratorIndex, "GeneratorIndex");
            CollectionAssert.AreEqual(expected.ElevatorWaveIndex, actual.ElevatorWaveIndex, "ElevatorWaveIndex");
            CollectionAssert.AreEqual(expected.ElevatorWaveActive, actual.ElevatorWaveActive, "ElevatorWaveActive");
            CollectionAssert.AreEqual(expected.ClearCountByColor, actual.ClearCountByColor, "ClearCountByColor");
            Assert.AreEqual(expected.TotalClearCount, actual.TotalClearCount, "TotalClearCount");
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
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { CreateSpawnedBlock(regionOrigin: new Coord(0, 0)) }
                });
            var ctx = CreateContext(elevators: new[] { elevator });
            var baseline = BoardState.CreateInitial(ctx);

            // Every wave placed, nothing alive anywhere, but Active still true
            // — an internally contradictory state a correct resolver would
            // never produce. The settled baseline has the wave's block alive
            // (D42), so it is killed here: otherwise that living block alone
            // would make the level unsolved.
            var contradictory = With(
                baseline,
                alive: ReplaceAt(baseline.Alive, 0, false),
                elevatorWaveIndex: ReplaceAt(baseline.ElevatorWaveIndex, 0, 1),
                elevatorWaveActive: ReplaceAt(baseline.ElevatorWaveActive, 0, true));

            Assert.IsFalse(contradictory.IsSolved(ctx));
        }
    }
}