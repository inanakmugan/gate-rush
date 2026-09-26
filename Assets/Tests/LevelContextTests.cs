using System;
using System.Collections.Generic;
using GateRush.Core;
using NUnit.Framework;

namespace GateRush.Tests
{
    public class LevelContextTests
    {
        private static BlockDefinition CreateBlock(
            int id,
            Coord startOrigin,
            IReadOnlyList<Coord> cells = null,
            int? lockId = null,
            int requiredKeyCount = 0,
            int? keyTargetLockId = null)
        {
            return new BlockDefinition(
                id: id,
                cells: cells ?? new[] { new Coord(0, 0) },
                colorStack: new[] { BlockColor.Red },
                startOrigin: startOrigin,
                axis: MovementAxis.Free,
                unfreezeAtClearCount: null,
                lockId: lockId,
                requiredKeyCount: requiredKeyCount,
                keyTargetLockId: keyTargetLockId,
                keyEffect: KeyEffect.UnlockMovement,
                timeBonusSeconds: 0);
        }

        private static SpawnedBlock CreateSpawnedBlock(
            int? keyTargetLockId = null,
            int? lockId = null,
            int requiredKeyCount = 0,
            Coord? regionOrigin = null)
        {
            return new SpawnedBlock(
                cells: new[] { new Coord(0, 0) },
                colorStack: new[] { BlockColor.Blue },
                axis: MovementAxis.Free,
                unfreezeAtClearCount: null,
                lockId: lockId,
                requiredKeyCount: requiredKeyCount,
                keyTargetLockId: keyTargetLockId,
                keyEffect: KeyEffect.UnlockMovement,
                timeBonusSeconds: 0,
                regionOrigin: regionOrigin);
        }

        private static LevelContext CreateContext(
            int width,
            int height,
            IReadOnlyList<BlockDefinition> blocks = null,
            IReadOnlyList<GateDefinition> gates = null,
            IReadOnlyList<ShutterDefinition> shutters = null,
            IReadOnlyList<GeneratorDefinition> generators = null,
            IReadOnlyList<ElevatorDefinition> elevators = null,
            IReadOnlyList<Coord> staticWalls = null)
        {
            return new LevelContext(
                levelId: 1,
                width: width,
                height: height,
                staticWalls: staticWalls ?? Array.Empty<Coord>(),
                blocks: blocks ?? Array.Empty<BlockDefinition>(),
                gates: gates ?? Array.Empty<GateDefinition>(),
                shutters: shutters ?? Array.Empty<ShutterDefinition>(),
                generators: generators ?? Array.Empty<GeneratorDefinition>(),
                elevators: elevators ?? Array.Empty<ElevatorDefinition>(),
                suggestedTimeBudgetSeconds: 60,
                goldReward: 100);
        }

