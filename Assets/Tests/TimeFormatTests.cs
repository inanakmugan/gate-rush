using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 17's <see cref="TimeFormat"/>: the countdown reads
    /// <c>mm:ss</c>, rounded up so it shows <c>00:00</c> only at expiry, and the
    /// warning colour follows the displayed second, not the raw time.
    /// </summary>
    public class TimeFormatTests
    {
        [TestCase(150f, "02:30")]
        [TestCase(600f, "10:00")]
        public void MinutesSeconds_WholeSeconds_ReadsMinutesAndSeconds(float seconds, string expected)
        {
            var text = TimeFormat.MinutesSeconds(seconds);

            Assert.AreEqual(expected, text);
        }

        [Test]
        public void MinutesSeconds_59Point2_RoundsUpTo0100()
        {
            var text = TimeFormat.MinutesSeconds(59.2f);

            Assert.AreEqual("01:00", text);
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        public void MinutesSeconds_ZeroOrNegative_Reads0000(float seconds)
        {
            var text = TimeFormat.MinutesSeconds(seconds);

            Assert.AreEqual("00:00", text);
        }

        [Test]
        public void MinutesSeconds_JustAboveZero_Reads0001()
        {
            var text = TimeFormat.MinutesSeconds(0.01f);

            Assert.AreEqual("00:01", text);
        }

        [Test]
        public void MinutesSeconds_6000_MinutesDoNotWrap()
        {
            var text = TimeFormat.MinutesSeconds(6000f);

            Assert.AreEqual("100:00", text);
        }

        [TestCase(2.1f, 3)]
        [TestCase(3f, 3)]
        [TestCase(0f, 0)]
        [TestCase(-1f, 0)]
        [TestCase(float.NaN, 0)]
        public void WholeSeconds_Any_RoundsUpAndNeverGoesBelowZero(float seconds, int expected)
        {
            var whole = TimeFormat.WholeSeconds(seconds);

            Assert.AreEqual(expected, whole);
        }

        [Test]
        public void IsWarning_JustAboveTheThresholdsDisplayedSecond_IsWhite()
        {
            // 10.2 s reads 00:11: the displayed second is above a threshold of 10.
            const float remaining = 10.2f;

            var isWarning = TimeFormat.IsWarning(remaining, 10);

            Assert.AreEqual("00:11", TimeFormat.MinutesSeconds(remaining));
            Assert.IsFalse(isWarning);
        }

        [Test]
        public void IsWarning_DisplayedSecondReachesTheThreshold_IsRed()
        {
            // 9.8 s reads 00:10: red from the moment 00:10 is shown, although
            // the raw time is not yet below 10 when the display first reads it.
            const float remaining = 9.8f;

            var isWarning = TimeFormat.IsWarning(remaining, 10);

            Assert.AreEqual("00:10", TimeFormat.MinutesSeconds(remaining));
            Assert.IsTrue(isWarning);
        }

        [Test]
        public void IsWarning_SameDisplayedSecond_SameColour()
        {
            // 10.0 and 9.01 both read 00:10, so both are red.
            var atTen = TimeFormat.IsWarning(10f, 10);
            var justBelow = TimeFormat.IsWarning(9.01f, 10);

            Assert.IsTrue(atTen);
            Assert.IsTrue(justBelow);
        }

        [Test]
        public void IsWarning_ZeroThreshold_IsRedOnlyAtExpiry()
        {
            var atOneSecond = TimeFormat.IsWarning(0.5f, 0);
            var atExpiry = TimeFormat.IsWarning(0f, 0);

            Assert.IsFalse(atOneSecond);
            Assert.IsTrue(atExpiry);
        }
    }
}
