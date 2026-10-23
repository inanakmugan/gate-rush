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
    /// and is plain white, the light pieces are white bands along edges that
    /// face the light and black ones along edges that face away, meeting the
    /// edge piece of their place at every seam, faint enough to read matte;
    /// the body pieces meet theirs the same way, with the top band over a
    /// lighter bevel along the top, a darker bevel easing into the middle
    /// along the bottom and the side away from the light, a faint lightening
    /// on the light's side, the reflection line above the lip, and the gloss
    /// streak in the top-left outer corner alone;
    /// the studs are a grid of Studs Per Cell Side a side, Stud Spacing
    /// Pixels apart, each a flat top inside a crisp rim, with a darker
    /// crescent and a soft shadow falling to the bottom right and a faint arc
    /// of light on the rim toward the light, no glossy spot; and the gate
    /// arrow's outline covers the arrow inside its sprite.
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

        // The matte finish's ceiling (Module 46): no white light on a block's
        // edge or a stud's rim stronger than this alpha.
        private const float MatteLightAlphaCeiling = 0.15f;

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

            Assert.AreEqual(56, checkedSprites, "every generated sprite is checked");
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

        [TestCase(ArtSprite.QuarterOuter)]
        [TestCase(ArtSprite.QuarterEdge)]
        [TestCase(ArtSprite.QuarterFill)]
        [TestCase(ArtSprite.QuarterConcave)]
        public void Paint_FacePiece_IsPlainWhiteSoTheTintShowsExactly(ArtSprite sprite)
        {
            var face = ArtPainter.Paint(recipe, sprite);

            Assert.IsTrue(face.Pixels.All(p => p.r == 255 && p.g == 255 && p.b == 255), "white everywhere, its edge too");
            Assert.AreEqual(255, face.Pixels[0].a, "opaque at the cell's centre");
        }

        [TestCase(ArtSprite.LightEdgeTop, true)]
        [TestCase(ArtSprite.LightEdgeLeft, true)]
        [TestCase(ArtSprite.LightEdgeBottom, false)]
        [TestCase(ArtSprite.LightEdgeRight, false)]
        public void Paint_LightEdge_IsABandOfOneKindAlongTheEdgeClearTowardTheCellsCentre(ArtSprite sprite, bool isLit)
        {
            // From the top left, the top and left edges face the light
            // squarely, the bottom and right ones face squarely away.
            var light = ArtPainter.Paint(recipe, sprite);
            var strongest = light.Pixels.Max(p => p.a);
            var strength = (isLit ? recipe.LightAlpha : recipe.ShadeAlpha) * 255f;

            Assert.IsTrue(Row(light, 0).All(p => p.a == 0), "the row toward the cell's centre is clear");
            Assert.IsTrue(light.Pixels.All(p => p.r == (isLit ? 255 : 0)), isLit ? "white" : "black");
            Assert.That(strongest, Is.GreaterThan(strength / 2f).And.LessThanOrEqualTo(strength + RoundingTolerance), "as strong as the band");
            CollectionAssert.AreEqual(Column(light, 0), Column(light, light.Width - 1), "one strength along the edge");
        }

        [TestCase(ArtSprite.LightOuterTopLeft, ArtSprite.LightEdgeTop, ArtSprite.LightEdgeLeft)]
        [TestCase(ArtSprite.LightOuterTopRight, ArtSprite.LightEdgeTop, ArtSprite.LightEdgeRight)]
        [TestCase(ArtSprite.LightOuterBottomLeft, ArtSprite.LightEdgeBottom, ArtSprite.LightEdgeLeft)]
        [TestCase(ArtSprite.LightOuterBottomRight, ArtSprite.LightEdgeBottom, ArtSprite.LightEdgeRight)]
        public void Paint_LightOuter_MeetsTheEdgesOfItsPlaceAtItsSeams(ArtSprite corner, ArtSprite alongX, ArtSprite alongY)
        {
            // Drawn for the top-right quarter: the left column meets the edge
            // along x that continues its top, the bottom row the edge along y
            // that continues its side, both posed at the corner's place.
            var outer = ArtPainter.Paint(recipe, corner);

            AssertSameProfile(Column(ArtPainter.Paint(recipe, alongX), 0), Column(outer, 0), "left column, beside the edge along x");
            AssertSameProfile(Column(ArtPainter.Paint(recipe, alongY), 0), Row(outer, 0), "bottom row, beside the edge along y");
        }

        [TestCase(ArtSprite.LightConcaveTopLeft, ArtSprite.LightEdgeTop, ArtSprite.LightEdgeLeft)]
        [TestCase(ArtSprite.LightConcaveTopRight, ArtSprite.LightEdgeTop, ArtSprite.LightEdgeRight)]
        [TestCase(ArtSprite.LightConcaveBottomLeft, ArtSprite.LightEdgeBottom, ArtSprite.LightEdgeLeft)]
        [TestCase(ArtSprite.LightConcaveBottomRight, ArtSprite.LightEdgeBottom, ArtSprite.LightEdgeRight)]
        public void Paint_LightConcave_MeetsTheEdgesOfItsPlaceAndIsClearInsideTheShape(ArtSprite corner, ArtSprite alongX, ArtSprite alongY)
        {
            // The notch's floor runs on as an edge along x, its side as an
            // edge along y; the other two seams lie inside the shape.
            var concave = ArtPainter.Paint(recipe, corner);

            AssertSameProfile(Column(ArtPainter.Paint(recipe, alongX), 0), Column(concave, concave.Width - 1), "right column, beside the edge along x");
            AssertSameProfile(Column(ArtPainter.Paint(recipe, alongY), 0), Row(concave, concave.Height - 1), "top row, beside the edge along y");
            Assert.IsTrue(Column(concave, 0).All(p => p.a == 0), "left column, inside the shape");
            Assert.IsTrue(Row(concave, 0).All(p => p.a == 0), "bottom row, inside the shape");
        }

        [Test]
        public void Paint_LightOuterTopRight_TurnsFromLitToShadedRoundItsArc()
        {
            // Lit under the top edge, shaded beside the right one, and clear
            // half way round, where the arc faces across the light.
            var outer = ArtPainter.Paint(recipe, ArtSprite.LightOuterTopRight);
            var e = recipe.QuarterPixels - recipe.BlockGapPixels / 2.0;
            var depth = recipe.PieceOutlinePixels + 1.5;
            var arcCentre = e - recipe.CornerRadiusPixels;
            var radial = (recipe.CornerRadiusPixels - depth) / System.Math.Sqrt(2.0);

            var underTop = Pixel(outer, 0.5, e - depth);
            var besideSide = Pixel(outer, e - depth, 0.5);
            var halfWay = Pixel(outer, arcCentre + radial, arcCentre + radial);

            Assert.AreEqual(255, underTop.r, "white under the top");
            Assert.Greater(underTop.a, 0, "lit under the top");
            Assert.AreEqual(0, besideSide.r, "black beside the side");
            Assert.Greater(besideSide.a, 0, "shaded beside the side");
            Assert.LessOrEqual(halfWay.a, RoundingTolerance, "clear where the arc faces across the light");
        }

        [TestCase(ArtSprite.BodyOuterTopLeft, ArtSprite.BodyEdgeTop, ArtSprite.BodyEdgeLeft)]
        [TestCase(ArtSprite.BodyOuterTopRight, ArtSprite.BodyEdgeTop, ArtSprite.BodyEdgeRight)]
        [TestCase(ArtSprite.BodyOuterBottomLeft, ArtSprite.BodyEdgeBottom, ArtSprite.BodyEdgeLeft)]
        [TestCase(ArtSprite.BodyOuterBottomRight, ArtSprite.BodyEdgeBottom, ArtSprite.BodyEdgeRight)]
        public void Paint_BodyOuter_MeetsTheEdgesOfItsPlaceAtItsSeams(ArtSprite corner, ArtSprite alongX, ArtSprite alongY)
        {
            // As a light piece: the left column meets the edge along x that
            // continues its top, the bottom row the edge along y that
            // continues its side. The top-left's streak stays off both.
            var outer = ArtPainter.Paint(recipe, corner);

            AssertSameProfile(Column(ArtPainter.Paint(recipe, alongX), 0), Column(outer, 0), "left column, beside the edge along x");
            AssertSameProfile(Column(ArtPainter.Paint(recipe, alongY), 0), Row(outer, 0), "bottom row, beside the edge along y");
        }

        [TestCase(ArtSprite.BodyConcaveTopLeft, ArtSprite.BodyEdgeTop, ArtSprite.BodyEdgeLeft)]
        [TestCase(ArtSprite.BodyConcaveTopRight, ArtSprite.BodyEdgeTop, ArtSprite.BodyEdgeRight)]
        [TestCase(ArtSprite.BodyConcaveBottomLeft, ArtSprite.BodyEdgeBottom, ArtSprite.BodyEdgeLeft)]
        [TestCase(ArtSprite.BodyConcaveBottomRight, ArtSprite.BodyEdgeBottom, ArtSprite.BodyEdgeRight)]
        public void Paint_BodyConcave_MeetsTheEdgesOfItsPlaceAndIsClearInsideTheShape(ArtSprite corner, ArtSprite alongX, ArtSprite alongY)
        {
            var concave = ArtPainter.Paint(recipe, corner);

            AssertSameProfile(Column(ArtPainter.Paint(recipe, alongX), 0), Column(concave, concave.Width - 1), "right column, beside the edge along x");
            AssertSameProfile(Column(ArtPainter.Paint(recipe, alongY), 0), Row(concave, concave.Height - 1), "top row, beside the edge along y");
            Assert.IsTrue(Column(concave, 0).All(p => p.a == 0), "left column, inside the shape");
            Assert.IsTrue(Row(concave, 0).All(p => p.a == 0), "bottom row, inside the shape");
        }

        [Test]
        public void Paint_BodyEdgeTop_IsTheTopBandOverALighterBevelAndClearInTheMiddle()
        {
            var edge = ArtPainter.Paint(recipe, ArtSprite.BodyEdgeTop);
            var column = Column(edge, 0);
            var faceEdge = FaceEdgeHeight();

            var inBand = column[(int)(faceEdge - 3.0)];
            var middle = column[(int)(faceEdge - recipe.BodyTopBandPixels - recipe.BodyTopBandSoftnessPixels - recipe.BodyBevelPixels)];

            Assert.IsTrue(edge.Pixels.All(p => p.r == 255), "white: the top's bevel lightens, never shades");
            Assert.That(
                inBand.a,
                Is.GreaterThanOrEqualTo(recipe.BodyTopBandAlpha * 255f - RoundingTolerance)
                    .And.LessThanOrEqualTo((recipe.BodyTopBandAlpha + recipe.BodyBevelLightAlpha) * 255f + RoundingTolerance),
                "the band, over the bevel's light");
            Assert.AreEqual(0, middle.a, "the flat middle");
        }

        [Test]
        public void Paint_BodyEdgeBottom_IsADarkerBevelUnderAReflectionLineAtItsEdge()
        {
            var column = Column(ArtPainter.Paint(recipe, ArtSprite.BodyEdgeBottom), 0);
            var faceEdge = FaceEdgeHeight();

            var line = column[(int)(faceEdge - recipe.BodyReflectionPixels / 2.0)];
            var bevel = column[(int)(faceEdge - recipe.BodyReflectionPixels - 4.0)];
            var middle = column[(int)(faceEdge - recipe.BodyBevelPixels - 2.0)];

            Assert.Greater(line.r, 128, "the reflection line reads light, over the bevel");
            Assert.Greater(line.a, 0, "the reflection line shows");
            Assert.AreEqual(0, bevel.r, "the bevel above it is black");
            Assert.Greater(bevel.a, 0, "and shows");
            Assert.AreEqual(0, middle.a, "the flat middle");
        }

        [Test]
        public void Paint_BodySides_TheLightsSideIsAFaintLighteningTheOtherADarkerBevel()
        {
            // From the top left: the left edge only lightens, faintly; the
            // right one only darkens.
            var left = ArtPainter.Paint(recipe, ArtSprite.BodyEdgeLeft);
            var right = ArtPainter.Paint(recipe, ArtSprite.BodyEdgeRight);

            Assert.IsTrue(left.Pixels.All(p => p.r == 255), "the left is white");
            Assert.That(left.Pixels.Max(p => p.a), Is.GreaterThan(0).And.LessThanOrEqualTo(recipe.BodySideLightAlpha * 255f + RoundingTolerance), "faint");
            Assert.IsTrue(right.Pixels.Where(p => p.a > 0).All(p => p.r == 0), "the right is black");
            Assert.That(right.Pixels.Max(p => p.a), Is.GreaterThan(0).And.LessThanOrEqualTo(recipe.BodyBevelShadeAlpha * 255f + RoundingTolerance), "as dark as the bevel");
        }

        [Test]
        public void Paint_BodyBevel_EasesIntoTheMiddleWithNoStep()
        {
            // Down the right edge's band from just inside the face's edge
            // toward the cell's centre, row by row.
            var column = Column(ArtPainter.Paint(recipe, ArtSprite.BodyEdgeRight), 0);
            var edgeRow = column.FindLastIndex(p => p.a > 0);
            var maxStep = recipe.BodyBevelShadeAlpha * 255f * 2f / recipe.BodyBevelPixels;

            for (var row = edgeRow - 1; row > 0; row--)
            {
                var step = column[row].a - column[row - 1].a;
                Assert.That(step, Is.GreaterThanOrEqualTo(0), $"row {row - 1} is no darker than the row outside it");
                Assert.That(step, Is.LessThanOrEqualTo(maxStep), $"no step between rows {row} and {row - 1}");
            }

            Assert.AreEqual(0, column[edgeRow - (int)System.Math.Ceiling(recipe.BodyBevelPixels) - 1].a, "gone by the bevel's width");
        }

        [Test]
        public void Paint_BodyStreak_IsOnlyInTheTopLeftOuterCorner()
        {
            // The streak's middle, in the sprites' own frame.
            var curveStart = recipe.QuarterPixels - recipe.BlockGapPixels / 2.0 - recipe.CornerRadiusPixels;
            var x = curveStart - recipe.BodyStreakGapPixels - recipe.BodyStreakLengthPixels / 2.0;
            var y = FaceEdgeHeight() - recipe.BodyStreakDepthPixels;

            var topLeft = Pixel(ArtPainter.Paint(recipe, ArtSprite.BodyOuterTopLeft), x, y);
            var topRight = Pixel(ArtPainter.Paint(recipe, ArtSprite.BodyOuterTopRight), x, y);

            Assert.AreEqual(255, topLeft.r, "white");
            Assert.GreaterOrEqual(topLeft.a, recipe.BodyStreakAlpha * 255f - RoundingTolerance, "the streak, at its strength or more");
            Assert.Less(topRight.a, recipe.BodyStreakAlpha * 255f, "no streak on the top-right corner, only the band");
        }

        [TestCase(1, 35f, 59f)]
        [TestCase(2, 35f, 59f)]
        [TestCase(3, 14f, 40f)]
        public void Paint_Studs_PaintsAGridOfFlatToppedCylindersPerCellSide(int perSide, float diameter, float spacing)
        {
            SetStudGrid(perSide, diameter, spacing);
            var studs = ArtPainter.Paint(recipe, ArtSprite.Studs);
            var radius = recipe.StudDiameterPixels / 2.0;
            var raise = recipe.StudDiscRaisePixels;
            var drop = recipe.StudFootDropPixels;
            var ring = recipe.StudRingWidthPixels;
            var centers = StudCenters(perSide);
            var diagonal = 1.0 / System.Math.Sqrt(2.0);

            foreach (var cy in centers)
            {
                foreach (var cx in centers)
                {
                    // The top sits its raise above the grid point; the
                    // crescent falls away from the light, to the bottom right.
                    var discY = cy + raise;
                    var middle = Pixel(studs, cx, discY);
                    var past = radius + drop / 2.0;
                    var crescent = Pixel(studs, cx + past * diagonal, discY - past * diagonal);

                    Assert.AreEqual(255, middle.a, $"the top at ({cx}, {cy})");
                    Assert.AreEqual(recipe.StudTone * 255f, middle.r, RoundingTolerance, $"the top's tone at ({cx}, {cy})");
                    Assert.AreEqual(255, crescent.a, $"the crescent beside ({cx}, {cy})");
                    Assert.AreEqual(recipe.StudFootTone * 255f, crescent.r, RoundingTolerance, $"the crescent's tone beside ({cx}, {cy})");

                    // The rim runs all round, crisp and in one tone, between
                    // the top and the crescent's.
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        var rim = SolidRimPixel(studs, cx, discY, dx, dy, radius, ring);
                        Assert.AreEqual(255, rim.a, $"the rim of ({cx}, {cy}) toward ({dx}, {dy})");
                        Assert.AreEqual(recipe.StudRingTone * 255f, rim.r, RoundingTolerance, $"the rim's tone on ({cx}, {cy}) toward ({dx}, {dy})");
                        Assert.Less(crescent.r, rim.r, "the crescent darker than the rim");
                        Assert.Less(rim.r, middle.r, "the rim darker than the top");
                    }

                    // Inside the rim the top is flat: toward the light it is
                    // the top's own tone, away from it only gently darker.
                    var inside = radius - ring - 2.0;
                    var towardLight = Pixel(studs, cx - inside * diagonal, discY + inside * diagonal);
                    var awayFromLight = Pixel(studs, cx + inside * diagonal, discY - inside * diagonal);
                    Assert.AreEqual(middle.r, towardLight.r, RoundingTolerance, $"the top of ({cx}, {cy}) toward the light");
                    Assert.Less(awayFromLight.r, middle.r, $"the top of ({cx}, {cy}) shifts away from the light");
                    Assert.GreaterOrEqual(awayFromLight.r, recipe.StudShadeTone * 255f - RoundingTolerance, $"only gently, on ({cx}, {cy})");
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

        [TestCase(1, 35f, 59f)]
        [TestCase(2, 35f, 59f)]
        [TestCase(3, 14f, 40f)]
        public void Paint_StudGloss_IsAFaintArcOnTheRimTowardTheLightAndNoSpot(int perSide, float diameter, float spacing)
        {
            SetStudGrid(perSide, diameter, spacing);
            var gloss = ArtPainter.Paint(recipe, ArtSprite.StudGloss);
            var radius = recipe.StudDiameterPixels / 2.0;
            var width = recipe.StudRimLightWidthPixels;
            var centers = StudCenters(perSide);
            var diagonal = 1.0 / System.Math.Sqrt(2.0);

            foreach (var cy in centers)
            {
                foreach (var cx in centers)
                {
                    // From the top left: an arc on the top-left rim, thin,
                    // and nothing on the top itself or the bottom-right rim.
                    var discY = cy + recipe.StudDiscRaisePixels;
                    var onArc = radius - width / 2.0;
                    var deeper = radius - width - 2.0;
                    Assert.Greater(Pixel(gloss, cx - onArc * diagonal, discY + onArc * diagonal).a, 0, $"the arc on ({cx}, {cy})");
                    Assert.AreEqual(0, Pixel(gloss, cx - deeper * diagonal, discY + deeper * diagonal).a, $"thin, on ({cx}, {cy})");
                    Assert.AreEqual(0, Pixel(gloss, cx, discY).a, $"no spot on ({cx}, {cy})");
                    Assert.AreEqual(0, Pixel(gloss, cx + onArc * diagonal, discY - onArc * diagonal).a, $"nothing on the bottom right of ({cx}, {cy})");
                }
            }

            Assert.IsTrue(gloss.Pixels.All(p => p.r == 255), "white");
            Assert.LessOrEqual(gloss.Pixels.Max(p => p.a), recipe.StudRimLightAlpha * 255f + RoundingTolerance, "no stronger than the rim light");
        }

        [Test]
        public void Paint_DefaultRecipe_IsMatte()
        {
            // The lit edge and the studs' rim light stay faint, and the
            // shaded edge is softer than black at 0.4.
            var ceiling = MatteLightAlphaCeiling * 255f;
            var litEdge = ArtPainter.Paint(recipe, ArtSprite.LightEdgeTop).Pixels.Max(p => p.a);
            var shadedEdge = ArtPainter.Paint(recipe, ArtSprite.LightEdgeBottom).Pixels.Max(p => p.a);
            var studLight = ArtPainter.Paint(recipe, ArtSprite.StudGloss).Pixels.Max(p => p.a);

            Assert.That(litEdge, Is.GreaterThan(0).And.LessThanOrEqualTo(ceiling), "the lit edge");
            Assert.That(studLight, Is.GreaterThan(0).And.LessThanOrEqualTo(ceiling), "the studs' rim light");
            Assert.That(shadedEdge, Is.GreaterThan(0).And.LessThan(0.4f * 255f), "the shaded edge");
        }

        [Test]
        public void Paint_Studs_CastASoftShadowAwayFromTheLightBeyondTheCrescent()
        {
            // The default grid's bottom-left stud: half way across the strip
            // the shadow shows past its crescent, to the bottom right, it is
            // in its own tone; as far to the top left, the face is clear.
            var studs = ArtPainter.Paint(recipe, ArtSprite.Studs);
            var center = StudCenters(recipe.StudsPerCellSide)[0];
            var discY = center + recipe.StudDiscRaisePixels;
            var diagonal = 1.0 / System.Math.Sqrt(2.0);
            var past = recipe.StudDiameterPixels / 2.0 + recipe.StudFootDropPixels + recipe.StudShadowOffsetPixels / 2.0;

            var shadow = Pixel(studs, center + past * diagonal, discY - past * diagonal);
            var towardLight = Pixel(studs, center - past * diagonal, discY + past * diagonal);

            Assert.Greater(shadow.a, 128, "the shadow, past the crescent");
            Assert.AreEqual(recipe.StudShadowTone * 255f, shadow.r, RoundingTolerance, "in the shadow's tone");
            Assert.AreEqual(0, towardLight.a, "nothing toward the light");
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
        /// Sets the recipe's stud grid and checks the recipe still has no
        /// problems.
        /// </summary>
        private void SetStudGrid(int perSide, float diameter, float spacing)
        {
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("studsPerCellSide").intValue = perSide;
            serialized.FindProperty("studDiameterPixels").floatValue = diameter;
            serialized.FindProperty("studSpacingPixels").floatValue = spacing;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsEmpty(recipe.Problems(), "the grid is a valid recipe");
        }

        /// <summary>
        /// Where a face piece's straight top edge runs in a quarter sprite,
        /// from its bottom, in pixels: the outline's outer edge less the
        /// outline.
        /// </summary>
        private double FaceEdgeHeight() =>
            recipe.QuarterPixels - recipe.BlockGapPixels / 2.0 - recipe.PieceOutlinePixels;

        /// <summary>
        /// The pixel on the rim of the stud top centred at
        /// <c>(cx, cy)</c>, out from it along axis <c>(dx, dy)</c>, whose own
        /// centre lies wholly inside both the top and its rim: at least half a
        /// pixel inside the top's edge, so antialiasing does not mix in what
        /// lies beneath, and at least half a pixel short of the rim's inner
        /// edge. Picked by the pixel's exact centre, since a point on a
        /// pixel's corner can floor into one whose centre is on the edge.
        /// </summary>
        private static Color32 SolidRimPixel(ArtImage image, double cx, double cy, int dx, int dy, double radius, double ring)
        {
            // The row (or column) of pixels through the centre, and the
            // pixels out along it.
            var across = (dx != 0 ? System.Math.Floor(cy) : System.Math.Floor(cx)) + 0.5;
            var along = dx != 0 ? cx : cy;
            var offAxis = across - (dx != 0 ? cy : cx);
            for (var step = 0; step <= radius + 1.0; step++)
            {
                var pixelCentre = System.Math.Floor(along) + 0.5 + (dx + dy) * step;
                var sd = System.Math.Sqrt((pixelCentre - along) * (pixelCentre - along) + offAxis * offAxis) - radius;
                if (sd <= -0.5 && sd >= -(ring - 0.5))
                {
                    return dx != 0 ? Pixel(image, pixelCentre, across) : Pixel(image, across, pixelCentre);
                }
            }

            Assert.Fail($"no pixel lies wholly on the rim of the stud at ({cx}, {cy}) toward ({dx}, {dy})");
            return default;
        }

        /// <summary>
        /// The centres of the studs along one axis of the cell sprite:
        /// Stud Spacing Pixels apart, centred on the cell.
        /// </summary>
        private List<double> StudCenters(int perSide)
        {
            double spacing = recipe.StudSpacingPixels;
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

        /// <summary>
        /// Asserts two seam profiles match: alpha everywhere, and tone wherever
        /// it can show — at a pixel with some alpha on either side, or next to
        /// one, where filtering blends it in. Deeper in a transparent run the
        /// tone is never seen, and a light piece leaves it undefined where two
        /// edges are equally near: on the diagonal at the cell's centre, or in
        /// the corner of a notch, between a lit edge and a shaded one.
        /// </summary>
        private static void AssertSameProfile(IReadOnlyList<Color32> expected, IReadOnlyList<Color32> actual, string label)
        {
            Assert.AreEqual(expected.Count, actual.Count, label);
            var tonesCompared = 0;
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(expected[i].a - actual[i].a), RoundingTolerance, $"{label}, pixel {i}: alpha");

                if (CanShow(expected, i) || CanShow(actual, i))
                {
                    Assert.LessOrEqual(Mathf.Abs(expected[i].r - actual[i].r), RoundingTolerance, $"{label}, pixel {i}: tone");
                    tonesCompared++;
                }
            }

            Assert.Greater(tonesCompared, 0, $"{label}: some of the profile shows, so its tone was compared");
        }

        /// <summary>True when pixel <paramref name="i"/> of <paramref name="profile"/> or a neighbour along it has any alpha.</summary>
        private static bool CanShow(IReadOnlyList<Color32> profile, int i) =>
            profile[i].a > 0 || (i > 0 && profile[i - 1].a > 0) || (i + 1 < profile.Count && profile[i + 1].a > 0);
    }
}
