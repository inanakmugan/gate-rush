using System;
using System.Collections.Generic;
using GateRush.Meta;

namespace GateRush.Runtime
{
    /// <summary>One level as the level select shows it (Module 23).</summary>
    /// <remarks>
    /// There is no "locked" here, on purpose: every level in the select is
    /// playable whatever is completed (D50).
    /// </remarks>
    public readonly struct MenuLevelEntry
    {
        /// <summary>The level's file name, which starts it.</summary>
        public string Name { get; }

        /// <summary>The number the player sees: the level's 1-based position in the level order.</summary>
        public int Number { get; }

        /// <summary>True when the level has been won; it carries a mark.</summary>
        public bool IsCompleted { get; }

        public MenuLevelEntry(string name, int number, bool isCompleted)
        {
            Name = name;
            Number = number;
            IsCompleted = isCompleted;
        }
    }

    /// <summary>
    /// The level select's rule (Module 23), as plain code: what it lists.
    /// Which level Play starts is the campaign's (<see cref="CampaignLevels"/>,
    /// Module 32).
    /// </summary>
    public static class MenuLevels
    {
        /// <summary>
        /// The level select's entries: every level of
        /// <paramref name="catalog"/>, in its order, with the number
        /// <see cref="LevelCatalog.TryGetNumber"/> gives it and whether it is
        /// in <paramref name="completed"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="completed"/> is null.</exception>
        public static IReadOnlyList<MenuLevelEntry> Entries(LevelCatalog catalog, CompletedLevels completed)
        {
            Require(catalog, completed);

            // The name at index i is the level numbered i + 1 (LevelCatalog.Names).
            var names = catalog.Names;
            var ids = catalog.Ids;
            var entries = new List<MenuLevelEntry>(names.Count);
            for (var i = 0; i < names.Count; i++)
            {
                entries.Add(new MenuLevelEntry(names[i], i + 1, completed.IsCompleted(ids[i])));
            }

            return entries.AsReadOnly();
        }

        private static void Require(LevelCatalog catalog, CompletedLevels completed)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }
        }
    }
}