        [Test]
        public void Constructor_BlockFootprintOutsideGrid_Throws()
        {
            var block = CreateBlock(1, new Coord(5, 5));

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, new[] { block }));
        }

        [Test]
        public void Constructor_TwoBlocksOverlappingAtStart_Throws()
        {
            var a = CreateBlock(1, new Coord(0, 0));
            var b = CreateBlock(2, new Coord(0, 0));

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, new[] { a, b }));
        }

        [Test]
        public void Constructor_KeyTargetingNonexistentLock_Throws()
        {
            var block = CreateBlock(1, new Coord(0, 0), keyTargetLockId: 99);

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, new[] { block }));
        }

        [Test]
        public void Constructor_LockWithFewerKeysThanRequired_Throws()
        {
            var locked = CreateBlock(1, new Coord(0, 0), lockId: 5, requiredKeyCount: 2);
            var key = CreateBlock(2, new Coord(1, 0), keyTargetLockId: 5);

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, new[] { locked, key }));
        }

        [Test]
        public void Constructor_LockSatisfiedByKeyFromGeneratorQueue_Succeeds()
        {
            var locked = CreateBlock(1, new Coord(0, 0), lockId: 5, requiredKeyCount: 1);
            var generator = new GeneratorDefinition(
                id: 1,
                edge: BoardEdge.Top,
                offset: 0,
                width: 1,
                queue: new[] { CreateSpawnedBlock(keyTargetLockId: 5) });

            var context = CreateContext(3, 3, new[] { locked }, generators: new[] { generator });

            Assert.AreEqual(1, context.Blocks.Count);
        }

        [Test]
        public void ShutterAt_InteriorEdgeAndOutsideCells_ReturnsExpectedShutterId()
        {
            var shutter = new ShutterDefinition(9, new Coord(1, 1), new Coord(3, 3), 3, null);

            var context = CreateContext(6, 6, shutters: new[] { shutter });

            Assert.AreEqual(9, context.ShutterAt(new Coord(2, 2))); // interior
            Assert.AreEqual(9, context.ShutterAt(new Coord(1, 1))); // edge (corner)
            Assert.IsNull(context.ShutterAt(new Coord(0, 0))); // outside
            Assert.IsNull(context.ShutterAt(new Coord(5, 5))); // outside
        }

        [Test]
        public void Constructor_TwoBlocksOverlappingAtStart_NamesBothBlocksInMessage()
        {
            var a = CreateBlock(1, new Coord(0, 0));
            var b = CreateBlock(2, new Coord(0, 0));

            var ex = Assert.Throws<ArgumentException>(() => CreateContext(3, 3, new[] { a, b }));

            Assert.That(ex.Message, Does.Contain("1"));
            Assert.That(ex.Message, Does.Contain("2"));
        }

        [Test]
        public void Constructor_DuplicateBlockIds_Throws()
        {
            var a = CreateBlock(1, new Coord(0, 0));
            var b = CreateBlock(1, new Coord(1, 0));

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, new[] { a, b }));
        }

        [Test]
        public void Constructor_DuplicateGateIds_Throws()
        {
            var gateA = new GateDefinition(1, BoardEdge.Top, 0, 1, BlockColor.Red, null);
            var gateB = new GateDefinition(1, BoardEdge.Bottom, 0, 1, BlockColor.Blue, null);

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, gates: new[] { gateA, gateB }));
        }

        [Test]
        public void Constructor_DuplicateShutterIds_Throws()
        {
            var shutterA = new ShutterDefinition(1, new Coord(0, 0), new Coord(0, 0), 1, null);
            var shutterB = new ShutterDefinition(1, new Coord(2, 2), new Coord(2, 2), 1, null);

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, shutters: new[] { shutterA, shutterB }));
        }

        [Test]
        public void Constructor_DuplicateGeneratorIds_Throws()
        {
            var generatorA = new GeneratorDefinition(1, BoardEdge.Top, 0, 1, Array.Empty<SpawnedBlock>());
            var generatorB = new GeneratorDefinition(1, BoardEdge.Bottom, 0, 1, Array.Empty<SpawnedBlock>());

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, generators: new[] { generatorA, generatorB }));
        }

        // -- Edge features: fit and non-overlap (M6, D34) ---------------

        private static GateDefinition CreateGate(int id, BoardEdge edge, int offset, int width) =>
            new GateDefinition(id, edge, offset, width, BlockColor.Red, openAtClearCount: null);

        private static GeneratorDefinition CreateGenerator(int id, BoardEdge edge, int offset, int width) =>
            new GeneratorDefinition(id, edge, offset, width, Array.Empty<SpawnedBlock>());

        [Test]
        public void Constructor_GateSpanExceedsEdgeLength_Throws()
        {
            var gate = CreateGate(1, BoardEdge.Top, offset: 5, width: 2);

            Assert.Throws<ArgumentException>(() => CreateContext(6, 6, gates: new[] { gate }));
        }

        [Test]
        public void Constructor_GeneratorSpanExceedsEdgeLength_Throws()
        {
            var generator = CreateGenerator(1, BoardEdge.Top, offset: 5, width: 2);

            Assert.Throws<ArgumentException>(() => CreateContext(6, 6, generators: new[] { generator }));
        }

        [Test]
        public void Constructor_GeneratorOffsetNegative_Throws()
        {
            var generator = CreateGenerator(1, BoardEdge.Left, offset: -1, width: 1);

            Assert.Throws<ArgumentException>(() => CreateContext(6, 6, generators: new[] { generator }));
        }

        [Test]
        public void Constructor_TwoGatesOverlappingOnTheSameEdge_Throws()
        {
            var wide = CreateGate(1, BoardEdge.Top, offset: 0, width: 2);
            var inside = CreateGate(2, BoardEdge.Top, offset: 1, width: 1);

            Assert.Throws<ArgumentException>(() => CreateContext(6, 6, gates: new[] { wide, inside }));
        }

        [Test]
        public void Constructor_GateAndGeneratorOverlappingOnTheSameEdge_Throws()
        {
            var gate = CreateGate(1, BoardEdge.Bottom, offset: 1, width: 2);
            var generator = CreateGenerator(2, BoardEdge.Bottom, offset: 2, width: 1);

            var ex = Assert.Throws<ArgumentException>(
                () => CreateContext(6, 6, gates: new[] { gate }, generators: new[] { generator }));

            StringAssert.Contains("Gate 1", ex.Message);
            StringAssert.Contains("Generator 2", ex.Message);
        }

        [Test]
        public void Constructor_TwoGeneratorsOverlappingOnTheSameEdge_Throws()
        {
            var wide = CreateGenerator(1, BoardEdge.Left, offset: 0, width: 2);
            var inside = CreateGenerator(2, BoardEdge.Left, offset: 1, width: 1);

            Assert.Throws<ArgumentException>(() => CreateContext(6, 6, generators: new[] { wide, inside }));
        }

        [Test]
        public void Constructor_GateAndGeneratorAdjacentButDisjointOnTheSameEdge_Succeeds()
        {
            // M6 allows side by side, only not overlapping: [0, 2) then [2, 4).
            var gate = CreateGate(1, BoardEdge.Top, offset: 0, width: 2);
            var generator = CreateGenerator(1, BoardEdge.Top, offset: 2, width: 2);

            var context = CreateContext(6, 6, gates: new[] { gate }, generators: new[] { generator });

            Assert.AreEqual(2, context.Generators[0].Width);
        }

        [Test]
        public void Constructor_GateAndGeneratorAtTheSameOffsetOnDifferentEdges_Succeeds()
        {
            var gate = CreateGate(1, BoardEdge.Top, offset: 0, width: 2);
            var generator = CreateGenerator(1, BoardEdge.Bottom, offset: 0, width: 2);

            Assert.DoesNotThrow(
                () => CreateContext(6, 6, gates: new[] { gate }, generators: new[] { generator }));
        }

        [Test]
        public void Constructor_DuplicateElevatorIds_Throws()
        {
            var elevatorA = new ElevatorDefinition(1, new Coord(0, 0), new Coord(0, 0), Array.Empty<IReadOnlyList<SpawnedBlock>>());
            var elevatorB = new ElevatorDefinition(1, new Coord(2, 2), new Coord(2, 2), Array.Empty<IReadOnlyList<SpawnedBlock>>());

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, elevators: new[] { elevatorA, elevatorB }));
        }

        [Test]
        public void Constructor_DuplicateLockIdAcrossTwoBlocks_Throws()
        {
            var a = CreateBlock(1, new Coord(0, 0), lockId: 5, requiredKeyCount: 1);
            var b = CreateBlock(2, new Coord(1, 0), lockId: 5, requiredKeyCount: 1);

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, new[] { a, b }));
        }

        [Test]
        public void Constructor_DuplicateLockIdBetweenBlockAndGeneratorSpawnedBlock_Throws()
        {
            var block = CreateBlock(1, new Coord(0, 0), lockId: 5, requiredKeyCount: 1);
            var generator = new GeneratorDefinition(
                id: 1,
                edge: BoardEdge.Top,
                offset: 0,
                width: 1,
                queue: new[] { CreateSpawnedBlock(lockId: 5, requiredKeyCount: 1) });

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, new[] { block }, generators: new[] { generator }));
        }

        [Test]
        public void Constructor_OverlappingShutterRegions_Throws()
        {
            var shutterA = new ShutterDefinition(1, new Coord(0, 0), new Coord(2, 2), 1, null);
            var shutterB = new ShutterDefinition(2, new Coord(1, 1), new Coord(3, 3), 1, null);

            Assert.Throws<ArgumentException>(() => CreateContext(5, 5, shutters: new[] { shutterA, shutterB }));
        }

        [Test]
        public void Constructor_ShutterMaxFarOutsideGrid_ThrowsFromBoundsCheck()
        {
            // Bounds must be validated before the position lookup is built —
            // otherwise this Max would make the lookup iterate on the order of
            // 10^12 cells before ever getting a chance to reject it.
            var shutter = new ShutterDefinition(1, new Coord(0, 0), new Coord(1_000_000, 1_000_000), 1, null);

            Assert.Throws<ArgumentException>(() => CreateContext(5, 5, shutters: new[] { shutter }));
        }

        [Test]
        public void Constructor_LockSatisfiedByTwoBlocksSharingTheSameKeyTargetLockId_Succeeds()
        {
            var locked = CreateBlock(1, new Coord(0, 0), lockId: 5, requiredKeyCount: 2);
            var keyA = CreateBlock(2, new Coord(1, 0), keyTargetLockId: 5);
            var keyB = CreateBlock(3, new Coord(2, 0), keyTargetLockId: 5);

            var context = CreateContext(3, 3, new[] { locked, keyA, keyB });

            Assert.AreEqual(3, context.Blocks.Count);
        }

        [Test]
        public void Constructor_StaticWallOutsideGrid_Throws()
        {
            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, staticWalls: new[] { new Coord(5, 5) }));
        }

        [Test]
        public void Constructor_DuplicateStaticWalls_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => CreateContext(3, 3, staticWalls: new[] { new Coord(1, 1), new Coord(1, 1) }));
        }

        [Test]
        public void Constructor_NonNormalisedBlockWithOutOfGridStartOrigin_ValidatesAndPlacesFootprintCorrectly()
        {
            // Cells sit far to the left of their origin; the origin itself is
            // outside the grid. Normalisation (D30) shifts the cells to a (0,0)
            // minimum and compensates StartOrigin, and the block's absolute
            // footprint lands at (0,0)-(1,0), fully inside the 3x3 grid.
            var block = CreateBlock(
                1, new Coord(5, 0), cells: new[] { new Coord(-5, 0), new Coord(-4, 0) });

            var context = CreateContext(3, 3, new[] { block });

            var occupied = new HashSet<Coord>(
                BoardState.CreateInitial(context).OccupiedCells(context, 0));

            Assert.AreEqual(new Coord(0, 0), context.Blocks[0].StartOrigin);
            CollectionAssert.AreEquivalent(new[] { new Coord(0, 0), new Coord(1, 0) }, occupied);
        }

        [Test]
        public void SpecAt_TopLevelBlockIndex_ReturnsThatBlocksCellsAndColorStack()
        {
            var cells = new[] { new Coord(0, 0), new Coord(1, 0) };
            var block = CreateBlock(1, new Coord(0, 0), cells: cells);

            var context = CreateContext(3, 3, new[] { block });
            var spec = context.SpecAt(0);

            Assert.AreEqual(2, spec.Cells.Count);
            Assert.AreEqual(BlockColor.Red, spec.ColorStack[0]);
        }

        [Test]
        public void SpecAt_GeneratorSpawnIndex_ReturnsGeneratorsQueuedSpec()
        {
            var generator = new GeneratorDefinition(
                id: 1, edge: BoardEdge.Top, offset: 0, width: 1, queue: new[] { CreateSpawnedBlock() });

            var context = CreateContext(3, 3, generators: new[] { generator });
            var spec = context.SpecAt(0);

            Assert.AreEqual(BlockColor.Blue, spec.ColorStack[0]);
        }

        [Test]
        public void SpecAt_ElevatorSpawnIndex_ReturnsElevatorsWaveSpec()
        {
            var elevator = new ElevatorDefinition(
                1, new Coord(0, 0), new Coord(0, 0),
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { CreateSpawnedBlock(regionOrigin: new Coord(0, 0)) }
                });

            var context = CreateContext(3, 3, elevators: new[] { elevator });
            var spec = context.SpecAt(0);

            Assert.AreEqual(BlockColor.Blue, spec.ColorStack[0]);
        }

        [Test]
        public void TotalBlockCapacity_CountsTopLevelBlocksGeneratorQueueAndElevatorWaves()
        {
            var blockA = CreateBlock(1, new Coord(0, 0));
            var blockB = CreateBlock(2, new Coord(1, 0));
            var generator = new GeneratorDefinition(
                1, BoardEdge.Top, 0, 1, new[] { CreateSpawnedBlock(), CreateSpawnedBlock() });
            var elevator = new ElevatorDefinition(
                1, new Coord(0, 2), new Coord(0, 2),
                new IReadOnlyList<SpawnedBlock>[]
                {
                    new[] { CreateSpawnedBlock(regionOrigin: new Coord(0, 0)) }
                });

            var context = CreateContext(
                3, 3, new[] { blockA, blockB }, generators: new[] { generator }, elevators: new[] { elevator });

            Assert.AreEqual(5, context.TotalBlockCapacity);
        }

        [Test]
        public void ShutterPositionAt_CellCoveredBySecondShutter_ReturnsItsListPosition()
        {
            var shutterA = new ShutterDefinition(1, new Coord(0, 0), new Coord(0, 0), 1, null);
            var shutterB = new ShutterDefinition(2, new Coord(2, 2), new Coord(2, 2), 1, null);

            var context = CreateContext(3, 3, shutters: new[] { shutterA, shutterB });

            Assert.AreEqual(1, context.ShutterPositionAt(new Coord(2, 2)));
            Assert.AreEqual(2, context.ShutterAt(new Coord(2, 2)));
        }

        [Test]
        public void ShutterPositionAt_CellNotCoveredByAnyShutter_ReturnsNull()
        {
            var shutter = new ShutterDefinition(1, new Coord(0, 0), new Coord(0, 0), 1, null);

            var context = CreateContext(3, 3, shutters: new[] { shutter });

            Assert.IsNull(context.ShutterPositionAt(new Coord(2, 2)));
        }

        [Test]
        public void LockOwnerIndex_ReturnsTheFlatIndexOfTheBlockOwningThatLock()
        {
            var key = CreateBlock(1, new Coord(0, 0), keyTargetLockId: 5);
            var locked = CreateBlock(2, new Coord(1, 0), lockId: 5, requiredKeyCount: 1);

            var context = CreateContext(3, 3, new[] { key, locked });

            Assert.AreEqual(1, context.LockOwnerIndex(5));
        }

        [Test]
        public void LockOwnerIndex_ResolvesAcrossTheFlatIndexSpaceIncludingSpawnSlots()
        {
            var key = CreateBlock(1, new Coord(0, 0), keyTargetLockId: 5);
            var generator = new GeneratorDefinition(
                id: 1, edge: BoardEdge.Top, offset: 0, width: 1,
                queue: new[] { CreateSpawnedBlock(lockId: 5, requiredKeyCount: 1) });

            var context = CreateContext(3, 3, new[] { key }, generators: new[] { generator });

            Assert.AreEqual(1, context.LockOwnerIndex(5));
        }

        [Test]
        public void LockOwnerIndex_UnknownLockId_Throws()
        {
            var context = CreateContext(3, 3);

            Assert.Throws<ArgumentException>(() => context.LockOwnerIndex(42));
        }

        [Test]
        public void KeyIndicesForLock_ReturnsEveryBlockCarryingAKeyForThatLock_InIndexOrder()
        {
            var locked = CreateBlock(1, new Coord(0, 0), lockId: 5, requiredKeyCount: 2);
            var keyA = CreateBlock(2, new Coord(1, 0), keyTargetLockId: 5);
            var keyB = CreateBlock(3, new Coord(2, 0), keyTargetLockId: 5);

            var context = CreateContext(3, 3, new[] { locked, keyA, keyB });

            CollectionAssert.AreEqual(new[] { 1, 2 }, context.KeyIndicesForLock(5));
        }

        [Test]
        public void KeyIndicesForLock_LockIdNothingTargets_ReturnsEmpty()
        {
            var context = CreateContext(3, 3);

            CollectionAssert.IsEmpty(context.KeyIndicesForLock(999));
        }

        [Test]
        public void LockOwnerIndices_ListsEveryLockOwnerIncludingSpawnSlots_InAscendingIndexOrder()
        {
            var lockedLate = CreateBlock(1, new Coord(0, 0), lockId: 7, requiredKeyCount: 1);
            var plain = CreateBlock(2, new Coord(1, 0));
            var lockedEarly = CreateBlock(3, new Coord(2, 0), lockId: 3, requiredKeyCount: 1);
            var keyFor7 = CreateBlock(4, new Coord(0, 1), keyTargetLockId: 7);
            var keyFor3 = CreateBlock(5, new Coord(1, 1), keyTargetLockId: 3);
            var keyFor9 = CreateBlock(6, new Coord(2, 1), keyTargetLockId: 9);
            var generator = new GeneratorDefinition(
                id: 1, edge: BoardEdge.Bottom, offset: 0, width: 1,
                queue: new[] { CreateSpawnedBlock(lockId: 9, requiredKeyCount: 1) });

            var context = CreateContext(
                3, 3, new[] { lockedLate, plain, lockedEarly, keyFor7, keyFor3, keyFor9 },
                generators: new[] { generator });

            CollectionAssert.AreEqual(new[] { 0, 2, 6 }, context.LockOwnerIndices);
        }

        [Test]
        public void LockOwnerIndices_LevelWithNoLocks_IsEmpty()
        {
            var context = CreateContext(3, 3, new[] { CreateBlock(1, new Coord(0, 0)) });

            CollectionAssert.IsEmpty(context.LockOwnerIndices);
        }

        // ----- IsClearMonotone ----------------------------------------------

        [Test]
        public void IsClearMonotone_NoGeneratorOrElevator_IsTrue()
        {
            var context = CreateContext(3, 3, new[] { CreateBlock(1, new Coord(0, 0)) });

            Assert.IsTrue(context.IsClearMonotone);
        }

        [Test]
        public void IsClearMonotone_LockWhoseKeysShareOneEffect_IsTrue()
        {
            var locked = CreateBlock(1, new Coord(0, 0), lockId: 1, requiredKeyCount: 2);
            var keyA = CreateBlock(2, new Coord(1, 0), keyTargetLockId: 1);
            var keyB = CreateBlock(3, new Coord(2, 0), keyTargetLockId: 1);

            var context = CreateContext(3, 3, new[] { locked, keyA, keyB });

            Assert.IsTrue(context.IsClearMonotone);
        }

        [Test]
        public void IsClearMonotone_LockWhoseKeysCarryDifferentEffects_IsFalse()
        {
            var context = SearchCorpus.WastedClearKeyTrapBoard();

            Assert.IsFalse(context.IsClearMonotone);
        }

        [Test]
        public void IsClearMonotone_WithAGenerator_IsFalse()
        {
            var generator = new GeneratorDefinition(1, BoardEdge.Top, 0, 1, new[] { CreateSpawnedBlock() });

            var context = CreateContext(3, 3, generators: new[] { generator });

            Assert.IsFalse(context.IsClearMonotone);
        }

        [Test]
        public void IsClearMonotone_WithAnElevator_IsFalse()
        {
            var elevator = new ElevatorDefinition(
                1, new Coord(0, 0), new Coord(0, 0),
                new IReadOnlyList<SpawnedBlock>[] { new[] { CreateSpawnedBlock(regionOrigin: new Coord(0, 0)) } });

            var context = CreateContext(3, 3, elevators: new[] { elevator });

            Assert.IsFalse(context.IsClearMonotone);
        }
    }
}
