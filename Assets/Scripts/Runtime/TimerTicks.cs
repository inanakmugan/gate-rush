namespace GateRush.Runtime
{
    /// <summary>
    /// When the countdown's last seconds tick (Module 25): once for each whole
    /// second from the tick-from second down to 1, as the displayed second
    /// reaches it. Zero never ticks — the loss sounds instead.
    /// </summary>
    public static class TimerTicks
    {
        /// <summary>
        /// True when the displayed second went down from
        /// <paramref name="previousWholeSeconds"/> to
        /// <paramref name="wholeSeconds"/> and the new one is a ticking
        /// second: at least 1 and at most <paramref name="tickFromSeconds"/>.
        /// A frame that skips seconds ticks once, for the second it lands on;
        /// a second that stays, or rises with a time bonus (M10), does not
        /// tick. With <paramref name="tickFromSeconds"/> at 0 nothing ticks.
        /// </summary>
        /// <param name="previousWholeSeconds">The displayed second before the frame (<see cref="Countdown.WholeSecondsRemaining"/>).</param>
        /// <param name="wholeSeconds">The displayed second after it.</param>
        /// <param name="tickFromSeconds">The first second that ticks, counting down.</param>
        public static bool ShouldTick(int previousWholeSeconds, int wholeSeconds, int tickFromSeconds) =>
            wholeSeconds < previousWholeSeconds && wholeSeconds >= 1 && wholeSeconds <= tickFromSeconds;
    }
}
