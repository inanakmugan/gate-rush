using System;
using System.Collections.Generic;

namespace GateRush.Meta
{
    /// <summary>
    /// What <see cref="MetaGame.ReportCampaignWin"/> changed: the gold paid,
    /// the streak reached, the streak rewards given and the jokers the new
    /// campaign position unlocked. A win always changes the save, so the
    /// caller saves after every one.
    /// </summary>
    public sealed class CampaignWin
    {
        internal CampaignWin(
            int goldEarned, int streak, IReadOnlyList<StreakReward> rewardsGiven, IReadOnlyList<JokerKind> jokersUnlocked)
        {
            GoldEarned = goldEarned;
            Streak = streak;
            RewardsGiven = rewardsGiven ?? throw new ArgumentNullException(nameof(rewardsGiven));
            JokersUnlocked = jokersUnlocked ?? throw new ArgumentNullException(nameof(jokersUnlocked));
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

        /// <summary>
        /// The jokers whose unlock level this win moved the campaign onto
        /// (D76), in kind order, one of each already added to the jokers.
        /// Empty, never null, when the new level unlocks none.
        /// </summary>
        public IReadOnlyList<JokerKind> JokersUnlocked { get; }
    }
}
