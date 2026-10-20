using System;
using GateRush.Platform;

namespace GateRush.Tests
{
    /// <summary>
    /// A leaderboard service for tests (Module 39, D70): it holds each fetch
    /// until the test answers it, so a screen can be shown Loading, a failed
    /// result and any rows a real backend could send.
    /// </summary>
    public sealed class StubLeaderboardService : ILeaderboardService
    {
        private Action<LeaderboardResult> pending;

        /// <summary>The player of the last fetch.</summary>
        public LeaderboardPlayer LastPlayer { get; private set; }

        /// <inheritdoc />
        public bool IsBusy => pending != null;

        /// <inheritdoc />
        public void Fetch(LeaderboardPlayer player, Action<LeaderboardResult> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (IsBusy)
            {
                throw new InvalidOperationException("A fetch is already pending.");
            }

            LastPlayer = player;
            pending = done;
        }

        /// <summary>Answers the pending fetch with <paramref name="result"/>, clearing <see cref="IsBusy"/> first.</summary>
        /// <exception cref="InvalidOperationException">No fetch is pending.</exception>
        public void Answer(LeaderboardResult result)
        {
            var done = pending ?? throw new InvalidOperationException("No fetch is pending.");
            pending = null;
            done(result);
        }
    }
}
