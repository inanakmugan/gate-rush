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
            int requiredKeyCount = 0)
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
                timeBonusSeconds: 0);
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
            var generatorA = new GeneratorDefinition(1, BoardEdge.Top, 0, Array.Empty<SpawnedBlock>());
            var generatorB = new GeneratorDefinition(1, BoardEdge.Bottom, 0, Array.Empty<SpawnedBlock>());

            Assert.Throws<ArgumentException>(() => CreateContext(3, 3, generators: new[] { generatorA, generatorB }));
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
    }
}
