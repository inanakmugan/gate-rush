using System;
using System.Globalization;
using GateRush.Runtime;

namespace GateRush.UI
{
    /// <summary>
    /// The level panels' texts (Module 41): the result panel's title and gold
    /// line, and the confirmation's title and line. Formatting only, every
    /// text from <see cref="UiConfig"/>, read with the invariant culture; the
    /// outcome, the gold and the streak are the bootstrap's.
    /// </summary>
    public static class LevelPanelTexts
    {
        /// <summary>The result panel's title: <see cref="UiConfig.LevelCompleteTitle"/> after a win, <see cref="UiConfig.TimesUpTitle"/> after a loss.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string ResultTitle(LevelResult result, UiConfig config)
        {
            Require(config);
            return result.IsWin ? config.LevelCompleteTitle : config.TimesUpTitle;
        }

        /// <summary>The gold a campaign win paid, beside the coin, through <see cref="UiConfig.GoldEarnedFormat"/>: "+50" by default.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string GoldEarned(int gold, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.GoldEarnedFormat, gold);
        }

        /// <summary>The confirmation's title for <paramref name="kind"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public static string ConfirmTitle(ConfirmKind kind, UiConfig config)
        {
            Require(config);
            switch (kind)
            {
                case ConfirmKind.Leave:
                    return config.LeaveLevelTitle;
                case ConfirmKind.Restart:
                    return config.RestartLevelTitle;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a confirmation kind.");
            }
        }

        /// <summary>
        /// The confirmation's line: <see cref="UiConfig.LoseLifeLine"/> with no
        /// win streak at risk, a streak of 0 or less; otherwise the streak
        /// through <see cref="UiConfig.LoseLifeAndStreakFormat"/> (D74).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string ConfirmLine(int streakAtRisk, UiConfig config)
        {
            Require(config);
            return streakAtRisk > 0
                ? string.Format(CultureInfo.InvariantCulture, config.LoseLifeAndStreakFormat, streakAtRisk)
                : config.LoseLifeLine;
        }

        private static void Require(UiConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
        }
    }
}
