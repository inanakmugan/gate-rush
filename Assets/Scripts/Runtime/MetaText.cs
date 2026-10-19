using System;
using System.Globalization;

namespace GateRush.Runtime
{
    /// <summary>
    /// The meta game's texts on the HUD and the result panel (Module 32,
    /// D64): formatting only, every text from <see cref="RuntimeConfig"/>'s
    /// format strings. The front end's texts are <c>HomeTexts</c>' (Module 35). The numbers are the caller's, read from <c>MetaGame</c> and
    /// <c>MetaRules</c>; nothing here decides a rule.
    /// </summary>
    /// <remarks>
    /// A wait reads as minutes and seconds rounded up to a whole second
    /// (<see cref="TimeFormat.MinutesSeconds"/>), the countdown's rounding, so
    /// a caller that refreshes when <see cref="TimeFormat.WholeSeconds"/>
    /// changes rebuilds the text exactly when it would read differently.
    /// </remarks>
    public static class MetaText
    {
        /// <summary>
        /// The lives: <paramref name="count"/> and <paramref name="cap"/>, and
        /// the time to the next life when <paramref name="untilNext"/> has one
        /// — null when the lives are full.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="texts"/> is null.</exception>
        public static string Lives(int count, int cap, TimeSpan? untilNext, RuntimeConfig texts)
        {
            Require(texts);
            return untilNext.HasValue
                ? string.Format(CultureInfo.InvariantCulture, texts.LivesWaitingFormat, count, cap, Wait(untilNext.Value))
                : string.Format(CultureInfo.InvariantCulture, texts.LivesFullFormat, count, cap);
        }

        /// <summary>The gold balance.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="texts"/> is null.</exception>
        public static string Gold(int balance, RuntimeConfig texts)
        {
            Require(texts);
            return string.Format(CultureInfo.InvariantCulture, texts.GoldFormat, balance);
        }

        /// <summary>Two parts of a meta line, <paramref name="first"/> then <paramref name="second"/>, with the separator between them.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="texts"/> is null.</exception>
        public static string Joined(string first, string second, RuntimeConfig texts)
        {
            Require(texts);
            return first + texts.MetaSeparator + second;
        }

        /// <summary>The gold a campaign win paid, for the line under the result panel's title.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="texts"/> is null.</exception>
        public static string GoldEarned(int gold, RuntimeConfig texts)
        {
            Require(texts);
            return string.Format(CultureInfo.InvariantCulture, texts.GoldEarnedFormat, gold);
        }

        /// <summary>
        /// The whole seconds a wait reads as: what a refresh compares, so the
        /// text is rebuilt exactly when it would change.
        /// </summary>
        public static int WholeSeconds(TimeSpan wait) => TimeFormat.WholeSeconds((float)wait.TotalSeconds);

        private static string Wait(TimeSpan wait) => TimeFormat.MinutesSeconds((float)wait.TotalSeconds);

        private static void Require(RuntimeConfig texts)
        {
            if (texts == null)
            {
                throw new ArgumentNullException(nameof(texts));
            }
        }
    }
}
