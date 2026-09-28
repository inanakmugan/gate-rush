using System;
using System.Collections.Generic;

namespace GateRush.Runtime
{
    /// <summary>
    /// The playable levels by <c>levelId</c>, answering one question: which
    /// level comes after this one. The order is by id, not by file name, and
    /// ids need not be contiguous.
    /// </summary>
    public sealed class LevelCatalog
    {
        private readonly SortedList<int, string> namesById = new SortedList<int, string>();

        /// <summary>A catalog of <paramref name="levels"/>, each a file name and the level id inside it.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="levels"/> or one of its names is null.</exception>
        /// <exception cref="ArgumentException">
        /// Two levels share an id — a level data error; the message names both
        /// files.
        /// </exception>
        public LevelCatalog(IEnumerable<(string name, int levelId)> levels)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            foreach (var (name, levelId) in levels)
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(levels), $"The level with id {levelId} has no name.");
                }

                if (namesById.TryGetValue(levelId, out var existing))
                {
                    throw new ArgumentException(
                        $"Levels '{existing}' and '{name}' both have levelId {levelId}.", nameof(levels));
                }

                namesById.Add(levelId, name);
            }
        }

        /// <summary>
        /// The name of the level with the smallest id greater than
        /// <paramref name="levelId"/>; false after the last level.
        /// <paramref name="levelId"/> need not be in the catalog itself.
        /// </summary>
        public bool TryGetNext(int levelId, out string name)
        {
            var ids = namesById.Keys;
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] > levelId)
                {
                    name = namesById.Values[i];
                    return true;
                }
            }

            name = null;
            return false;
        }
    }
}
