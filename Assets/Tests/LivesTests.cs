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
    /// nothing, and put right again counts only the real time waited (D20),
    /// saved or not, while one left behind still counts the time between its
    /// readings,
    /// so a player at 0 lives is never locked out; a clock set forward gives
    /// what it reads and nothing more once corrected; and invalid counts and
    /// non-UTC times throw.
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
            rules = MetaRulesFixture.Build(lifeCap: Cap, lifeRegenInterval: Interval, startingGold: StartingGold);
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
        public void CountAt_ClockSetBackThenRestored_GivesOnlyTheRealTimeWaited()
        {
            // The review's case: 1 life, 30 minutes an interval; the clock is
            // set back 3 hours and read, then put right; 10 real minutes in all.
            var halfHour = TimeSpan.FromMinutes(30);
            var halfHourRules = MetaRulesFixture.Build(lifeCap: Cap, lifeRegenInterval: halfHour, startingGold: StartingGold);
            var lives = new Lives(halfHourRules, 1, Noon);
            lives.CountAt(clock.UtcNow);

            clock.Advance(-TimeSpan.FromHours(3));
            var countWhenSetBack = lives.CountAt(clock.UtcNow);
            clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(10));
            var countWhenRestored = lives.CountAt(clock.UtcNow);

            Assert.AreEqual(1, countWhenSetBack);
            Assert.AreEqual(1, countWhenRestored, "10 real minutes give no life");
            Assert.AreEqual(TimeSpan.FromMinutes(20), lives.UntilNextLifeAt(clock.UtcNow), "only the 10 real minutes count");
        }

        [Test]
        public void TryLose_AtCapWithClockSetBackThenRestored_GivesNoLife()
        {
            var lives = Lives.Full(rules);
            lives.CountAt(clock.UtcNow);
            clock.Advance(-TimeSpan.FromHours(3));

            lives.TryLose(clock.UtcNow);
            clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(10));

            Assert.AreEqual(Cap - 1, lives.CountAt(clock.UtcNow), "the wait starts at the latest time seen, not the earlier reading");
            Assert.AreEqual(Interval - TimeSpan.FromMinutes(10), lives.UntilNextLifeAt(clock.UtcNow), "only the 10 real minutes count");
        }

        [Test]
        public void TryLose_AfterRefillWithClockSetBackThenRestored_GivesNoLife()
        {
            var lives = new Lives(rules, 1, Noon - TimeSpan.FromMinutes(5));
            lives.Refill(clock.UtcNow);
            clock.Advance(-TimeSpan.FromHours(1));

            lives.TryLose(clock.UtcNow);
            clock.Advance(TimeSpan.FromHours(1) + OneSecond);

            Assert.AreEqual(Cap - 1, lives.CountAt(clock.UtcNow), "the refill's time is the latest seen");
        }

        [Test]
        public void CountAt_ClockSetBackSavedThenRestored_GivesNoLife()
        {
            var lives = new Lives(rules, 1, Noon);
            clock.Advance(-TimeSpan.FromHours(3));
            lives.CountAt(clock.UtcNow);

            var restored = new Lives(rules, lives.StoredCount, lives.RegenStartUtc);
            clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(10));

            Assert.AreEqual(Noon, lives.RegenStartUtc, "the start never moves back to the earlier reading");
            Assert.AreEqual(1, restored.CountAt(clock.UtcNow));
        }

        [Test]
        public void CountAt_RealTimePassing_GivesOneLifePerIntervalKeepingThePartOne()
        {
            var lives = new Lives(rules, 1, Noon);
            var partial = TimeSpan.FromMinutes(7);

            clock.Advance(Interval);
            var afterOne = lives.CountAt(clock.UtcNow);
            clock.Advance(Interval + partial);
            var afterTwo = lives.CountAt(clock.UtcNow);

            Assert.AreEqual(2, afterOne);
            Assert.AreEqual(3, afterTwo);
            Assert.AreEqual(Interval - partial, lives.UntilNextLifeAt(clock.UtcNow));
        }

        [Test]
        public void CountAt_ClockJumpedForward_GivesWhatItReadsAndNothingMoreOnceCorrected()
        {
            var lives = new Lives(rules, 1, Noon);

            clock.Advance(TimeSpan.FromHours(1));
            var countWhenAhead = lives.CountAt(clock.UtcNow);
            clock.Advance(-TimeSpan.FromHours(1) + TimeSpan.FromMinutes(10));
            var countWhenCorrected = lives.CountAt(clock.UtcNow);
            clock.Advance(Interval);
            var countOneIntervalLater = lives.CountAt(clock.UtcNow);

            Assert.AreEqual(4, countWhenAhead, "a clock set forward reads as time passed: three intervals");
            Assert.AreEqual(4, countWhenCorrected, "nothing is taken back, nothing is added");
            Assert.AreEqual(5, countOneIntervalLater, "the corrected clock still counts real time: no lockout");
        }

        [Test]
        public void CountAt_ClockSetBackAndLeftThere_GivesALifePerIntervalOfReadings()
        {
            var lives = new Lives(rules, 0, Noon);
            clock.Advance(-TimeSpan.FromHours(3));
            lives.CountAt(clock.UtcNow);

            clock.Advance(Interval + Interval - OneSecond);
            var count = lives.CountAt(clock.UtcNow);

            Assert.AreEqual(1, count);
            Assert.AreEqual(OneSecond, lives.UntilNextLifeAt(clock.UtcNow));
            Assert.AreEqual(Noon + Interval, lives.RegenStartUtc, "the saved start moved forward by the life's interval, never back");
        }

        [Test]
        public void CountAt_AnyClockSetBackAndRestored_NeverExceedsTheRealClock()
        {
            // A property over random readings: the device clock is set back by
            // a random amount, read for a while, and restored, again and again;
            // lives are lost only while it is right. The lives read through it
            // may never exceed those read through the real clock.
            var random = new System.Random(20261105);
            for (var run = 0; run < 200; run++)
            {
                var start = random.Next(0, Cap);
                var device = new Lives(rules, start, Noon);
                var real = new Lives(rules, start, Noon);
                var realNow = Noon;
                var behind = TimeSpan.Zero;
                for (var step = 0; step < 30; step++)
                {
                    realNow += TimeSpan.FromMinutes(random.Next(0, 15));
                    if (random.Next(4) == 0)
                    {
                        behind = behind == TimeSpan.Zero ? TimeSpan.FromMinutes(random.Next(1, 600)) : TimeSpan.Zero;
                    }

                    var deviceNow = realNow - behind;
                    if (behind == TimeSpan.Zero && random.Next(6) == 0 && device.CountAt(deviceNow) > 0 && real.CountAt(realNow) > 0)
                    {
                        device.TryLose(deviceNow);
                        real.TryLose(realNow);
                    }

                    Assert.LessOrEqual(device.CountAt(deviceNow), real.CountAt(realNow), $"run {run}, step {step}");
                }
            }
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
