using System.Collections.Generic;
using System.Linq;
using GateRush.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ArtPainter"/>: the same recipe always paints the same
    /// bytes (D46), and the quarter pieces carry the same pixels on both sides
    /// of every seam a tiling can put them at, so a block reads as one piece.
    /// The state sprites (Module 16) stretch only straight profile when sliced,
    /// tile without seams, and keep frost on a block's face. The HUD icons
    /// (Module 17) are white, unsliced, and have their shapes where the recipe
    /// puts them.
    /// </summary>
    /// <remarks>
    /// Seams are checked against one profile: the edge piece's left column,
    /// the cross-section of a straight outline. Pieces that meet a straight
    /// outline at a seam must show that profile there, and pieces that meet
    /// the inside of the shape must be fully opaque. Pixels may differ by one
    /// step of rounding, since two pieces reach the same distance by different
    /// arithmetic.
    /// </remarks>
    public class ArtPainterTests
    {
        private const int RoundingTolerance = 1;

        private ArtRecipe recipe;

        [SetUp]
        public void SetUp()
        {
            recipe = ScriptableObject.CreateInstance<ArtRecipe>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(recipe);
        }

        [Test]
        public void Paint_SameRecipeTwice_GivesIdenticalPixelsAndPngBytes()
        {
            var checkedSprites = 0;

            foreach (var sprite in ArtPainter.All)
            {
                var first = ArtPainter.Paint(recipe, sprite);
                var second = ArtPainter.Paint(recipe, sprite);

                CollectionAssert.AreEqual(first.Pixels, second.Pixels, $"{sprite}: pixels");
                CollectionAssert.AreEqual(ArtPainter.EncodePng(first), ArtPainter.EncodePng(second), $"{sprite}: PNG bytes");
                checkedSprites++;
            }

            Assert.AreEqual(26, checkedSprites, "every generated sprite is checked");
        }

        [Test]
        public void Paint_Fill_IsOpaqueEverywhere()
        {
            var fill = ArtPainter.Paint(recipe, ArtSprite.QuarterFill);

            Assert.IsTrue(fill.Pixels.All(p => p.a == 255));
        }

        [Test]
        public void Paint_Edge_ShowsTheStraightProfileOnBothSeamsAlongItAndIsOpaqueOnTheInsideSeam()
        {
            var edge = ArtPainter.Paint(recipe, ArtSprite.QuarterEdge);
            var profile = Column(edge, 0);

            AssertSameProfile(profile, Column(edge, edge.Width - 1), "right column");
            Assert.IsTrue(Row(edge, 0).All(p => p.a == 255), "the bottom row meets the inside of the shape");
        }

        [Test]
        public void Paint_OuterCorner_MeetsStraightOutlinesOnBothSeams()
        {
            var profile = Column(ArtPainter.Paint(recipe, ArtSprite.QuarterEdge), 0);
            var outer = ArtPainter.Paint(recipe, ArtSprite.QuarterOuter);

            AssertSameProfile(profile, Column(outer, 0), "left column");
            AssertSameProfile(profile, Row(outer, 0), "bottom row");
        }

        [Test]
        public void Paint_ConcaveCorner_IsOpaqueOnItsInsideSeamsAndMeetsStraightOutlinesOnTheOthers()
        {
            var profile = Column(ArtPainter.Paint(recipe, ArtSprite.QuarterEdge), 0);
            var concave = ArtPainter.Paint(recipe, ArtSprite.QuarterConcave);

            Assert.IsTrue(Column(concave, 0).All(p => p.a == 255), "left column");
            Assert.IsTrue(Row(concave, 0).All(p => p.a == 255), "bottom row");
            AssertSameProfile(profile, Column(concave, concave.Width - 1), "right column");
            AssertSameProfile(profile, Row(concave, concave.Height - 1), "top row");
        }

        [Test]
        public void Paint_OuterCorner_IsTransparentAtTheCellCornerAndOpaqueAtTheCellCentre()
        {
            var outer = ArtPainter.Paint(recipe, ArtSprite.QuarterOuter);

            Assert.AreEqual(0, outer.Pixels[outer.Pixels.Count - 1].a, "the cell's corner is outside the rounded outline");
            Assert.AreEqual(255, outer.Pixels[0].a, "the cell's centre is face");
        }

        [Test]
        public void Paint_AxisArrow_IsNineSlicedAroundItsHeads()
        {
            var arrow = ArtPainter.Paint(recipe, ArtSprite.AxisArrow);

            Assert.Greater(arrow.Border.x, 0f);
            Assert.AreEqual(arrow.Border.x, arrow.Border.z);
            var middle = Column(arrow, arrow.Width / 2);
            for (var i = (int)arrow.Border.x; i < arrow.Width - (int)arrow.Border.z; i++)
            {
                CollectionAssert.AreEqual(middle, Column(arrow, i), $"column {i} of the stretched middle holds only the shaft");
            }
        }

        [TestCase(ArtSprite.RoundedRect)]
        [TestCase(ArtSprite.Ring)]
        public void Paint_SlicedBox_HasOnlyStraightProfileInItsStretchedMiddle(ArtSprite sprite)
        {
            var box = ArtPainter.Paint(recipe, sprite);

            Assert.Greater(box.Border.x, 0f);
            Assert.AreEqual(new Vector4(box.Border.x, box.Border.x, box.Border.x, box.Border.x), box.Border, "the same border on every side");
            var border = (int)box.Border.x;
            var middleColumn = Column(box, box.Width / 2);
            var middleRow = Row(box, box.Height / 2);
            for (var i = border; i < box.Width - border; i++)
            {
                CollectionAssert.AreEqual(middleColumn, Column(box, i), $"column {i}");
            }

            for (var j = border; j < box.Height - border; j++)
            {
                CollectionAssert.AreEqual(middleRow, Row(box, j), $"row {j}");
            }
        }

        [Test]
        public void Paint_Ring_IsOpaqueOnItsOutlineAndClearInItsMiddle()
        {
            var ring = ArtPainter.Paint(recipe, ArtSprite.Ring);
            var middle = Row(ring, ring.Height / 2);

            Assert.AreEqual(255, middle[recipe.RingWidthPixels / 2].a, "on the outline");
            Assert.AreEqual(0, middle[ring.Width / 2].a, "inside the outline");
        }

        [TestCase(ArtSprite.Chain)]
        [TestCase(ArtSprite.DoorPanel)]
        public void Paint_TiledAlongX_MatchesItselfAcrossTheSeam(ArtSprite sprite)
        {
            // Symmetric about the tile's ends: the last column of one tile and
            // the first of the next are mirror images, so they are equal.
            var tile = ArtPainter.Paint(recipe, sprite);

            CollectionAssert.AreEqual(Column(tile, 0), Column(tile, tile.Width - 1));
        }

        [Test]
        public void Paint_ShutterSlats_MeetAtAGrooveOnTheTopAndBottomEdges()
        {
            var slats = ArtPainter.Paint(recipe, ArtSprite.ShutterSlats);
            var groove = (byte)System.Math.Round(recipe.SlatGapTone * 255.0, System.MidpointRounding.AwayFromZero);

            Assert.IsTrue(Row(slats, 0).All(p => p.r == groove && p.a == 255), "bottom row");
            Assert.IsTrue(Row(slats, slats.Height - 1).All(p => p.r == groove && p.a == 255), "top row");
        }

        [Test]
        public void Paint_Frost_IsTransparentWithinItsInsetOfEveryEdge()
        {
            // The inset keeps frost on a block's rounded face; one anti-aliased
            // pixel inside it may still show the clip's soft edge.
            var frost = ArtPainter.Paint(recipe, ArtSprite.Frost);
            var clear = recipe.FrostInsetPixels - recipe.AntiAliasPixels;
            var checkedPixels = 0;

            for (var j = 0; j < frost.Height; j++)
            {
                for (var i = 0; i < frost.Width; i++)
                {
                    var toEdge = System.Math.Min(System.Math.Min(i + 0.5, frost.Width - i - 0.5), System.Math.Min(j + 0.5, frost.Height - j - 0.5));
                    if (toEdge < clear)
                    {
                        Assert.AreEqual(0, frost.Pixels[j * frost.Width + i].a, $"pixel ({i}, {j})");
                        checkedPixels++;
                    }
                }
            }

            Assert.Greater(checkedPixels, 0, "the inset holds pixels to check");
            Assert.IsTrue(frost.Pixels.Any(p => p.a > 0), "and the frost is drawn somewhere");
        }

        [Test]
        public void Paint_KeyGem_SitsInsideTheKeysHole()
        {
            var body = ArtPainter.Paint(recipe, ArtSprite.KeyBody);
            var gem = ArtPainter.Paint(recipe, ArtSprite.KeyGem);
            var center = recipe.KeyHeightPixels / 2;

            Assert.AreEqual(body.Width, gem.Width);
            Assert.AreEqual(body.Height, gem.Height);
            Assert.AreEqual(0, body.Pixels[center * body.Width + center].a, "the bow's hole");
            Assert.AreEqual(255, gem.Pixels[center * gem.Width + center].a, "filled by the gem");
        }

        [TestCase(ArtSprite.Clock)]
        [TestCase(ArtSprite.Restart)]
        [TestCase(ArtSprite.Sparkle)]
        public void Paint_Icon_IsWhiteAndUnsliced(ArtSprite sprite)
        {
            var icon = ArtPainter.Paint(recipe, sprite);

            Assert.AreEqual(Vector4.zero, icon.Border);
            Assert.IsTrue(icon.Pixels.All(p => p.r == 255 && p.g == 255 && p.b == 255), "tinted where placed, so painted white");
            Assert.IsTrue(icon.Pixels.Any(p => p.a == 255), "and drawn somewhere");
        }

        [Test]
        public void Paint_Clock_HasHandsAtTheCentreARimAndAClearFace()
        {
            var clock = ArtPainter.Paint(recipe, ArtSprite.Clock);
            var size = clock.Width;
            var middleRow = Row(clock, size / 2);

            // Lower left, halfway to the rim: away from both hands, which
            // point up and right.
            var face = size / 2 - size / 5;

            Assert.AreEqual(255, clock.Pixels[size / 2 * size + size / 2].a, "the hands meet at the centre");
            Assert.AreEqual(0, clock.Pixels[face * size + face].a, "the face between the hands");
            Assert.AreEqual(0, middleRow[0].a, "outside the rim");
            Assert.IsTrue(middleRow.Take(size / 4).Any(p => p.a == 255), "the rim, on the left of the middle row");
        }

        [Test]
        public void Paint_Restart_HasAnArcWithAGapAndAHeadAtItsEnd()
        {
            var restart = ArtPainter.Paint(recipe, ArtSprite.Restart);
            var size = restart.Width;
            var center = size / 2.0;
            var radius = recipe.RestartRadius * size;

            // On the ring's middle line in the cut-away upper-right quarter,
            // low enough and far enough right to miss the head.
            var gap = Pixel(restart, center + 0.92 * radius, center + 0.38 * radius);

            // On the ring's middle line at 6 o'clock.
            var bottom = Pixel(restart, center, center - radius);

            // Just past the arc's end at 12 o'clock, inside the head.
            var head = Pixel(restart, center + recipe.RestartHeadLength * size / 3.0, center + radius);

            Assert.AreEqual(0, gap.a, "the gap");
            Assert.AreEqual(255, bottom.a, "the arc");
            Assert.AreEqual(255, head.a, "the head");
        }

        [Test]
        public void Paint_Cube_IsOpaqueAtTheCentreAndClearAtTheCorners()
        {
            var cube = ArtPainter.Paint(recipe, ArtSprite.Cube);
            var last = cube.Width - 1;

            Assert.AreEqual(Vector4.zero, cube.Border);
            Assert.AreEqual(255, Pixel(cube, cube.Width / 2.0, cube.Height / 2.0).a, "the centre");
            Assert.AreEqual(0, cube.Pixels[0].a, "bottom-left corner");
            Assert.AreEqual(0, cube.Pixels[last].a, "bottom-right corner");
            Assert.AreEqual(0, cube.Pixels[last * cube.Width].a, "top-left corner");
            Assert.AreEqual(0, cube.Pixels[last * cube.Width + last].a, "top-right corner");
        }

        [Test]
        public void Paint_Shard_IsClearOutsideItsPointsAndOpaqueInside()
        {
            var shard = ArtPainter.Paint(recipe, ArtSprite.Shard);
            var points = recipe.ShardPoints;
            var size = recipe.ShardPixels;
            var centreX = 0.0;
            var centreY = 0.0;
            for (var i = 0; i < points.Count; i++)
            {
                centreX += points[i].x * size / points.Count;
                centreY += points[i].y * size / points.Count;
            }

            // The default shard reaches none of the sprite's four corners.
            var last = shard.Width - 1;

            Assert.AreEqual(255, Pixel(shard, centreX, centreY).a, "the middle of its corners");
            Assert.AreEqual(0, shard.Pixels[0].a, "bottom-left corner");
            Assert.AreEqual(0, shard.Pixels[last].a, "bottom-right corner");
            Assert.AreEqual(0, shard.Pixels[last * shard.Width].a, "top-left corner");
            Assert.AreEqual(0, shard.Pixels[last * shard.Width + last].a, "top-right corner");
        }

        [Test]
        public void Paint_GateGlow_IsOpaqueAtTheGateAndClearAtTheFarEnd()
        {
            var glow = ArtPainter.Paint(recipe, ArtSprite.GateGlow);
            var column = Column(glow, 0);

            Assert.AreEqual(Vector4.zero, glow.Border);
            Assert.IsTrue(glow.Pixels.All(p => p.r == 255 && p.g == 255 && p.b == 255), "tinted where placed, so painted white");
            Assert.AreEqual(255, column[0].a, "full strength at the gate");
            Assert.AreEqual(0, column[column.Count - 1].a, "nothing at the far end");
            for (var y = 1; y < column.Count; y++)
            {
                Assert.LessOrEqual(column[y].a, column[y - 1].a, $"row {y}: fades away from the gate, never back");
            }
        }

        [Test]
        public void Paint_Sparkle_HasTipsOnTheAxesAndIsClearOnTheDiagonals()
        {
            var sparkle = ArtPainter.Paint(recipe, ArtSprite.Sparkle);
            var center = sparkle.Width / 2.0;

            // Most of the way to a tip along each axis, and as far out along
            // each diagonal, where a four-point star has drawn in its sides.
            var reach = 0.7 * center;
            var diagonal = reach / System.Math.Sqrt(2.0);

            Assert.AreEqual(255, Pixel(sparkle, center, center).a, "the centre");
            Assert.AreEqual(255, Pixel(sparkle, center + reach, center).a, "toward the right tip");
            Assert.AreEqual(255, Pixel(sparkle, center - reach, center).a, "toward the left tip");
            Assert.AreEqual(255, Pixel(sparkle, center, center + reach).a, "toward the top tip");
            Assert.AreEqual(255, Pixel(sparkle, center, center - reach).a, "toward the bottom tip");
            Assert.AreEqual(0, Pixel(sparkle, center + diagonal, center + diagonal).a, "between the top and right tips");
            Assert.AreEqual(0, Pixel(sparkle, center - diagonal, center + diagonal).a, "between the top and left tips");
            Assert.AreEqual(0, Pixel(sparkle, center + diagonal, center - diagonal).a, "between the bottom and right tips");
            Assert.AreEqual(0, Pixel(sparkle, center - diagonal, center - diagonal).a, "between the bottom and left tips");
        }

        private static Color32 Pixel(ArtImage image, double x, double y) =>
            image.Pixels[(int)System.Math.Floor(y) * image.Width + (int)System.Math.Floor(x)];

        private static List<Color32> Column(ArtImage image, int x) =>
            Enumerable.Range(0, image.Height).Select(y => image.Pixels[y * image.Width + x]).ToList();

        private static List<Color32> Row(ArtImage image, int y) =>
            Enumerable.Range(0, image.Width).Select(x => image.Pixels[y * image.Width + x]).ToList();

        private static void AssertSameProfile(IReadOnlyList<Color32> expected, IReadOnlyList<Color32> actual, string label)
        {
            Assert.AreEqual(expected.Count, actual.Count, label);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(expected[i].r - actual[i].r), RoundingTolerance, $"{label}, pixel {i}: tone");
                Assert.LessOrEqual(Mathf.Abs(expected[i].a - actual[i].a), RoundingTolerance, $"{label}, pixel {i}: alpha");
            }
        }
    }
}
