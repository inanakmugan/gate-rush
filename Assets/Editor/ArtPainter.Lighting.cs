using System;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// The lit body of Module 46: blocks, gates and the frame as raised,
    /// rounded bodies under one light, and their studs.
    /// </summary>
    /// <remarks>
    /// <para><b>The model.</b> A body's height rises from its edge as a
    /// quarter circle over <see cref="ArtRecipe.BevelPixels"/> to
    /// <see cref="ArtRecipe.BodyHeightPixels"/>; its normal follows from that
    /// height, and one light (<see cref="ArtRecipe.LightDirection"/>) lights it
    /// with Blinn-Phong: the colour is the tint times a grey shade,
    /// <c>(ambient + diffuse × n·L) × occlusion</c>, plus a white highlight,
    /// <c>specular × (n·H)^shininess</c>, all in linear light. The shade
    /// becomes the tinted face pieces, stored divided by
    /// <see cref="ArtRecipe.ShadeHeadroom"/> and sRGB-encoded, for the lit
    /// material to multiply back; the highlight becomes the untinted body
    /// pieces, which the specular material adds.</para>
    /// <para><b>Seamless by construction.</b> Height, normal and occlusion
    /// are functions of the signed distance to the piece's own edge and the
    /// direction that edge faces once posed, never of a neighbour or a blur,
    /// so two pieces that meet at a seam compute the same pixels there. The
    /// occlusion is a falloff from the edge: darker at the bevel's very
    /// foot, which wraps round an inside corner's notch, and none on the
    /// flat.</para>
    /// <para><b>Places.</b> A piece is drawn for the top-right quarter and
    /// posed by mirroring, or rotating for a side; the light has a direction,
    /// so each corner of a cell and each side of an edge has its own sprite,
    /// lit for that place, and the flat fill one more.</para>
    /// </remarks>
    public static partial class ArtPainter
    {
        /// <summary>What a lit sprite carries: the tinted shade, or the untinted highlight.</summary>
        private enum LitLayer
        {
            Face,
            Specular
        }

        /// <summary>
        /// A place a lit, highlight or shadow piece is drawn for: a corner of
        /// its cell, the side an edge faces, or the fill.
        /// </summary>
        private readonly struct Place
        {
            public Place(QuarterShape shape, int signX, int signY)
            {
                Shape = shape;
                SignX = signX;
                SignY = signY;
            }

            /// <summary>The piece's shape, drawn for the top-right quarter.</summary>
            public QuarterShape Shape { get; }

            /// <summary>A corner's horizontal side, or an edge's outward x on the board.</summary>
            public int SignX { get; }

            /// <summary>A corner's vertical side, or an edge's outward y on the board.</summary>
            public int SignY { get; }
        }

        /// <summary>
        /// The light and the half vector of <see cref="ArtRecipe"/>, worked
        /// out once per sprite.
        /// </summary>
        private readonly struct LitModel
        {
            private readonly double lx;
            private readonly double ly;
            private readonly double lz;
            private readonly double hx;
            private readonly double hy;
            private readonly double hz;
            private readonly ArtRecipe recipe;
            private readonly double bevel;
            private readonly double height;
            private readonly double shininess;
            private readonly double specular;
            private readonly bool isOccluded;

            /// <summary>The lit body's model: its bevel, height and highlight, with its occlusion.</summary>
            public LitModel(ArtRecipe recipe)
                : this(recipe, recipe.BevelPixels, recipe.BodyHeightPixels, new LitFinish(recipe.Shininess, recipe.SpecularStrength), true)
            {
            }

            /// <summary>
            /// A raised part lit as the body is, with its own bevel, height and
            /// finish, and the body's occlusion when <paramref name="isOccluded"/>:
            /// the ice of Module 46's second pass has none, as the reference has not.
            /// </summary>
            public LitModel(ArtRecipe recipe, double bevel, double height, LitFinish finish, bool isOccluded)
            {
                this.recipe = recipe;
                this.bevel = bevel;
                this.height = height;
                shininess = finish.Shininess;
                specular = finish.Specular;
                this.isOccluded = isOccluded;
                var light = recipe.LightDirection;
                var length = Math.Sqrt((double)light.x * light.x + (double)light.y * light.y + (double)light.z * light.z);
                lx = light.x / length;
                ly = light.y / length;
                lz = light.z / length;

                // The half vector between the light and the viewer, straight
                // out of the board.
                var halfLength = Math.Sqrt(lx * lx + ly * ly + (lz + 1.0) * (lz + 1.0));
                hx = lx / halfLength;
                hy = ly / halfLength;
                hz = (lz + 1.0) / halfLength;
            }

            /// <summary>
            /// The grey shade and the white highlight <paramref name="depth"/>
            /// pixels inside a body's edge, where the edge faces
            /// <c>(outX, outY)</c> on the board.
            /// </summary>
            public (double shade, double specular) At(double depth, double outX, double outY)
            {
                // The bevel is a quarter circle: with u from 1 at the edge to
                // 0 at its top, the slope is k·u/√(1 − u²), and the normal
                // (k·u·out, √(1 − u²)) over its length — horizontal at the
                // very edge, straight up on the flat.
                var t = Clamp01(depth / bevel);
                var u = 1.0 - t;
                var k = height / bevel;
                var rise = Math.Sqrt(1.0 - u * u);
                var length = Math.Sqrt(k * k * u * u + rise * rise);
                var nx = k * u * outX / length;
                var ny = k * u * outY / length;
                var nz = rise / length;

                var diffuse = Math.Max(0.0, nx * lx + ny * ly + nz * lz);
                var mirror = Math.Max(0.0, nx * hx + ny * hy + nz * hz);
                var highlight = specular * StablePow(mirror, shininess);
                var shade = (recipe.Ambient + recipe.Diffuse * diffuse) * (isOccluded ? Occlusion(depth) : 1.0);
                return (shade, highlight);
            }

            /// <summary>
            /// The ambient occlusion <paramref name="depth"/> pixels inside the
            /// edge: darker by <see cref="ArtRecipe.OcclusionFoot"/> at the
            /// bevel's foot, easing to none over
            /// <see cref="ArtRecipe.OcclusionFootPixels"/>, never below
            /// <see cref="ArtRecipe.OcclusionMinimum"/>.
            /// </summary>
            public double Occlusion(double depth)
            {
                var fade = Smooth(Clamp01(depth / recipe.OcclusionFootPixels));
                return Clamp(1.0 - recipe.OcclusionFoot * (1.0 - fade), recipe.OcclusionMinimum, 1.0);
            }
        }

        /// <summary>The place a face, body or shadow sprite is painted for.</summary>
        private static Place PlaceOf(ArtSprite sprite)
        {
            switch (sprite)
            {
                case ArtSprite.IceOuterTopLeft:
                case ArtSprite.FaceOuterTopLeft:
                case ArtSprite.BodyOuterTopLeft:
                case ArtSprite.ShadowOuterTopLeft:
                    return new Place(QuarterShape.Outer, -1, 1);
                case ArtSprite.IceOuterTopRight:
                case ArtSprite.FaceOuterTopRight:
                case ArtSprite.BodyOuterTopRight:
                case ArtSprite.ShadowOuterTopRight:
                    return new Place(QuarterShape.Outer, 1, 1);
                case ArtSprite.IceOuterBottomLeft:
                case ArtSprite.FaceOuterBottomLeft:
                case ArtSprite.BodyOuterBottomLeft:
                case ArtSprite.ShadowOuterBottomLeft:
                    return new Place(QuarterShape.Outer, -1, -1);
                case ArtSprite.IceOuterBottomRight:
                case ArtSprite.FaceOuterBottomRight:
                case ArtSprite.BodyOuterBottomRight:
                case ArtSprite.ShadowOuterBottomRight:
                    return new Place(QuarterShape.Outer, 1, -1);
                case ArtSprite.IceConcaveTopLeft:
                case ArtSprite.FaceConcaveTopLeft:
                case ArtSprite.BodyConcaveTopLeft:
                case ArtSprite.ShadowConcaveTopLeft:
                    return new Place(QuarterShape.Concave, -1, 1);
                case ArtSprite.IceConcaveTopRight:
                case ArtSprite.FaceConcaveTopRight:
                case ArtSprite.BodyConcaveTopRight:
                case ArtSprite.ShadowConcaveTopRight:
                    return new Place(QuarterShape.Concave, 1, 1);
                case ArtSprite.IceConcaveBottomLeft:
                case ArtSprite.FaceConcaveBottomLeft:
                case ArtSprite.BodyConcaveBottomLeft:
                case ArtSprite.ShadowConcaveBottomLeft:
                    return new Place(QuarterShape.Concave, -1, -1);
                case ArtSprite.IceConcaveBottomRight:
                case ArtSprite.FaceConcaveBottomRight:
                case ArtSprite.BodyConcaveBottomRight:
                case ArtSprite.ShadowConcaveBottomRight:
                    return new Place(QuarterShape.Concave, 1, -1);
                case ArtSprite.IceEdgeTop:
                case ArtSprite.FaceEdgeTop:
                case ArtSprite.BodyEdgeTop:
                case ArtSprite.ShadowEdgeTop:
                    return new Place(QuarterShape.Edge, 0, 1);
                case ArtSprite.IceEdgeBottom:
                case ArtSprite.FaceEdgeBottom:
                case ArtSprite.BodyEdgeBottom:
                case ArtSprite.ShadowEdgeBottom:
                    return new Place(QuarterShape.Edge, 0, -1);
                case ArtSprite.IceEdgeLeft:
                case ArtSprite.FaceEdgeLeft:
                case ArtSprite.BodyEdgeLeft:
                case ArtSprite.ShadowEdgeLeft:
                    return new Place(QuarterShape.Edge, -1, 0);
                case ArtSprite.IceEdgeRight:
                case ArtSprite.FaceEdgeRight:
                case ArtSprite.BodyEdgeRight:
                case ArtSprite.ShadowEdgeRight:
                    return new Place(QuarterShape.Edge, 1, 0);
                default:
                    return new Place(QuarterShape.Fill, 1, 1);
            }
        }

        /// <summary>
        /// A lit piece for <paramref name="place"/>: the face's shade, tinted
        /// and stored for the lit material, or the white highlight for the
        /// specular material, each covering the piece's silhouette.
        /// </summary>
        private static ArtImage PaintLit(ArtRecipe recipe, Place place, LitLayer layer)
        {
            var model = new LitModel(recipe);
            double headroom = recipe.ShadeHeadroom;
            return Paint(recipe.QuarterPixels, recipe.QuarterPixels, Vector4.zero, (x, y) =>
            {
                var sd = SilhouetteSd(recipe, place.Shape, x, y);
                var (outX, outY) = BoardOutward(recipe, place, x, y);
                var (shade, specular) = model.At(-sd, outX, outY);
                var coverage = Coverage(recipe, sd);
                return layer == LitLayer.Face
                    ? (EncodeSrgb(shade / headroom), coverage)
                    : (1.0, specular * coverage);
            });
        }

        /// <summary>
        /// The direction the edge nearest <c>(x, y)</c> faces on the board: a
        /// corner piece's own outward direction mirrored onto its corner, an
        /// edge piece's side. A fill has none that matters: its pixels are all
        /// on the flat.
        /// </summary>
        private static (double x, double y) BoardOutward(ArtRecipe recipe, Place place, double x, double y)
        {
            if (place.Shape == QuarterShape.Edge || place.Shape == QuarterShape.Fill)
            {
                return (place.SignX, place.SignY);
            }

            var (outX, outY) = OutwardNormal(recipe, place.Shape, x, y);
            return (place.SignX * outX, place.SignY * outY);
        }

        /// <summary>
        /// A floor-shadow piece for <paramref name="place"/>: black, as strong
        /// as <see cref="ArtRecipe.FloorShadowAlpha"/> under the body and
        /// easing out across twice <see cref="ArtRecipe.FloorShadowSpreadPixels"/>
        /// round its edge — a smoothstep matched to a blur's slope — carried by
        /// <see cref="ArtRecipe.FloorShadowOffsetPixels"/>. Its canvas is the
        /// quarter widened by <see cref="ArtRecipe.FloorShadowMarginPixels"/>
        /// on the sides that face out of the body — both for an outer corner,
        /// the outward one for an edge, none for an inside corner — so two
        /// pieces of one body never cover the same floor.
        /// </summary>
        private static ArtImage PaintShadow(ArtRecipe recipe, Place place)
        {
            var quarter = recipe.QuarterPixels;
            var margin = recipe.FloorShadowMarginPixels;
            var offset = recipe.FloorShadowOffsetPixels;

            // The offset in the sprite's own frame. An edge's sprite runs
            // along x with its edge on top, so only the offset across the edge
            // moves it; a corner's sprite is mirrored onto its corner.
            double shiftX;
            double shiftY;
            int width;
            int height;
            switch (place.Shape)
            {
                case QuarterShape.Edge:
                    shiftX = 0.0;
                    shiftY = place.SignX * offset.x + place.SignY * offset.y;
                    width = quarter;
                    height = quarter + margin;
                    break;
                case QuarterShape.Outer:
                    shiftX = place.SignX * offset.x;
                    shiftY = place.SignY * offset.y;
                    width = quarter + margin;
                    height = quarter + margin;
                    break;
                default:
                    shiftX = place.SignX * offset.x;
                    shiftY = place.SignY * offset.y;
                    width = quarter;
                    height = quarter;
                    break;
            }

            double spread = recipe.FloorShadowSpreadPixels;
            return Paint(width, height, Vector4.zero, (x, y) =>
            {
                var sd = SilhouetteSd(recipe, place.Shape, x - shiftX, y - shiftY);
                return (0.0, recipe.FloorShadowAlpha * (1.0 - SmoothStep(-spread, spread, sd)));
            });
        }

        /// <summary>
        /// A grid of <paramref name="perSide"/> by <paramref name="perSide"/>
        /// studs on a cell, <see cref="ArtRecipe.StudSpacingPixels"/> apart and
        /// centred on it, each, over the face: a soft contact shadow; its
        /// wall, a disc offset down and to the right in
        /// <see cref="ArtRecipe.StudWallTone"/>; and its top, a disc offset up
        /// and to the left in <see cref="ArtRecipe.StudTopTone"/>, darkened
        /// slightly on the rim away from the light. Tinted by the block and
        /// stored for the lit material, like the face. The recipe keeps the
        /// spacing above a stud's reach, so every pixel belongs to its nearest
        /// stud. One per side is the lone stud a layered block's inner block
        /// places on its own grid.
        /// </summary>
        private static ArtImage PaintStuds(ArtRecipe recipe, int perSide)
        {
            var radius = recipe.StudDiameterPixels / 2.0;
            double edge = recipe.StudEdgePixels;
            double headroom = recipe.ShadeHeadroom;
            var contactOffset = recipe.StudContactOffsetPixels;
            var wallOffset = recipe.StudWallOffsetPixels;
            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var (cx, cy) = NearestStud(recipe, perSide, x, y);
                var contactDistance = Length(x - cx - contactOffset.x, y - cy - contactOffset.y);
                var contact = 1.0 - SmoothStep(radius + recipe.StudContactInnerPixels, radius + recipe.StudContactOuterPixels, contactDistance);
                var wall = 1.0 - SmoothStep(radius - edge, radius + edge, Length(x - cx - wallOffset.x, y - cy - wallOffset.y));
                var (top, rim, toward) = StudTop(recipe, x, y, cx, cy);
                var topTone = recipe.StudTopTone * (1.0 - recipe.StudRimDarken * rim * Clamp01(-toward));

                var layer = (tone: 0.0, alpha: recipe.StudContactAlpha * contact);
                layer = Over((recipe.StudWallTone, wall), layer);
                layer = Over((topTone, top), layer);
                return (EncodeSrgb(layer.tone / headroom), layer.alpha);
            });
        }

        /// <summary>
        /// What lies untinted over each stud of <see cref="PaintStuds"/>' grid:
        /// the rim's light on the side toward the light, white, as strong as
        /// <see cref="ArtRecipe.StudRimLightAlpha"/> times how squarely the
        /// rim faces the light to the power
        /// <see cref="ArtRecipe.StudRimLightExponent"/>. Blended over the
        /// top, it lifts it toward white by that share.
        /// </summary>
        private static ArtImage PaintGloss(ArtRecipe recipe, int perSide)
        {
            return Paint(recipe.CellPixels, recipe.CellPixels, Vector4.zero, (x, y) =>
            {
                var (cx, cy) = NearestStud(recipe, perSide, x, y);
                var (top, rim, toward) = StudTop(recipe, x, y, cx, cy);
                var light = StablePow(Clamp01(toward), recipe.StudRimLightExponent);
                return (1.0, recipe.StudRimLightAlpha * rim * light * top);
            });
        }

        /// <summary>
        /// A stud's top at <c>(x, y)</c>, the stud's grid point at
        /// <c>(cx, cy)</c>: how much the top covers the pixel, how much of its
        /// rim lies there, and how far along the rim's light direction the
        /// pixel sits from the grid point, as a share of the radius —
        /// positive toward the light, negative away.
        /// </summary>
        private static (double top, double rim, double toward) StudTop(ArtRecipe recipe, double x, double y, double cx, double cy)
        {
            var radius = recipe.StudDiameterPixels / 2.0;
            double edge = recipe.StudEdgePixels;
            var topOffset = recipe.StudTopOffsetPixels;
            var r = Length(x - cx - topOffset.x, y - cy - topOffset.y);
            var top = 1.0 - SmoothStep(radius - edge, radius + edge, r);
            var rim = SmoothStep(radius - recipe.StudRimInnerPixels, radius - recipe.StudRimFullPixels, r)
                      * (1.0 - SmoothStep(radius - recipe.StudRimFadePixels, radius, r));

            var direction = recipe.StudRimLightDirection;
            var length = Math.Sqrt((double)direction.x * direction.x + (double)direction.y * direction.y);
            var toward = ((x - cx) * direction.x + (y - cy) * direction.y) / (length * radius);
            return (top, rim, toward);
        }

        /// <summary>
        /// <paramref name="linear"/>, a share of a colour in linear light,
        /// encoded as sRGB: what the textures store, since they import as sRGB
        /// and the GPU decodes them back to linear before tinting and blending.
        /// </summary>
        private static double EncodeSrgb(double linear)
        {
            var value = Clamp01(linear);
            return value <= SrgbLinearLimit
                ? value * SrgbLinearSlope
                : SrgbScale * StablePow(value, 1.0 / SrgbExponent) - SrgbOffset;
        }

        /// <summary>
        /// <paramref name="value"/> to the power <paramref name="power"/>,
        /// rounded to <see cref="StableDecimals"/> decimals: <c>Math.Pow</c> is
        /// not exactly rounded, so a platform's last bit could otherwise tip a
        /// byte.
        /// </summary>
        private static double StablePow(double value, double power) =>
            Math.Round(Math.Pow(value, power), StableDecimals, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Hermite smoothstep from <paramref name="from"/> to
        /// <paramref name="to"/>: 0 at the first, 1 at the second, easing in
        /// between.
        /// </summary>
        private static double SmoothStep(double from, double to, double value) =>
            Smooth(Clamp01((value - from) / (to - from)));

        // The sRGB transfer function's constants (IEC 61966-2-1): a standard,
        // not a look parameter.
        private const double SrgbLinearLimit = 0.0031308;
        private const double SrgbLinearSlope = 12.92;
        private const double SrgbScale = 1.055;
        private const double SrgbOffset = 0.055;
        private const double SrgbExponent = 2.4;
    }
}
