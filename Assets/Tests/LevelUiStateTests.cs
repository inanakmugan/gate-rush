using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 40's <see cref="LevelUiState.Of"/>, the one place the
    /// level's interface reads its numbers: the level number or free play;
    /// the lives, the cap and the wait to the next life, rounded up to a
    /// whole second, from the save at the time given, none in free play; the
    /// gold; each joker's button from the inventory and the rules' price in
    /// the campaign and from the allowance in free play, the Clock usable only
    /// with a countdown; the selection and the offers as given; and equality,
    /// the bootstrap's refresh test.
    /// </summary>
    /// <remarks>
    /// The rules are <see cref="MetaRulesFixture"/>'s, so a price or a cap
    /// taken from anywhere but the rules fails.
    /// </remarks>
    public class LevelUiStateTests
    {
        private const int Gold = 640;
        private const int SomeLives = 2;
        private const int LevelNumber = 7;
        private const int Allowance = 3;

        private static readonly DateTime Noon = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

        private MetaRules rules;
        private IReadOnlyList<GoldOffer> offers;

        [SetUp]
        public void SetUp()
        {
            rules = MetaRulesFixture.Build();
            offers = new[] { new GoldOffer("gold_small", 500, "$0.99") };
        }

        [Test]
        public void Of_Campaign_LevelNumberLivesCapAndGoldComeFromTheSessionSaveAndRules()
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(0, 0, 0));

            var state = Campaign(meta, Noon);

            Assert.IsFalse(state.IsFreePlay);
            Assert.AreEqual(LevelNumber, state.LevelNumber);
            Assert.AreEqual(SomeLives, state.Lives);
            Assert.AreEqual(rules.LifeCap, state.LifeCap);
            Assert.AreEqual(Gold, state.Gold);
        }

        [Test]
        public void Of_CampaignLivesBelowTheCap_WaitIsTheWholeSecondsToTheNextLife()
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(0, 0, 0));
            var now = Noon + TimeSpan.FromSeconds(0.5);

            var state = Campaign(meta, now);

            var untilNext = meta.Save.Lives.UntilNextLifeAt(now);
            Assert.IsTrue(untilNext.HasValue);
            Assert.AreEqual(MetaText.WholeSeconds(untilNext.Value), state.NextLifeSeconds, "rounded up as the countdown is");
        }

        [Test]
        public void Of_CampaignLivesFull_HasNoWait()
        {
            var meta = Meta(Gold, rules.LifeCap, new JokerInventory(0, 0, 0));

            var state = Campaign(meta, Noon);

            Assert.IsNull(state.NextLifeSeconds);
        }

        [Test]
        public void Of_FreePlay_ShowsFreePlayWithNoLivesAndTheGold()
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(0, 0, 0));

            var state = LevelUiState.Of(PlayMode.FreePlay, LevelNumber, meta, rules, Noon, new FreePlayJokers(Allowance), true, true, null, offers);

            Assert.IsTrue(state.IsFreePlay);
            Assert.AreEqual(0, state.Lives, "free play shows no lives (D58)");
            Assert.IsNull(state.NextLifeSeconds);
            Assert.AreEqual(Gold, state.Gold);
        }

        [Test]
        public void Of_FreePlay_ButtonsCountTheAllowanceNotTheInventory([Values] JokerKind kind)
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(9, 9, 9));
            var freePlay = new FreePlayJokers(Allowance);
            freePlay.TryUse(kind);

            var state = LevelUiState.Of(PlayMode.FreePlay, LevelNumber, meta, rules, Noon, freePlay, true, true, null, offers);

            Assert.AreEqual(JokerButtons.FreePlay(Allowance - 1, true), state.Button(kind));
        }

        [Test]
        public void Of_Campaign_ButtonsComeFromTheInventoryPricesAndBalance([Values] JokerKind kind)
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(1, 0, 4));

            var state = Campaign(meta, Noon);

            var expected = JokerButtons.Campaign(meta.Save.Jokers.CountOf(kind), rules.JokerPrice(kind), Gold, true);
            Assert.AreEqual(expected, state.Button(kind));
        }

        [Test]
        public void Of_CampaignNoneHeldAndTheBalanceShort_ButtonCarriesTheShortfallFromTheRulesPrice()
        {
            var balance = MetaRulesFixture.RocketPrice - 1;
            var meta = Meta(balance, SomeLives, new JokerInventory(0, 0, 0));

            var state = Campaign(meta, Noon);

            var rocket = state.Button(JokerKind.Rocket);
            Assert.IsTrue(rocket.IsInteractable);
            Assert.AreEqual(MetaRulesFixture.RocketPrice - balance, rocket.Shortfall);
        }

        [Test]
        public void Of_NoCountdown_OnlyTheClockIsNotPressable()
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(1, 1, 1));

            var state = LevelUiState.Of(PlayMode.Campaign, LevelNumber, meta, rules, Noon, new FreePlayJokers(Allowance), true, false, null, offers);

            Assert.IsFalse(state.Button(JokerKind.Clock).IsInteractable);
            Assert.IsTrue(state.Button(JokerKind.Rocket).IsInteractable);
            Assert.IsTrue(state.Button(JokerKind.Broom).IsInteractable);
        }

        [Test]
        public void Of_NotUsable_NoButtonIsPressable([Values] JokerKind kind)
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(1, 1, 1));

            var state = LevelUiState.Of(PlayMode.Campaign, LevelNumber, meta, rules, Noon, new FreePlayJokers(Allowance), false, true, null, offers);

            Assert.IsFalse(state.Button(kind).IsInteractable);
        }

        [Test]
        public void Of_SelectionAndOffers_AreTheGivenOnes()
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(0, 0, 0));

            var state = LevelUiState.Of(
                PlayMode.Campaign, null, meta, rules, Noon, new FreePlayJokers(Allowance), true, true, JokerKind.Broom, offers);

            Assert.AreEqual(JokerKind.Broom, state.SelectedJoker);
            Assert.AreSame(offers, state.GoldOffers);
            Assert.IsNull(state.LevelNumber);
        }

        [Test]
        public void Button_UndefinedKind_Throws()
        {
            var state = Campaign(Meta(Gold, SomeLives, new JokerInventory(0, 0, 0)), Noon);

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Button((JokerKind)99));
        }

        [Test]
        public void Of_NullMeta_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => LevelUiState.Of(PlayMode.Campaign, LevelNumber, null, rules, Noon, new FreePlayJokers(Allowance), true, true, null, offers));
        }

        [Test]
        public void Equals_SameSaveAMomentLater_IsEqual()
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(0, 0, 0));

            var first = Campaign(meta, Noon + TimeSpan.FromSeconds(0.2));
            var second = Campaign(meta, Noon + TimeSpan.FromSeconds(0.4));

            Assert.IsTrue(first.Equals(second), "the wait reads the same whole second");
        }

        [Test]
        public void Equals_SelectionDiffers_IsNotEqual()
        {
            var meta = Meta(Gold, SomeLives, new JokerInventory(0, 0, 0));

            var none = Campaign(meta, Noon);
            var rocket = LevelUiState.Of(
                PlayMode.Campaign, LevelNumber, meta, rules, Noon, new FreePlayJokers(Allowance), true, true, JokerKind.Rocket, offers);

            Assert.IsFalse(none.Equals(rocket));
        }

        private LevelUiState Campaign(MetaGame meta, DateTime nowUtc) =>
            LevelUiState.Of(PlayMode.Campaign, LevelNumber, meta, rules, nowUtc, new FreePlayJokers(Allowance), true, true, null, offers);

        private MetaGame Meta(int gold, int lives, JokerInventory jokers)
        {
            var save = new SaveData(
                new CompletedLevels(),
                new SoundSetting(),
                new Wallet(gold, 0),
                new Lives(rules, lives, Noon),
                jokers,
                new WinStreak(0, 0),
                new CampaignProgress(0),
                PlayerProfile.Starting(rules),
                new HapticsSetting());
            return new MetaGame(save, rules, new ManualClock(Noon));
        }
    }
}
