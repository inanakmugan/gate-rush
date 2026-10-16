using System;
using System.Collections.Generic;

namespace GateRush.Meta
{
    /// <summary>
    /// One row of the win streak's reward table (D61): when the streak
    /// becomes exactly <see cref="Wins"/>, the player gets <see cref="Count"/>
    /// of <see cref="Joker"/>. Plain data with no checks of its own: a table
    /// is checked whole by <see cref="ProblemsOf"/>, since whether
    /// <see cref="Wins"/> is in order depends on the row before it.
    /// </summary>
    public readonly struct StreakReward
    {
        /// <summary>A reward of <paramref name="count"/> <paramref name="joker"/> at a streak of <paramref name="wins"/>.</summary>
        public StreakReward(int wins, JokerKind joker, int count)
        {
            Wins = wins;
            Joker = joker;
            Count = count;
        }

        /// <summary>The streak at which the reward is given. Positive in a valid table.</summary>
        public int Wins { get; }

        /// <summary>The joker given.</summary>
        public JokerKind Joker { get; }

        /// <summary>How many of <see cref="Joker"/> are given. At least 1 in a valid table.</summary>
        public int Count { get; }

        /// <summary>
        /// Everything wrong with <paramref name="table"/>, one sentence per
        /// problem, each starting with the element's index: a
        /// <see cref="Wins"/> below 1 or not above the element before it, a
        /// <see cref="Count"/> below 1, or a <see cref="Joker"/> that is not a
        /// declared kind. Empty when the table is valid; an empty table is
        /// valid and gives no reward. The one check of a table:
        /// <see cref="MetaRules"/> throws on it, and the configuration asset
        /// reports it.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="table"/> is null.</exception>
        public static IReadOnlyList<string> ProblemsOf(IReadOnlyList<StreakReward> table)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            var problems = new List<string>();
            for (var i = 0; i < table.Count; i++)
            {
                var reward = table[i];
                if (reward.Wins < 1)
                {
                    problems.Add($"element {i}: Wins must be at least 1, not {reward.Wins}.");
                }

                if (i > 0 && reward.Wins <= table[i - 1].Wins)
                {
                    problems.Add($"element {i}: Wins {reward.Wins} must be above the {table[i - 1].Wins} of element {i - 1}.");
                }

                if (reward.Count < 1)
                {
                    problems.Add($"element {i}: Count must be at least 1, not {reward.Count}.");
                }

                if (!JokerKinds.IsDefined(reward.Joker))
                {
                    problems.Add($"element {i}: Joker {(int)reward.Joker} is not a joker kind.");
                }
            }

            return problems;
        }
    }
}
