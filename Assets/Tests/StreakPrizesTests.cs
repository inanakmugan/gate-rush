using System;
using System.Linq;
using GateRush.Meta;
using GateRush.UI;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 37's <see cref="StreakPrizes.Merge"/>: the rewards of
    /// several campaign wins announced together become one prize per joker
    /// kind, the counts added up, in the order Clock, Rocket, Broom whatever
    /// order the wins came in; no rewards give no prizes; and the argument
    /// checks.
    /// </summary>
    public class StreakPrizesTests
    {
        [Test]
        public void Merge_NoRewards_IsEmpty()
        {
            var prizes = StreakPrizes.Merge(Array.Empty<StreakReward>());

            Assert.IsEmpty(prizes);
        }

        [Test]
        public void Merge_TwoClocks_OneClockPrizeOfTwo()
        {
            var rewards = new[] { new StreakReward(3, JokerKind.Clock, 1), new StreakReward(3, JokerKind.Clock, 1) };

            var prizes = StreakPrizes.Merge(rewards);

            Assert.AreEqual(1, prizes.Count);
            Assert.AreEqual(JokerKind.Clock, prizes[0].Joker);
            Assert.AreEqual(2, prizes[0].Count);
        }

        [Test]
        public void Merge_KindsOutOfOrder_ComeAsClockRocketBroomWithTheirSums()
        {
            var rewards = new[]
            {
                new StreakReward(10, JokerKind.Broom, 1),
                new StreakReward(3, JokerKind.Clock, 1),
                new StreakReward(5, JokerKind.Rocket, 2),
                new StreakReward(3, JokerKind.Clock, 3)
            };

            var prizes = StreakPrizes.Merge(rewards);

            CollectionAssert.AreEqual(new[] { JokerKind.Clock, JokerKind.Rocket, JokerKind.Broom }, prizes.Select(prize => prize.Joker));
            CollectionAssert.AreEqual(new[] { 4, 2, 1 }, prizes.Select(prize => prize.Count));
        }

        [Test]
        public void Merge_NullRewards_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StreakPrizes.Merge(null));
        }

        [Test]
        public void Merge_UndeclaredKind_Throws()
        {
            var rewards = new[] { new StreakReward(3, (JokerKind)(-1), 1) };

            Assert.Throws<ArgumentOutOfRangeException>(() => StreakPrizes.Merge(rewards));
        }
    }
}
