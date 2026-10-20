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
    /// Covers Module 35's <see cref="UiConfig"/>: the defaults report no
    /// problem; each text or format that is empty, each format with a
    /// placeholder beyond the numbers it is given, and each fully transparent
    /// colour is reported by its field's name; and each colour role reads its
    /// own field. Module 36's Store texts are checked the same way, and a
    /// ribbon with an empty or repeated id or an empty text is reported.
    /// Module 37's win streak texts and Streak colour are checked the same
    /// way, and each joker kind reads its own name. Module 38's Profile and
    /// Haptics texts are checked the same way. Module 39's Leaderboard texts
    /// and colours are checked the same way, and the first three ranks read
    /// their own medal colour while every other rank has none. Module 40's
    /// level HUD texts, formats and colours are checked the same way, an
    /// unusable timing is reported, the defaults read as specified, and the
    /// selected joker's ring reads its own colour. Module 41's level panel
    /// texts and formats are checked the same way, an unusable pop is
    /// reported, and the defaults read as specified.
    /// </summary>
    public class UiConfigTests
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

        [Test]
        public void Problems_Defaults_ReportNothing()
        {
            var problems = config.Problems();

            Assert.IsEmpty(problems, string.Join(" | ", problems));
        }

        [TestCase("playLevelFormat", "Play Level Format")]
        [TestCase("allPlayedLabel", "All Played Label")]
        [TestCase("goldFormat", "Gold Format")]
        [TestCase("livesCountFormat", "Lives Count Format")]
        [TestCase("livesFractionFormat", "Lives Fraction Format")]
        [TestCase("livesFullLabel", "Lives Full Label")]
        [TestCase("waitFormat", "Wait Format")]
        [TestCase("refillFormat", "Refill Format")]
        [TestCase("levelNumberFormat", "Level Number Format")]
        [TestCase("goldAmountFormat", "Gold Amount Format")]
        [TestCase("goldPriceFormat", "Gold Price Format")]
        [TestCase("jokerCountFormat", "Joker Count Format")]
        [TestCase("shortfallFormat", "Shortfall Format")]
        [TestCase("goldHeader", "Gold Header")]
        [TestCase("jokersHeader", "Jokers Header")]
        [TestCase("livesHeader", "Lives Header")]
        [TestCase("testStoreNote", "Test Store Note")]
        [TestCase("testPurchaseTitle", "Test Purchase Title")]
        [TestCase("testPurchaseOfferFormat", "Test Purchase Offer Format")]
        [TestCase("testPurchaseNote", "Test Purchase Note")]
        [TestCase("purchaseFailedTitle", "Purchase Failed Title")]
        [TestCase("purchaseFailedText", "Purchase Failed Text")]
        [TestCase("streakCountFormat", "Streak Count Format")]
        [TestCase("firstRewardFormat", "First Reward Format")]
        [TestCase("moreWinsFormat", "More Wins Format")]
        [TestCase("oneMoreWinFormat", "One More Win Format")]
        [TestCase("allRewardsCollectedLabel", "All Rewards Collected Label")]
        [TestCase("currentStreakFormat", "Current Streak Format")]
        [TestCase("bestStreakFormat", "Best Streak Format")]
        [TestCase("ladderWinsFormat", "Ladder Wins Format")]
        [TestCase("ladderRewardFormat", "Ladder Reward Format")]
        [TestCase("markerCountFormat", "Marker Count Format")]
        [TestCase("collectedLabel", "Collected Label")]
        [TestCase("nextLabel", "Next Label")]
        [TestCase("streakRuleText", "Streak Rule Text")]
        [TestCase("rewardHeadlineFormat", "Reward Headline Format")]
        [TestCase("prizeFormat", "Prize Format")]
        [TestCase("clockName", "Clock Name")]
        [TestCase("rocketName", "Rocket Name")]
        [TestCase("broomName", "Broom Name")]
        [TestCase("profileTitle", "Profile Title")]
        [TestCase("nameEditHint", "Name Edit Hint")]
        [TestCase("nameRefusedText", "Name Refused Text")]
        [TestCase("levelsCompletedLabel", "Levels Completed Label")]
        [TestCase("campaignLevelLabel", "Campaign Level Label")]
        [TestCase("bestStreakLabel", "Best Streak Label")]
        [TestCase("lifetimeGoldLabel", "Lifetime Gold Label")]
        [TestCase("statisticFormat", "Statistic Format")]
        [TestCase("hapticsLabel", "Haptics Label")]
        [TestCase("leaderboardSimulatedNote", "Leaderboard Simulated Note")]
        [TestCase("leaderboardLoadingText", "Leaderboard Loading Text")]
        [TestCase("leaderboardFailedText", "Leaderboard Failed Text")]
        [TestCase("leaderboardRetryLabel", "Leaderboard Retry Label")]
        [TestCase("leaderboardScoreFormat", "Leaderboard Score Format")]
        [TestCase("leaderboardRankFormat", "Leaderboard Rank Format")]
        [TestCase("freePlayLabel", "Free Play Label")]
        [TestCase("levelLabelFormat", "Level Label Format")]
        [TestCase("timerFormat", "Timer Format")]
        [TestCase("timeBonusFormat", "Time Bonus Format")]
        [TestCase("jokerCountBadgeFormat", "Joker Count Badge Format")]
        [TestCase("jokerPriceBadgeFormat", "Joker Price Badge Format")]
        [TestCase("goldPacksTitle", "Gold Packs Title")]
        [TestCase("levelCompleteTitle", "Level Complete Title")]
        [TestCase("timesUpTitle", "Times Up Title")]
        [TestCase("nextButtonLabel", "Next Button Label")]
        [TestCase("restartButtonLabel", "Restart Button Label")]
        [TestCase("goldEarnedFormat", "Gold Earned Format")]
        [TestCase("closingMessage", "Closing Message")]
        [TestCase("leaveLevelTitle", "Leave Level Title")]
        [TestCase("restartLevelTitle", "Restart Level Title")]
        [TestCase("loseLifeLine", "Lose Life Line")]
        [TestCase("loseLifeAndStreakFormat", "Lose Life And Streak Format")]
        [TestCase("loseLifeLabel", "Lose Life Label")]
        [TestCase("keepPlayingLabel", "Keep Playing Label")]
        public void Problems_EmptyText_IsReportedByName(string field, string shownName)
        {
            SetString(field, "  ");

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} is empty"), string.Join(" | ", problems));
        }

        // Each format is given one placeholder more than it is formatted with.
        [TestCase("playLevelFormat", "Play Level Format", "Level {1}")]
        [TestCase("goldFormat", "Gold Format", "{1}")]
        [TestCase("livesCountFormat", "Lives Count Format", "{1}")]
        [TestCase("livesFractionFormat", "Lives Fraction Format", "{0}/{2}")]
        [TestCase("waitFormat", "Wait Format", "{0}:{2:00}")]
        [TestCase("refillFormat", "Refill Format", "Refill ({1})")]
        [TestCase("levelNumberFormat", "Level Number Format", "{1}")]
        [TestCase("goldAmountFormat", "Gold Amount Format", "{1:N0}")]
        [TestCase("goldPriceFormat", "Gold Price Format", "{1}")]
        [TestCase("jokerCountFormat", "Joker Count Format", "You have {1}")]
        [TestCase("shortfallFormat", "Shortfall Format", "You need {1} more gold.")]
        [TestCase("testPurchaseOfferFormat", "Test Purchase Offer Format", "{0} gold for {2}")]
        [TestCase("streakCountFormat", "Streak Count Format", "{1}")]
        [TestCase("firstRewardFormat", "First Reward Format", "Win {0} in a row: {2}")]
        [TestCase("moreWinsFormat", "More Wins Format", "{0} more wins: {2}")]
        [TestCase("oneMoreWinFormat", "One More Win Format", "1 more win: {1}")]
        [TestCase("currentStreakFormat", "Current Streak Format", "Streak {1}")]
        [TestCase("bestStreakFormat", "Best Streak Format", "Best {1}")]
        [TestCase("ladderWinsFormat", "Ladder Wins Format", "{1} wins")]
        [TestCase("ladderRewardFormat", "Ladder Reward Format", "{0} x{2}")]
        [TestCase("markerCountFormat", "Marker Count Format", "x{1}")]
        [TestCase("rewardHeadlineFormat", "Reward Headline Format", "{1} wins in a row!")]
        [TestCase("prizeFormat", "Prize Format", "+{0} {2}")]
        [TestCase("statisticFormat", "Statistic Format", "{1:N0}")]
        [TestCase("leaderboardScoreFormat", "Leaderboard Score Format", "{1:N0}")]
        [TestCase("leaderboardRankFormat", "Leaderboard Rank Format", "{1}")]
        [TestCase("levelLabelFormat", "Level Label Format", "Level {1}")]
        [TestCase("timerFormat", "Timer Format", "{0}:{2:00}")]
        [TestCase("timeBonusFormat", "Time Bonus Format", "+{1}")]
        [TestCase("jokerCountBadgeFormat", "Joker Count Badge Format", "{1}")]
        [TestCase("jokerPriceBadgeFormat", "Joker Price Badge Format", "{1}")]
        [TestCase("goldEarnedFormat", "Gold Earned Format", "+{1}")]
        [TestCase("loseLifeAndStreakFormat", "Lose Life And Streak Format", "You will lose a life and your {1}-win streak.")]
        public void Problems_UnusableFormat_IsReportedByName(string field, string shownName, string format)
        {
            SetString(field, format);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be a format"), string.Join(" | ", problems));
        }

        [TestCase("backgroundColor", "Background Color")]
        [TestCase("iconColor", "Icon Color")]
        [TestCase("idleTabIconColor", "Idle Tab Icon Color")]
        [TestCase("coinColor", "Coin Color")]
        [TestCase("heartColor", "Heart Color")]
        [TestCase("streakColor", "Streak Color")]
        [TestCase("buttonLabelColor", "Button Label Color")]
        [TestCase("disabledButtonLabelColor", "Disabled Button Label Color")]
        [TestCase("goldMedalColor", "Gold Medal Color")]
        [TestCase("silverMedalColor", "Silver Medal Color")]
        [TestCase("bronzeMedalColor", "Bronze Medal Color")]
        [TestCase("leaderboardHighlightColor", "Leaderboard Highlight Color")]
        [TestCase("leaderboardRowColor", "Leaderboard Row Color")]
        [TestCase("jokerSelectedColor", "Joker Selected Color")]
        [TestCase("timerColor", "Timer Color")]
        [TestCase("timerWarningColor", "Timer Warning Color")]
        [TestCase("timeBonusColor", "Time Bonus Color")]
        [TestCase("jokerBadgeTextColor", "Joker Badge Text Color")]
        [TestCase("shortPriceColor", "Short Price Color")]
        public void Problems_TransparentColor_IsReportedByName(string field, string shownName)
        {
            SetColor(field, new Color(1f, 1f, 1f, 0f));

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must not be fully transparent"), string.Join(" | ", problems));
        }

        [TestCase(UiColorRole.Background, "backgroundColor")]
        [TestCase(UiColorRole.Icon, "iconColor")]
        [TestCase(UiColorRole.IdleTabIcon, "idleTabIconColor")]
        [TestCase(UiColorRole.Coin, "coinColor")]
        [TestCase(UiColorRole.Heart, "heartColor")]
        [TestCase(UiColorRole.Streak, "streakColor")]
        [TestCase(UiColorRole.JokerSelected, "jokerSelectedColor")]
        public void ColorOf_Role_IsItsOwnField(UiColorRole role, string field)
        {
            // A colour no default has, so a role reading another field fails.
            var marker = new Color(0.12f, 0.34f, 0.56f, 0.78f);
            SetColor(field, marker);

            var color = config.ColorOf(role);

            Assert.AreEqual(marker, color);
        }

        [Test]
        public void Defaults_LevelHudTexts_AreTheSpecified()
        {
            Assert.AreEqual("Free Play", config.FreePlayLabel);
            Assert.AreEqual("Level {0}", config.LevelLabelFormat);
            Assert.AreEqual("{0}:{1:00}", config.TimerFormat, "the timer reads m:ss");
            Assert.AreEqual("+{0}", config.TimeBonusFormat, "the Clock's bonus reads +20");
            Assert.AreEqual("Get Gold", config.GoldPacksTitle);
        }

        [TestCase("timeBonusSeconds", 0f, "Time Bonus Seconds must be positive")]
        [TestCase("timerPulseSeconds", 0f, "Timer Pulse Seconds must be positive")]
        [TestCase("timeBonusRiseUnits", -1f, "Time Bonus Rise Units must be at least 0")]
        [TestCase("timerPulseScale", 0.9f, "Timer Pulse Scale must be at least 1")]
        public void Problems_UnusableLevelTiming_IsReportedByName(string field, float value, string expected)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(expected), string.Join(" | ", problems));
        }

        [Test]
        public void Defaults_LevelPanelTexts_AreTheSpecified()
        {
            Assert.AreEqual("Level Complete", config.LevelCompleteTitle);
            Assert.AreEqual("Time's Up", config.TimesUpTitle);
            Assert.AreEqual("+{0}", config.GoldEarnedFormat, "the gold reads +50 beside the coin");
            Assert.AreEqual("Thanks for playing!", config.ClosingMessage);
            Assert.AreEqual("Leave level?", config.LeaveLevelTitle);
            Assert.AreEqual("Restart level?", config.RestartLevelTitle);
            Assert.AreEqual("You will lose a life.", config.LoseLifeLine);
            Assert.AreEqual("You will lose a life and your {0}-win streak.", config.LoseLifeAndStreakFormat);
            Assert.AreEqual("Lose a life", config.LoseLifeLabel);
            Assert.AreEqual("Keep playing", config.KeepPlayingLabel);
        }

        [TestCase("overlayPopSeconds", "Overlay Pop Seconds must be positive")]
        [TestCase("overlayPopStartScale", "Overlay Pop Start Scale must be positive")]
        public void Problems_NonPositiveOverlayPop_IsReportedByName(string field, string expected)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(expected), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NegativeTimerWarningSeconds_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("timerWarningSeconds").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Timer Warning Seconds may not be negative"), string.Join(" | ", problems));
        }

        [Test]
        public void ColorOf_UndeclaredRole_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => config.ColorOf((UiColorRole)(-1)));
        }

        [TestCase(JokerKind.Clock, "clockName")]
        [TestCase(JokerKind.Rocket, "rocketName")]
        [TestCase(JokerKind.Broom, "broomName")]
        public void JokerName_Kind_IsItsOwnField(JokerKind kind, string field)
        {
            SetString(field, "Marker");

            var name = config.JokerName(kind);

            Assert.AreEqual("Marker", name);
        }

        [TestCase(1, "goldMedalColor")]
        [TestCase(2, "silverMedalColor")]
        [TestCase(3, "bronzeMedalColor")]
        public void TryGetMedalColor_FirstThreeRanks_IsTheirOwnField(int rank, string field)
        {
            var marker = new Color(0.12f, 0.34f, 0.56f, 0.78f);
            SetColor(field, marker);

            var hasMedal = config.TryGetMedalColor(rank, out var color);

            Assert.IsTrue(hasMedal);
            Assert.AreEqual(marker, color);
        }

        [TestCase(0)]
        [TestCase(4)]
        public void TryGetMedalColor_OtherRank_HasNoMedal(int rank)
        {
            var hasMedal = config.TryGetMedalColor(rank, out _);

            Assert.IsFalse(hasMedal);
        }

        [Test]
        public void JokerName_UndeclaredKind_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => config.JokerName((JokerKind)(-1)));
        }

        [TestCase(" ", "Popular", "b", "Best", "Ribbons element 0: Product Id is empty", TestName = "Problems_RibbonWithEmptyId_IsReported")]
        [TestCase("a", "Popular", "a", "Best", "Ribbons element 1: Product Id 'a' is used", TestName = "Problems_RibbonsWithRepeatedId_AreReported")]
        [TestCase("a", "", "b", "Best", "Ribbons element 0: Text is empty", TestName = "Problems_RibbonWithEmptyText_IsReported")]
        public void Problems_RibbonWithAProblem_IsReported(
            string firstId, string firstText, string secondId, string secondText, string expected)
        {
            SetRibbons((firstId, firstText), (secondId, secondText));

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(expected), string.Join(" | ", problems));
        }

        [Test]
        public void RibbonFor_KnownAndUnknownId_IsItsTextOrNull()
        {
            SetRibbons(("pack_a", "Hot"), ("pack_b", "Cheap"));

            var known = config.RibbonFor("pack_b");
            var unknown = config.RibbonFor("pack_c");

            Assert.AreEqual("Cheap", known);
            Assert.IsNull(unknown);
        }

        private void SetRibbons(params (string id, string text)[] ribbons)
        {
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty("ribbons");
            Assert.IsNotNull(entries, "UiConfig has no serialized field 'ribbons'.");
            entries.arraySize = ribbons.Length;
            for (var i = 0; i < ribbons.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("productId").stringValue = ribbons[i].id;
                entry.FindPropertyRelative("text").stringValue = ribbons[i].text;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetString(string field, string value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetColor(string field, Color value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
