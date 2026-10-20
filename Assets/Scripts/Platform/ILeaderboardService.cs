using System;

namespace GateRush.Platform
{
    /// <summary>
    /// Ranks the player among other players (Module 39, D70, D73). The one
    /// seam between the Leaderboard screen and wherever the rows come from:
    /// labelled demo players in this build, an online backend in a later one.
    /// Nothing above this interface knows which, beyond
    /// <see cref="LeaderboardResult.IsSimulated"/>.
    /// </summary>
    /// <remarks>
    /// The caller passes the player's own entry, read when the tab is shown;
    /// the service decides the ranks. Everything runs on the caller's thread
    /// (WebGL has no other).
    /// </remarks>
    public interface ILeaderboardService
    {
        /// <summary>
        /// True from <see cref="Fetch"/> until its answer; cleared before
        /// <c>done</c> runs, so <c>done</c> may fetch again.
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Ranks <paramref name="player"/> and calls <paramref name="done"/>
        /// exactly once with the result, now or later. A fetch that fails gives
        /// a result whose <see cref="LeaderboardResult.IsLoaded"/> is false.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="done"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="player"/> has a null name or avatar id.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="player"/>'s score is negative.</exception>
        /// <exception cref="InvalidOperationException"><see cref="IsBusy"/> is true; the caller checks it first.</exception>
        void Fetch(LeaderboardPlayer player, Action<LeaderboardResult> done);
    }
}
