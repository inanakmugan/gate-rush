using System;
using System.Globalization;
using GateRush.Meta;

namespace GateRush.UI
{
    /// <summary>
    /// The win streak's texts (Module 37): formatting only, every text from
    /// <see cref="UiConfig"/>'s formats, labels and joker names, read with the
    /// invariant culture. What the streak has reached comes from
    /// <see cref="StreakProgress"/>; nothing here decides a rule.
    /// </summary>
    public static class StreakTexts
    {
        /// <summary>The streak beside the fire on Home's streak bar.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string StreakCount(int streak, UiConfig config)
        {
            Require(config);
            return Format(config.StreakCountFormat, streak);
        }

        /// <summary>
        /// The line under the streak bar: at a streak of 0, the first reward's
        /// wins and the reward (<see cref="UiConfig.FirstRewardFormat"/>);
        /// otherwise the wins left to the next reward, with
        /// <see cref="UiConfig.OneMoreWinFormat"/> when one is left;
        /// <see cref="UiConfig.AllRewardsCollectedLabel"/> past the last
        /// reward; and empty for an empty table, which has nothing to collect.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="progress"/> or <paramref name="config"/> is null.</exception>
        public static string BarLine(StreakProgress progress, UiConfig config)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            Require(config);
            if (!progress.NextReward.HasValue)
            {
                return progress.States.Count == 0 ? string.Empty : config.AllRewardsCollectedLabel;
            }

            var next = progress.NextReward.Value;
            var name = RewardName(next, config);
            if (progress.Streak == 0)
            {
                return Format(config.FirstRewardFormat, next.Wins, name);
            }

            return progress.WinsToNext == 1
                ? Format(config.OneMoreWinFormat, name)
                : Format(config.MoreWinsFormat, progress.WinsToNext, name);
        }

        /// <summary>
        /// A reward as the bar's line names it: the joker's name alone for a
        /// count of 1, otherwise with its count (<see cref="LadderReward"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string RewardName(StreakReward reward, UiConfig config)
        {
            Require(config);
            return reward.Count == 1 ? config.JokerName(reward.Joker) : LadderReward(reward, config);
        }

        /// <summary>A marker's count on the streak bar: empty for a count of 1, which needs no number.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string MarkerCount(int count, UiConfig config)
        {
            Require(config);
            return count == 1 ? string.Empty : Format(config.MarkerCountFormat, count);
        }

        /// <summary>The Win Streak overlay's current streak.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string CurrentStreak(int streak, UiConfig config)
        {
            Require(config);
            return Format(config.CurrentStreakFormat, streak);
        }

        /// <summary>The Win Streak overlay's best streak.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string BestStreak(int best, UiConfig config)
        {
            Require(config);
            return Format(config.BestStreakFormat, best);
        }

        /// <summary>A ladder row's wins.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string LadderWins(int wins, UiConfig config)
        {
            Require(config);
            return Format(config.LadderWinsFormat, wins);
        }

        /// <summary>A reward with its count, always shown: "Clock x1" by default.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string LadderReward(StreakReward reward, UiConfig config)
        {
            Require(config);
            return Format(config.LadderRewardFormat, config.JokerName(reward.Joker), reward.Count);
        }

        /// <summary>A ladder row's state: Collected, Next, or empty for a reward ahead.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not a declared state.</exception>
        public static string LadderState(StreakRewardState state, UiConfig config)
        {
            Require(config);
            switch (state)
            {
                case StreakRewardState.Collected:
                    return config.CollectedLabel;
                case StreakRewardState.Next:
                    return config.NextLabel;
                case StreakRewardState.Ahead:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Not a streak reward state.");
            }
        }

        /// <summary>The Streak Reward overlay's headline, with the streak of the latest rewarded win.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string RewardHeadline(int latestStreak, UiConfig config)
        {
            Require(config);
            return Format(config.RewardHeadlineFormat, latestStreak);
        }

        /// <summary>A line of the Streak Reward overlay: "+1 Rocket" by default.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string Prize(JokerPrize prize, UiConfig config)
        {
            Require(config);
            return Format(config.PrizeFormat, prize.Count, config.JokerName(prize.Joker));
        }

        private static string Format(string format, params object[] arguments) =>
            string.Format(CultureInfo.InvariantCulture, format, arguments);

        private static void Require(UiConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
        }
    }
}
