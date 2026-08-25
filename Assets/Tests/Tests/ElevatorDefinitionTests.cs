using System.Collections.Generic;
using GateRush.Core;
using NUnit.Framework;

namespace GateRush.Tests
{
    public class ElevatorDefinitionTests
    {
        private static SpawnedBlock CreateSpawnedBlock()
        {
            return new SpawnedBlock(
                cells: new[] { new Coord(0, 0) },
                colorStack: new[] { BlockColor.Blue },
                axis: MovementAxis.Free,
                unfreezeAtClearCount: null,
                lockId: null,
                requiredKeyCount: 0,
                keyTargetLockId: null,
                keyEffect: KeyEffect.UnlockMovement,
                timeBonusSeconds: 0);
        }

        [Test]
        public void Constructor_MutatingCallerWaveListAfterConstruction_DoesNotAffectStoredWaves()
        {
            var wave = new List<SpawnedBlock> { CreateSpawnedBlock() };
            var waves = new List<IReadOnlyList<SpawnedBlock>> { wave };

            var elevator = new ElevatorDefinition(1, new Coord(0, 0), new Coord(1, 1), waves);

            wave.Add(CreateSpawnedBlock());

            Assert.AreEqual(1, elevator.Waves[0].Count);
        }
    }
}
