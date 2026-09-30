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
        Vignette,
        Frost,
        RoundedRect,
        Ring,
        Chain,
        Padlock,
        KeyBody,
        KeyGem,
        ShutterSlats,
        DoorPanel,
        Clock,
        Restart,
        Cube,
        Shard,
        GateGlow
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
    /// Paints every generated sprite of Modules 15, 16, 17 and 18 from an
    /// <see cref="ArtRecipe"/> alone, in greyscale plus alpha, for the runtime
    /// to tint (D46).
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
    /// <c>Math.Sqrt</c>, <c>Abs</c>, <c>Floor</c>, <c>Min</c> and <c>Max</c> are
    /// used, all exactly rounded — no <c>Mathf</c>,
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
                case ArtSprite.Vignette:
                    return PaintVignette(recipe);
                case ArtSprite.Frost:
                    return PaintFrost(recipe);
                case ArtSprite.RoundedRect:
                    return PaintRoundedRect(recipe);
                case ArtSprite.Ring:
                    return PaintRing(recipe);
                case ArtSprite.Chain:
                    return PaintChain(recipe);
                case ArtSprite.Padlock:
                    return PaintPadlock(recipe);
                case ArtSprite.KeyBody:
                    return PaintKeyBody(recipe);
                case ArtSprite.KeyGem:
                    return PaintKeyGem(recipe);
                case ArtSprite.ShutterSlats:
                    return PaintSlats(recipe);
                case ArtSprite.Clock:
                    return PaintClock(recipe);
                case ArtSprite.Restart:
                    return PaintRestart(recipe);
                case ArtSprite.Cube:
                    return PaintCube(recipe);
                case ArtSprite.Shard:
                    return PaintShard(recipe);
                case ArtSprite.GateGlow:
                    return PaintGateGlow(recipe);
                default:
                    return PaintDoorPanel(recipe);
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
        /// Frost for one cell of ice: pale streaks on the diagonal, side by
        /// side, clipped to a box inset from every side so frost never leaves a
        /// block's rounded face. The clip also shortens the outer streaks.
        /// White; its colour and strength come from the runtime.
        /// </summary>
        private static ArtImage PaintFrost(ArtRecipe recipe)
        {
            var center = recipe.CellPixels / 2.0;
            var clipHalf = center - recipe.FrostInsetPixels;
            var halfWidth = recipe.FrostStreakWidthPixels / 2.0;
            var halfLength = recipe.FrostStreakLengthPixels / 2.0;
            var count = recipe.FrostStreakCount;
            var diagonal = 1.0 / Math.Sqrt(2.0);

            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var along = (x - center + (y - center)) * diagonal;
                var across = (y - center - (x - center)) * diagonal;
                var nearest = double.MaxValue;
                for (var i = 0; i < count; i++)
                {
                    var offset = (i - (count - 1) / 2.0) * recipe.FrostStreakSpacingPixels;
                    var sd = Length(Math.Max(Math.Abs(along) - halfLength, 0.0), across - offset) - halfWidth;
                    nearest = Math.Min(nearest, sd);
                }

                var clip = SdBox(x - center, y - center, clipHalf, clipHalf);
                return (1.0, recipe.FrostAlpha * Coverage(recipe, nearest) * Coverage(recipe, clip));
            });
        }

        /// <summary>
        /// A rounded box shaded like a block's face — outline, then a rim
        /// highlight easing into the face — reaching the sprite's edges. It is
        /// 9-sliced at <see cref="ArtRecipe.PanelBorderPixels"/> on every side,
        /// which holds the corner and the bevel, so only straight profile
        /// stretches.
        /// </summary>
        private static ArtImage PaintRoundedRect(ArtRecipe recipe)
        {
            var half = recipe.PanelPixels / 2.0;
            double radius = recipe.PanelCornerRadiusPixels;
            float border = recipe.PanelBorderPixels;

            return Paint(recipe.PanelPixels, recipe.PanelPixels, new Vector4(border, border, border, border), (x, y) =>
            {
                var sd = SdRoundedBox(x - half, y - half, half, half, radius);
                return (FaceTone(recipe, -sd), Coverage(recipe, sd));
            });
        }

        /// <summary>
        /// A white outline around the sprite's edges, as wide as its 9-slice
        /// border, with its outer corners rounded by that width. The runtime
        /// scales it so the border draws at the thickness it asks for.
        /// </summary>
        private static ArtImage PaintRing(ArtRecipe recipe)
        {
            var half = recipe.RingPixels / 2.0;
            double width = recipe.RingWidthPixels;
            float border = recipe.RingWidthPixels;

            return Paint(recipe.RingPixels, recipe.RingPixels, new Vector4(border, border, border, border), (x, y) =>
            {
                var outer = SdRoundedBox(x - half, y - half, half, half, width);
                var inner = SdBox(x - half, y - half, half - width, half - width);
                return (1.0, Coverage(recipe, outer) * (1.0 - Coverage(recipe, inner)));
            });
        }

        /// <summary>
        /// One period of chain along x: a link seen face-on — an open oval — in
        /// the middle, and a link seen edge-on — a short bar — straddling both
        /// ends, drawn over it. The pattern is symmetric about the period's
        /// ends, so tiles meet without a seam.
        /// </summary>
        private static ArtImage PaintChain(ArtRecipe recipe)
        {
            double period = recipe.ChainPeriodPixels;
            var middle = recipe.ChainThicknessPixels / 2.0;
            double wall = recipe.ChainLinkWallPixels;
            var halfWall = wall / 2.0;
            var linkHalfLength = recipe.ChainLinkLengthPixels / 2.0;
            var edgeHalfLength = recipe.ChainEdgeLinkLengthPixels / 2.0;

            (double tone, double alpha) Metal(double sd) =>
                (Lerp(recipe.ChainShadeTone, recipe.ChainTone, Clamp01(-sd / halfWall)), Coverage(recipe, sd));

            return Paint(recipe.ChainPeriodPixels, recipe.ChainThicknessPixels, Vector4.zero, (x, y) =>
            {
                var v = y - middle;
                var outline = SdRoundedBox(x - period / 2.0, v, linkHalfLength, middle, middle);
                var faceOn = Math.Abs(outline + halfWall) - halfWall;

                var edgeOn = Math.Min(
                    SdRoundedBox(x, v, edgeHalfLength, halfWall, halfWall),
                    SdRoundedBox(x - period, v, edgeHalfLength, halfWall, halfWall));

                return Over(Metal(edgeOn), Metal(faceOn));
            });
        }

        /// <summary>
        /// A padlock: a bevelled body in the lower part and a shackle arching
        /// over it, its legs running down behind the body. Body and shackle
        /// together are centred vertically.
        /// </summary>
        private static ArtImage PaintPadlock(ArtRecipe recipe)
        {
            double size = recipe.PadlockPixels;
            var centerX = size / 2.0;
            var bodyHalfWidth = recipe.PadlockBodyWidth * size / 2.0;
            var bodyHalfHeight = recipe.PadlockBodyHeight * size / 2.0;
            var radius = recipe.PadlockShackleRadius * size;
            var shackleHalf = recipe.PadlockShackleThickness * size / 2.0;
            var bottom = (size - (2.0 * bodyHalfHeight + radius + shackleHalf)) / 2.0;
            var bodyCenterY = bottom + bodyHalfHeight;
            var bodyTop = bottom + 2.0 * bodyHalfHeight;

            return Paint(recipe.PadlockPixels, recipe.PadlockPixels, Vector4.zero, (x, y) =>
            {
                var body = SdRoundedBox(x - centerX, y - bodyCenterY, bodyHalfWidth, bodyHalfHeight, recipe.PadlockBodyCornerPixels);

                // Above the body's top the shackle is an arc; below it, two
                // straight legs ending at the body's middle.
                var shackle = y >= bodyTop
                    ? Math.Abs(Length(x - centerX, y - bodyTop) - radius) - shackleHalf
                    : Length(Math.Abs(Math.Abs(x - centerX) - radius), Math.Max(bodyCenterY - y, 0.0)) - shackleHalf;
                var shackleTone = Lerp(recipe.OutlineTone, recipe.PadlockShackleTone, Clamp01(-shackle / (shackleHalf / 2.0)));

                return Over(
                    (FaceTone(recipe, -body), Coverage(recipe, body)),
                    (shackleTone, Coverage(recipe, shackle)));
            });
        }

        /// <summary>
        /// A key lying horizontally: a round bow on the left with a hole for the
        /// gem, a shaft to the right edge, and two teeth under its end. Shaded
        /// like a block's face.
        /// </summary>
        private static ArtImage PaintKeyBody(ArtRecipe recipe)
        {
            var (bowX, middle) = KeyBowCenter(recipe);
            var bowRadius = recipe.KeyBowDiameterPixels / 2.0;
            var holeRadius = recipe.KeyHoleDiameterPixels / 2.0;
            var shaftHalf = recipe.KeyShaftThicknessPixels / 2.0;
            var shaftEnd = recipe.KeyWidthPixels - recipe.AntiAliasPixels;
            var toothWidth = recipe.KeyToothWidthPixels;
            var toothHalfLength = (recipe.KeyToothLengthPixels + shaftHalf) / 2.0;
            var toothCenterY = middle - (shaftHalf + recipe.KeyToothLengthPixels) / 2.0;

            return Paint(recipe.KeyWidthPixels, recipe.KeyHeightPixels, Vector4.zero, (x, y) =>
            {
                var bow = Length(x - bowX, y - middle) - bowRadius;
                var shaft = SdBox(x - (bowX + shaftEnd) / 2.0, y - middle, (shaftEnd - bowX) / 2.0, shaftHalf);

                // Two teeth, the first flush with the shaft's end, one tooth
                // width apart; each reaches up into the shaft so they join.
                var teeth = Math.Min(
                    SdBox(x - (shaftEnd - toothWidth / 2.0), y - toothCenterY, toothWidth / 2.0, toothHalfLength),
                    SdBox(x - (shaftEnd - 2.5 * toothWidth), y - toothCenterY, toothWidth / 2.0, toothHalfLength));

                var hole = holeRadius - Length(x - bowX, y - middle);
                var sd = Math.Max(Math.Min(bow, Math.Min(shaft, teeth)), hole);
                return (FaceTone(recipe, -sd), Coverage(recipe, sd));
            });
        }

        /// <summary>
        /// The gem in the key's bow, on the same canvas as the key so the two
        /// line up: a disc, lighter towards its centre.
        /// </summary>
        private static ArtImage PaintKeyGem(ArtRecipe recipe)
        {
            var (bowX, middle) = KeyBowCenter(recipe);
            var radius = recipe.KeyGemDiameterPixels / 2.0;

            return Paint(recipe.KeyWidthPixels, recipe.KeyHeightPixels, Vector4.zero, (x, y) =>
            {
                var sd = Length(x - bowX, y - middle) - radius;
                return (Lerp(recipe.KeyGemEdgeTone, recipe.KeyGemTone, Clamp01(-sd / radius)), Coverage(recipe, sd));
            });
        }

        /// <summary>
        /// One opaque cell of shutter: horizontal slats, each light at its top
        /// and shaded at its bottom, with a dark groove between them. The
        /// grooves fall on the cell's top and bottom edges, so tiles meet
        /// without a seam.
        /// </summary>
        private static ArtImage PaintSlats(ArtRecipe recipe)
        {
            var pitch = (double)recipe.CellPixels / recipe.SlatsPerCell;
            var halfGap = recipe.SlatGapPixels / 2.0;

            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var inSlat = y - pitch * Math.Floor(y / pitch);
                var toGroove = Math.Min(inSlat, pitch - inSlat);
                var groove = Clamp01(halfGap - toGroove + 0.5);
                var slat = Lerp(recipe.SlatShadeTone, recipe.SlatTone, inSlat / pitch);
                return (Lerp(slat, recipe.SlatGapTone, groove), 1.0);
            });
        }

        /// <summary>
        /// One opaque cell of lift door: a flat panel with faint vertical lines,
        /// one on each side edge and the rest evenly between, so tiles meet
        /// without a seam.
        /// </summary>
        private static ArtImage PaintDoorPanel(ArtRecipe recipe)
        {
            var pitch = (double)recipe.CellPixels / recipe.DoorLinesPerCell;
            var halfLine = recipe.DoorLineWidthPixels / 2.0;

            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var inPanel = x - pitch * Math.Floor(x / pitch);
                var toLine = Math.Min(inPanel, pitch - inPanel);
                var line = Clamp01(halfLine - toLine + 0.5);
                return (Lerp(recipe.DoorTone, recipe.DoorLineTone, line), 1.0);
            });
        }

        /// <summary>
        /// The HUD's clock, in white: a round rim, its outer edge half the
        /// anti-aliased edge inside the sprite, and two round-ended hands from
        /// the centre — the minute hand to 12 o'clock, the hour hand to 3.
        /// Both hands lie on the axes, so no angle is ever computed.
        /// </summary>
        private static ArtImage PaintClock(ArtRecipe recipe)
        {
            double size = recipe.ClockPixels;
            var center = size / 2.0;
            var rimHalf = recipe.ClockRimThickness * size / 2.0;
            var rimMiddle = center - recipe.AntiAliasPixels - rimHalf;
            var handHalf = recipe.ClockHandThickness * size / 2.0;
            var minute = recipe.ClockMinuteHandLength * size;
            var hour = recipe.ClockHourHandLength * size;

            return Paint(recipe.ClockPixels, recipe.ClockPixels, Vector4.zero, (x, y) =>
            {
                var dx = x - center;
                var dy = y - center;
                var rim = Math.Abs(Length(dx, dy) - rimMiddle) - rimHalf;

                // Each hand is a segment from the centre along one axis,
                // thickened into a capsule.
                var minuteHand = Length(dx, dy - Clamp(dy, 0.0, minute)) - handHalf;
                var hourHand = Length(dx - Clamp(dx, 0.0, hour), dy) - handHalf;

                var sd = Math.Min(rim, Math.Min(minuteHand, hourHand));
                return (1.0, Coverage(recipe, sd));
            });
        }

        /// <summary>
        /// The HUD's restart arrow, in white, turning clockwise: a ring with its
        /// upper-right quarter cut away, so it runs from 3 o'clock through 6
        /// and 9 to 12, and a triangular head at the 12 o'clock end pointing
        /// right, along the direction of travel. The cut lies on the axes, so
        /// no angle is ever computed.
        /// </summary>
        private static ArtImage PaintRestart(ArtRecipe recipe)
        {
            double size = recipe.RestartPixels;
            var center = size / 2.0;
            var radius = recipe.RestartRadius * size;
            var strokeHalf = recipe.RestartThickness * size / 2.0;
            var headHalf = recipe.RestartHeadWidth * size / 2.0;
            var headLength = recipe.RestartHeadLength * size;

            return Paint(recipe.RestartPixels, recipe.RestartPixels, Vector4.zero, (x, y) =>
            {
                var ring = Math.Abs(Length(x - center, y - center) - radius) - strokeHalf;
                var arc = Math.Max(ring, -SdUpperQuadrant(x, y, center, center));
                var head = SdTriangle(x - center, y - (center + radius), 0.0, headLength, headHalf);

                return (1.0, Coverage(recipe, Math.Min(arc, head)));
            });
        }

        /// <summary>
        /// One cube of a destroyed block's burst: a rounded box with a light
        /// bevel inside its edge, easing into the face, and a soft highlight
        /// toward its top left, like a stud's gloss.
        /// </summary>
        private static ArtImage PaintCube(ArtRecipe recipe)
        {
            var half = recipe.CubePixels / 2.0;
            var extent = half - recipe.AntiAliasPixels;
            var highlightX = half + recipe.CubeHighlightOffsetPixels.x;
            var highlightY = half + recipe.CubeHighlightOffsetPixels.y;
            var highlightRadius = recipe.CubeHighlightDiameterPixels / 2.0;
            var softness = Math.Max(recipe.CubeHighlightSoftnessPixels, recipe.AntiAliasPixels);

            return Paint(recipe.CubePixels, recipe.CubePixels, Vector4.zero, (x, y) =>
            {
                var sd = SdRoundedBox(x - half, y - half, extent, extent, recipe.CubeCornerPixels);
                var intoFace = recipe.CubeRimPixels > 0.0 ? Clamp01(-sd / recipe.CubeRimPixels) : 1.0;
                var tone = Lerp(recipe.CubeRimTone, recipe.CubeFaceTone, Smooth(intoFace));
                var highlight = Clamp01(0.5 + (highlightRadius - Length(x - highlightX, y - highlightY)) / softness);
                return (Lerp(tone, recipe.CubeHighlightTone, highlight), Coverage(recipe, sd));
            });
        }

        /// <summary>
        /// One ice shard: a convex four-cornered splinter with a lighter frost
        /// streak from its first corner to its third. Its signed distance is the
        /// largest distance past any of its edges — exact along the edges,
        /// which is all anti-aliasing reads.
        /// </summary>
        private static ArtImage PaintShard(ArtRecipe recipe)
        {
            double size = recipe.ShardPixels;
            var points = recipe.ShardPoints;
            var xs = new double[points.Count];
            var ys = new double[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                xs[i] = points[i].x * size;
                ys[i] = points[i].y * size;
            }

            var halfStreak = recipe.ShardStreakWidthPixels / 2.0;

            return Paint(recipe.ShardPixels, recipe.ShardPixels, Vector4.zero, (x, y) =>
            {
                var sd = double.NegativeInfinity;
                for (var i = 0; i < xs.Length; i++)
                {
                    var j = (i + 1) % xs.Length;
                    var dx = xs[j] - xs[i];
                    var dy = ys[j] - ys[i];

                    // Counter-clockwise, so the outward normal is (dy, −dx).
                    sd = Math.Max(sd, ((x - xs[i]) * dy - (y - ys[i]) * dx) / Length(dx, dy));
                }

                var toStreak = DistanceToSegment(x, y, xs[0], ys[0], xs[2], ys[2]);
                var streak = Clamp01(halfStreak - toStreak + 0.5);
                return (Lerp(recipe.ShardFaceTone, recipe.ShardStreakTone, streak), Coverage(recipe, sd));
            });
        }

        /// <summary>
        /// The glow inside a gate a block is passing: white, opaque along its
        /// bottom rows for the held fraction of its height, then easing to
        /// nothing at its top. Only the height varies, so it is as narrow as the
        /// background ramp and stretched along the gate.
        /// </summary>
        private static ArtImage PaintGateGlow(ArtRecipe recipe)
        {
            double height = recipe.GateGlowPixels;
            double hold = recipe.GateGlowHold;
            return Paint(RampWidthPixels, recipe.GateGlowPixels, Vector4.zero, (x, y) =>
            {
                var intoFade = Clamp01((y / height - hold) / (1.0 - hold));
                return (1.0, 1.0 - Smooth(intoFade));
            });
        }

        /// <summary>The distance from <c>(x, y)</c> to the segment from <c>(ax, ay)</c> to <c>(bx, by)</c>.</summary>
        private static double DistanceToSegment(double x, double y, double ax, double ay, double bx, double by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var lengthSquared = dx * dx + dy * dy;
            var t = lengthSquared > 0.0 ? Clamp01(((x - ax) * dx + (y - ay) * dy) / lengthSquared) : 0.0;
            return Length(x - (ax + t * dx), y - (ay + t * dy));
        }

        /// <summary>The centre of the key's bow, in pixels: as far in from the left edge as the key is half high.</summary>
        private static (double x, double y) KeyBowCenter(ArtRecipe recipe)
        {
            var middle = recipe.KeyHeightPixels / 2.0;
            return (middle, middle);
        }

        /// <summary>
        /// <paramref name="top"/> composited over <paramref name="bottom"/>:
        /// alpha adds as paint does, and the tone is their alpha-weighted blend.
        /// </summary>
        private static (double tone, double alpha) Over((double tone, double alpha) top, (double tone, double alpha) bottom)
        {
            var bottomShown = bottom.alpha * (1.0 - top.alpha);
            var alpha = top.alpha + bottomShown;
            if (alpha <= 0.0)
            {
                // Keep the upper shape's tone in the transparent fringe, so
                // filtering pulls no dark edge in.
                return (top.tone, 0.0);
            }

            return ((top.tone * top.alpha + bottom.tone * bottomShown) / alpha, alpha);
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
        /// Signed distance to a box centred on the origin with half extents
        /// <paramref name="hx"/>, <paramref name="hy"/> whose corners are
        /// rounded by <paramref name="radius"/>, capped at the smaller half extent.
        /// </summary>
        private static double SdRoundedBox(double x, double y, double hx, double hy, double radius)
        {
            var r = Math.Min(radius, Math.Min(hx, hy));
            return SdBox(x, y, hx - r, hy - r) - r;
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

        private static double Clamp01(double value) => Clamp(value, 0.0, 1.0);

        private static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;

        /// <summary>Smoothstep on <c>[0, 1]</c>.</summary>
        private static double Smooth(double t) => t * t * (3.0 - 2.0 * t);

        private static byte ToByte(double value) =>
            (byte)Math.Round(Clamp01(value) * 255.0, MidpointRounding.AwayFromZero);
    }
}
