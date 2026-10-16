using System;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Modules 30 and 31's <see cref="MetaConfig"/>: a fresh asset is
    /// usable; a cap below 1, regeneration minutes that are not positive, a
    /// negative starting gold, price or starting joker count, and a reward
    /// table out of order, with a repeated or non-positive Wins or with a
    /// count below 1, are each reported by <see cref="MetaConfig.Problems"/>,
    /// naming the field; so are Module 33's Clock seconds that are not
    /// positive and a negative free-play allowance, where 0 is accepted; an
    /// empty table is accepted; <see cref="MetaConfig.ToRules"/> carries the
    /// values over; and the free-play allowance is read back as set.
    /// </summary>
    public class MetaConfigTests
    {
        private const string LifeCapField = "lifeCap";
        private const string LifeRegenMinutesField = "lifeRegenMinutes";
        private const string StartingGoldField = "startingGold";
        private const string StreakRewardsField = "streakRewards";
        private const string ClockBonusSecondsField = "clockBonusSeconds";
        private const string FreePlayJokersPerLevelField = "freePlayJokersPerLevel";
        private const string StreakRewardsLabel = "Streak Rewards";

        private MetaConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<MetaConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void Problems_FreshConfig_IsEmpty()
        {
            var problems = config.Problems();

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Problems_CapBelowOne_IsReported(int cap)
        {
            SetInt(LifeCapField, cap);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Life Cap"), string.Join(" | ", problems));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Problems_RegenMinutesNotPositive_IsReported(int minutes)
        {
            SetInt(LifeRegenMinutesField, minutes);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Life Regen Minutes"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NegativeStartingGold_IsReported()
        {
            SetInt(StartingGoldField, -1);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Starting Gold"), string.Join(" | ", problems));
        }

        [TestCase("clockPrice", "Clock Price")]
        [TestCase("rocketPrice", "Rocket Price")]
        [TestCase("broomPrice", "Broom Price")]
        [TestCase("livesRefillPrice", "Lives Refill Price")]
        public void Problems_NegativePrice_IsReported(string field, string label)
        {
            SetInt(field, -1);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(label), string.Join(" | ", problems));
        }

        [TestCase("startingClocks", "Starting Clocks")]
        [TestCase("startingRockets", "Starting Rockets")]
        [TestCase("startingBrooms", "Starting Brooms")]
        public void Problems_NegativeStartingJokers_IsReported(string field, string label)
        {
            SetInt(field, -1);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(label), string.Join(" | ", problems));
        }

        [TestCase(4, 2, 1, TestName = "Problems_RewardTableOutOfOrder_IsReported")]
        [TestCase(4, 4, 1, TestName = "Problems_RewardTableWithRepeatedWins_IsReported")]
        [TestCase(0, 4, 1, TestName = "Problems_RewardTableWithZeroWins_IsReported")]
        [TestCase(-1, 4, 1, TestName = "Problems_RewardTableWithNegativeWins_IsReported")]
        [TestCase(2, 4, 0, TestName = "Problems_RewardTableWithCountZero_IsReported")]
        public void Problems_RewardTableWithAProblem_IsReported(int firstWins, int secondWins, int secondCount)
        {
            SetRewards(new StreakReward(firstWins, JokerKind.Clock, 1), new StreakReward(secondWins, JokerKind.Rocket, secondCount));

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(StreakRewardsLabel), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_EmptyRewardTable_IsEmpty()
        {
            SetRewards();

            var problems = config.Problems();

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
            CollectionAssert.IsEmpty(config.ToRules().StreakRewards);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Problems_ClockBonusSecondsNotPositive_IsReported(int seconds)
        {
            SetInt(ClockBonusSecondsField, seconds);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Clock Bonus Seconds"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NegativeFreePlayJokersPerLevel_IsReported()
        {
            SetInt(FreePlayJokersPerLevelField, -1);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Free Play Jokers Per Level"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_ZeroFreePlayJokersPerLevel_IsEmpty()
        {
            SetInt(FreePlayJokersPerLevelField, 0);

            var problems = config.Problems();

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
        }

        [Test]
        public void FreePlayJokersPerLevel_SetValue_IsReadBack()
        {
            const int perLevel = 7;
            SetInt(FreePlayJokersPerLevelField, perLevel);

            var read = config.FreePlayJokersPerLevel;

            Assert.AreEqual(perLevel, read);
        }

        [Test]
        public void ToRules_SetValues_CarriesThemOver()
        {
            const int cap = 3;
            const int minutes = 7;
            const int gold = 11;
            var table = new[] { new StreakReward(2, JokerKind.Broom, 4), new StreakReward(6, JokerKind.Clock, 2) };
            SetInt(LifeCapField, cap);
            SetInt(LifeRegenMinutesField, minutes);
            SetInt(StartingGoldField, gold);
            SetInt("clockPrice", 21);
            SetInt("rocketPrice", 22);
            SetInt("broomPrice", 23);
            SetInt("livesRefillPrice", 24);
            SetInt("startingClocks", 31);
            SetInt("startingRockets", 32);
            SetInt("startingBrooms", 33);
            SetInt(ClockBonusSecondsField, 41);
            SetRewards(table);

            var rules = config.ToRules();

            Assert.AreEqual(cap, rules.LifeCap);
            Assert.AreEqual(TimeSpan.FromMinutes(minutes), rules.LifeRegenInterval);
            Assert.AreEqual(gold, rules.StartingGold);
            Assert.AreEqual(21, rules.JokerPrice(JokerKind.Clock));
            Assert.AreEqual(22, rules.JokerPrice(JokerKind.Rocket));
            Assert.AreEqual(23, rules.JokerPrice(JokerKind.Broom));
            Assert.AreEqual(24, rules.LivesRefillPrice);
            Assert.AreEqual(31, rules.StartingJokers(JokerKind.Clock));
            Assert.AreEqual(32, rules.StartingJokers(JokerKind.Rocket));
            Assert.AreEqual(33, rules.StartingJokers(JokerKind.Broom));
            CollectionAssert.AreEqual(table, rules.StreakRewards);
            Assert.AreEqual(41, rules.ClockBonusSeconds);
        }

        private void SetInt(string field, int value)
        {
            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"MetaConfig has no serialized field '{field}'.");
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetRewards(params StreakReward[] rewards)
        {
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(StreakRewardsField);
            Assert.IsNotNull(entries, $"MetaConfig has no serialized field '{StreakRewardsField}'.");
            entries.arraySize = rewards.Length;
            for (var i = 0; i < rewards.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("wins").intValue = rewards[i].Wins;
                entry.FindPropertyRelative("joker").intValue = (int)rewards[i].Joker;
                entry.FindPropertyRelative("count").intValue = rewards[i].Count;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
