using System;
using System.Collections.Generic;

namespace GateRush.Meta
{
    /// <summary>
    /// The numbers the meta game runs on (Modules 30, 31 and 33): how many
    /// lives a player can hold and how long one takes to come back (D59), the
    /// gold and jokers a new player starts with (D59, D61), the store's prices
    /// (D62), the win streak's reward table (D61), the seconds a Clock adds
    /// (D65), the gold packs sold for real money (Module 36, D72) and the
    /// name and avatar a new player gets (Module 38, D71). Built by
    /// <c>Runtime</c> from
    /// its configuration asset; this assembly holds no default of its own, so
    /// every default — a new player's, and that of a section an older save
    /// lacks (D60) — comes from one instance of this class.
    /// </summary>
    /// <remarks>Immutable: the reward table and the gold packs are copied on construction.</remarks>
    public sealed class MetaRules
    {
        private readonly JokerAmounts jokerPrices;
        private readonly JokerAmounts startingJokers;

        /// <summary>Rules with the given values, every one of them checked.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="lifeCap"/> is below 1, <paramref name="lifeRegenInterval"/>
        /// is not positive, <paramref name="startingGold"/>, a joker price,
        /// <paramref name="livesRefillPrice"/> or a starting count is negative,
        /// or <paramref name="clockBonusSeconds"/> is below 1.
        /// </exception>
        /// <exception cref="ArgumentNullException"><paramref name="streakRewards"/> or <paramref name="goldPacks"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="streakRewards"/> has a problem <see cref="StreakReward.ProblemsOf"/> reports,
        /// <paramref name="goldPacks"/> one <see cref="GoldPack.ProblemsOf"/> reports,
        /// <paramref name="defaultPlayerName"/> is refused by <see cref="PlayerProfile.TryNormalizeName"/>, or
        /// <paramref name="defaultAvatarId"/> is null, empty or white space.
        /// </exception>
        public MetaRules(
            int lifeCap,
            TimeSpan lifeRegenInterval,
            int startingGold,
            JokerAmounts jokerPrices,
            int livesRefillPrice,
            JokerAmounts startingJokers,
            IReadOnlyList<StreakReward> streakRewards,
            int clockBonusSeconds,
            IReadOnlyList<GoldPack> goldPacks,
            string defaultPlayerName,
            string defaultAvatarId)
        {
            if (lifeCap < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(lifeCap), lifeCap, "The life cap must be at least 1.");
            }

            if (lifeRegenInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifeRegenInterval), lifeRegenInterval, "The life regeneration interval must be positive.");
            }

            if (startingGold < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingGold), startingGold, "The starting gold may not be negative.");
            }

            for (var kind = JokerKinds.First; kind <= JokerKinds.Last; kind++)
            {
                if (jokerPrices.Of(kind) < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(jokerPrices), jokerPrices.Of(kind), $"The {kind} price may not be negative.");
                }

                if (startingJokers.Of(kind) < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(startingJokers), startingJokers.Of(kind), $"The starting count of {kind} may not be negative.");
                }
            }

            if (livesRefillPrice < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(livesRefillPrice), livesRefillPrice, "The lives refill price may not be negative.");
            }

            if (streakRewards == null)
            {
                throw new ArgumentNullException(nameof(streakRewards));
            }

            var problems = StreakReward.ProblemsOf(streakRewards);
            if (problems.Count > 0)
            {
                throw new ArgumentException($"The streak reward table is invalid: {problems[0]}", nameof(streakRewards));
            }

            if (clockBonusSeconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(clockBonusSeconds), clockBonusSeconds, "The Clock's bonus seconds must be positive.");
            }

            if (goldPacks == null)
            {
                throw new ArgumentNullException(nameof(goldPacks));
            }

            var packProblems = GoldPack.ProblemsOf(goldPacks);
            if (packProblems.Count > 0)
            {
                throw new ArgumentException($"The gold packs are invalid: {packProblems[0]}", nameof(goldPacks));
            }

            if (!PlayerProfile.TryNormalizeName(defaultPlayerName, out var playerName))
            {
                throw new ArgumentException(
                    $"The default player name '{defaultPlayerName}' is refused by the name rule.", nameof(defaultPlayerName));
            }

            if (string.IsNullOrWhiteSpace(defaultAvatarId))
            {
                throw new ArgumentException("The default avatar id may not be empty.", nameof(defaultAvatarId));
            }

            var packs = new GoldPack[goldPacks.Count];
            for (var i = 0; i < packs.Length; i++)
            {
                packs[i] = goldPacks[i];
            }

            var rewards = new StreakReward[streakRewards.Count];
            for (var i = 0; i < rewards.Length; i++)
            {
                rewards[i] = streakRewards[i];
            }

            LifeCap = lifeCap;
            LifeRegenInterval = lifeRegenInterval;
            StartingGold = startingGold;
            this.jokerPrices = jokerPrices;
            LivesRefillPrice = livesRefillPrice;
            this.startingJokers = startingJokers;
            StreakRewards = Array.AsReadOnly(rewards);
            ClockBonusSeconds = clockBonusSeconds;
            GoldPacks = Array.AsReadOnly(packs);
            DefaultPlayerName = playerName;
            DefaultAvatarId = defaultAvatarId;
        }

        /// <summary>The most lives a player holds; lives never come back above it. At least 1.</summary>
        public int LifeCap { get; }

        /// <summary>How long one life takes to come back. Positive.</summary>
        public TimeSpan LifeRegenInterval { get; }

        /// <summary>The gold a new player's balance starts with. Not earned gold (D22). Never negative.</summary>
        public int StartingGold { get; }

        /// <summary>What refilling the lives to the cap costs, however many are missing (D62). Never negative.</summary>
        public int LivesRefillPrice { get; }

        /// <summary>
        /// The win streak's rewards (D61), <see cref="StreakReward.Wins"/>
        /// strictly ascending, each positive with a count of at least 1. May
        /// be empty: no streak pays anything.
        /// </summary>
        public IReadOnlyList<StreakReward> StreakRewards { get; }

        /// <summary>
        /// The seconds one Clock adds to the countdown (D65). Positive, so a
        /// Clock whose use was checked always adds time.
        /// </summary>
        public int ClockBonusSeconds { get; }

        /// <summary>
        /// The gold packs sold for real money (D72), in the store's order:
        /// each product id not blank and unique, each pack's gold at least 1.
        /// May be empty: the store sells no gold. A pack's price is the
        /// purchase service's, not a rule.
        /// </summary>
        public IReadOnlyList<GoldPack> GoldPacks { get; }

        /// <summary>
        /// The name of a new player and of a save older than format version 4
        /// (D71), in the form the name rule normalizes it to; "Player" in a
        /// fresh Meta Config.
        /// </summary>
        public string DefaultPlayerName { get; }

        /// <summary>
        /// The avatar id of a new player and of a save older than format
        /// version 4 (D71): the first of the catalog, "bear" in a fresh Meta Config. Never
        /// empty; whether the catalog has it is checked where the catalog is.
        /// </summary>
        public string DefaultAvatarId { get; }

        /// <summary>What one <paramref name="kind"/> costs in the store (D62). Never negative.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public int JokerPrice(JokerKind kind) => jokerPrices.Of(kind);

        /// <summary>How many of <paramref name="kind"/> a new player holds (D61). Never negative.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public int StartingJokers(JokerKind kind) => startingJokers.Of(kind);
    }
}
