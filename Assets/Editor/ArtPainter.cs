using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Editor
{
    /// <summary>The sprites <see cref="ArtGenerator"/> writes; each name is its PNG's file name.</summary>
    public enum ArtSprite
    {
        QuarterOuter,
        QuarterEdge,
        QuarterFill,
        QuarterConcave,
        Studs,
        StudGloss,
        Floor,
        GateArrow,
        AxisArrow,
        BackgroundRamp,
        Vignette
    }

    /// <summary>One painted sprite: its pixels, bottom row first, and its 9-slice border.</summary>
    public sealed class ArtImage
    {
        private readonly Color32[] pixels;

        /// <summary>An image of <paramref name="width"/> x <paramref name="height"/> pixels.</summary>
        public ArtImage(int width, int height, Color32[] pixels, Vector4 border)
        {
            Width = width;
            Height = height;
            this.pixels = pixels;
            Border = border;
        }

        /// <summary>Width in pixels.</summary>
        public int Width { get; }

        /// <summary>Height in pixels.</summary>
        public int Height { get; }

        /// <summary>Row by row from the bottom, as <c>Texture2D.SetPixels32</c> takes them.</summary>
        public IReadOnlyList<Color32> Pixels => pixels;

        /// <summary>The sprite's 9-slice border in pixels (left, bottom, right, top); zero when it is not sliced.</summary>
        public Vector4 Border { get; }

        /// <summary>The pixel array itself, for encoding without a copy.</summary>
        internal Color32[] PixelArray => pixels;
    }

    /// <summary>
    /// Paints every generated sprite of Module 15 from an <see cref="ArtRecipe"/>
    /// alone, in greyscale plus alpha, for the runtime to tint (D46).
    /// </summary>
    /// <remarks>
    /// <para><b>Shapes</b> are signed distance functions evaluated at pixel
    /// centres: coverage is <c>0.5 − d / antiAlias</c>, clamped, and tone is a
    /// function of the depth inside the shape — an outline, then a rim
    /// highlight fading into the face. A piece's shading therefore depends only
    /// on the distance to its boundary, so mirroring or rotating it onto
    /// another quarter never turns its lighting the wrong way. Fully
    /// transparent pixels keep the edge's tone in RGB, so filtering never pulls
    /// a dark fringe into the edge.</para>
    /// <para><b>Quarter pieces</b> are drawn for the top-right quarter, in
    /// quarter-local pixels whose origin is the cell's centre: the cell's
    /// corner is at <c>(q, q)</c> and the outline runs at <c>e = q − gap/2</c>.
    /// Every seam between two pieces carries the same pixels on both sides, as
    /// long as the recipe passes <see cref="ArtRecipe.Problems"/>.</para>
    /// <para><b>Determinism.</b> Only double-precision <c>+ − × ÷</c>,
    /// <c>Math.Sqrt</c>, <c>Min</c> and <c>Max</c> are used — no <c>Mathf</c>,
    /// <c>Pow</c> or <c>Exp</c>, no randomness, no clock — and each value is
    /// rounded to a byte with an explicit midpoint rule. The same recipe
    /// always paints the same bytes.</para>
    /// </remarks>
    public static class ArtPainter
    {
        /// <summary>
        /// Width of the background ramp. Not a look parameter: the ramp only
        /// varies vertically, and two columns let bilinear filtering stretch it
        /// without sampling a single column's edge.
        /// </summary>
        private const int RampWidthPixels = 2;

        /// <summary>Every sprite, in the order the generator writes them.</summary>
        public static IReadOnlyList<ArtSprite> All { get; } = (ArtSprite[])Enum.GetValues(typeof(ArtSprite));

        /// <summary>Paints <paramref name="sprite"/> from <paramref name="recipe"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="recipe"/> is null.</exception>
        public static ArtImage Paint(ArtRecipe recipe, ArtSprite sprite)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            switch (sprite)
            {
                case ArtSprite.QuarterOuter:
                case ArtSprite.QuarterEdge:
                case ArtSprite.QuarterFill:
                case ArtSprite.QuarterConcave:
                    return PaintQuarter(recipe, sprite);
                case ArtSprite.Studs:
                    return PaintStuds(recipe);
                case ArtSprite.StudGloss:
                    return PaintGloss(recipe);
                case ArtSprite.Floor:
                    return PaintFloor(recipe);
                case ArtSprite.GateArrow:
                    return PaintGateArrow(recipe);
                case ArtSprite.AxisArrow:
                    return PaintAxisArrow(recipe);
                case ArtSprite.BackgroundRamp:
                    return PaintRamp(recipe);
                default:
                    return PaintVignette(recipe);
            }
        }

        /// <summary>
        /// Encodes <paramref name="image"/> as PNG bytes. The same pixels always
        /// encode to the same bytes within one Unity version.
        /// </summary>
        public static byte[] EncodePng(ArtImage image)
        {
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(image.PixelArray);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static ArtImage PaintQuarter(ArtRecipe recipe, ArtSprite sprite)
        {
            double q = recipe.QuarterPixels;
            var e = q - recipe.BlockGapPixels / 2.0;
            double r = recipe.CornerRadiusPixels;
            double rc = recipe.ConcaveRadiusPixels;

            return Paint(recipe.QuarterPixels, recipe.QuarterPixels, Vector4.zero, (x, y) =>
            {
                double sd;
                switch (sprite)
                {
                    case ArtSprite.QuarterOuter:
                        sd = SdLowerQuadrant(x, y, e - r, e - r) - r;
                        break;
                    case ArtSprite.QuarterEdge:
                        sd = y - e;
                        break;
                    case ArtSprite.QuarterFill:
                        // Deeper than any bevel reaches: the whole quarter is face.
                        sd = -2.0 * q;
                        break;
                    default:
                        sd = rc - SdUpperQuadrant(x, y, e + rc, e + rc);
                        break;
                }

                return (FaceTone(recipe, -sd), Coverage(recipe, sd));
            });
        }

        /// <summary>A cell's 2×2 studs, centred on the cell, each with a darker ring at its edge.</summary>
        private static ArtImage PaintStuds(ArtRecipe recipe)
        {
            var radius = recipe.StudDiameterPixels / 2.0;
            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var (cx, cy) = NearestStud(recipe, x, y);
                var sd = Length(x - cx, y - cy) - radius;
                var inStud = Clamp01(-sd - recipe.StudRingWidthPixels + 0.5);
                var tone = Lerp(recipe.StudRingTone, recipe.StudTone, inStud);
                return (tone, Coverage(recipe, sd));
            });
        }

        /// <summary>One soft white highlight per stud, offset from its centre.</summary>
        private static ArtImage PaintGloss(ArtRecipe recipe)
        {
            var radius = recipe.GlossDiameterPixels / 2.0;
            var softness = Math.Max(recipe.GlossSoftnessPixels, recipe.AntiAliasPixels);
            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var (cx, cy) = NearestStud(recipe, x, y);
                var d = Length(x - cx - recipe.GlossOffsetPixels.x, y - cy - recipe.GlossOffsetPixels.y);
                return (1.0, recipe.GlossAlpha * Clamp01(0.5 + (radius - d) / softness));
            });
        }

        /// <summary>An opaque tile with half the grid line along each of its edges.</summary>
        private static ArtImage PaintFloor(ArtRecipe recipe)
        {
            double size = recipe.CellPixels;
            var halfLine = recipe.GridLinePixels / 2.0;
            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var toEdge = Math.Min(Math.Min(x, size - x), Math.Min(y, size - y));
                var line = Clamp01(halfLine - toEdge + 0.5);
                return (Lerp(recipe.FloorTone, recipe.GridLineTone, line), 1.0);
            });
        }

        /// <summary>A white arrow pointing up, centred in its square.</summary>
        private static ArtImage PaintGateArrow(ArtRecipe recipe)
        {
            double s = recipe.GateArrowPixels;
            var length = recipe.GateArrowLength * s;
            var headLength = recipe.GateArrowHeadLength * s;
            var tip = length / 2.0;
            var headBase = tip - headLength;
            var tail = -tip;
            var halfHead = recipe.GateArrowHeadWidth * s / 2.0;
            var halfShaft = recipe.GateArrowShaftWidth * s / 2.0;

            return Paint(recipe.GateArrowPixels, recipe.GateArrowPixels, Vector4.zero, (x, y) =>
            {
                var u = x - s / 2.0;
                var v = y - s / 2.0;
                var head = SdTriangle(v, u, headBase, tip, halfHead);
                var shaft = SdBox(u, v - (tail + headBase) / 2.0, halfShaft, (headBase - tail) / 2.0);
                return (1.0, Coverage(recipe, Math.Min(head, shaft)));
            });
        }

        /// <summary>
        /// A white double-headed horizontal arrow. The 9-slice border covers
        /// each head and its anti-aliased base, so the stretched middle holds
        /// only the uniform shaft.
        /// </summary>
        private static ArtImage PaintAxisArrow(ArtRecipe recipe)
        {
            double w = recipe.AxisArrowLengthPixels;
            double h = recipe.AxisArrowThicknessPixels;
            var aa = recipe.AntiAliasPixels;
            var tip = w / 2.0 - aa;
            var headBase = w / 2.0 - recipe.AxisArrowHeadLengthPixels;
            var halfHead = h / 2.0 - aa;
            var halfShaft = recipe.AxisArrowShaftPixels / 2.0;
            var border = (float)Math.Ceiling(recipe.AxisArrowHeadLengthPixels + aa);

            return Paint(recipe.AxisArrowLengthPixels, recipe.AxisArrowThicknessPixels, new Vector4(border, 0f, border, 0f), (x, y) =>
            {
                var u = x - w / 2.0;
                var v = y - h / 2.0;
                var right = SdTriangle(u, v, headBase, tip, halfHead);
                var left = SdTriangle(-u, v, headBase, tip, halfHead);
                var shaft = SdBox(u, v, headBase, halfShaft);
                return (1.0, Coverage(recipe, Math.Min(shaft, Math.Min(left, right))));
            });
        }

        /// <summary>White, with alpha rising linearly from the bottom row to the top.</summary>
        private static ArtImage PaintRamp(ArtRecipe recipe)
        {
            double height = recipe.RampPixels;
            return Paint(RampWidthPixels, recipe.RampPixels, Vector4.zero, (x, y) => (1.0, y / height));
        }

        /// <summary>White, with alpha rising smoothly from the inner radius to the outer one.</summary>
        private static ArtImage PaintVignette(ArtRecipe recipe)
        {
            double size = recipe.VignettePixels;
            var half = size / 2.0;
            var toCorner = Length(half, half);
            double inner = recipe.VignetteInner;
            double outer = recipe.VignetteOuter;

            return Paint(recipe.VignettePixels, recipe.VignettePixels, Vector4.zero, (x, y) =>
            {
                var r = Length(x - half, y - half) / toCorner;
                return (1.0, Smooth(Clamp01((r - inner) / (outer - inner))));
            });
        }

        /// <summary>
        /// Evaluates <paramref name="shade"/> at every pixel centre — <c>(x + ½,
        /// y + ½)</c>, bottom row first — and quantizes its tone and alpha.
        /// </summary>
        private static ArtImage Paint(int width, int height, Vector4 border, Func<double, double, (double tone, double alpha)> shade)
        {
            var pixels = new Color32[width * height];
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var (tone, alpha) = shade(i + 0.5, j + 0.5);
                    var grey = ToByte(tone);
                    pixels[j * width + i] = new Color32(grey, grey, grey, ToByte(alpha));
                }
            }

            return new ArtImage(width, height, pixels, border);
        }

        /// <summary>
        /// The tone of a face pixel <paramref name="depth"/> pixels inside the
        /// outline (negative outside): the outline tone, blended over one pixel
        /// into a rim highlight that eases into the face tone.
        /// </summary>
        private static double FaceTone(ArtRecipe recipe, double depth)
        {
            double outline = recipe.OutlineWidthPixels;
            double rim = recipe.RimWidthPixels;
            var intoRim = rim > 0.0 ? Clamp01((depth - outline) / rim) : (depth >= outline ? 1.0 : 0.0);
            var bevel = Lerp(recipe.RimTone, recipe.FaceTone, Smooth(intoRim));
            return Lerp(recipe.OutlineTone, bevel, Clamp01(depth - outline + 0.5));
        }

        private static double Coverage(ArtRecipe recipe, double sd) => Clamp01(0.5 - sd / recipe.AntiAliasPixels);

        /// <summary>The centre of the stud nearest a cell-sprite pixel: one of four, spaced around the cell's centre.</summary>
        private static (double x, double y) NearestStud(ArtRecipe recipe, double x, double y)
        {
            var center = recipe.CellPixels / 2.0;
            var offset = recipe.StudSpacingPixels / 2.0;
            return (x < center ? center - offset : center + offset, y < center ? center - offset : center + offset);
        }

        /// <summary>Signed distance to the region <c>x ≤ cx, y ≤ cy</c>.</summary>
        private static double SdLowerQuadrant(double x, double y, double cx, double cy) =>
            SdQuadrant(x - cx, y - cy);

        /// <summary>Signed distance to the region <c>x ≥ cx, y ≥ cy</c>.</summary>
        private static double SdUpperQuadrant(double x, double y, double cx, double cy) =>
            SdQuadrant(cx - x, cy - y);

        /// <summary>Signed distance to the quadrant <c>dx ≤ 0, dy ≤ 0</c>.</summary>
        private static double SdQuadrant(double dx, double dy) =>
            Length(Math.Max(dx, 0.0), Math.Max(dy, 0.0)) + Math.Min(Math.Max(dx, dy), 0.0);

        /// <summary>Signed distance to an axis-aligned box centred on the origin with half extents <paramref name="hx"/>, <paramref name="hy"/>.</summary>
        private static double SdBox(double x, double y, double hx, double hy)
        {
            var dx = Math.Abs(x) - hx;
            var dy = Math.Abs(y) - hy;
            return Length(Math.Max(dx, 0.0), Math.Max(dy, 0.0)) + Math.Min(Math.Max(dx, dy), 0.0);
        }

        /// <summary>
        /// Signed distance, near the edges, to an isosceles triangle whose base
        /// lies across the axis at <paramref name="baseAt"/> with half width
        /// <paramref name="halfWidth"/>, and whose tip lies on the axis at
        /// <paramref name="tipAt"/>. The largest of the three edge-line
        /// distances: exact along the edges, which is all anti-aliasing reads.
        /// </summary>
        private static double SdTriangle(double along, double across, double baseAt, double tipAt, double halfWidth)
        {
            var height = tipAt - baseAt;
            var slant = Length(height, halfWidth);
            var side = (height * Math.Abs(across) + halfWidth * (along - tipAt)) / slant;
            return Math.Max(baseAt - along, side);
        }

        private static double Length(double x, double y) => Math.Sqrt(x * x + y * y);

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static double Clamp01(double value) => value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;

        /// <summary>Smoothstep on <c>[0, 1]</c>.</summary>
        private static double Smooth(double t) => t * t * (3.0 - 2.0 * t);

        private static byte ToByte(double value) =>
            (byte)Math.Round(Clamp01(value) * 255.0, MidpointRounding.AwayFromZero);
    }
}
