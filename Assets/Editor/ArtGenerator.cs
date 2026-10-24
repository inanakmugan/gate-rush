using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GateRush.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace GateRush.Editor
{
    /// <summary>
    /// <i>Gate Rush → Generate Art</i>: paints every sprite of Modules 15, 16, 17, 18, 19 and 46 from
    /// the project's <see cref="ArtRecipe"/> and writes it as a PNG under
    /// <see cref="OutputFolder"/>, with its import settings (D46), then sets
    /// each into its <see cref="RuntimeConfig"/> field, with the four
    /// materials the lit body draws with. The PNGs and materials are
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

            if (!TryFindRuntimeConfig(out var config, out var configError))
            {
                Debug.LogError($"Generate Art: {configError} Its colours paint the mechanics (Module 46); nothing was generated.");
                return;
            }

            var report = Generate(recipe, ArtPalette.From(config));
            Debug.Log(
                $"Generate Art: {ArtPainter.All.Count} sprites from {recipe.name} into {OutputFolder} — " +
                $"{report.Written} PNG(s) written, {report.Reimported} reimported with new settings, " +
                $"{report.Unchanged} unchanged.",
                recipe);

            var outlineFont = EnsureLabelOutlineFont(config);
            var materials = GenerateMaterials(recipe, config, outlineFont);
            AssignToRuntimeConfig(config, materials, outlineFont, recipe);
        }

        /// <summary>
        /// The project's only <see cref="RuntimeConfig"/>; false, with why,
        /// when there is none or more than one.
        /// </summary>
        internal static bool TryFindRuntimeConfig(out RuntimeConfig config, out string error)
        {
            config = null;
            var guids = AssetDatabase.FindAssets($"t:{nameof(RuntimeConfig)}");
            if (guids.Length != 1)
            {
                error = $"the project has {guids.Length} Runtime Configs, not one.";
                return false;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            config = AssetDatabase.LoadAssetAtPath<RuntimeConfig>(path);
            error = config != null ? null : $"the Runtime Config at {path} could not be loaded.";
            return config != null;
        }

        /// <summary>Where the generated materials live.</summary>
        public const string MaterialFolder = "Assets/Art/Materials";

        private const string MaterialFolderName = "Materials";

        /// <summary>The shader the board's lit and highlight sprites draw with (Module 46).</summary>
        public const string BoardShaderName = "Gate Rush/Board Sprite";

        /// <summary>Its twin for the introduction cards' UI Images.</summary>
        public const string BoardUiShaderName = "Gate Rush/Board UI Sprite";

        /// <summary>
        /// The materials the lit body draws with (Module 46), keyed by their
        /// <see cref="RuntimeConfig"/> field: for SpriteRenderers and for UI
        /// Images, a lit one that multiplies faces and studs back by
        /// <see cref="ArtRecipe.ShadeHeadroom"/>, and a highlight one that adds
        /// its white; and the board's labels' material with its soft drop
        /// shadow (Module 46, pass 2), a copy of the label font's own with the
        /// underlay <paramref name="config"/> sets; and, given
        /// <paramref name="outlineFont"/>, the time-bonus seconds' material
        /// with its outline (<see cref="EnsureLabelOutlineMaterial"/>). Each is
        /// created at <see cref="MaterialFolder"/> when missing and brought to
        /// these settings, saved only when one changed.
        /// </summary>
        /// <returns>The materials made or found; empty when a shader could not be found, which is logged.</returns>
        public static IReadOnlyDictionary<string, Material> GenerateMaterials(
            ArtRecipe recipe, RuntimeConfig config, TMP_FontAsset outlineFont)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder(ParentFolder, MaterialFolderName);
            }

            var board = Shader.Find(BoardShaderName);
            var ui = Shader.Find(BoardUiShaderName);
            if (board == null || ui == null)
            {
                Debug.LogError($"Generate Art: the shaders '{BoardShaderName}' and '{BoardUiShaderName}' must both be in the project; no material was made.");
                return new Dictionary<string, Material>();
            }

            var materials = new Dictionary<string, Material>
            {
                ["litMaterial"] = EnsureMaterial("BoardLit", board, recipe.ShadeHeadroom, isAdditive: false),
                ["specularMaterial"] = EnsureMaterial("BoardSpecular", board, 1f, isAdditive: true),
                ["uiLitMaterial"] = EnsureMaterial("BoardUiLit", ui, recipe.ShadeHeadroom, isAdditive: false),
                ["uiSpecularMaterial"] = EnsureMaterial("BoardUiSpecular", ui, 1f, isAdditive: true),
            };

            var shadow = EnsureLabelShadowMaterial(config);
            if (shadow != null)
            {
                materials["labelShadowMaterial"] = shadow;
            }

            var outline = outlineFont != null ? EnsureLabelOutlineMaterial(config, outlineFont) : null;
            if (outline != null)
            {
                materials["labelOutlineMaterial"] = outline;
            }

            return materials;
        }

        // The TextMeshPro distance-field shader's underlay: its keyword and
        // properties, named as the shader declares them.
        private const string UnderlayKeyword = "UNDERLAY_ON";
        private const string UnderlayColor = "_UnderlayColor";
        private const string UnderlayOffsetX = "_UnderlayOffsetX";
        private const string UnderlayOffsetY = "_UnderlayOffsetY";
        private const string UnderlaySoftness = "_UnderlaySoftness";

        /// <summary>
        /// <c>LabelShadow.mat</c> under <see cref="MaterialFolder"/>: the label
        /// font's own material with its underlay on, in
        /// <see cref="RuntimeConfig.LabelShadowColor"/>, moved by
        /// <see cref="RuntimeConfig.LabelShadowOffset"/> and softened by
        /// <see cref="RuntimeConfig.LabelShadowSoftness"/> — the soft drop shadow
        /// under the board's white digits. Made from the font's material when
        /// missing, so it carries the font's atlas; null, logged, when the
        /// config has no label font.
        /// </summary>
        private static Material EnsureLabelShadowMaterial(RuntimeConfig config)
        {
            var source = config.LabelFontMaterial;
            if (source == null)
            {
                Debug.LogError($"Generate Art: {config.name} has no Label Font, so no label shadow material was made.", config);
                return null;
            }

            var path = $"{MaterialFolder}/LabelShadow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }

            var isChanged = false;
            if (material.shader != source.shader)
            {
                material.shader = source.shader;
                isChanged = true;
            }

            if (material.mainTexture != source.mainTexture)
            {
                material.mainTexture = source.mainTexture;
                isChanged = true;
            }

            if (!material.IsKeywordEnabled(UnderlayKeyword))
            {
                material.EnableKeyword(UnderlayKeyword);
                isChanged = true;
            }

            if (material.GetColor(UnderlayColor) != config.LabelShadowColor)
            {
                material.SetColor(UnderlayColor, config.LabelShadowColor);
                isChanged = true;
            }

            isChanged |= SetFloat(material, UnderlayOffsetX, config.LabelShadowOffset.x);
            isChanged |= SetFloat(material, UnderlayOffsetY, config.LabelShadowOffset.y);
            isChanged |= SetFloat(material, UnderlaySoftness, config.LabelShadowSoftness);
            if (isChanged)
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }

            return material;
        }

        // The TextMeshPro distance-field shader's outline and the properties
        // its ratio reads, named as the shader declares them.
        private const string OutlineColor = "_OutlineColor";
        private const string OutlineWidth = "_OutlineWidth";
        private const string FaceDilate = "_FaceDilate";
        private const string GradientScale = "_GradientScale";
        private const string WeightNormal = "_WeightNormal";
        private const string WeightBold = "_WeightBold";

        /// <summary>
        /// TextMeshPro's own constants for the outline: the atlas pixels its
        /// scale ratio holds back from the gradient scale, the divisor that
        /// turns a font weight into a dilation, the most its outline and
        /// dilation sliders take, and the atlas pixels one unit of the
        /// distance field spans, in gradient scales — two: its value falls
        /// from 0.5 at a glyph's edge to 0 a gradient scale outside it,
        /// measured in the outline font's atlas (about 7.1 of 255 per pixel at
        /// a gradient scale of 18) and as TextMeshPro's own padding assumes.
        /// Not tunables.
        /// </summary>
        private const float TmpRatioClamp = 1f;
        private const float TmpWeightDivisor = 4f;
        private const float TmpMostOutline = 1f;
        private const float TmpTexelsPerFieldUnitInGradientScales = 2f;

        /// <summary>The outline font asset is named after the label font with this after it.</summary>
        private const string OutlineFontSuffix = " Outline";

        /// <summary>The <see cref="RuntimeConfig"/> field that holds the outline font asset.</summary>
        private const string LabelOutlineFontField = "labelOutlineFont";

        /// <summary>
        /// <c>&lt;label font&gt; Outline.asset</c> beside the label font: Lilita
        /// One sampled at the label font's size, with
        /// <see cref="RuntimeConfig.LabelOutlineAtlasPadding"/> of padding —
        /// room in its distance field for a wide outline — holding only
        /// <see cref="RuntimeConfig.LabelOutlineCharacters"/>, static, with its
        /// atlas and its own material inside it. Made from the TrueType file
        /// the label font was made from. Kept as it is when it already has
        /// that padding, size and those characters, so regenerating changes
        /// nothing; otherwise made afresh. The shared label font and its
        /// shadow material are never touched. Null, logged, when it cannot be
        /// made.
        /// </summary>
        private static TMP_FontAsset EnsureLabelOutlineFont(RuntimeConfig config)
        {
            var source = config.LabelFont;
            if (source == null)
            {
                Debug.LogError($"Generate Art: {config.name} has no Label Font, so no label outline font was made.", config);
                return null;
            }

            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(source))?.Replace('\\', '/');
            var path = $"{folder}/{source.name}{OutlineFontSuffix}.asset";
            var padding = config.LabelOutlineAtlasPadding;
            var characters = config.LabelOutlineCharacters;
            var pointSize = Mathf.RoundToInt(source.faceInfo.pointSize);

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null && existing.atlasPadding == padding && Mathf.RoundToInt(existing.faceInfo.pointSize) == pointSize
                && existing.HasCharacters(characters))
            {
                return existing;
            }

            var fontPath = AssetDatabase.GUIDToAssetPath(source.creationSettings.sourceFontFileGUID);
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError($"Generate Art: the TrueType file {source.name} was made from could not be found, so no label outline font was made.", source);
                return null;
            }

            var made = TMP_FontAsset.CreateFontAsset(
                font, pointSize, padding, GlyphRenderMode.SDFAA, source.atlasWidth, source.atlasHeight, AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: false);
            if (made == null)
            {
                Debug.LogError($"Generate Art: TextMeshPro could not make a font asset from {fontPath}.", font);
                return null;
            }

            if (!made.TryAddCharacters(characters, out var missing))
            {
                Debug.LogError($"Generate Art: {fontPath} could not render \"{missing}\" into the label outline font; it was not made.", font);
                UnityEngine.Object.DestroyImmediate(made.material);
                UnityEngine.Object.DestroyImmediate(made.atlasTexture);
                UnityEngine.Object.DestroyImmediate(made);
                return null;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            made.name = Path.GetFileNameWithoutExtension(path);
            made.atlasTexture.name = $"{made.name} Atlas";
            made.material.name = $"{made.name} Material";
            AssetDatabase.CreateAsset(made, path);
            AssetDatabase.AddObjectToAsset(made.atlasTexture, made);
            AssetDatabase.AddObjectToAsset(made.material, made);

            // Static: the glyphs stay as rendered, and nothing is added or
            // cleared in a build.
            made.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(made);
            AssetDatabase.SaveAssetIfDirty(made);
            Debug.Log($"Generate Art: made {path}, padding {padding}, \"{characters}\".", made);
            return made;
        }

        /// <summary>
        /// <c>LabelOutline.mat</c> under <see cref="MaterialFolder"/>: the
        /// label font's distance-field shader on <paramref name="font"/>'s
        /// atlas, with the dark outline of
        /// <see cref="RuntimeConfig.LabelOutlineColor"/> wide enough to stand
        /// <see cref="RuntimeConfig.LabelOutlineStrokeCells"/> outside the
        /// glyph at <see cref="RuntimeConfig.CountFontSize"/>, the white face
        /// kept at the glyph's own size (<see cref="OutlineFor"/>), and the
        /// label shadow's underlay. The shadow sits under the outline, as the
        /// reference's does. Logs a warning when the font's padding cannot
        /// hold the whole stroke.
        /// </summary>
        /// <remarks>
        /// Revision B: the outline used to stand twice the stroke out — the
        /// face was right, but the reach was worked out with a field unit of
        /// one gradient scale of atlas pixels instead of two
        /// (<see cref="EdgesOf"/>).
        /// </remarks>
        private static Material EnsureLabelOutlineMaterial(RuntimeConfig config, TMP_FontAsset font)
        {
            var shaderSource = config.LabelFontMaterial;
            if (shaderSource == null || font.material == null)
            {
                Debug.LogError($"Generate Art: {config.name} has no Label Font, or {font.name} no material, so no label outline material was made.", config);
                return null;
            }

            var path = $"{MaterialFolder}/LabelOutline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material) { shader = shaderSource.shader };
                AssetDatabase.CreateAsset(material, path);
            }

            var isChanged = false;
            if (material.shader != shaderSource.shader)
            {
                material.shader = shaderSource.shader;
                isChanged = true;
            }

            if (material.mainTexture != font.atlasTexture)
            {
                material.mainTexture = font.atlasTexture;
                isChanged = true;
            }

            // The atlas's own measures, which its distance field was made with.
            isChanged |= SetFloat(material, GradientScale, font.material.GetFloat(GradientScale));
            isChanged |= SetFloat(material, WeightNormal, font.material.GetFloat(WeightNormal));
            isChanged |= SetFloat(material, WeightBold, font.material.GetFloat(WeightBold));

            var strokeTexels = StrokeTexels(config, font.faceInfo.pointSize);
            var weight = Mathf.Max(material.GetFloat(WeightNormal), material.GetFloat(WeightBold)) / TmpWeightDivisor;
            var outline = OutlineFor(strokeTexels, material.GetFloat(GradientScale), weight);
            if (!outline.IsWholeStroke)
            {
                Debug.LogWarning(
                    $"Generate Art: {font.name}'s padding of {font.atlasPadding} holds an outline of {outline.ReachTexels:0.##} of the " +
                    $"{strokeTexels:0.##} atlas pixels Label Outline Stroke Cells asks for; set {config.name}'s Label Outline Atlas Padding " +
                    $"to at least {outline.PaddingNeeded} and run Generate Art again.",
                    config);
            }

            if (material.GetColor(OutlineColor) != config.LabelOutlineColor)
            {
                material.SetColor(OutlineColor, config.LabelOutlineColor);
                isChanged = true;
            }

            isChanged |= SetFloat(material, OutlineWidth, outline.Width);
            isChanged |= SetFloat(material, FaceDilate, outline.Width);

            if (!material.IsKeywordEnabled(UnderlayKeyword))
            {
                material.EnableKeyword(UnderlayKeyword);
                isChanged = true;
            }

            if (material.GetColor(UnderlayColor) != config.LabelShadowColor)
            {
                material.SetColor(UnderlayColor, config.LabelShadowColor);
                isChanged = true;
            }

            isChanged |= SetFloat(material, UnderlayOffsetX, config.LabelShadowOffset.x);
            isChanged |= SetFloat(material, UnderlayOffsetY, config.LabelShadowOffset.y);
            isChanged |= SetFloat(material, UnderlaySoftness, config.LabelShadowSoftness);
            if (isChanged)
            {
                // The scale ratios TextMeshPro keeps on the material follow the
                // outline and dilation just set.
                ShaderUtilities.UpdateShaderRatios(material);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }

            return material;
        }

        /// <summary>
        /// Where TextMeshPro's distance-field shader draws a label's edges
        /// with an outline of <paramref name="width"/> and a face dilation of
        /// <paramref name="dilate"/>, on an atlas of
        /// <paramref name="gradientScale"/> with the font's weight dilation
        /// <paramref name="weight"/>: in atlas pixels outside the glyph's own
        /// edge, the white face's edge and the outline's outer edge. Its scale
        /// ratio is (gradientScale − 1) / (gradientScale · max(1, weight +
        /// dilate + width)); the dilation moves the edge out by half of
        /// dilate × ratio in field units, and the outline spans width × ratio
        /// field units centred on that moved edge — half inside, eaten from the
        /// face, half outside — and a field unit is
        /// <see cref="TmpTexelsPerFieldUnitInGradientScales"/> gradient scales
        /// of atlas pixels. So the face keeps the glyph's size exactly when the
        /// dilation equals the width.
        /// </summary>
        public static OutlineEdges EdgesOf(float width, float dilate, float gradientScale, float weight)
        {
            var ratio = (gradientScale - TmpRatioClamp) / (gradientScale * Mathf.Max(1f, weight + dilate + width));
            var texelsPerFieldUnit = TmpTexelsPerFieldUnitInGradientScales * gradientScale;
            var face = (dilate - width) * 0.5f * ratio * texelsPerFieldUnit;
            var outer = (dilate + width) * 0.5f * ratio * texelsPerFieldUnit;
            return new OutlineEdges(face, outer);
        }

        /// <summary>
        /// The outline that stands <paramref name="strokeTexels"/> atlas pixels
        /// outside a glyph, on an atlas of <paramref name="gradientScale"/> with
        /// the font's weight dilation <paramref name="weight"/>, like the
        /// reference's stroke: the face dilated by as much as the outline is
        /// wide, so the white face keeps the glyph's size and only the outline
        /// reaches out (<see cref="EdgesOf"/>). With both W, the outer edge
        /// stands k · W · (gradientScale − 1) / max(1, weight + 2W) atlas
        /// pixels out, k the field unit in gradient scales: linear in W up to
        /// the ratio's knee, then approaching the padding. Solved for W, at
        /// most the slider's 1; where even that falls short, the reach it gets
        /// and the padding the stroke needs.
        /// </summary>
        public static OutlineSetting OutlineFor(float strokeTexels, float gradientScale, float weight)
        {
            var reachPerUsable = TmpTexelsPerFieldUnitInGradientScales * (gradientScale - TmpRatioClamp);

            // Below the knee (weight + 2W ≤ 1) the reach is W · k · usable;
            // above it W · k · usable / (weight + 2W).
            var width = strokeTexels / reachPerUsable;
            if (weight + 2f * width > 1f)
            {
                width = reachPerUsable > 2f * strokeTexels
                    ? strokeTexels * weight / (reachPerUsable - 2f * strokeTexels)
                    : TmpMostOutline;
            }

            width = Mathf.Min(width, TmpMostOutline);
            var reach = EdgesOf(width, width, gradientScale, weight).OuterTexels;

            // The padding at which the slider's 1 still reaches the stroke:
            // k · padding / (weight + 2) ≥ stroke.
            var needed = Mathf.CeilToInt(
                strokeTexels * (weight + 2f * TmpMostOutline) / (TmpTexelsPerFieldUnitInGradientScales * TmpMostOutline));
            return new OutlineSetting(width, reach, reach >= strokeTexels - OutlineReachTolerance, needed);
        }

        /// <summary>Where a label's face and outline end, outside the glyph's own edge, in atlas pixels (<see cref="EdgesOf"/>).</summary>
        public readonly struct OutlineEdges
        {
            /// <summary>Edges at <paramref name="faceTexels"/> and <paramref name="outerTexels"/>.</summary>
            public OutlineEdges(float faceTexels, float outerTexels)
            {
                FaceTexels = faceTexels;
                OuterTexels = outerTexels;
            }

            /// <summary>The white face's edge: 0 when it keeps the glyph's size, above 0 when dilated past it.</summary>
            public float FaceTexels { get; }

            /// <summary>The outline's outer edge.</summary>
            public float OuterTexels { get; }
        }

        /// <summary>
        /// <paramref name="config"/>'s outline stroke in atlas pixels of a font
        /// sampled at <paramref name="pointSize"/>: the stroke's share of the
        /// em of a label at <see cref="RuntimeConfig.CountFontSize"/>.
        /// </summary>
        public static float StrokeTexels(RuntimeConfig config, float pointSize) =>
            config.LabelOutlineStrokeCells * config.CellSize
            / (config.CountFontSize * RuntimeConfig.WorldUnitsPerFontSize) * pointSize;

        /// <summary>How far short of the stroke an outline may stand and still count as the whole stroke, in atlas pixels: rounding, not a tunable.</summary>
        private const float OutlineReachTolerance = 1e-3f;

        /// <summary>The outline <see cref="OutlineFor"/> settles on.</summary>
        public readonly struct OutlineSetting
        {
            /// <summary>An outline of <paramref name="width"/> reaching <paramref name="reachTexels"/> out.</summary>
            public OutlineSetting(float width, float reachTexels, bool isWholeStroke, int paddingNeeded)
            {
                Width = width;
                ReachTexels = reachTexels;
                IsWholeStroke = isWholeStroke;
                PaddingNeeded = paddingNeeded;
            }

            /// <summary>The outline width, and the face dilation, to set: from 0 to 1.</summary>
            public float Width { get; }

            /// <summary>How far outside the glyph the outline's outer edge stands, in atlas pixels.</summary>
            public float ReachTexels { get; }

            /// <summary>True when the outline reaches the whole stroke asked for.</summary>
            public bool IsWholeStroke { get; }

            /// <summary>The least atlas padding at which the stroke can be reached.</summary>
            public int PaddingNeeded { get; }
        }

        /// <summary>
        /// The material <paramref name="name"/> under <see cref="MaterialFolder"/>
        /// on <paramref name="shader"/>, created when missing: alpha-blended and
        /// scaled by <paramref name="colorScale"/>, or premultiplied so it adds
        /// its colour (<paramref name="isAdditive"/>).
        /// </summary>
        private static Material EnsureMaterial(string name, Shader shader, float colorScale, bool isAdditive)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            var isChanged = material.shader != shader;
            if (isChanged)
            {
                material.shader = shader;
            }

            isChanged |= SetFloat(material, "_ColorScale", colorScale);
            isChanged |= SetFloat(material, "_Premultiply", isAdditive ? 1f : 0f);
            isChanged |= SetFloat(material, "_SrcBlend", (float)(isAdditive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
            isChanged |= SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (isChanged)
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }

            return material;
        }

        private static bool SetFloat(Material material, string property, float value)
        {
            if (material.GetFloat(property) == value)
            {
                return false;
            }

            material.SetFloat(property, value);
            return true;
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
        /// Sets every generated sprite into its field of
        /// <paramref name="config"/> (<see cref="RuntimeConfigField"/>), each of
        /// <paramref name="materials"/> into the field it is keyed by, and
        /// every one of <paramref name="recipe"/>'s
        /// <see cref="ArtRecipe.BoardMeasures"/> into its field, so the config
        /// always holds what the generator wrote and the sizes it was painted
        /// at. Saves only when a field changed, and logs one line per field it
        /// set; logs an error and sets nothing it cannot resolve.
        /// </summary>
        private static void AssignToRuntimeConfig(
            RuntimeConfig config, IReadOnlyDictionary<string, Material> materials, TMP_FontAsset outlineFont, ArtRecipe recipe)
        {
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

            foreach (var pair in materials)
            {
                var property = serialized.FindProperty(pair.Key);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    Debug.LogError($"Generate Art: {config.name} has no material field '{pair.Key}'.", config);
                    continue;
                }

                if (property.objectReferenceValue != pair.Value)
                {
                    property.objectReferenceValue = pair.Value;
                    set.Add($"{pair.Key} = {AssetDatabase.GetAssetPath(pair.Value)}");
                }
            }

            var fontProperty = serialized.FindProperty(LabelOutlineFontField);
            if (fontProperty == null || fontProperty.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogError($"Generate Art: {config.name} has no font field '{LabelOutlineFontField}'.", config);
            }
            else if (outlineFont != null && fontProperty.objectReferenceValue != outlineFont)
            {
                fontProperty.objectReferenceValue = outlineFont;
                set.Add($"{LabelOutlineFontField} = {AssetDatabase.GetAssetPath(outlineFont)}");
            }

            foreach (var (field, value) in recipe.BoardMeasures())
            {
                var property = serialized.FindProperty(field);
                if (property == null || property.propertyType != SerializedPropertyType.Float)
                {
                    Debug.LogError($"Generate Art: {config.name} has no measure field '{field}'.", config);
                    continue;
                }

                if (property.floatValue != value)
                {
                    property.floatValue = value;
                    set.Add($"{field} = {value}");
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
        /// must have no <see cref="ArtRecipe.Problems"/>, in
        /// <paramref name="palette"/>'s colours where a sprite has its own, and
        /// applies the import settings.
        /// </summary>
        public static GenerationReport Generate(ArtRecipe recipe, ArtPalette palette)
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
                var image = ArtPainter.Paint(recipe, palette, sprite);
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

        /// <summary>
        /// The <see cref="ArtRecipe"/> selected in the Project window, or the
        /// project's only one; false, with why, when there is neither.
        /// </summary>
        internal static bool TryFindRecipe(out ArtRecipe recipe, out string error)
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
