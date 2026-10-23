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
        ContourOuter,
        ContourEdge,
        ContourConcave,
        LightOuterTopLeft,
        LightOuterTopRight,
        LightOuterBottomLeft,
        LightOuterBottomRight,
        LightConcaveTopLeft,
        LightConcaveTopRight,
        LightConcaveBottomLeft,
        LightConcaveBottomRight,
        LightEdgeTop,
        LightEdgeBottom,
        LightEdgeLeft,
        LightEdgeRight,
        Studs,
        StudGloss,
        SingleStud,
        SingleStudGloss,
        Floor,
        GateArrow,
        GateArrowOutline,
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
        GateGlow,
        Sparkle
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
    /// Paints every generated sprite of Modules 15, 16, 17, 18, 19 and 46 from an
    /// <see cref="ArtRecipe"/> alone, in greyscale plus alpha, for the runtime
    /// to tint (D46).
    /// </summary>
    /// <remarks>
    /// <para><b>Shapes</b> are signed distance functions evaluated at pixel
    /// centres: coverage is <c>0.5 − d / antiAlias</c>, clamped, and tone is a
    /// function of the depth inside the shape. A face piece (Module 46) is
    /// plain white; its outline is a separate contour piece drawn untinted
    /// under it, and its lit and shaded edges a separate light piece drawn
    /// untinted over it. Face and contour depend only on the distance to the
    /// boundary, so mirroring or rotating them never turns anything the wrong
    /// way; a light piece also depends on which way its edge faces the light
    /// once posed, so there is one per corner of a cell and one per side for
    /// an edge, and the runtime picks the one for the quarter's place. Fully
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
    /// rounded to a byte with an explicit midpoint rule. The one exception,
    /// the light's direction, takes a cosine and a sine once and rounds them
    /// to nine decimals (<see cref="TowardLight"/>). The same recipe always
    /// paints the same bytes.</para>
    /// </remarks>
    public static class ArtPainter
    {
        /// <summary>
        /// Width of the background ramp. Not a look parameter: the ramp only
        /// varies vertically, and two columns let bilinear filtering stretch it
        /// without sampling a single column's edge.
        /// </summary>
        private const int RampWidthPixels = 2;

        /// <summary>
        /// The decimals the light's direction is rounded to (<see cref="TowardLight"/>).
        /// Not a look parameter: it only keeps a platform's last bit out of the pixels.
        /// </summary>
        private const int LightDirectionDecimals = 9;

        /// <summary>The four shapes a quarter piece takes (Module 15), whatever layer it is painted for.</summary>
        private enum QuarterShape
        {
            Outer,
            Edge,
            Fill,
            Concave
        }

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
                case ArtSprite.ContourOuter:
                case ArtSprite.ContourEdge:
                case ArtSprite.ContourConcave:
                    return PaintContour(recipe, sprite);
                case ArtSprite.LightOuterTopLeft:
                    return PaintLight(recipe, QuarterShape.Outer, -1, 1);
                case ArtSprite.LightOuterTopRight:
                    return PaintLight(recipe, QuarterShape.Outer, 1, 1);
                case ArtSprite.LightOuterBottomLeft:
                    return PaintLight(recipe, QuarterShape.Outer, -1, -1);
                case ArtSprite.LightOuterBottomRight:
                    return PaintLight(recipe, QuarterShape.Outer, 1, -1);
                case ArtSprite.LightConcaveTopLeft:
                    return PaintLight(recipe, QuarterShape.Concave, -1, 1);
                case ArtSprite.LightConcaveTopRight:
                    return PaintLight(recipe, QuarterShape.Concave, 1, 1);
                case ArtSprite.LightConcaveBottomLeft:
                    return PaintLight(recipe, QuarterShape.Concave, -1, -1);
                case ArtSprite.LightConcaveBottomRight:
                    return PaintLight(recipe, QuarterShape.Concave, 1, -1);
                case ArtSprite.LightEdgeTop:
                    return PaintLightEdge(recipe, 0, 1);
                case ArtSprite.LightEdgeBottom:
                    return PaintLightEdge(recipe, 0, -1);
                case ArtSprite.LightEdgeLeft:
                    return PaintLightEdge(recipe, -1, 0);
                case ArtSprite.LightEdgeRight:
                    return PaintLightEdge(recipe, 1, 0);
                case ArtSprite.Studs:
                    return PaintStuds(recipe, recipe.StudsPerCellSide);
                case ArtSprite.StudGloss:
                    return PaintGloss(recipe, recipe.StudsPerCellSide);
                case ArtSprite.SingleStud:
                    return PaintStuds(recipe, 1);
                case ArtSprite.SingleStudGloss:
                    return PaintGloss(recipe, 1);
                case ArtSprite.Floor:
                    return PaintFloor(recipe);
                case ArtSprite.GateArrow:
                    return PaintGateArrow(recipe, 0.0);
                case ArtSprite.GateArrowOutline:
                    return PaintGateArrow(recipe, recipe.GateArrowOutlinePixels);
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
                case ArtSprite.Sparkle:
                    return PaintSparkle(recipe);
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

        /// <summary>
        /// A face piece: the piece's silhouette pulled in by the outline, with
        /// its own corners sharing the silhouette's centres, so the contour
        /// under it shows as a ring of even width. White throughout, so the
        /// tint shows exactly; its lit and shaded edges are the light pieces'
        /// (<see cref="PaintLight"/>), drawn over it.
        /// </summary>
        private static ArtImage PaintQuarter(ArtRecipe recipe, ArtSprite sprite)
        {
            var shape = ShapeOf(sprite);
            return Paint(recipe.QuarterPixels, recipe.QuarterPixels, Vector4.zero, (x, y) =>
                (1.0, Coverage(recipe, FaceSd(recipe, shape, x, y))));
        }

        /// <summary>
        /// A contour piece: the piece's whole silhouette, white, for the runtime
        /// to draw untinted in the outline colour under the face. No fill piece
        /// is painted; a fill's contour would be the opaque white the face
        /// fill already is.
        /// </summary>
        private static ArtImage PaintContour(ArtRecipe recipe, ArtSprite sprite)
        {
            var shape = ShapeOf(sprite);
            return Paint(recipe.QuarterPixels, recipe.QuarterPixels, Vector4.zero, (x, y) =>
                (1.0, Coverage(recipe, SilhouetteSd(recipe, shape, x, y))));
        }

        /// <summary>
        /// A light piece over a face piece of the same kind, for the quarter at
        /// corner <c>(sx, sy)</c> of its cell (Module 46): white along the
        /// edges that face the light, black along those that face away, each
        /// band as strong as its edge faces that way and fading smoothly in
        /// from the edge. The sprite is drawn for the top-right quarter and
        /// mirrored onto its corner, so the edge's facing is worked out after
        /// that mirroring: a top-left corner is lit along both its edges, a
        /// bottom-right one shaded along both, and the other two turn from
        /// one to the other round their arc.
        /// </summary>
        private static ArtImage PaintLight(ArtRecipe recipe, QuarterShape shape, int sx, int sy)
        {
            var toward = TowardLight(recipe);
            return Paint(recipe.QuarterPixels, recipe.QuarterPixels, Vector4.zero, (x, y) =>
            {
                var (nx, ny) = OutwardNormal(recipe, shape, x, y);
                return LightPixel(recipe, FaceSd(recipe, shape, x, y), Facing(sx * nx, sy * ny, toward));
            });
        }

        /// <summary>
        /// A light piece over an edge piece whose boundary faces
        /// <c>(wx, wy)</c> once posed: the edge sprite's band, of one
        /// strength along the whole edge.
        /// </summary>
        private static ArtImage PaintLightEdge(ArtRecipe recipe, int wx, int wy)
        {
            var facing = Facing(wx, wy, TowardLight(recipe));
            return Paint(recipe.QuarterPixels, recipe.QuarterPixels, Vector4.zero, (x, y) =>
                LightPixel(recipe, FaceSd(recipe, QuarterShape.Edge, x, y), facing));
        }

        /// <summary>
        /// One light-piece pixel <c>−sd</c> inside the face's edge: white for a
        /// lit edge (<paramref name="facing"/> above 0), black for a shaded
        /// one, its alpha the band's strength times how squarely the edge
        /// faces, easing to nothing across the band's width.
        /// </summary>
        private static (double tone, double alpha) LightPixel(ArtRecipe recipe, double sd, double facing)
        {
            var depth = -sd;
            var isLit = facing >= 0.0;
            double band = isLit ? recipe.LightBandPixels : recipe.ShadeBandPixels;
            double strength = isLit ? recipe.LightAlpha : recipe.ShadeAlpha;
            var fade = 1.0 - Smooth(Clamp01(depth / band));
            return (isLit ? 1.0 : 0.0, Coverage(recipe, sd) * strength * Math.Abs(facing) * fade);
        }

        /// <summary>
        /// How squarely an edge with outward normal <c>(nx, ny)</c> faces the
        /// light, from 1 (lit) to −1 (shaded): their dot product, scaled so
        /// that the straight edges nearest the light — the top and the left,
        /// from the top left — count fully.
        /// </summary>
        private static double Facing(double nx, double ny, (double x, double y) toward)
        {
            var nearest = Math.Max(Math.Abs(toward.x), Math.Abs(toward.y));
            return Clamp((nx * toward.x + ny * toward.y) / nearest, -1.0, 1.0);
        }

        /// <summary>
        /// The unit vector toward the light, from
        /// <see cref="ArtRecipe.LightAngleDegrees"/>. Cosine and sine are not
        /// exactly rounded, so the result is rounded to nine decimals: a
        /// last-bit difference between platforms then never reaches a pixel.
        /// </summary>
        private static (double x, double y) TowardLight(ArtRecipe recipe)
        {
            var radians = recipe.LightAngleDegrees * Math.PI / 180.0;
            return (Math.Round(Math.Cos(radians), LightDirectionDecimals, MidpointRounding.AwayFromZero),
                Math.Round(Math.Sin(radians), LightDirectionDecimals, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// A grid of <paramref name="perSide"/> by <paramref name="perSide"/>
        /// studs on a cell, <see cref="ArtRecipe.StudSpacingPixels"/> apart
        /// and centred on it, each a short flat-topped cylinder lit from
        /// <see cref="ArtRecipe.LightAngleDegrees"/>, matte: a flat top in the
        /// face's tone, shifting only gently away from the light, inside a
        /// crisp darker rim all round; a darker crescent — its side — falling
        /// away from the light; and under both the stud's soft shadow on the
        /// face, falling further. The top sits
        /// <see cref="ArtRecipe.StudDiscRaisePixels"/> above its grid point.
        /// The shadow fades in from its outline, so it reaches no further than
        /// its offset. The recipe keeps the spacing above a stud's reach, so
        /// every pixel belongs to its nearest stud. One per side is the lone
        /// stud a layered block's inner block places on its own grid.
        /// </summary>
        private static ArtImage PaintStuds(ArtRecipe recipe, int perSide)
        {
            var radius = recipe.StudDiameterPixels / 2.0;
            double drop = recipe.StudFootDropPixels;
            double shadowFall = drop + recipe.StudShadowOffsetPixels;
            double shadowSoftness = recipe.StudShadowSoftnessPixels;
            double raise = recipe.StudDiscRaisePixels;
            var toward = TowardLight(recipe);
            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var (cx, cy) = NearestStud(recipe, perSide, x, y);
                var discY = cy + raise;
                var top = Length(x - cx, y - discY) - radius;
                var side = Length(x - cx + toward.x * drop, y - discY + toward.y * drop) - radius;

                // The shadow is the stud's whole outline — disc and side —
                // carried further away from the light.
                var shadow = Math.Min(
                    Length(x - cx + toward.x * recipe.StudShadowOffsetPixels, y - discY + toward.y * recipe.StudShadowOffsetPixels),
                    Length(x - cx + toward.x * shadowFall, y - discY + toward.y * shadowFall)) - radius;
                var shadowAlpha = Coverage(recipe, shadow) * (shadowSoftness > 0.0 ? Smooth(Clamp01(-shadow / shadowSoftness)) : 1.0);

                // Away from the light the flat top shifts gently; the rim
                // runs all round it, crisp, with one pixel of antialiasing.
                var away = Clamp01(-((x - cx) * toward.x + (y - discY) * toward.y) / radius);
                var tone = Lerp(Lerp(recipe.StudTone, recipe.StudShadeTone, Smooth(away)), recipe.StudRingTone, InRing(top, recipe.StudRingWidthPixels));

                var sideAndShadow = Over((recipe.StudFootTone, Coverage(recipe, side)), (recipe.StudShadowTone, shadowAlpha));
                return Over((tone, Coverage(recipe, top)), sideAndShadow);
            });
        }

        /// <summary>
        /// What lies untinted over each stud of <see cref="PaintStuds"/>' grid:
        /// a faint thin white arc just inside the top's edge, as strong as
        /// that edge faces the light, so it runs round the side toward the
        /// light and is gone a quarter turn away. Matte: no glossy spot.
        /// </summary>
        private static ArtImage PaintGloss(ArtRecipe recipe, int perSide)
        {
            var radius = recipe.StudDiameterPixels / 2.0;
            double raise = recipe.StudDiscRaisePixels;
            var toward = TowardLight(recipe);
            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var (cx, cy) = NearestStud(recipe, perSide, x, y);
                var discY = cy + raise;
                var fromCentre = Length(x - cx, y - discY);
                var top = fromCentre - radius;
                var facing = fromCentre > 0.0 ? Clamp01(((x - cx) * toward.x + (y - discY) * toward.y) / fromCentre) : 0.0;
                var arc = Coverage(recipe, top) * InRing(top, recipe.StudRimLightWidthPixels);
                return (1.0, recipe.StudRimLightAlpha * arc * facing);
            });
        }

        /// <summary>
        /// How much a pixel <c>−sd</c> inside a disc's edge belongs to a ring
        /// <paramref name="width"/> wide along that edge: 1 within it, 0 deeper,
        /// with one pixel of antialiasing between, so the ring stays crisp.
        /// </summary>
        private static double InRing(double sd, double width) => 1.0 - Clamp01(-sd - width + 0.5);

        /// <summary>
        /// The centre of the stud nearest a cell-sprite pixel: one of
        /// <paramref name="perSide"/> per axis,
        /// <see cref="ArtRecipe.StudSpacingPixels"/> apart, centred on the cell.
        /// </summary>
        private static (double x, double y) NearestStud(ArtRecipe recipe, int perSide, double x, double y) =>
            (NearestStudAlong(recipe, perSide, x), NearestStudAlong(recipe, perSide, y));

        private static double NearestStudAlong(ArtRecipe recipe, int perSide, double position)
        {
            var count = perSide;
            double spacing = recipe.StudSpacingPixels;
            var center = recipe.CellPixels / 2.0;
            var first = center - (count - 1) * spacing / 2.0;
            var index = count > 1 ? Clamp(Math.Floor((position - first) / spacing + 0.5), 0.0, count - 1) : 0.0;
            return first + index * spacing;
        }

        /// <summary>The quarter shape a face or contour sprite is painted for.</summary>
        private static QuarterShape ShapeOf(ArtSprite sprite)
        {
            switch (sprite)
            {
                case ArtSprite.QuarterOuter:
                case ArtSprite.ContourOuter:
                    return QuarterShape.Outer;
                case ArtSprite.QuarterEdge:
                case ArtSprite.ContourEdge:
                    return QuarterShape.Edge;
                case ArtSprite.QuarterFill:
                    return QuarterShape.Fill;
                default:
                    return QuarterShape.Concave;
            }
        }

        /// <summary>
        /// Signed distance to a piece's whole silhouette, the outline's outer
        /// edge, in quarter-local pixels: the cell's centre at the origin, its
        /// corner at <c>(q, q)</c>, the edge at <c>e = q − gap/2</c>.
        /// </summary>
        private static double SilhouetteSd(ArtRecipe recipe, QuarterShape shape, double x, double y)
        {
            double q = recipe.QuarterPixels;
            var e = q - recipe.BlockGapPixels / 2.0;
            double r = recipe.CornerRadiusPixels;
            double rc = recipe.ConcaveRadiusPixels;

            switch (shape)
            {
                case QuarterShape.Outer:
                    return SdLowerQuadrant(x, y, e - r, e - r) - r;
                case QuarterShape.Edge:
                    return y - e;
                case QuarterShape.Fill:
                    // Deeper than any bevel reaches: the whole quarter is face.
                    return -2.0 * q;
                default:
                    return rc - SdUpperQuadrant(x, y, e + rc, e + rc);
            }
        }

        /// <summary>
        /// Signed distance to a face piece: the silhouette's, offset inward by
        /// the outline. Offsetting a signed distance keeps every corner's
        /// centre, so the ring between the two is even all round.
        /// </summary>
        private static double FaceSd(ArtRecipe recipe, QuarterShape shape, double x, double y) =>
            SilhouetteSd(recipe, shape, x, y) + recipe.PieceOutlinePixels;

        /// <summary>
        /// The outward normal of the boundary nearest <c>(x, y)</c>, in the
        /// sprite's own frame (the top-right quarter). Exact wherever a light
        /// band reaches, which <see cref="ArtRecipe.Problems"/> keeps within a
        /// face corner's radius of the edge: there the normal turns smoothly,
        /// never jumping across a corner's diagonal.
        /// </summary>
        private static (double x, double y) OutwardNormal(ArtRecipe recipe, QuarterShape shape, double x, double y)
        {
            double q = recipe.QuarterPixels;
            var e = q - recipe.BlockGapPixels / 2.0;

            switch (shape)
            {
                case QuarterShape.Outer:
                {
                    // Round the corner's centre the normal points out of it;
                    // beside it, the nearer straight side decides.
                    var c = e - recipe.CornerRadiusPixels;
                    var dx = x - c;
                    var dy = y - c;
                    if (dx > 0.0 && dy > 0.0)
                    {
                        var length = Length(dx, dy);
                        return (dx / length, dy / length);
                    }

                    return dy >= dx ? (0.0, 1.0) : (1.0, 0.0);
                }

                case QuarterShape.Edge:
                case QuarterShape.Fill:
                    return (0.0, 1.0);
                default:
                {
                    // The notch is the quadrant beyond (c, c), rounded: below
                    // it the edge is the notch's floor, beside it a side, and
                    // diagonally off it the normal points at its corner.
                    var c = e + recipe.ConcaveRadiusPixels;
                    if (x >= c && y < c)
                    {
                        return (0.0, 1.0);
                    }

                    if (x < c && y < c)
                    {
                        var length = Length(c - x, c - y);
                        return ((c - x) / length, (c - y) / length);
                    }

                    return (1.0, 0.0);
                }
            }
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

        /// <summary>
        /// A white arrow pointing up, centred in its square, grown by
        /// <paramref name="growPixels"/> on every side: 0 for the arrow itself,
        /// the outline's width for the outline the runtime draws under it.
        /// The head's distance is exact outside it too, so the grown arrow
        /// is rounded at its tip and corners, never spiked, and reaches no
        /// further than the growth.
        /// </summary>
        private static ArtImage PaintGateArrow(ArtRecipe recipe, double growPixels)
        {
            double s = recipe.GateArrowPixels;
            var length = recipe.GateArrowLength * s;
            var headLength = recipe.GateArrowHeadLength * s;
            var tip = length / 2.0;
            var headBase = tip - headLength;
            var tail = -tip;
            var halfHead = recipe.GateArrowHeadWidth * s / 2.0;
            var halfShaft = recipe.GateArrowShaftWidth * s / 2.0;

            // The head's corners counter-clockwise, across then along.
            var headXs = new[] { halfHead, 0.0, -halfHead };
            var headYs = new[] { headBase, tip, headBase };

            return Paint(recipe.GateArrowPixels, recipe.GateArrowPixels, Vector4.zero, (x, y) =>
            {
                var u = x - s / 2.0;
                var v = y - s / 2.0;
                var head = SdConvexPolygon(u, v, headXs, headYs);
                var shaft = SdBox(u, v - (tail + headBase) / 2.0, halfShaft, (headBase - tail) / 2.0);
                return (1.0, Coverage(recipe, Math.Min(head, shaft) - growPixels));
            });
        }

        /// <summary>
        /// Exact signed distance to a convex polygon whose corners are listed
        /// counter-clockwise: inside, the nearest edge line's (the largest of
        /// them); outside, the nearest edge segment's.
        /// </summary>
        private static double SdConvexPolygon(double x, double y, IReadOnlyList<double> xs, IReadOnlyList<double> ys)
        {
            var nearestLine = double.NegativeInfinity;
            var nearestSegment = double.MaxValue;
            for (var i = 0; i < xs.Count; i++)
            {
                var j = (i + 1) % xs.Count;
                var dx = xs[j] - xs[i];
                var dy = ys[j] - ys[i];

                // Counter-clockwise, so the outward normal is (dy, −dx).
                nearestLine = Math.Max(nearestLine, ((x - xs[i]) * dy - (y - ys[i]) * dx) / Length(dx, dy));
                nearestSegment = Math.Min(nearestSegment, DistanceToSegment(x, y, xs[i], ys[i], xs[j], ys[j]));
            }

            return nearestLine <= 0.0 ? nearestLine : nearestSegment;
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

        /// <summary>
        /// A sparkle, in white: a four-point star with its tips on the axes,
        /// the anti-aliased edge inside the sprite, and its inner corners on
        /// the diagonals. The star is the same in all eight octants, so a
        /// pixel is folded into the one below the diagonal on the right, where
        /// the star's outline is a single edge from a tip to an inner corner;
        /// the distance past that edge's line is exact along the outline,
        /// which is all anti-aliasing reads.
        /// </summary>
        private static ArtImage PaintSparkle(ArtRecipe recipe)
        {
            double size = recipe.SparklePixels;
            var center = size / 2.0;
            var tip = center - recipe.AntiAliasPixels;
            var waist = recipe.SparkleWaist * size;
            var edgeLength = Length(waist, tip - waist);

            return Paint(recipe.SparklePixels, recipe.SparklePixels, Vector4.zero, (x, y) =>
            {
                var along = Math.Max(Math.Abs(x - center), Math.Abs(y - center));
                var across = Math.Min(Math.Abs(x - center), Math.Abs(y - center));

                // The edge runs from (tip, 0) to (waist, waist); its outward
                // normal is (waist, tip − waist).
                var sd = ((along - tip) * waist + across * (tip - waist)) / edgeLength;
                return (1.0, Coverage(recipe, sd));
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
