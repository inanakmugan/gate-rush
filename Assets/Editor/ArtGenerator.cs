using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GateRush.Runtime;
using UnityEditor;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// <i>Gate Rush → Generate Art</i>: paints every sprite of Modules 15, 16, 17, 18, 19 and 46 from
    /// the project's <see cref="ArtRecipe"/> and writes it as a PNG under
    /// <see cref="OutputFolder"/>, with its import settings (D46), then sets
    /// each into its <see cref="RuntimeConfig"/> field. The PNGs are
    /// committed; the build never runs this.
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing changes when nothing changed.</b> A PNG whose bytes
    /// equal the file already on disk is not rewritten, and a texture whose
    /// import settings already match is not reimported, so regenerating with an
    /// unchanged recipe touches no file — neither the PNGs nor the
    /// <c>.meta</c> files Unity keeps for them. Nothing here writes a
    /// <c>.meta</c> file; the importer does.</para>
    /// <para><b>Import settings</b>, chosen with WebGL in mind: single sprites,
    /// full-rect quads (9-slicing needs them, and a quad is the cheapest
    /// mesh), centre pivot so mirroring keeps a piece in place, no physics
    /// shape, no mipmaps (a cell is drawn at roughly its texture's size),
    /// bilinear filtering with clamped edges (an opaque tile border stays
    /// opaque, so tiles meet without seams), and <b>no compression</b>. Block
    /// compression smears anti-aliased edges and smooth gradients; the only
    /// block format both mobile WebGL and Android support, ASTC, is missing on
    /// desktop WebGL; the whole set is under a megabyte uncompressed, and
    /// uncompressed textures ignore a build's texture-compression override.
    /// </para>
    /// <para><b>Batching (deferred until the first WebGL profile).</b> Each
    /// sprite is its own texture, so sprites of different kinds cannot batch
    /// into one draw call. If profiling asks for it, this generator is where a
    /// packed sheet belongs: paint every sprite into one texture, surround each
    /// with a few pixels copied outward from its own border (edge-extended
    /// padding, so bilinear sampling at a tile's edge never reads a neighbour or
    /// transparency), and import it as a Multiple sprite sheet. A plain Sprite
    /// Atlas would not do: its transparent padding brings back the seams that
    /// clamped, separate textures avoid.</para>
    /// </remarks>
    public static class ArtGenerator
    {
        /// <summary>Where the generated PNGs live.</summary>
        public const string OutputFolder = "Assets/Art/Generated";

        private const string ParentFolder = "Assets/Art";
        private const string FolderName = "Generated";

        /// <summary>
        /// Generates from the <see cref="ArtRecipe"/> selected in the Project
        /// window, or from the only one in the project. Logs what it wrote, or
        /// why it wrote nothing.
        /// </summary>
        [MenuItem("Gate Rush/Generate Art")]
        public static void GenerateFromMenu()
        {
            if (!TryFindRecipe(out var recipe, out var error))
            {
                Debug.LogError($"Generate Art: {error} Nothing was generated.");
                return;
            }

            var problems = recipe.Problems();
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                {
                    Debug.LogError($"Generate Art: {problem}", recipe);
                }

                Debug.LogError($"Generate Art: {recipe.name} has {problems.Count} problem(s); nothing was generated.", recipe);
                return;
            }

            var report = Generate(recipe);
            Debug.Log(
                $"Generate Art: {ArtPainter.All.Count} sprites from {recipe.name} into {OutputFolder} — " +
                $"{report.Written} PNG(s) written, {report.Reimported} reimported with new settings, " +
                $"{report.Unchanged} unchanged.",
                recipe);

            AssignToRuntimeConfig();
        }

        /// <summary>
        /// The <see cref="RuntimeConfig"/> field that holds <paramref name="sprite"/>:
        /// the sprite's name in camel case followed by <c>Sprite</c>. Null for
        /// <see cref="ArtSprite.Restart"/>, which the interface's prefabs use
        /// and the board's config does not hold.
        /// </summary>
        public static string RuntimeConfigField(ArtSprite sprite)
        {
            if (sprite == ArtSprite.Restart)
            {
                return null;
            }

            var name = sprite.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1) + "Sprite";
        }

        /// <summary>Where <paramref name="sprite"/>'s PNG is written.</summary>
        public static string PathOf(ArtSprite sprite) => $"{OutputFolder}/{sprite}.png";

        /// <summary>
        /// Sets every generated sprite into its field of the project's only
        /// <see cref="RuntimeConfig"/> (<see cref="RuntimeConfigField"/>), so the
        /// config always holds what the generator wrote. Saves only when a
        /// field changed, and logs one line per field it set; logs an error
        /// and sets nothing it cannot resolve.
        /// </summary>
        private static void AssignToRuntimeConfig()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(RuntimeConfig)}");
            if (guids.Length != 1)
            {
                Debug.LogError(
                    $"Generate Art: the project has {guids.Length} Runtime Configs, not one; no sprite was assigned.");
                return;
            }

            var configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var config = AssetDatabase.LoadAssetAtPath<RuntimeConfig>(configPath);
            if (config == null)
            {
                Debug.LogError($"Generate Art: the Runtime Config at {configPath} could not be loaded; no sprite was assigned.");
                return;
            }

            var serialized = new SerializedObject(config);
            var set = new List<string>();
            foreach (var sprite in ArtPainter.All)
            {
                var field = RuntimeConfigField(sprite);
                if (field == null)
                {
                    continue;
                }

                var property = serialized.FindProperty(field);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    Debug.LogError($"Generate Art: {config.name} has no sprite field '{field}' for {sprite}.", config);
                    continue;
                }

                var path = PathOf(sprite);
                var loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (loaded == null)
                {
                    Debug.LogError($"Generate Art: no sprite could be loaded from {path} for '{field}'.", config);
                    continue;
                }

                if (property.objectReferenceValue != loaded)
                {
                    property.objectReferenceValue = loaded;
                    set.Add($"{field} = {path}");
                }
            }

            if (set.Count == 0)
            {
                return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(config);
            foreach (var line in set)
            {
                Debug.Log($"Generate Art: {config.name}.{line}", config);
            }
        }

        /// <summary>
        /// Paints and writes every sprite from <paramref name="recipe"/>, which
        /// must have no <see cref="ArtRecipe.Problems"/>, and applies the import
        /// settings.
        /// </summary>
        public static GenerationReport Generate(ArtRecipe recipe)
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder(ParentFolder, FolderName);
            }

            var written = 0;
            var reimported = 0;
            var unchanged = 0;

            foreach (var sprite in ArtPainter.All)
            {
                var image = ArtPainter.Paint(recipe, sprite);
                var bytes = ArtPainter.EncodePng(image);
                var path = PathOf(sprite);

                var isWritten = !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes);
                if (isWritten)
                {
                    File.WriteAllBytes(path, bytes);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }

                var isReimported = ApplyImportSettings(path, image, recipe);

                if (isWritten)
                {
                    written++;
                }
                else if (isReimported)
                {
                    reimported++;
                }
                else
                {
                    unchanged++;
                }
            }

            return new GenerationReport(written, reimported, unchanged);
        }

        /// <summary>
        /// Brings the texture at <paramref name="path"/> to the generator's
        /// import settings, reimporting only when one differs. True when it
        /// reimported.
        /// </summary>
        private static bool ApplyImportSettings(string path, ArtImage image, ArtRecipe recipe)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            var isChanged = false;
            isChanged |= Apply(settings.textureType, TextureImporterType.Sprite, v => settings.textureType = v);
            isChanged |= Apply(settings.spriteMode, (int)SpriteImportMode.Single, v => settings.spriteMode = v);
            isChanged |= Apply(settings.spritePixelsPerUnit, (float)recipe.CellPixels, v => settings.spritePixelsPerUnit = v);
            isChanged |= Apply(settings.spriteAlignment, (int)SpriteAlignment.Center, v => settings.spriteAlignment = v);
            isChanged |= Apply(settings.spriteBorder, image.Border, v => settings.spriteBorder = v);
            isChanged |= Apply(settings.spriteMeshType, SpriteMeshType.FullRect, v => settings.spriteMeshType = v);
            isChanged |= Apply(settings.spriteGenerateFallbackPhysicsShape, false, v => settings.spriteGenerateFallbackPhysicsShape = v);
            isChanged |= Apply(settings.mipmapEnabled, false, v => settings.mipmapEnabled = v);
            isChanged |= Apply(settings.filterMode, FilterMode.Bilinear, v => settings.filterMode = v);
            isChanged |= Apply(settings.wrapMode, TextureWrapMode.Clamp, v => settings.wrapMode = v);
            isChanged |= Apply(settings.alphaSource, TextureImporterAlphaSource.FromInput, v => settings.alphaSource = v);
            isChanged |= Apply(settings.alphaIsTransparency, true, v => settings.alphaIsTransparency = v);
            isChanged |= Apply(settings.sRGBTexture, true, v => settings.sRGBTexture = v);
            isChanged |= Apply(settings.readable, false, v => settings.readable = v);
            isChanged |= Apply(settings.npotScale, TextureImporterNPOTScale.None, v => settings.npotScale = v);

            if (isChanged)
            {
                importer.SetTextureSettings(settings);
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                isChanged = true;
            }

            if (isChanged)
            {
                importer.SaveAndReimport();
            }

            return isChanged;
        }

        /// <summary>Sets <paramref name="desired"/> through <paramref name="set"/> when it differs exactly from <paramref name="current"/>.</summary>
        private static bool Apply<T>(T current, T desired, Action<T> set)
        {
            if (EqualityComparer<T>.Default.Equals(current, desired))
            {
                return false;
            }

            set(desired);
            return true;
        }

        private static bool TryFindRecipe(out ArtRecipe recipe, out string error)
        {
            recipe = Selection.activeObject as ArtRecipe;
            if (recipe != null)
            {
                error = null;
                return true;
            }

            var guids = AssetDatabase.FindAssets($"t:{nameof(ArtRecipe)}");
            if (guids.Length == 1)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                recipe = AssetDatabase.LoadAssetAtPath<ArtRecipe>(path);
                error = recipe != null ? null : $"the Art Recipe at {path} could not be loaded.";
                return recipe != null;
            }

            error = guids.Length == 0
                ? "the project has no Art Recipe; create one with Assets → Create → Gate Rush → Art Recipe."
                : $"the project has {guids.Length} Art Recipes; select the one to use in the Project window.";
            return false;
        }
    }

    /// <summary>What one run of <see cref="ArtGenerator.Generate"/> did, sprite by sprite.</summary>
    public readonly struct GenerationReport
    {
        /// <summary>A report.</summary>
        public GenerationReport(int written, int reimported, int unchanged)
        {
            Written = written;
            Reimported = reimported;
            Unchanged = unchanged;
        }

        /// <summary>PNGs whose bytes changed, or that did not exist, and were written.</summary>
        public int Written { get; }

        /// <summary>PNGs left as they were whose import settings were brought up to date.</summary>
        public int Reimported { get; }

        /// <summary>PNGs and settings that already matched: nothing touched.</summary>
        public int Unchanged { get; }
    }
}
