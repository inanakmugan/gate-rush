using System;
using System.Collections.Generic;
using System.Globalization;

namespace GateRush.Meta
{
    /// <summary>
    /// The set of levels the player has won, by level id (Module 23), kept as
    /// a section of <see cref="SaveData"/> (Module 29). It knows nothing of the
    /// level order: whether a level exists is the catalog's question.
    /// </summary>
    /// <remarks>
    /// <para><b>Legacy data.</b> Before Module 29 the set was saved on its own
    /// under <see cref="LegacySaveKey"/>: the ids separated by
    /// <see cref="Separator"/>, in the invariant culture, such as
    /// <c>0,1,3</c>, with the empty string for no level completed. That format
    /// is now only read, by <see cref="SaveRepository"/> when it carries an
    /// old save over; nothing writes it any more.</para>
    /// <para><b>Unreadable legacy data reads as none.</b> Saved data comes back
    /// from outside the game and may be anything; reading it never throws.
    /// <see cref="TryFromData"/> says whether it could be read, so the caller
    /// — this assembly does not log — can report it.</para>
    /// </remarks>
    public sealed class CompletedLevels
    {
        /// <summary>
        /// The key the set was saved under on its own before Module 29. Read
        /// only while no save exists under <see cref="SaveRepository.SaveKey"/>.
        /// </summary>
        public const string LegacySaveKey = "GateRush.CompletedLevels";

        private const char Separator = ',';

        private readonly HashSet<int> ids = new HashSet<int>();

        /// <summary>How many levels are completed.</summary>
        public int Count => ids.Count;

        /// <summary>
        /// The completed level ids in ascending order, so that the same set
        /// always reads, and saves, the same. A new list on each read.
        /// </summary>
        public IReadOnlyList<int> Ids
        {
            get
            {
                var sorted = new List<int>(ids);
                sorted.Sort();
                return sorted;
            }
        }

        /// <summary>True when the level with id <paramref name="levelId"/> has been won.</summary>
        public bool IsCompleted(int levelId) => ids.Contains(levelId);

        /// <summary>
        /// Records the level with id <paramref name="levelId"/> as won. True
        /// when it was not completed before — the moment to save; false when
        /// it already was, and nothing changed.
        /// </summary>
        public bool MarkCompleted(int levelId) => ids.Add(levelId);

        /// <summary>
        /// The set holding <paramref name="levelIds"/>, given in any order;
        /// an id given twice is completed once.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="levelIds"/> is null.</exception>
        public static CompletedLevels Of(IEnumerable<int> levelIds)
        {
            if (levelIds == null)
            {
                throw new ArgumentNullException(nameof(levelIds));
            }

            var levels = new CompletedLevels();
            foreach (var levelId in levelIds)
            {
                levels.ids.Add(levelId);
            }

            return levels;
        }

        /// <summary>
        /// The set the legacy <paramref name="data"/> describes. Data that
        /// cannot be read — anything not in the legacy format — gives an empty
        /// set; this never throws.
        /// </summary>
        public static CompletedLevels FromData(string data)
        {
            TryFromData(data, out var levels);
            return levels;
        }

        /// <summary>
        /// Reads the legacy <paramref name="data"/> into
        /// <paramref name="levels"/>, which is never null. False when the data
        /// cannot be read, and <paramref name="levels"/> is then empty: no part
        /// of unreadable data is trusted. Null and empty data are readable and
        /// mean no level completed.
        /// </summary>
        public static bool TryFromData(string data, out CompletedLevels levels)
        {
            levels = new CompletedLevels();
            if (string.IsNullOrEmpty(data))
            {
                return true;
            }

            var parts = data.Split(Separator);
            var read = new List<int>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var levelId))
                {
                    return false;
                }

                read.Add(levelId);
            }

            for (var i = 0; i < read.Count; i++)
            {
                levels.ids.Add(read[i]);
            }

            return true;
        }
    }
}
