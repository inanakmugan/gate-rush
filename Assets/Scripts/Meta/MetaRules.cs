using System;
using System.Collections.Generic;

namespace GateRush.Meta
{
    /// <summary>
    /// The numbers the meta game runs on (Modules 30 and 31): how many lives a
    /// player can hold and how long one takes to come back (D59), the gold and
    /// jokers a new player starts with (D59, D61), the store's prices (D62)
    /// and the win streak's reward table (D61). Built by <c>Runtime</c> from
    /// its configuration asset; this assembly holds no default of its own, so
    /// every default — a new player's, and that of a section an older save
    /// lacks (D60) — comes from one instance of this class.
    /// </summary>
    /// <remarks>Immutable: the reward table is copied on construction.</remarks>
    public sealed class MetaRules
    {
        private readonly JokerAmounts jokerPrices;
        private readonly JokerAmounts startingJokers;

        /// <summary>Rules with the given values, every one of them checked.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="lifeCap"/> is below 1, <paramref name="lifeRegenInterval"/>
        /// is not positive, or <paramref name="startingGold"/>, a joker price,
        /// <paramref name="livesRefillPrice"/> or a starting count is negative.
        /// </exception>
        /// <exception cref="ArgumentNullException"><paramref name="streakRewards"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="streakRewards"/> has a problem <see cref="StreakReward.ProblemsOf"/> reports.
        /// </exception>
        public MetaRules(
            int lifeCap,
            TimeSpan lifeRegenInterval,
            int startingGold,
            JokerAmounts jokerPrices,
            int livesRefillPrice,
            JokerAmounts startingJokers,
            IReadOnlyList<StreakReward> streakRewards)
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

        /// <summary>What one <paramref name="kind"/> costs in the store (D62). Never negative.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public int JokerPrice(JokerKind kind) => jokerPrices.Of(kind);

        /// <summary>How many of <paramref name="kind"/> a new player holds (D61). Never negative.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public int StartingJokers(JokerKind kind) => startingJokers.Of(kind);
    }
}
