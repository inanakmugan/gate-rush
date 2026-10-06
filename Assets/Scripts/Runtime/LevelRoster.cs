using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Serialization;

namespace GateRush.Runtime
{
    /// <summary>
    /// A level file <see cref="LevelRoster.Read"/> left out, and why.
    /// </summary>
    public readonly struct LevelRosterFailure
    {
        /// <summary>The file's name.</summary>
        public string Name { get; }

        /// <summary>Why the file is left out, as a sentence.</summary>
        public string Error { get; }

        /// <summary>
        /// True when the file itself may be sound but an earlier file has its
        /// name; false when the file failed to load as a level.
        /// </summary>
        public bool IsDuplicateName { get; }

        public LevelRosterFailure(string name, string error, bool isDuplicateName)
        {
            Name = name;
            Error = error;
            IsDuplicateName = isDuplicateName;
        }
    }

    /// <summary>
    /// What the level files say, read in one pass: which load, which do not
    /// and why, the level order, and what each level in that order contains.
    /// The one reading both the game (<see cref="LevelBootstrap"/>) and the
    /// Play Level window (Module 21) go by, so the window cannot list a level
    /// the game would leave out, nor number one differently from the HUD.
    /// </summary>
    /// <remarks>
    /// A file that fails to load is left out: it has no place in the order and
    /// introduces nothing, so a later level introduces what it would have. Of
    /// two files with one name the first is kept. Two files sharing a level id
    /// leave no order at all — <see cref="Catalog"/> is null and
    /// <see cref="CatalogError"/> says why — though each still loads.
    /// </remarks>
    public sealed class LevelRoster
    {
        private static readonly IReadOnlyList<IReadOnlyCollection<LevelMechanic>> NoMechanics =
            Array.Empty<IReadOnlyCollection<LevelMechanic>>();

        private LevelRoster(
            IReadOnlyList<string> loaded,
            IReadOnlyList<LevelRosterFailure> failed,
            LevelCatalog catalog,
            string catalogError,
            IReadOnlyList<IReadOnlyCollection<LevelMechanic>> mechanicsInOrder)
        {
            Loaded = loaded;
            Failed = failed;
            Catalog = catalog;
            CatalogError = catalogError;
            MechanicsInOrder = mechanicsInOrder;
        }

        /// <summary>The name of every file that loads, in the order the files were given.</summary>
        public IReadOnlyList<string> Loaded { get; }

        /// <summary>Every file left out, in the order the files were given.</summary>
        public IReadOnlyList<LevelRosterFailure> Failed { get; }

        /// <summary>The level order over <see cref="Loaded"/>, or null when two of them share a level id.</summary>
        public LevelCatalog Catalog { get; }

        /// <summary>Why there is no <see cref="Catalog"/>, naming both files; null when there is one.</summary>
        public string CatalogError { get; }

        /// <summary>
        /// What each level contains (<see cref="LevelMechanics.Of"/>), in the
        /// catalog's order: entry <c>i</c> belongs to the level the catalog
        /// numbers <c>i + 1</c>. Empty while there is no catalog.
        /// </summary>
        public IReadOnlyList<IReadOnlyCollection<LevelMechanic>> MechanicsInOrder { get; }

        /// <summary>
        /// Reads <paramref name="files"/>, each a level file's name and its
        /// JSON text.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="files"/> or one of its names is null.</exception>
        public static LevelRoster Read(IEnumerable<(string name, string json)> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            var loaded = new List<string>();
            var failed = new List<LevelRosterFailure>();
            var entries = new List<(string name, int levelId)>();
            var mechanicsByName = new Dictionary<string, IReadOnlyCollection<LevelMechanic>>();

            foreach (var (name, json) in files)
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(files), "A level file has no name.");
                }

                if (mechanicsByName.ContainsKey(name))
                {
                    failed.Add(new LevelRosterFailure(name, $"An earlier level file is also named '{name}'.", true));
                    continue;
                }

                if (!TryParse(json, name, out var ctx, out var error))
                {
                    failed.Add(new LevelRosterFailure(name, error, false));
                    continue;
                }

                loaded.Add(name);
                entries.Add((name, ctx.LevelId));
                mechanicsByName.Add(name, LevelMechanics.Of(ctx));
            }

            LevelCatalog catalog;
            try
            {
                catalog = new LevelCatalog(entries);
            }
            catch (ArgumentException e)
            {
                return new LevelRoster(loaded.AsReadOnly(), failed.AsReadOnly(), null, e.Message, NoMechanics);
            }

            // Every name in the catalog came from entries, each added together
            // with its mechanics, so the lookup cannot miss.
            var names = catalog.Names;
            var mechanicsInOrder = new List<IReadOnlyCollection<LevelMechanic>>(names.Count);
            for (var i = 0; i < names.Count; i++)
            {
                mechanicsInOrder.Add(mechanicsByName[names[i]]);
            }

            return new LevelRoster(loaded.AsReadOnly(), failed.AsReadOnly(), catalog, null, mechanicsInOrder.AsReadOnly());
        }

        /// <summary>
        /// The one rule for "this text loads as a level": it parses and
        /// describes a valid level. On failure <paramref name="error"/> is the
        /// serializer's or <c>Core</c>'s own message.
        /// </summary>
        /// <param name="sourceName">The file's name, for the error message.</param>
        public static bool TryParse(string json, string sourceName, out LevelContext ctx, out string error)
        {
            try
            {
                ctx = LevelSerializer.FromJson(json, sourceName);
                error = null;
                return true;
            }
            catch (Exception e) when (e is LevelSerializationException || e is ArgumentException)
            {
                ctx = null;
                error = e.Message;
                return false;
            }
        }
    }
}
