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
    /// the best and the position; the store (D62) spends only what the
    /// balance covers and never touches the lifetime gold; and Module 33's
    /// <see cref="MetaGame.TryUseJoker"/> takes a held joker, else buys and
    /// uses one without adding it to the inventory, else changes nothing,
    /// never touching the streak, the position or the lives (D65). Module
    /// 36's <see cref="MetaGame.ReportGoldPurchase"/> adds a pack's gold to
    /// the balance only and touches nothing else; an unknown id throws.
    /// </summary>
    public class MetaGameTests
    {
        private const int Reward = 45;
        private const int FirstReward = 40;
        private const int SecondReward = 55;
        private const int ThirdReward = 65;
        private const int HeldOfTheUsedKind = 2;
        private const int OtherKindsHeld = 5;
        private const int LivesHeld = 2;
        private const int StreakCurrent = 4;
        private const int StreakBest = 6;
        private const int Position = 3;

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
        public void TryUseJoker_OneInInventory_IsUsedTakesOneAndSpendsNoGold([Values] JokerKind kind)
        {
            var balance = rules.JokerPrice(kind);
            var save = Save(new Wallet(balance, Reward), Lives.Full(rules), WinStreak.Starting(), CampaignProgress.Starting(),
                Holding(kind, HeldOfTheUsedKind));
            var game = Game(save);

            var result = game.TryUseJoker(kind);

            Assert.AreEqual(JokerUse.Used, result);
            AssertSameJokers(Holding(kind, HeldOfTheUsedKind - 1), save.Jokers);
            Assert.AreEqual(balance, save.Wallet.Balance, "a held joker costs no gold");
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned);
        }

        [Test]
        public void TryUseJoker_NoneAndEnoughGold_IsBoughtAndUsedSpendsThePriceAndLeavesTheInventoryAtZero([Values] JokerKind kind)
        {
            var save = Save(new Wallet(rules.JokerPrice(kind), Reward), Lives.Full(rules), WinStreak.Starting(), CampaignProgress.Starting(),
                Holding(kind, 0));
            var game = Game(save);

            var result = game.TryUseJoker(kind);

            Assert.AreEqual(JokerUse.BoughtAndUsed, result);
            Assert.AreEqual(0, save.Wallet.Balance);
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned, "lifetime gold never changes when buying");
            AssertSameJokers(Holding(kind, 0), save.Jokers);
        }

        [Test]
        public void TryUseJoker_NoneAndTooLittleGold_IsNotEnoughGoldAndChangesNothing([Values] JokerKind kind)
        {
            var balance = rules.JokerPrice(kind) - 1;
            var save = Save(new Wallet(balance, Reward), Lives.Full(rules), WinStreak.Starting(), CampaignProgress.Starting(),
                Holding(kind, 0));
            var game = Game(save);

            var result = game.TryUseJoker(kind);

            Assert.AreEqual(JokerUse.NotEnoughGold, result);
            Assert.AreEqual(balance, save.Wallet.Balance);
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned);
            AssertSameJokers(Holding(kind, 0), save.Jokers);
        }

        [TestCase(HeldOfTheUsedKind, true, TestName = "TryUseJoker_Used_LeavesStreakPositionAndLives")]
        [TestCase(0, true, TestName = "TryUseJoker_BoughtAndUsed_LeavesStreakPositionAndLives")]
        [TestCase(0, false, TestName = "TryUseJoker_NotEnoughGold_LeavesStreakPositionAndLives")]
        public void TryUseJoker_EveryOutcome_LeavesStreakPositionAndLives(int held, bool canPay)
        {
            const JokerKind kind = JokerKind.Rocket;
            var balance = canPay ? rules.JokerPrice(kind) : rules.JokerPrice(kind) - 1;
            var save = Save(new Wallet(balance, Reward), new Lives(rules, LivesHeld, clock.UtcNow),
                new WinStreak(StreakCurrent, StreakBest), new CampaignProgress(Position), Holding(kind, held));
            var game = Game(save);

            game.TryUseJoker(kind);

            Assert.AreEqual(StreakCurrent, save.Streak.Current);
            Assert.AreEqual(StreakBest, save.Streak.Best);
            Assert.AreEqual(Position, game.CampaignPosition);
            Assert.AreEqual(LivesHeld, save.Lives.CountAt(clock.UtcNow));
        }

        [Test]
        public void TryUseJoker_UndefinedKind_ThrowsAndChangesNothing()
        {
            var save = SaveData.CreateDefault(rules);
            var game = Game(save);
            var jokersBefore = Snapshot(save.Jokers);

            Assert.Throws<ArgumentOutOfRangeException>(() => game.TryUseJoker((JokerKind)99));

            Assert.AreEqual(rules.StartingGold, save.Wallet.Balance);
            AssertSameJokers(jokersBefore, save.Jokers);
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
        public void ReportGoldPurchase_KnownId_AddsPackGoldToBalanceOnlyAndReturnsIt()
        {
            var save = Save(new Wallet(Reward, Reward), Lives.Full(rules), new WinStreak(0, 0), new CampaignProgress(0));
            var game = Game(save);

            var added = game.ReportGoldPurchase(MetaRulesFixture.LargePackId);

            Assert.AreEqual(MetaRulesFixture.LargePackGold, added);
            Assert.AreEqual(Reward + MetaRulesFixture.LargePackGold, save.Wallet.Balance);
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned, "bought gold never enters the lifetime gold (D22)");
        }

        [Test]
        public void ReportGoldPurchase_UnknownId_ThrowsAndChangesNothing()
        {
            var save = Save(new Wallet(Reward, Reward), Lives.Full(rules), new WinStreak(0, 0), new CampaignProgress(0));
            var game = Game(save);

            Assert.Throws<ArgumentException>(() => game.ReportGoldPurchase("not_a_pack"));

            Assert.AreEqual(Reward, save.Wallet.Balance);
            Assert.AreEqual(Reward, save.Wallet.LifetimeEarned);
        }

        [Test]
        public void ReportGoldPurchase_NullId_Throws()
        {
            var game = Game(SaveData.CreateDefault(rules));

            Assert.Throws<ArgumentNullException>(() => game.ReportGoldPurchase(null));
        }

        [Test]
        public void ReportGoldPurchase_KnownId_LeavesStreakLivesJokersAndPosition()
        {
            var jokers = Holding(JokerKind.Broom, HeldOfTheUsedKind);
            var save = Save(new Wallet(Reward, Reward), new Lives(rules, LivesHeld, clock.UtcNow),
                new WinStreak(StreakCurrent, StreakBest), new CampaignProgress(Position), jokers);
            var before = Snapshot(save.Jokers);
            var game = Game(save);

            game.ReportGoldPurchase(MetaRulesFixture.SmallPackId);

            Assert.AreEqual(StreakCurrent, save.Streak.Current);
            Assert.AreEqual(StreakBest, save.Streak.Best);
            Assert.AreEqual(Position, game.CampaignPosition);
            Assert.AreEqual(LivesHeld, save.Lives.CountAt(clock.UtcNow));
            AssertSameJokers(before, save.Jokers);
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

        private SaveData Save(
            Wallet wallet, Lives lives, WinStreak streak, CampaignProgress campaign, JokerInventory jokers = null) =>
            new SaveData(
                new CompletedLevels(), new SoundSetting(), wallet, lives, jokers ?? JokerInventory.Starting(rules), streak, campaign);

        /// <summary>An inventory holding <paramref name="count"/> of <paramref name="kind"/> and <see cref="OtherKindsHeld"/> of every other kind.</summary>
        private static JokerInventory Holding(JokerKind kind, int count) =>
            new JokerInventory(
                kind == JokerKind.Clock ? count : OtherKindsHeld,
                kind == JokerKind.Rocket ? count : OtherKindsHeld,
                kind == JokerKind.Broom ? count : OtherKindsHeld);

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
