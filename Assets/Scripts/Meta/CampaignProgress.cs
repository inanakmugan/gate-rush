using System;

namespace GateRush.Meta
{
    /// <summary>
    /// Where the player is in the campaign (Module 31, D58, D63): the index of
    /// the campaign level in the level order, 0 for a new player, raised by
    /// one on each campaign win. A section of <see cref="SaveData"/>, separate
    /// from the completed marks, so a free-play win neither skips a level nor
    /// takes its gold.
    /// </summary>
    /// <remarks>
    /// This class does not know how many levels there are. Whether the
    /// position is past the last one, and which level it names, is the
    /// catalog's question. The position stops at <see cref="int.MaxValue"/>
    /// rather than overflowing.
    /// </remarks>
    public sealed class CampaignProgress
    {
        /// <summary>The campaign at <paramref name="position"/>, as a save restores it.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
        public CampaignProgress(int position)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "The campaign position may not be negative.");
            }

            Position = position;
        }

        /// <summary>The index of the campaign level in the level order. Never negative.</summary>
        public int Position { get; private set; }

        /// <summary>The campaign of a new player: at its first level.</summary>
        public static CampaignProgress Starting() => new CampaignProgress(0);

        /// <summary>Moves on to the next level after a campaign win, stopping at <see cref="int.MaxValue"/>.</summary>
        public void Advance()
        {
            if (Position < int.MaxValue)
            {
                Position++;
            }
        }
    }
}
