using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 31's <see cref="WinStreak"/>: a new player has none; a
    /// win raises the best only when the streak passes it; a reset keeps the
    /// best; the streak stops at <see cref="int.MaxValue"/>; and a restored
    /// best below the streak, or a negative value, throws.
    /// </summary>
    public class WinStreakTests
    {
        [Test]
        public void Starting_IsZeroWithBestZero()
        {
            var streak = WinStreak.Starting();

            Assert.AreEqual(0, streak.Current);
            Assert.AreEqual(0, streak.Best);
        }

        [Test]
        public void RecordWin_PassingBest_RaisesBest()
        {
            var streak = new WinStreak(3, 3);

            var current = streak.RecordWin();

            Assert.AreEqual(4, current);
            Assert.AreEqual(4, streak.Current);
            Assert.AreEqual(4, streak.Best);
        }

        [Test]
        public void RecordWin_BelowBest_KeepsBest()
        {
            var streak = new WinStreak(2, 6);

            streak.RecordWin();

            Assert.AreEqual(3, streak.Current);
            Assert.AreEqual(6, streak.Best);
        }

        [Test]
        public void Reset_AnyStreak_IsZeroAndKeepsBest()
        {
            var streak = new WinStreak(5, 7);

            streak.Reset();

            Assert.AreEqual(0, streak.Current);
            Assert.AreEqual(7, streak.Best);
        }

        [Test]
        public void RecordWin_AtIntMax_StopsAtIntMax()
        {
            var streak = new WinStreak(int.MaxValue, int.MaxValue);

            var current = streak.RecordWin();

            Assert.AreEqual(int.MaxValue, current);
            Assert.AreEqual(int.MaxValue, streak.Best);
        }

        [Test]
        public void Constructor_BestBelowCurrent_Throws()
        {
            Assert.Throws<ArgumentException>(() => new WinStreak(4, 3));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void Constructor_Negative_Throws(int current, int best)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WinStreak(current, best));
        }
    }
}
