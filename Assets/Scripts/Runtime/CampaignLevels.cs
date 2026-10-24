using System;
using GateRush.Meta;

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

        /// <summary>
        /// What starting the campaign level does for <paramref name="meta"/>'s
        /// position in <paramref name="catalog"/>'s order: past the last level
        /// the campaign is over, whatever the lives — checked first, so a
        /// finished campaign never asks for a life; with no life left, nothing
        /// starts (D57); otherwise the level <paramref name="name"/> starts.
        /// Reads the lives at the meta game's clock and changes nothing.
        /// <paramref name="name"/> is null unless the answer is
        /// <see cref="CampaignStart.Level"/>. With no usable level order the
        /// answer is <see cref="CampaignStart.Over"/>, as
        /// <see cref="TryGetLevel"/> has no level; Play falls back before
        /// asking.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="meta"/> is null.</exception>
        public static CampaignStart ChooseStart(LevelCatalog catalog, MetaGame meta, out string name)
        {
            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            if (!TryGetLevel(catalog, meta.CampaignPosition, out var levelName))
            {
                name = null;
                return CampaignStart.Over;
            }

            if (!meta.CanStartCampaignLevel())
            {
                name = null;
                return CampaignStart.NoLife;
            }

            name = levelName;
            return CampaignStart.Level;
        }
    }
}
