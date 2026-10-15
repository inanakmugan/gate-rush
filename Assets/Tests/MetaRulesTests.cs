using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 30's <see cref="MetaRules"/>: a cap below 1, an interval
    /// that is not positive and negative starting gold each throw, and valid
    /// values are carried as given.
    /// </summary>
    public class MetaRulesTests
    {
        private const int Cap = 3;
        private const int StartingGold = 250;

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(7);

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_CapBelowOne_Throws(int cap)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MetaRules(cap, Interval, StartingGold));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_IntervalNotPositive_Throws(int minutes)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MetaRules(Cap, TimeSpan.FromMinutes(minutes), StartingGold));
        }

        [Test]
        public void Constructor_NegativeStartingGold_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MetaRules(Cap, Interval, -1));
        }

        [Test]
        public void Constructor_ValidValues_CarriesThem()
        {
            var rules = new MetaRules(Cap, Interval, StartingGold);

            Assert.AreEqual(Cap, rules.LifeCap);
            Assert.AreEqual(Interval, rules.LifeRegenInterval);
            Assert.AreEqual(StartingGold, rules.StartingGold);
        }

        [Test]
        public void Constructor_ZeroStartingGold_IsAllowed()
        {
            var rules = new MetaRules(Cap, Interval, 0);

            Assert.AreEqual(0, rules.StartingGold);
        }
    }
}
