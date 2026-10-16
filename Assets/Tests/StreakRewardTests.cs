using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 31's <see cref="StreakReward.ProblemsOf"/>, the one check
    /// of a reward table: a valid or empty table has no problem; a Wins below
    /// 1 or not above the element before it, a count below 1 and an undefined
    /// joker are each reported, naming the element.
    /// </summary>
    public class StreakRewardTests
    {
        [Test]
        public void ProblemsOf_ValidTable_IsEmpty()
        {
            var problems = StreakReward.ProblemsOf(MetaRulesFixture.StreakRewards());

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
        }

        [Test]
        public void ProblemsOf_EmptyTable_IsEmpty()
        {
            CollectionAssert.IsEmpty(StreakReward.ProblemsOf(Array.Empty<StreakReward>()));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ProblemsOf_WinsNotPositive_IsReported(int wins)
        {
            var problems = StreakReward.ProblemsOf(new[] { new StreakReward(wins, JokerKind.Clock, 1) });

            Assert.That(problems, Has.Some.Contains("element 0: Wins"), string.Join(" | ", problems));
        }

        [Test]
        public void ProblemsOf_WinsRepeated_IsReported()
        {
            var table = new[] { new StreakReward(4, JokerKind.Clock, 1), new StreakReward(4, JokerKind.Rocket, 1) };

            var problems = StreakReward.ProblemsOf(table);

            Assert.That(problems, Has.Some.Contains("element 1: Wins"), string.Join(" | ", problems));
        }

        [Test]
        public void ProblemsOf_WinsDescending_IsReported()
        {
            var table = new[] { new StreakReward(4, JokerKind.Clock, 1), new StreakReward(2, JokerKind.Rocket, 1) };

            var problems = StreakReward.ProblemsOf(table);

            Assert.That(problems, Has.Some.Contains("element 1: Wins"), string.Join(" | ", problems));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ProblemsOf_CountBelowOne_IsReported(int count)
        {
            var table = new[] { new StreakReward(2, JokerKind.Clock, 1), new StreakReward(3, JokerKind.Rocket, count) };

            var problems = StreakReward.ProblemsOf(table);

            Assert.That(problems, Has.Some.Contains("element 1: Count"), string.Join(" | ", problems));
        }

        [Test]
        public void ProblemsOf_UndefinedJoker_IsReported()
        {
            var problems = StreakReward.ProblemsOf(new[] { new StreakReward(2, (JokerKind)99, 1) });

            Assert.That(problems, Has.Some.Contains("element 0: Joker"), string.Join(" | ", problems));
        }

        [Test]
        public void ProblemsOf_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StreakReward.ProblemsOf(null));
        }
    }
}
