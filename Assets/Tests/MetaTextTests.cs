using System;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="MetaText"/>, formatting only: a campaign win's gold
    /// line reads through the config's format (Module 32), and a wait reads
    /// as the whole seconds the countdown would show. The HUD's lives and
    /// gold moved to the level's interface in Module 40.
    /// </summary>
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
        public void WholeSeconds_Wait_IsTheSecondTheTextShows()
        {
            var wait = TimeSpan.FromSeconds(59.2);

            var whole = MetaText.WholeSeconds(wait);

            Assert.AreEqual(TimeFormat.WholeSeconds((float)wait.TotalSeconds), whole);
            Assert.AreEqual(60, whole);
        }

        [Test]
        public void GoldEarned_Reward_UsesTheConfigFormat()
        {
            SetString("goldEarnedFormat", "won {0}");

            var text = MetaText.GoldEarned(50, config);

            Assert.AreEqual("won 50", text);
        }

        [Test]
        public void GoldEarned_NoConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MetaText.GoldEarned(50, null));
        }

        private void SetString(string field, string value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
