using System;
using GateRush.Runtime;
using GateRush.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 40's <see cref="LevelHudTexts"/>, formatting only: the
    /// level label reads Free Play in free play and the level's number in the
    /// campaign; the timer reads minutes and seconds rounded up as the
    /// countdown is, and warns at the configured second; the time bonus and
    /// the joker badges read through their formats, a locked joker's "Lv 10"
    /// (Module 43) among them; the lives' status reads
    /// Full or the wait as the top bar does. Every text comes from the UI
    /// Config.
    /// </summary>
    /// <remarks>
    /// Most tests give the config texts of their own, unlike the defaults, so
    /// a text written into the code instead of read from the config fails.
    /// </remarks>
    public class LevelHudTextsTests
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
        public void LevelLabel_FreePlay_IsTheFreePlayLabelWhateverTheNumber()
        {
            SetString("freePlayLabel", "Practice");

            var text = LevelHudTexts.LevelLabel(true, 4, config);

            Assert.AreEqual("Practice", text);
        }

        [Test]
        public void LevelLabel_Campaign_UsesTheLevelFormat()
        {
            SetString("levelLabelFormat", "Stage #{0}");

            var text = LevelHudTexts.LevelLabel(false, 4, config);

            Assert.AreEqual("Stage #4", text);
        }

        [Test]
        public void LevelLabel_CampaignWithoutANumber_IsEmpty()
        {
            var text = LevelHudTexts.LevelLabel(false, null, config);

            Assert.AreEqual(string.Empty, text);
        }

        [TestCase(75f, "1:15")]
        [TestCase(59.2f, "1:00")]
        [TestCase(9.01f, "0:10")]
        [TestCase(0f, "0:00")]
        public void Timer_RemainingTime_ReadsMinutesAndSecondsRoundedUp(float remaining, string expected)
        {
            var text = LevelHudTexts.Timer(remaining, config);

            Assert.AreEqual(expected, text);
        }

        [Test]
        public void Timer_OwnFormat_IsUsed()
        {
            SetString("timerFormat", "{0}m{1}s");

            var text = LevelHudTexts.Timer(75f, config);

            Assert.AreEqual("1m15s", text);
        }

        [TestCase(10.5f, false)]
        [TestCase(10f, true)]
        public void IsTimerWarning_AtTheConfiguredSecond_Warns(float remaining, bool expected)
        {
            SetInt("timerWarningSeconds", 10);

            var isWarning = LevelHudTexts.IsTimerWarning(remaining, config);

            Assert.AreEqual(expected, isWarning);
        }

        [Test]
        public void TimeBonus_Seconds_UsesTheFormat()
        {
            SetString("timeBonusFormat", "plus {0}");

            var text = LevelHudTexts.TimeBonus(20, config);

            Assert.AreEqual("plus 20", text);
        }

        [Test]
        public void JokerBadge_CountAndPrice_UseTheirOwnFormats()
        {
            SetString("jokerCountBadgeFormat", "x{0}");
            SetString("jokerPriceBadgeFormat", "{0}g");

            var count = LevelHudTexts.JokerBadge(JokerButtons.Campaign(3, 70, 0, true, false), config);
            var price = LevelHudTexts.JokerBadge(JokerButtons.Campaign(0, 70, 0, true, false), config);

            Assert.AreEqual("x3", count);
            Assert.AreEqual("70g", price);
        }

        [Test]
        public void JokerBadge_LockedDefault_ReadsLvAndTheUnlockLevel()
        {
            var text = LevelHudTexts.JokerBadge(JokerButtons.Locked(10), config);

            Assert.AreEqual("Lv 10", text);
        }

        [Test]
        public void JokerBadge_LockedOwnFormat_IsUsed()
        {
            SetString("jokerLockedBadgeFormat", "L{0}");

            var text = LevelHudTexts.JokerBadge(JokerButtons.Locked(15), config);

            Assert.AreEqual("L15", text);
        }

        [Test]
        public void LivesStatus_FullOrWaiting_ReadsAsTheTopBar()
        {
            var full = LevelHudTexts.LivesStatus(null, config);
            var waiting = LevelHudTexts.LivesStatus(754, config);

            Assert.AreEqual(config.LivesFullLabel, full);
            Assert.AreEqual(HomeTexts.LivesStatus(TimeSpan.FromSeconds(754), config), waiting);
            Assert.AreEqual("12:34", waiting);
        }

        [Test]
        public void Timer_NoConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LevelHudTexts.Timer(1f, null));
        }

        private void SetString(string field, string value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInt(string field, int value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
