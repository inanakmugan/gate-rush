using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Platform;

namespace GateRush.Runtime
{
    /// <summary>
    /// Everything the front end shows (Modules 35 and 36): plain numbers read
    /// once from the save, the rules and the purchase service's products by
    /// <see cref="Of"/>, so the screens hold no rule and every number on them
    /// comes from here.
    /// </summary>
    /// <remarks>
    /// Which gold packs are offered is decided here (<see cref="Runtime.GoldOffers"/>),
    /// not on a screen (ARCHITECTURE, UI). Whether a purchase is affordable is
    /// not shown: every purchase can be pressed, and a refusal opens the Not
    /// Enough Gold prompt (D72). The wait to the next life is exact to the
    /// tick; rounding it for display is the screen's.
    /// </remarks>
    public readonly struct FrontEndState
    {
        private readonly JokerAmounts jokerCounts;
        private readonly JokerAmounts jokerPrices;

        private FrontEndState(
            int gold,
            int lives,
            int lifeCap,
            TimeSpan? untilNextLife,
            int? campaignLevelNumber,
            int refillPrice,
            bool isLivesFull,
            bool isSoundOn,
            IReadOnlyList<LevelEntry> levels,
            JokerAmounts jokerCounts,
            JokerAmounts jokerPrices,
            IReadOnlyList<GoldOffer> goldOffers)
        {
            Gold = gold;
            Lives = lives;
            LifeCap = lifeCap;
            UntilNextLife = untilNextLife;
            CampaignLevelNumber = campaignLevelNumber;
            RefillPrice = refillPrice;
            IsLivesFull = isLivesFull;
            IsSoundOn = isSoundOn;
            Levels = levels;
            this.jokerCounts = jokerCounts;
            this.jokerPrices = jokerPrices;
            GoldOffers = goldOffers;
        }

        /// <summary>The gold balance.</summary>
        public int Gold { get; }

        /// <summary>The lives at the time the state was read.</summary>
        public int Lives { get; }

        /// <summary>The most lives a player holds.</summary>
        public int LifeCap { get; }

        /// <summary>How long until the next life comes back; null when the lives are full.</summary>
        public TimeSpan? UntilNextLife { get; }

        /// <summary>
        /// The campaign level's 1-based number in the level order; null past
        /// the last level, and with no usable level order.
        /// </summary>
        public int? CampaignLevelNumber { get; }

        /// <summary>What refilling the lives costs (D62).</summary>
        public int RefillPrice { get; }

        /// <summary>
        /// True when the lives are at the cap: the refill cannot be pressed
        /// then. Below the cap it can, whatever the balance (Module 36).
        /// </summary>
        public bool IsLivesFull { get; }

        /// <summary>Whether sound and vibration are on.</summary>
        public bool IsSoundOn { get; }

        /// <summary>
        /// Every level of the level order, in order, with its number and its
        /// completed mark (D50, D58); empty with no usable level order. Never
        /// null for a state built by <see cref="Of"/>.
        /// </summary>
        public IReadOnlyList<LevelEntry> Levels { get; }

        /// <summary>
        /// The gold packs the Store shows (D72): only those the rules and the
        /// purchase service both know, in the rules' order, with the service's
        /// price text. Never null for a state built by <see cref="Of"/>.
        /// </summary>
        public IReadOnlyList<GoldOffer> GoldOffers { get; }

        /// <summary>How many of <paramref name="kind"/> the campaign's inventory holds.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public int JokerCount(JokerKind kind) => jokerCounts.Of(kind);

        /// <summary>What one <paramref name="kind"/> costs in the store (D62).</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public int JokerPrice(JokerKind kind) => jokerPrices.Of(kind);

        /// <summary>
        /// The front end's state at <paramref name="nowUtc"/>: the gold, the
        /// lives and the jokers of <paramref name="meta"/>'s save; the cap, the
        /// prices and the gold packs of <paramref name="rules"/> — the rules the
        /// save was loaded with; the campaign level's number and the level
        /// list from <paramref name="catalog"/>, which may be null when the
        /// level order is unusable; <paramref name="isSoundOn"/>; and the gold
        /// offers, priced from <paramref name="products"/>, the purchase
        /// service's. Reading the lives settles them, which needs no save
        /// (<see cref="MetaGame"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="meta"/>, <paramref name="rules"/> or <paramref name="products"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public static FrontEndState Of(
            MetaGame meta, MetaRules rules, DateTime nowUtc, LevelCatalog catalog, bool isSoundOn, IReadOnlyList<StoreProduct> products)
        {
            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (products == null)
            {
                throw new ArgumentNullException(nameof(products));
            }

            var save = meta.Save;
            var lives = save.Lives.CountAt(nowUtc);
            var isFull = save.Lives.IsFullAt(nowUtc);

            // The position is an index into the level order; the player counts from 1.
            int? campaignNumber = CampaignLevels.TryGetLevel(catalog, meta.CampaignPosition, out _)
                ? meta.CampaignPosition + 1
                : (int?)null;

            var jokers = save.Jokers;
            return new FrontEndState(
                save.Wallet.Balance,
                lives,
                rules.LifeCap,
                save.Lives.UntilNextLifeAt(nowUtc),
                campaignNumber,
                rules.LivesRefillPrice,
                isFull,
                isSoundOn,
                LevelsOf(catalog, save.Completed),
                new JokerAmounts(jokers.CountOf(JokerKind.Clock), jokers.CountOf(JokerKind.Rocket), jokers.CountOf(JokerKind.Broom)),
                new JokerAmounts(rules.JokerPrice(JokerKind.Clock), rules.JokerPrice(JokerKind.Rocket), rules.JokerPrice(JokerKind.Broom)),
                Runtime.GoldOffers.Match(rules.GoldPacks, products));
        }

        /// <summary>
        /// Every level of <paramref name="catalog"/>, in its order, with the
        /// number <see cref="LevelCatalog.TryGetNumber"/> gives it and whether
        /// it is in <paramref name="completed"/>; empty for no catalog.
        /// </summary>
        private static IReadOnlyList<LevelEntry> LevelsOf(LevelCatalog catalog, CompletedLevels completed)
        {
            if (catalog == null)
            {
                return Array.Empty<LevelEntry>();
            }

            // The name at index i is the level numbered i + 1 (LevelCatalog.Names).
            var names = catalog.Names;
            var ids = catalog.Ids;
            var entries = new LevelEntry[names.Count];
            for (var i = 0; i < entries.Length; i++)
            {
                entries[i] = new LevelEntry(names[i], i + 1, completed.IsCompleted(ids[i]));
            }

            return Array.AsReadOnly(entries);
        }
    }
}
