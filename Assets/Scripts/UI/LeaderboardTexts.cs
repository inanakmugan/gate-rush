using System;
using System.Globalization;

namespace GateRush.UI
{
    /// <summary>
    /// The Leaderboard's texts (Module 39): formatting only, every text from
    /// <see cref="UiConfig"/>'s formats, read with the invariant culture. The
    /// ranks and scores come from the leaderboard service's result.
    /// </summary>
    public static class LeaderboardTexts
    {
        /// <summary>A row's score beside the coin, through <see cref="UiConfig.LeaderboardScoreFormat"/>: "1,250" by default.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string Score(int score, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.LeaderboardScoreFormat, score);
        }

        /// <summary>A row's rank, through <see cref="UiConfig.LeaderboardRankFormat"/>: "7" by default.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string Rank(int rank, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.LeaderboardRankFormat, rank);
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
