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
        FaceOuterTopLeft,
        FaceOuterTopRight,
        FaceOuterBottomLeft,
        FaceOuterBottomRight,
        FaceConcaveTopLeft,
        FaceConcaveTopRight,
        FaceConcaveBottomLeft,
        FaceConcaveBottomRight,
        FaceEdgeTop,
        FaceEdgeBottom,
        FaceEdgeLeft,
        FaceEdgeRight,
        FaceFill,
        BodyOuterTopLeft,
        BodyOuterTopRight,
        BodyOuterBottomLeft,
        BodyOuterBottomRight,
        BodyConcaveTopLeft,
        BodyConcaveTopRight,
        BodyConcaveBottomLeft,
        BodyConcaveBottomRight,
        BodyEdgeTop,
        BodyEdgeBottom,
        BodyEdgeLeft,
        BodyEdgeRight,
        BodyFill,
        ShadowOuterTopLeft,
        ShadowOuterTopRight,
        ShadowOuterBottomLeft,
        ShadowOuterBottomRight,
        ShadowConcaveTopLeft,
        ShadowConcaveTopRight,
        ShadowConcaveBottomLeft,
        ShadowConcaveBottomRight,
        ShadowEdgeTop,
        ShadowEdgeBottom,
        ShadowEdgeLeft,
        ShadowEdgeRight,
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
        RoundedRect,
        Chain,
        Padlock,
        KeyBody,
        KeyGem,
        ShutterSlats,
        Clock,
        Restart,
        Cube,
        Shard,
        GateGlow,
        Sparkle,
        IceOuterTopLeft,
        IceOuterTopRight,
        IceOuterBottomLeft,
        IceOuterBottomRight,
        IceConcaveTopLeft,
        IceConcaveTopRight,
        IceConcaveBottomLeft,
        IceConcaveBottomRight,
        IceEdgeTop,
        IceEdgeBottom,
        IceEdgeLeft,
        IceEdgeRight,
        IceFill,
        IceStreaks,
        IceStreaksWide,
        BadgeRim,
        BadgeFace,
        BadgeHighlight,
        Lock,
        GoldKey,
        Gem,
        GemHighlight,
        BonusCoin,
        OneWayArrow,
        OneWayArrowHighlight,
        OneWayArrowVertical,
        OneWayArrowVerticalHighlight,
        ShutterRim,
        ShutterCylinders,
        MachineBody,
        MachineScreen,
        MachineVent,
        MachineButton,
        ElevatorRim,
        ElevatorHazardLeft,
        ElevatorHazardRight,
        ElevatorDoor,
        ElevatorHandle
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
    /// <see cref="ArtRecipe"/>, in greyscale plus alpha for the runtime to
    /// tint where a colour varies (D46), and in an <see cref="ArtPalette"/>'s
    /// colours where it never does (Module 46, pass 2).
    /// </summary>
    /// <remarks>
    /// <para><b>Shapes</b> are signed distance functions evaluated at pixel
    /// centres: coverage is <c>0.5 − d / antiAlias</c>, clamped, and tone is a
    /// function of the depth inside the shape. A block's body (Module 46) is
    /// lit: its face pieces carry the shade of a height field under one light,
    /// its body pieces the white highlight and its shadow pieces its shadow on
    /// the floor (see <c>ArtPainter.Lighting.cs</c>). Those depend on which
    /// way the edge faces once posed, so there is one per corner of a cell
    /// and one per side for an edge, and the runtime picks the one for the
    /// quarter's place; the plain pieces depend only on the distance to the
    /// boundary. Fully transparent pixels keep the edge's tone in RGB, so
    /// filtering never pulls a dark fringe into the edge.</para>
    /// <para><b>Quarter pieces</b> are drawn for the top-right quarter, in
    /// quarter-local pixels whose origin is the cell's centre: the cell's
    /// corner is at <c>(q, q)</c> and the outline runs at <c>e = q − gap/2</c>.
    /// Every seam between two pieces carries the same pixels on both sides, as
    /// long as the recipe passes <see cref="ArtRecipe.Problems"/>.</para>
    /// <para><b>Determinism.</b> Only double-precision <c>+ − × ÷</c>,
    /// <c>Math.Sqrt</c>, <c>Abs</c>, <c>Floor</c>, <c>Min</c> and <c>Max</c> are
    /// used, all exactly rounded — no <c>Mathf</c>,
    /// <c>Pow</c> or <c>Exp</c>, no randomness, no clock — and each value is
    /// rounded to a byte with an explicit midpoint rule. The exceptions —
    /// the highlight's and the stud rim's powers and the sRGB encoding, and
    /// the mechanics' blurs and slats (<c>ArtPainter.Mechanics.cs</c>) — go
    /// through <c>Math.Pow</c>, <c>Math.Exp</c> or <c>Math.Sin</c> and are
    /// rounded to nine decimals (<see cref="StablePow"/>), so a last-bit
    /// difference never reaches a byte. The same recipe and palette always
    /// paint the same bytes.</para>
    /// </remarks>
    public static partial class ArtPainter
    {
        /// <summary>
        /// Width of the background ramp. Not a look parameter: the ramp only
        /// varies vertically, and two columns let bilinear filtering stretch it
        /// without sampling a single column's edge.
        /// </summary>
        private const int RampWidthPixels = 2;

        /// <summary>
        /// The decimals a power or a square root of a power is rounded to
        /// (<see cref="StablePow"/>). Not a look parameter: it only keeps a
        /// platform's last bit out of the pixels.
        /// </summary>
        private const int StableDecimals = 9;

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

        /// <summary>
        /// Paints <paramref name="sprite"/> from <paramref name="recipe"/>, for
        /// a sprite that takes no colour of its own (<see cref="NeedsPalette"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="recipe"/> is null, or the sprite needs a palette.</exception>
        public static ArtImage Paint(ArtRecipe recipe, ArtSprite sprite) => Paint(recipe, null, sprite);

        /// <summary>
        /// True for the sprites painted in colours of their own, read from
        /// <paramref name="sprite"/>'s <see cref="ArtPalette"/> (Module 46,
        /// pass 2): the parts of the mechanics whose colour never varies.
        /// </summary>
        public static bool NeedsPalette(ArtSprite sprite) =>
            IsIcePiece(sprite)
            || sprite == ArtSprite.Chain || sprite == ArtSprite.Lock || sprite == ArtSprite.GoldKey
            || sprite == ArtSprite.BonusCoin || sprite == ArtSprite.ShutterRim || sprite == ArtSprite.ShutterCylinders
            || sprite == ArtSprite.MachineBody || sprite == ArtSprite.MachineScreen || sprite == ArtSprite.MachineButton
            || sprite == ArtSprite.ElevatorRim || sprite == ArtSprite.ElevatorHazardLeft
            || sprite == ArtSprite.ElevatorHazardRight || sprite == ArtSprite.ElevatorDoor || sprite == ArtSprite.ElevatorHandle;

        /// <summary>
        /// Paints <paramref name="sprite"/> from <paramref name="recipe"/>, in
        /// <paramref name="palette"/>'s colours where it has colours of its own.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="recipe"/> is null, or <paramref name="palette"/> is and the sprite needs it.</exception>
        public static ArtImage Paint(ArtRecipe recipe, ArtPalette palette, ArtSprite sprite)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            if (palette == null && NeedsPalette(sprite))
            {
                throw new ArgumentNullException(nameof(palette), $"{sprite} is painted in the palette's colours.");
            }

            if (IsIcePiece(sprite))
            {
                return PaintIce(recipe, palette, PlaceOf(sprite));
            }

            switch (sprite)
            {
                case ArtSprite.QuarterOuter:
                case ArtSprite.QuarterEdge:
                case ArtSprite.QuarterFill:
                case ArtSprite.QuarterConcave:
                    return PaintQuarter(recipe, sprite);
                case ArtSprite.FaceOuterTopLeft:
                case ArtSprite.FaceOuterTopRight:
                case ArtSprite.FaceOuterBottomLeft:
                case ArtSprite.FaceOuterBottomRight:
                case ArtSprite.FaceConcaveTopLeft:
                case ArtSprite.FaceConcaveTopRight:
                case ArtSprite.FaceConcaveBottomLeft:
                case ArtSprite.FaceConcaveBottomRight:
                case ArtSprite.FaceEdgeTop:
                case ArtSprite.FaceEdgeBottom:
                case ArtSprite.FaceEdgeLeft:
                case ArtSprite.FaceEdgeRight:
                case ArtSprite.FaceFill:
                    return PaintLit(recipe, PlaceOf(sprite), LitLayer.Face);
                case ArtSprite.BodyOuterTopLeft:
                case ArtSprite.BodyOuterTopRight:
                case ArtSprite.BodyOuterBottomLeft:
                case ArtSprite.BodyOuterBottomRight:
                case ArtSprite.BodyConcaveTopLeft:
                case ArtSprite.BodyConcaveTopRight:
                case ArtSprite.BodyConcaveBottomLeft:
                case ArtSprite.BodyConcaveBottomRight:
                case ArtSprite.BodyEdgeTop:
                case ArtSprite.BodyEdgeBottom:
                case ArtSprite.BodyEdgeLeft:
                case ArtSprite.BodyEdgeRight:
                case ArtSprite.BodyFill:
                    return PaintLit(recipe, PlaceOf(sprite), LitLayer.Specular);
                case ArtSprite.ShadowOuterTopLeft:
                case ArtSprite.ShadowOuterTopRight:
                case ArtSprite.ShadowOuterBottomLeft:
                case ArtSprite.ShadowOuterBottomRight:
                case ArtSprite.ShadowConcaveTopLeft:
                case ArtSprite.ShadowConcaveTopRight:
                case ArtSprite.ShadowConcaveBottomLeft:
                case ArtSprite.ShadowConcaveBottomRight:
                case ArtSprite.ShadowEdgeTop:
                case ArtSprite.ShadowEdgeBottom:
                case ArtSprite.ShadowEdgeLeft:
                case ArtSprite.ShadowEdgeRight:
                    return PaintShadow(recipe, PlaceOf(sprite));
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
                case ArtSprite.RoundedRect:
                    return PaintRoundedRect(recipe);
                case ArtSprite.Chain:
                    return PaintChain(recipe, palette);
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
                case ArtSprite.IceStreaks:
                    return PaintIceStreaks(recipe, 1);
                case ArtSprite.IceStreaksWide:
                    return PaintIceStreaks(recipe, 2);
                case ArtSprite.BadgeRim:
                    return PaintBadge(recipe, BadgeLayer.Rim);
                case ArtSprite.BadgeFace:
                    return PaintBadge(recipe, BadgeLayer.Face);
                case ArtSprite.BadgeHighlight:
                    return PaintBadge(recipe, BadgeLayer.Highlight);
                case ArtSprite.Lock:
                    return PaintLock(recipe, palette);
                case ArtSprite.GoldKey:
                    return PaintGoldKey(recipe, palette);
                case ArtSprite.Gem:
                    return PaintGem(recipe, isHighlight: false);
                case ArtSprite.GemHighlight:
                    return PaintGem(recipe, isHighlight: true);
                case ArtSprite.BonusCoin:
                    return PaintCoin(recipe, palette);
                case ArtSprite.OneWayArrow:
                    return PaintOneWayArrow(recipe, isVertical: false, isHighlight: false);
                case ArtSprite.OneWayArrowHighlight:
                    return PaintOneWayArrow(recipe, isVertical: false, isHighlight: true);
                case ArtSprite.OneWayArrowVertical:
                    return PaintOneWayArrow(recipe, isVertical: true, isHighlight: false);
                case ArtSprite.OneWayArrowVerticalHighlight:
                    return PaintOneWayArrow(recipe, isVertical: true, isHighlight: true);
                case ArtSprite.ShutterRim:
                    return PaintShutterRim(recipe, palette);
                case ArtSprite.ShutterCylinders:
                    return PaintShutterCylinders(recipe, palette);
                case ArtSprite.MachineBody:
                    return PaintMachineBody(recipe, palette);
                case ArtSprite.MachineScreen:
                    return PaintMachineScreen(recipe, palette);
                case ArtSprite.MachineVent:
                    return PaintMachineVent(recipe);
                case ArtSprite.MachineButton:
                    return PaintMachineButton(recipe, palette);
                case ArtSprite.ElevatorRim:
                    return PaintElevatorRim(recipe, palette);
                case ArtSprite.ElevatorHazardLeft:
                    return PaintElevatorHazard(recipe, palette, isLeft: true);
                case ArtSprite.ElevatorHazardRight:
                    return PaintElevatorHazard(recipe, palette, isLeft: false);
                case ArtSprite.ElevatorDoor:
                    return PaintElevatorDoor(recipe, palette);
                case ArtSprite.ElevatorHandle:
                    return PaintElevatorHandle(recipe, palette);
                default:
                    throw new ArgumentOutOfRangeException(nameof(sprite), sprite, "Not a generated sprite.");
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
        /// A plain piece: the piece's silhouette, white, for whatever draws a
        /// block's shape flat — a grabbed block's outline, a preview. The lit
        /// body is the face pieces' (<see cref="PaintFace"/>).
        /// </summary>
        private static ArtImage PaintQuarter(ArtRecipe recipe, ArtSprite sprite)
        {
            var shape = ShapeOf(sprite);
            return Paint(recipe.QuarterPixels, recipe.QuarterPixels, Vector4.zero, (x, y) =>
                (1.0, Coverage(recipe, SilhouetteSd(recipe, shape, x, y))));
        }

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

        /// <summary>The quarter shape a plain sprite is painted for.</summary>
        private static QuarterShape ShapeOf(ArtSprite sprite)
        {
            switch (sprite)
            {
                case ArtSprite.QuarterOuter:
                    return QuarterShape.Outer;
                case ArtSprite.QuarterEdge:
                    return QuarterShape.Edge;
                case ArtSprite.QuarterFill:
                    return QuarterShape.Fill;
                default:
                    return QuarterShape.Concave;
            }
        }

        /// <summary>
        /// Signed distance to a piece's silhouette, the body's edge, in
        /// quarter-local pixels: the cell's centre at the origin, its corner
        /// at <c>(q, q)</c>, the edge at <c>e = q − gap/2</c>.
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
        /// The outward direction of the boundary nearest <c>(x, y)</c>, in the
        /// sprite's own frame (the top-right quarter). Exact wherever the
        /// bevel reaches, which <see cref="ArtRecipe.Problems"/> keeps within a
        /// corner's radius of the edge: there it turns smoothly, never jumping
        /// across a corner's diagonal.
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
