using System;
using System.Collections.Generic;
using GateRush.Meta;

namespace GateRush.Tests
{
    /// <summary>
    /// Builds <see cref="MetaRules"/> for tests. Every default differs from a
    /// fresh <c>MetaConfig</c>'s, and the per-kind values differ from each
    /// other, so code that used numbers of its own, or mixed up two kinds,
    /// fails. A test names only the values it is about.
    /// </summary>
    public static class MetaRulesFixture
    {
        /// <summary>The life cap of the test rules.</summary>
        public const int LifeCap = 4;

        /// <summary>The regeneration minutes of the test rules.</summary>
        public const int LifeRegenMinutes = 15;

        /// <summary>The starting gold of the test rules.</summary>
        public const int StartingGold = 300;

        /// <summary>The price of a Clock in the test rules.</summary>
        public const int ClockPrice = 30;

        /// <summary>The price of a Rocket in the test rules.</summary>
        public const int RocketPrice = 70;

        /// <summary>The price of a Broom in the test rules.</summary>
        public const int BroomPrice = 110;

        /// <summary>The lives refill price of the test rules.</summary>
        public const int LivesRefillPrice = 90;

        /// <summary>The Clocks a new player holds by the test rules.</summary>
        public const int StartingClocks = 2;

        /// <summary>The Rockets a new player holds by the test rules.</summary>
        public const int StartingRockets = 0;

        /// <summary>The Brooms a new player holds by the test rules.</summary>
        public const int StartingBrooms = 3;

        /// <summary>The seconds a Clock adds by the test rules.</summary>
        public const int ClockBonusSeconds = 12;

        /// <summary>
        /// The reward table of the test rules: 2 Rockets at 3 wins, 3 Clocks
        /// at 5, 1 Broom at 8. A new array on each call.
        /// </summary>
        public static StreakReward[] StreakRewards() => new[]
        {
            new StreakReward(3, JokerKind.Rocket, 2),
            new StreakReward(5, JokerKind.Clock, 3),
            new StreakReward(8, JokerKind.Broom, 1),
        };

        /// <summary>The first gold pack's product id in the test rules.</summary>
        public const string SmallPackId = "test_small";

        /// <summary>The second gold pack's product id in the test rules.</summary>
        public const string LargePackId = "test_large";

        /// <summary>The first gold pack's gold in the test rules.</summary>
        public const int SmallPackGold = 120;

        /// <summary>The second gold pack's gold in the test rules.</summary>
        public const int LargePackGold = 950;

        /// <summary>
        /// The gold packs of the test rules: <see cref="SmallPackId"/> and
        /// <see cref="LargePackId"/>, in that order. A new array on each call.
        /// </summary>
        public static GoldPack[] GoldPacks() => new[]
        {
            new GoldPack(SmallPackId, SmallPackGold),
            new GoldPack(LargePackId, LargePackGold),
        };

        /// <summary>Rules with the test defaults, except the values given.</summary>
        public static MetaRules Build(
            int lifeCap = LifeCap,
            TimeSpan? lifeRegenInterval = null,
            int startingGold = StartingGold,
            JokerAmounts? jokerPrices = null,
            int livesRefillPrice = LivesRefillPrice,
            JokerAmounts? startingJokers = null,
            IReadOnlyList<StreakReward> streakRewards = null,
            int clockBonusSeconds = ClockBonusSeconds,
            IReadOnlyList<GoldPack> goldPacks = null) =>
            new MetaRules(
                lifeCap,
                lifeRegenInterval ?? TimeSpan.FromMinutes(LifeRegenMinutes),
                startingGold,
                jokerPrices ?? new JokerAmounts(ClockPrice, RocketPrice, BroomPrice),
                livesRefillPrice,
                startingJokers ?? new JokerAmounts(StartingClocks, StartingRockets, StartingBrooms),
                streakRewards ?? StreakRewards(),
                clockBonusSeconds,
                goldPacks ?? GoldPacks());
    }
}
