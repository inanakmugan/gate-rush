using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// The state visuals of Module 46's second pass, ported from the reference
    /// script <c>mechanics_reference.py</c>: ice, the count badge, chains, the
    /// padlock, the key and its gem, the time-bonus coin, the one-way arrow,
    /// the shutter, the generator's machine and the elevator.
    /// </summary>
    /// <remarks>
    /// <para><b>The script's model.</b> Each part is a height field lit from
    /// the top left in linear light — its normal from the field's gradient,
    /// central differences as <c>numpy.gradient</c> takes them — composited
    /// over what lies under it, with soft shadows that are its mask blurred and
    /// moved. The painting below works in the script's frame: pixel centres
    /// at <c>i + ½</c>, y down, on a <see cref="Raster"/> holding premultiplied
    /// colour, so a part painted on a transparent canvas composites over the
    /// board exactly as the script's paints over its floor.</para>
    /// <para><b>Colour or grey.</b> A part whose colour never varies is painted
    /// in its <see cref="ArtPalette"/> colour, its highlight folded in, stored
    /// divided by <see cref="ArtRecipe.ShadeHeadroom"/> for the lit material.
    /// A part whose colour varies — a badge, a gem, the one-way arrow — is a
    /// grey shade for the tint and, apart, its white highlight for the
    /// specular material, which adds it: the lit sprite and its highlight
    /// together are the script's <c>colour × shade + highlight</c>.</para>
    /// <para><b>Stretching.</b> A part that fits any size is 9-sliced or tiled;
    /// its slice border is measured from the painted pixels, out from the
    /// middle while each row or column equals the middle one in every sprite
    /// of its set, so the stretched middle is uniform by construction.</para>
    /// <para><b>Determinism.</b> As the rest of the painter, plus
    /// <c>Math.Exp</c> and <c>Math.Sin</c>, rounded like the powers
    /// (<see cref="StablePow"/>).</para>
    /// </remarks>
    public static partial class ArtPainter
    {
        /// <summary>The three sprites of the count badge, painted together so they share one slice border.</summary>
        private enum BadgeLayer
        {
            Rim,
            Face,
            Highlight
        }

        /// <summary>True for the placed quarter set of the ice (Module 46, pass 2).</summary>
        private static bool IsIcePiece(ArtSprite sprite) => sprite >= ArtSprite.IceOuterTopLeft && sprite <= ArtSprite.IceFill;

        // ----- Ice: a placed quarter set over a frozen block or a closed gate -----

        /// <summary>
        /// One piece of ice for <paramref name="place"/>, over the lit body:
        /// the ice's own raised shape, its edge <see cref="ArtRecipe.IceInsetPixels"/>
        /// in from the cell's, concentric with the body's, so its distance is
        /// the body's plus the extra inset and it meets itself at every seam as
        /// the body does. Over the block it lays a thin tint in
        /// <see cref="ArtPalette.IceTint"/> shaded by the ice's light, its
        /// white highlight, and a bright rim just inside its edge in
        /// <see cref="ArtPalette.IceRim"/> — the script's <c>frozen()</c>.
        /// </summary>
        private static ArtImage PaintIce(ArtRecipe recipe, ArtPalette palette, Place place)
        {
            var model = new LitModel(recipe, recipe.IceBevelPixels, recipe.IceHeightPixels, recipe.IceFinish, false);
            var inward = recipe.IceInsetPixels - recipe.BlockGapPixels / 2.0;
            double tintAlpha = recipe.IceTintAlpha;
            double rimAlpha = recipe.IceRimAlpha;
            double rimDepth = recipe.IceRimDepthPixels;
            double rimWidth = recipe.IceRimWidthPixels;

            return PaintColor(recipe.QuarterPixels, recipe.QuarterPixels, recipe.ShadeHeadroom, (x, y) =>
            {
                var sd = SilhouetteSd(recipe, place.Shape, x, y) + inward;
                var (outX, outY) = BoardOutward(recipe, place, x, y);
                var (shade, specular) = model.At(-sd, outX, outY);
                var cover = Coverage(recipe, sd);
                var rim = rimAlpha * Clamp01(1.0 - Math.Abs(sd + rimDepth) / rimWidth);
                return Premultiplied.Clear
                    .Over(palette.IceTint * shade, tintAlpha * cover)
                    .Over(Linear.White, specular * cover)
                    .Over(palette.IceRim, rim);
            });
        }

        /// <summary>
        /// The frost streaks of a frozen block, white, drawn once per block at
        /// its top row's leftmost cell: diagonal Gaussian streaks across the
        /// script's offsets, fading in from the cell's left and top edges, out
        /// toward a lone cell's right edge (<paramref name="cells"/> is 1) and
        /// above the cell's bottom edge, and kept on the ice — whose top and
        /// left edges, and right edge for a lone cell, are the anchor cell's
        /// own. Two cells wide when the cell to the anchor's right is the
        /// block's.
        /// </summary>
        private static ArtImage PaintIceStreaks(ArtRecipe recipe, int cells)
        {
            var cell = recipe.CellPixels;
            var raster = new Raster(cells * cell, cell);
            var inset = recipe.IceInsetPixels;
            var corner = recipe.CornerRadiusPixels - (recipe.IceInsetPixels - recipe.BlockGapPixels / 2.0);

            // The ice's edge as the anchor cell sees it: a box from the inset
            // on the left and top, running on far to the right when the block
            // does, and far below the cell either way, so only the cell's own
            // exposed edges round it.
            var far = cell * FarCells;
            var right = cells > 1 ? far : cell - inset;
            var centreX = (inset + right) / 2.0;
            var halfWidth = (right - inset) / 2.0;
            var centreY = (inset + far) / 2.0;
            var halfHeight = (far - inset) / 2.0;

            var offsets = recipe.IceStreakOffsetsPixels;
            var left = recipe.IceStreakLeftFadePixels;
            var rightFade = recipe.IceStreakRightFadePixels;
            var top = recipe.IceStreakTopFadePixels;
            var width = cells * cell;
            var alpha = Field(raster, (x, y) =>
            {
                var ice = Clamp01(0.5 - Rr(x, y, centreX, centreY, halfWidth, halfHeight, corner));
                var clip = SmoothStep(left.x, left.y, x)
                           * SmoothStep(width - rightFade.x, width - rightFade.y, x)
                           * SmoothStep(top.x, top.y, y)
                           * (1.0 - SmoothStep(cell - recipe.IceStreakBottomFadePixels, cell, y))
                           * ice;
                var clear = 1.0;
                for (var k = 0; k < offsets.Count; k++)
                {
                    var t = (x + y - offsets[k]) / (recipe.IceStreakWidthPixels + k * recipe.IceStreakWidthStepPixels);
                    clear *= 1.0 - recipe.IceStreakAlpha * StableExp(-t * t) * clip;
                }

                return 1.0 - clear;
            });

            raster.Over(Linear.White, alpha);
            return raster.ToPlain(Vector4.zero);
        }

        // ----- Count badge -----

        /// <summary>
        /// One of the count badge's three sprites, for one digit — the script's
        /// <c>count_badge()</c>: a raised rim and the soft shadow under it (grey,
        /// tinted gold, or the colour a colour-bound shutter counts); a raised
        /// face inside it (grey, tinted red); and both highlights, white, for
        /// the specular material. 9-sliced along the width, so the badge
        /// widens with its digits; the three share one border.
        /// </summary>
        private static ArtImage PaintBadge(ArtRecipe recipe, BadgeLayer layer)
        {
            var size = recipe.BadgeSizePixels;
            var margin = recipe.BadgeShadow.ReachPixels;
            var rim = new Raster((int)Math.Ceiling(size.x) + 2 * margin, (int)Math.Ceiling(size.y) + 2 * margin);
            var face = new Raster(rim.Width, rim.Height);
            var cx = rim.Width / 2.0;
            var cy = rim.Height / 2.0;

            var rimShape = recipe.BadgeRim;
            var rimSd = Field(rim, (x, y) => Rr(x, y, cx, cy, size.x / 2.0, size.y / 2.0, rimShape.CornerPixels));
            var rimMask = Map(rimSd, d => Clamp01(0.5 - d));
            var rimHeight = Map(rimSd, d => d < 0.0 ? Bevel(d, rimShape.BevelPixels, rimShape.HeightPixels) : 0.0);

            var faceShape = recipe.BadgeFace;
            double inset = recipe.BadgeFaceInsetPixels;
            var faceSd = Field(rim, (x, y) => Rr(x, y, cx, cy, size.x / 2.0 - inset, size.y / 2.0 - inset, faceShape.CornerPixels));
            var faceMask = Map(faceSd, d => Clamp01(0.5 - d));
            var faceHeight = Map(faceSd, d => d < 0.0 ? Bevel(d, faceShape.BevelPixels, faceShape.HeightPixels) : 0.0);

            CastShadow(rim, rimMask, recipe.BadgeShadow, false);
            rim.OverShaded(Linear.White, LightOf(recipe, rim, rimHeight, recipe.BadgeRimFinish, false, false), rimMask, isSplit: true);
            face.OverShaded(Linear.White, LightOf(recipe, face, faceHeight, recipe.BadgeFaceFinish, false, false), faceMask, isSplit: true);

            // The face covers the rim's highlight where it lies over it, as it
            // covers the rim.
            var highlight = new double[rim.Count];
            for (var i = 0; i < highlight.Length; i++)
            {
                highlight[i] = rim.Highlight(i) * (1.0 - faceMask[i]) + face.Highlight(i);
            }

            var images = new[]
            {
                rim.ToLit(recipe.ShadeHeadroom, Vector4.zero),
                face.ToLit(recipe.ShadeHeadroom, Vector4.zero),
                HighlightImage(rim.Width, rim.Height, highlight, Vector4.zero)
            };

            var border = SlicedBorder(images, alongX: true, alongY: false);
            return WithBorder(images[(int)layer], border);
        }

        // ----- Chain, padlock, key, gem, coin -----

        /// <summary>
        /// One period of chain, tiled along a locked block's rows — the
        /// script's tube links: a flat ring in the middle and a side-on link
        /// straddling each end, silver, lit, with their soft shadow. The
        /// period's ends meet the next period's: the links, the shadow's blur
        /// and the normals are all taken round the period.
        /// </summary>
        private static ArtImage PaintChain(ArtRecipe recipe, ArtPalette palette)
        {
            var spacing = (int)Math.Round(recipe.ChainSpacingPixels);
            var ring = recipe.ChainRingRadiiPixels;
            var link = recipe.ChainLinkRadiiPixels;
            var ringReach = ring.y * (1.0 + (recipe.ChainRingTubePixels + 0.5) / Math.Min(ring.x, ring.y));
            var linkReach = link.y * (1.0 + (recipe.ChainLinkTubePixels + 0.5) / Math.Min(link.x, link.y));
            var half = (int)Math.Ceiling(Math.Max(ringReach, linkReach)) + recipe.ChainShadow.ReachPixels;
            var raster = new Raster(2 * spacing, 2 * half);
            var cy = raster.Height / 2.0;

            var mask = new double[raster.Count];
            var height = new double[raster.Count];

            // Links at every spacing, the side-on ones on the period's ends
            // and the rings between, a period either way beyond the tile so
            // what reaches in from a neighbouring period is there too.
            for (var k = -2; k <= 4; k++)
            {
                var isRing = k % 2 != 0;
                var radii = isRing ? ring : link;
                var tube = isRing ? recipe.ChainRingTubePixels : recipe.ChainLinkTubePixels;
                var rise = isRing ? recipe.ChainRingHeightPixels : recipe.ChainLinkHeightPixels;
                var lx = k * (double)spacing;
                for (var i = 0; i < raster.Count; i++)
                {
                    var (m, h) = TubeRing(raster.X(i), raster.Y(i), lx, cy, radii.x, radii.y, tube, rise);
                    mask[i] = Math.Max(mask[i], m);
                    height[i] = Math.Max(height[i], h);
                }
            }

            CastShadow(raster, mask, recipe.ChainShadow, true);
            raster.OverShaded(palette.Silver, LightOf(recipe, raster, height, recipe.ChainFinish, true, false), mask, isSplit: false);
            return raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
        }

        /// <summary>
        /// The padlock on a locked block — the script's: a silver tube
        /// shackle whose legs run behind the body, a raised gold body below the
        /// mark's centre, and a keyhole recessed into it in
        /// <see cref="ArtPalette.Keyhole"/>, each with its soft shadow. The keys
        /// still needed are a label on the body, not part of the sprite.
        /// </summary>
        private static ArtImage PaintLock(ArtRecipe recipe, ArtPalette palette)
        {
            var body = recipe.LockBodyHalfSizePixels;
            var shackle = recipe.LockShackleRadiiPixels;
            double drop = recipe.LockDropPixels;
            double rise = recipe.LockShackleRisePixels;
            var shackleTop = rise - drop + shackle.y + recipe.LockShackleTubePixels + 0.5;
            var halfWidth = (int)Math.Ceiling(Math.Max(
                body.x + recipe.LockShadow.ReachPixels,
                shackle.x + recipe.LockShackleTubePixels + 0.5 + recipe.LockShackleShadow.ReachPixels));
            var halfHeight = (int)Math.Ceiling(Math.Max(
                shackleTop + recipe.LockShackleShadow.ReachPixels,
                drop + body.y + recipe.LockShadow.ReachPixels));
            var raster = new Raster(2 * halfWidth, 2 * halfHeight);
            var bx = raster.Width / 2.0;
            var by = raster.Height / 2.0 + drop;

            var shackleMask = new double[raster.Count];
            var shackleHeight = new double[raster.Count];
            for (var i = 0; i < raster.Count; i++)
            {
                var y = raster.Y(i);
                var (m, h) = TubeRing(
                    raster.X(i), y, bx, by - rise, shackle.x, shackle.y, recipe.LockShackleTubePixels, recipe.LockShackleHeightPixels);
                shackleMask[i] = y < by - recipe.LockShackleCutPixels ? m : 0.0;
                shackleHeight[i] = h;
            }

            CastShadow(raster, shackleMask, recipe.LockShackleShadow, false);
            raster.OverShaded(
                palette.Silver, LightOf(recipe, raster, shackleHeight, recipe.LockShackleFinish, false, false), shackleMask, isSplit: false);

            var shape = recipe.LockBody;
            var sd = Field(raster, (x, y) => Rr(x, y, bx, by, body.x, body.y, shape.CornerPixels));
            var bodyMask = Map(sd, d => Clamp01(0.5 - d));
            var holeTop = by - recipe.KeyholeRisePixels;
            var hole = Field(raster, (x, y) =>
                Length(x - bx, y - holeTop) < recipe.KeyholeRadiusPixels
                || (Math.Abs(x - bx) < recipe.KeyholeSlotHalfWidthPixels && y > holeTop && y < by + recipe.KeyholeSlotEndPixels)
                    ? 1.0
                    : 0.0);
            var recess = Blurred(hole, raster.Width, raster.Height, recipe.KeyholeBlurPixels, false);
            var bodyHeight = new double[raster.Count];
            for (var i = 0; i < raster.Count; i++)
            {
                var rising = sd[i] < 0.0 ? Bevel(sd[i], shape.BevelPixels, shape.HeightPixels) : 0.0;
                bodyHeight[i] = rising - recess[i] * recipe.KeyholeDepthPixels;
            }

            CastShadow(raster, bodyMask, recipe.LockShadow, false);
            raster.OverShaded(palette.Gold, LightOf(recipe, raster, bodyHeight, recipe.LockFinish, false, false), bodyMask, isSplit: false);
            raster.Over(palette.Keyhole, Map2(hole, bodyMask, (h, m) => h * m * recipe.KeyholeAlpha));
            return raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
        }

        /// <summary>The canvas of the key and of its gem, in pixels, centred on the mark's centre.</summary>
        private static (int width, int height) GoldKeyCanvas(ArtRecipe recipe)
        {
            var first = recipe.GoldKeyFirstToothPixels;
            var second = recipe.GoldKeySecondToothPixels;
            var leftReach = recipe.GoldKeyBowOffsetPixels + recipe.GoldKeyBowRadiusPixels;
            var rightReach = Math.Max(recipe.GoldKeyShaftEndPixels, Math.Max(first.y, second.y));
            var downReach = Math.Max(recipe.GoldKeyBowRadiusPixels, Math.Max(first.z, second.z));
            var margin = recipe.GoldKeyShadow.ReachPixels;
            return (
                2 * ((int)Math.Ceiling(Math.Max(leftReach, rightReach)) + margin),
                2 * ((int)Math.Ceiling(downReach) + margin));
        }

        /// <summary>
        /// The key on a key-carrying block — the script's, its shaft and bow
        /// 1.2 times as thick: a gold key lying horizontally, its height its
        /// shape blurred, its edge softened, with its soft shadow and the dark
        /// ring of the gem's seat in its bow. The gem is <see cref="PaintGem"/>'s,
        /// on the same canvas.
        /// </summary>
        private static ArtImage PaintGoldKey(ArtRecipe recipe, ArtPalette palette)
        {
            var (width, height) = GoldKeyCanvas(recipe);
            var raster = new Raster(width, height);
            var kx = width / 2.0;
            var ky = height / 2.0;
            var bowX = kx - recipe.GoldKeyBowOffsetPixels;
            var first = recipe.GoldKeyFirstToothPixels;
            var second = recipe.GoldKeySecondToothPixels;

            var key = Field(raster, (x, y) =>
                Length(x - bowX, y - ky) < recipe.GoldKeyBowRadiusPixels
                || (Math.Abs(y - ky) < recipe.GoldKeyShaftHalfPixels && x > bowX && x < kx + recipe.GoldKeyShaftEndPixels)
                || (x > kx + first.x && x < kx + first.y && y > ky && y < ky + first.z)
                || (x > kx + second.x && x < kx + second.y && y > ky && y < ky + second.z)
                    ? 1.0
                    : 0.0);

            CastShadow(raster, key, recipe.GoldKeyShadow, false);
            var rising = Map(Blurred(key, width, height, recipe.GoldKeyRoundingPixels, false), b => b * recipe.GoldKeyHeightPixels);
            var edge = Blurred(key, width, height, recipe.GoldKeyEdgePixels, false);
            raster.OverShaded(palette.Gold, LightOf(recipe, raster, rising, recipe.GoldKeyFinish, false, false), edge, isSplit: false);

            raster.Darken(Field(raster, (x, y) =>
                recipe.GemSeatDarken
                * Clamp01(1.0 - Math.Abs(Length(x - bowX, y - ky) - recipe.GemSeatRadiusPixels) / recipe.GemSeatWidthPixels)));
            return raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
        }

        /// <summary>
        /// The gem in the key's bow, on the key's canvas: a smooth dome, grey
        /// for the lock's colour to tint (D47), or its white highlight.
        /// </summary>
        private static ArtImage PaintGem(ArtRecipe recipe, bool isHighlight)
        {
            var (width, height) = GoldKeyCanvas(recipe);
            var raster = new Raster(width, height);
            var bowX = width / 2.0 - recipe.GoldKeyBowOffsetPixels;
            var ky = height / 2.0;
            double radius = recipe.GemRadiusPixels;

            var distance = Field(raster, (x, y) => Length(x - bowX, y - ky));
            var dome = Map(distance, g => Math.Sqrt(Clamp01(1.0 - g / radius * (g / radius))) * recipe.GemHeightPixels);
            var mask = Map(distance, g => g < radius ? 1.0 : 0.0);
            raster.OverShaded(Linear.White, LightOf(recipe, raster, dome, recipe.GemFinish, false, false), mask, isSplit: true);
            return isHighlight
                ? HighlightImage(width, height, raster.HighlightField(), Vector4.zero)
                : raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
        }

        /// <summary>
        /// The time-bonus coin — <c>time_bonus_reference.py</c>'s <c>coin()</c>,
        /// option D: a raised coin in the config's coin colour (green), its
        /// face set in and in the face colour (cream), two hands in the hand
        /// colour (dark green) at twelve and three, scaled with the coin
        /// (<see cref="ArtRecipe.CoinMinuteHandPixels"/>), and its soft shadow,
        /// centred on its canvas. The bonus beside it is a label.
        /// </summary>
        private static ArtImage PaintCoin(ArtRecipe recipe, ArtPalette palette)
        {
            double radius = recipe.CoinRadiusPixels;
            var half = (int)Math.Ceiling(radius) + recipe.CoinShadow.ReachPixels;
            var raster = new Raster(2 * half, 2 * half);
            var cx = raster.Width / 2.0;
            var cy = raster.Height / 2.0;

            var distance = Field(raster, (x, y) => Length(x - cx, y - cy));
            var coin = Map(distance, r => r < radius ? 1.0 : 0.0);
            CastShadow(raster, coin, recipe.CoinShadow, false);

            var rising = Map(distance, r =>
                (r < radius ? Clamp01((radius - r) / recipe.CoinRimPixels) * recipe.CoinHeightPixels : 0.0)
                - (r < recipe.CoinFaceRadiusPixels ? recipe.CoinFaceDepthPixels : 0.0));
            raster.OverShaded(palette.Coin, LightOf(recipe, raster, rising, recipe.CoinFinish, false, false), coin, isSplit: false);
            raster.Over(palette.CoinFace, Map(distance, r => Clamp01(recipe.CoinFaceRadiusPixels + recipe.CoinFaceEdgePixels - r)));

            double hand = recipe.CoinHandHalfWidthPixels;
            raster.Over(palette.CoinHands, Field(raster, (x, y) =>
                Math.Abs(x - cx) < hand && y < cy && y > cy - recipe.CoinMinuteHandPixels ? 1.0 : 0.0));
            raster.Over(palette.CoinHands, Field(raster, (x, y) =>
                Math.Abs(y - cy) < hand && x > cx - recipe.CoinHourHandTailPixels && x < cx + recipe.CoinHourHandPixels ? 1.0 : 0.0));
            return raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
        }

        // ----- One-way arrow -----

        /// <summary>
        /// The one-way arrow along a block's axis — the script's
        /// <c>one_way()</c>: a double-headed arrow raised on the block, its soft
        /// shadow, a thin darker edge, and its face a touch lighter than the
        /// block. Grey for the block's colour to tint, or white for the
        /// specular material: the lightening and the highlight. Painted for a
        /// block <see cref="ArtRecipe.OneWayPaintedCells"/> long and 9-sliced
        /// along its length, so its heads keep their shape on any block. A
        /// vertical arrow is painted, not turned, so its light still falls from
        /// the top left.
        /// </summary>
        private static ArtImage PaintOneWayArrow(ArtRecipe recipe, bool isVertical, bool isHighlight)
        {
            var margin = recipe.OneWayMarginPixels;
            var headHalf = (recipe.OneWayTipPixels - recipe.OneWayHeadStartPixels) * recipe.OneWayHeadSlope;
            var along = 2 * ((int)Math.Ceiling(recipe.OneWayTipPixels) + margin);
            var across = 2 * ((int)Math.Ceiling(Math.Max(headHalf, recipe.OneWayShaftHalfThicknessPixels) + margin));
            var raster = isVertical ? new Raster(across, along) : new Raster(along, across);
            var cx = raster.Width / 2.0;
            var cy = raster.Height / 2.0;

            var arrow = Field(raster, (x, y) =>
            {
                var a = Math.Abs(isVertical ? y - cy : x - cx);
                var c = Math.Abs(isVertical ? x - cx : y - cy);
                var isShaft = a < recipe.OneWayShaftHalfLengthPixels && c < recipe.OneWayShaftHalfThicknessPixels;
                var isHead = a >= recipe.OneWayHeadStartPixels && a < recipe.OneWayTipPixels
                             && c < (recipe.OneWayTipPixels - a) * recipe.OneWayHeadSlope;
                return isShaft || isHead ? 1.0 : 0.0;
            });

            var rounded = Blurred(arrow, raster.Width, raster.Height, recipe.OneWayRoundingPixels, false);
            CastShadow(raster, arrow, recipe.OneWayShadow, false);
            raster.Over(
                Linear.Grey(recipe.OneWayEdgeTone),
                Map(Grown(arrow, raster.Width, raster.Height, recipe.OneWayEdgeGrowPixels), g => g * recipe.OneWayEdgeAlpha));

            var lit = LightOf(recipe, raster, Map(rounded, b => b * recipe.OneWayRaisePixels), recipe.OneWayFinish, false, false);
            var face = Map2(rounded, arrow, (b, m) => Clamp01(b * recipe.OneWayAlphaGain) * Clamp01(m + b));
            double lighten = recipe.OneWayLighten;
            var shade = Map(lit.Shade, s => s * (1.0 - lighten));
            var highlight = Map2(lit.Shade, lit.Specular, (s, p) => s * lighten + p);
            raster.OverShaded(Linear.White, new Lit(shade, highlight), face, isSplit: true);

            var images = new[]
            {
                raster.ToLit(recipe.ShadeHeadroom, Vector4.zero),
                HighlightImage(raster.Width, raster.Height, raster.HighlightField(), Vector4.zero)
            };

            var border = SlicedBorder(images, alongX: !isVertical, alongY: isVertical);
            return WithBorder(images[isHighlight ? 1 : 0], border);
        }

        // ----- Shutter -----

        /// <summary>The side, in pixels, of a raised panel's canvas painted for a region <paramref name="cells"/> wide: the region and its floor shadow.</summary>
        private static int PanelCanvas(ArtRecipe recipe, int cells) => cells * recipe.CellPixels + 2 * recipe.RaisedShadow.ReachPixels;

        /// <summary>
        /// A closed shutter's rim — the script's <c>shutter()</c> without its
        /// slats: a raised gold rim round the region with the region's soft
        /// floor shadow, and inside its inner edge the recess's darkening over
        /// the slats it is drawn above. The shadow is kept out of the slats'
        /// area, which the slats cover in the script. 9-sliced both ways.
        /// </summary>
        private static ArtImage PaintShutterRim(ArtRecipe recipe, ArtPalette palette)
        {
            var side = PanelCanvas(recipe, recipe.ShutterPaintedCells);
            var raster = new Raster(side, side);
            var c = side / 2.0;
            var half = recipe.ShutterPaintedCells * recipe.CellPixels / 2.0;

            var rim = recipe.ShutterRim;
            var rimSd = Field(raster, (x, y) => Rr(x, y, c, c, half - recipe.ShutterRimInsetPixels, half - recipe.ShutterRimInsetPixels, rim.CornerPixels));
            var inner = Field(raster, (x, y) =>
                Rr(x, y, c, c, half - recipe.ShutterInnerInsetPixels, half - recipe.ShutterInnerInsetPixels, recipe.ShutterInnerCornerPixels));
            var mask = Map(rimSd, d => Clamp01(0.5 - d));
            var innerMask = Map(inner, d => Clamp01(0.5 - d));
            var height = Map2(rimSd, inner, (d, n) => n < 0.0 || d >= 0.0 ? 0.0 : Bevel(d, rim.BevelPixels, rim.HeightPixels));

            CastShadow(raster, mask, recipe.RaisedShadow, false, innerMask);
            raster.OverShaded(
                palette.ShutterRim, LightOf(recipe, raster, height, recipe.ShutterRimFinish, false, false),
                Map2(mask, inner, (m, n) => m * Clamp01(0.5 + n)), isSplit: false);
            raster.Darken(Map2(innerMask, inner, (m, n) =>
                recipe.ShutterRecessDarken * m * (1.0 - SmoothStep(0.0, recipe.ShutterRecessPixels, -n))));

            var image = raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
            return WithBorder(image, SlicedBorder(new[] { image }, alongX: true, alongY: true));
        }

        /// <summary>The width, in pixels, of a tile uniform across: the slats'. Not a look parameter.</summary>
        private const int UniformTilePixels = 4;

        /// <summary>
        /// One slat of a closed shutter, tiled over its region: a lit cylinder
        /// in <see cref="ArtPalette.ShutterCylinder"/>, its profile a half sine,
        /// darkening toward the gap at its top and bottom. Opaque; its normals
        /// are taken round the tile, so tiles meet without a seam.
        /// </summary>
        private static ArtImage PaintShutterCylinders(ArtRecipe recipe, ArtPalette palette)
        {
            var pitch = recipe.ShutterCylinderPitchPixels;
            var raster = new Raster(UniformTilePixels, pitch);
            var profile = Field(raster, (x, y) => StableSin(Math.PI * y / pitch));
            var height = Map(profile, s => s * recipe.ShutterCylinderHeightPixels);

            raster.OverShaded(
                palette.ShutterCylinder, LightOf(recipe, raster, height, recipe.ShutterCylinderFinish, true, true),
                Map(profile, s => 1.0), isSplit: false);
            raster.Darken(Map(profile, s => recipe.ShutterGapDarken * Clamp01(1.0 - s * recipe.ShutterGapSharpness)));
            return raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
        }

        // ----- Generator machine -----

        /// <summary>
        /// A generator's body — the script's <c>generator()</c> body: raised,
        /// rounded metal with its floor shadow and a rivet in each corner,
        /// painted for a one-cell machine and 9-sliced both ways, the rivets in
        /// the corners, so it fits any machine. Its edge is
        /// <see cref="ArtRecipe.MachineInsetPixels"/> inside the machine's
        /// rectangle, which the sprite's middle spans less its shadow margin.
        /// </summary>
        private static ArtImage PaintMachineBody(ArtRecipe recipe, ArtPalette palette)
        {
            var side = PanelCanvas(recipe, 1);
            var raster = new Raster(side, side);
            var c = side / 2.0;
            var half = recipe.CellPixels / 2.0 - recipe.MachineInsetPixels;
            var body = recipe.MachineBody;

            var sd = Field(raster, (x, y) => Rr(x, y, c, c, half, half, body.CornerPixels));
            var mask = Map(sd, d => Clamp01(0.5 - d));
            CastShadow(raster, mask, recipe.RaisedShadow, false);
            raster.OverShaded(
                palette.MachineMetal,
                LightOf(recipe, raster, Map(sd, d => d < 0.0 ? Bevel(d, body.BevelPixels, body.HeightPixels) : 0.0), recipe.MachineFinish, false, false),
                mask, isSplit: false);

            var at = half - recipe.MachineRivetInsetPixels;
            double radius = recipe.MachineRivetRadiusPixels;
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                var distance = Field(raster, (x, y) => Length(x - (c + sx * at), y - (c + sy * at)));
                var dome = Map(distance, r => Math.Sqrt(Clamp01(1.0 - r / radius * (r / radius))) * recipe.MachineRivetHeightPixels);
                raster.OverShaded(
                    palette.MachineRivet, LightOf(recipe, raster, dome, recipe.MachineRivetFinish, false, false),
                    Map(distance, r => r < radius ? 1.0 : 0.0), isSplit: false);
            }

            var image = raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
            return WithBorder(image, SlicedBorder(new[] { image }, alongX: true, alongY: true));
        }

        /// <summary>
        /// A generator's screen — the script's, over the flat of the body: the
        /// slope down into it in the body's metal, the glass lightening
        /// downward, its edge darkened under the slope, and a faint glare kept
        /// in its left corner so it survives the slicing. The next block and
        /// the count are drawn over it at runtime. 9-sliced across by its
        /// measured border, and down by its corner and shadow, through which
        /// the glass's gradient stretches.
        /// </summary>
        private static ArtImage PaintMachineScreen(ArtRecipe recipe, ArtPalette palette)
        {
            var size = recipe.MachineScreenSizePixels;
            var raster = new Raster((int)size.x, (int)size.y);
            var cx = raster.Width / 2.0;
            var cy = raster.Height / 2.0;
            double depth = recipe.MachineScreenDepthPixels;
            double slope = recipe.MachineScreenSlopePixels;

            var sd = Field(raster, (x, y) => Rr(x, y, cx, cy, size.x / 2.0, size.y / 2.0, recipe.MachineScreenCornerPixels));
            var mask = Map(sd, d => Clamp01(0.5 - d));
            var height = Map(sd, d => d < 0.0 ? -Clamp01(-d / slope) * depth : 0.0);
            raster.OverShaded(palette.MachineMetal, LightOf(recipe, raster, height, recipe.MachineFinish, false, false), mask, isSplit: false);

            var fade = recipe.MachineGlassFadePixels;
            var gradient = recipe.MachineGlassGradientPixels;
            for (var i = 0; i < raster.Count; i++)
            {
                var shade = recipe.MachineGlassBaseShade
                            + recipe.MachineGlassShadeGain * SmoothStep(gradient.x, gradient.y, raster.Y(i) - cy);
                raster.Over(i, palette.MachineGlass * shade, mask[i] * SmoothStep(-fade.x, -fade.y, sd[i]));
            }

            raster.Darken(Map2(mask, sd, (m, d) =>
                recipe.MachineScreenShadowDarken * m * (1.0 - SmoothStep(0.0, recipe.MachineScreenShadowPixels, -d))));

            var inset = recipe.MachineGlareInsetPixels;
            var glareFade = recipe.MachineGlareFadePixels;
            raster.Over(Linear.White, Field(raster, (x, y) =>
            {
                var t = (x - y + recipe.MachineGlareDropPixels) / recipe.MachineGlareWidthPixels;
                var d = Rr(x, y, cx, cy, size.x / 2.0, size.y / 2.0, recipe.MachineScreenCornerPixels);
                return recipe.MachineGlareAlpha * StableExp(-t * t) * Clamp01(0.5 - d) * SmoothStep(-inset.x, -inset.y, d)
                       * (1.0 - SmoothStep(glareFade.x, glareFade.y, x));
            }));

            var image = raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
            var across = SlicedBorder(new[] { image }, alongX: true, alongY: false);
            var down = (float)Math.Ceiling(recipe.MachineScreenCornerPixels + recipe.MachineScreenShadowPixels);
            return WithBorder(image, new Vector4(across.x, down, across.z, down));
        }

        /// <summary>The generator's vent — the script's: slots darkening the metal under them, whole.</summary>
        private static ArtImage PaintMachineVent(ArtRecipe recipe)
        {
            var slot = recipe.MachineVentSlotHalfSizePixels;
            var count = recipe.MachineVentSlots;
            double pitch = recipe.MachineVentPitchPixels;
            var raster = new Raster(
                2 * ((int)Math.Ceiling(slot.x) + 1),
                2 * ((int)Math.Ceiling((count - 1) * pitch / 2.0 + slot.y) + 1));
            var cx = raster.Width / 2.0;
            var cy = raster.Height / 2.0;
            for (var k = 0; k < count; k++)
            {
                var sy = cy + (k - (count - 1) / 2.0) * pitch;
                raster.Darken(Field(raster, (x, y) =>
                    recipe.MachineVentDarken * Clamp01(0.5 - Rr(x, y, cx, sy, slot.x, slot.y, recipe.MachineVentSlotCornerPixels))));
            }

            return raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
        }

        /// <summary>The generator's button — the script's: raised, orange, with its shadow, whole.</summary>
        private static ArtImage PaintMachineButton(ArtRecipe recipe, ArtPalette palette) =>
            PaintRaisedBox(
                recipe, recipe.MachineButtonHalfSizePixels, recipe.MachineButton, recipe.MachineButtonFinish,
                recipe.MachineButtonShadow, palette.MachineButton);

        // ----- Elevator -----

        /// <summary>
        /// An elevator's rim — the script's <c>elevator()</c> without its doors
        /// and hazard strips: a raised orange rim with its floor shadow, and
        /// the dark recess inside its inner edge, which the doors stand in.
        /// 9-sliced both ways.
        /// </summary>
        private static ArtImage PaintElevatorRim(ArtRecipe recipe, ArtPalette palette)
        {
            var raster = ElevatorRimRaster(recipe, palette, (x, y) => palette.ElevatorRim, out var innerMask);
            raster.Over(palette.ElevatorRecess, innerMask);
            var image = raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
            return WithBorder(image, SlicedBorder(new[] { image }, alongX: true, alongY: true));
        }

        /// <summary>
        /// One tile of an elevator's hazard strip, down its left or right side:
        /// the script's stripes in <see cref="ArtPalette.Hazard"/> and
        /// <see cref="ArtPalette.HazardDark"/>, lit as the rim under them —
        /// cut from the rim painted with them, at the strip's columns and one
        /// period of rows from the strip's top end. Tiled down the strip; the
        /// stripes repeat every two stripes.
        /// </summary>
        private static ArtImage PaintElevatorHazard(ArtRecipe recipe, ArtPalette palette, bool isLeft)
        {
            var cell = recipe.CellPixels;
            var half = recipe.ElevatorPaintedCells * cell / 2.0;
            var side = PanelCanvas(recipe, recipe.ElevatorPaintedCells);
            var c = side / 2.0;
            double stripe = recipe.ElevatorHazardStripePixels;

            bool InStrip(double x, double y) =>
                Math.Abs(Math.Abs(x - c) - (half - recipe.ElevatorHazardCentrePixels)) < recipe.ElevatorHazardHalfWidthPixels
                && Math.Abs(y - c) < half - recipe.ElevatorHazardEndPixels;

            Linear ColourAt(double x, double y) =>
                !InStrip(x, y) ? palette.ElevatorRim
                : Math.Floor((y + x) / stripe) % 2.0 == 0.0 ? palette.Hazard
                : palette.HazardDark;

            var raster = ElevatorRimRaster(recipe, palette, ColourAt, out _);

            // The strip's columns on its side, and one period of rows from its
            // top end.
            var columns = new List<int>();
            for (var i = 0; i < side; i++)
            {
                var x = i + 0.5;
                if ((isLeft ? x < c : x > c) && InStrip(x, c))
                {
                    columns.Add(i);
                }
            }

            var firstRow = 0;
            while (!InStrip(columns[0] + 0.5, firstRow + 0.5))
            {
                firstRow++;
            }

            var rows = 2 * recipe.ElevatorHazardStripePixels;
            return raster.CropLit(recipe.ShadeHeadroom, columns[0], firstRow, columns.Count, rows);
        }

        /// <summary>
        /// An elevator's rim painted for <see cref="ArtRecipe.ElevatorPaintedCells"/>,
        /// with its floor shadow, in <paramref name="colourAt"/> — orange, or
        /// with the hazard stripes — and the mask of the recess inside it.
        /// </summary>
        private static Raster ElevatorRimRaster(
            ArtRecipe recipe, ArtPalette palette, Func<double, double, Linear> colourAt, out double[] innerMask)
        {
            var side = PanelCanvas(recipe, recipe.ElevatorPaintedCells);
            var raster = new Raster(side, side);
            var c = side / 2.0;
            var half = recipe.ElevatorPaintedCells * recipe.CellPixels / 2.0;
            var rim = recipe.ElevatorRim;
            var inner = recipe.ElevatorInnerInsetPixels;

            var rimSd = Field(raster, (x, y) => Rr(x, y, c, c, half - recipe.ElevatorRimInsetPixels, half - recipe.ElevatorRimInsetPixels, rim.CornerPixels));
            var innerSd = Field(raster, (x, y) => Rr(x, y, c, c, half - inner.x, half - inner.y, recipe.ElevatorInnerCornerPixels));
            var mask = Map(rimSd, d => Clamp01(0.5 - d));
            innerMask = Map(innerSd, d => Clamp01(0.5 - d));
            var height = Map2(rimSd, innerSd, (d, n) => n < 0.0 || d >= 0.0 ? 0.0 : Bevel(d, rim.BevelPixels, rim.HeightPixels));

            CastShadow(raster, mask, recipe.RaisedShadow, false);
            var lit = LightOf(recipe, raster, height, recipe.ElevatorRimFinish, false, false);
            for (var i = 0; i < raster.Count; i++)
            {
                var colour = colourAt(raster.X(i), raster.Y(i)) * lit.Shade[i] + Linear.White * lit.Specular[i];
                raster.Over(i, colour, mask[i] * Clamp01(0.5 + innerSd[i]));
            }

            return raster;
        }

        /// <summary>
        /// One of an elevator's two doors — the script's: raised purple
        /// plastic with an inset panel set into it and its soft shadow on the
        /// recess, painted for the doors of an elevator
        /// <see cref="ArtRecipe.ElevatorPaintedCells"/> wide and 9-sliced both
        /// ways. Both doors are this sprite; the lighting is the same on each.
        /// </summary>
        private static ArtImage PaintElevatorDoor(ArtRecipe recipe, ArtPalette palette)
        {
            var half = recipe.ElevatorDoorHalfSizePixels;
            var margin = recipe.ElevatorDoorShadow.ReachPixels;
            var raster = new Raster(2 * ((int)Math.Ceiling(half.x) + margin), 2 * ((int)Math.Ceiling(half.y) + margin));
            var cx = raster.Width / 2.0;
            var cy = raster.Height / 2.0;
            var door = recipe.ElevatorDoor;
            var panel = new Vector2(half.x - recipe.ElevatorDoorPanelInsetPixels.x, half.y - recipe.ElevatorDoorPanelInsetPixels.y);

            var sd = Field(raster, (x, y) => Rr(x, y, cx, cy, half.x, half.y, door.CornerPixels));
            var panelSd = Field(raster, (x, y) => Rr(x, y, cx, cy, panel.x, panel.y, recipe.ElevatorDoorPanelCornerPixels));
            var mask = Map(sd, d => Clamp01(0.5 - d));
            var height = Map2(sd, panelSd, (d, p) =>
                (d < 0.0 ? Bevel(d, door.BevelPixels, door.HeightPixels) : 0.0)
                - (p < 0.0 ? Clamp01(-p / recipe.ElevatorDoorPanelSlopePixels) * recipe.ElevatorDoorPanelDepthPixels : 0.0));

            CastShadow(raster, mask, recipe.ElevatorDoorShadow, false);
            raster.OverShaded(palette.ElevatorDoor, LightOf(recipe, raster, height, recipe.ElevatorDoorFinish, false, false), mask, isSplit: false);
            var image = raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
            return WithBorder(image, SlicedBorder(new[] { image }, alongX: true, alongY: true));
        }

        /// <summary>A door's handle — the script's: a raised silver bar with its shadow, whole.</summary>
        private static ArtImage PaintElevatorHandle(ArtRecipe recipe, ArtPalette palette) =>
            PaintRaisedBox(
                recipe, recipe.ElevatorHandleHalfSizePixels, recipe.ElevatorHandle, recipe.ElevatorHandleFinish,
                recipe.ElevatorHandleShadow, palette.ElevatorHandle);

        /// <summary>A raised rounded box in <paramref name="colour"/> with its soft shadow, centred on its canvas: a button, a handle.</summary>
        private static ArtImage PaintRaisedBox(
            ArtRecipe recipe, Vector2 half, RaisedShape shape, LitFinish finish, SoftShadow shadow, Linear colour)
        {
            var raster = new Raster(
                2 * ((int)Math.Ceiling(half.x) + shadow.ReachPixels), 2 * ((int)Math.Ceiling(half.y) + shadow.ReachPixels));
            var cx = raster.Width / 2.0;
            var cy = raster.Height / 2.0;
            var sd = Field(raster, (x, y) => Rr(x, y, cx, cy, half.x, half.y, shape.CornerPixels));
            var mask = Map(sd, d => Clamp01(0.5 - d));
            CastShadow(raster, mask, shadow, false);
            raster.OverShaded(
                colour,
                LightOf(recipe, raster, Map(sd, d => d < 0.0 ? Bevel(d, shape.BevelPixels, shape.HeightPixels) : 0.0), finish, false, false),
                mask, isSplit: false);
            return raster.ToLit(recipe.ShadeHeadroom, Vector4.zero);
        }

        // ----- The script's operations -----

        /// <summary>
        /// How far, in cells, a box standing for "the rest of the block" runs
        /// past a frost streak's canvas, so its far corners never round the
        /// streaks. Not a look parameter.
        /// </summary>
        private const int FarCells = 10;

        /// <summary>
        /// The script's <c>rr()</c>: the signed distance to a rounded box
        /// centred on <c>(cx, cy)</c> with half extents <paramref name="hw"/>,
        /// <paramref name="hh"/> and corner radius <paramref name="r"/>.
        /// </summary>
        private static double Rr(double x, double y, double cx, double cy, double hw, double hh, double r)
        {
            var qx = Math.Abs(x - cx) - (hw - r);
            var qy = Math.Abs(y - cy) - (hh - r);
            return Length(Math.Max(qx, 0.0), Math.Max(qy, 0.0)) + Math.Min(Math.Max(qx, qy), 0.0) - r;
        }

        /// <summary>The script's <c>bevel()</c>: a quarter circle rising over <paramref name="bevel"/> pixels inside an edge to <paramref name="height"/>.</summary>
        private static double Bevel(double d, double bevel, double height)
        {
            var t = Clamp01(-d / bevel);
            return Math.Sqrt(Clamp01(1.0 - (1.0 - t) * (1.0 - t))) * height;
        }

        /// <summary>
        /// The script's <c>tube_ring()</c>: an elliptic ring of tube with radii
        /// <paramref name="a"/>, <paramref name="b"/> about <c>(cx, cy)</c> — its
        /// coverage and its round height.
        /// </summary>
        private static (double mask, double height) TubeRing(
            double x, double y, double cx, double cy, double a, double b, double tube, double height)
        {
            var e = Length((x - cx) / a, (y - cy) / b);
            var distance = Math.Abs(e - 1.0) * Math.Min(a, b);
            return (Clamp01(tube - distance + 0.5), Math.Sqrt(Clamp01(1.0 - distance / tube * (distance / tube))) * height);
        }

        /// <summary><paramref name="value"/>'s exponential, rounded as <see cref="StablePow"/> rounds a power.</summary>
        private static double StableExp(double value) =>
            Math.Round(Math.Exp(value), StableDecimals, MidpointRounding.AwayFromZero);

        /// <summary><paramref name="value"/>'s sine, rounded as <see cref="StablePow"/> rounds a power.</summary>
        private static double StableSin(double value) =>
            Math.Round(Math.Sin(value), StableDecimals, MidpointRounding.AwayFromZero);

        /// <summary><paramref name="f"/> at every pixel centre of <paramref name="raster"/>, row by row from the top.</summary>
        private static double[] Field(Raster raster, Func<double, double, double> f)
        {
            var field = new double[raster.Count];
            for (var i = 0; i < field.Length; i++)
            {
                field[i] = f(raster.X(i), raster.Y(i));
            }

            return field;
        }

        private static double[] Map(double[] field, Func<double, double> f)
        {
            var mapped = new double[field.Length];
            for (var i = 0; i < field.Length; i++)
            {
                mapped[i] = f(field[i]);
            }

            return mapped;
        }

        private static double[] Map2(double[] a, double[] b, Func<double, double, double> f)
        {
            var mapped = new double[a.Length];
            for (var i = 0; i < a.Length; i++)
            {
                mapped[i] = f(a[i], b[i]);
            }

            return mapped;
        }

        /// <summary>
        /// <paramref name="field"/> blurred by a Gaussian of standard deviation
        /// <paramref name="sigma"/>, cut off at <see cref="ArtRecipe.BlurReachSigmas"/>:
        /// across, then down. Beyond the canvas lies nothing, or, when
        /// <paramref name="wrapX"/>, the tile's other end.
        /// </summary>
        private static double[] Blurred(double[] field, int width, int height, double sigma, bool wrapX)
        {
            var reach = (int)Math.Ceiling(ArtRecipe.BlurReachSigmas * sigma);
            var weights = new double[2 * reach + 1];
            var total = 0.0;
            for (var k = -reach; k <= reach; k++)
            {
                weights[k + reach] = StableExp(-k * k / (2.0 * sigma * sigma));
                total += weights[k + reach];
            }

            var across = new double[field.Length];
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var sum = 0.0;
                    for (var k = -reach; k <= reach; k++)
                    {
                        var at = i + k;
                        if (wrapX)
                        {
                            at = ((at % width) + width) % width;
                        }
                        else if (at < 0 || at >= width)
                        {
                            continue;
                        }

                        sum += weights[k + reach] * field[j * width + at];
                    }

                    across[j * width + i] = sum / total;
                }
            }

            var blurred = new double[field.Length];
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var sum = 0.0;
                    for (var k = -reach; k <= reach; k++)
                    {
                        var at = j + k;
                        if (at < 0 || at >= height)
                        {
                            continue;
                        }

                        sum += weights[k + reach] * across[at * width + i];
                    }

                    blurred[j * width + i] = sum / total;
                }
            }

            return blurred;
        }

        /// <summary>
        /// <paramref name="field"/> moved <paramref name="dx"/> pixels right and
        /// <paramref name="dy"/> down, nothing coming in from beyond the canvas,
        /// or, when <paramref name="wrapX"/>, the tile's other end across.
        /// </summary>
        private static double[] Shifted(double[] field, int width, int height, int dx, int dy, bool wrapX)
        {
            var moved = new double[field.Length];
            for (var j = 0; j < height; j++)
            {
                var from = j - dy;
                if (from < 0 || from >= height)
                {
                    continue;
                }

                for (var i = 0; i < width; i++)
                {
                    var at = i - dx;
                    if (wrapX)
                    {
                        at = ((at % width) + width) % width;
                    }
                    else if (at < 0 || at >= width)
                    {
                        continue;
                    }

                    moved[j * width + i] = field[from * width + at];
                }
            }

            return moved;
        }

        /// <summary>The script's <c>grow()</c>: the largest of each <paramref name="size"/> by <paramref name="size"/> square, a max filter.</summary>
        private static double[] Grown(double[] field, int width, int height, int size)
        {
            var reach = size / 2;
            var grown = new double[field.Length];
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var most = 0.0;
                    for (var dj = -reach; dj <= reach; dj++)
                    {
                        for (var di = -reach; di <= reach; di++)
                        {
                            var x = i + di;
                            var y = j + dj;
                            if (x >= 0 && x < width && y >= 0 && y < height)
                            {
                                most = Math.Max(most, field[y * width + x]);
                            }
                        }
                    }

                    grown[j * width + i] = most;
                }
            }

            return grown;
        }

        /// <summary>
        /// The script's <c>shadow()</c> on <paramref name="raster"/>:
        /// <paramref name="mask"/> blurred and moved by <paramref name="shadow"/>
        /// darkens what is there by its strength — kept out of
        /// <paramref name="keepOut"/>'s coverage when given, where something
        /// opaque drawn under this sprite would hide it in the script.
        /// </summary>
        private static void CastShadow(Raster raster, double[] mask, SoftShadow shadow, bool wrapX, double[] keepOut = null)
        {
            var offset = shadow.OffsetPixels;
            var blurred = Blurred(mask, raster.Width, raster.Height, shadow.BlurPixels, wrapX);
            var moved = Shifted(blurred, raster.Width, raster.Height, (int)Math.Round(offset.x), -(int)Math.Round(offset.y), wrapX);
            double strength = shadow.Strength;
            raster.Darken(keepOut == null
                ? Map(moved, m => strength * m)
                : Map2(moved, keepOut, (m, k) => strength * m * (1.0 - k)));
        }

        /// <summary>
        /// The script's <c>light()</c> of <paramref name="height"/>: the
        /// normal from the field's gradient — central differences inside, one
        /// sided at the canvas's edges, or taken round a tile across or down
        /// when <paramref name="wrapX"/>, <paramref name="wrapY"/> — lit by the
        /// recipe's light, y down, with the body's ambient and diffuse and
        /// <paramref name="finish"/>'s highlight.
        /// </summary>
        private static Lit LightOf(ArtRecipe recipe, Raster raster, double[] height, LitFinish finish, bool wrapX, bool wrapY)
        {
            var width = raster.Width;
            var rows = raster.Height;
            var light = recipe.LightDirection;

            // The recipe's light has y up; the script's frame has y down.
            var lx = (double)light.x;
            var ly = -(double)light.y;
            var lz = (double)light.z;
            var length = Math.Sqrt(lx * lx + ly * ly + lz * lz);
            lx /= length;
            ly /= length;
            lz /= length;
            var halfLength = Math.Sqrt(lx * lx + ly * ly + (lz + 1.0) * (lz + 1.0));
            var hx = lx / halfLength;
            var hy = ly / halfLength;
            var hz = (lz + 1.0) / halfLength;

            double At(int i, int j) => height[j * width + i];

            double Slope(int index, int count, bool wrap, Func<int, double> value)
            {
                if (wrap)
                {
                    return (value((index + 1) % count) - value((index - 1 + count) % count)) / 2.0;
                }

                if (count < 2)
                {
                    return 0.0;
                }

                if (index == 0)
                {
                    return value(1) - value(0);
                }

                if (index == count - 1)
                {
                    return value(count - 1) - value(count - 2);
                }

                return (value(index + 1) - value(index - 1)) / 2.0;
            }

            var shade = new double[height.Length];
            var specular = new double[height.Length];
            for (var j = 0; j < rows; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var rowIndex = j;
                    var columnIndex = i;
                    var gx = Slope(i, width, wrapX, x => At(x, rowIndex));
                    var gy = Slope(j, rows, wrapY, y => At(columnIndex, y));
                    var normal = Math.Sqrt(gx * gx + gy * gy + 1.0);
                    var nx = -gx / normal;
                    var ny = -gy / normal;
                    var nz = 1.0 / normal;
                    var diffuse = Clamp01(nx * lx + ny * ly + nz * lz);
                    var mirror = Clamp01(nx * hx + ny * hy + nz * hz);
                    shade[j * width + i] = recipe.Ambient + recipe.Diffuse * diffuse;
                    specular[j * width + i] = finish.Specular * StablePow(mirror, finish.Shininess);
                }
            }

            return new Lit(shade, specular);
        }

        /// <summary>
        /// The slice border that keeps the stretched middle of every one of
        /// <paramref name="images"/> uniform: out from the middle row or column
        /// while each equals the middle one in all of them. An axis not sliced
        /// gets half the sprite on each side, so it draws at its own size.
        /// </summary>
        private static Vector4 SlicedBorder(IReadOnlyList<ArtImage> images, bool alongX, bool alongY)
        {
            var width = images[0].Width;
            var height = images[0].Height;
            var (left, right) = alongX ? UniformRun(images, width, true) : (width / 2, width - width / 2);
            var (bottom, top) = alongY ? UniformRun(images, height, false) : (height / 2, height - height / 2);
            return new Vector4(left, bottom, right, top);
        }

        /// <summary>
        /// How many columns (<paramref name="isColumns"/>) or rows lie before
        /// and after the run round the middle one that are equal to it in every
        /// image.
        /// </summary>
        private static (int before, int after) UniformRun(IReadOnlyList<ArtImage> images, int count, bool isColumns)
        {
            var middle = count / 2;

            bool IsLikeMiddle(int line)
            {
                foreach (var image in images)
                {
                    var pixels = image.Pixels;
                    var length = isColumns ? image.Height : image.Width;
                    for (var k = 0; k < length; k++)
                    {
                        var a = isColumns ? pixels[k * image.Width + line] : pixels[line * image.Width + k];
                        var b = isColumns ? pixels[k * image.Width + middle] : pixels[middle * image.Width + k];
                        if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            var low = middle;
            while (low > 0 && IsLikeMiddle(low - 1))
            {
                low--;
            }

            var high = middle;
            while (high < count - 1 && IsLikeMiddle(high + 1))
            {
                high++;
            }

            return (low, count - 1 - high);
        }

        private static ArtImage WithBorder(ArtImage image, Vector4 border) =>
            new ArtImage(image.Width, image.Height, image.PixelArray, border);

        /// <summary>A white highlight sprite whose alpha is <paramref name="highlight"/>, rows from the top: what the specular material adds.</summary>
        private static ArtImage HighlightImage(int width, int height, double[] highlight, Vector4 border)
        {
            var pixels = new Color32[width * height];
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    pixels[(height - 1 - j) * width + i] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, ToByte(highlight[j * width + i]));
                }
            }

            return new ArtImage(width, height, pixels, border);
        }

        /// <summary>
        /// Evaluates <paramref name="shade"/> at every pixel centre — <c>(x + ½,
        /// y + ½)</c>, y up, bottom row first, as the grey pieces are painted —
        /// and stores its colour divided by <paramref name="scale"/>, sRGB
        /// encoded, for the lit material: a quarter piece painted in colour.
        /// </summary>
        private static ArtImage PaintColor(int width, int height, double scale, Func<double, double, Premultiplied> shade)
        {
            var pixels = new Premultiplied[width * height];
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    pixels[j * width + i] = shade(i + 0.5, j + 0.5);
                }
            }

            return Encode(width, height, pixels, scale, Vector4.zero);
        }

        /// <summary>
        /// <paramref name="pixels"/>, bottom row first, as a sprite: each
        /// pixel's colour, un-premultiplied and divided by
        /// <paramref name="scale"/>, sRGB encoded, with its alpha. A pixel with
        /// no alpha takes the colour of a neighbour that has some, so filtering
        /// never pulls a dark fringe into an edge.
        /// </summary>
        private static ArtImage Encode(int width, int height, Premultiplied[] pixels, double scale, Vector4 border)
        {
            var colours = new Color32[pixels.Length];
            var alphas = new byte[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                alphas[i] = ToByte(pixels[i].A);
                var straight = pixels[i].Straight;
                colours[i] = new Color32(
                    ToByte(EncodeSrgb(straight.R / scale)), ToByte(EncodeSrgb(straight.G / scale)), ToByte(EncodeSrgb(straight.B / scale)),
                    alphas[i]);
            }

            // The order a transparent pixel looks for a neighbour's colour in.
            var neighbours = new[] { (-1, 0), (1, 0), (0, -1), (0, 1) };
            var filled = new Color32[colours.Length];
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var index = j * width + i;
                    filled[index] = colours[index];
                    if (alphas[index] > 0)
                    {
                        continue;
                    }

                    foreach (var (di, dj) in neighbours)
                    {
                        var x = i + di;
                        var y = j + dj;
                        if (x >= 0 && x < width && y >= 0 && y < height && alphas[y * width + x] > 0)
                        {
                            var from = colours[y * width + x];
                            filled[index] = new Color32(from.r, from.g, from.b, 0);
                            break;
                        }
                    }
                }
            }

            return new ArtImage(width, height, filled, border);
        }

        /// <summary>A part's shade and its highlight, pixel by pixel.</summary>
        private readonly struct Lit
        {
            public Lit(double[] shade, double[] specular)
            {
                Shade = shade;
                Specular = specular;
            }

            public double[] Shade { get; }

            public double[] Specular { get; }
        }

        /// <summary>A colour premultiplied by its alpha, in linear light: one pixel of a composite.</summary>
        private readonly struct Premultiplied
        {
            private Premultiplied(double r, double g, double b, double a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public static Premultiplied Clear => new Premultiplied(0.0, 0.0, 0.0, 0.0);

            public double R { get; }
            public double G { get; }
            public double B { get; }
            public double A { get; }

            /// <summary>The colour un-premultiplied; black where nothing covers.</summary>
            public Linear Straight => A > 0.0 ? new Linear(R / A, G / A, B / A) : Linear.Black;

            /// <summary><paramref name="colour"/> laid over this at <paramref name="alpha"/>, clamped to [0, 1].</summary>
            public Premultiplied Over(Linear colour, double alpha)
            {
                var a = Clamp01(alpha);
                return new Premultiplied(
                    R * (1.0 - a) + colour.R * a, G * (1.0 - a) + colour.G * a, B * (1.0 - a) + colour.B * a, A * (1.0 - a) + a);
            }
        }

        /// <summary>
        /// A transparent canvas in the script's frame — pixel centres at
        /// <c>i + ½</c>, rows from the top — holding a premultiplied composite
        /// and, apart, the white highlight a split part leaves for the specular
        /// material, which what is laid over it later covers.
        /// </summary>
        private sealed class Raster
        {
            private readonly Premultiplied[] pixels;
            private readonly double[] highlight;

            public Raster(int width, int height)
            {
                Width = width;
                Height = height;
                pixels = new Premultiplied[width * height];
                highlight = new double[width * height];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Premultiplied.Clear;
                }
            }

            public int Width { get; }

            public int Height { get; }

            public int Count => pixels.Length;

            /// <summary>The x of pixel <paramref name="index"/>'s centre.</summary>
            public double X(int index) => index % Width + 0.5;

            /// <summary>The y of pixel <paramref name="index"/>'s centre, down from the top.</summary>
            public double Y(int index) => index / Width + 0.5;

            /// <summary>The highlight left at pixel <paramref name="index"/>.</summary>
            public double Highlight(int index) => highlight[index];

            /// <summary>The highlight left at every pixel.</summary>
            public double[] HighlightField() => (double[])highlight.Clone();

            /// <summary><paramref name="colour"/> laid over pixel <paramref name="index"/> at <paramref name="alpha"/>.</summary>
            public void Over(int index, Linear colour, double alpha)
            {
                var a = Clamp01(alpha);
                pixels[index] = pixels[index].Over(colour, a);
                highlight[index] *= 1.0 - a;
            }

            /// <summary><paramref name="colour"/> laid over every pixel at its <paramref name="alpha"/>.</summary>
            public void Over(Linear colour, double[] alpha)
            {
                for (var i = 0; i < pixels.Length; i++)
                {
                    Over(i, colour, alpha[i]);
                }
            }

            /// <summary>Black laid over every pixel at its <paramref name="amount"/>: the script's darkening multiply.</summary>
            public void Darken(double[] amount) => Over(Linear.Black, amount);

            /// <summary>
            /// The script's <c>paint()</c>: <paramref name="colour"/> times the
            /// shade, plus the highlight, laid over at <paramref name="alpha"/> —
            /// or, when <paramref name="isSplit"/>, the shade alone laid over
            /// and the highlight kept apart for the specular material.
            /// </summary>
            public void OverShaded(Linear colour, Lit lit, double[] alpha, bool isSplit)
            {
                for (var i = 0; i < pixels.Length; i++)
                {
                    var a = Clamp01(alpha[i]);
                    if (isSplit)
                    {
                        Over(i, colour * lit.Shade[i], a);
                        highlight[i] += lit.Specular[i] * a;
                    }
                    else
                    {
                        Over(i, colour * lit.Shade[i] + Linear.White * lit.Specular[i], a);
                    }
                }
            }

            /// <summary>The composite for the lit material, divided by <paramref name="headroom"/>, bottom row first.</summary>
            public ArtImage ToLit(double headroom, Vector4 border) => Encode(Width, Height, BottomFirst(0, 0, Width, Height), headroom, border);

            /// <summary>The composite as it is, for the plain sprite material, bottom row first.</summary>
            public ArtImage ToPlain(Vector4 border) => Encode(Width, Height, BottomFirst(0, 0, Width, Height), 1.0, border);

            /// <summary>The rectangle from column <paramref name="x"/> and row <paramref name="y"/> down from the top, for the lit material.</summary>
            public ArtImage CropLit(double headroom, int x, int y, int width, int height) =>
                Encode(width, height, BottomFirst(x, y, width, height), headroom, Vector4.zero);

            private Premultiplied[] BottomFirst(int x, int y, int width, int height)
            {
                var flipped = new Premultiplied[width * height];
                for (var j = 0; j < height; j++)
                {
                    for (var i = 0; i < width; i++)
                    {
                        flipped[(height - 1 - j) * width + i] = pixels[(y + j) * Width + x + i];
                    }
                }

                return flipped;
            }
        }
    }
}
