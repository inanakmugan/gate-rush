using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// The end of the campaign (Module 49): whether it is finished, whether
    /// the level just left finished it, and what the End of Content
    /// overlay's Restart plays. Plain answers for the bootstrap, which opens
    /// the overlay and starts the level.
    /// </summary>
    /// <remarks>
    /// Restart plays level 1 in free play (D58), so it needs no life, costs
    /// none, pays nothing and leaves the save as it was: the campaign stays
    /// finished, and Play on Home opens the overlay again.
    /// </remarks>
    public static class CampaignEnd
    {
        /// <summary>
        /// How Restart plays its level: free play, never a campaign attempt
        /// (D58). The bootstrap loads the level in this mode, through the
        /// level grid's path.
        /// </summary>
        public const PlayMode RestartMode = PlayMode.FreePlay;

        /// <summary>
        /// True when <paramref name="catalog"/> is a usable level order and
        /// <paramref name="position"/> is past its last level. False with no
        /// usable order: there is no campaign to have finished, and Play
        /// falls back to the scene's level.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative; the save never holds one.</exception>
        public static bool IsOver(LevelCatalog catalog, int position)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "A campaign position may not be negative.");
            }

            return catalog != null && catalog.Names.Count > 0 && position >= catalog.Names.Count;
        }

        /// <summary>
        /// True when the level being left was won in the campaign and left
        /// the campaign over (<see cref="IsOver"/>): the win of the last
        /// level. Leaving it opens Home with the End of Content overlay. A
        /// free-play win of the same level, or a loss, is false.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative; the save never holds one.</exception>
        public static bool IsJustFinished(PlayMode mode, LevelOutcome outcome, LevelCatalog catalog, int position) =>
            IsOver(catalog, position) && mode == PlayMode.Campaign && outcome == LevelOutcome.Won;

        /// <summary>
        /// The level Restart plays: the first of <paramref name="catalog"/>'s
        /// order, level 1, always in <see cref="PlayMode.FreePlay"/>
        /// (<see cref="RestartMode"/>). False, with <paramref name="levelName"/>
        /// null, with no usable level order.
        /// </summary>
        public static bool TryGetRestartLevel(LevelCatalog catalog, out string levelName)
        {
            if (catalog == null || catalog.Names.Count == 0)
            {
                levelName = null;
                return false;
            }

            levelName = catalog.Names[0];
            return true;
        }
    }
}
