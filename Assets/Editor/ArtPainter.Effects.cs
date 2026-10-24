using System;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// The board's debris of Module 47, pass 1, ported from the approved
    /// reference script <c>effects_reference.py</c> in the lit model of D80:
    /// the cube a cleared block breaks into, the glassy ice shards, the gate
    /// glow and the sparkle.
    /// </summary>
    /// <remarks>
    /// <para><b>Painted large, then scaled.</b> The script paints the cube and
    /// the shards on a larger canvas and scales them down with Pillow's
    /// LANCZOS filter; so does this (<see cref="Downsampled"/>), in the
    /// script's frame (<c>Raster</c>, y down) and with its lighting
    /// (<c>LightOf</c>). The glow and the sparkle are painted at their own
    /// size.</para>
    /// <para><b>Tinted or baked.</b> The cube is a block's, so it is split as
    /// a block face is: its grey shade for the lit material, tinted the
    /// block's colour, and its white highlight apart for the specular
    /// material (<see cref="ArtSprite.CubeHighlight"/>). A shard's colour
    /// never varies, so it is baked in, highlight and all, from
    /// <see cref="ArtPalette"/>'s shard colour, and drawn plain. The glow and
    /// the sparkle are white.</para>
    /// </remarks>
    public static partial class ArtPainter
    {
        // Pillow's LANCZOS filter: a sinc windowed by a sinc three times as
        // wide, reaching three source pixels each side at full scale. The
        // filter's definition, not a look parameter.
        private const double LanczosLobes = 3.0;

        // A sparkle arm's width never reaches zero at its tip, so the Gaussian
        // across it stays defined; the script's own guard, far below a pixel.
        private const double SparkleArmMinimumWidth = 1e-4;

        /// <summary>Which outline of <see cref="ArtRecipe.ShardOutlines"/> a shard sprite is painted from.</summary>
        private static int ShardIndexOf(ArtSprite sprite)
        {
            switch (sprite)
            {
                case ArtSprite.Shard:
                    return 0;
                case ArtSprite.Shard2:
                    return 1;
                case ArtSprite.Shard3:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sprite), sprite, "Not a shard sprite.");
            }
        }

        /// <summary>
        /// The script's <c>cube_sprite()</c>, split for the board: a rounded
        /// square inset from a quarter-block canvas, rising as a quarter circle
        /// to the cube's height, lit as a block's body is, with one of the
        /// block's studs at its centre, then scaled to the cube's size. The
        /// face layer is the grey shade, stored divided by the headroom for the
        /// lit material; the highlight layer is the white the specular material
        /// adds — the body's highlight, and the stud's rim light, added rather
        /// than blended so the cube stays two layers.
        /// </summary>
        private static ArtImage PaintCube(ArtRecipe recipe, LitLayer layer)
        {
            var size = recipe.CubePaintPixels;
            var centre = size / 2.0;
            var half = centre - recipe.CubeInsetPixels;
            var raster = new Raster(size, size);

            var distance = Field(raster, (x, y) => Rr(x, y, centre, centre, half, half, recipe.CubeCornerRadiusPixels));
            var mask = Map(distance, d => Coverage(recipe, d));
            var height = Map(distance, d => d < 0.0 ? Bevel(d, recipe.CubeBevelPixels, recipe.CubeHeightPixels) : 0.0);
            var lit = LightOf(recipe, raster, height, new LitFinish(recipe.Shininess, recipe.SpecularStrength), false, false);
            raster.OverShaded(Linear.White, lit, mask, isSplit: true);
            PaintCubeStud(recipe, raster, centre);

            var painted = layer == LitLayer.Face
                ? raster.ToLit(recipe.ShadeHeadroom, Vector4.zero)
                : HighlightImage(size, size, raster.HighlightField(), Vector4.zero);
            return Downsampled(painted, recipe.CubePixels);
        }

        /// <summary>
        /// One of the block's studs, at the block's own scale, at the centre of
        /// the cube's canvas: the stud model of <see cref="PaintStuds"/> and
        /// <see cref="PaintGloss"/>, worked out y up as they are and laid on
        /// the script-frame raster — its contact shadow, its wall, its top in
        /// grey for the tint, and its rim light added to the highlight.
        /// </summary>
        private static void PaintCubeStud(ArtRecipe recipe, Raster raster, double centre)
        {
            var radius = recipe.StudDiameterPixels / 2.0;
            double edge = recipe.StudEdgePixels;
            var contactOffset = recipe.StudContactOffsetPixels;
            var wallOffset = recipe.StudWallOffsetPixels;
            for (var i = 0; i < raster.Count; i++)
            {
                var x = raster.X(i);
                var y = raster.Height - raster.Y(i);
                var contactDistance = Length(x - centre - contactOffset.x, y - centre - contactOffset.y);
                var contact = 1.0 - SmoothStep(radius + recipe.StudContactInnerPixels, radius + recipe.StudContactOuterPixels, contactDistance);
                var wall = 1.0 - SmoothStep(radius - edge, radius + edge, Length(x - centre - wallOffset.x, y - centre - wallOffset.y));
                var (top, rim, toward) = StudTop(recipe, x, y, centre, centre);
                var topTone = recipe.StudTopTone * (1.0 - recipe.StudRimDarken * rim * Clamp01(-toward));

                raster.Over(i, Linear.Black, recipe.StudContactAlpha * contact);
                raster.Over(i, Linear.Grey(recipe.StudWallTone), wall);
                raster.Over(i, Linear.Grey(topTone), top);
                raster.AddHighlight(
                    i, recipe.StudRimLightAlpha * rim * StablePow(Clamp01(toward), recipe.StudRimLightExponent) * top);
            }
        }

        /// <summary>
        /// The script's <c>shard_sprite()</c> for outline <paramref name="index"/>:
        /// a convex polygon of glassy ice, its edge rising as a quarter circle
        /// and a ridge along its first-to-third diagonal, lit brighter and
        /// flatter than a block, in the palette's shard colour; then a bright
        /// rim just inside the edge and a frost streak across the second
        /// corner, both lifting the colour toward white; partly transparent,
        /// most opaque on the rim, the streak and the highlight. Baked in
        /// colour and clipped to white, stored as it shows, for the plain
        /// sprite material: scaled down in the same bytes the script scales,
        /// so it matches the reference where a headroom-divided store would
        /// not.
        /// </summary>
        private static ArtImage PaintShard(ArtRecipe recipe, ArtPalette palette, int index)
        {
            var size = recipe.ShardPaintPixels;
            var raster = new Raster(size, size);
            var corners = recipe.ShardOutlines[index].Points;

            // The outline is y up; the raster is the script's frame, y down.
            var xs = new double[corners.Count];
            var ys = new double[corners.Count];
            for (var c = 0; c < corners.Count; c++)
            {
                xs[c] = corners[c].x * (double)size;
                ys[c] = (1.0 - corners[c].y) * size;
            }

            var distance = Field(raster, (x, y) => PolygonSd(xs, ys, x, y));

            // Either winding: the script turns the distance round when no
            // pixel lies inside, which a polygon's own centre settles.
            var centreX = 0.0;
            var centreY = 0.0;
            for (var c = 0; c < xs.Length; c++)
            {
                centreX += xs[c] / xs.Length;
                centreY += ys[c] / ys.Length;
            }

            if (PolygonSd(xs, ys, centreX, centreY) > 0.0)
            {
                distance = Map(distance, d => -d);
            }

            var mask = Map(distance, d => Coverage(recipe, d));

            // The ridge along the diagonal from the first corner to the third.
            var ex = xs[2] - xs[0];
            var ey = ys[2] - ys[0];
            var diagonal = Length(ex, ey);
            var height = new double[raster.Count];
            for (var i = 0; i < raster.Count; i++)
            {
                var d = distance[i];
                if (!(d < 0.0))
                {
                    continue;
                }

                var across = ((raster.X(i) - xs[0]) * -ey + (raster.Y(i) - ys[0]) * ex) / diagonal;
                var ridge = Clamp01(1.0 - Math.Abs(across) / recipe.ShardRidgeHalfWidthPixels);
                height[i] = Bevel(d, recipe.ShardBevelPixels, recipe.ShardHeightPixels)
                            + ridge * recipe.ShardRidgeHeightPixels * SmoothStep(0.0, recipe.ShardRidgeRisePixels, -d);
            }

            var lit = LightOf(recipe, raster, height, recipe.ShardFinish, recipe.ShardAmbient, recipe.ShardDiffuse, false, false);

            // The streak's direction is y up; the raster's y runs down.
            var streakX = (double)recipe.ShardStreakAcross.x;
            var streakY = -(double)recipe.ShardStreakAcross.y;
            double rimStrength = recipe.ShardRimStrength;
            double streakStrength = recipe.ShardStreakStrength;
            double baseAlpha = recipe.ShardBaseAlpha;
            for (var i = 0; i < raster.Count; i++)
            {
                var d = distance[i];
                var m = mask[i];
                var colour = palette.Shard * lit.Shade[i] + Linear.White * lit.Specular[i];

                var rim = Clamp01(1.0 - Math.Abs(d + recipe.ShardRimOffsetPixels) / recipe.ShardRimHalfWidthPixels) * m;
                colour = colour * (1.0 - rimStrength * rim) + Linear.White * (rimStrength * rim);

                var t = ((raster.X(i) - xs[1]) * streakX + (raster.Y(i) - ys[1]) * streakY) / recipe.ShardStreakWidthPixels;
                var streak = StableExp(-t * t) * m * SmoothStep(0.0, recipe.ShardStreakRisePixels, -d);
                colour = colour * (1.0 - streakStrength * streak) + Linear.White * (streakStrength * streak);

                var lift = Clamp01(rim + streak + lit.Specular[i] * recipe.ShardHighlightOpacity);

                // Clipped to white, as the script clips before it encodes and
                // scales: the shard is stored as it shows, for the plain material.
                raster.Over(i, new Linear(Clamp01(colour.R), Clamp01(colour.G), Clamp01(colour.B)), m * (baseAlpha + (1.0 - baseAlpha) * lift));
            }

            return Downsampled(raster.ToPlain(Vector4.zero), recipe.ShardPixels);
        }

        /// <summary>
        /// The signed distance from <c>(x, y)</c> to the convex polygon of
        /// <paramref name="xs"/>, <paramref name="ys"/>, as the script measures
        /// it: the largest distance past any edge's line, with each edge's
        /// normal turned clockwise from it — negative inside for one winding,
        /// positive for the other.
        /// </summary>
        private static double PolygonSd(double[] xs, double[] ys, double x, double y)
        {
            var sd = double.NegativeInfinity;
            for (var c = 0; c < xs.Length; c++)
            {
                var next = (c + 1) % xs.Length;
                var ex = xs[next] - xs[c];
                var ey = ys[next] - ys[c];
                sd = Math.Max(sd, ((x - xs[c]) * ey - (y - ys[c]) * ex) / Length(ex, ey));
            }

            return sd;
        }

        /// <summary>
        /// The script's <c>glow_sprite()</c>: white, its alpha a bump across the
        /// gate — nothing at both ends, full in the middle — times a falloff
        /// into the board that halves every
        /// <see cref="ArtRecipe.GateGlowHalfDepth"/> of the sprite and eases to
        /// nothing at the far edge. The gate is its bottom edge, which the
        /// board turns to face out through the gate.
        /// </summary>
        private static ArtImage PaintGateGlow(ArtRecipe recipe)
        {
            double size = recipe.GateGlowPixels;
            double halfDepth = recipe.GateGlowHalfDepth;
            double fadeFrom = recipe.GateGlowFadeFrom;
            var ln2 = Math.Log(2.0);
            return Paint(recipe.GateGlowPixels, recipe.GateGlowPixels, Vector4.zero, (x, y) =>
            {
                // The script's 0.5 − 0.5·cos(2πu), as sin²(πu).
                var sine = StableSin(Math.PI * (x / size));
                var across = sine * sine;
                var v = y / size;
                var depth = StableExp(-v / halfDepth * ln2) * SmoothStep(1.0, fadeFrom, v);
                return (1.0, across * depth);
            });
        }

        /// <summary>
        /// The script's <c>sparkle_sprite()</c>: white, its alpha a thin
        /// four-point star whose arms taper to nothing at the sprite's edge,
        /// plus a soft round halo and a small bright core. Measured in half
        /// sides from the centre.
        /// </summary>
        private static ArtImage PaintSparkle(ArtRecipe recipe)
        {
            var half = recipe.SparklePixels / 2.0;
            double armWidth = recipe.SparkleArmWidth;
            double taper = recipe.SparkleArmTaper;
            double sigma = recipe.SparkleHaloSigma;
            double core = recipe.SparkleCoreRadius;

            double Arm(double along, double across)
            {
                var left = Clamp01(1.0 - Math.Abs(along));
                var width = armWidth * StablePow(left, taper) + SparkleArmMinimumWidth;
                var q = across / width;
                return StableExp(-q * q) * left;
            }

            return Paint(recipe.SparklePixels, recipe.SparklePixels, Vector4.zero, (x, y) =>
            {
                var u = (x - half) / half;
                var v = (y - half) / half;
                var r = Length(u, v);
                var star = Math.Max(Arm(u, v), Arm(v, u));
                var halo = recipe.SparkleHaloStrength * StableExp(-(r / sigma) * (r / sigma) / 2.0);
                var glint = StableExp(-(r / core) * (r / core));
                return (1.0, Clamp01(star + halo + glint));
            });
        }

        /// <summary>
        /// <paramref name="image"/> scaled to <paramref name="size"/> by
        /// <paramref name="size"/>, as Pillow's <c>resize(…, LANCZOS)</c> scales
        /// the script's sprites: the colours premultiplied by alpha in bytes,
        /// filtered across, then down, each pass rounded to bytes, and divided
        /// back. Pixels left clear take a neighbour's colour, as every
        /// generated sprite's do. The image itself when it is that size.
        /// </summary>
        private static ArtImage Downsampled(ArtImage image, int size)
        {
            if (image.Width == size && image.Height == size)
            {
                return image;
            }

            var source = image.Pixels;
            var premultiplied = new byte[source.Count * 4];
            for (var i = 0; i < source.Count; i++)
            {
                var p = source[i];
                premultiplied[i * 4] = MultiplyBytes(p.r, p.a);
                premultiplied[i * 4 + 1] = MultiplyBytes(p.g, p.a);
                premultiplied[i * 4 + 2] = MultiplyBytes(p.b, p.a);
                premultiplied[i * 4 + 3] = p.a;
            }

            var across = Resampled(premultiplied, image.Width, image.Height, size, true);
            var down = Resampled(across, size, image.Height, size, false);

            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                var a = down[i * 4 + 3];
                pixels[i] = a == 0
                    ? new Color32(0, 0, 0, 0)
                    : new Color32(DivideBytes(down[i * 4], a), DivideBytes(down[i * 4 + 1], a), DivideBytes(down[i * 4 + 2], a), a);
            }

            return new ArtImage(size, size, FilledClear(pixels, size, size), Vector4.zero);
        }

        /// <summary>
        /// One LANCZOS pass over four-channel bytes, <paramref name="width"/> by
        /// <paramref name="height"/>: across to <paramref name="size"/> columns
        /// when <paramref name="isAcross"/>, else down to <paramref name="size"/>
        /// rows. Each output pixel weighs the source pixels within the filter's
        /// reach, widened by the scale, the weights normalised to one.
        /// </summary>
        private static byte[] Resampled(byte[] source, int width, int height, int size, bool isAcross)
        {
            var inCount = isAcross ? width : height;
            var outWidth = isAcross ? size : width;
            var outHeight = isAcross ? height : size;
            var scale = (double)inCount / size;
            var filterScale = Math.Max(scale, 1.0);
            var support = LanczosLobes * filterScale;
            var result = new byte[outWidth * outHeight * 4];

            for (var o = 0; o < size; o++)
            {
                var centre = (o + 0.5) * scale;
                var first = Math.Max(0, (int)(centre - support + 0.5));
                var last = Math.Min(inCount, (int)(centre + support + 0.5));
                var weights = new double[last - first];
                var total = 0.0;
                for (var k = 0; k < weights.Length; k++)
                {
                    weights[k] = Lanczos((k + first - centre + 0.5) / filterScale);
                    total += weights[k];
                }

                for (var line = 0; line < (isAcross ? height : width); line++)
                {
                    for (var channel = 0; channel < 4; channel++)
                    {
                        var sum = 0.0;
                        for (var k = 0; k < weights.Length; k++)
                        {
                            var s = first + k;
                            var index = isAcross ? line * width + s : s * width + line;
                            sum += source[index * 4 + channel] * weights[k];
                        }

                        var outIndex = isAcross ? line * outWidth + o : o * outWidth + line;
                        result[outIndex * 4 + channel] = (byte)Clamp(Math.Floor(sum / total + 0.5), 0.0, byte.MaxValue);
                    }
                }
            }

            return result;
        }

        /// <summary>Pillow's LANCZOS weight at <paramref name="x"/> source pixels: sinc(x) · sinc(x / 3) inside three, nothing outside.</summary>
        private static double Lanczos(double x)
        {
            if (!(x >= -LanczosLobes && x < LanczosLobes))
            {
                return 0.0;
            }

            return Sinc(x) * Sinc(x / LanczosLobes);
        }

        private static double Sinc(double x) => x == 0.0 ? 1.0 : StableSin(Math.PI * x) / (Math.PI * x);

        /// <summary><paramref name="colour"/> times <paramref name="alpha"/> over 255, rounded, as Pillow premultiplies a byte.</summary>
        private static byte MultiplyBytes(byte colour, byte alpha)
        {
            var product = colour * alpha + 128;
            return (byte)(((product >> 8) + product) >> 8);
        }

        /// <summary><paramref name="colour"/> divided by <paramref name="alpha"/> back to a straight byte, as Pillow divides it.</summary>
        private static byte DivideBytes(byte colour, byte alpha) => (byte)Math.Min(byte.MaxValue, byte.MaxValue * colour / alpha);
    }
}
