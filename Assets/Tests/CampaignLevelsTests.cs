using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 32's <see cref="CampaignLevels"/>: the campaign position
    /// is an index into the level order, so position 0 is the first level and
    /// the last position the last; one past it means the campaign is over;
    /// and with no usable level order there is no campaign level.
    /// </summary>
    /// <remarks>
    /// The catalog's ids are 0, 1, 3 and 4, given out of order: the position
    /// follows the order by id, not the ids nor the files.
    /// </remarks>
    public class CampaignLevelsTests
    {
        private static readonly (string name, int levelId)[] Levels =
        {
            ("level-d", 4), ("level-a", 0), ("level-c", 3), ("level-b", 1)
        };

        private static readonly string[] NamesInOrder = { "level-a", "level-b", "level-c", "level-d" };

        [Test]
        public void TryGetLevel_PositionZero_IsTheFirstLevelOfTheOrder()
        {
            var found = CampaignLevels.TryGetLevel(Catalog(), 0, out var name);

            Assert.IsTrue(found);
            Assert.AreEqual(NamesInOrder[0], name);
        }

        [Test]
        public void TryGetLevel_LastPosition_IsTheLastLevel()
        {
            var found = CampaignLevels.TryGetLevel(Catalog(), NamesInOrder.Length - 1, out var name);

            Assert.IsTrue(found);
            Assert.AreEqual(NamesInOrder[NamesInOrder.Length - 1], name);
        }

        [Test]
        public void TryGetLevel_OnePastTheLast_IsFalse()
        {
            var found = CampaignLevels.TryGetLevel(Catalog(), NamesInOrder.Length, out var name);

            Assert.IsFalse(found);
            Assert.IsNull(name);
        }

        [Test]
        public void TryGetLevel_NoCatalog_IsFalse()
        {
            var found = CampaignLevels.TryGetLevel(null, 0, out var name);

            Assert.IsFalse(found);
            Assert.IsNull(name);
        }

        [Test]
        public void TryGetLevel_CatalogWithNoLevels_IsFalse()
        {
            var empty = new LevelCatalog(Array.Empty<(string, int)>());

            var found = CampaignLevels.TryGetLevel(empty, 0, out var name);

            Assert.IsFalse(found);
            Assert.IsNull(name);
        }

        [Test]
        public void TryGetLevel_NonContiguousIds_FollowsTheOrderNotTheIds()
        {
            // Position 2 is the third level, id 3 — not a level with id 2,
            // which does not exist.
            var found = CampaignLevels.TryGetLevel(Catalog(), 2, out var name);

            Assert.IsTrue(found);
            Assert.AreEqual(NamesInOrder[2], name);
        }

        [Test]
        public void TryGetLevel_EveryPosition_MatchesTheCatalogsNumbers()
        {
            var catalog = Catalog();

            for (var position = 0; position < NamesInOrder.Length; position++)
            {
                CampaignLevels.TryGetLevel(catalog, position, out var name);

                Assert.IsTrue(catalog.TryGetNumber(name, out var number));
                Assert.AreEqual(position + 1, number, $"position {position}");
            }
        }

        [Test]
        public void TryGetLevel_NegativePosition_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CampaignLevels.TryGetLevel(Catalog(), -1, out _));
        }

        private static LevelCatalog Catalog() => new LevelCatalog(Levels);
    }
}
