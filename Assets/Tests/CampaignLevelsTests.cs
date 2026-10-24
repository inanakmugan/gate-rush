using System;
using GateRush.Meta;
using GateRush.Runtime;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 32's <see cref="CampaignLevels"/>: the campaign position
    /// is an index into the level order, so position 0 is the first level and
    /// the last position the last; one past it means the campaign is over;
    /// and with no usable level order there is no campaign level. Module 49's
    /// <see cref="CampaignLevels.ChooseStart"/>: a finished campaign is over
    /// whatever the lives and changes nothing; otherwise the level starts
    /// with a life, and nothing starts without one.
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

        private static readonly DateTime Noon = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

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

        // ----- Module 49: what starting the campaign level does --------------

        [Test]
        public void ChooseStart_CampaignNotFinishedWithALife_StartsTheCampaignLevel()
        {
            var meta = Meta(position: 1, lives: 1);

            var start = CampaignLevels.ChooseStart(Catalog(), meta, out var name);

            Assert.AreEqual(CampaignStart.Level, start);
            Assert.AreEqual(NamesInOrder[1], name);
        }

        [Test]
        public void ChooseStart_LastLevelWithALife_StartsIt()
        {
            var meta = Meta(position: NamesInOrder.Length - 1, lives: 1);

            var start = CampaignLevels.ChooseStart(Catalog(), meta, out var name);

            Assert.AreEqual(CampaignStart.Level, start, "the last level is still played; only past it is the campaign over");
            Assert.AreEqual(NamesInOrder[NamesInOrder.Length - 1], name);
        }

        [Test]
        public void ChooseStart_CampaignNotFinishedWithNoLife_StartsNothing()
        {
            var meta = Meta(position: 1, lives: 0);

            var start = CampaignLevels.ChooseStart(Catalog(), meta, out var name);

            Assert.AreEqual(CampaignStart.NoLife, start);
            Assert.IsNull(name);
        }

        [TestCase(1)]
        [TestCase(0)]
        public void ChooseStart_CampaignFinished_IsOverAndStartsNothingWhateverTheLives(int lives)
        {
            var meta = Meta(position: NamesInOrder.Length, lives: lives);

            var start = CampaignLevels.ChooseStart(Catalog(), meta, out var name);

            Assert.AreEqual(CampaignStart.Over, start, "a finished campaign never asks for a life");
            Assert.IsNull(name);
        }

        [Test]
        public void ChooseStart_CampaignFinished_ChangesNothingInTheSave()
        {
            var meta = Meta(position: NamesInOrder.Length, lives: 1);
            var before = new JsonSaveCodec().Encode(meta.Save);

            CampaignLevels.ChooseStart(Catalog(), meta, out _);

            Assert.AreEqual(before, new JsonSaveCodec().Encode(meta.Save));
        }

        [Test]
        public void ChooseStart_NoMeta_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignLevels.ChooseStart(Catalog(), null, out _));
        }

        private static LevelCatalog Catalog() => new LevelCatalog(Levels);

        /// <summary>A meta game with the campaign at <paramref name="position"/> and <paramref name="lives"/> lives, read at the moment they were saved.</summary>
        private static MetaGame Meta(int position, int lives)
        {
            var rules = MetaRulesFixture.Build();
            var save = new SaveData(
                CompletedLevels.Of(Array.Empty<int>()),
                new SoundSetting(),
                Wallet.Starting(rules),
                new Lives(rules, lives, Noon),
                JokerInventory.Starting(rules),
                WinStreak.Starting(),
                new CampaignProgress(position),
                PlayerProfile.Starting(rules),
                new HapticsSetting());
            return new MetaGame(save, rules, new ManualClock(Noon));
        }
    }
}
