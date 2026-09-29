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
    /// tile without seams, and keep frost on a block's face.
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

            Assert.AreEqual(20, checkedSprites, "every generated sprite is checked");
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
