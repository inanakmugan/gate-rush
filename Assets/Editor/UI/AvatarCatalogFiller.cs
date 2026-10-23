using System;
using System.Collections.Generic;
using System.IO;
using GateRush.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.UI.Editor
{
    /// <summary>
    /// <i>Gate Rush → Fill Avatar Catalog</i> (Module 38): fills
    /// <see cref="CatalogPath"/> with every PNG directly in
    /// <see cref="AvatarFolder"/>, in ordinal order of file name, each id the
    /// file name without its extension — "bear", "buffalo", … — so the first,
    /// and the default avatar, is the bear.
    /// </summary>
    /// <remarks>
    /// Every file must import as exactly one sprite (Sprite Mode: Single);
    /// otherwise nothing is written and every such file is named. The asset
    /// is created when missing; an existing one with entries is overwritten
    /// only after asking. Ids are written once here and then belong to the
    /// save: renaming a file later would orphan the saves that hold its id.
    /// </remarks>
    public static class AvatarCatalogFiller
    {
        /// <summary>
        /// The folder the avatars are read from: the owner's generated discs
        /// since the Module 45 follow-up, one per id the Animal Pack Redux
        /// avatars had, so no save's id changes.
        /// </summary>
        public const string AvatarFolder = InterfaceArtImportRules.AvatarFolder;

        /// <summary>The catalog the menu fills.</summary>
        public const string CatalogPath = "Assets/Config/AvatarCatalog.asset";

        private const string PngExtension = ".png";
        private const string TextureFilter = "t:Texture2D";
        private const string LogPrefix = "Fill Avatar Catalog";

        // Serialized field names of AvatarCatalog and its entries.
        private const string AvatarsField = "avatars";
        private const string IdField = "id";
        private const string SpriteField = "sprite";

        private const string DialogTitle = "Fill Avatar Catalog";
        private const string DialogMessage = "The catalog already has avatars and will be overwritten:";
        private const string DialogOverwrite = "Overwrite";
        private const string DialogCancel = "Cancel";

        /// <summary>Fills <see cref="CatalogPath"/> from <see cref="AvatarFolder"/>, creating it when missing. Logs what it wrote, or why it wrote nothing.</summary>
        [MenuItem("Gate Rush/Fill Avatar Catalog")]
        public static void FillFromMenu()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            if (existing != null && existing.Count > 0
                && !EditorUtility.DisplayDialog(DialogTitle, $"{DialogMessage}\n\n{CatalogPath}", DialogOverwrite, DialogCancel))
            {
                Debug.Log($"{LogPrefix}: cancelled; nothing was written.");
                return;
            }

            var catalog = existing != null ? existing : ScriptableObject.CreateInstance<AvatarCatalog>();
            try
            {
                Fill(catalog, AvatarFolder);
            }
            catch (InvalidOperationException exception)
            {
                if (existing == null)
                {
                    Object.DestroyImmediate(catalog);
                }

                Debug.LogError($"{LogPrefix}: {exception.Message}");
                return;
            }

            if (existing == null)
            {
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            else
            {
                EditorUtility.SetDirty(catalog);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix}: wrote {catalog.Count} avatars from {AvatarFolder} into {CatalogPath}; the first is '{catalog.IdAt(0)}'.", catalog);
        }

        /// <summary>
        /// Replaces the entries of <paramref name="catalog"/> with every PNG
        /// directly in <paramref name="folder"/>, in ordinal order of file
        /// name, each with its file name as id. Saves nothing itself.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// The folder holds no PNG, or a PNG does not import as exactly one
        /// sprite; <paramref name="catalog"/> is then unchanged.
        /// </exception>
        public static void Fill(AvatarCatalog catalog, string folder)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var paths = PngPathsIn(folder);
            if (paths.Count == 0)
            {
                throw new InvalidOperationException($"{folder} holds no PNG. Nothing was written.");
            }

            var sprites = new Sprite[paths.Count];
            var refused = new List<string>();
            for (var i = 0; i < paths.Count; i++)
            {
                sprites[i] = SingleSprite(paths[i], out var count);
                if (sprites[i] == null)
                {
                    refused.Add($"{paths[i]} ({count})");
                }
            }

            if (refused.Count > 0)
            {
                throw new InvalidOperationException(
                    "Each avatar must import as exactly one sprite (Sprite Mode: Single); these do not, with their sprite counts: " +
                    $"{string.Join(", ", refused)}. Nothing was written.");
            }

            using (var serialized = new SerializedObject(catalog))
            {
                var avatars = serialized.FindProperty(AvatarsField)
                    ?? throw new InvalidOperationException($"{nameof(AvatarCatalog)} has no serialized field '{AvatarsField}'.");
                avatars.arraySize = paths.Count;
                for (var i = 0; i < paths.Count; i++)
                {
                    var entry = avatars.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative(IdField).stringValue = Path.GetFileNameWithoutExtension(paths[i]);
                    entry.FindPropertyRelative(SpriteField).objectReferenceValue = sprites[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>The PNGs directly in <paramref name="folder"/>, not in its subfolders, in ordinal order of file name.</summary>
        private static List<string> PngPathsIn(string folder)
        {
            var paths = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return paths;
            }

            foreach (var guid in AssetDatabase.FindAssets(TextureFilter, new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (string.Equals(directory, folder, StringComparison.Ordinal)
                    && string.Equals(Path.GetExtension(path), PngExtension, StringComparison.OrdinalIgnoreCase)
                    && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
            return paths;
        }

        /// <summary>The one sprite <paramref name="path"/> imports as; null, with the <paramref name="count"/> found, when it is not exactly one.</summary>
        private static Sprite SingleSprite(string path, out int count)
        {
            Sprite found = null;
            count = 0;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                {
                    found = sprite;
                    count++;
                }
            }

            return count == 1 ? found : null;
        }
    }
}
