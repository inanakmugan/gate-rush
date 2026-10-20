using System;
using System.Collections.Generic;
using GateRush.Platform;

namespace GateRush.Runtime
{
    /// <summary>
    /// Orders the demo players and the player into ranked rows (Module 39,
    /// D73): score descending, ranks 1 to N + 1, one per row with no shared
    /// ranks. The player sits below every demo player with the same score —
    /// they have to pass a score to take its place. Demo players with the
    /// same score keep their configuration order, so the table never changes
    /// between fetches.
    /// </summary>
    public static class LeaderboardRanking
    {
        /// <summary>
        /// <paramref name="demoPlayers"/> and <paramref name="player"/> as
        /// ranked rows, best first; only the player's row is marked
        /// <see cref="LeaderboardEntry.IsPlayer"/>. The demo players are not
        /// checked here; their service checks them once.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="demoPlayers"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="player"/>'s name or avatar id is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="player"/>'s score is negative.</exception>
        public static IReadOnlyList<LeaderboardEntry> Rank(IReadOnlyList<LeaderboardPlayer> demoPlayers, LeaderboardPlayer player)
        {
            if (demoPlayers == null)
            {
                throw new ArgumentNullException(nameof(demoPlayers));
            }

            RequirePlayer(player);

            var order = new int[demoPlayers.Count];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            // The index breaks ties, so equal scores keep their configuration order.
            Array.Sort(order, (a, b) =>
            {
                var byScore = demoPlayers[b].Score.CompareTo(demoPlayers[a].Score);
                return byScore != 0 ? byScore : a.CompareTo(b);
            });

            // Below every demo player with as much or more.
            var playerIndex = 0;
            for (var i = 0; i < demoPlayers.Count; i++)
            {
                if (demoPlayers[i].Score >= player.Score)
                {
                    playerIndex++;
                }
            }

            var entries = new LeaderboardEntry[order.Length + 1];
            var next = 0;
            for (var i = 0; i < entries.Length; i++)
            {
                var rank = i + 1;
                if (i == playerIndex)
                {
                    entries[i] = new LeaderboardEntry(rank, player.Name, player.AvatarId, player.Score, true);
                    continue;
                }

                var demo = demoPlayers[order[next]];
                next++;
                entries[i] = new LeaderboardEntry(rank, demo.Name, demo.AvatarId, demo.Score, false);
            }

            return Array.AsReadOnly(entries);
        }

        private static void RequirePlayer(LeaderboardPlayer player)
        {
            if (player.Name == null)
            {
                throw new ArgumentException("The player's name may not be null.", nameof(player));
            }

            if (player.AvatarId == null)
            {
                throw new ArgumentException("The player's avatar id may not be null.", nameof(player));
            }

            if (player.Score < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(player), player.Score, "The player's score may not be negative.");
            }
        }
    }
}
