using System;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 32's <see cref="MetaText"/>, formatting only — the rules
    /// behind the numbers are <c>MetaGame</c>'s (Module 31): full lives show
    /// the count and the cap without a time; lives below the cap show the
    /// time to the next as minutes and seconds, rounded up as the countdown
    /// is; gold shows the balance; and every text comes from the config's
    /// format strings.
    /// </summary>
    /// <remarks>
    /// Most tests give the config formats of their own, unlike the defaults,
    /// so a text written into the code instead of read from the config fails.
    /// </remarks>
    public class MetaTextTests
    {
        private RuntimeConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<RuntimeConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Lives_Full_ShowsCountAndCapWithoutATime()
        {
            SetString("livesFullFormat", "L{0} of {1}");
            SetString("livesWaitingFormat", "W{0} of {1} in {2}");

            var text = MetaText.Lives(5, 5, null, config);

            Assert.AreEqual("L5 of 5", text);
        }

        [Test]
        public void Lives_BelowCap_ShowsTheTimeToTheNextAsMinutesAndSeconds()
        {
            SetString("livesFullFormat", "L{0} of {1}");
            SetString("livesWaitingFormat", "W{0} of {1} in {2}");

            var text = MetaText.Lives(3, 5, new TimeSpan(0, 12, 34), config);

            Assert.AreEqual("W3 of 5 in 12:34", text);
        }

        [Test]
        public void Lives_PartOfASecondLeft_RoundsUpAsTheTimerDoes()
        {
            SetString("livesWaitingFormat", "{2}");
            var wait = TimeSpan.FromSeconds(59.2);

            var text = MetaText.Lives(0, 5, wait, config);

            Assert.AreEqual(TimeFormat.MinutesSeconds((float)wait.TotalSeconds), text);
            Assert.AreEqual("01:00", text);
        }

        [Test]
        public void WholeSeconds_Wait_IsTheSecondTheTextShows()
        {
            var wait = TimeSpan.FromSeconds(59.2);

            var whole = MetaText.WholeSeconds(wait);

            Assert.AreEqual(TimeFormat.WholeSeconds((float)wait.TotalSeconds), whole);
            Assert.AreEqual(60, whole);
        }

        [Test]
        public void Gold_Balance_ShowsTheBalance()
        {
            SetString("goldFormat", "G={0}");

            var text = MetaText.Gold(640, config);

            Assert.AreEqual("G=640", text);
        }

        [Test]
        public void Defaults_LivesAndGold_ReadAsSpecified()
        {
            var full = MetaText.Lives(5, 5, null, config);
            var waiting = MetaText.Lives(3, 5, new TimeSpan(0, 12, 34), config);
            var gold = MetaText.Gold(640, config);

            Assert.AreEqual("Lives 5/5", full);
            Assert.AreEqual("Lives 3/5 | next 12:34", waiting);
            Assert.AreEqual("Gold 640", gold);
        }

        [Test]
        public void Joined_TwoParts_UsesTheConfigSeparator()
        {
            SetString("metaSeparator", " ~ ");

            var text = MetaText.Joined("first", "second", config);

            Assert.AreEqual("first ~ second", text);
        }

        [Test]
        public void GoldEarned_Reward_UsesTheConfigFormat()
        {
            SetString("goldEarnedFormat", "won {0}");

            var text = MetaText.GoldEarned(50, config);

            Assert.AreEqual("won 50", text);
        }

        [Test]
        public void NextLife_Wait_UsesTheConfigFormat()
        {
            SetString("nextLifeFormat", "back at {0}");

            var text = MetaText.NextLife(new TimeSpan(0, 12, 34), config);

            Assert.AreEqual("back at 12:34", text);
        }

        [Test]
        public void Refill_Price_UsesTheConfigFormatAndThePriceGiven()
        {
            SetString("refillFormat", "fill for {0}");

            var text = MetaText.Refill(123, config);

            Assert.AreEqual("fill for 123", text);
        }

        [Test]
        public void Lives_NoConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MetaText.Lives(1, 5, null, null));
        }

        private void SetString(string field, string value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
