using System;
using System.Collections.Generic;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Modules 30 and 31's <see cref="MetaRules"/>: a cap below 1, an
    /// interval that is not positive, and negative starting gold, prices or
    /// starting jokers each throw; a reward table out of order, with a
    /// repeated or non-positive Wins, or a count below 1 throws; Module 33's
    /// Clock seconds that are not positive throw; an empty table is allowed;
    /// and valid values are carried as given, the table as a copy. Module
    /// 36's gold packs: a null list, a blank or repeated id, or gold below 1
    /// throws; an empty list is allowed; the packs are carried as a copy.
    /// Module 38's defaults: a name the name rule refuses or an empty avatar
    /// id throws; valid ones are carried, the name normalized.
    /// </summary>
    public class MetaRulesTests
    {
        private const int Cap = 3;
        private const int StartingGold = 250;
        private const int ClockSeconds = 18;
        private const string PlayerName = "Rules Name";
        private const string AvatarId = "moose";

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(7);

        /// <summary>Reward tables each with one problem, named by the problem.</summary>
        public static IEnumerable<TestCaseData> TablesWithAProblem()
        {
            yield return new TestCaseData((object)new[] { new StreakReward(4, JokerKind.Clock, 1), new StreakReward(2, JokerKind.Rocket, 1) })
                .SetName("Constructor_RewardTableOutOfOrder_Throws");
            yield return new TestCaseData((object)new[] { new StreakReward(4, JokerKind.Clock, 1), new StreakReward(4, JokerKind.Rocket, 1) })
                .SetName("Constructor_RewardTableWithRepeatedWins_Throws");
            yield return new TestCaseData((object)new[] { new StreakReward(0, JokerKind.Clock, 1) })
                .SetName("Constructor_RewardTableWithZeroWins_Throws");
            yield return new TestCaseData((object)new[] { new StreakReward(-1, JokerKind.Clock, 1) })
                .SetName("Constructor_RewardTableWithNegativeWins_Throws");
            yield return new TestCaseData((object)new[] { new StreakReward(4, JokerKind.Clock, 0) })
                .SetName("Constructor_RewardTableWithCountZero_Throws");
        }

        /// <summary>Gold pack lists each with one problem, named by the problem.</summary>
        public static IEnumerable<TestCaseData> PacksWithAProblem()
        {
            yield return new TestCaseData((object)new[] { new GoldPack(string.Empty, 10) })
                .SetName("Constructor_GoldPackWithEmptyId_Throws");
            yield return new TestCaseData((object)new[] { new GoldPack("  ", 10) })
                .SetName("Constructor_GoldPackWithBlankId_Throws");
            yield return new TestCaseData((object)new[] { new GoldPack("a", 10), new GoldPack("a", 20) })
                .SetName("Constructor_GoldPacksWithRepeatedId_Throws");
            yield return new TestCaseData((object)new[] { new GoldPack("a", 0) })
                .SetName("Constructor_GoldPackWithZeroGold_Throws");
            yield return new TestCaseData((object)new[] { new GoldPack("a", -5) })
                .SetName("Constructor_GoldPackWithNegativeGold_Throws");
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_CapBelowOne_Throws(int cap)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MetaRulesFixture.Build(lifeCap: cap));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_IntervalNotPositive_Throws(int minutes)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MetaRulesFixture.Build(lifeRegenInterval: TimeSpan.FromMinutes(minutes)));
        }

        [Test]
        public void Constructor_NegativeStartingGold_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MetaRulesFixture.Build(startingGold: -1));
        }

        [Test]
        public void Constructor_NegativeJokerPrice_Throws([Values] JokerKind kind)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MetaRulesFixture.Build(jokerPrices: NegativeFor(kind)));
        }

        [Test]
        public void Constructor_NegativeRefillPrice_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MetaRulesFixture.Build(livesRefillPrice: -1));
        }

        [Test]
        public void Constructor_NegativeStartingJokers_Throws([Values] JokerKind kind)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MetaRulesFixture.Build(startingJokers: NegativeFor(kind)));
        }

        [TestCaseSource(nameof(TablesWithAProblem))]
        public void Constructor_RewardTableWithAProblem_Throws(StreakReward[] table)
        {
            Assert.Throws<ArgumentException>(() => MetaRulesFixture.Build(streakRewards: table));
        }

        [Test]
        public void Constructor_NullRewards_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MetaRules(
                Cap, Interval, StartingGold, new JokerAmounts(1, 2, 3), 4, new JokerAmounts(5, 6, 7), null, ClockSeconds,
                MetaRulesFixture.GoldPacks(), PlayerName, AvatarId));
        }

        [Test]
        public void Constructor_NullGoldPacks_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MetaRules(
                Cap, Interval, StartingGold, new JokerAmounts(1, 2, 3), 4, new JokerAmounts(5, 6, 7),
                MetaRulesFixture.StreakRewards(), ClockSeconds, null, PlayerName, AvatarId));
        }

        [Test]
        public void Constructor_PlayerDefaults_AreCarried()
        {
            var rules = MetaRulesFixture.Build(defaultPlayerName: "Ayşe 7", defaultAvatarId: "zebra");

            Assert.AreEqual("Ayşe 7", rules.DefaultPlayerName);
            Assert.AreEqual("zebra", rules.DefaultAvatarId);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Pl@yer")]
        [TestCase("abcdefghijklmnopq")]
        public void Constructor_DefaultNameRefusedByTheRule_Throws(string name)
        {
            Assert.Throws<ArgumentException>(() => MetaRulesFixture.Build(defaultPlayerName: name));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Constructor_EmptyDefaultAvatarId_Throws(string avatarId)
        {
            Assert.Throws<ArgumentException>(() => MetaRulesFixture.Build(defaultAvatarId: avatarId));
        }

        [Test]
        public void Constructor_DefaultNameWithExtraSpaces_IsKeptNormalized()
        {
            var rules = MetaRulesFixture.Build(defaultPlayerName: "  New   Player ");

            Assert.AreEqual("New Player", rules.DefaultPlayerName);
        }

        [TestCaseSource(nameof(PacksWithAProblem))]
        public void Constructor_GoldPacksWithAProblem_Throws(GoldPack[] packs)
        {
            Assert.Throws<ArgumentException>(() => MetaRulesFixture.Build(goldPacks: packs));
        }

        [Test]
        public void Constructor_EmptyGoldPacks_IsAllowed()
        {
            var rules = MetaRulesFixture.Build(goldPacks: Array.Empty<GoldPack>());

            CollectionAssert.IsEmpty(rules.GoldPacks);
        }

        [Test]
        public void GoldPacks_SourceChangedAfterConstruction_IsUnchanged()
        {
            var packs = MetaRulesFixture.GoldPacks();
            var rules = MetaRulesFixture.Build(goldPacks: packs);
            var first = packs[0];

            packs[0] = new GoldPack("changed", 1);

            Assert.AreEqual(first, rules.GoldPacks[0]);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_ClockBonusSecondsNotPositive_Throws(int seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MetaRulesFixture.Build(clockBonusSeconds: seconds));
        }

        [Test]
        public void Constructor_EmptyRewards_IsAllowed()
        {
            var rules = MetaRulesFixture.Build(streakRewards: Array.Empty<StreakReward>());

            CollectionAssert.IsEmpty(rules.StreakRewards);
        }

        [Test]
        public void Constructor_ValidValues_CarriesThem()
        {
            var table = new[] { new StreakReward(2, JokerKind.Broom, 4), new StreakReward(9, JokerKind.Clock, 1) };
            var packs = new[] { new GoldPack("first", 18), new GoldPack("second", 19) };

            var rules = new MetaRules(
                Cap, Interval, StartingGold, new JokerAmounts(11, 12, 13), 14, new JokerAmounts(15, 16, 17), table, ClockSeconds, packs,
                PlayerName, AvatarId);

            Assert.AreEqual(Cap, rules.LifeCap);
            Assert.AreEqual(Interval, rules.LifeRegenInterval);
            Assert.AreEqual(StartingGold, rules.StartingGold);
            Assert.AreEqual(11, rules.JokerPrice(JokerKind.Clock));
            Assert.AreEqual(12, rules.JokerPrice(JokerKind.Rocket));
            Assert.AreEqual(13, rules.JokerPrice(JokerKind.Broom));
            Assert.AreEqual(14, rules.LivesRefillPrice);
            Assert.AreEqual(15, rules.StartingJokers(JokerKind.Clock));
            Assert.AreEqual(16, rules.StartingJokers(JokerKind.Rocket));
            Assert.AreEqual(17, rules.StartingJokers(JokerKind.Broom));
            CollectionAssert.AreEqual(table, rules.StreakRewards);
            Assert.AreEqual(ClockSeconds, rules.ClockBonusSeconds);
            CollectionAssert.AreEqual(packs, rules.GoldPacks);
            Assert.AreEqual(PlayerName, rules.DefaultPlayerName);
            Assert.AreEqual(AvatarId, rules.DefaultAvatarId);
        }

        [Test]
        public void Constructor_ZeroStartingGold_IsAllowed()
        {
            var rules = MetaRulesFixture.Build(startingGold: 0);

            Assert.AreEqual(0, rules.StartingGold);
        }

        [Test]
        public void Constructor_ZeroPricesAndStartingJokers_AreAllowed([Values] JokerKind kind)
        {
            var rules = MetaRulesFixture.Build(
                jokerPrices: new JokerAmounts(0, 0, 0), livesRefillPrice: 0, startingJokers: new JokerAmounts(0, 0, 0));

            Assert.AreEqual(0, rules.JokerPrice(kind));
            Assert.AreEqual(0, rules.StartingJokers(kind));
            Assert.AreEqual(0, rules.LivesRefillPrice);
        }

        [Test]
        public void StreakRewards_SourceChangedAfterConstruction_IsUnchanged()
        {
            var table = MetaRulesFixture.StreakRewards();
            var rules = MetaRulesFixture.Build(streakRewards: table);
            var first = table[0];

            table[0] = new StreakReward(1, JokerKind.Broom, 9);

            Assert.AreEqual(first, rules.StreakRewards[0]);
        }

        [Test]
        public void JokerPriceAndStartingJokers_UndefinedKind_Throw()
        {
            var rules = MetaRulesFixture.Build();
            var undefined = (JokerKind)99;

            Assert.Throws<ArgumentOutOfRangeException>(() => rules.JokerPrice(undefined));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.StartingJokers(undefined));
        }

        private static JokerAmounts NegativeFor(JokerKind kind) =>
            new JokerAmounts(
                kind == JokerKind.Clock ? -1 : 1,
                kind == JokerKind.Rocket ? -1 : 1,
                kind == JokerKind.Broom ? -1 : 1);
    }
}
