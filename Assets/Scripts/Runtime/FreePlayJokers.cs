using System;
using GateRush.Meta;

namespace GateRush.Runtime
{
    /// <summary>
    /// Free play's jokers for one level (D66): the same number of each kind
    /// at the start, its own allowance apart from the campaign's inventory,
    /// full again on <see cref="Refill"/> and never topped up otherwise. Not
    /// saved, and never seen by <see cref="MetaGame"/>, which hears nothing of
    /// free play (D58).
    /// </summary>
    /// <remarks>
    /// The counts are kept in a <see cref="JokerInventory"/> of its own, which
    /// already counts per kind, never below zero, and rejects an undefined
    /// kind. It is a separate instance from the save's, so nothing here can
    /// reach the campaign's jokers.
    /// </remarks>
    public sealed class FreePlayJokers
    {
        private readonly int perLevel;
        private JokerInventory jokers;

        /// <summary>An allowance of <paramref name="perLevel"/> of each kind, full.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="perLevel"/> is negative.</exception>
        public FreePlayJokers(int perLevel)
        {
            if (perLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(perLevel), perLevel, "Free play's jokers per level may not be negative.");
            }

            this.perLevel = perLevel;
            Refill();
        }

        /// <summary>How many of <paramref name="kind"/> are left in this level.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public int CountOf(JokerKind kind) => jokers.CountOf(kind);

        /// <summary>Takes one <paramref name="kind"/>. False, with nothing changed, when none is left.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public bool TryUse(JokerKind kind) => jokers.TryUse(kind);

        /// <summary>Back to the whole allowance of every kind, for a restart or a new level.</summary>
        public void Refill()
        {
            jokers = new JokerInventory(perLevel, perLevel, perLevel);
        }
    }
}
