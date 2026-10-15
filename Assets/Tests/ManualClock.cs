using System;
using GateRush.Platform;

namespace GateRush.Tests
{
    /// <summary>
    /// An <see cref="ITimeProvider"/> a test moves by hand, so time-dependent
    /// rules are proved by moving the clock instead of waiting (D19). It lives
    /// only in the tests: nothing in the shipped game can set the time.
    /// </summary>
    public sealed class ManualClock : ITimeProvider
    {
        /// <summary>A clock reading <paramref name="startUtc"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="startUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public ManualClock(DateTime startUtc)
        {
            if (startUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException($"The clock must start at a UTC time, not {startUtc.Kind}.", nameof(startUtc));
            }

            UtcNow = startUtc;
        }

        /// <inheritdoc />
        public DateTime UtcNow { get; private set; }

        /// <summary>Moves the clock by <paramref name="span"/>; a negative span sets it back.</summary>
        public void Advance(TimeSpan span) => UtcNow += span;
    }
}
