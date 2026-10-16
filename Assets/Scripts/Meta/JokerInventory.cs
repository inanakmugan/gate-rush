using System;

namespace GateRush.Meta
{
    /// <summary>
    /// The jokers the player holds (Module 31, D61): a count per
    /// <see cref="JokerKind"/>, never negative. A section of
    /// <see cref="SaveData"/>.
    /// </summary>
    /// <remarks>
    /// <para>Counts come in from streak rewards and the store, through
    /// <c>MetaGame</c>. <see cref="TryUse"/> takes one out; nothing calls it
    /// before Phase 5 uses jokers on the board.</para>
    /// <para>A negative count is a bug in the caller and throws. Sums stop at
    /// <see cref="int.MaxValue"/> rather than overflowing.</para>
    /// </remarks>
    public sealed class JokerInventory
    {
        private int clocks;
        private int rockets;
        private int brooms;

        /// <summary>An inventory holding the given counts, as a save restores it.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        public JokerInventory(int clocks, int rockets, int brooms)
        {
            this.clocks = NonNegative(clocks, nameof(clocks));
            this.rockets = NonNegative(rockets, nameof(rockets));
            this.brooms = NonNegative(brooms, nameof(brooms));
        }

        /// <summary>The jokers of a new player: <see cref="MetaRules.StartingJokers"/> of each kind.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static JokerInventory Starting(MetaRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return new JokerInventory(
                rules.StartingJokers(JokerKind.Clock),
                rules.StartingJokers(JokerKind.Rocket),
                rules.StartingJokers(JokerKind.Broom));
        }

        /// <summary>How many of <paramref name="kind"/> are held.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public int CountOf(JokerKind kind) => Slot(kind);

        /// <summary>Adds <paramref name="count"/> of <paramref name="kind"/>, stopping at <see cref="int.MaxValue"/>. Zero changes nothing.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative, or <paramref name="kind"/> is not a declared kind.</exception>
        public void Add(JokerKind kind, int count)
        {
            NonNegative(count, nameof(count));
            ref var slot = ref Slot(kind);
            slot = (int)Math.Min((long)slot + count, int.MaxValue);
        }

        /// <summary>Takes one <paramref name="kind"/>. False, with nothing changed, when none is held.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public bool TryUse(JokerKind kind)
        {
            ref var slot = ref Slot(kind);
            if (slot == 0)
            {
                return false;
            }

            slot--;
            return true;
        }

        private ref int Slot(JokerKind kind)
        {
            switch (kind)
            {
                case JokerKind.Clock:
                    return ref clocks;
                case JokerKind.Rocket:
                    return ref rockets;
                case JokerKind.Broom:
                    return ref brooms;
                default:
                    throw JokerKinds.Undefined(kind, nameof(kind));
            }
        }

        private static int NonNegative(int count, string paramName)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, count, "A count of jokers may not be negative.");
            }

            return count;
        }
    }
}
