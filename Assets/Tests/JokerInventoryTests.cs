using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 31's <see cref="JokerInventory"/>: a new player holds
    /// the starting jokers of the rules, kind by kind; adding raises only the
    /// kind added and stops at <see cref="int.MaxValue"/>; using takes one and
    /// is false at 0; and negative counts and undefined kinds throw.
    /// </summary>
    public class JokerInventoryTests
    {
        private const int Clocks = 1;
        private const int Rockets = 4;
        private const int Brooms = 6;
        private const int Count = 3;

        [Test]
        public void Starting_AnyRules_HoldsEachKindsStartingCount([Values] JokerKind kind)
        {
            var rules = MetaRulesFixture.Build(startingJokers: new JokerAmounts(Clocks, Rockets, Brooms));

            var jokers = JokerInventory.Starting(rules);

            Assert.AreEqual(rules.StartingJokers(kind), jokers.CountOf(kind));
        }

        [Test]
        public void Starting_NullRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => JokerInventory.Starting(null));
        }

        [TestCase(-1, Rockets, Brooms)]
        [TestCase(Clocks, -1, Brooms)]
        [TestCase(Clocks, Rockets, -1)]
        public void Constructor_NegativeCount_Throws(int clocks, int rockets, int brooms)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new JokerInventory(clocks, rockets, brooms));
        }

        [Test]
        public void Constructor_Counts_AreHeldByKind()
        {
            var jokers = new JokerInventory(Clocks, Rockets, Brooms);

            Assert.AreEqual(Clocks, jokers.CountOf(JokerKind.Clock));
            Assert.AreEqual(Rockets, jokers.CountOf(JokerKind.Rocket));
            Assert.AreEqual(Brooms, jokers.CountOf(JokerKind.Broom));
        }

        [Test]
        public void Add_Count_RaisesOnlyThatKind()
        {
            var jokers = new JokerInventory(Clocks, Rockets, Brooms);

            jokers.Add(JokerKind.Rocket, Count);

            Assert.AreEqual(Clocks, jokers.CountOf(JokerKind.Clock));
            Assert.AreEqual(Rockets + Count, jokers.CountOf(JokerKind.Rocket));
            Assert.AreEqual(Brooms, jokers.CountOf(JokerKind.Broom));
        }

        [Test]
        public void Add_Zero_ChangesNothing()
        {
            var jokers = new JokerInventory(Clocks, Rockets, Brooms);

            jokers.Add(JokerKind.Broom, 0);

            Assert.AreEqual(Brooms, jokers.CountOf(JokerKind.Broom));
        }

        [Test]
        public void Add_NegativeCount_ThrowsAndChangesNothing()
        {
            var jokers = new JokerInventory(Clocks, Rockets, Brooms);

            Assert.Throws<ArgumentOutOfRangeException>(() => jokers.Add(JokerKind.Clock, -1));
            Assert.AreEqual(Clocks, jokers.CountOf(JokerKind.Clock));
        }

        [Test]
        public void Add_PastIntMax_StopsAtIntMax()
        {
            var jokers = new JokerInventory(int.MaxValue - 1, Rockets, Brooms);

            jokers.Add(JokerKind.Clock, Count);

            Assert.AreEqual(int.MaxValue, jokers.CountOf(JokerKind.Clock));
        }

        [Test]
        public void TryUse_AboveZero_TakesOne()
        {
            var jokers = new JokerInventory(Clocks, Rockets, Brooms);

            var isUsed = jokers.TryUse(JokerKind.Broom);

            Assert.IsTrue(isUsed);
            Assert.AreEqual(Brooms - 1, jokers.CountOf(JokerKind.Broom));
            Assert.AreEqual(Rockets, jokers.CountOf(JokerKind.Rocket));
        }

        [Test]
        public void TryUse_AtZero_IsFalseAndChangesNothing()
        {
            var jokers = new JokerInventory(Clocks, 0, Brooms);

            var isUsed = jokers.TryUse(JokerKind.Rocket);

            Assert.IsFalse(isUsed);
            Assert.AreEqual(0, jokers.CountOf(JokerKind.Rocket));
        }

        [Test]
        public void Members_UndefinedKind_Throw()
        {
            var jokers = new JokerInventory(Clocks, Rockets, Brooms);
            var undefined = (JokerKind)99;

            Assert.Throws<ArgumentOutOfRangeException>(() => jokers.CountOf(undefined), nameof(JokerInventory.CountOf));
            Assert.Throws<ArgumentOutOfRangeException>(() => jokers.Add(undefined, Count), nameof(JokerInventory.Add));
            Assert.Throws<ArgumentOutOfRangeException>(() => jokers.TryUse(undefined), nameof(JokerInventory.TryUse));
        }
    }
}
