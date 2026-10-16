using System;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 33's <see cref="FreePlayJokers"/> (D66): it starts with
    /// its allowance of every kind, a use takes one of that kind only, a use
    /// at 0 is false and changes nothing, <see cref="FreePlayJokers.Refill"/>
    /// restores every kind, a negative allowance throws and 0 is allowed, and
    /// an undefined kind throws.
    /// </summary>
    public class FreePlayJokersTests
    {
        // Differs from MetaConfig's default, so a hardcoded allowance fails.
        private const int PerLevel = 2;

        [Test]
        public void Constructor_PerLevel_StartsWithThatManyOfEachKind([Values] JokerKind kind)
        {
            var jokers = new FreePlayJokers(PerLevel);

            var count = jokers.CountOf(kind);

            Assert.AreEqual(PerLevel, count);
        }

        [Test]
        public void Constructor_NegativePerLevel_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FreePlayJokers(-1));
        }

        [Test]
        public void Constructor_ZeroPerLevel_EveryTryUseIsFalse([Values] JokerKind kind)
        {
            var jokers = new FreePlayJokers(0);

            var used = jokers.TryUse(kind);

            Assert.IsFalse(used);
            Assert.AreEqual(0, jokers.CountOf(kind));
        }

        [Test]
        public void TryUse_WithSomeLeft_TakesOneOfThatKindOnly([Values] JokerKind kind)
        {
            var jokers = new FreePlayJokers(PerLevel);

            var used = jokers.TryUse(kind);

            Assert.IsTrue(used);
            foreach (JokerKind other in Enum.GetValues(typeof(JokerKind)))
            {
                Assert.AreEqual(other == kind ? PerLevel - 1 : PerLevel, jokers.CountOf(other), other.ToString());
            }
        }

        [Test]
        public void TryUse_AtZero_IsFalseAndChangesNothing([Values] JokerKind kind)
        {
            var jokers = new FreePlayJokers(PerLevel);
            UseAll(jokers, kind);

            var used = jokers.TryUse(kind);

            Assert.IsFalse(used);
            Assert.AreEqual(0, jokers.CountOf(kind));
        }

        [Test]
        public void Refill_AfterUses_RestoresAllThree()
        {
            var jokers = new FreePlayJokers(PerLevel);
            foreach (JokerKind kind in Enum.GetValues(typeof(JokerKind)))
            {
                UseAll(jokers, kind);
            }

            jokers.Refill();

            foreach (JokerKind kind in Enum.GetValues(typeof(JokerKind)))
            {
                Assert.AreEqual(PerLevel, jokers.CountOf(kind), kind.ToString());
            }
        }

        [Test]
        public void CountOfAndTryUse_UndefinedKind_Throw()
        {
            var jokers = new FreePlayJokers(PerLevel);
            var undefined = (JokerKind)99;

            Assert.Throws<ArgumentOutOfRangeException>(() => jokers.CountOf(undefined));
            Assert.Throws<ArgumentOutOfRangeException>(() => jokers.TryUse(undefined));
        }

        private static void UseAll(FreePlayJokers jokers, JokerKind kind)
        {
            for (var i = 0; i < PerLevel; i++)
            {
                Assert.IsTrue(jokers.TryUse(kind), $"use {i} of {kind}");
            }
        }
    }
}
