using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// <i>Gate Rush → Render Effects Comparison</i> (Module 47, pass 1): draws
    /// each generated debris sprite as the board draws it beside the approved
    /// reference sprite of <c>effects_reference_sprites.py</c>, both over the
    /// script's floor, logs how many of their pixels agree, and writes the
    /// pairs, with their difference, next to the references.
    /// </summary>
    /// <remarks>
    /// <para><b>What is compared.</b> The red cube, its shade tinted
    /// <c>#F02A1E</c> with its highlight added, as the reference composites
    /// it; the three shards and the white gate glow and sparkle as they are.
    /// The gate glow's gate is its bottom row on the board and its top row in
    /// the script's image, so the reference is turned upside down.</para>
    /// <para><b>Two measures</b> (<see cref="EffectsMatch"/>): the share of
    /// the footprint within 4 of 255 once drawn over the floor, Module 46's
    /// measure; and the share of the sprite's own bytes within 4, which
    /// leaves out a faint edge's one step of alpha that the dark floor
    /// magnifies.</para>
    /// <para>The art comes from <see cref="ArtPainter.Paint"/>, which writes
    /// exactly the bytes Generate Art writes; the references are read from
    /// their PNGs.</para>
    /// </remarks>
    public static class EffectsComparison
    {
        /// <summary>The folder of the references and the comparison, from the project's folder.</summary>
        public const string Folder = "Claude outputs/board-lighting";

        /// <summary>The image written, inside <see cref="Folder"/>.</summary>
        public const string OutputFile = "generated_effects.png";

        // The colour the reference cube is tinted (effects_reference_sprites.py).
        private static readonly Color32 CubeTint = new Color32(0xF0, 0x2A, 0x1E, 0xFF);

        // Space between the panels of the comparison image, in pixels.
        private const int GapPixels = 8;

        // The difference panel shows a pixel's largest channel difference this
        // many times brighter, so a few bytes show.
        private const int DifferenceGain = 8;

        private const string LogPrefix = "Render Effects Comparison";

        /// <summary>One comparison: the generated sprite, how it is drawn, and its reference file.</summary>
        private readonly struct Pair
        {
            public Pair(string name, ArtSprite sprite, ArtSprite? highlight, EffectLook look, string reference, bool isUpsideDown)
            {
                Name = name;
                Sprite = sprite;
                Highlight = highlight;
                Look = look;
                Reference = reference;
                IsUpsideDown = isUpsideDown;
            }

            public string Name { get; }
            public ArtSprite Sprite { get; }
            public ArtSprite? Highlight { get; }
            public EffectLook Look { get; }
            public string Reference { get; }
            public bool IsUpsideDown { get; }
        }

        private static IReadOnlyList<Pair> Pairs => new[]
        {
            new Pair("Cube", ArtSprite.Cube, ArtSprite.CubeHighlight, EffectLook.LitTinted, "reference_cube.png", false),
            new Pair("Shard", ArtSprite.Shard, null, EffectLook.Plain, "reference_shard_0.png", false),
            new Pair("Shard2", ArtSprite.Shard2, null, EffectLook.Plain, "reference_shard_1.png", false),
            new Pair("Shard3", ArtSprite.Shard3, null, EffectLook.Plain, "reference_shard_2.png", false),
            new Pair("GateGlow", ArtSprite.GateGlow, null, EffectLook.Plain, "reference_gate_glow.png", true),
            new Pair("Sparkle", ArtSprite.Sparkle, null, EffectLook.Plain, "reference_sparkle.png", false)
        };

        /// <summary>Compares every debris sprite with its reference, logs the measures, and writes the image.</summary>
        [MenuItem("Gate Rush/Render Effects Comparison")]
        public static void RenderFromMenu()
        {
            if (!ArtGenerator.TryFindRecipe(out var recipe, out var error))
            {
                Debug.LogError($"{LogPrefix}: {error} Nothing was rendered.");
                return;
            }

            if (!ArtGenerator.TryFindRuntimeConfig(out var config, out var configError))
            {
                Debug.LogError($"{LogPrefix}: {configError} Nothing was rendered.");
                return;
            }

            var problems = recipe.Problems();
            if (problems.Count > 0)
            {
                Debug.LogError($"{LogPrefix}: {recipe.name} has {problems.Count} problem(s); see Generate Art. Nothing was rendered.", recipe);
                return;
            }

            var folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Folder);
            var palette = ArtPalette.From(config);
            var rows = new List<(Color32[] Generated, Color32[] Reference, int Size)>();
            var lines = new List<string>();
            foreach (var pair in Pairs)
            {
                var referencePath = Path.Combine(folder, pair.Reference);
                if (!File.Exists(referencePath))
                {
                    Debug.LogError($"{LogPrefix}: {referencePath} is missing; run effects_reference_sprites.py in {Folder}. Nothing was rendered.");
                    return;
                }

                var reference = LoadPixels(referencePath, out var width, out var height);
                var image = ArtPainter.Paint(recipe, palette, pair.Sprite);
                if (width != image.Width || height != image.Height)
                {
                    Debug.LogError($"{LogPrefix}: {pair.Name} is {image.Width} by {image.Height}, its reference {width} by {height}. Nothing was rendered.");
                    return;
                }

                var highlight = pair.Highlight.HasValue ? ArtPainter.Paint(recipe, palette, pair.Highlight.Value) : null;
                var drawn = EffectsMatch.Draw(pair.Look, image, highlight, CubeTint, recipe.ShadeHeadroom);
                var referenceDrawn = EffectsMatch.DrawReference(reference, width, height, pair.IsUpsideDown);
                var (share, footprint) = EffectsMatch.Agreement(drawn, referenceDrawn);
                var (byteShare, _) = EffectsMatch.ByteAgreement(image.Pixels, reference, width, height, pair.IsUpsideDown);
                lines.Add(pair.Highlight.HasValue
                    ? $"{pair.Name}: {share:P1} of {footprint} pixels within {EffectsMatch.ToleranceBytes}/255 drawn over the floor"
                    : $"{pair.Name}: {share:P1} of {footprint} pixels within {EffectsMatch.ToleranceBytes}/255 drawn over the floor, {byteShare:P1} of the sprite's own bytes");
                rows.Add((drawn, referenceDrawn, width));
            }

            var path = Path.Combine(folder, OutputFile);
            File.WriteAllBytes(path, ArtPainter.EncodePng(Sheet(rows)));
            Debug.Log($"{LogPrefix}: wrote {path} from {recipe.name}.\n{string.Join("\n", lines)}", recipe);
        }

        /// <summary>A PNG's pixels, bottom row first, as Unity reads them.</summary>
        private static Color32[] LoadPixels(string path, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadImage(File.ReadAllBytes(path));
                width = texture.width;
                height = texture.height;
                return texture.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// One row per comparison, top to bottom in <see cref="Pairs"/>' order:
        /// the generated sprite, the reference, and their difference, each
        /// over the floor.
        /// </summary>
        private static ArtImage Sheet(IReadOnlyList<(Color32[] Generated, Color32[] Reference, int Size)> rows)
        {
            var widest = 0;
            var height = GapPixels;
            foreach (var row in rows)
            {
                widest = Mathf.Max(widest, row.Size);
                height += row.Size + GapPixels;
            }

            var width = 3 * widest + 4 * GapPixels;
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = EffectsMatch.Floor;
            }

            // Rows are stored bottom first; the first comparison goes on top.
            var top = height - GapPixels;
            foreach (var row in rows)
            {
                var bottom = top - row.Size;
                var difference = new Color32[row.Generated.Length];
                for (var i = 0; i < difference.Length; i++)
                {
                    var g = row.Generated[i];
                    var r = row.Reference[i];
                    var d = Mathf.Max(Mathf.Abs(g.r - r.r), Mathf.Max(Mathf.Abs(g.g - r.g), Mathf.Abs(g.b - r.b)));
                    var shown = (byte)Mathf.Min(byte.MaxValue, d * DifferenceGain);
                    difference[i] = new Color32(shown, shown, shown, byte.MaxValue);
                }

                Blit(pixels, width, row.Generated, row.Size, GapPixels, bottom);
                Blit(pixels, width, row.Reference, row.Size, 2 * GapPixels + widest, bottom);
                Blit(pixels, width, difference, row.Size, 3 * GapPixels + 2 * widest, bottom);
                top = bottom - GapPixels;
            }

            return new ArtImage(width, height, pixels, Vector4.zero);
        }

        private static void Blit(Color32[] into, int width, Color32[] from, int size, int x, int y)
        {
            for (var j = 0; j < size; j++)
            {
                for (var i = 0; i < size; i++)
                {
                    into[(y + j) * width + x + i] = from[j * size + i];
                }
            }
        }
    }
}
