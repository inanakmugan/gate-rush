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
    /// file the catalog does not hold has none. Module 19: the names come out
    /// in id order. Module 21: the previous level is the one with the largest
    /// smaller id, and the lowest id has none.
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
        public void TryGetPrevious_FileNamesOutOfIdOrder_FollowsTheIds()
        {
            // By name, level-b precedes level-c; by id, level-a does.
            var catalog = new LevelCatalog(new[] { ("level-a", 2), ("level-b", 1), ("level-c", 3) });

            var found = catalog.TryGetPrevious(3, out var name);

            Assert.IsTrue(found);
            Assert.AreEqual("level-a", name);
        }

        [Test]
        public void TryGetPrevious_LowestId_HasNoPrevious()
        {
            var catalog = new LevelCatalog(new[] { ("level-0", 0), ("level-1", 1) });

            var found = catalog.TryGetPrevious(0, out var name);

            Assert.IsFalse(found);
            Assert.IsNull(name);
        }

        [Test]
        public void TryGetPrevious_NonContiguousIds_SkipsTheGap()
        {
            var catalog = new LevelCatalog(new[] { ("level-0", 0), ("level-1", 1), ("level-3", 3), ("level-4", 4) });

            var found = catalog.TryGetPrevious(3, out var name);

            Assert.IsTrue(found);
            Assert.AreEqual("level-1", name);
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
        public void Names_AreInIdOrder()
        {
            // Given out of id order, and with names whose own order differs from their ids'.
            var catalog = new LevelCatalog(new[] { ("level-b", 4), ("level-d", 1), ("level-a", 3), ("level-c", 0) });

            var names = catalog.Names;

            CollectionAssert.AreEqual(new[] { "level-c", "level-d", "level-a", "level-b" }, names);
        }

        [Test]
        public void Ids_AreAscendingAndParallelToNames()
        {
            var catalog = new LevelCatalog(new[] { ("level-b", 4), ("level-d", 1), ("level-a", 3), ("level-c", 0) });

            var ids = catalog.Ids;

            CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, ids);
            Assert.AreEqual(catalog.Names.Count, ids.Count);

            // The level just before the one after id i is the level with id
            // i, so its name must be the name at index i.
            for (var i = 0; i + 1 < ids.Count; i++)
            {
                Assert.IsTrue(catalog.TryGetPrevious(ids[i + 1], out var name), $"id {ids[i + 1]} has a previous level");
                Assert.AreEqual(catalog.Names[i], name, $"id {ids[i]}");
            }
        }

        [Test]
        public void TryGetNumber_NullName_Throws()
        {
            var catalog = new LevelCatalog(new[] { ("level-0", 0) });

            Assert.Throws<ArgumentNullException>(() => catalog.TryGetNumber(null, out _));
        }
    }
}
