using System;
using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// <see cref="SpawnedBlock"/> shares its construction-time validation with
    /// <see cref="BlockDefinition"/> through <c>BlockValidation</c>. This covers
    /// that the shared rules fire for spawner output too, and that the message
    /// names the spawned block rather than a block id it does not have.
    /// </summary>
    public class SpawnedBlockTests
    {
        [Test]
        public void Constructor_CarriesBothALockAndAKey_ThrowsNamingTheSpawnedBlock()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => Spawned(lockId: 7, requiredKeys: 1, keyTarget: 3));

            StringAssert.Contains("Spawned block", ex.Message);
        }

        [Test]
        public void Constructor_CarriesOnlyALock_Succeeds()
        {
            var spawned = Spawned(lockId: 7, requiredKeys: 1);

            Assert.AreEqual(7, spawned.LockId);
            Assert.IsNull(spawned.KeyTargetLockId);
        }

        [Test]
        public void Constructor_CarriesOnlyAKey_Succeeds()
        {
            var spawned = Spawned(keyTarget: 3);

            Assert.AreEqual(3, spawned.KeyTargetLockId);
            Assert.IsNull(spawned.LockId);
        }
    }
}
