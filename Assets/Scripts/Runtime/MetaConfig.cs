using System;
using System.Collections.Generic;
using GateRush.Meta;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Every tunable number of the meta game (Modules 30, 31, 33 and 36): the
    /// life cap, how long a life takes to come back, a new player's gold and
    /// jokers, the store's prices, the win streak's reward table, the seconds
    /// a Clock adds, free play's jokers per level and the gold of each gold
    /// pack. Nothing in
    /// <c>GateRush.Meta</c> hardcodes one of these; <see cref="ToRules"/>
    /// hands them over as <see cref="MetaRules"/>, all but free play's
    /// allowance, which <c>Meta</c> knows nothing of (D66) and
    /// <see cref="FreePlayJokersPerLevel"/> gives to <c>Runtime</c>.
    /// </summary>
    /// <remarks>
    /// The save cannot be read without these rules — an older save takes the
    /// sections it lacks from them (D60) — so a missing config, or one with
    /// <see cref="Problems"/>, stops the bootstrap.
    /// </remarks>
    [CreateAssetMenu(fileName = "MetaConfig", menuName = "Gate Rush/Meta Config")]
    public sealed class MetaConfig : ScriptableObject
    {
        [Header("Lives and gold")]
        [Tooltip("The most lives a player holds; lives come back one at a time up to it. At least 1. A saved count above a lowered cap reads as the cap.")]
        [SerializeField] private int lifeCap = 5;

        [Tooltip("Minutes for one life to come back, counted on real-world UTC time, also while the game is closed. Positive.")]
        [SerializeField] private int lifeRegenMinutes = 20;

        [Tooltip("The gold a new player starts with, and what a save from before Module 30 gets. It is not earned gold, so it is not in the lifetime gold the leaderboard ranks on. At least 0.")]
        [SerializeField] private int startingGold = 500;

        [Header("Store")]
        [Tooltip("Gold for one Clock joker in the store. At least 0.")]
        [SerializeField] private int clockPrice = 100;

        [Tooltip("Gold for one Rocket joker in the store. At least 0.")]
        [SerializeField] private int rocketPrice = 200;

        [Tooltip("Gold for one Broom joker in the store. At least 0.")]
        [SerializeField] private int broomPrice = 300;

        [Tooltip("Gold to refill the lives to the cap, however many are missing. Refilling full lives is refused and costs nothing. At least 0.")]
        [SerializeField] private int livesRefillPrice = 300;

        [Header("Starting jokers")]
        [Tooltip("Clock jokers a new player holds, and what a save from before Module 31 gets. At least 0.")]
        [SerializeField] private int startingClocks = 1;

        [Tooltip("Rocket jokers a new player holds, and what a save from before Module 31 gets. At least 0.")]
        [SerializeField] private int startingRockets = 1;

        [Tooltip("Broom jokers a new player holds, and what a save from before Module 31 gets. At least 0.")]
        [SerializeField] private int startingBrooms = 1;

        [Header("Win streak")]
        [Tooltip("The jokers given when the campaign win streak becomes exactly Wins; reaching it again after a loss pays again, and past the last entry nothing more is paid. Wins positive and strictly ascending, Count at least 1. May be empty.")]
        [SerializeField] private StreakRewardEntry[] streakRewards =
        {
            new StreakRewardEntry(3, JokerKind.Clock, 1),
            new StreakRewardEntry(5, JokerKind.Rocket, 1),
            new StreakRewardEntry(10, JokerKind.Broom, 1),
        };

        [Header("Jokers")]
        [Tooltip("Seconds one Clock joker adds to the level's countdown. Positive.")]
        [SerializeField] private int clockBonusSeconds = 20;

        [Tooltip("Jokers of each kind a level played in free play starts with, apart from the campaign's inventory. Not saved; full again on Restart and on every new level. At least 0.")]
        [SerializeField] private int freePlayJokersPerLevel = 3;

        [Header("Gold packs")]
        [Tooltip("The gold packs sold for real money (D72), in the Store's order: the product id the purchase service sells each under, and the gold it adds to the balance — never to the lifetime gold. Ids not empty and unique, Gold at least 1. Prices belong to the purchase service (the Simulated Store Config). May be empty.")]
        [SerializeField] private GoldPackEntry[] goldPacks =
        {
            new GoldPackEntry("gold_small", 500),
            new GoldPackEntry("gold_medium", 1500),
            new GoldPackEntry("gold_large", 4000),
        };

        /// <summary>
        /// How many of each joker a level played in free play starts with
        /// (D66). Never negative when <see cref="Problems"/> is empty. Not part
        /// of <see cref="MetaRules"/>: <c>Meta</c> knows nothing of free play.
        /// </summary>
        public int FreePlayJokersPerLevel => freePlayJokersPerLevel;

        /// <summary>
        /// Everything wrong with this asset, each message naming its field;
        /// empty when <see cref="ToRules"/> can be called.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();

            if (lifeCap < 1)
            {
                problems.Add($"{name}: Life Cap must be at least 1.");
            }

            if (lifeRegenMinutes <= 0)
            {
                problems.Add($"{name}: Life Regen Minutes must be positive.");
            }

            AddIfNegative(problems, startingGold, "Starting Gold");
            AddIfNegative(problems, clockPrice, "Clock Price");
            AddIfNegative(problems, rocketPrice, "Rocket Price");
            AddIfNegative(problems, broomPrice, "Broom Price");
            AddIfNegative(problems, livesRefillPrice, "Lives Refill Price");
            AddIfNegative(problems, startingClocks, "Starting Clocks");
            AddIfNegative(problems, startingRockets, "Starting Rockets");
            AddIfNegative(problems, startingBrooms, "Starting Brooms");

            foreach (var problem in StreakReward.ProblemsOf(StreakRewards()))
            {
                problems.Add($"{name}: Streak Rewards {problem}");
            }

            if (clockBonusSeconds <= 0)
            {
                problems.Add($"{name}: Clock Bonus Seconds must be positive.");
            }

            AddIfNegative(problems, freePlayJokersPerLevel, "Free Play Jokers Per Level");

            foreach (var problem in GoldPack.ProblemsOf(GoldPacks()))
            {
                problems.Add($"{name}: Gold Packs {problem}");
            }

            return problems;
        }

        /// <summary>
        /// The rules these values describe. Call only when
        /// <see cref="Problems"/> is empty.
        /// </summary>
        /// <exception cref="ArgumentException">A value <see cref="Problems"/> reports.</exception>
        public MetaRules ToRules() =>
            new MetaRules(
                lifeCap,
                TimeSpan.FromMinutes(lifeRegenMinutes),
                startingGold,
                new JokerAmounts(clockPrice, rocketPrice, broomPrice),
                livesRefillPrice,
                new JokerAmounts(startingClocks, startingRockets, startingBrooms),
                StreakRewards(),
                clockBonusSeconds,
                GoldPacks());

        private StreakReward[] StreakRewards()
        {
            var entries = streakRewards ?? Array.Empty<StreakRewardEntry>();
            var rewards = new StreakReward[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                rewards[i] = entries[i].ToReward();
            }

            return rewards;
        }

        private GoldPack[] GoldPacks()
        {
            var entries = goldPacks ?? Array.Empty<GoldPackEntry>();
            var packs = new GoldPack[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                packs[i] = entries[i].ToPack();
            }

            return packs;
        }

        private void AddIfNegative(List<string> problems, int value, string field)
        {
            if (value < 0)
            {
                problems.Add($"{name}: {field} may not be negative.");
            }
        }

        /// <summary>One row of the win streak's reward table, as the Inspector edits it.</summary>
        [Serializable]
        private sealed class StreakRewardEntry
        {
            [SerializeField] private int wins;
            [SerializeField] private JokerKind joker;
            [SerializeField] private int count;

            public StreakRewardEntry(int wins, JokerKind joker, int count)
            {
                this.wins = wins;
                this.joker = joker;
                this.count = count;
            }

            public StreakReward ToReward() => new StreakReward(wins, joker, count);
        }

        /// <summary>One gold pack, as the Inspector edits it.</summary>
        [Serializable]
        private sealed class GoldPackEntry
        {
            [SerializeField] private string productId;
            [SerializeField] private int gold;

            public GoldPackEntry(string productId, int gold)
            {
                this.productId = productId;
                this.gold = gold;
            }

            public GoldPack ToPack() => new GoldPack(productId, gold);
        }
    }
}
