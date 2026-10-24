using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// <i>Gate Rush → Clean Up Unused Third-Party Art</i> (7.8): deletes the
    /// third-party files the game no longer shows — Animal Pack Redux, Board
    /// Game Icons, the game-icons.net icons, the unused Kenney Game Icons and
    /// UI Pack sprites, and Fredoka SemiBold — and the folders they leave
    /// empty. Only files on <see cref="ApprovedDeletions"/>, the list the
    /// owner approved, are ever deleted.
    /// </summary>
    /// <remarks>
    /// <para>Before deleting, it checks again that nothing uses each file:
    /// <see cref="AssetDatabase.GetDependencies(string[], bool)"/> over every
    /// scene, prefab and asset in the project, and a search of every C#
    /// source for the file's path as written. A file that is still used is
    /// refused, named with what uses it, and kept. Deleting goes through
    /// <see cref="AssetDatabase.DeleteAsset"/>, so Unity removes each
    /// <c>.meta</c> with its file and no GUID is left behind.</para>
    /// <para>Idempotent: a file already gone is counted and skipped, so a
    /// second run deletes nothing. A pack's licence is on the list only when
    /// every file of that pack is.</para>
    /// <para>The code search finds a path only where it is written out in
    /// full. The list is therefore built from folders and file names here, so
    /// this source never names an approved path in full and never refuses
    /// itself.</para>
    /// </remarks>
    public static class ThirdPartyCleanup
    {
        /// <summary>The folder every third-party pack lives under; nothing outside it is deleted.</summary>
        public const string Root = "Assets/ThirdParty";

        private const string KenneyFolder = Root + "/Kenney";
        private const string AnimalPackFolder = KenneyFolder + "/AnimalPackRedux";
        private const string AnimalPackRoundFolder = AnimalPackFolder + "/Round";
        private const string BoardGameIconsFolder = KenneyFolder + "/BoardGameIcons";
        private const string GameIconsFolder = KenneyFolder + "/GameIcons";
        private const string UiPackFolder = KenneyFolder + "/UIPack";
        private const string GameIconsNetFolder = Root + "/GameIconsNet";
        private const string FredokaFolder = Root + "/Fonts/Fredoka";

        // The licence file name Kenney's packs and game-icons.net share.
        private const string PackLicence = "License.txt";
        private const string PngExtension = ".png";
        private const string SourceFilter = "*.cs";
        private const string MetaExtension = ".meta";
        private const char Separator = '/';
        private const string AssetsFolder = "Assets";
        private const string LogPrefix = "Clean Up Unused Third-Party Art";

        private static readonly string[] Animals =
        {
            "bear", "buffalo", "chick", "chicken", "cow", "crocodile", "dog", "duck", "elephant", "frog",
            "giraffe", "goat", "gorilla", "hippo", "horse", "monkey", "moose", "narwhal", "owl", "panda",
            "parrot", "penguin", "pig", "rabbit", "rhino", "sloth", "snake", "walrus", "whale", "zebra",
        };

        private static readonly string[] BoardGameIcons =
        {
            "arrow_horizontal", "award", "crown_a", "exploding", "fire", "hourglass", "pouch", "suit_hearts",
            "timer_100", "token", "tokens_stack",
        };

        private static readonly string[] GameIconsNetIcons = { "cog", "hourglass", "magic-broom", "rocket" };

        // Kenney Game Icons the screens no longer use; plus, cross, home,
        // return, audioOn, audioOff, phone and checkmark stay.
        private static readonly string[] UnusedGameIcons =
        {
            "gear", "information", "leaderboardsComplex", "locked", "medal1", "shoppingCart", "singleplayer", "star",
            "trophy",
        };

        // The sixteen sprites every UI Pack colour folder holds.
        private static readonly string[] UiPackColourSprites =
        {
            "button_rectangle_depth_flat", "button_rectangle_depth_gradient", "button_rectangle_flat",
            "button_round_depth_flat", "button_round_depth_gradient", "button_square_depth_flat",
            "button_square_depth_gradient", "button_square_flat", "check_square_color", "check_square_grey",
            "icon_checkmark", "icon_cross", "slide_horizontal_color", "slide_horizontal_grey", "star", "star_outline",
        };

        // Wholly unused colours; Green keeps its checkmark (Streak Marker).
        private static readonly string[] UnusedUiPackColours = { "Blue", "Grey", "Red", "Yellow" };
        private const string UiPackGreen = "Green";
        private const string KeptGreenSprite = "icon_checkmark";

        private const string UiPackExtra = "Extra";

        private static readonly string[] UiPackExtraSprites =
        {
            "divider", "icon_arrow_down_light", "icon_arrow_up_light", "icon_play_light", "icon_repeat_light",
            "input_outline_rectangle", "input_rectangle",
        };

        private const string FredokaSemiBold = "Fredoka-SemiBold.ttf";

        /// <summary>
        /// The files the owner approved for deletion, every one under
        /// <see cref="Root"/>: 144, from the reference check of 7.8.
        /// </summary>
        /// <remarks>Declared after the name arrays it is built from, since static initializers run in the order written.</remarks>
        public static IReadOnlyList<string> ApprovedDeletions { get; } = Array.AsReadOnly(BuildApprovedDeletions());

        /// <summary>
        /// The pack folders the list removes entirely, which no asset may
        /// depend on once the command has run; their files are all on
        /// <see cref="ApprovedDeletions"/>.
        /// </summary>
        public static IReadOnlyList<string> RemovedFolders { get; } = Array.AsReadOnly(BuildRemovedFolders());

        /// <summary>An approved file kept because something still uses it.</summary>
        public readonly struct Refusal
        {
            /// <summary>Creates a refusal of <paramref name="path"/>.</summary>
            public Refusal(string path, bool isUsedByAsset, string source)
            {
                Path = path;
                IsUsedByAsset = isUsedByAsset;
                Source = source;
            }

            /// <summary>The refused file.</summary>
            public string Path { get; }

            /// <summary>Whether an asset depends on it; otherwise <see cref="Source"/> names it.</summary>
            public bool IsUsedByAsset { get; }

            /// <summary>The C# source that writes the path out; null when an asset uses it.</summary>
            public string Source { get; }
        }

        /// <summary>
        /// What the command does with each approved file: deletes it, refuses
        /// it because something still uses it, or skips it because it is
        /// already gone.
        /// </summary>
        public sealed class Plan
        {
            /// <summary>Creates a plan from its three disjoint lists.</summary>
            public Plan(IReadOnlyList<string> toDelete, IReadOnlyList<Refusal> refused, IReadOnlyList<string> alreadyGone)
            {
                ToDelete = toDelete;
                Refused = refused;
                AlreadyGone = alreadyGone;
            }

            /// <summary>Approved files that exist and that nothing uses, in list order.</summary>
            public IReadOnlyList<string> ToDelete { get; }

            /// <summary>Approved files that exist but are still used.</summary>
            public IReadOnlyList<Refusal> Refused { get; }

            /// <summary>Approved files no longer in the project.</summary>
            public IReadOnlyList<string> AlreadyGone { get; }
        }

        /// <summary>
        /// Sorts <paramref name="approved"/> into the three outcomes. A file
        /// is refused when <paramref name="referencedByAssets"/> holds it, or
        /// when a source in <paramref name="sources"/> (path, text) writes its
        /// path out; <paramref name="exists"/> says whether it is still in the
        /// project. Changes nothing.
        /// </summary>
        public static Plan PlanDeletions(
            IReadOnlyList<string> approved, ICollection<string> referencedByAssets,
            IReadOnlyList<KeyValuePair<string, string>> sources, Func<string, bool> exists)
        {
            var toDelete = new List<string>();
            var refused = new List<Refusal>();
            var alreadyGone = new List<string>();
            foreach (var path in approved)
            {
                if (!exists(path))
                {
                    alreadyGone.Add(path);
                    continue;
                }

                if (referencedByAssets.Contains(path))
                {
                    refused.Add(new Refusal(path, true, null));
                    continue;
                }

                var namedIn = SourceNaming(path, sources);
                if (namedIn != null)
                {
                    refused.Add(new Refusal(path, false, namedIn));
                    continue;
                }

                toDelete.Add(path);
            }

            return new Plan(toDelete.AsReadOnly(), refused.AsReadOnly(), alreadyGone.AsReadOnly());
        }

        /// <summary>
        /// The folders strictly under <paramref name="root"/> that
        /// <paramref name="deleted"/> leaves with nothing in them, judged by
        /// <paramref name="remaining"/>, every asset and folder path still in
        /// the project; deepest first, so each can be deleted in turn. A
        /// folder emptied only by an empty child counts as empty.
        /// </summary>
        public static IReadOnlyList<string> FoldersLeftEmpty(IEnumerable<string> deleted, IEnumerable<string> remaining, string root)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            var rootPrefix = root + Separator;
            foreach (var path in deleted)
            {
                var folder = ParentOf(path);
                while (folder != null && folder.StartsWith(rootPrefix, StringComparison.Ordinal))
                {
                    candidates.Add(folder);
                    folder = ParentOf(folder);
                }
            }

            var ordered = new List<string>(candidates);
            ordered.Sort((a, b) =>
            {
                var depth = Depth(b).CompareTo(Depth(a));
                return depth != 0 ? depth : string.CompareOrdinal(a, b);
            });

            var left = new HashSet<string>(remaining, StringComparer.Ordinal);
            var empty = new List<string>();
            foreach (var folder in ordered)
            {
                if (HoldsAnything(folder, left))
                {
                    continue;
                }

                empty.Add(folder);
                left.Remove(folder);
            }

            return empty.AsReadOnly();
        }

        /// <summary>
        /// Re-checks every approved file, deletes those nothing uses, then the
        /// folders left empty, and logs each one.
        /// </summary>
        [MenuItem("Gate Rush/Clean Up Unused Third-Party Art")]
        public static void CleanUpFromMenu()
        {
            var approved = new HashSet<string>(ApprovedDeletions, StringComparer.Ordinal);
            var roots = new List<string>();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (path.StartsWith(AssetsFolder + Separator, StringComparison.Ordinal) && !approved.Contains(path)
                    && !AssetDatabase.IsValidFolder(path))
                {
                    roots.Add(path);
                }
            }

            var referenced = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true), StringComparer.Ordinal);
            var plan = PlanDeletions(ApprovedDeletions, referenced, ReadSources(), File.Exists);

            foreach (var refusal in plan.Refused)
            {
                var user = refusal.IsUsedByAsset ? $"used by {UsersOf(refusal.Path, roots)}" : $"named in {refusal.Source}";
                Debug.LogError($"{LogPrefix}: kept {refusal.Path}: it is still {user}.");
            }

            var deleted = new List<string>();
            foreach (var path in plan.ToDelete)
            {
                if (AssetDatabase.DeleteAsset(path))
                {
                    deleted.Add(path);
                    Debug.Log($"{LogPrefix}: deleted {path}.");
                }
                else
                {
                    Debug.LogError($"{LogPrefix}: could not delete {path}.");
                }
            }

            var folders = FoldersLeftEmpty(deleted, AssetDatabase.GetAllAssetPaths(), Root);
            var deletedFolders = 0;
            foreach (var folder in folders)
            {
                if (HasFilesOnDisk(folder))
                {
                    Debug.LogWarning($"{LogPrefix}: kept the folder {folder}: it still holds files Unity does not import.");
                    continue;
                }

                if (AssetDatabase.DeleteAsset(folder))
                {
                    deletedFolders++;
                    Debug.Log($"{LogPrefix}: deleted the empty folder {folder}.");
                }
                else
                {
                    Debug.LogError($"{LogPrefix}: could not delete the folder {folder}.");
                }
            }

            var summary = $"{LogPrefix}: {deleted.Count} files and {deletedFolders} folders deleted, "
                + $"{plan.AlreadyGone.Count} already gone, {plan.Refused.Count} kept because they are still used.";
            if (plan.Refused.Count > 0)
            {
                Debug.LogError(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        private static string[] BuildApprovedDeletions()
        {
            var paths = new List<string>();
            AddAll(paths, AnimalPackRoundFolder, Animals, PngExtension);
            paths.Add($"{AnimalPackFolder}/{PackLicence}");
            AddAll(paths, BoardGameIconsFolder, BoardGameIcons, PngExtension);
            paths.Add($"{BoardGameIconsFolder}/{PackLicence}");
            AddAll(paths, GameIconsNetFolder, GameIconsNetIcons, PngExtension);
            paths.Add($"{GameIconsNetFolder}/{PackLicence}");
            AddAll(paths, GameIconsFolder, UnusedGameIcons, PngExtension);
            foreach (var colour in UnusedUiPackColours)
            {
                AddAll(paths, $"{UiPackFolder}/{colour}", UiPackColourSprites, PngExtension);
            }

            foreach (var sprite in UiPackColourSprites)
            {
                if (!string.Equals(sprite, KeptGreenSprite, StringComparison.Ordinal))
                {
                    paths.Add($"{UiPackFolder}/{UiPackGreen}/{sprite}{PngExtension}");
                }
            }

            AddAll(paths, $"{UiPackFolder}/{UiPackExtra}", UiPackExtraSprites, PngExtension);
            paths.Add($"{FredokaFolder}/{FredokaSemiBold}");
            return paths.ToArray();
        }

        private static string[] BuildRemovedFolders()
        {
            var folders = new List<string> { AnimalPackFolder, BoardGameIconsFolder, GameIconsNetFolder, $"{UiPackFolder}/{UiPackExtra}" };
            foreach (var colour in UnusedUiPackColours)
            {
                folders.Add($"{UiPackFolder}/{colour}");
            }

            return folders.ToArray();
        }

        private static void AddAll(List<string> paths, string folder, IEnumerable<string> names, string extension)
        {
            foreach (var name in names)
            {
                paths.Add($"{folder}/{name}{extension}");
            }
        }

        /// <summary>The first source that writes <paramref name="path"/> out, or null.</summary>
        private static string SourceNaming(string path, IReadOnlyList<KeyValuePair<string, string>> sources)
        {
            foreach (var source in sources)
            {
                if (source.Value.IndexOf(path, StringComparison.Ordinal) >= 0)
                {
                    return source.Key;
                }
            }

            return null;
        }

        /// <summary>Every C# source under Assets, as (path, text).</summary>
        private static List<KeyValuePair<string, string>> ReadSources()
        {
            var sources = new List<KeyValuePair<string, string>>();
            foreach (var file in Directory.GetFiles(AssetsFolder, SourceFilter, SearchOption.AllDirectories))
            {
                sources.Add(new KeyValuePair<string, string>(file.Replace('\\', Separator), File.ReadAllText(file)));
            }

            return sources;
        }

        /// <summary>
        /// The assets that use <paramref name="path"/> directly, for the log.
        /// Runs only for a refusal, one dependency query per asset.
        /// </summary>
        private static string UsersOf(string path, List<string> roots)
        {
            var users = new List<string>();
            foreach (var root in roots)
            {
                if (Array.IndexOf(AssetDatabase.GetDependencies(root, false), path) >= 0)
                {
                    users.Add(root);
                }
            }

            return users.Count == 0 ? "an asset, through another" : string.Join(", ", users);
        }

        private static bool HasFilesOnDisk(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return false;
            }

            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(MetaExtension, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HoldsAnything(string folder, HashSet<string> paths)
        {
            var prefix = folder + Separator;
            foreach (var path in paths)
            {
                if (path.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ParentOf(string path)
        {
            var separator = path.LastIndexOf(Separator);
            return separator <= 0 ? null : path.Substring(0, separator);
        }

        private static int Depth(string path)
        {
            var depth = 0;
            foreach (var character in path)
            {
                if (character == Separator)
                {
                    depth++;
                }
            }

            return depth;
        }
    }
}
