using System;
using System.Linq;
using GateRush.Meta;
using GateRush.UI;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 37's <see cref="StreakProgress"/>: for each streak of the
    /// default table of D61, the next reward, the wins left to it, the share
    /// of the bar filled and each reward's state; an empty table and a table
    /// of one reward; a threshold reached turns Collected and the following
    /// reward Next; the markers' places; and the argument checks.
    /// </summary>
    /// <remarks>
    /// States are written as one letter each: C for Collected, N for Next, A
    /// for Ahead.
    /// </remarks>
    public class StreakProgressTests
    {
        private const float Tolerance = 1e-6f;

        // No next reward.
        private const int None = 0;

        [TestCase(0, 3, 3, 0f, "NAA")]
        [TestCase(2, 3, 1, 0.2f, "NAA")]
        [TestCase(3, 5, 2, 0.3f, "CNA")]
        [TestCase(4, 5, 1, 0.4f, "CNA")]
        [TestCase(5, 10, 5, 0.5f, "CCN")]
        [TestCase(9, 10, 1, 0.9f, "CCN")]
        [TestCase(10, None, 0, 1f, "CCC")]
        [TestCase(12, None, 0, 1f, "CCC")]
        public void Of_DefaultTable_ShowsTheStreak(int streak, int nextWins, int winsToNext, float fill, string states)
        {
            var progress = StreakProgress.Of(streak, DefaultTable());

            Assert.AreEqual(nextWins, progress.NextReward?.Wins ?? None, "the next reward's wins");
            Assert.AreEqual(winsToNext, progress.WinsToNext, "the wins left");
            Assert.AreEqual(fill, progress.Fill, Tolerance, "the fill");
            Assert.AreEqual(states, Letters(progress), "the states");
        }

        [Test]
        public void Of_EmptyTable_NoNextRewardAndAnEmptyBar()
        {
            var progress = StreakProgress.Of(4, Array.Empty<StreakReward>());

            Assert.IsNull(progress.NextReward);
            Assert.AreEqual(0, progress.WinsToNext);
            Assert.AreEqual(0f, progress.Fill);
            Assert.IsEmpty(progress.States);
            Assert.IsEmpty(progress.Positions);
        }

        [TestCase(1, 2, 0.5f, "N", TestName = "Of_OneRewardBelowIt_IsNextAndHalfFilled")]
        [TestCase(2, None, 1f, "C", TestName = "Of_OneRewardReached_IsCollectedAndFull")]
        public void Of_OneReward_ShowsIt(int streak, int nextWins, float fill, string states)
        {
            var table = new[] { new StreakReward(2, JokerKind.Rocket, 1) };

            var progress = StreakProgress.Of(streak, table);

            Assert.AreEqual(nextWins, progress.NextReward?.Wins ?? None);
            Assert.AreEqual(fill, progress.Fill, Tolerance);
            Assert.AreEqual(states, Letters(progress));
        }

        [Test]
        public void Of_ReachingAThreshold_MakesItCollectedAndTheFollowingNext()
        {
            var before = StreakProgress.Of(4, DefaultTable());
            var after = StreakProgress.Of(5, DefaultTable());

            Assert.AreEqual(StreakRewardState.Next, before.States[1]);
            Assert.AreEqual(StreakRewardState.Collected, after.States[1]);
            Assert.AreEqual(StreakRewardState.Next, after.States[2]);
            Assert.AreEqual(JokerKind.Broom, after.NextReward?.Joker);
        }

        [Test]
        public void Of_DefaultTable_PositionsAreWinsOverTheLastRewardsWins()
        {
            var progress = StreakProgress.Of(0, DefaultTable());

            Assert.That(progress.Positions, Is.EqualTo(new[] { 0.3f, 0.5f, 1f }).Within(Tolerance));
        }

        [Test]
        public void Of_NegativeStreak_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StreakProgress.Of(-1, DefaultTable()));
        }

        [Test]
        public void Of_NullTable_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StreakProgress.Of(0, null));
        }

        /// <summary>The reward table of D61: 1 Clock at 3 wins, 1 Rocket at 5, 1 Broom at 10.</summary>
        private static StreakReward[] DefaultTable() => new[]
        {
            new StreakReward(3, JokerKind.Clock, 1),
            new StreakReward(5, JokerKind.Rocket, 1),
            new StreakReward(10, JokerKind.Broom, 1)
        };

        private static string Letters(StreakProgress progress) =>
            new string(progress.States.Select(Letter).ToArray());

        private static char Letter(StreakRewardState state)
        {
            switch (state)
            {
                case StreakRewardState.Collected:
                    return 'C';
                case StreakRewardState.Next:
                    return 'N';
                default:
                    return 'A';
            }
        }
    }
}
