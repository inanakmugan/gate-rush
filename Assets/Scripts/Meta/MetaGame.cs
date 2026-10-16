using System;
using System.Collections.Generic;
using GateRush.Platform;

namespace GateRush.Meta
{
    /// <summary>
    /// The one way <c>Runtime</c> changes the meta game (Module 31, D63): it
    /// reports campaign wins and losses here, and buys here. One object keeps
    /// the rules of D57 (a loss costs one life, starting costs none), D58
    /// (only the campaign counts), D61 (the streak and its rewards) and D62
    /// (the store) together.
    /// </summary>
    /// <remarks>
    /// <para><b>It never saves.</b> It changes the <see cref="SaveData"/> it
    /// wraps in place; every call that can change it says so in its result,
    /// and the caller saves the whole save (D56).</para>
    /// <para><b>Time comes from <see cref="ITimeProvider"/></b>, read at most
    /// once per call and passed to <see cref="Lives"/>. Nothing here reads the
    /// system clock.</para>
    /// <para><b>Free play reports nothing</b> (D58), so there is no member
    /// for it.</para>
    /// <para>The rules given must be the ones the save was loaded with: the
    /// lives keep their own, and the prices and the streak table are read
    /// from these.</para>
    /// </remarks>
    public sealed class MetaGame
    {
        // D62 sells jokers one at a time; a rule, not a tunable.
        private const int JokersPerPurchase = 1;

        private readonly MetaRules rules;
        private readonly ITimeProvider time;

        /// <summary>The meta game of <paramref name="save"/>, by <paramref name="rules"/>, at the time <paramref name="time"/> reads.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public MetaGame(SaveData save, MetaRules rules, ITimeProvider time)
        {
            Save = save ?? throw new ArgumentNullException(nameof(save));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.time = time ?? throw new ArgumentNullException(nameof(time));
        }

        /// <summary>The save this object changes, for the caller to save whole.</summary>
        public SaveData Save { get; }

        /// <summary>The index of the campaign level in the level order. Which level that is, if any, is the catalog's question.</summary>
        public int CampaignPosition => Save.Campaign.Position;

        /// <summary>
        /// True when a campaign level may start: there is at least one life
        /// now. Starting spends nothing (D57). Settles the lives, which the
        /// caller need not save.
        /// </summary>
        public bool CanStartCampaignLevel() => Save.Lives.CountAt(time.UtcNow) > 0;

        /// <summary>
        /// Reports a campaign win, in this order: pays
        /// <paramref name="goldReward"/> into the balance and the lifetime
        /// gold (D22, D58); adds one to the streak, raising the best; gives
        /// the jokers of every streak reward whose wins equal the new streak
        /// (D61); and advances the campaign by one. It spends no life (D57).
        /// Always changes the save.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="goldReward"/> is negative; nothing changes.</exception>
        public CampaignWin ReportCampaignWin(int goldReward)
        {
            if (goldReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goldReward), goldReward, "A gold reward may not be negative.");
            }

            Save.Wallet.Earn(goldReward);
            var streak = Save.Streak.RecordWin();

            // Wins are strictly ascending in valid rules, so at most one
            // reward matches; the list keeps the result honest regardless.
            var given = new List<StreakReward>();
            var rewards = rules.StreakRewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward.Wins != streak)
                {
                    continue;
                }

                Save.Jokers.Add(reward.Joker, reward.Count);
                given.Add(reward);
            }

            Save.Campaign.Advance();
            return new CampaignWin(goldReward, streak, given);
        }

        /// <summary>
        /// Reports a campaign loss — the countdown running out, or leaving or
        /// restarting while playing (D57): loses one life now and sets the
        /// streak to 0, keeping the best. The campaign position stays. True
        /// when a life was lost; false when none was left, and the streak is
        /// still reset. Either way the save may have changed, so the caller
        /// saves after every loss.
        /// </summary>
        public bool ReportCampaignLoss()
        {
            var isLost = Save.Lives.TryLose(time.UtcNow);
            Save.Streak.Reset();
            return isLost;
        }

        /// <summary>
        /// Buys one <paramref name="kind"/> at its price (D62):
        /// <see cref="PurchaseResult.Bought"/> when the balance covers it,
        /// otherwise <see cref="PurchaseResult.NotEnoughGold"/> with nothing
        /// changed. The lifetime gold never changes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public PurchaseResult TryBuyJoker(JokerKind kind)
        {
            if (!Save.Wallet.TrySpend(rules.JokerPrice(kind)))
            {
                return PurchaseResult.NotEnoughGold;
            }

            Save.Jokers.Add(kind, JokersPerPurchase);
            return PurchaseResult.Bought;
        }

        /// <summary>
        /// Refills the lives to the cap at the refill price (D62): full lives
        /// give <see cref="PurchaseResult.AlreadyFull"/> and cost nothing;
        /// otherwise <see cref="PurchaseResult.Bought"/> when the balance
        /// covers the price, or <see cref="PurchaseResult.NotEnoughGold"/>
        /// with nothing spent. The lifetime gold never changes.
        /// </summary>
        public PurchaseResult TryRefillLives()
        {
            var nowUtc = time.UtcNow;
            if (Save.Lives.IsFullAt(nowUtc))
            {
                return PurchaseResult.AlreadyFull;
            }

            if (!Save.Wallet.TrySpend(rules.LivesRefillPrice))
            {
                return PurchaseResult.NotEnoughGold;
            }

            Save.Lives.Refill(nowUtc);
            return PurchaseResult.Bought;
        }
    }
}
