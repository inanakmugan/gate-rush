using System;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 35's <see cref="FrontEndState.Of"/>, the one place the
    /// front end's numbers are read: the gold, the lives and the cap from the
    /// save at the time given, with no wait when full; the campaign level's
    /// 1-based number, null past the last level and with no usable order;
    /// whether the refill is possible; and the level list with its marks.
    /// </summary>
    /// <remarks>
    /// The rules are <see cref="MetaRulesFixture"/>'s, unlike a fresh config,
    /// so a number taken from anywhere but the rules fails. The catalog's ids
    /// are 0, 1, 3 and 4, given out of order: the order is by id.
    /// </remarks>
    public class FrontEndStateTests
    {
        private const int Gold = 640;
        private const int SomeLives = 2;

        private static readonly DateTime Noon = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

        private static readonly (string name, int levelId)[] Levels =
        {
            ("level-d", 4), ("level-a", 0), ("level-c", 3), ("level-b", 1)
        };

        private static readonly string[] NamesInOrder = { "level-a", "level-b", "level-c", "level-d" };
        private static readonly int[] IdsInOrder = { 0, 1, 3, 4 };

        private MetaRules rules;

        [SetUp]
        public void SetUp()
        {
            rules = MetaRulesFixture.Build();
        }

        [Test]
        public void Of_SaveAndRules_GoldLivesAndCapComeFromThem()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true);

            Assert.AreEqual(Gold, state.Gold);
            Assert.AreEqual(SomeLives, state.Lives);
            Assert.AreEqual(rules.LifeCap, state.LifeCap);
        }

        [Test]
        public void Of_LivesCameBackByTheGivenTime_CountsThem()
        {
            var meta = Meta(Gold, 1);
            var twoIntervalsLater = Noon + rules.LifeRegenInterval + rules.LifeRegenInterval + OneSecond;

            var state = FrontEndState.Of(meta, rules, twoIntervalsLater, Catalog(), true);

            Assert.AreEqual(3, state.Lives);
        }

        [Test]
        public void Of_FullLives_HaveNoWait()
        {
            var meta = Meta(Gold, rules.LifeCap);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true);

            Assert.IsNull(state.UntilNextLife);
        }

        [Test]
        public void Of_LivesBelowTheCap_WaitIsWhatIsLeftOfTheInterval()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon + FiveMinutes, Catalog(), true);

            Assert.AreEqual(rules.LifeRegenInterval - FiveMinutes, state.UntilNextLife);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void Of_CampaignPosition_NumberIsOneBased(int position)
        {
            var meta = Meta(Gold, SomeLives, position);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true);

            Assert.AreEqual(position + 1, state.CampaignLevelNumber);
        }

        [Test]
        public void Of_PositionPastTheLastLevel_NumberIsNull()
        {
            var meta = Meta(Gold, SomeLives, NamesInOrder.Length);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true);

            Assert.IsNull(state.CampaignLevelNumber);
        }

        [Test]
        public void Of_NoCatalog_NoNumberAndNoLevels()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, null, true);

            Assert.IsNull(state.CampaignLevelNumber);
            Assert.IsNotNull(state.Levels);
            Assert.AreEqual(0, state.Levels.Count);
        }

        [Test]
        public void Of_EmptyCatalog_NoNumberAndNoLevels()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, new LevelCatalog(Array.Empty<(string, int)>()), true);

            Assert.IsNull(state.CampaignLevelNumber);
            Assert.AreEqual(0, state.Levels.Count);
        }

        [Test]
        public void Of_FullLives_CannotRefill()
        {
            var meta = Meta(rules.LivesRefillPrice + Gold, rules.LifeCap);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true);

            Assert.IsFalse(state.CanRefill);
        }

        [Test]
        public void Of_BalanceBelowThePrice_CannotRefill()
        {
            var meta = Meta(rules.LivesRefillPrice - 1, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true);

            Assert.IsFalse(state.CanRefill);
        }

        [Test]
        public void Of_LivesBelowTheCapAndBalanceExactlyThePrice_CanRefill()
        {
            var meta = Meta(rules.LivesRefillPrice, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true);

            Assert.IsTrue(state.CanRefill);
        }

        [Test]
        public void Of_RefillPrice_IsTheRulesPrice()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true);

            Assert.AreEqual(rules.LivesRefillPrice, state.RefillPrice);
        }

        [Test]
        public void Of_Levels_ListTheOrderWithNumbersAndCompletedMarks()
        {
            var catalog = Catalog();
            var meta = Meta(Gold, SomeLives, 0, IdsInOrder[1], IdsInOrder[3]);

            var state = FrontEndState.Of(meta, rules, Noon, catalog, true);

            Assert.AreEqual(NamesInOrder.Length, state.Levels.Count, "every catalog level is listed");
            for (var i = 0; i < NamesInOrder.Length; i++)
            {
                var entry = state.Levels[i];
                Assert.AreEqual(NamesInOrder[i], entry.Name, $"entry {i}");
                Assert.IsTrue(catalog.TryGetNumber(entry.Name, out var number), $"entry {i} is a catalog level");
                Assert.AreEqual(number, entry.Number, $"entry {i} carries the catalog's number");
                Assert.AreEqual(meta.Save.Completed.IsCompleted(IdsInOrder[i]), entry.IsCompleted, $"entry {i}");
            }
        }

        [Test]
        public void Of_SoundSetting_IsTheValueGiven([Values] bool isSoundOn)
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), isSoundOn);

            Assert.AreEqual(isSoundOn, state.IsSoundOn);
        }

        [Test]
        public void Of_NullMeta_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => FrontEndState.Of(null, rules, Noon, Catalog(), true));
        }

        [Test]
        public void Of_NullRules_Throws()
        {
            var meta = Meta(Gold, SomeLives);

            Assert.Throws<ArgumentNullException>(() => FrontEndState.Of(meta, null, Noon, Catalog(), true));
        }

        private static LevelCatalog Catalog() => new LevelCatalog(Levels);

        /// <summary>
        /// A meta game over a save with <paramref name="gold"/>,
        /// <paramref name="lives"/> waiting since noon, the campaign at
        /// <paramref name="position"/> and <paramref name="completedIds"/> won.
        /// </summary>
        private MetaGame Meta(int gold, int lives, int position = 0, params int[] completedIds)
        {
            var save = new SaveData(
                CompletedLevels.Of(completedIds),
                new SoundSetting(),
                new Wallet(gold, 0),
                new Lives(rules, lives, Noon),
                new JokerInventory(0, 0, 0),
                new WinStreak(0, 0),
                new CampaignProgress(position));
            return new MetaGame(save, rules, new ManualClock(Noon));
        }
    }
}
