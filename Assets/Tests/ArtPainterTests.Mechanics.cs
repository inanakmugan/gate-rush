using System;
using System.Linq;
using GateRush.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers the mechanics of Module 46's second pass in <see cref="ArtPainter"/>
    /// (<c>ArtPainter.Mechanics.cs</c>), against the reference script
    /// <c>mechanics_reference.py</c>: the parts painted in colour need a
    /// palette; every 9-sliced part is uniform in its stretched middle and
    /// draws at its own size across; every tiled part runs on across its
    /// seam; and the badge, the ice, the frost, the padlock, the key, the coin
    /// and the hazard strips are what the script's formulas give at sample
    /// points. The ice's quarter set is checked at its seams with the other
    /// placed sets (<see cref="Paint_PlacedSet_MeetsItselfAtEverySeam"/>).
    /// </summary>
    public partial class ArtPainterTests
    {
        [Test]
        public void Paint_PaletteSpriteWithoutAPalette_Throws()
        {
            var painted = ArtPainter.All.Where(ArtPainter.NeedsPalette).ToList();

            foreach (var sprite in painted)
            {
                Assert.Throws<ArgumentNullException>(() => ArtPainter.Paint(recipe, sprite), sprite.ToString());
            }

            Assert.GreaterOrEqual(painted.Count, 13, "the ice set at least");
        }

        [TestCase(ArtSprite.BadgeRim, true, false)]
        [TestCase(ArtSprite.BadgeFace, true, false)]
        [TestCase(ArtSprite.BadgeHighlight, true, false)]
        [TestCase(ArtSprite.OneWayArrow, true, false)]
        [TestCase(ArtSprite.OneWayArrowHighlight, true, false)]
        [TestCase(ArtSprite.OneWayArrowVertical, false, true)]
        [TestCase(ArtSprite.OneWayArrowVerticalHighlight, false, true)]
        [TestCase(ArtSprite.ShutterRim, true, true)]
        [TestCase(ArtSprite.MachineBody, true, true)]
        [TestCase(ArtSprite.MachineScreen, true, false)]
        [TestCase(ArtSprite.ElevatorRim, true, true)]
        [TestCase(ArtSprite.ElevatorDoor, true, true)]
        public void Paint_SlicedMechanic_IsUniformInItsStretchedMiddle(ArtSprite sprite, bool alongX, bool alongY)
        {
            var image = ArtPainter.Paint(recipe, palette, sprite);
            var border = image.Border;

            if (alongX)
            {
                var from = (int)border.x;
                var to = image.Width - (int)border.z;
                Assert.Greater(border.x, 0f, "a left border");
                Assert.Greater(border.z, 0f, "a right border");
                Assert.Less(from, to, "a middle to stretch across");
                for (var i = from; i < to; i++)
                {
                    CollectionAssert.AreEqual(Column(image, from), Column(image, i), $"column {i}");
                }
            }

            if (alongY)
            {
                var from = (int)border.y;
                var to = image.Height - (int)border.w;
                Assert.Greater(border.y, 0f, "a bottom border");
                Assert.Greater(border.w, 0f, "a top border");
                Assert.Less(from, to, "a middle to stretch down");
                for (var j = from; j < to; j++)
                {
                    CollectionAssert.AreEqual(Row(image, from), Row(image, j), $"row {j}");
                }
            }
        }

        [TestCase(ArtSprite.BadgeRim, false)]
        [TestCase(ArtSprite.OneWayArrow, false)]
        [TestCase(ArtSprite.OneWayArrowVertical, true)]
        public void Paint_SlicedMechanic_DrawsAtItsOwnSizeAcross(ArtSprite sprite, bool isAcrossX)
        {
            var image = ArtPainter.Paint(recipe, palette, sprite);
            var border = image.Border;

            if (isAcrossX)
            {
                Assert.AreEqual(image.Width, border.x + border.z, "its borders hold its whole width");
            }
            else
            {
                Assert.AreEqual(image.Height, border.y + border.w, "its borders hold its whole height");
            }
        }

        [Test]
        public void Paint_BadgeSet_SharesOneCanvasAndOneBorder()
        {
            var rim = ArtPainter.Paint(recipe, palette, ArtSprite.BadgeRim);
            var face = ArtPainter.Paint(recipe, palette, ArtSprite.BadgeFace);
            var highlight = ArtPainter.Paint(recipe, palette, ArtSprite.BadgeHighlight);

            foreach (var other in new[] { face, highlight })
            {
                Assert.AreEqual((rim.Width, rim.Height), (other.Width, other.Height), "the same canvas");
                Assert.AreEqual(rim.Border, other.Border, "the same border");
            }
        }

        [Test]
        public void Paint_OneWayArrow_UprightIsTheSameSizeTurned()
        {
            var across = ArtPainter.Paint(recipe, palette, ArtSprite.OneWayArrow);
            var upright = ArtPainter.Paint(recipe, palette, ArtSprite.OneWayArrowVertical);

            Assert.AreEqual((across.Width, across.Height), (upright.Height, upright.Width));
        }

        // The shutter's slats are not here: their tile ends on the groove
        // between two slats, the sharpest step of the pattern by design, so
        // their seam is checked against the script's formula instead
        // (Paint_ShutterCylinders_RowsEitherSideOfTheGrooveAreTheScriptsSlatTakenRoundTheTile).
        [TestCase(ArtSprite.Chain, true)]
        [TestCase(ArtSprite.ElevatorHazardLeft, false)]
        [TestCase(ArtSprite.ElevatorHazardRight, false)]
        public void Paint_TiledMechanic_RunsOnAcrossItsSeam(ArtSprite sprite, bool alongX)
        {
            // Where two tiles meet, the step from one tile's last line to the
            // next one's first is no sharper than the sharpest step inside a
            // tile: the pattern was painted round the tile.
            var tile = ArtPainter.Paint(recipe, palette, sprite);
            var count = alongX ? tile.Width : tile.Height;
            System.Collections.Generic.List<Color32> Line(int k) => alongX ? Column(tile, k) : Row(tile, k);

            var inside = 0;
            for (var k = 0; k + 1 < count; k++)
            {
                inside = Math.Max(inside, Step(Line(k), Line(k + 1)));
            }

            Assert.Greater(count, 1);
            Assert.LessOrEqual(Step(Line(count - 1), Line(0)), inside);
        }

        [Test]
        public void Paint_ShutterCylinders_RowsEitherSideOfTheGrooveAreTheScriptsSlatTakenRoundTheTile()
        {
            // The tile is one slat period; its first and last rows meet at the
            // groove. Each is the script's slat — a half-sine profile, lit, then
            // darkened toward the gap — with its slope taken from the row on
            // the far side of the groove, as the next tile lies there.
            var tile = ArtPainter.Paint(recipe, palette, ArtSprite.ShutterCylinders);
            var pitch = recipe.ShutterCylinderPitchPixels;
            var colour = Decode(config.ShutterCylinderColor);
            var finish = recipe.ShutterCylinderFinish;
            var (light, half) = LightAndHalf();

            double Profile(int row) => Math.Sin(Math.PI * (((row % pitch) + pitch) % pitch + 0.5) / pitch);
            double Height(int row) => Profile(row) * recipe.ShutterCylinderHeightPixels;
            double Darkening(int row) => recipe.ShutterGapDarken * Math.Max(0.0, Math.Min(1.0, 1.0 - Profile(row) * recipe.ShutterGapSharpness));

            (double r, double g, double b) Expected(int row)
            {
                var slope = (Height(row + 1) - Height(row - 1)) / 2.0;
                var n = Normalize(0.0, -slope, 1.0);
                var diffuse = Math.Max(0.0, n.y * light.y + n.z * light.z);
                var mirror = Math.Max(0.0, n.y * half.y + n.z * half.z);
                var shade = recipe.Ambient + recipe.Diffuse * diffuse;
                var specular = finish.Specular * Math.Pow(mirror, finish.Shininess);
                var keep = 1.0 - Darkening(row);
                return ((colour.r * shade + specular) * keep, (colour.g * shade + specular) * keep, (colour.b * shade + specular) * keep);
            }

            Assert.AreEqual(pitch, tile.Height, "the tile is one slat period");
            Assert.AreEqual(Height(0), Height(pitch - 1), 1e-9, "the profile is the same height at both ends of the period");
            Assert.AreEqual(Darkening(0), Darkening(pitch - 1), 1e-9, "and the gap darkens both ends alike");

            foreach (var row in new[] { 0, pitch - 1 })
            {
                AssertColour((Expected(row), 1.0), TopDown(tile, 0, row), $"row {row}, beside the groove");
            }
        }

        [Test]
        public void Paint_Badge_IsTheScriptsRimFaceHighlightAndShadowAtSamplePoints()
        {
            var rim = ArtPainter.Paint(recipe, palette, ArtSprite.BadgeRim);
            var face = ArtPainter.Paint(recipe, palette, ArtSprite.BadgeFace);
            var highlight = ArtPainter.Paint(recipe, palette, ArtSprite.BadgeHighlight);
            var cx = rim.Width / 2;
            var cy = rim.Height / 2;

            // The middle: the face's flat top over the rim's.
            var flat = FlatShade();
            Assert.AreEqual(255, TopDown(face, cx, cy).a, "the face covers the middle");
            Assert.AreEqual(Srgb(flat / recipe.ShadeHeadroom), TopDown(face, cx, cy).r, RoundingTolerance, "the face's flat shade");
            Assert.AreEqual(Srgb(flat / recipe.ShadeHeadroom), TopDown(rim, cx, cy).r, RoundingTolerance, "the rim's flat shade under it");
            Assert.AreEqual(FlatSpecular(recipe.BadgeFaceFinish) * 255.0, TopDown(highlight, cx, cy).a, RoundingTolerance, "the face's highlight");

            // Two pixels below the badge, in its middle column: its mask
            // blurred and moved down and right, darkening by its strength.
            var shadow = recipe.BadgeShadow;
            var bottom = cy + (int)(recipe.BadgeSizePixels.y / 2f);
            var row = bottom + 2;
            var from = row - (int)-shadow.OffsetPixels.y;
            var expected = shadow.Strength * GaussianShareUpTo(bottom - 1 - from, shadow.BlurPixels) * 255.0;
            var pixel = TopDown(rim, cx, row);
            Assert.AreEqual(0, pixel.r, "black");
            Assert.AreEqual(expected, pixel.a, RoundingTolerance, "the shadow's strength there");
        }

        [Test]
        public void Paint_Ice_IsTheScriptsTintHighlightAndRimAtSamplePoints()
        {
            var tint = Decode(config.IceTintColor);
            var rimColour = Decode(config.IceRimColor);
            var finish = recipe.IceFinish;

            // Deep in the fill: the thin tint lit flat, and the highlight.
            var flat = Ice(tint, rimColour, FlatShade(), FlatSpecular(finish), 0.0);
            AssertColour(flat, Pixel(ArtPainter.Paint(recipe, palette, ArtSprite.IceFill), 10.5, 10.5), "the fill");

            // On an edge's bevel, on the bright rim's line: an edge sprite's
            // edge is its top, at the cell's less the ice's inset.
            var edge = recipe.QuarterPixels - recipe.IceInsetPixels;
            const double y = 56.5;
            var sd = y - edge;
            var (shade, specular) = IceLight(-sd);
            var rim = recipe.IceRimAlpha * Math.Max(0.0, 1.0 - Math.Abs(sd + recipe.IceRimDepthPixels) / recipe.IceRimWidthPixels);
            Assert.Greater(rim, 0.0, "the sample lies on the rim");
            AssertColour(Ice(tint, rimColour, shade, specular, rim), Pixel(ArtPainter.Paint(recipe, palette, ArtSprite.IceEdgeTop), 20.5, y), "the rim");
        }

        [Test]
        public void Paint_IceStreaks_AreTheScriptsStreaksFadingFromTheCellsEdges()
        {
            var streaks = ArtPainter.Paint(recipe, palette, ArtSprite.IceStreaks);
            var cell = recipe.CellPixels;

            // On the second streak's line, x + y = its offset, y down.
            const double x = 70.5;
            var y = recipe.IceStreakOffsetsPixels[1] - x;
            var alpha = 0.0;
            var clear = 1.0;
            var left = recipe.IceStreakLeftFadePixels;
            var right = recipe.IceStreakRightFadePixels;
            var top = recipe.IceStreakTopFadePixels;
            var clip = SmoothStep(left.x, left.y, x) * SmoothStep(cell - right.x, cell - right.y, x) * SmoothStep(top.x, top.y, y)
                       * (1.0 - SmoothStep(cell - recipe.IceStreakBottomFadePixels, cell, y));
            for (var k = 0; k < recipe.IceStreakOffsetsPixels.Count; k++)
            {
                var t = (x + y - recipe.IceStreakOffsetsPixels[k]) / (recipe.IceStreakWidthPixels + k * recipe.IceStreakWidthStepPixels);
                clear *= 1.0 - recipe.IceStreakAlpha * Math.Exp(-t * t) * clip;
            }

            alpha = 1.0 - clear;
            var pixel = TopDown(streaks, (int)x, (int)y);
            Assert.AreEqual(255, pixel.r, "white");
            Assert.AreEqual(alpha * 255.0, pixel.a, RoundingTolerance, "the streaks' strength there");
            Assert.AreEqual(0, TopDown(streaks, 2, 2).a, "nothing in the cell's corner");
        }

        [Test]
        public void Paint_Lock_IsAGoldBodyWithADarkKeyholeUnderASilverShackle()
        {
            var image = ArtPainter.Paint(recipe, palette, ArtSprite.Lock);
            var bx = image.Width / 2;
            var by = image.Height / 2 + (int)recipe.LockDropPixels;

            var body = TopDown(image, bx + (int)(recipe.LockBodyHalfSizePixels.x / 2f), by + (int)(recipe.LockBodyHalfSizePixels.y / 2f));
            var hole = TopDown(image, bx, by - (int)recipe.KeyholeRisePixels);
            var shackle = TopDown(image, bx, by - (int)(recipe.LockShackleRisePixels + recipe.LockShackleRadiiPixels.y));

            Assert.AreEqual(255, body.a, "the body is opaque");
            Assert.Greater(body.r, body.b, "gold");
            Assert.Less(hole.r + hole.g + hole.b, body.r + body.g + body.b, "the keyhole is darker than the body");
            Assert.AreEqual(255, shackle.a, "the shackle's top");
            Assert.LessOrEqual(Math.Abs(shackle.r - shackle.b), 40, "silver");
        }

        [Test]
        public void Paint_GoldKeyAndGem_ShareACanvasWithTheGemInItsSeat()
        {
            var key = ArtPainter.Paint(recipe, palette, ArtSprite.GoldKey);
            var gem = ArtPainter.Paint(recipe, palette, ArtSprite.Gem);
            var highlight = ArtPainter.Paint(recipe, palette, ArtSprite.GemHighlight);
            var bowX = key.Width / 2 - (int)recipe.GoldKeyBowOffsetPixels;
            var ky = key.Height / 2;

            Assert.AreEqual((key.Width, key.Height), (gem.Width, gem.Height), "the gem on the key's canvas");
            Assert.AreEqual((key.Width, key.Height), (highlight.Width, highlight.Height), "and its highlight");
            Assert.AreEqual(255, TopDown(gem, bowX, ky).a, "the gem at the bow's centre");
            Assert.AreEqual(0, TopDown(gem, bowX + (int)recipe.GemRadiusPixels + 1, ky).a, "and nowhere past its radius");

            var seat = TopDown(key, bowX + (int)recipe.GemSeatRadiusPixels, ky);
            var bow = TopDown(key, bowX + (int)(recipe.GemSeatRadiusPixels + recipe.GemSeatWidthPixels * 3f), ky);
            Assert.Less(seat.r, bow.r, "the seat darkens the bow round the gem");
        }

        [Test]
        public void Paint_Coin_HasACreamFaceAndDarkHands()
        {
            var coin = ArtPainter.Paint(recipe, palette, ArtSprite.BonusCoin);
            var c = coin.Width / 2;
            var off = (int)(recipe.CoinFaceRadiusPixels / 2f);

            var face = TopDown(coin, c - off, c + off);
            var hand = TopDown(coin, c, c - (int)(recipe.CoinMinuteHandPixels / 2f));

            Assert.AreEqual(255, face.a);
            Assert.Greater(face.r, face.b, "cream");
            Assert.Less(hand.r + hand.g + hand.b, face.r + face.g + face.b, "the hand is dark on the face");
        }

        [TestCase(ArtSprite.ElevatorHazardLeft)]
        [TestCase(ArtSprite.ElevatorHazardRight)]
        public void Paint_ElevatorHazard_HasLightAndDarkStripes(ArtSprite sprite)
        {
            var tile = ArtPainter.Paint(recipe, palette, sprite);
            var light = Decode(config.HazardColor);
            var dark = Decode(config.HazardDarkColor);

            // Lit, each stripe keeps its hue: the light stripes redder than
            // blue, the dark ones bluer than red.
            Assert.IsTrue(tile.Pixels.Any(p => p.r > p.b), $"light stripes ({light})");
            Assert.IsTrue(tile.Pixels.Any(p => p.b > p.r), $"dark stripes ({dark})");
            Assert.IsTrue(tile.Pixels.All(p => p.a == 255), "opaque, as the rim under it");
        }

        // ----- The script's model, on the test's side -----

        /// <summary>The recipe's light, y down as the script's frame has it, and its half vector.</summary>
        private ((double x, double y, double z) light, (double x, double y, double z) half) LightAndHalf()
        {
            var l = Normalize(recipe.LightDirection.x, -recipe.LightDirection.y, recipe.LightDirection.z);
            return (l, Normalize(l.x, l.y, l.z + 1.0));
        }

        /// <summary>The shade of a flat top facing straight out: ambient plus the diffuse share of the light's z.</summary>
        private double FlatShade() => recipe.Ambient + recipe.Diffuse * LightAndHalf().light.z;

        /// <summary>The highlight of a flat top in <paramref name="finish"/>.</summary>
        private double FlatSpecular(LitFinish finish) => finish.Specular * Math.Pow(LightAndHalf().half.z, finish.Shininess);

        /// <summary>
        /// The ice's shade and highlight <paramref name="depth"/> pixels inside
        /// a top edge: its quarter-circle bevel's normal, facing up the board.
        /// The edge's direction does not depend on the light's frame, so the
        /// recipe's own light, y up, lights it.
        /// </summary>
        private (double shade, double specular) IceLight(double depth)
        {
            var t = Math.Min(1.0, Math.Max(0.0, depth / recipe.IceBevelPixels));
            var u = 1.0 - t;
            var k = recipe.IceHeightPixels / recipe.IceBevelPixels;
            var rise = Math.Sqrt(1.0 - u * u);
            var n = Normalize(0.0, k * u, rise);
            var l = Normalize(recipe.LightDirection.x, recipe.LightDirection.y, recipe.LightDirection.z);
            var h = Normalize(l.x, l.y, l.z + 1.0);
            var diffuse = Math.Max(0.0, n.y * l.y + n.z * l.z);
            var mirror = Math.Max(0.0, n.y * h.y + n.z * h.z);
            return (recipe.Ambient + recipe.Diffuse * diffuse, recipe.IceFinish.Specular * Math.Pow(mirror, recipe.IceFinish.Shininess));
        }

        /// <summary>
        /// The ice's composite — its tint lit by <paramref name="shade"/> at
        /// its alpha, the white highlight over it, the bright rim over that —
        /// as the stored colour and alpha of a lit sprite.
        /// </summary>
        private ((double r, double g, double b) colour, double alpha) Ice(
            (double r, double g, double b) tint, (double r, double g, double b) rim, double shade, double specular, double rimAlpha)
        {
            var a = recipe.IceTintAlpha;
            var colour = (r: tint.r * shade * a, g: tint.g * shade * a, b: tint.b * shade * a);
            colour = (colour.r * (1.0 - specular) + specular, colour.g * (1.0 - specular) + specular, colour.b * (1.0 - specular) + specular);
            var alpha = a * (1.0 - specular) + specular;
            colour = (colour.r * (1.0 - rimAlpha) + rim.r * rimAlpha, colour.g * (1.0 - rimAlpha) + rim.g * rimAlpha, colour.b * (1.0 - rimAlpha) + rim.b * rimAlpha);
            alpha = alpha * (1.0 - rimAlpha) + rimAlpha;
            return ((colour.r / alpha, colour.g / alpha, colour.b / alpha), alpha);
        }

        private void AssertColour(((double r, double g, double b) colour, double alpha) expected, Color32 actual, string label)
        {
            double headroom = recipe.ShadeHeadroom;
            Assert.AreEqual(expected.alpha * 255.0, actual.a, RoundingTolerance, $"{label}: alpha");
            Assert.AreEqual(Srgb(expected.colour.r / headroom), actual.r, RoundingTolerance, $"{label}: red");
            Assert.AreEqual(Srgb(expected.colour.g / headroom), actual.g, RoundingTolerance, $"{label}: green");
            Assert.AreEqual(Srgb(expected.colour.b / headroom), actual.b, RoundingTolerance, $"{label}: blue");
        }

        /// <summary>
        /// The share of a Gaussian of standard deviation <paramref name="sigma"/>,
        /// cut off at <see cref="ArtRecipe.BlurReachSigmas"/>, at offsets up to
        /// <paramref name="last"/>: a blurred straight edge, one side of which
        /// is covered.
        /// </summary>
        private static double GaussianShareUpTo(int last, double sigma)
        {
            var reach = (int)Math.Ceiling(ArtRecipe.BlurReachSigmas * sigma);
            var all = 0.0;
            var covered = 0.0;
            for (var k = -reach; k <= reach; k++)
            {
                var w = Math.Exp(-k * k / (2.0 * sigma * sigma));
                all += w;
                if (k <= last)
                {
                    covered += w;
                }
            }

            return covered / all;
        }

        /// <summary>An sRGB colour's channels, rounded to bytes, in linear light.</summary>
        private static (double r, double g, double b) Decode(Color color)
        {
            double Channel(float c)
            {
                var v = Math.Round(c * 255.0) / 255.0;
                return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }

            return (Channel(color.r), Channel(color.g), Channel(color.b));
        }

        /// <summary>Pixel <paramref name="i"/> of row <paramref name="j"/>, counted down from the top as the script counts.</summary>
        private static Color32 TopDown(ArtImage image, int i, int j) => image.Pixels[(image.Height - 1 - j) * image.Width + i];

        /// <summary>The largest difference of any channel between two lines of pixels.</summary>
        private static int Step(System.Collections.Generic.IReadOnlyList<Color32> a, System.Collections.Generic.IReadOnlyList<Color32> b)
        {
            var most = 0;
            for (var k = 0; k < a.Count; k++)
            {
                most = Math.Max(most, Math.Max(
                    Math.Max(Math.Abs(a[k].r - b[k].r), Math.Abs(a[k].g - b[k].g)),
                    Math.Max(Math.Abs(a[k].b - b[k].b), Math.Abs(a[k].a - b[k].a))));
            }

            return most;
        }
    }
}
