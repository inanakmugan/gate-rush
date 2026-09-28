using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 12's <see cref="LevelCatalog"/>: the next level is the one
    /// with the smallest greater id — not the next file name — ids may skip,
    /// the highest id has no next, and two files sharing an id are refused.
    /// </summary>
    public class LevelCatalogTests
    {
        [Test]
        public void TryGetNext_FileNamesOutOfIdOrder_FollowsTheIds()
        {
            // By name, level-b follows level-a; by id, level-c does.
            var catalog = new LevelCatalog(new[] { ("level-a", 1), ("level-b", 3), ("level-c", 2) });

            var found = catalog.TryGetNext(1, out var name);

            Assert.IsTrue(found);
            Assert.AreEqual("level-c", name);
        }

        [Test]
        public void TryGetNext_HighestId_HasNoNext()
        {
            var catalog = new LevelCatalog(new[] { ("level-0", 0), ("level-1", 1) });

            var found = catalog.TryGetNext(1, out var name);

            Assert.IsFalse(found);
            Assert.IsNull(name);
        }

        [Test]
        public void TryGetNext_NonContiguousIds_SkipsTheGap()
        {
            var catalog = new LevelCatalog(new[] { ("level-0", 0), ("level-1", 1), ("level-3", 3), ("level-4", 4) });

            var found = catalog.TryGetNext(1, out var name);

            Assert.IsTrue(found);
            Assert.AreEqual("level-3", name);
        }

        [Test]
        public void Constructor_TwoLevelsSharingAnId_ThrowsNamingBoth()
        {
            var levels = new[] { ("level-a", 0), ("level-b", 1), ("level-c", 1) };

            var error = Assert.Throws<ArgumentException>(() => new LevelCatalog(levels));

            StringAssert.Contains("level-b", error.Message);
            StringAssert.Contains("level-c", error.Message);
        }
    }
}
