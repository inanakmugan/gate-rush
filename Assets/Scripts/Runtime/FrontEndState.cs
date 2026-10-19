using System;
using System.Collections.Generic;
using GateRush.Meta;

namespace GateRush.Runtime
{
    /// <summary>
    /// Everything the front end shows (Module 35): plain numbers read once
    /// from the save and the rules by <see cref="Of"/>, so the screens hold
    /// no rule and every number on them comes from here.
    /// </summary>
    /// <remarks>
    /// Whether the refill is affordable is decided here, not on a screen
    /// (ARCHITECTURE, UI). The wait to the next life is exact to the tick;
    /// rounding it for display is the screen's.
    /// </remarks>
    public readonly struct FrontEndState
    {
        private FrontEndState(
            int gold,
            int lives,
            int lifeCap,
            TimeSpan? untilNextLife,
            int? campaignLevelNumber,
            int refillPrice,
            bool canRefill,
            bool isSoundOn,
            IReadOnlyList<LevelEntry> levels)
        {
            Gold = gold;
            Lives = lives;
            LifeCap = lifeCap;
            UntilNextLife = untilNextLife;
            CampaignLevelNumber = campaignLevelNumber;
            RefillPrice = refillPrice;
            CanRefill = canRefill;
            IsSoundOn = isSoundOn;
            Levels = levels;
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

        /// <summary>True when the lives are not full and the balance covers <see cref="RefillPrice"/>.</summary>
        public bool CanRefill { get; }

        /// <summary>Whether sound and vibration are on.</summary>
        public bool IsSoundOn { get; }

        /// <summary>
        /// Every level of the level order, in order, with its number and its
        /// completed mark (D50, D58); empty with no usable level order. Never
        /// null for a state built by <see cref="Of"/>.
        /// </summary>
        public IReadOnlyList<LevelEntry> Levels { get; }

        /// <summary>
        /// The front end's state at <paramref name="nowUtc"/>: the gold and
        /// the lives of <paramref name="meta"/>'s save, the cap and the refill
        /// price of <paramref name="rules"/> — the rules the save was loaded
        /// with — the campaign level's number and the level list from
        /// <paramref name="catalog"/>, which may be null when the level order
        /// is unusable, and <paramref name="isSoundOn"/>. Reading the lives
        /// settles them, which needs no save (<see cref="MetaGame"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="meta"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public static FrontEndState Of(MetaGame meta, MetaRules rules, DateTime nowUtc, LevelCatalog catalog, bool isSoundOn)
        {
            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var save = meta.Save;
            var lives = save.Lives.CountAt(nowUtc);
            var isFull = save.Lives.IsFullAt(nowUtc);
            var gold = save.Wallet.Balance;
            var price = rules.LivesRefillPrice;

            // The position is an index into the level order; the player counts from 1.
            int? campaignNumber = CampaignLevels.TryGetLevel(catalog, meta.CampaignPosition, out _)
                ? meta.CampaignPosition + 1
                : (int?)null;

            return new FrontEndState(
                gold,
                lives,
                rules.LifeCap,
                save.Lives.UntilNextLifeAt(nowUtc),
                campaignNumber,
                price,
                !isFull && gold >= price,
                isSoundOn,
                LevelsOf(catalog, save.Completed));
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
