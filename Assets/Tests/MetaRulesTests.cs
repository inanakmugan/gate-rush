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
    /// repeated or non-positive Wins, or a count below 1 throws; an empty
    /// table is allowed; and valid values are carried as given, the table as
    /// a copy.
    /// </summary>
    public class MetaRulesTests
    {
        private const int Cap = 3;
        private const int StartingGold = 250;

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
                Cap, Interval, StartingGold, new JokerAmounts(1, 2, 3), 4, new JokerAmounts(5, 6, 7), null));
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

            var rules = new MetaRules(Cap, Interval, StartingGold, new JokerAmounts(11, 12, 13), 14, new JokerAmounts(15, 16, 17), table);

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
