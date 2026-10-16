using System;

namespace GateRush.Meta
{
    /// <summary>
    /// The win streak (Module 31, D61): campaign wins in a row, and the
    /// highest the streak has ever reached. Any campaign loss sets the streak
    /// to 0; the best never goes down. A section of <see cref="SaveData"/>.
    /// </summary>
    /// <remarks>
    /// When a win is a campaign win, and what reaching a threshold pays, is
    /// <c>MetaGame</c>'s question; this class only counts. The streak stops at
    /// <see cref="int.MaxValue"/> rather than overflowing.
    /// </remarks>
    public sealed class WinStreak
    {
        /// <summary>A streak of <paramref name="current"/> with a best of <paramref name="best"/>, as a save restores it.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Either value is negative.</exception>
        /// <exception cref="ArgumentException"><paramref name="best"/> is below <paramref name="current"/>.</exception>
        public WinStreak(int current, int best)
        {
            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "The win streak may not be negative.");
            }

            if (best < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(best), best, "The best win streak may not be negative.");
            }

            if (best < current)
            {
                throw new ArgumentException($"The best win streak {best} may not be below the current one, {current}.", nameof(best));
            }

            Current = current;
            Best = best;
        }

        /// <summary>The campaign wins in a row since the last campaign loss.</summary>
        public int Current { get; private set; }

        /// <summary>The highest <see cref="Current"/> has reached. Never below it.</summary>
        public int Best { get; private set; }

        /// <summary>The streak of a new player: none, with no best.</summary>
        public static WinStreak Starting() => new WinStreak(0, 0);

        /// <summary>
        /// Adds one win, stopping at <see cref="int.MaxValue"/>, and raises
        /// <see cref="Best"/> when the streak passes it. Returns the new
        /// <see cref="Current"/>.
        /// </summary>
        public int RecordWin()
        {
            if (Current < int.MaxValue)
            {
                Current++;
            }

            Best = Math.Max(Best, Current);
            return Current;
        }

        /// <summary>Sets the streak to 0 after a campaign loss. <see cref="Best"/> is kept.</summary>
        public void Reset() => Current = 0;
    }
}
