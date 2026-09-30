using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 12's <see cref="LevelCatalog"/>: the next level is the one
    /// with the smallest greater id — not the next file name — ids may skip,
    /// the highest id has no next, and two files sharing an id are refused.
    /// Module 17: a level's number is its 1-based position in id order, and a
    /// file the catalog does not hold has none.
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

        [Test]
        public void TryGetNumber_IdsZeroOneThreeFour_IdThreeIsNumberThree()
        {
            // Given out of id order, so the number cannot come from the input's order.
            var catalog = new LevelCatalog(new[] { ("level-4", 4), ("level-1", 1), ("level-3", 3), ("level-0", 0) });

            var found = catalog.TryGetNumber("level-3", out var number);

            Assert.IsTrue(found);
            Assert.AreEqual(3, number);
        }

        [Test]
        public void TryGetNumber_LowestId_IsNumberOne()
        {
            var catalog = new LevelCatalog(new[] { ("level-a", 5), ("level-b", 2) });

            var found = catalog.TryGetNumber("level-b", out var number);

            Assert.IsTrue(found);
            Assert.AreEqual(1, number);
        }

        [Test]
        public void TryGetNumber_UnknownName_HasNoNumber()
        {
            var catalog = new LevelCatalog(new[] { ("level-0", 0), ("level-1", 1) });

            var found = catalog.TryGetNumber("level-9", out var number);

            Assert.IsFalse(found);
            Assert.AreEqual(0, number);
        }

        [Test]
        public void TryGetNumber_NullName_Throws()
        {
            var catalog = new LevelCatalog(new[] { ("level-0", 0) });

            Assert.Throws<ArgumentNullException>(() => catalog.TryGetNumber(null, out _));
        }
    }
}
