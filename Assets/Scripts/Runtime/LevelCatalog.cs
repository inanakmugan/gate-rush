using System;
using System.Collections.Generic;

namespace GateRush.Runtime
{
    /// <summary>
    /// The playable levels by <c>levelId</c>, answering three questions: which
    /// level comes after or before this one, what number the player sees for
    /// it, and in what order the levels are played. The order is by id, not by file name,
    /// and ids need not be contiguous.
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

            Names = new List<string>(namesById.Values).AsReadOnly();
        }

        /// <summary>
        /// Every level's file name in id order: the name at index <c>i</c> is
        /// the level <see cref="TryGetNumber"/> numbers <c>i + 1</c>.
        /// </summary>
        public IReadOnlyList<string> Names { get; }

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

        /// <summary>
        /// The name of the level with the largest id smaller than
        /// <paramref name="levelId"/>; false before the first level.
        /// <paramref name="levelId"/> need not be in the catalog itself.
        /// </summary>
        public bool TryGetPrevious(int levelId, out string name)
        {
            var ids = namesById.Keys;
            for (var i = ids.Count - 1; i >= 0; i--)
            {
                if (ids[i] < levelId)
                {
                    name = namesById.Values[i];
                    return true;
                }
            }

            name = null;
            return false;
        }

        /// <summary>
        /// The level number of the file <paramref name="name"/>: its 1-based
        /// position in id order, so with ids 0, 1, 3 and 4 the level with id 3
        /// is number 3. False for a name the catalog does not hold — not an
        /// error, since the level being played need not be one of the
        /// catalog's files.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
        public bool TryGetNumber(string name, out int number)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var names = namesById.Values;
            for (var i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                {
                    number = i + 1;
                    return true;
                }
            }

            number = 0;
            return false;
        }
    }
}
