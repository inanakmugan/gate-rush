using System.Collections.Generic;
using System.Globalization;

namespace GateRush.Meta
{
    /// <summary>
    /// The set of levels the player has won, by level id (Module 23). The only
    /// progress kept before Phase 4, whose save model takes it over. It knows
    /// nothing of the level order: whether a level exists is the catalog's
    /// question.
    /// </summary>
    /// <remarks>
    /// <para><b>Data.</b> <see cref="ToData"/> writes the ids in ascending
    /// order, separated by <see cref="Separator"/>, in the invariant culture:
    /// <c>0,1,3</c>. No level completed is the empty string.</para>
    /// <para><b>Unreadable data reads as none.</b> Saved data comes back from
    /// outside the game and may be anything; reading it never throws.
    /// <see cref="TryFromData"/> says whether it could be read, so the caller
    /// — this assembly does not log — can report it.</para>
    /// </remarks>
    public sealed class CompletedLevels
    {
        /// <summary>The key the set is saved under in an <c>ISaveStore</c>.</summary>
        public const string SaveKey = "GateRush.CompletedLevels";

        private const char Separator = ',';

        private readonly HashSet<int> ids = new HashSet<int>();

        /// <summary>How many levels are completed.</summary>
        public int Count => ids.Count;

        /// <summary>True when the level with id <paramref name="levelId"/> has been won.</summary>
        public bool IsCompleted(int levelId) => ids.Contains(levelId);

        /// <summary>
        /// Records the level with id <paramref name="levelId"/> as won. True
        /// when it was not completed before — the moment to save; false when
        /// it already was, and nothing changed.
        /// </summary>
        public bool MarkCompleted(int levelId) => ids.Add(levelId);

        /// <summary>The set as text, for <see cref="FromData"/> to read back.</summary>
        public string ToData()
        {
            var sorted = new List<int>(ids);
            sorted.Sort();

            var parts = new string[sorted.Count];
            for (var i = 0; i < sorted.Count; i++)
            {
                parts[i] = sorted[i].ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(Separator.ToString(), parts);
        }

        /// <summary>
        /// The set <paramref name="data"/> describes. Data that cannot be read
        /// — anything <see cref="ToData"/> would not have written — gives an
        /// empty set; this never throws.
        /// </summary>
        public static CompletedLevels FromData(string data)
        {
            TryFromData(data, out var levels);
            return levels;
        }

        /// <summary>
        /// Reads <paramref name="data"/> into <paramref name="levels"/>, which
        /// is never null. False when the data cannot be read, and
        /// <paramref name="levels"/> is then empty: no part of unreadable data
        /// is trusted. Null and empty data are readable and mean no level
        /// completed.
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
