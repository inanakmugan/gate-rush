using System;
using GateRush.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 39's <see cref="LeaderboardTexts"/>: the score with
    /// thousands separators and the rank, from the UI Config's default
    /// formats, and a missing config throwing.
    /// </summary>
    public class LeaderboardTextsTests
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

        [TestCase(40, "40")]
        [TestCase(1250, "1,250")]
        [TestCase(1000000, "1,000,000")]
        public void Score_Value_HasThousandsSeparators(int score, string expected)
        {
            var text = LeaderboardTexts.Score(score, config);

            Assert.AreEqual(expected, text);
        }

        [TestCase(1, "1")]
        [TestCase(21, "21")]
        public void Rank_Value_IsTheNumber(int rank, string expected)
        {
            var text = LeaderboardTexts.Rank(rank, config);

            Assert.AreEqual(expected, text);
        }

        [Test]
        public void Score_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LeaderboardTexts.Score(1, null));
        }

        [Test]
        public void Rank_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LeaderboardTexts.Rank(1, null));
        }
    }
}
