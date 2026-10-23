using System;
using GateRush.Meta;
using GateRush.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 37's <see cref="StreakTexts"/>, formatting only: the line
    /// under the streak bar for each streak of the default table, "1 more
    /// win" with one left, empty for an empty table and naming a reward's
    /// count above 1; the streak's count; the current and best streaks; the
    /// ladder's wins, rewards and states; the markers' counts; the Streak
    /// Reward headline and prize lines, a locked joker's with the level it
    /// opens at (Module 43); and every text from the config.
    /// </summary>
    public class StreakTextsTests
    {
        private UiConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<UiConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [TestCase(0, "Win 3 in a row: Clock")]
        [TestCase(2, "1 more win: Clock")]
        [TestCase(3, "2 more wins: Rocket")]
        [TestCase(4, "1 more win: Rocket")]
        [TestCase(5, "5 more wins: Broom")]
        [TestCase(9, "1 more win: Broom")]
        [TestCase(10, "Every reward collected")]
        [TestCase(12, "Every reward collected")]
        public void BarLine_DefaultTable_ReadsTowardTheNextReward(int streak, string expected)
        {
            var progress = StreakProgress.Of(streak, DefaultTable());

            var line = StreakTexts.BarLine(progress, config);

            Assert.AreEqual(expected, line);
        }

        [Test]
        public void BarLine_EmptyTable_IsEmpty()
        {
            var progress = StreakProgress.Of(3, Array.Empty<StreakReward>());

            var line = StreakTexts.BarLine(progress, config);

            Assert.AreEqual(string.Empty, line);
        }

        [Test]
        public void BarLine_NextRewardOfTwo_NamesItWithItsCount()
        {
            var progress = StreakProgress.Of(1, new[] { new StreakReward(3, JokerKind.Rocket, 2) });

            var line = StreakTexts.BarLine(progress, config);

            Assert.AreEqual("2 more wins: Rocket x2", line);
        }

        [Test]
        public void BarLine_ConfigFormatsAndNames_AreUsed()
        {
            SetString("moreWinsFormat", "{1} in {0}");
            SetString("oneMoreWinFormat", "{0} next win");
            SetString("rocketName", "Missile");

            var more = StreakTexts.BarLine(StreakProgress.Of(3, DefaultTable()), config);
            var one = StreakTexts.BarLine(StreakProgress.Of(4, DefaultTable()), config);

            Assert.AreEqual("Missile in 2", more);
            Assert.AreEqual("Missile next win", one);
        }

        [Test]
        public void StreakCount_Streak_UsesTheStreakCountFormat()
        {
            SetString("streakCountFormat", "{0}!");

            var text = StreakTexts.StreakCount(4, config);

            Assert.AreEqual("4!", text);
        }

        [Test]
        public void CurrentStreak_Streak_ReadsStreakN()
        {
            var text = StreakTexts.CurrentStreak(4, config);

            Assert.AreEqual("Streak: 4", text);
        }

        [Test]
        public void BestStreak_Best_ReadsBestN()
        {
            var text = StreakTexts.BestStreak(7, config);

            Assert.AreEqual("Best: 7", text);
        }

        [Test]
        public void LadderWins_Wins_ReadsNWins()
        {
            var text = StreakTexts.LadderWins(3, config);

            Assert.AreEqual("3 wins", text);
        }

        [Test]
        public void LadderReward_CountOfOne_StillShowsTheCount()
        {
            var text = StreakTexts.LadderReward(new StreakReward(3, JokerKind.Clock, 1), config);

            Assert.AreEqual("Clock x1", text);
        }

        [TestCase(StreakRewardState.Collected, "Collected")]
        [TestCase(StreakRewardState.Next, "Next")]
        [TestCase(StreakRewardState.Ahead, "")]
        public void LadderState_State_IsItsLabel(StreakRewardState state, string expected)
        {
            var text = StreakTexts.LadderState(state, config);

            Assert.AreEqual(expected, text);
        }

        [TestCase(1, "")]
        [TestCase(2, "x2")]
        public void MarkerCount_Count_ShownOnlyAboveOne(int count, string expected)
        {
            var text = StreakTexts.MarkerCount(count, config);

            Assert.AreEqual(expected, text);
        }

        [Test]
        public void RewardHeadline_LatestStreak_ReadsNWinsInARow()
        {
            var text = StreakTexts.RewardHeadline(5, config);

            Assert.AreEqual("5 wins in a row!", text);
        }

        [Test]
        public void Prize_MergedCount_ReadsPlusCountAndName()
        {
            var text = StreakTexts.Prize(new JokerPrize(JokerKind.Clock, 2), config);

            Assert.AreEqual("+2 Clock", text);
        }

        [Test]
        public void Prize_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StreakTexts.Prize(new JokerPrize(JokerKind.Clock, 1), null));
        }

        [Test]
        public void LockedPrize_DefaultFormat_ReadsTheLevelItOpensAt()
        {
            var text = StreakTexts.LockedPrize(new JokerPrize(JokerKind.Rocket, 1), 10, config);

            Assert.AreEqual("+1 Rocket, opens at Level 10", text);
        }

        [Test]
        public void LockedPrize_OwnFormatAndName_AreUsed()
        {
            SetString("lockedPrizeFormat", "{1} x{0} (Lv {2})");
            SetString("broomName", "Sweeper");

            var text = StreakTexts.LockedPrize(new JokerPrize(JokerKind.Broom, 2), 15, config);

            Assert.AreEqual("Sweeper x2 (Lv 15)", text);
        }

        [Test]
        public void LockedPrize_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StreakTexts.LockedPrize(new JokerPrize(JokerKind.Clock, 1), 5, null));
        }

        /// <summary>The reward table of D61: 1 Clock at 3 wins, 1 Rocket at 5, 1 Broom at 10.</summary>
        private static StreakReward[] DefaultTable() => new[]
        {
            new StreakReward(3, JokerKind.Clock, 1),
            new StreakReward(5, JokerKind.Rocket, 1),
            new StreakReward(10, JokerKind.Broom, 1)
        };

        private void SetString(string field, string value)
        {
            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"UiConfig has no serialized field '{field}'.");
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
