using System;
using System.Collections.Generic;

namespace GateRush.Meta
{
    /// <summary>
    /// What <see cref="MetaGame.ReportCampaignWin"/> changed: the gold paid,
    /// the streak reached and the streak rewards given. A win always changes
    /// the save, so the caller saves after every one.
    /// </summary>
    public sealed class CampaignWin
    {
        internal CampaignWin(int goldEarned, int streak, IReadOnlyList<StreakReward> rewardsGiven)
        {
            GoldEarned = goldEarned;
            Streak = streak;
            RewardsGiven = rewardsGiven ?? throw new ArgumentNullException(nameof(rewardsGiven));
        }

        /// <summary>The level's gold reward, paid into the balance and the lifetime gold.</summary>
        public int GoldEarned { get; }

        /// <summary>The win streak after this win.</summary>
        public int Streak { get; }

        /// <summary>
        /// The rewards whose <see cref="StreakReward.Wins"/> equals
        /// <see cref="Streak"/>, already added to the jokers. Empty, never
        /// null, when the win reached no threshold.
        /// </summary>
        public IReadOnlyList<StreakReward> RewardsGiven { get; }
    }
}
