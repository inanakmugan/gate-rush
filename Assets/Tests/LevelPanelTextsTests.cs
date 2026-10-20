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
    /// Covers Module 41's <see cref="LevelPanelTexts"/>, formatting only: the
    /// confirmation's line names no streak at 0 and the streak above it; the
    /// gold line reads through its format; the result titles and the
    /// confirmation titles are their own fields. Every text comes from the UI
    /// Config.
    /// </summary>
    /// <remarks>
    /// The tests give the config texts of their own, unlike the defaults, so a
    /// text written into the code instead of read from the config fails.
    /// </remarks>
    public class LevelPanelTextsTests
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
        public void ConfirmLine_NoStreak_IsTheLoseALifeLine()
        {
            SetString("loseLifeLine", "One life goes.");
            SetString("loseLifeAndStreakFormat", "One life and {0} wins go.");

            var line = LevelPanelTexts.ConfirmLine(0, config);

            Assert.AreEqual("One life goes.", line);
        }

        [TestCase(1, "One life and 1 wins go.")]
        [TestCase(4, "One life and 4 wins go.")]
        public void ConfirmLine_Streak_NamesTheStreak(int streak, string expected)
        {
            SetString("loseLifeLine", "One life goes.");
            SetString("loseLifeAndStreakFormat", "One life and {0} wins go.");

            var line = LevelPanelTexts.ConfirmLine(streak, config);

            Assert.AreEqual(expected, line);
        }

        [Test]
        public void GoldEarned_Gold_UsesTheFormat()
        {
            SetString("goldEarnedFormat", "won {0}");

            var text = LevelPanelTexts.GoldEarned(50, config);

            Assert.AreEqual("won 50", text);
        }

        [Test]
        public void ResultTitle_WinAndLoss_AreTheirOwnTitles()
        {
            SetString("levelCompleteTitle", "Cleared");
            SetString("timesUpTitle", "Out of time");

            var win = LevelPanelTexts.ResultTitle(LevelResult.FreePlayWin(true), config);
            var loss = LevelPanelTexts.ResultTitle(LevelResult.Loss(), config);

            Assert.AreEqual("Cleared", win);
            Assert.AreEqual("Out of time", loss);
        }

        [TestCase(ConfirmKind.Leave, "leaveLevelTitle")]
        [TestCase(ConfirmKind.Restart, "restartLevelTitle")]
        public void ConfirmTitle_Kind_IsItsOwnField(ConfirmKind kind, string field)
        {
            SetString(field, "Marker");

            var title = LevelPanelTexts.ConfirmTitle(kind, config);

            Assert.AreEqual("Marker", title);
        }

        [Test]
        public void ConfirmTitle_UndeclaredKind_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LevelPanelTexts.ConfirmTitle((ConfirmKind)(-1), config));
        }

        [Test]
        public void ConfirmLine_NoConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LevelPanelTexts.ConfirmLine(4, null));
        }

        [Test]
        public void GoldEarned_NoConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LevelPanelTexts.GoldEarned(50, null));
        }

        private void SetString(string field, string value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
