using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 31's <see cref="MetaGame"/> against a
    /// <see cref="ManualClock"/>, with <see cref="MetaRulesFixture"/>'s rules:
    /// the roadmap's "win three, lose one, wait forty minutes", where one
    /// object applies D57, D58 and D61 together; a win pays gold, counts for
    /// the streak, pays its threshold's jokers, advances the campaign and
    /// spends no life; a loss costs one life and resets the streak, keeping
    /// the best and the position; and the store (D62) spends only what the
    /// balance covers and never touches the lifetime gold.
    /// </summary>
    public class MetaGameTests
    {
        private const int Reward = 45;
        private const int FirstReward = 40;
        private const int SecondReward = 55;
        private const int ThirdReward = 65;

        private static readonly DateTime Noon = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan FortyMinutes = TimeSpan.FromMinutes(40);

        private MetaRules rules;
        private ManualClock clock;

        [SetUp]
        public void SetUp()
        {
            rules = MetaRulesFixture.Build();
            clock = new ManualClock(Noon);
        }

        [Test]
        public void RoadmapCase_WinThreeLoseOneWaitForty_StreakGoldRewardsAndLivesFollow()
        {
            var game = Game(SaveData.CreateDefault(rules));
            var save = game.Save;
            var thirdWinReward = rules.StreakRewards[0];
            Assume.That(thirdWinReward.Wins, Is.EqualTo(3), "the test rules reward the third win");
            var jokersBefore = save.Jokers.CountOf(thirdWinReward.Joker);

            var first = game.ReportCampaignWin(FirstReward);
            var second = game.ReportCampaignWin(SecondReward);
            var third = game.ReportCampaignWin(ThirdReward);
            var isLost = game.ReportCampaignLoss();
            var livesAfterLoss = save.Lives.CountAt(clock.UtcNow);
            clock.Advance(rules.LifeRegenInterval - OneSecond);
            var isFullJustBeforeOneInterval = save.Lives.IsFullAt(clock.UtcNow);
            clock.Advance(FortyMinutes - rules.LifeRegenInterval + OneSecond);

            Assert.AreEqual(new[] { 1, 2, 3 }, new[] { first.Streak, second.Streak, third.Streak }, "the streak goes 1, 2, 3");
            Assert.AreEqual(0, save.Streak.Current, "the loss sets the streak to 0");
            Assert.AreEqual(3, save.Streak.Best, "and keeps the best");
            const int earned = FirstReward + SecondReward + ThirdReward;
            Assert.AreEqual(rules.StartingGold + earned, save.Wallet.Balance, "the balance rose by the three rewards");
            Assert.AreEqual(earned, save.Wallet.LifetimeEarned, "so did the lifetime gold");
            CollectionAssert.IsEmpty(first.RewardsGiven);
            CollectionAssert.IsEmpty(second.RewardsGiven);
            CollectionAssert.AreEqual(new[] { thirdWinReward }, third.RewardsGiven, "the third win gives the 3-win reward");
            Assert.AreEqual(jokersBefore + thirdWinReward.Count, save.Jokers.CountOf(thirdWinReward.Joker));
            Assert.AreEqual(3, game.CampaignPosition, "the loss leaves the position");
            Assert.IsTrue(isLost);
            Assert.AreEqual(rules.LifeCap - 1, livesAfterLoss, "the loss costs one life");
            Assert.IsFalse(isFullJustBeforeOneInterval);
            Assert.IsTrue(save.Lives.IsFullAt(clock.UtcNow), "forty minutes later the lives are full again");
            Assert.IsTrue(game.CanStartCampaignLevel());
        }

        [Test]
        public void ReportCampaignWin_Reward_PaysBalanceAndLifetimeAdvancesPositionAndSpendsNoLife()
        {
            var game = Game(SaveData.CreateDefault(rules));
            var save = game.Save;

            var win = game.ReportCampaignWin(Reward);

            Assert.AreEqual(Reward, win.GoldEarned);
            Assert.AreEqual(rules.StartingGold + Reward, save.Wallet.Balance);
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned);
            Assert.AreEqual(1, game.CampaignPosition);
            Assert.AreEqual(rules.LifeCap, save.Lives.CountAt(clock.UtcNow));
        }

        [Test]
        public void ReportCampaignWin_ZeroReward_IsAllowed()
        {
            var game = Game(SaveData.CreateDefault(rules));

            var win = game.ReportCampaignWin(0);

            Assert.AreEqual(0, win.GoldEarned);
            Assert.AreEqual(rules.StartingGold, game.Save.Wallet.Balance);
            Assert.AreEqual(1, win.Streak);
            Assert.AreEqual(1, game.CampaignPosition);
        }

        [Test]
        public void ReportCampaignWin_NegativeReward_ThrowsAndChangesNothing()
        {
            var game = Game(SaveData.CreateDefault(rules));
            var expected = SaveData.CreateDefault(rules);

            Assert.Throws<ArgumentOutOfRangeException>(() => game.ReportCampaignWin(-1));
            SaveFixture.AssertSameSave(expected, game.Save);
        }

        [Test]
        public void ReportCampaignWin_EmptyRewardTable_GivesNoRewards()
        {
            rules = MetaRulesFixture.Build(streakRewards: Array.Empty<StreakReward>());
            var game = Game(SaveData.CreateDefault(rules));
            var wouldBeThreshold = MetaRulesFixture.StreakRewards()[0].Wins;

            var win = WinTimes(game, wouldBeThreshold);

            Assert.AreEqual(wouldBeThreshold, win.Streak);
            CollectionAssert.IsEmpty(win.RewardsGiven);
            AssertSameJokers(JokerInventory.Starting(rules), game.Save.Jokers);
        }

        [Test]
        public void ReportCampaignLoss_WithLives_CostsOneResetsStreakKeepsBestAndPosition()
        {
            var save = Save(Wallet.Starting(rules), Lives.Full(rules), new WinStreak(2, 5), new CampaignProgress(4));
            var game = Game(save);

            var isLost = game.ReportCampaignLoss();

            Assert.IsTrue(isLost);
            Assert.AreEqual(rules.LifeCap - 1, save.Lives.CountAt(clock.UtcNow));
            Assert.AreEqual(0, save.Streak.Current);
            Assert.AreEqual(5, save.Streak.Best);
            Assert.AreEqual(4, game.CampaignPosition);
            Assert.AreEqual(rules.StartingGold, save.Wallet.Balance, "a loss costs no gold");
        }

        [Test]
        public void ReportCampaignLoss_NoLives_ResetsStreakAndIsFalse()
        {
            var save = Save(Wallet.Starting(rules), new Lives(rules, 0, clock.UtcNow), new WinStreak(2, 5), CampaignProgress.Starting());
            var game = Game(save);

            var isLost = game.ReportCampaignLoss();

            Assert.IsFalse(isLost);
            Assert.AreEqual(0, save.Lives.CountAt(clock.UtcNow));
            Assert.AreEqual(0, save.Streak.Current);
            Assert.AreEqual(5, save.Streak.Best);
        }

        [Test]
        public void ReportCampaignWin_ThresholdReachedAgainAfterLoss_PaysAgain()
        {
            var game = Game(SaveData.CreateDefault(rules));
            var reward = rules.StreakRewards[0];
            WinTimes(game, reward.Wins);
            game.ReportCampaignLoss();
            var jokersAfterFirstReward = game.Save.Jokers.CountOf(reward.Joker);

            var win = WinTimes(game, reward.Wins);

            CollectionAssert.AreEqual(new[] { reward }, win.RewardsGiven);
            Assert.AreEqual(jokersAfterFirstReward + reward.Count, game.Save.Jokers.CountOf(reward.Joker));
        }

        [Test]
        public void ReportCampaignWin_PastLastThreshold_PaysNothing()
        {
            var game = Game(SaveData.CreateDefault(rules));
            var last = rules.StreakRewards[rules.StreakRewards.Count - 1];
            WinTimes(game, last.Wins);
            var before = Snapshot(game.Save.Jokers);

            var win = game.ReportCampaignWin(Reward);

            Assert.AreEqual(last.Wins + 1, win.Streak);
            CollectionAssert.IsEmpty(win.RewardsGiven);
            AssertSameJokers(before, game.Save.Jokers);
        }

        [Test]
        public void CanStartCampaignLevel_NoLives_IsFalseUntilALifeComesBack()
        {
            var game = Game(Save(Wallet.Starting(rules), new Lives(rules, 0, clock.UtcNow), WinStreak.Starting(), CampaignProgress.Starting()));

            var canStartAtZero = game.CanStartCampaignLevel();
            clock.Advance(rules.LifeRegenInterval - OneSecond);
            var canStartJustBefore = game.CanStartCampaignLevel();
            clock.Advance(OneSecond);

            Assert.IsFalse(canStartAtZero);
            Assert.IsFalse(canStartJustBefore);
            Assert.IsTrue(game.CanStartCampaignLevel());
        }

        [Test]
        public void TryBuyJoker_EnoughGold_SpendsPriceAndAddsOne([Values] JokerKind kind)
        {
            var save = Save(new Wallet(rules.JokerPrice(kind), Reward), Lives.Full(rules), WinStreak.Starting(), CampaignProgress.Starting());
            var game = Game(save);
            var before = save.Jokers.CountOf(kind);

            var result = game.TryBuyJoker(kind);

            Assert.AreEqual(PurchaseResult.Bought, result);
            Assert.AreEqual(0, save.Wallet.Balance);
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned, "lifetime gold never changes in the store");
            Assert.AreEqual(before + 1, save.Jokers.CountOf(kind));
        }

        [Test]
        public void TryBuyJoker_NotEnoughGold_ChangesNothing([Values] JokerKind kind)
        {
            var price = rules.JokerPrice(kind);
            var save = Save(new Wallet(price - 1, Reward), Lives.Full(rules), WinStreak.Starting(), CampaignProgress.Starting());
            var game = Game(save);
            var before = save.Jokers.CountOf(kind);

            var result = game.TryBuyJoker(kind);

            Assert.AreEqual(PurchaseResult.NotEnoughGold, result);
            Assert.AreEqual(price - 1, save.Wallet.Balance);
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned, "lifetime gold never changes in the store");
            Assert.AreEqual(before, save.Jokers.CountOf(kind));
        }

        [Test]
        public void TryRefillLives_Full_IsAlreadyFullAndSpendsNothing()
        {
            var save = SaveData.CreateDefault(rules);
            var game = Game(save);

            var result = game.TryRefillLives();

            Assert.AreEqual(PurchaseResult.AlreadyFull, result);
            Assert.AreEqual(rules.StartingGold, save.Wallet.Balance);
        }

        [Test]
        public void TryRefillLives_BelowCapWithGold_SpendsPriceAndFills()
        {
            var save = Save(new Wallet(rules.LivesRefillPrice, Reward), new Lives(rules, 1, clock.UtcNow), WinStreak.Starting(), CampaignProgress.Starting());
            var game = Game(save);

            var result = game.TryRefillLives();

            Assert.AreEqual(PurchaseResult.Bought, result);
            Assert.AreEqual(0, save.Wallet.Balance);
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned, "lifetime gold never changes in the store");
            Assert.IsTrue(save.Lives.IsFullAt(clock.UtcNow));
        }

        [Test]
        public void TryRefillLives_BelowCapWithoutGold_IsNotEnoughGoldAndChangesNothing()
        {
            var balance = rules.LivesRefillPrice - 1;
            var save = Save(new Wallet(balance, Reward), new Lives(rules, 1, clock.UtcNow), WinStreak.Starting(), CampaignProgress.Starting());
            var game = Game(save);

            var result = game.TryRefillLives();

            Assert.AreEqual(PurchaseResult.NotEnoughGold, result);
            Assert.AreEqual(balance, save.Wallet.Balance);
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned);
            Assert.AreEqual(1, save.Lives.CountAt(clock.UtcNow));
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            var save = SaveData.CreateDefault(rules);

            Assert.Throws<ArgumentNullException>(() => new MetaGame(null, rules, clock));
            Assert.Throws<ArgumentNullException>(() => new MetaGame(save, null, clock));
            Assert.Throws<ArgumentNullException>(() => new MetaGame(save, rules, null));
        }

        private MetaGame Game(SaveData save) => new MetaGame(save, rules, clock);

        private SaveData Save(Wallet wallet, Lives lives, WinStreak streak, CampaignProgress campaign) =>
            new SaveData(new CompletedLevels(), new SoundSetting(), wallet, lives, JokerInventory.Starting(rules), streak, campaign);

        /// <summary>Reports <paramref name="wins"/> campaign wins of <see cref="Reward"/>, at least one, and returns the last.</summary>
        private static CampaignWin WinTimes(MetaGame game, int wins)
        {
            Assert.That(wins, Is.Positive, "at least one win");
            CampaignWin last = null;
            for (var i = 0; i < wins; i++)
            {
                last = game.ReportCampaignWin(Reward);
            }

            return last;
        }

        private static JokerInventory Snapshot(JokerInventory jokers) =>
            new JokerInventory(jokers.CountOf(JokerKind.Clock), jokers.CountOf(JokerKind.Rocket), jokers.CountOf(JokerKind.Broom));

        private static void AssertSameJokers(JokerInventory expected, JokerInventory actual)
        {
            foreach (JokerKind kind in Enum.GetValues(typeof(JokerKind)))
            {
                Assert.AreEqual(expected.CountOf(kind), actual.CountOf(kind), $"{kind} jokers");
            }
        }
    }
}
