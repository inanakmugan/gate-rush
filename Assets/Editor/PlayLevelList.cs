using System;
using System.Collections.Generic;
using GateRush.Runtime;

namespace GateRush.Editor
{
    /// <summary>One line of the Play Level window (Module 21).</summary>
    public readonly struct PlayLevelRow
    {
        /// <summary>The level's file name, which is what the game is asked to play.</summary>
        public string Name { get; }

        /// <summary>
        /// The level number the HUD shows for it, or <see cref="PlayLevelList.NoNumber"/>
        /// when it has none: it does not load, or there is no level order.
        /// </summary>
        public int Number { get; }

        /// <summary>Why the level cannot be played; null when it can.</summary>
        public string Error { get; }

        public PlayLevelRow(string name, int number, string error)
        {
            Name = name;
            Number = number;
            Error = error;
        }

        /// <summary>True when the level loads, so clicking it plays it.</summary>
        public bool IsPlayable => Error == null;

        /// <summary>The line's text: "7 — level-6", or the bare file name without a number.</summary>
        public string Label => Number == PlayLevelList.NoNumber ? Name : $"{Number} — {Name}";
    }

    /// <summary>
    /// What the Play Level window lists (Module 21), decided from the same
    /// <see cref="LevelRoster"/> the game reads its levels by: the levels that
    /// load, in the catalog's order with the catalog's numbers, then the files
    /// that do not, each with its error.
    /// </summary>
    public sealed class PlayLevelList
    {
        /// <summary>The <see cref="PlayLevelRow.Number"/> of a level with no number. Level numbers start at 1.</summary>
        public const int NoNumber = 0;

        private PlayLevelList(IReadOnlyList<PlayLevelRow> rows, string orderError)
        {
            Rows = rows;
            OrderError = orderError;
        }

        /// <summary>
        /// Playable levels first — in catalog order, or by name when there is
        /// no catalog — then the files left out, by name.
        /// </summary>
        public IReadOnlyList<PlayLevelRow> Rows { get; }

        /// <summary>
        /// Why the levels have no order and so no numbers — two files share a
        /// level id — or null when they have one. The levels still play.
        /// </summary>
        public string OrderError { get; }

        /// <summary>The list for <paramref name="roster"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="roster"/> is null.</exception>
        public static PlayLevelList Build(LevelRoster roster)
        {
            if (roster == null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            var rows = new List<PlayLevelRow>();
            var catalog = roster.Catalog;
            if (catalog != null)
            {
                var names = catalog.Names;
                for (var i = 0; i < names.Count; i++)
                {
                    // The number is asked of the catalog, as the HUD asks it,
                    // rather than taken from the position here.
                    var number = catalog.TryGetNumber(names[i], out var found) ? found : NoNumber;
                    rows.Add(new PlayLevelRow(names[i], number, null));
                }
            }
            else
            {
                var loaded = new List<string>(roster.Loaded);
                loaded.Sort(StringComparer.Ordinal);
                foreach (var name in loaded)
                {
                    rows.Add(new PlayLevelRow(name, NoNumber, null));
                }
            }

            var failed = new List<LevelRosterFailure>(roster.Failed);
            failed.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var failure in failed)
            {
                rows.Add(new PlayLevelRow(failure.Name, NoNumber, failure.Error));
            }

            return new PlayLevelList(rows.AsReadOnly(), roster.CatalogError);
        }
    }
}
