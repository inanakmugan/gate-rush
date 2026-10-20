using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Platform;

namespace GateRush.Runtime
{
    /// <summary>
    /// The leaderboard service of this build (Module 39, D73): the player
    /// ranked among demo players from configuration
    /// (<see cref="LeaderboardRanking"/>), and every result marked
    /// <see cref="LeaderboardResult.IsSimulated"/> so the screen says so. It
    /// answers at once and never fails; <see cref="IsBusy"/> is never true.
    /// </summary>
    /// <remarks>
    /// A real backend is a new <see cref="ILeaderboardService"/>; nothing
    /// above the interface changes (D70).
    /// </remarks>
    public sealed class SimulatedLeaderboardService : ILeaderboardService
    {
        private readonly IReadOnlyList<LeaderboardPlayer> demoPlayers;

        /// <summary>A service ranking the player among <paramref name="demoPlayers"/>, copied.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="demoPlayers"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="demoPlayers"/> has a problem <see cref="ProblemsOf"/> reports.</exception>
        public SimulatedLeaderboardService(IReadOnlyList<LeaderboardPlayer> demoPlayers)
        {
            if (demoPlayers == null)
            {
                throw new ArgumentNullException(nameof(demoPlayers));
            }

            var problems = ProblemsOf(demoPlayers);
            if (problems.Count > 0)
            {
                throw new ArgumentException($"The demo players are invalid: {problems[0]}", nameof(demoPlayers));
            }

            var copy = new LeaderboardPlayer[demoPlayers.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = demoPlayers[i];
            }

            this.demoPlayers = Array.AsReadOnly(copy);
        }

        /// <inheritdoc />
        public bool IsBusy => false;

        /// <summary>
        /// Everything wrong with <paramref name="demoPlayers"/>, one sentence
        /// per problem, each starting with the element's index: a name the
        /// player name rule refuses (<see cref="PlayerProfile.TryNormalizeName"/>),
        /// a blank avatar id, or a negative score; and a table with no player
        /// at all. Empty when the table can be shown. The one check: the
        /// constructor throws on it, and the configuration asset reports it.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="demoPlayers"/> is null.</exception>
        public static IReadOnlyList<string> ProblemsOf(IReadOnlyList<LeaderboardPlayer> demoPlayers)
        {
            if (demoPlayers == null)
            {
                throw new ArgumentNullException(nameof(demoPlayers));
            }

            var problems = new List<string>();
            if (demoPlayers.Count == 0)
            {
                problems.Add("there is no demo player; add at least one.");
            }

            for (var i = 0; i < demoPlayers.Count; i++)
            {
                var player = demoPlayers[i];
                if (!PlayerProfile.TryNormalizeName(player.Name, out _))
                {
                    problems.Add($"element {i}: Name '{player.Name}' is refused by the player name rule.");
                }

                if (string.IsNullOrWhiteSpace(player.AvatarId))
                {
                    problems.Add($"element {i}: Avatar Id is empty.");
                }

                if (player.Score < 0)
                {
                    problems.Add($"element {i}: Score {player.Score} is negative.");
                }
            }

            return problems;
        }

        /// <summary>Calls <paramref name="done"/> at once with the ranked table, loaded and simulated.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="done"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="player"/> has a null name or avatar id.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="player"/>'s score is negative.</exception>
        public void Fetch(LeaderboardPlayer player, Action<LeaderboardResult> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            var entries = LeaderboardRanking.Rank(demoPlayers, player);
            done(LeaderboardResult.Loaded(entries, true));
        }
    }
}
