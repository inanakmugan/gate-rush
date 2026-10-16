using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// Which level the campaign is at (Module 32, D58, D63): the campaign's
    /// position in the save is an index into the level order, and this is
    /// the catalog's answer to it. <c>Meta</c> knows nothing of levels, so
    /// whether the position is past the last level is asked here.
    /// </summary>
    public static class CampaignLevels
    {
        /// <summary>
        /// The file name of the level at <paramref name="position"/> in
        /// <paramref name="catalog"/>'s order. False past the last level — the
        /// campaign is over — and with no usable level order: a null catalog
        /// or one with no level.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative; the save never holds one.</exception>
        public static bool TryGetLevel(LevelCatalog catalog, int position, out string name)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "A campaign position may not be negative.");
            }

            if (catalog == null || position >= catalog.Names.Count)
            {
                name = null;
                return false;
            }

            name = catalog.Names[position];
            return true;
        }
    }
}
