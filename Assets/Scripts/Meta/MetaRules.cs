using System;

namespace GateRush.Meta
{
    /// <summary>
    /// The numbers the meta game runs on (Module 30, D59): how many lives a
    /// player can hold, how long one takes to come back, and the gold a new
    /// player starts with. Built by <c>Runtime</c> from its configuration
    /// asset; this assembly holds no default of its own, so every default —
    /// a new player's, and that of a section an older save lacks (D60) —
    /// comes from one instance of this class.
    /// </summary>
    public sealed class MetaRules
    {
        /// <summary>Rules with the given cap, regeneration interval and starting gold.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="lifeCap"/> is below 1, <paramref name="lifeRegenInterval"/>
        /// is not positive, or <paramref name="startingGold"/> is negative.
        /// </exception>
        public MetaRules(int lifeCap, TimeSpan lifeRegenInterval, int startingGold)
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

            LifeCap = lifeCap;
            LifeRegenInterval = lifeRegenInterval;
            StartingGold = startingGold;
        }

        /// <summary>The most lives a player holds; lives never come back above it. At least 1.</summary>
        public int LifeCap { get; }

        /// <summary>How long one life takes to come back. Positive.</summary>
        public TimeSpan LifeRegenInterval { get; }

        /// <summary>The gold a new player's balance starts with. Not earned gold (D22). Never negative.</summary>
        public int StartingGold { get; }
    }
}
