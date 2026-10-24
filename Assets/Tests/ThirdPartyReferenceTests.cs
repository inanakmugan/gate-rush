using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GateRush.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers the project after the 7.8 cleanup, by reading its real assets:
    /// nothing uses a file <see cref="ThirdPartyCleanup"/> deletes or Lilita
    /// One's old place; no sprite or font in the UI prefabs points there; no
    /// prefab, config, scene or the sprite atlas holds a GUID that no longer
    /// resolves; the kept packs keep their licences; and Lilita One sits in
    /// <see cref="ThirdPartyFontMove.TargetFolder"/> with its licence.
    /// </summary>
    /// <remarks>
    /// The dependency tests fail until <i>Gate Rush → Apply Hand-Made Prefab
    /// Settings</i> has removed the Top Bar's cog override; the font tests
    /// until <i>Gate Rush → Move Fonts To Third Party</i> has run; and the
    /// licence test until the owner adds Lilita One's OFL.txt.
    /// </remarks>
    public class ThirdPartyReferenceTests
    {
        private const string UiPrefabFolder = "Assets/Prefabs/UI";
        private const string ConfigFolder = "Assets/Config";
        private const string SceneFolder = "Assets/Scenes";
        private const string SpriteAtlasPath = "Assets/Art/UI.spriteatlasv2";

        // Unity's built-in resources: their GUIDs are not asset files.
        private const string BuiltInGuidPrefix = "0000000000000000";

        private static readonly Regex GuidPattern = new Regex("guid: ([0-9a-f]{32})", RegexOptions.Compiled);

        // The packs that stay, each with a licence and at least one file shown.
        private static readonly string[] KeptLicences =
        {
            "Assets/ThirdParty/Kenney/GameIcons/License.txt",
            "Assets/ThirdParty/Kenney/UIPack/License.txt",
            "Assets/ThirdParty/Fonts/Fredoka/OFL.txt",
            "Assets/ThirdParty/Fonts/Nunito/OFL.txt",
        };

        // The deleted files and Lilita One's old paths, built once per fixture.
        private HashSet<string> removed;

        [OneTimeSetUp]
        public void CollectRemovedPaths()
        {
            removed = new HashSet<string>(ThirdPartyCleanup.ApprovedDeletions, StringComparer.Ordinal);
            foreach (var move in ThirdPartyFontMove.Moves)
            {
                removed.Add(move.From);
            }
        }

        [Test]
        public void ProjectAssets_DependOnNoDeletedFileOrOldFontPath()
        {
            var roots = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) && !removed.Contains(path) && !AssetDatabase.IsValidFolder(path))
                .ToArray();

            var offenders = AssetDatabase.GetDependencies(roots, true).Where(IsInRemovedPlace).ToList();

            Assert.That(roots.Length, Is.GreaterThan(0), "the project's assets were read");
            Assert.IsEmpty(offenders, "files deleted or moved by 7.8 that an asset still uses");
        }

        [Test]
        public void UiPrefabs_SpritesAndFonts_PointNowhereDeletedOrMoved()
        {
            var prefabs = PathsIn(UiPrefabFolder, "t:Prefab");
            var offenders = new List<string>();
            var checkedCount = 0;
            foreach (var prefabPath in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, prefabPath);
                foreach (var image in prefab.GetComponentsInChildren<Image>(true))
                {
                    checkedCount++;
                    AddIfRemoved(offenders, prefabPath, image.name, image.sprite);
                }

                foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    checkedCount++;
                    AddIfRemoved(offenders, prefabPath, text.name, text.font);
                    AddIfRemoved(offenders, prefabPath, text.name, text.fontSharedMaterial);
                }
            }

            Assert.That(prefabs.Count, Is.GreaterThan(0), $"{UiPrefabFolder} holds the built and hand-made prefabs");
            Assert.That(checkedCount, Is.GreaterThan(0), "images and texts were read");
            Assert.IsEmpty(offenders);
        }

        [Test]
        public void PrefabsConfigsScenesAndAtlas_HoldNoGuidThatNoLongerResolves()
        {
            var files = PathsIn(UiPrefabFolder, "t:Prefab")
                .Concat(PathsIn(ConfigFolder, string.Empty).Where(path => path.EndsWith(".asset", StringComparison.Ordinal)))
                .Concat(PathsIn(SceneFolder, "t:Scene"))
                .Append(SpriteAtlasPath)
                .Distinct()
                .ToList();
            var dangling = new List<string>();

            foreach (var file in files)
            {
                foreach (Match match in GuidPattern.Matches(File.ReadAllText(file)))
                {
                    var guid = match.Groups[1].Value;
                    if (guid.StartsWith(BuiltInGuidPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !AssetDatabase.AssetPathExists(path))
                    {
                        dangling.Add($"{file}: {guid}");
                    }
                }
            }

            Assert.That(files, Has.Member(SpriteAtlasPath));
            Assert.That(files.Count, Is.GreaterThan(1), "the prefabs, configs and scenes were read");
            Assert.IsEmpty(dangling.Distinct(), "references to files no longer in the project");
        }

        [Test]
        public void RemovedFolders_HoldOnlyApprovedFiles()
        {
            // Guards the list before a run; once a folder is gone it holds nothing.
            var approved = new HashSet<string>(ThirdPartyCleanup.ApprovedDeletions, StringComparer.Ordinal);
            var unapproved = new List<string>();
            foreach (var folder in ThirdPartyCleanup.RemovedFolders)
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                unapproved.AddRange(Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                    .Select(file => file.Replace('\\', '/'))
                    .Where(file => !file.EndsWith(".meta", StringComparison.Ordinal) && !approved.Contains(file)));
            }

            Assert.IsEmpty(unapproved, "a removed folder would take these unapproved files with it");
        }

        [TestCaseSource(nameof(KeptLicences))]
        public void KeptPack_KeepsItsLicenceBesideAFileStillThere(string licence)
        {
            var folder = licence.Substring(0, licence.LastIndexOf('/'));
            var approved = new HashSet<string>(ThirdPartyCleanup.ApprovedDeletions, StringComparer.Ordinal);

            var kept = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Select(file => file.Replace('\\', '/'))
                .Where(file => !file.EndsWith(".meta", StringComparison.Ordinal) && file != licence && !approved.Contains(file))
                .ToList();

            Assert.IsTrue(File.Exists(licence), $"{licence} stays");
            CollectionAssert.DoesNotContain(ThirdPartyCleanup.ApprovedDeletions, licence);
            Assert.That(kept, Is.Not.Empty, $"{folder} still holds a file that is used");
        }

        [Test]
        public void LilitaOne_IsInTheThirdPartyFontsFolder()
        {
            foreach (var move in ThirdPartyFontMove.Moves)
            {
                Assert.IsTrue(AssetDatabase.AssetPathExists(move.To), $"{move.To}: run Gate Rush → Move Fonts To Third Party");
                Assert.IsFalse(AssetDatabase.AssetPathExists(move.From), $"{move.From} is moved, not copied");
            }
        }

        [Test]
        public void LilitaOne_LicenceIsBesideTheFont()
        {
            Assert.IsTrue(File.Exists(ThirdPartyFontMove.LicencePath), $"add Lilita One's OFL text at {ThirdPartyFontMove.LicencePath}");
        }

        private bool IsInRemovedPlace(string path)
        {
            if (removed.Contains(path))
            {
                return true;
            }

            return ThirdPartyCleanup.RemovedFolders.Any(folder => path.StartsWith(folder + "/", StringComparison.Ordinal));
        }

        private void AddIfRemoved(List<string> offenders, string prefabPath, string objectName, UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (IsInRemovedPlace(path))
            {
                offenders.Add($"{prefabPath}: '{objectName}' uses {path}");
            }
        }

        private static List<string> PathsIn(string folder, string filter) =>
            AssetDatabase.FindAssets(filter, new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
    }
}
