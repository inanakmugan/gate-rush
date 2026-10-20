using System;
using System.Collections.Generic;
using GateRush.Meta;

namespace GateRush.UI
{
    /// <summary>One line of the Streak Reward overlay: how many of a joker the announced wins gave in all (Module 37).</summary>
    public readonly struct JokerPrize
    {
        /// <summary>A prize of <paramref name="count"/> <paramref name="joker"/>.</summary>
        public JokerPrize(JokerKind joker, int count)
        {
            Joker = joker;
            Count = count;
        }

        /// <summary>The joker given.</summary>
        public JokerKind Joker { get; }

        /// <summary>How many were given in all.</summary>
        public int Count { get; }
    }

    /// <summary>
    /// Merges the streak rewards announced together (Module 37): several
    /// campaign wins may each have given rewards before Home was shown, and
    /// the overlay shows one line per joker kind. Plain code, so the merging
    /// is tested on its own.
    /// </summary>
    public static class StreakPrizes
    {
        /// <summary>
        /// One prize per joker kind <paramref name="rewards"/> holds, with the
        /// counts of that kind added up, stopping at <see cref="int.MaxValue"/>;
        /// in the order Clock, Rocket, Broom, whatever the order of
        /// <paramref name="rewards"/>. Empty for no rewards.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="rewards"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A reward's joker is not a declared kind.</exception>
        public static IReadOnlyList<JokerPrize> Merge(IReadOnlyList<StreakReward> rewards)
        {
            if (rewards == null)
            {
                throw new ArgumentNullException(nameof(rewards));
            }

            var clock = 0L;
            var rocket = 0L;
            var broom = 0L;
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                switch (reward.Joker)
                {
                    case JokerKind.Clock:
                        clock += reward.Count;
                        break;
                    case JokerKind.Rocket:
                        rocket += reward.Count;
                        break;
                    case JokerKind.Broom:
                        broom += reward.Count;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(rewards), reward.Joker, $"Reward {i} gives {(int)reward.Joker}, which is not a joker kind.");
                }
            }

            var prizes = new List<JokerPrize>();
            AddIfAny(prizes, JokerKind.Clock, clock);
            AddIfAny(prizes, JokerKind.Rocket, rocket);
            AddIfAny(prizes, JokerKind.Broom, broom);
            return prizes;
        }

        private static void AddIfAny(List<JokerPrize> prizes, JokerKind joker, long count)
        {
            if (count > 0)
            {
                prizes.Add(new JokerPrize(joker, (int)Math.Min(count, int.MaxValue)));
            }
        }
    }
}
