using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// Whether a won level is the last one (Modules 18 and 23): after a
    /// free-play win, the result panel shows the all-done title and the
    /// closing message under it exactly then, and the win title otherwise.
    /// A campaign win decides it from the campaign's position instead
    /// (Module 32).
    /// </summary>
    /// <remarks>
    /// "The last level" is decided by the same catalog query that decides
    /// whether Next shows after a free-play win —
    /// <see cref="LevelCatalog.TryGetNext"/> — so the panel can never show
    /// Next together with the all-done title, nor neither. With no catalog, or
    /// a level outside it, nothing says the player has done every level.
    /// </remarks>
    public static class ResultTitle
    {
        /// <summary>
        /// True exactly when <paramref name="catalog"/> holds the level
        /// <paramref name="levelName"/> with id <paramref name="levelId"/> and
        /// has no next level after it. The one query behind the all-done
        /// title and the closing message under it (Module 23), so the two
        /// always show together.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="levelName"/> is null.</exception>
        public static bool IsLastLevel(LevelCatalog catalog, string levelName, int levelId)
        {
            if (levelName == null)
            {
                throw new ArgumentNullException(nameof(levelName));
            }

            return catalog != null
                   && catalog.TryGetNumber(levelName, out _)
                   && !catalog.TryGetNext(levelId, out _);
        }
    }
}
