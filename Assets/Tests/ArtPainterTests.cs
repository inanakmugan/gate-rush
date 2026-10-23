using System.Collections.Generic;
using System.Linq;
using GateRush.Editor;
using NUnit.Framework;
using UnityEditor;
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
    /// puts them. Module 46: the contour pieces are white silhouettes meeting
    /// at the same seams, the face sits inside them by the outline's width
    /// and is white past its bevel, the shine is a band that meets at top
    /// seams and is clear at side seams, the studs are a grid of Studs Per
    /// Cell Side discs a side, each with a darker foot and its own highlight,
    /// and the gate arrow's outline covers the arrow inside its sprite.
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

        // The default highlight's size and offset as shares of the default
        // stud (6 and 7 of 32 pixels), so a smaller stud keeps its highlight inside.
        private const float GlossShareOfStud = 6f / 32f;
        private const float GlossOffsetShareOfStud = 7f / 32f;

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

            Assert.AreEqual(35, checkedSprites, "every generated sprite is checked");
        }

        [Test]
        public void Paint_SingleStudAndItsGloss_AreTheStudGridAtOnePerSide()
        {
            var single = ArtPainter.Paint(recipe, ArtSprite.SingleStud);
            var singleGloss = ArtPainter.Paint(recipe, ArtSprite.SingleStudGloss);
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("studsPerCellSide").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CollectionAssert.AreEqual(ArtPainter.Paint(recipe, ArtSprite.Studs).Pixels, single.Pixels, "the stud");
            CollectionAssert.AreEqual(ArtPainter.Paint(recipe, ArtSprite.StudGloss).Pixels, singleGloss.Pixels, "its gloss");
        }

        [TestCase(ArtSprite.QuarterOuter, ArtSprite.ContourOuter)]
        [TestCase(ArtSprite.QuarterEdge, ArtSprite.ContourEdge)]
        [TestCase(ArtSprite.QuarterConcave, ArtSprite.ContourConcave)]
        public void Paint_ContourPiece_IsSizedLikeItsFacePieceAndMeetsStraightOutlinesOnTheSameSeams(ArtSprite face, ArtSprite contour)
        {
            var faceImage = ArtPainter.Paint(recipe, face);
            var contourImage = ArtPainter.Paint(recipe, contour);

            Assert.AreEqual(faceImage.Width, contourImage.Width);
            Assert.AreEqual(faceImage.Height, contourImage.Height);

            // Where a face piece meets the inside of the shape, its contour
            // does too; where it shows the straight profile, its contour shows
            // the contour edge's.
            var profile = Column(ArtPainter.Paint(recipe, ArtSprite.ContourEdge), 0);
            switch (contour)
            {
                case ArtSprite.ContourOuter:
                    AssertSameProfile(profile, Column(contourImage, 0), "left column");
                    AssertSameProfile(profile, Row(contourImage, 0), "bottom row");
                    break;
                case ArtSprite.ContourEdge:
                    AssertSameProfile(profile, Column(contourImage, contourImage.Width - 1), "right column");
                    Assert.IsTrue(Row(contourImage, 0).All(p => p.a == 255), "the bottom row meets the inside of the shape");
                    break;
                default:
                    Assert.IsTrue(Column(contourImage, 0).All(p => p.a == 255), "left column");
                    Assert.IsTrue(Row(contourImage, 0).All(p => p.a == 255), "bottom row");
                    AssertSameProfile(profile, Column(contourImage, contourImage.Width - 1), "right column");
                    AssertSameProfile(profile, Row(contourImage, contourImage.Height - 1), "top row");
                    break;
            }
        }

        [Test]
        public void Paint_ContourPieces_AreWhite()
        {
            foreach (var sprite in new[] { ArtSprite.ContourOuter, ArtSprite.ContourEdge, ArtSprite.ContourConcave })
            {
                var image = ArtPainter.Paint(recipe, sprite);

                Assert.IsTrue(image.Pixels.All(p => p.r == 255 && p.g == 255 && p.b == 255), sprite.ToString());
            }
        }

        [Test]
        public void Paint_FaceEdge_IsInsetFromItsContourByTheOutlineWidth()
        {
            // Along the edge piece's column, the outline's outer edge is where
            // the contour is half covered, and the face's edge where the face is.
            var contourEdge = HalfCoveredHeight(Column(ArtPainter.Paint(recipe, ArtSprite.ContourEdge), 0));
            var faceEdge = HalfCoveredHeight(Column(ArtPainter.Paint(recipe, ArtSprite.QuarterEdge), 0));

            Assert.AreEqual(recipe.PieceOutlinePixels, contourEdge - faceEdge, 1.0);
        }

        [Test]
        public void Paint_FaceDeeperThanItsBevel_IsWhiteSoTheTintShowsExactly()
        {
            var fill = ArtPainter.Paint(recipe, ArtSprite.QuarterFill);
            var edge = ArtPainter.Paint(recipe, ArtSprite.QuarterEdge);

            Assert.IsTrue(fill.Pixels.All(p => p.r == 255 && p.a == 255), "the fill");
            Assert.IsTrue(Row(edge, 0).All(p => p.r == 255 && p.a == 255), "the edge's row toward the cell's centre");
        }

        [Test]
        public void Paint_FaceAtItsEdge_IsDarkerThanItsMiddle()
        {
            var column = Column(ArtPainter.Paint(recipe, ArtSprite.QuarterEdge), 0);
            var lastOpaque = column.FindLastIndex(p => p.a == 255);

            Assert.Less(column[lastOpaque].r, column[0].r);
        }

        [Test]
        public void Paint_FaceEdgeBand_RampsSmoothlyFromItsToneToWhite()
        {
            // Down the edge piece's column from just inside the face's edge
            // toward the cell's centre, row by row.
            var column = Column(ArtPainter.Paint(recipe, ArtSprite.QuarterEdge), 0);
            var edgeRow = column.FindLastIndex(p => p.a == 255);
            var bandRange = 255 - recipe.FaceEdgeBandTone * 255f;
            var whiteBy = edgeRow - (int)System.Math.Ceiling(recipe.FaceEdgeBandPixels) - 1;

            for (var row = edgeRow; row > 0; row--)
            {
                var step = column[row - 1].r - column[row].r;
                Assert.That(step, Is.GreaterThanOrEqualTo(0), $"row {row - 1} is no darker than the row outside it");
                Assert.That(step, Is.LessThan(bandRange / 2f), $"no step between rows {row} and {row - 1}");
            }

            Assert.Less(column[edgeRow].r, 255, "darker at the edge");
            Assert.AreEqual(255, column[whiteBy].r, "white past the band");
        }

        [Test]
        public void Paint_ShineEdge_IsABandAlongTheEdgeClearTowardTheCellsCentre()
        {
            var shine = ArtPainter.Paint(recipe, ArtSprite.ShineEdge);
            var column = Column(shine, 0);
            var lastShown = column.FindLastIndex(p => p.a > 0);

            Assert.IsTrue(Row(shine, 0).All(p => p.a == 0), "the row toward the cell's centre is clear");
            Assert.Greater(lastShown, 0, "something is lit");
            Assert.AreEqual(255, column[lastShown - (int)recipe.ShineWidthPixels / 2].a, "the band is at full strength just inside the edge");
            Assert.IsTrue(shine.Pixels.All(p => p.r == 255), "white");
        }

        [Test]
        public void Paint_ShinePieces_MeetTheEdgesBandAtTopSeamsAndNothingAtSideSeams()
        {
            var profile = Column(ArtPainter.Paint(recipe, ArtSprite.ShineEdge), 0);
            var outer = ArtPainter.Paint(recipe, ArtSprite.ShineOuter);
            var concave = ArtPainter.Paint(recipe, ArtSprite.ShineConcave);

            AssertSameProfile(profile, Column(outer, 0), "outer corner, left column, beside a top edge");
            Assert.IsTrue(Row(outer, 0).All(p => p.a == 0), "outer corner, bottom row, beside a side");
            AssertSameProfile(profile, Column(concave, concave.Width - 1), "concave corner, right column, beside a top edge");
            Assert.IsTrue(Row(concave, concave.Height - 1).All(p => p.a == 0), "concave corner, top row, beside a side");
            Assert.IsTrue(Column(concave, 0).All(p => p.a == 0), "concave corner, left column, inside the shape");
            Assert.IsTrue(Row(concave, 0).All(p => p.a == 0), "concave corner, bottom row, inside the shape");
        }

        [Test]
        public void Paint_ShineOuter_FadesRoundTheCornerFromTopToSide()
        {
            // Along the corner's diagonal the edge faces half up: lit, but
            // less than straight under the top edge.
            var outer = ArtPainter.Paint(recipe, ArtSprite.ShineOuter);
            var reachBeforeRounding = recipe.QuarterPixels - recipe.BlockGapPixels / 2.0 - recipe.CornerRadiusPixels;
            var depth = recipe.PieceOutlinePixels + recipe.ShineWidthPixels / 2.0;
            var underTop = Pixel(outer, 0.5, recipe.QuarterPixels - recipe.BlockGapPixels / 2.0 - depth);
            var radial = recipe.CornerRadiusPixels - depth;
            var onDiagonal = Pixel(
                outer, reachBeforeRounding + radial / System.Math.Sqrt(2.0), reachBeforeRounding + radial / System.Math.Sqrt(2.0));

            Assert.AreEqual(255, underTop.a, "under the top edge");
            Assert.Greater(onDiagonal.a, 0, "round the corner");
            Assert.Less(onDiagonal.a, underTop.a, "fading toward the side");
        }

        [TestCase(1, 32f)]
        [TestCase(2, 32f)]
        [TestCase(3, 20f)]
        public void Paint_Studs_PaintsAGridOfCylindersPerCellSide(int perSide, float diameter)
        {
            SetStudGrid(perSide, diameter);
            var studs = ArtPainter.Paint(recipe, ArtSprite.Studs);
            var radius = recipe.StudDiameterPixels / 2.0;
            var raise = recipe.StudDiscRaisePixels;
            var drop = recipe.StudFootDropPixels;
            var centers = StudCenters(perSide);

            foreach (var cy in centers)
            {
                foreach (var cx in centers)
                {
                    // The disc sits its raise above the grid point; the
                    // crescent hangs below it, the rim runs along its top.
                    var discY = cy + raise;
                    var middle = Pixel(studs, cx, discY);
                    var crescent = Pixel(studs, cx, discY - radius - drop / 2.0);
                    var rim = Pixel(studs, cx, discY + radius - recipe.StudRingWidthPixels / 2.0 - 0.5);
                    var lowerEdge = Pixel(studs, cx, discY - radius + recipe.StudRingWidthPixels / 2.0 + 0.5);

                    Assert.AreEqual(255, middle.a, $"the disc at ({cx}, {cy})");
                    Assert.AreEqual(255, crescent.a, $"the crescent below ({cx}, {cy})");
                    Assert.Less(crescent.r, middle.r, $"the crescent below ({cx}, {cy}) is darker");
                    Assert.Less(rim.r, middle.r, $"the rim at the top of ({cx}, {cy}) is darker");
                    Assert.AreEqual(middle.r, lowerEdge.r, $"the rim has faded out by the bottom of ({cx}, {cy})");
                }
            }

            // Between two neighbours the face shows; so it does in the cell's
            // corners and above the top row, where no stud is.
            for (var i = 0; i + 1 < centers.Count; i++)
            {
                var between = (centers[i] + centers[i + 1]) / 2.0;
                Assert.AreEqual(0, Pixel(studs, between, centers[0]).a, $"between studs along x at {between}");
                Assert.AreEqual(0, Pixel(studs, centers[0], between).a, $"between studs along y at {between}");
            }

            Assert.AreEqual(0, Pixel(studs, 0.5, 0.5).a, "the cell's corner");
            Assert.AreEqual(0, Pixel(studs, centers[0], centers[centers.Count - 1] + raise + radius + 1.0).a, "above the top row");
            Assert.AreEqual(perSide * perSide, CountStudCentres(studs), "one disc with its crescent per stud");
        }

        [TestCase(1, 32f)]
        [TestCase(2, 32f)]
        [TestCase(3, 20f)]
        public void Paint_StudGloss_IsAHighlightAndATopLightPerStud(int perSide, float diameter)
        {
            SetStudGrid(perSide, diameter);
            var gloss = ArtPainter.Paint(recipe, ArtSprite.StudGloss);
            var offset = recipe.GlossOffsetPixels;
            var radius = recipe.StudDiameterPixels / 2.0;
            var centers = StudCenters(perSide);

            foreach (var cy in centers)
            {
                foreach (var cx in centers)
                {
                    var discY = cy + recipe.StudDiscRaisePixels;
                    Assert.Greater(Pixel(gloss, cx + offset.x, discY + offset.y).a, 0, $"the highlight on ({cx}, {cy})");
                    Assert.Greater(Pixel(gloss, cx - offset.x, discY + radius / 2.0).a, 0, $"the top light on ({cx}, {cy})");
                    Assert.AreEqual(0, Pixel(gloss, cx - offset.x, discY - offset.y).a, $"nothing low on ({cx}, {cy})");
                }
            }
        }

        [Test]
        public void Paint_GateArrowOutline_CoversTheArrowAndReachesPastIt()
        {
            var arrow = ArtPainter.Paint(recipe, ArtSprite.GateArrow);
            var outline = ArtPainter.Paint(recipe, ArtSprite.GateArrowOutline);

            Assert.AreEqual(arrow.Width, outline.Width);
            Assert.AreEqual(arrow.Height, outline.Height);
            for (var i = 0; i < arrow.Pixels.Count; i++)
            {
                Assert.GreaterOrEqual(outline.Pixels[i].a, arrow.Pixels[i].a, $"pixel {i}");
            }

            Assert.IsTrue(
                Enumerable.Range(0, arrow.Pixels.Count).Any(i => outline.Pixels[i].a == 255 && arrow.Pixels[i].a == 0),
                "the outline shows beyond the arrow");
            Assert.IsTrue(Row(outline, 0).All(p => p.a == 0) && Row(outline, outline.Height - 1).All(p => p.a == 0), "inside the sprite");
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

        /// <summary>
        /// Sets the recipe's stud grid, with a highlight scaled to the stud so
        /// it stays inside it, and checks the recipe still has no problems.
        /// </summary>
        private void SetStudGrid(int perSide, float diameter)
        {
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("studsPerCellSide").intValue = perSide;
            serialized.FindProperty("studDiameterPixels").floatValue = diameter;
            serialized.FindProperty("glossDiameterPixels").floatValue = diameter * GlossShareOfStud;
            serialized.FindProperty("glossOffsetPixels").vector2Value = new Vector2(-diameter, diameter) * GlossOffsetShareOfStud;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsEmpty(recipe.Problems(), "the grid is a valid recipe");
        }

        /// <summary>
        /// The centres of the studs along one axis of the cell sprite: the
        /// cell divided evenly, (i + ½) of a share each, so neighbouring cells
        /// continue the grid.
        /// </summary>
        private List<double> StudCenters(int perSide)
        {
            var spacing = recipe.CellPixels / (double)perSide;
            var center = recipe.CellPixels / 2.0;
            var first = center - (perSide - 1) * spacing / 2.0;
            return Enumerable.Range(0, perSide).Select(i => first + i * spacing).ToList();
        }

        /// <summary>The number of separate opaque shapes in <paramref name="image"/>, each pixel joined to its four neighbours.</summary>
        private static int CountStudCentres(ArtImage image)
        {
            var seen = new bool[image.Pixels.Count];
            var shapes = 0;
            for (var start = 0; start < seen.Length; start++)
            {
                if (seen[start] || image.Pixels[start].a != 255)
                {
                    continue;
                }

                shapes++;
                var open = new Stack<int>();
                open.Push(start);
                seen[start] = true;
                while (open.Count > 0)
                {
                    var index = open.Pop();
                    var x = index % image.Width;
                    var y = index / image.Width;
                    foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        var next = ny * image.Width + nx;
                        if (nx >= 0 && nx < image.Width && ny >= 0 && ny < image.Height && !seen[next] && image.Pixels[next].a == 255)
                        {
                            seen[next] = true;
                            open.Push(next);
                        }
                    }
                }
            }

            return shapes;
        }

        /// <summary>The first pixel, from the bottom of <paramref name="column"/>, that is less than half covered: where its shape's edge lies.</summary>
        private static int HalfCoveredHeight(List<Color32> column) => column.FindIndex(p => p.a < 128);

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
