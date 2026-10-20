using System;
using System.Collections.Generic;

namespace GateRush.Platform
{
    /// <summary>
    /// What a leaderboard fetch answered (Module 39, D70, D73): whether it
    /// loaded, whether its rows are demo players rather than online ones, and
    /// the rows by rank. Immutable; the entries are copied when it is made.
    /// </summary>
    /// <remarks>
    /// <see cref="IsSimulated"/> is the only thing a screen knows of where the
    /// rows came from: while it is true, the screen says so in a line that is
    /// never hidden.
    /// </remarks>
    public sealed class LeaderboardResult
    {
        private LeaderboardResult(bool isLoaded, bool isSimulated, IReadOnlyList<LeaderboardEntry> entries)
        {
            IsLoaded = isLoaded;
            IsSimulated = isSimulated;
            Entries = entries;
        }

        /// <summary>False when the fetch failed: there are no entries, and the screen offers Retry.</summary>
        public bool IsLoaded { get; }

        /// <summary>True when the rows are demo players, not online ones.</summary>
        public bool IsSimulated { get; }

        /// <summary>The rows by rank, best first; empty when not loaded. Never null.</summary>
        public IReadOnlyList<LeaderboardEntry> Entries { get; }

        /// <summary>A loaded result holding a copy of <paramref name="entries"/>, in their order.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="entries"/> is null.</exception>
        public static LeaderboardResult Loaded(IReadOnlyList<LeaderboardEntry> entries, bool isSimulated)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var copy = new LeaderboardEntry[entries.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = entries[i];
            }

            return new LeaderboardResult(true, isSimulated, Array.AsReadOnly(copy));
        }

        /// <summary>A failed fetch: not loaded, with no entries.</summary>
        public static LeaderboardResult Failed(bool isSimulated) =>
            new LeaderboardResult(false, isSimulated, Array.AsReadOnly(Array.Empty<LeaderboardEntry>()));
    }
}
