using System;
using System.Collections.Generic;
using System.IO;
using GateRush.Core;
using GateRush.Runtime;
using UnityEditor;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// <i>Gate Rush → Render Lighting Comparison</i> (Module 46): composes the
    /// six blocks of the lighting reference script from the generated
    /// sprites, as the board draws them, and writes the result next to the
    /// reference image, so the two can be compared pixel by pixel.
    /// </summary>
    /// <remarks>
    /// <para><b>The layout is the reference's.</b> The script lays six blocks
    /// on a four-by-three board with a 24-pixel margin, on its own floor
    /// colour, at 128 pixels a cell — the generated sprites' own size, so
    /// every quarter lands on whole pixels and is sampled one to one. Its
    /// blocks are given from the top left, y down; they are turned to the
    /// board's y-up cells here.</para>
    /// <para><b>The blending is the board's.</b> In linear light: the floor,
    /// then every block's floor shadow, then its lit body (the face's stored
    /// shade times the shade headroom, tinted), then its highlight added,
    /// then its studs (lit like the body) and their rim light blended over
    /// them — the order of the board's sorting orders, with the sprites posed
    /// by the same tiles, flips and turn. Each block sits in its own cells;
    /// pieces never draw into the gap between blocks except the shadows.</para>
    /// <para>The art comes from <see cref="ArtPainter.Paint"/>, which writes
    /// exactly the bytes Generate Art writes, so the image needs neither the
    /// PNGs' import nor a readable texture.</para>
    /// </remarks>
    public static class LightingComparison
    {
        /// <summary>Where the comparison image is written, from the project's folder: beside the reference.</summary>
        public const string OutputPath = "Claude outputs/board-lighting/generated_lit.png";

        // The reference script's layout and colours (reference_render.py):
        // the comparison reproduces its scene, so these are its data, not
        // look parameters of the game.
        private const int Columns = 4;
        private const int Rows = 3;
        private const int MarginPixels = 24;
        private const string FloorHex = "#281350";

        private static readonly (int X, int Y, int Width, int Height, string Hex)[] Blocks =
        {
            (0, 0, 2, 2, "#F02A1E"),
            (2, 0, 1, 2, "#0054F6"),
            (3, 0, 1, 1, "#F6BA00"),
            (3, 1, 1, 1, "#07A515"),
            (0, 2, 3, 1, "#A236F6"),
            (3, 2, 1, 1, "#F6660C"),
        };

        /// <summary>Renders the comparison from the selected or only recipe and writes it to <see cref="OutputPath"/>.</summary>
        [MenuItem("Gate Rush/Render Lighting Comparison")]
        public static void RenderFromMenu()
        {
            if (!ArtGenerator.TryFindRecipe(out var recipe, out var error))
            {
                Debug.LogError($"Render Lighting Comparison: {error} Nothing was rendered.");
                return;
            }

            var problems = recipe.Problems();
            if (problems.Count > 0)
            {
                Debug.LogError($"Render Lighting Comparison: {recipe.name} has {problems.Count} problem(s); see Generate Art. Nothing was rendered.", recipe);
                return;
            }

            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, ArtPainter.EncodePng(Render(recipe)));
            Debug.Log($"Render Lighting Comparison: wrote {path} from {recipe.name}.", recipe);
        }

        /// <summary>The reference script's scene, drawn from <paramref name="recipe"/>'s sprites, bottom row first.</summary>
        public static ArtImage Render(ArtRecipe recipe)
        {
            var cell = recipe.CellPixels;
            var width = Columns * cell + 2 * MarginPixels;
            var height = Rows * cell + 2 * MarginPixels;
            var floor = Linear(FloorHex);
            var image = new Vector3[width * height];
            for (var i = 0; i < image.Length; i++)
            {
                image[i] = floor;
            }

            var sprites = new Dictionary<ArtSprite, ArtImage>();
            ArtImage SpriteOf(ArtSprite sprite)
            {
                if (!sprites.TryGetValue(sprite, out var painted))
                {
                    painted = ArtPainter.Paint(recipe, sprite);
                    sprites[sprite] = painted;
                }

                return painted;
            }

            var blocks = new List<(Vector2 Origin, Coord[] Cells, IReadOnlyList<QuarterTile> Tiles, Vector3 Albedo)>();
            foreach (var block in Blocks)
            {
                var cells = new List<Coord>();
                for (var y = 0; y < block.Height; y++)
                {
                    for (var x = 0; x < block.Width; x++)
                    {
                        cells.Add(new Coord(x, y));
                    }
                }

                // The script's rows run down from the top; the board's cells run up.
                var origin = new Vector2(MarginPixels + block.X * cell, MarginPixels + (Rows - block.Y - block.Height) * cell);
                var array = cells.ToArray();
                blocks.Add((origin, array, BlockTiling.Compute(array), Linear(block.Hex)));
            }

            double headroom = recipe.ShadeHeadroom;
            var margin = recipe.FloorShadowMarginPixels / (float)cell;

            // The shadows first, under every block, as their sorting order has them.
            foreach (var block in blocks)
            {
                foreach (var tile in QuarterLayers.Edged(block.Tiles))
                {
                    var rect = FaceShape.ShadowQuarterRect(BlockTiling.QuarterRect(tile), tile, margin);
                    Draw(image, width, height, SpriteOf(Placed(PlacedLayer.Shadow, tile)), tile, block.Origin, rect, cell,
                        (under, tone, alpha) => under * (1f - alpha));
                }
            }

            foreach (var block in blocks)
            {
                var albedo = block.Albedo;
                foreach (var tile in block.Tiles)
                {
                    var rect = BlockTiling.QuarterRect(tile);
                    Draw(image, width, height, SpriteOf(Placed(PlacedLayer.Face, tile)), tile, block.Origin, rect, cell,
                        (under, tone, alpha) => Vector3.Lerp(under, albedo * (float)(tone * headroom), alpha));
                    Draw(image, width, height, SpriteOf(Placed(PlacedLayer.Body, tile)), tile, block.Origin, rect, cell,
                        (under, tone, alpha) => under + Vector3.one * (float)(tone * alpha));
                }

                foreach (var c in block.Cells)
                {
                    var rect = new Rect(c.X, c.Y, 1f, 1f);
                    Draw(image, width, height, SpriteOf(ArtSprite.Studs), null, block.Origin, rect, cell,
                        (under, tone, alpha) => Vector3.Lerp(under, albedo * (float)(tone * headroom), alpha));
                    Draw(image, width, height, SpriteOf(ArtSprite.StudGloss), null, block.Origin, rect, cell,
                        (under, tone, alpha) => Vector3.Lerp(under, Vector3.one * (float)tone, alpha));
                }
            }

            var pixels = new Color32[image.Length];
            for (var i = 0; i < image.Length; i++)
            {
                var c = image[i];
                pixels[i] = new Color32(Byte(c.x), Byte(c.y), Byte(c.z), 255);
            }

            return new ArtImage(width, height, pixels, Vector4.zero);
        }

        /// <summary>The sets a placed sprite comes from.</summary>
        private enum PlacedLayer
        {
            Face,
            Body,
            Shadow
        }

        /// <summary>The sprite the runtime draws <paramref name="tile"/> with in <paramref name="layer"/>: by its piece and its place.</summary>
        private static ArtSprite Placed(PlacedLayer layer, QuarterTile tile)
        {
            var isRight = tile.SignX > 0;
            var isTop = tile.SignY > 0;
            var index = tile.Piece switch
            {
                QuarterPiece.OuterCorner => isTop ? (isRight ? 1 : 0) : (isRight ? 3 : 2),
                QuarterPiece.ConcaveCorner => 4 + (isTop ? (isRight ? 1 : 0) : (isRight ? 3 : 2)),
                QuarterPiece.EdgeAlongX => isTop ? 8 : 9,
                QuarterPiece.EdgeAlongY => isRight ? 11 : 10,
                _ => 12,
            };

            var first = layer == PlacedLayer.Face ? ArtSprite.FaceOuterTopLeft
                : layer == PlacedLayer.Body ? ArtSprite.BodyOuterTopLeft
                : ArtSprite.ShadowOuterTopLeft;
            return first + index;
        }

        /// <summary>
        /// Blends <paramref name="sprite"/>, posed as <paramref name="tile"/>
        /// says (a cell sprite when null) over <paramref name="cellRect"/> of a
        /// block at <paramref name="origin"/>, into <paramref name="image"/>
        /// through <paramref name="blend"/>, given what is under, the texel's
        /// tone decoded to linear, and its alpha.
        /// </summary>
        private static void Draw(
            Vector3[] image, int width, int height, ArtImage sprite, QuarterTile? tile, Vector2 origin, Rect cellRect, int cell,
            Func<Vector3, double, float, Vector3> blend)
        {
            var xMin = (int)Math.Round(origin.x + cellRect.xMin * cell);
            var xMax = (int)Math.Round(origin.x + cellRect.xMax * cell);
            var yMin = (int)Math.Round(origin.y + cellRect.yMin * cell);
            var yMax = (int)Math.Round(origin.y + cellRect.yMax * cell);
            var centerX = (xMin + xMax) / 2.0;
            var centerY = (yMin + yMax) / 2.0;

            for (var y = Math.Max(0, yMin); y < Math.Min(height, yMax); y++)
            {
                for (var x = Math.Max(0, xMin); x < Math.Min(width, xMax); x++)
                {
                    // Back from the board to the sprite: undo the turn, then
                    // the flips, which act in the sprite's own frame first.
                    var dx = x + 0.5 - centerX;
                    var dy = y + 0.5 - centerY;
                    if (tile.HasValue)
                    {
                        if (tile.Value.IsRotated)
                        {
                            (dx, dy) = Unturn(dx, dy);
                        }

                        dx = tile.Value.FlipX ? -dx : dx;
                        dy = tile.Value.FlipY ? -dy : dy;
                    }

                    var sx = (int)Math.Floor(dx + sprite.Width / 2.0);
                    var sy = (int)Math.Floor(dy + sprite.Height / 2.0);
                    if (sx < 0 || sy < 0 || sx >= sprite.Width || sy >= sprite.Height)
                    {
                        continue;
                    }

                    var texel = sprite.Pixels[sy * sprite.Width + sx];
                    var index = y * width + x;
                    image[index] = blend(image[index], Decode(texel.r), texel.a / 255f);
                }
            }
        }

        /// <summary>
        /// Undoes <see cref="QuarterTile.EdgeAlongYRotationDegrees"/>, a
        /// quarter turn, exactly.
        /// </summary>
        private static (double x, double y) Unturn(double x, double y)
        {
            var radians = -QuarterTile.EdgeAlongYRotationDegrees * Math.PI / 180.0;
            var cos = Math.Round(Math.Cos(radians));
            var sin = Math.Round(Math.Sin(radians));
            return (x * cos - y * sin, x * sin + y * cos);
        }

        private static Vector3 Linear(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return new Vector3((float)Decode(color.r * 255f), (float)Decode(color.g * 255f), (float)Decode(color.b * 255f));
        }

        /// <summary>An sRGB byte as linear light.</summary>
        private static double Decode(double srgbByte)
        {
            var c = srgbByte / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>Linear light as an sRGB byte, clipped.</summary>
        private static byte Byte(float linear)
        {
            var c = Math.Min(1.0, Math.Max(0.0, linear));
            var srgb = c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
            return (byte)Math.Round(srgb * 255.0, MidpointRounding.AwayFromZero);
        }
    }
}
