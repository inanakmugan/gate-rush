using System;
using System.Collections.Generic;
using GateRush.Meta;

namespace GateRush.UI
{
    /// <summary>
    /// What the win streak's bar shows for a streak and a reward table
    /// (Module 37, D61): the next reward, the wins still needed for it, how
    /// much of the bar is filled, and where each reward stands and sits on the
    /// bar. Plain code with no scene; the screens draw it and decide nothing.
    /// </summary>
    /// <remarks>
    /// <para>The bar runs from 0 to the last reward's wins. The fill and every
    /// reward's position are both shares of that span, and the states are
    /// read from the same streak, so a reward is <see cref="StreakRewardState.Collected"/>
    /// exactly when the fill has reached its marker.</para>
    /// <para>The table must be valid, as <see cref="MetaRules"/> ensures:
    /// wins at least 1 and strictly ascending. Past the last reward the
    /// streak keeps counting while the bar stays full.</para>
    /// </remarks>
    public sealed class StreakProgress
    {
        private StreakProgress(
            int streak, StreakReward? nextReward, int winsToNext, float fill,
            IReadOnlyList<StreakRewardState> states, IReadOnlyList<float> positions)
        {
            Streak = streak;
            NextReward = nextReward;
            WinsToNext = winsToNext;
            Fill = fill;
            States = states;
            Positions = positions;
        }

        /// <summary>The streak this progress is read for.</summary>
        public int Streak { get; }

        /// <summary>The first reward whose wins are above the streak; null past the last reward and for an empty table.</summary>
        public StreakReward? NextReward { get; }

        /// <summary>The wins still needed for <see cref="NextReward"/>; 0 when there is none.</summary>
        public int WinsToNext { get; }

        /// <summary>The share of the bar filled, from 0 to 1: the streak over the last reward's wins, capped at 1; 0 for an empty table.</summary>
        public float Fill { get; }

        /// <summary>Each reward's state, in the table's order.</summary>
        public IReadOnlyList<StreakRewardState> States { get; }

        /// <summary>Each reward's place on the bar, in the table's order: its wins over the last reward's wins, so the last is at 1.</summary>
        public IReadOnlyList<float> Positions { get; }

        /// <summary>The progress of <paramref name="streak"/> through <paramref name="rewards"/>, a valid reward table.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="streak"/> is negative.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="rewards"/> is null.</exception>
        public static StreakProgress Of(int streak, IReadOnlyList<StreakReward> rewards)
        {
            if (streak < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(streak), streak, "A win streak may not be negative.");
            }

            if (rewards == null)
            {
                throw new ArgumentNullException(nameof(rewards));
            }

            var count = rewards.Count;
            if (count == 0)
            {
                return new StreakProgress(streak, null, 0, 0f, Array.Empty<StreakRewardState>(), Array.Empty<float>());
            }

            var states = new StreakRewardState[count];
            var positions = new float[count];

            // At least 1 in a valid table, so the divisions below are safe.
            var lastWins = rewards[count - 1].Wins;
            StreakReward? next = null;
            for (var i = 0; i < count; i++)
            {
                var reward = rewards[i];
                positions[i] = (float)reward.Wins / lastWins;
                if (reward.Wins <= streak)
                {
                    states[i] = StreakRewardState.Collected;
                }
                else if (!next.HasValue)
                {
                    next = reward;
                    states[i] = StreakRewardState.Next;
                }
                else
                {
                    states[i] = StreakRewardState.Ahead;
                }
            }

            var winsToNext = next.HasValue ? next.Value.Wins - streak : 0;
            var fill = (float)Math.Min(streak, lastWins) / lastWins;
            return new StreakProgress(streak, next, winsToNext, fill, Array.AsReadOnly(states), Array.AsReadOnly(positions));
        }
    }
}
