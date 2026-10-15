using System;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 30's <see cref="MetaConfig"/>: a fresh asset is usable;
    /// a cap below 1, regeneration minutes that are not positive and negative
    /// starting gold are each reported by <see cref="MetaConfig.Problems"/>,
    /// naming the field; and <see cref="MetaConfig.ToRules"/> carries the
    /// values over.
    /// </summary>
    public class MetaConfigTests
    {
        private const string LifeCapField = "lifeCap";
        private const string LifeRegenMinutesField = "lifeRegenMinutes";
        private const string StartingGoldField = "startingGold";

        private MetaConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<MetaConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void Problems_FreshConfig_IsEmpty()
        {
            var problems = config.Problems();

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Problems_CapBelowOne_IsReported(int cap)
        {
            SetInt(LifeCapField, cap);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Life Cap"), string.Join(" | ", problems));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Problems_RegenMinutesNotPositive_IsReported(int minutes)
        {
            SetInt(LifeRegenMinutesField, minutes);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Life Regen Minutes"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NegativeStartingGold_IsReported()
        {
            SetInt(StartingGoldField, -1);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Starting Gold"), string.Join(" | ", problems));
        }

        [Test]
        public void ToRules_SetValues_CarriesThemOver()
        {
            const int cap = 3;
            const int minutes = 7;
            const int gold = 11;
            SetInt(LifeCapField, cap);
            SetInt(LifeRegenMinutesField, minutes);
            SetInt(StartingGoldField, gold);

            var rules = config.ToRules();

            Assert.AreEqual(cap, rules.LifeCap);
            Assert.AreEqual(TimeSpan.FromMinutes(minutes), rules.LifeRegenInterval);
            Assert.AreEqual(gold, rules.StartingGold);
        }

        private void SetInt(string field, int value)
        {
            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"MetaConfig has no serialized field '{field}'.");
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
