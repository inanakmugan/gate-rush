using System;
using System.Globalization;

namespace GateRush.UI
{
    /// <summary>
    /// The Profile overlay's texts (Module 38): formatting only, every text
    /// from <see cref="UiConfig"/>, read with the invariant culture. The
    /// numbers come from <see cref="Runtime.FrontEndState"/>, which reads them
    /// from the save as it is.
    /// </summary>
    public static class ProfileTexts
    {
        /// <summary>A numeric statistic — levels completed, best streak, lifetime gold — through <see cref="UiConfig.StatisticFormat"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string Statistic(int value, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.StatisticFormat, value);
        }

        /// <summary>
        /// The campaign level as Home's play button shows it: "Level N" for
        /// <paramref name="campaignLevelNumber"/>, or the all-played label when
        /// there is none (<see cref="HomeTexts.PlayLabel"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string CampaignLevel(int? campaignLevelNumber, UiConfig config) =>
            HomeTexts.PlayLabel(campaignLevelNumber, config);

        /// <summary>The line under the name field after a refused name.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string NameRefused(UiConfig config)
        {
            Require(config);
            return config.NameRefusedText;
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
