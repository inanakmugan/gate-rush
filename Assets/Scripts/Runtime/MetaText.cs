using System;
using System.Globalization;

namespace GateRush.Runtime
{
    /// <summary>
    /// The meta game's texts on the result panel (Module 32, D64):
    /// formatting only, every text from <see cref="RuntimeConfig"/>'s format
    /// strings. The front end's and the level HUD's texts are the
    /// <c>GateRush.UI</c> assembly's (Modules 35 and 40). The numbers are the
    /// caller's, read from <c>MetaGame</c> and <c>MetaRules</c>; nothing here
    /// decides a rule.
    /// </summary>
    public static class MetaText
    {
        /// <summary>The gold a campaign win paid, for the line under the result panel's title.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="texts"/> is null.</exception>
        public static string GoldEarned(int gold, RuntimeConfig texts)
        {
            if (texts == null)
            {
                throw new ArgumentNullException(nameof(texts));
            }

            return string.Format(CultureInfo.InvariantCulture, texts.GoldEarnedFormat, gold);
        }

        /// <summary>
        /// The whole seconds a wait reads as, rounded up as the countdown is
        /// (<see cref="TimeFormat.WholeSeconds"/>): what a refresh compares, so
        /// the text is rebuilt exactly when it would change.
        /// </summary>
        public static int WholeSeconds(TimeSpan wait) => TimeFormat.WholeSeconds((float)wait.TotalSeconds);
    }
}
