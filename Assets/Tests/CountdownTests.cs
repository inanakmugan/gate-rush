using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 12's <see cref="Countdown"/>: it runs only between start
    /// and stop, never goes below zero, expires exactly once, and takes time
    /// bonuses (M10) only while the level can still be won.
    /// </summary>
    public class CountdownTests
    {
        private const float Budget = 10f;
        private const float Tick = 3f;
        private const int Bonus = 5;

        [Test]
        public void Tick_BeforeStart_ChangesNothing()
        {
            var countdown = new Countdown(Budget);

            countdown.Tick(Tick);

            Assert.AreEqual(Budget, countdown.RemainingSeconds);
            Assert.IsFalse(countdown.IsRunning);
        }

        [Test]
        public void Tick_AfterStart_CountsDown()
        {
            var countdown = new Countdown(Budget);
            countdown.Start();

            countdown.Tick(Tick);

            Assert.AreEqual(Budget - Tick, countdown.RemainingSeconds, 1e-5f);
            Assert.IsTrue(countdown.IsRunning);
        }

        [Test]
        public void Tick_PastZero_HoldsAtZeroAndExpiresOnce()
        {
            var countdown = new Countdown(Budget);
            var expired = 0;
            countdown.Expired += () => expired++;
            countdown.Start();

            countdown.Tick(Budget * 3f);
            countdown.Tick(Tick);

            Assert.AreEqual(0f, countdown.RemainingSeconds);
            Assert.AreEqual(0, countdown.WholeSecondsRemaining);
            Assert.IsTrue(countdown.HasExpired);
            Assert.IsFalse(countdown.IsRunning);
            Assert.AreEqual(1, expired);
        }

        [Test]
        public void Tick_ExactlyToZero_Expires()
        {
            var countdown = new Countdown(Budget);
            var expired = 0;
            countdown.Expired += () => expired++;
            countdown.Start();

            countdown.Tick(Budget);

            Assert.IsTrue(countdown.HasExpired);
            Assert.AreEqual(1, expired);
        }

        [Test]
        public void Stop_FreezesTheRemainingTime()
        {
            var countdown = new Countdown(Budget);
            countdown.Start();
            countdown.Tick(Tick);

            countdown.Stop();
            countdown.Tick(Budget);

            Assert.AreEqual(Budget - Tick, countdown.RemainingSeconds, 1e-5f);
            Assert.IsFalse(countdown.HasExpired);
        }

        [Test]
        public void AddBonus_WhileRunning_ExtendsTheTime()
        {
            var countdown = new Countdown(Budget);
            countdown.Start();
            countdown.Tick(Tick);

            countdown.AddBonus(Bonus);

            Assert.AreEqual(Budget - Tick + Bonus, countdown.RemainingSeconds, 1e-5f);
        }

        [Test]
        public void AddBonus_AfterExpiry_ChangesNothing()
        {
            var countdown = new Countdown(Budget);
            countdown.Start();
            countdown.Tick(Budget);

            countdown.AddBonus(Bonus);

            Assert.AreEqual(0f, countdown.RemainingSeconds);
            Assert.IsTrue(countdown.HasExpired);
        }

        [Test]
        public void Start_AfterExpiry_DoesNotRun()
        {
            var countdown = new Countdown(Budget);
            countdown.Start();
            countdown.Tick(Budget);

            countdown.Start();

            Assert.IsFalse(countdown.IsRunning);
        }

        [TestCase(10f, 10)]
        [TestCase(9.2f, 10)]
        [TestCase(0.01f, 1)]
        public void WholeSecondsRemaining_RoundsUp(float remaining, int expected)
        {
            var countdown = new Countdown(Budget);
            countdown.Start();

            countdown.Tick(Budget - remaining);

            Assert.AreEqual(expected, countdown.WholeSecondsRemaining);
        }

        [Test]
        public void Reset_AfterExpiry_RestoresTheWholeBudgetStopped()
        {
            var countdown = new Countdown(Budget);
            countdown.Start();
            countdown.Tick(Budget);

            countdown.Reset();

            Assert.AreEqual(Budget, countdown.RemainingSeconds);
            Assert.IsFalse(countdown.HasExpired);
            Assert.IsFalse(countdown.IsRunning);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Constructor_NonPositiveBudget_Throws(float budget)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Countdown(budget));
        }
    }
}
