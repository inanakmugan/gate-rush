using System;
using GateRush.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 35's <see cref="HomeTexts"/>, formatting only: "Level N"
    /// and the all-played label; "Full" while the lives are full, otherwise
    /// the wait as m:ss rounded up as the countdown is; and every text from
    /// the config's formats.
    /// </summary>
    /// <remarks>
    /// Most tests give the config texts of their own, unlike the defaults, so
    /// a text written into the code instead of read from the config fails.
    /// </remarks>
    public class HomeTextsTests
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
        public void PlayLabel_Number_UsesThePlayLevelFormat()
        {
            SetString("playLevelFormat", "Stage {0}!");

            var text = HomeTexts.PlayLabel(7, config);

            Assert.AreEqual("Stage 7!", text);
        }

        [Test]
        public void PlayLabel_NoNumber_IsTheAllPlayedLabel()
        {
            SetString("allPlayedLabel", "Done for now");

            var text = HomeTexts.PlayLabel(null, config);

            Assert.AreEqual("Done for now", text);
        }

        [Test]
        public void Defaults_PlayAndStatus_ReadAsSpecified()
        {
            Assert.AreEqual("Level 3", HomeTexts.PlayLabel(3, config));
            Assert.AreEqual("All Played", HomeTexts.PlayLabel(null, config));
            Assert.AreEqual("Full", HomeTexts.LivesStatus(null, config));
            Assert.AreEqual("12:34", HomeTexts.LivesStatus(new TimeSpan(0, 12, 34), config));
            Assert.AreEqual("1:05", HomeTexts.LivesStatus(TimeSpan.FromSeconds(65), config));
            Assert.AreEqual("Refill (300)", HomeTexts.Refill(300, config));
            Assert.AreEqual("3/5", HomeTexts.LivesFraction(3, 5, config));
        }

        [Test]
        public void LivesStatus_Full_IsTheFullLabel()
        {
            SetString("livesFullLabel", "Topped up");

            var text = HomeTexts.LivesStatus(null, config);

            Assert.AreEqual("Topped up", text);
        }

        [Test]
        public void LivesStatus_Waiting_GivesTheWaitFormatMinutesAndSeconds()
        {
            SetString("waitFormat", "{0}m {1}s");

            var text = HomeTexts.LivesStatus(new TimeSpan(0, 12, 34), config);

            Assert.AreEqual("12m 34s", text);
        }

        [Test]
        public void LivesStatus_PartOfASecondLeft_RoundsUpAsTheTimerDoes()
        {
            SetString("waitFormat", "{0}m {1}s");

            var text = HomeTexts.LivesStatus(TimeSpan.FromSeconds(59.2), config);

            Assert.AreEqual("1m 0s", text);
        }

        [Test]
        public void Gold_Balance_UsesTheGoldFormat()
        {
            SetString("goldFormat", "{0} coins");

            var text = HomeTexts.Gold(640, config);

            Assert.AreEqual("640 coins", text);
        }

        [Test]
        public void LivesCount_Lives_UsesTheLivesCountFormat()
        {
            SetString("livesCountFormat", "x{0}");

            var text = HomeTexts.LivesCount(4, config);

            Assert.AreEqual("x4", text);
        }

        [Test]
        public void LivesFraction_LivesAndCap_UsesTheLivesFractionFormat()
        {
            SetString("livesFractionFormat", "{0} of {1}");

            var text = HomeTexts.LivesFraction(2, 5, config);

            Assert.AreEqual("2 of 5", text);
        }

        [Test]
        public void Refill_Price_UsesTheRefillFormatAndThePriceGiven()
        {
            SetString("refillFormat", "fill for {0}");

            var text = HomeTexts.Refill(123, config);

            Assert.AreEqual("fill for 123", text);
        }

        [Test]
        public void LevelNumber_Number_UsesTheLevelNumberFormat()
        {
            SetString("levelNumberFormat", "#{0}");

            var text = HomeTexts.LevelNumber(12, config);

            Assert.AreEqual("#12", text);
        }

        [Test]
        public void PlayLabel_NoConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => HomeTexts.PlayLabel(1, null));
        }

        private void SetString(string field, string value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
