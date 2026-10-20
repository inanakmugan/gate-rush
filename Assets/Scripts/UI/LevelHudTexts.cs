using System;
using System.Globalization;
using GateRush.Runtime;

namespace GateRush.UI
{
    /// <summary>
    /// The level HUD's and the joker bar's texts (Module 40): formatting only,
    /// every text from <see cref="UiConfig"/>'s formats and labels, read with
    /// the invariant culture. The numbers come from <see cref="LevelUiState"/>
    /// and the countdown; nothing here decides a rule. The gold and the lives
    /// read as the top bar's do (<see cref="HomeTexts"/>).
    /// </summary>
    public static class LevelHudTexts
    {
        private const int SecondsPerMinute = 60;

        /// <summary>
        /// The label at the HUD's right: <see cref="UiConfig.FreePlayLabel"/>
        /// in free play; the level's number through
        /// <see cref="UiConfig.LevelLabelFormat"/> in the campaign; empty for a
        /// campaign level with no number, which only a level outside the level
        /// order has.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string LevelLabel(bool isFreePlay, int? levelNumber, UiConfig config)
        {
            Require(config);
            if (isFreePlay)
            {
                return config.FreePlayLabel;
            }

            return levelNumber.HasValue
                ? string.Format(CultureInfo.InvariantCulture, config.LevelLabelFormat, levelNumber.Value)
                : string.Empty;
        }

        /// <summary>
        /// The timer: <paramref name="remainingSeconds"/> rounded up to a whole
        /// second, as the countdown is (<see cref="TimeFormat.WholeSeconds"/>),
        /// through <see cref="UiConfig.TimerFormat"/> as minutes and seconds.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string Timer(float remainingSeconds, UiConfig config)
        {
            Require(config);
            var whole = TimeFormat.WholeSeconds(remainingSeconds);
            return string.Format(
                CultureInfo.InvariantCulture, config.TimerFormat, whole / SecondsPerMinute, whole % SecondsPerMinute);
        }

        /// <summary>
        /// True once the displayed second is at most
        /// <see cref="UiConfig.TimerWarningSeconds"/>: the timer takes the
        /// warning colour (<see cref="TimeFormat.IsWarning"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static bool IsTimerWarning(float remainingSeconds, UiConfig config)
        {
            Require(config);
            return TimeFormat.IsWarning(remainingSeconds, config.TimerWarningSeconds);
        }

        /// <summary>The seconds added beside the timer, through <see cref="UiConfig.TimeBonusFormat"/>: "+20" by default.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string TimeBonus(int seconds, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.TimeBonusFormat, seconds);
        }

        /// <summary>
        /// A joker button's badge: the count through
        /// <see cref="UiConfig.JokerCountBadgeFormat"/>, or the price through
        /// <see cref="UiConfig.JokerPriceBadgeFormat"/>, as
        /// <paramref name="button"/>'s badge says.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string JokerBadge(JokerButton button, UiConfig config)
        {
            Require(config);
            var format = button.Badge == Runtime.JokerBadge.Price ? config.JokerPriceBadgeFormat : config.JokerCountBadgeFormat;
            return string.Format(CultureInfo.InvariantCulture, format, button.Amount);
        }

        /// <summary>
        /// Beside the lives: <see cref="UiConfig.LivesFullLabel"/> when
        /// <paramref name="nextLifeSeconds"/> is null — the lives are full —
        /// and otherwise the wait as the top bar shows it
        /// (<see cref="HomeTexts.LivesStatus"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string LivesStatus(int? nextLifeSeconds, UiConfig config)
        {
            Require(config);
            return HomeTexts.LivesStatus(
                nextLifeSeconds.HasValue ? TimeSpan.FromSeconds(nextLifeSeconds.Value) : (TimeSpan?)null, config);
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
