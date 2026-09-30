using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// The result panel's title after a win (Module 18): the all-done title
    /// after the last level, the win title after any other.
    /// </summary>
    /// <remarks>
    /// "The last level" is decided by the same catalog query that decides
    /// whether Next shows — <see cref="LevelCatalog.TryGetNext"/> — so the panel
    /// can never show Next together with the all-done title, nor neither.
    /// </remarks>
    public static class ResultTitle
    {
        /// <summary>
        /// <paramref name="allDoneTitle"/> exactly when <paramref name="catalog"/>
        /// holds the level <paramref name="levelName"/> with id
        /// <paramref name="levelId"/> and has no next level after it; otherwise
        /// <paramref name="winTitle"/>. With no catalog, or a level outside it,
        /// nothing says the player has done every level.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="levelName"/> is null.</exception>
        public static string ForWin(
            LevelCatalog catalog, string levelName, int levelId, string winTitle, string allDoneTitle)
        {
            if (levelName == null)
            {
                throw new ArgumentNullException(nameof(levelName));
            }

            var isLast = catalog != null
                         && catalog.TryGetNumber(levelName, out _)
                         && !catalog.TryGetNext(levelId, out _);
            return isLast ? allDoneTitle : winTitle;
        }
    }
}
