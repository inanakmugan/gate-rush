using System;
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
    /// own field.
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
        [TestCase("buttonLabelColor", "Button Label Color")]
        [TestCase("disabledButtonLabelColor", "Disabled Button Label Color")]
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
        public void ColorOf_Role_IsItsOwnField(UiColorRole role, string field)
        {
            // A colour no default has, so a role reading another field fails.
            var marker = new Color(0.12f, 0.34f, 0.56f, 0.78f);
            SetColor(field, marker);

            var color = config.ColorOf(role);

            Assert.AreEqual(marker, color);
        }

        [Test]
        public void ColorOf_UndeclaredRole_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => config.ColorOf((UiColorRole)(-1)));
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
