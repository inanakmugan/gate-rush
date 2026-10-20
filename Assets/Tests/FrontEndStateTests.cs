using System;
using GateRush.Meta;
using GateRush.Platform;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Modules 35, 36 and 37's <see cref="FrontEndState.Of"/>, the one
    /// place the front end's numbers are read: the gold, the lives and the cap
    /// from the save at the time given, with no wait when full; the campaign
    /// level's 1-based number, null past the last level and with no usable
    /// order; whether the lives are full, whatever the balance; the level list
    /// with its marks; the jokers' counts and prices; and the gold offers,
    /// only packs both sides know, in the rules' order with the service's
    /// price text; the win streak and its best from the save, and the reward
    /// table from the rules. Module 38: the name, the avatar id, the haptics
    /// setting apart from the sound, the levels completed and the lifetime
    /// gold from the save.
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
        private const int ClockCount = 4;
        private const int RocketCount = 7;
        private const int BroomCount = 9;
        private const string SmallPrice = "$0.10";
        private const string LargePrice = "$0.90";

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

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.AreEqual(Gold, state.Gold);
            Assert.AreEqual(SomeLives, state.Lives);
            Assert.AreEqual(rules.LifeCap, state.LifeCap);
        }

        [Test]
        public void Of_LivesCameBackByTheGivenTime_CountsThem()
        {
            var meta = Meta(Gold, 1);
            var twoIntervalsLater = Noon + rules.LifeRegenInterval + rules.LifeRegenInterval + OneSecond;

            var state = FrontEndState.Of(meta, rules, twoIntervalsLater, Catalog(), true, Products());

            Assert.AreEqual(3, state.Lives);
        }

        [Test]
        public void Of_SaveProfile_NameAndAvatarComeFromIt()
        {
            var meta = MetaOfEveryField();

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.AreEqual(SaveFixture.EveryFieldName, state.PlayerName);
            Assert.AreEqual(SaveFixture.EveryFieldAvatarId, state.AvatarId);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Of_SaveHaptics_IsHapticsOnComesFromItNotFromTheSound(bool isHapticsOn)
        {
            var meta = Meta(Gold, SomeLives);
            meta.Save.Haptics.IsOn = isHapticsOn;

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), !isHapticsOn, Products());

            Assert.AreEqual(isHapticsOn, state.IsHapticsOn);
            Assert.AreEqual(!isHapticsOn, state.IsSoundOn, "the two settings stay apart");
        }

        [Test]
        public void Of_SaveStatistics_LevelsCompletedAndLifetimeGoldComeFromIt()
        {
            var meta = MetaOfEveryField();
            var save = meta.Save;

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.AreEqual(save.Completed.Count, state.LevelsCompleted, "every completed mark of the save, as it holds them");
            Assert.AreEqual(save.Wallet.LifetimeEarned, state.LifetimeGold);
            Assert.AreNotEqual(save.Wallet.Balance, state.LifetimeGold, "the lifetime gold, not the balance");
        }

        [Test]
        public void Of_SaveStreak_StreakAndBestComeFromIt()
        {
            var meta = MetaWithStreak(new WinStreak(4, 9));

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.AreEqual(4, state.Streak);
            Assert.AreEqual(9, state.BestStreak);
        }

        [Test]
        public void Of_Rules_StreakRewardsAreTheRulesTableInOrder()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            CollectionAssert.AreEqual(rules.StreakRewards, state.StreakRewards);
        }

        [Test]
        public void Of_FullLives_HaveNoWait()
        {
            var meta = Meta(Gold, rules.LifeCap);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.IsNull(state.UntilNextLife);
        }

        [Test]
        public void Of_LivesBelowTheCap_WaitIsWhatIsLeftOfTheInterval()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon + FiveMinutes, Catalog(), true, Products());

            Assert.AreEqual(rules.LifeRegenInterval - FiveMinutes, state.UntilNextLife);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void Of_CampaignPosition_NumberIsOneBased(int position)
        {
            var meta = Meta(Gold, SomeLives, position);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.AreEqual(position + 1, state.CampaignLevelNumber);
        }

        [Test]
        public void Of_PositionPastTheLastLevel_NumberIsNull()
        {
            var meta = Meta(Gold, SomeLives, NamesInOrder.Length);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.IsNull(state.CampaignLevelNumber);
        }

        [Test]
        public void Of_NoCatalog_NoNumberAndNoLevels()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, null, true, Products());

            Assert.IsNull(state.CampaignLevelNumber);
            Assert.IsNotNull(state.Levels);
            Assert.AreEqual(0, state.Levels.Count);
        }

        [Test]
        public void Of_EmptyCatalog_NoNumberAndNoLevels()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, new LevelCatalog(Array.Empty<(string, int)>()), true, Products());

            Assert.IsNull(state.CampaignLevelNumber);
            Assert.AreEqual(0, state.Levels.Count);
        }

        [Test]
        public void Of_FullLives_AreFull()
        {
            var meta = Meta(rules.LivesRefillPrice + Gold, rules.LifeCap);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.IsTrue(state.IsLivesFull);
        }

        [Test]
        public void Of_LivesBelowTheCapWithNoGold_AreNotFull()
        {
            var meta = Meta(0, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.IsFalse(state.IsLivesFull, "the refill is pressable whatever the balance");
        }

        [Test]
        public void Of_JokerCounts_ComeFromTheInventory([Values] JokerKind kind)
        {
            var meta = Meta(Gold, SomeLives, jokers: new JokerInventory(ClockCount, RocketCount, BroomCount));

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.AreEqual(meta.Save.Jokers.CountOf(kind), state.JokerCount(kind));
        }

        [Test]
        public void Of_JokerPrices_ComeFromTheRules([Values] JokerKind kind)
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.AreEqual(rules.JokerPrice(kind), state.JokerPrice(kind));
        }

        [Test]
        public void Of_GoldOffers_OnlyPacksTheServiceSells()
        {
            var meta = Meta(Gold, SomeLives);
            var onlyLarge = new[] { new StoreProduct(MetaRulesFixture.LargePackId, LargePrice), new StoreProduct("unknown", SmallPrice) };

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, onlyLarge);

            Assert.AreEqual(1, state.GoldOffers.Count);
            Assert.AreEqual(MetaRulesFixture.LargePackId, state.GoldOffers[0].ProductId);
        }

        [Test]
        public void Of_GoldOffers_InTheRulesOrderWithTheServicePriceText()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.AreEqual(rules.GoldPacks.Count, state.GoldOffers.Count);
            Assert.AreEqual(MetaRulesFixture.SmallPackId, state.GoldOffers[0].ProductId);
            Assert.AreEqual(MetaRulesFixture.SmallPackGold, state.GoldOffers[0].Gold);
            Assert.AreEqual(SmallPrice, state.GoldOffers[0].PriceText);
            Assert.AreEqual(MetaRulesFixture.LargePackId, state.GoldOffers[1].ProductId);
            Assert.AreEqual(MetaRulesFixture.LargePackGold, state.GoldOffers[1].Gold);
            Assert.AreEqual(LargePrice, state.GoldOffers[1].PriceText);
        }

        [Test]
        public void Of_NullProducts_Throws()
        {
            var meta = Meta(Gold, SomeLives);

            Assert.Throws<ArgumentNullException>(() => FrontEndState.Of(meta, rules, Noon, Catalog(), true, null));
        }

        [Test]
        public void Of_RefillPrice_IsTheRulesPrice()
        {
            var meta = Meta(Gold, SomeLives);

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), true, Products());

            Assert.AreEqual(rules.LivesRefillPrice, state.RefillPrice);
        }

        [Test]
        public void Of_Levels_ListTheOrderWithNumbersAndCompletedMarks()
        {
            var catalog = Catalog();
            var meta = Meta(Gold, SomeLives, 0, null, IdsInOrder[1], IdsInOrder[3]);

            var state = FrontEndState.Of(meta, rules, Noon, catalog, true, Products());

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

            var state = FrontEndState.Of(meta, rules, Noon, Catalog(), isSoundOn, Products());

            Assert.AreEqual(isSoundOn, state.IsSoundOn);
        }

        [Test]
        public void Of_NullMeta_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => FrontEndState.Of(null, rules, Noon, Catalog(), true, Products()));
        }

        [Test]
        public void Of_NullRules_Throws()
        {
            var meta = Meta(Gold, SomeLives);

            Assert.Throws<ArgumentNullException>(() => FrontEndState.Of(meta, null, Noon, Catalog(), true, Products()));
        }

        private static LevelCatalog Catalog() => new LevelCatalog(Levels);

        /// <summary>
        /// The purchase service's products: the fixture's two packs, listed
        /// in the opposite order to the rules', so an offer list in the
        /// service's order fails.
        /// </summary>
        private static StoreProduct[] Products() => new[]
        {
            new StoreProduct(MetaRulesFixture.LargePackId, LargePrice),
            new StoreProduct(MetaRulesFixture.SmallPackId, SmallPrice),
        };

        /// <summary>
        /// A meta game over a save with <paramref name="gold"/>,
        /// <paramref name="lives"/> waiting since noon, the campaign at
        /// <paramref name="position"/>, <paramref name="jokers"/> (none by
        /// default) and <paramref name="completedIds"/> won.
        /// </summary>
        private MetaGame Meta(int gold, int lives, int position = 0, JokerInventory jokers = null, params int[] completedIds)
        {
            var save = new SaveData(
                CompletedLevels.Of(completedIds),
                new SoundSetting(),
                new Wallet(gold, 0),
                new Lives(rules, lives, Noon),
                jokers ?? new JokerInventory(0, 0, 0),
                new WinStreak(0, 0),
                new CampaignProgress(position),
                PlayerProfile.Starting(rules),
                new HapticsSetting());
            return new MetaGame(save, rules, new ManualClock(Noon));
        }

        /// <summary>A meta game over <see cref="SaveFixture.EveryField"/>, whose sections are none at their default.</summary>
        private MetaGame MetaOfEveryField() => new MetaGame(SaveFixture.EveryField(), rules, new ManualClock(Noon));

        /// <summary>A meta game over a save with <paramref name="streak"/> and otherwise what <see cref="Meta"/> gives by default.</summary>
        private MetaGame MetaWithStreak(WinStreak streak)
        {
            var save = new SaveData(
                CompletedLevels.Of(Array.Empty<int>()),
                new SoundSetting(),
                new Wallet(Gold, 0),
                new Lives(rules, SomeLives, Noon),
                new JokerInventory(0, 0, 0),
                streak,
                new CampaignProgress(0),
                PlayerProfile.Starting(rules),
                new HapticsSetting());
            return new MetaGame(save, rules, new ManualClock(Noon));
        }
    }
}
