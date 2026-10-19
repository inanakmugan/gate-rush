using System;
using System.Globalization;
using GateRush.Runtime;

namespace GateRush.UI
{
    /// <summary>
    /// The front end's texts (Module 35): formatting only, every text from
    /// <see cref="UiConfig"/>'s formats and labels, read with the invariant
    /// culture. The numbers come from <see cref="FrontEndState"/>; nothing here
    /// decides a rule.
    /// </summary>
    /// <remarks>
    /// A wait reads as minutes and seconds rounded up to a whole second
    /// (<see cref="MetaText.WholeSeconds"/>), the countdown's rounding, which
    /// is also the second the bootstrap's refresh compares — so the text
    /// changes exactly when it is refreshed.
    /// </remarks>
    public static class HomeTexts
    {
        /// <summary>
        /// Home's play button: the campaign level's number through
        /// <see cref="UiConfig.PlayLevelFormat"/>, or
        /// <see cref="UiConfig.AllPlayedLabel"/> when there is none.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string PlayLabel(int? campaignLevelNumber, UiConfig config)
        {
            Require(config);
            return campaignLevelNumber.HasValue
                ? string.Format(CultureInfo.InvariantCulture, config.PlayLevelFormat, campaignLevelNumber.Value)
                : config.AllPlayedLabel;
        }

        /// <summary>The gold balance.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string Gold(int gold, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.GoldFormat, gold);
        }

        /// <summary>
        /// Beside the lives: <see cref="UiConfig.LivesFullLabel"/> when
        /// <paramref name="untilNext"/> is null — the lives are full — and
        /// otherwise the wait through <see cref="UiConfig.WaitFormat"/>, as
        /// minutes and seconds rounded up.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string LivesStatus(TimeSpan? untilNext, UiConfig config)
        {
            Require(config);
            if (!untilNext.HasValue)
            {
                return config.LivesFullLabel;
            }

            var wait = TimeSpan.FromSeconds(MetaText.WholeSeconds(untilNext.Value));
            return string.Format(CultureInfo.InvariantCulture, config.WaitFormat, (int)wait.TotalMinutes, wait.Seconds);
        }

        /// <summary>The count inside the top bar's heart.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string LivesCount(int lives, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.LivesCountFormat, lives);
        }

        /// <summary>The Lives overlay's count: <paramref name="lives"/> of <paramref name="cap"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string LivesFraction(int lives, int cap, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.LivesFractionFormat, lives, cap);
        }

        /// <summary>The refill button's label, with <paramref name="price"/> from the rules.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string Refill(int price, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.RefillFormat, price);
        }

        /// <summary>A level button's number.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string LevelNumber(int number, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.LevelNumberFormat, number);
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
