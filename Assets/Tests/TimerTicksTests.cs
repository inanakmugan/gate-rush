using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 25's <see cref="TimerTicks"/>: the countdown ticks once
    /// for each whole second from the tick-from second down to 1, never at
    /// zero — the loss sounds there — and never for a second that stays or
    /// rises.
    /// </summary>
    public class TimerTicksTests
    {
        private const int TickFrom = 5;

        [Test]
        public void ShouldTick_ReachingTheTickFromSecond_Ticks()
        {
            var ticks = TimerTicks.ShouldTick(TickFrom + 1, TickFrom, TickFrom);

            Assert.IsTrue(ticks);
        }

        [Test]
        public void ShouldTick_ReachingTheSecondAboveTickFrom_DoesNotTick()
        {
            var ticks = TimerTicks.ShouldTick(TickFrom + 2, TickFrom + 1, TickFrom);

            Assert.IsFalse(ticks);
        }

        [Test]
        public void ShouldTick_EverySecondFromTickFromDownToOne_Ticks()
        {
            var ticked = 0;

            for (var second = TickFrom; second >= 1; second--)
            {
                if (TimerTicks.ShouldTick(second + 1, second, TickFrom))
                {
                    ticked++;
                }
            }

            Assert.AreEqual(TickFrom, ticked, "one tick per second from tick-from down to 1");
        }

        [Test]
        public void ShouldTick_ReachingZero_DoesNotTick()
        {
            var ticks = TimerTicks.ShouldTick(1, 0, TickFrom);

            Assert.IsFalse(ticks);
        }

        [Test]
        public void ShouldTick_FrameThatSkipsASecond_TicksOnce()
        {
            // 5 to 3 is one frame, so one question and one tick; the next
            // frame, still at 3, does not tick again.
            var skipping = TimerTicks.ShouldTick(5, 3, TickFrom);
            var staying = TimerTicks.ShouldTick(3, 3, TickFrom);

            Assert.IsTrue(skipping);
            Assert.IsFalse(staying);
        }

        [Test]
        public void ShouldTick_FrameThatSkipsToZero_DoesNotTick()
        {
            var ticks = TimerTicks.ShouldTick(2, 0, TickFrom);

            Assert.IsFalse(ticks);
        }

        [Test]
        public void ShouldTick_SecondThatRisesWithABonus_DoesNotTick()
        {
            var ticks = TimerTicks.ShouldTick(2, 4, TickFrom);

            Assert.IsFalse(ticks);
        }

        [Test]
        public void ShouldTick_TickFromZero_NeverTicks()
        {
            var ticks = TimerTicks.ShouldTick(2, 1, 0);

            Assert.IsFalse(ticks);
        }
    }
}
