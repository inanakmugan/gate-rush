using System;
using System.Collections.Generic;
using GateRush.Platform;

namespace GateRush.Meta
{
    /// <summary>
    /// The one way <c>Runtime</c> changes the meta game (Module 31, D63): it
    /// reports campaign wins and losses here, buys here, and spends jokers
    /// here. One object keeps the rules of D57 (a loss costs one life,
    /// starting costs none), D58 (only the campaign counts), D61 (the streak
    /// and its rewards), D62 (the store), D65 (spending a joker), D72 (gold
    /// bought for real money) and D76 (jokers unlocked by the campaign)
    /// together.
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

        // D76 gives one of a joker when the campaign reaches its level; a
        // rule, not a tunable.
        private const int JokersPerUnlock = 1;

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
        /// True when <paramref name="kind"/> is unlocked at the campaign
        /// position (D76, <see cref="MetaRules.IsJokerUnlocked"/>). Only the
        /// campaign asks; free play never locks a joker (D66).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public bool IsJokerUnlocked(JokerKind kind) => rules.IsJokerUnlocked(kind, CampaignPosition);

        /// <summary>
        /// The jokers whose unlock level is the campaign level the position
        /// names, in kind order: those the New Joker card introduces each time
        /// that level loads in the campaign (D76). Empty when it unlocks none.
        /// </summary>
        public IReadOnlyList<JokerKind> JokersUnlockedAtCampaignLevel() => rules.JokersUnlockingAt(CampaignPosition);

        /// <summary>
        /// Reports a campaign win, in this order: pays
        /// <paramref name="goldReward"/> into the balance and the lifetime
        /// gold (D22, D58); adds one to the streak, raising the best; gives
        /// the jokers of every streak reward whose wins equal the new streak
        /// (D61), locked or not; advances the campaign by one; and gives one
        /// of each joker whose unlock level the new position is (D76), listed
        /// in <see cref="CampaignWin.JokersUnlocked"/>. It spends no life
        /// (D57). Always changes the save.
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

            var positionBefore = Save.Campaign.Position;
            Save.Campaign.Advance();

            // A position held at int.MaxValue did not move, so it reached no
            // new level and unlocks nothing.
            var unlocked = Save.Campaign.Position == positionBefore
                ? (IReadOnlyList<JokerKind>)Array.Empty<JokerKind>()
                : rules.JokersUnlockingAt(Save.Campaign.Position);
            for (var i = 0; i < unlocked.Count; i++)
            {
                Save.Jokers.Add(unlocked[i], JokersPerUnlock);
            }

            return new CampaignWin(goldReward, streak, given, unlocked);
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
        /// <see cref="PurchaseResult.Locked"/> with nothing changed while it
        /// is locked (D76); otherwise <see cref="PurchaseResult.Bought"/> when
        /// the balance covers it, or <see cref="PurchaseResult.NotEnoughGold"/>
        /// with nothing changed. The lifetime gold never changes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public PurchaseResult TryBuyJoker(JokerKind kind)
        {
            if (!IsJokerUnlocked(kind))
            {
                return PurchaseResult.Locked;
            }

            if (!Save.Wallet.TrySpend(rules.JokerPrice(kind)))
            {
                return PurchaseResult.NotEnoughGold;
            }

            Save.Jokers.Add(kind, JokersPerPurchase);
            return PurchaseResult.Bought;
        }

        /// <summary>
        /// Reports a gold pack the purchase service sold (D72): adds the gold
        /// of the pack in the rules with <paramref name="productId"/> to the
        /// balance only, never to the lifetime gold (D22), and returns it.
        /// The streak, the lives, the jokers and the campaign never change.
        /// Always changes the save; the caller saves.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="productId"/> is null.</exception>
        /// <exception cref="ArgumentException">No gold pack has <paramref name="productId"/>; nothing changes.</exception>
        public int ReportGoldPurchase(string productId)
        {
            if (productId == null)
            {
                throw new ArgumentNullException(nameof(productId));
            }

            var packs = rules.GoldPacks;
            for (var i = 0; i < packs.Count; i++)
            {
                if (string.Equals(packs[i].ProductId, productId, StringComparison.Ordinal))
                {
                    Save.Wallet.AddPurchased(packs[i].Gold);
                    return packs[i].Gold;
                }
            }

            throw new ArgumentException($"No gold pack has the product id '{productId}'.", nameof(productId));
        }

        /// <summary>
        /// Spends one <paramref name="kind"/> on the board (D65): while it is
        /// locked (D76), <see cref="JokerUse.Locked"/> with nothing changed,
        /// even when some are held; otherwise one from the
        /// inventory when it holds one, <see cref="JokerUse.Used"/>; otherwise
        /// its store price when the balance covers it,
        /// <see cref="JokerUse.BoughtAndUsed"/>, bought and used in one step so
        /// it never enters the inventory; otherwise
        /// <see cref="JokerUse.NotEnoughGold"/> with nothing changed. The
        /// lifetime gold, the streak, the campaign position and the lives
        /// never change.
        /// <para>It does not check the target: the caller asks whether the use
        /// is valid first and applies the joker only after
        /// <see cref="JokerUse.Used"/> or <see cref="JokerUse.BoughtAndUsed"/>,
        /// then saves.</para>
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind; nothing changes.</exception>
        public JokerUse TryUseJoker(JokerKind kind)
        {
            if (!IsJokerUnlocked(kind))
            {
                return JokerUse.Locked;
            }

            if (Save.Jokers.TryUse(kind))
            {
                return JokerUse.Used;
            }

            if (!Save.Wallet.TrySpend(rules.JokerPrice(kind)))
            {
                return JokerUse.NotEnoughGold;
            }

            return JokerUse.BoughtAndUsed;
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
