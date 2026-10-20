using System;
using GateRush.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 38's <see cref="ProfileTexts"/>: the numeric statistics
    /// through the config's format in the invariant culture, the campaign
    /// level as Home's play button shows it or the all-played label, the
    /// refusal line from the config, and a missing config throwing.
    /// </summary>
    public class ProfileTextsTests
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

        [TestCase(0, "0")]
        [TestCase(7, "7")]
        [TestCase(12345, "12,345")]
        public void Statistic_Number_UsesTheFormatInTheInvariantCulture(int value, string expected)
        {
            var text = ProfileTexts.Statistic(value, config);

            Assert.AreEqual(expected, text);
        }

        [Test]
        public void CampaignLevel_Number_IsHomesPlayLabel()
        {
            var text = ProfileTexts.CampaignLevel(13, config);

            Assert.AreEqual(HomeTexts.PlayLabel(13, config), text);
            StringAssert.Contains("13", text);
        }

        [Test]
        public void CampaignLevel_None_IsTheAllPlayedLabel()
        {
            var text = ProfileTexts.CampaignLevel(null, config);

            Assert.AreEqual(config.AllPlayedLabel, text);
        }

        [Test]
        public void NameRefused_IsTheConfigsLine()
        {
            using (var serialized = new SerializedObject(config))
            {
                serialized.FindProperty("nameRefusedText").stringValue = "Try another name.";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var text = ProfileTexts.NameRefused(config);

            Assert.AreEqual("Try another name.", text);
        }

        [Test]
        public void Statistic_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ProfileTexts.Statistic(1, null));
        }
    }
}
