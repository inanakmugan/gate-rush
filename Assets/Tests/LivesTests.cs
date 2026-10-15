using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 30's <see cref="Lives"/> against a
    /// <see cref="ManualClock"/>, with rules of cap 5 and 20 minutes: lives come
    /// back from timestamps, one per full interval, up to the cap; a wait
    /// running is never restarted by losing another; a clock set back gives
    /// nothing and restarts the wait, the queries included, so a player at 0
    /// lives is never locked out; and invalid counts and non-UTC times throw.
    /// </summary>
    public class LivesTests
    {
        private const int Cap = 5;
        private const int StartingGold = 0;

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
        private static readonly DateTime Noon = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

        private MetaRules rules;
        private ManualClock clock;

        [SetUp]
        public void SetUp()
        {
            rules = new MetaRules(Cap, Interval, StartingGold);
            clock = new ManualClock(Noon);
        }

        [Test]
        public void Full_AnyTime_IsFiveFullWithNoTimeToNext()
        {
            var lives = Lives.Full(rules);

            Assert.AreEqual(Cap, lives.CountAt(clock.UtcNow));
            Assert.IsTrue(lives.IsFullAt(clock.UtcNow));
            Assert.IsNull(lives.UntilNextLifeAt(clock.UtcNow));
        }

        [Test]
        public void TryLose_AtCap_GivesFourUntilTwentyMinutesPass()
        {
            var lives = Lives.Full(rules);

            var isLost = lives.TryLose(clock.UtcNow);

            Assert.IsTrue(isLost);
            Assert.AreEqual(Cap - 1, lives.CountAt(clock.UtcNow));
            clock.Advance(Interval - OneSecond);
            Assert.AreEqual(Cap - 1, lives.CountAt(clock.UtcNow), "one second short of an interval");
            clock.Advance(OneSecond);
            Assert.AreEqual(Cap, lives.CountAt(clock.UtcNow), "a full interval");
        }

        [Test]
        public void TryLose_ThreeAtOnce_GivesFourAfterFortyMinutesWithTwentyToNext()
        {
            var lives = Lives.Full(rules);
            for (var i = 0; i < 3; i++)
            {
                lives.TryLose(clock.UtcNow);
            }

            clock.Advance(Interval + Interval);
            var count = lives.CountAt(clock.UtcNow);
            var untilNext = lives.UntilNextLifeAt(clock.UtcNow);

            Assert.AreEqual(Cap - 1, count);
            Assert.AreEqual(Interval, untilNext);
        }

        [Test]
        public void TryLose_WhileALifeIsComing_KeepsItsWait()
        {
            var lives = Lives.Full(rules);
            lives.TryLose(clock.UtcNow);
            var waited = TimeSpan.FromMinutes(15);
            clock.Advance(waited);

            lives.TryLose(clock.UtcNow);

            Assert.AreEqual(Interval - waited, lives.UntilNextLifeAt(clock.UtcNow));
            clock.Advance(Interval - waited);
            Assert.AreEqual(Cap - 1, lives.CountAt(clock.UtcNow), "the first life came back on the first wait's time");
        }

        [Test]
        public void CountAt_TwoHoursAfterLosingOne_StopsAtCap()
        {
            var lives = Lives.Full(rules);
            lives.TryLose(clock.UtcNow);

            clock.Advance(TimeSpan.FromHours(2));

            Assert.AreEqual(Cap, lives.CountAt(clock.UtcNow));
            Assert.IsTrue(lives.IsFullAt(clock.UtcNow));
        }

        [Test]
        public void RoadmapCase_WinThreeLoseOneWaitForty_IsFullAndNoMore()
        {
            // Winning never touches the lives, so three wins leave them full;
            // only the lost attempt costs one.
            var lives = Lives.Full(rules);
            lives.TryLose(clock.UtcNow);

            clock.Advance(Interval + Interval);

            Assert.AreEqual(Cap, lives.CountAt(clock.UtcNow));
            Assert.IsTrue(lives.IsFullAt(clock.UtcNow));
            Assert.IsNull(lives.UntilNextLifeAt(clock.UtcNow));
        }

        [Test]
        public void CountAt_ClockSetBack_GivesNoLife()
        {
            var lives = Lives.Full(rules);
            lives.TryLose(clock.UtcNow);

            clock.Advance(-TimeSpan.FromHours(1));

            Assert.AreEqual(Cap - 1, lives.CountAt(clock.UtcNow));
        }

        [Test]
        public void TryLose_ClockSetBack_RestartsTheWaitFromTheEarlierTime()
        {
            var lives = Lives.Full(rules);
            lives.TryLose(clock.UtcNow);
            clock.Advance(-TimeSpan.FromHours(1));

            lives.TryLose(clock.UtcNow);

            Assert.AreEqual(Cap - 2, lives.CountAt(clock.UtcNow));
            clock.Advance(Interval);
            Assert.AreEqual(Cap - 1, lives.CountAt(clock.UtcNow), "one interval after the earlier time");
        }

        [Test]
        public void CountAt_ClockSetBackWithNoLives_GivesALifeOneIntervalLater()
        {
            var lives = Lives.Full(rules);
            for (var i = 0; i < Cap; i++)
            {
                lives.TryLose(clock.UtcNow);
            }

            clock.Advance(-TimeSpan.FromHours(1));
            var countWhenSetBack = lives.CountAt(clock.UtcNow);
            clock.Advance(Interval);

            Assert.AreEqual(0, countWhenSetBack);
            Assert.AreEqual(1, lives.CountAt(clock.UtcNow), "the query restarted the wait; nothing else could at 0 lives");
        }

        [Test]
        public void UntilNextLifeAt_ClockSetBack_IsOneInterval()
        {
            var lives = Lives.Full(rules);
            lives.TryLose(clock.UtcNow);
            clock.Advance(-TimeSpan.FromHours(1));

            var untilNext = lives.UntilNextLifeAt(clock.UtcNow);

            Assert.AreEqual(Interval, untilNext);
            clock.Advance(OneSecond);
            Assert.AreEqual(Interval - OneSecond, lives.UntilNextLifeAt(clock.UtcNow), "it then counts down");
        }

        [Test]
        public void UntilNextLifeAt_ClockSetBack_IsNeverNegative()
        {
            var lives = Lives.Full(rules);
            lives.TryLose(clock.UtcNow);
            clock.Advance(-TimeSpan.FromDays(1));

            var untilNext = lives.UntilNextLifeAt(clock.UtcNow);

            Assert.IsNotNull(untilNext);
            Assert.That(untilNext.Value, Is.GreaterThan(TimeSpan.Zero).And.LessThanOrEqualTo(Interval));
        }

        [Test]
        public void CountAt_Later_SettlesStoredCount()
        {
            var lives = Lives.Full(rules);
            for (var i = 0; i < 3; i++)
            {
                lives.TryLose(clock.UtcNow);
            }

            var partial = TimeSpan.FromMinutes(10);
            clock.Advance(Interval + Interval + partial);
            lives.CountAt(clock.UtcNow);

            Assert.AreEqual(Cap - 1, lives.StoredCount);
            Assert.AreEqual(clock.UtcNow - partial, lives.RegenStartUtc, "moved by whole intervals, the partial one kept");
        }

        [Test]
        public void TryLose_AtZero_IsFalseAndChangesNothing()
        {
            var lives = new Lives(rules, 0, Noon);

            var isLost = lives.TryLose(clock.UtcNow);

            Assert.IsFalse(isLost);
            Assert.AreEqual(0, lives.StoredCount);
            Assert.AreEqual(Noon, lives.RegenStartUtc);
        }

        [Test]
        public void Refill_BelowCap_GivesTheCap()
        {
            var lives = new Lives(rules, 1, Noon);

            lives.Refill(clock.UtcNow);

            Assert.AreEqual(Cap, lives.CountAt(clock.UtcNow));
            Assert.IsTrue(lives.IsFullAt(clock.UtcNow));
        }

        [Test]
        public void Constructor_CountAboveCap_ReadsAsCap()
        {
            var lives = new Lives(rules, Cap + 3, Noon);

            Assert.AreEqual(Cap, lives.StoredCount);
            Assert.AreEqual(Cap, lives.CountAt(clock.UtcNow));
        }

        [Test]
        public void Constructor_NegativeCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Lives(rules, -1, Noon));
        }

        [Test]
        public void Constructor_NullRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Lives(null, Cap, Noon));
            Assert.Throws<ArgumentNullException>(() => Lives.Full(null));
        }

        [Test]
        public void Members_NonUtcTime_Throw([Values(DateTimeKind.Local, DateTimeKind.Unspecified)] DateTimeKind kind)
        {
            var time = DateTime.SpecifyKind(Noon, kind);
            var lives = new Lives(rules, 1, Noon);

            Assert.Throws<ArgumentException>(() => new Lives(rules, 1, time), "constructor");
            Assert.Throws<ArgumentException>(() => lives.CountAt(time), nameof(Lives.CountAt));
            Assert.Throws<ArgumentException>(() => lives.IsFullAt(time), nameof(Lives.IsFullAt));
            Assert.Throws<ArgumentException>(() => lives.UntilNextLifeAt(time), nameof(Lives.UntilNextLifeAt));
            Assert.Throws<ArgumentException>(() => lives.TryLose(time), nameof(Lives.TryLose));
            Assert.Throws<ArgumentException>(() => lives.Refill(time), nameof(Lives.Refill));
        }
    }
}
