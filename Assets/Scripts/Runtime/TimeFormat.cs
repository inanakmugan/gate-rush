using System;
using System.Globalization;

namespace GateRush.Runtime
{
    /// <summary>
    /// How the countdown reads on screen: whole seconds rounded up, written as
    /// minutes and seconds, and whether the number shown has reached the
    /// warning threshold. The text and the warning colour share one rounding
    /// rule, so the colour changes exactly when the displayed number does.
    /// </summary>
    public static class TimeFormat
    {
        private const int SecondsPerMinute = 60;

        /// <summary>
        /// <paramref name="seconds"/> rounded up to a whole second, never below
        /// zero: the display reads 1 until the time is truly gone, and 0 only
        /// once it has expired. Not-a-number reads 0.
        /// </summary>
        public static int WholeSeconds(float seconds)
        {
            if (!(seconds > 0f))
            {
                return 0;
            }

            var whole = Math.Ceiling(seconds);
            return whole >= int.MaxValue ? int.MaxValue : (int)whole;
        }

        /// <summary>
        /// <paramref name="seconds"/> as <c>mm:ss</c> of its
        /// <see cref="WholeSeconds"/>: 150 reads <c>02:30</c>, 59.2 reads
        /// <c>01:00</c>, zero and below read <c>00:00</c>. Minutes do not wrap:
        /// 6000 reads <c>100:00</c>.
        /// </summary>
        public static string MinutesSeconds(float seconds)
        {
            var whole = WholeSeconds(seconds);
            return string.Format(
                CultureInfo.InvariantCulture, "{0:00}:{1:00}", whole / SecondsPerMinute, whole % SecondsPerMinute);
        }

        /// <summary>
        /// True when the number shown for <paramref name="remainingSeconds"/> —
        /// its <see cref="WholeSeconds"/> — is at most
        /// <paramref name="warningSeconds"/>. Deciding on the displayed second,
        /// not the raw time, keeps a number from changing colour while it stays
        /// on screen.
        /// </summary>
        public static bool IsWarning(float remainingSeconds, int warningSeconds) =>
            WholeSeconds(remainingSeconds) <= warningSeconds;
    }
}
