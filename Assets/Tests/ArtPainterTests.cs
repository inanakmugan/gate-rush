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
    /// puts them. Module 46: the plain pieces are white silhouettes; the lit
    /// face and highlight pieces are the reference model's shade and
    /// highlight (reference_render.py) at sample points, brighter on the
    /// bevel toward the light; every placed set — face, highlight, shadow —
    /// meets itself at every seam a tiling can put two of its pieces at; the
    /// shadow pieces are widened outward and fall down and to the right; the
    /// studs are the model's contact shadow, wall, top and rim light at sample
    /// points, one per grid point; and the gate arrow's outline covers the
    /// arrow inside its sprite.
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

        // A placed set's sprites from its first, in the generator's order:
        // outer corners and inside corners top left, top right, bottom left,
        // bottom right; edges top, bottom, left, right; the fill.
        private const int ConcaveIndex = 4;
        private const int EdgeTopIndex = 8;
        private const int EdgeBottomIndex = 9;
        private const int EdgeLeftIndex = 10;
        private const int EdgeRightIndex = 11;
        private const int FillIndex = 12;

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

            Assert.AreEqual(67, checkedSprites, "every generated sprite is checked");
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

        [TestCase(ArtSprite.QuarterOuter)]
        [TestCase(ArtSprite.QuarterEdge)]
        [TestCase(ArtSprite.QuarterFill)]
        [TestCase(ArtSprite.QuarterConcave)]
        public void Paint_PlainPiece_IsWhiteSoTheTintShowsExactly(ArtSprite sprite)
        {
            var plain = ArtPainter.Paint(recipe, sprite);

            Assert.IsTrue(plain.Pixels.All(p => p.r == 255 && p.g == 255 && p.b == 255), "white everywhere, its edge too");
            Assert.AreEqual(255, plain.Pixels[0].a, "opaque at the cell's centre");
        }

        // Sample points for the lit model, in a quarter sprite's own pixels:
        // the piece, its place (a corner's side, or an edge's outward
        // direction on the board), and a pixel centre. The flat, a lit and a
        // shaded bevel, the bevel's foot, a side, two corner arcs and an
        // inside corner.
        [TestCase(ArtSprite.FaceFill, ArtSprite.BodyFill, "fill", 1, 1, 10.5, 10.5)]
        [TestCase(ArtSprite.FaceEdgeTop, ArtSprite.BodyEdgeTop, "edge", 0, 1, 20.5, 54.5)]
        [TestCase(ArtSprite.FaceEdgeBottom, ArtSprite.BodyEdgeBottom, "edge", 0, -1, 20.5, 54.5)]
        [TestCase(ArtSprite.FaceEdgeLeft, ArtSprite.BodyEdgeLeft, "edge", -1, 0, 20.5, 59.5)]
        [TestCase(ArtSprite.FaceEdgeRight, ArtSprite.BodyEdgeRight, "edge", 1, 0, 20.5, 50.5)]
        [TestCase(ArtSprite.FaceOuterTopLeft, ArtSprite.BodyOuterTopLeft, "outer", -1, 1, 48.5, 48.5)]
        [TestCase(ArtSprite.FaceOuterBottomRight, ArtSprite.BodyOuterBottomRight, "outer", 1, -1, 48.5, 48.5)]
        [TestCase(ArtSprite.FaceConcaveBottomRight, ArtSprite.BodyConcaveBottomRight, "concave", 1, -1, 55.5, 55.5)]
        public void Paint_LitPieces_AreTheModelsShadeAndHighlightAtSamplePoints(
            ArtSprite faceSprite, ArtSprite bodySprite, string shape, int signX, int signY, double x, double y)
        {
            var (depth, outX, outY) = Geometry(shape, signX, signY, x, y);
            var (shade, specular) = Model(depth, outX, outY);

            var face = Pixel(ArtPainter.Paint(recipe, faceSprite), x, y);
            var body = Pixel(ArtPainter.Paint(recipe, bodySprite), x, y);

            Assert.Greater(depth, 1.0, "the sample lies inside the body, past its anti-aliased edge");
            Assert.AreEqual(255, face.a, "the face covers the body");
            Assert.AreEqual(Srgb(shade / recipe.ShadeHeadroom), face.r, RoundingTolerance, $"the shade {shade}, stored for the lit material");
            Assert.AreEqual(255, body.r, "the highlight is white");
            Assert.AreEqual(specular * 255.0, body.a, RoundingTolerance, $"the highlight {specular}, as alpha for the specular material");
        }

        [Test]
        public void Paint_LitPieces_AreBrighterOnTheTopLeftBevelThanOnTheBottomRight()
        {
            // The same depth into the bevel, facing toward the light and away.
            var lit = Pixel(ArtPainter.Paint(recipe, ArtSprite.FaceEdgeTop), 20.5, 54.5);
            var shaded = Pixel(ArtPainter.Paint(recipe, ArtSprite.FaceEdgeBottom), 20.5, 54.5);
            var flat = Pixel(ArtPainter.Paint(recipe, ArtSprite.FaceFill), 10.5, 10.5);

            Assert.Greater(lit.r, flat.r, "the top bevel catches more light than the flat");
            Assert.Less(shaded.r, flat.r, "the bottom bevel less");
        }

        [TestCase("Face")]
        [TestCase("Body")]
        [TestCase("Shadow")]
        public void Paint_PlacedSet_MeetsItselfAtEverySeam(string set)
        {
            var first = set == "Face" ? ArtSprite.FaceOuterTopLeft : set == "Body" ? ArtSprite.BodyOuterTopLeft : ArtSprite.ShadowOuterTopLeft;
            ArtImage Piece(int index) => ArtPainter.Paint(recipe, first + index);
            var places = new[] { (-1, 1), (1, 1), (-1, -1), (1, -1) };
            var concaveSeamPixels = 0;

            for (var corner = 0; corner < places.Length; corner++)
            {
                var (signX, signY) = places[corner];
                var alongX = Piece(signY > 0 ? EdgeTopIndex : EdgeBottomIndex);
                var alongY = Piece(signX > 0 ? EdgeRightIndex : EdgeLeftIndex);

                // An outer corner's left column runs on into the edge along x,
                // its bottom row into the edge along y.
                var outer = Piece(corner);
                AssertSameProfile(Column(alongX, 0), Column(outer, 0), $"{set} outer {corner}: left column");
                AssertSameProfile(Column(alongY, 0), Row(outer, 0), $"{set} outer {corner}: bottom row");

                // An inside corner's notch floor runs on into the edge along
                // x, its side into the edge along y. A shadow is compared short
                // of the notch's reach: carried by its offset, an inside
                // corner's shadow sees the notch's rounded corner, which an
                // edge's does not, so the gap at an inside corner is a little
                // darker — by design.
                var concave = Piece(ConcaveIndex + corner);
                var shiftX = signX * recipe.FloorShadowOffsetPixels.x;
                var shiftY = signY * recipe.FloorShadowOffsetPixels.y;
                var rightLength = set == "Shadow" ? SeamClearOfTheNotch(shiftX, shiftY, concave.Height) : concave.Height;
                var topLength = set == "Shadow" ? SeamClearOfTheNotch(shiftY, shiftX, concave.Width) : concave.Width;
                if (rightLength > 0)
                {
                    AssertSameProfile(
                        Column(alongX, 0).Take(rightLength).ToList(), Column(concave, concave.Width - 1).Take(rightLength).ToList(),
                        $"{set} concave {corner}: right column");
                }

                if (topLength > 0)
                {
                    AssertSameProfile(
                        Column(alongY, 0).Take(topLength).ToList(), Row(concave, concave.Height - 1).Take(topLength).ToList(),
                        $"{set} concave {corner}: top row");
                }

                concaveSeamPixels += rightLength + topLength;

                if (set != "Shadow")
                {
                    // Deep inside, every piece is the flat top the fill is.
                    var fill = Piece(FillIndex);
                    AssertSameProfile(Column(fill, fill.Width - 1), Column(concave, 0), $"{set} concave {corner}: left column, beside the fill");
                    AssertSameProfile(Row(fill, fill.Height - 1), Row(concave, 0), $"{set} concave {corner}: bottom row, beside the fill");
                    AssertSameProfile(Row(fill, fill.Height - 1), Row(alongX, 0), $"{set} edge along x {corner}: its inner row, beside the fill");
                }
            }

            // Whatever the notch leaves out, the inside corners' seams are
            // still compared over most of a quarter's length in all.
            Assert.GreaterOrEqual(concaveSeamPixels, recipe.QuarterPixels, $"{set}: the inside corners' seams compared");
        }

        [Test]
        public void Paint_ShadowPieces_AreWidenedOutwardAndFallDownAndToTheRight()
        {
            var quarter = recipe.QuarterPixels;
            var margin = recipe.FloorShadowMarginPixels;
            var outer = ArtPainter.Paint(recipe, ArtSprite.ShadowOuterTopLeft);
            var top = ArtPainter.Paint(recipe, ArtSprite.ShadowEdgeTop);
            var bottom = ArtPainter.Paint(recipe, ArtSprite.ShadowEdgeBottom);
            var concave = ArtPainter.Paint(recipe, ArtSprite.ShadowConcaveTopLeft);

            Assert.Greater(margin, 0, "the shadow reaches past the cell");
            Assert.AreEqual((quarter + margin, quarter + margin), (outer.Width, outer.Height), "an outer corner widens on both outward sides");
            Assert.AreEqual((quarter, quarter + margin), (top.Width, top.Height), "an edge widens on its outward side");
            Assert.AreEqual((quarter, quarter), (concave.Width, concave.Height), "an inside corner does not widen");

            // Along an edge's column, outward from its edge: the model's
            // softened step, carried by the offset — down, so the top edge's
            // shadow is pulled in under the body and the bottom edge's pushed
            // out past it. An edge sprite's y runs outward from the body.
            var e = quarter - recipe.BlockGapPixels / 2.0;
            var down = -recipe.FloorShadowOffsetPixels.y;
            foreach (var y in new[] { 10.5, e + 0.5, e + 5.5, e + 10.5 })
            {
                Assert.AreEqual(ShadowAlpha(y - e + down), Column(top, 0)[(int)y].a, RoundingTolerance, $"the top edge's shadow at {y}");
                Assert.AreEqual(ShadowAlpha(y - e - down), Column(bottom, 0)[(int)y].a, RoundingTolerance, $"the bottom edge's shadow at {y}");
            }

            Assert.Greater(Column(bottom, 0)[(int)(e + 5.5)].a, Column(top, 0)[(int)(e + 5.5)].a, "it falls down");
            Assert.IsTrue(top.Pixels.All(p => p.r == 0), "black");
        }

        [Test]
        public void Paint_Studs_AreTheModelsStudAtSamplePoints()
        {
            // The default grid's bottom-left stud: its top, its wall, its
            // contact shadow alone, and its rim toward and away from the light.
            var studs = ArtPainter.Paint(recipe, ArtSprite.Studs);
            var gloss = ArtPainter.Paint(recipe, ArtSprite.StudGloss);
            var cx = StudCenters(recipe.StudsPerCellSide)[0];
            var cy = cx;
            var radius = recipe.StudDiameterPixels / 2.0;
            var top = recipe.StudTopOffsetPixels;
            var wall = recipe.StudWallOffsetPixels;
            var contact = recipe.StudContactOffsetPixels;
            var light = recipe.StudRimLightDirection.normalized;
            var rimRadius = radius - recipe.StudRimFullPixels;

            var samples = new (string What, double X, double Y)[]
            {
                ("the top", cx + top.x, cy + top.y),
                ("the wall", cx + wall.x, cy + wall.y - (radius - 1.0)),
                ("the contact shadow", cx + contact.x, cy + contact.y - (radius + 2.0)),
                ("the rim toward the light", cx + top.x + light.x * rimRadius, cy + top.y + light.y * rimRadius),
                ("the rim away from the light", cx + top.x - light.x * rimRadius, cy + top.y - light.y * rimRadius),
            };

            foreach (var (what, x, y) in samples)
            {
                // The pixel the point falls in, at its centre.
                var px = System.Math.Floor(x) + 0.5;
                var py = System.Math.Floor(y) + 0.5;
                var (tone, alpha, rimLight) = StudModel(px, py, cx, cy);

                var stud = Pixel(studs, px, py);
                Assert.AreEqual(alpha * 255.0, stud.a, RoundingTolerance, $"{what}: coverage");
                if (alpha > 0.0)
                {
                    Assert.AreEqual(Srgb(tone / recipe.ShadeHeadroom), stud.r, RoundingTolerance, $"{what}: tone {tone}, stored for the lit material");
                }

                Assert.AreEqual(rimLight * 255.0, Pixel(gloss, px, py).a, RoundingTolerance, $"{what}: rim light");
            }

            Assert.Greater(Pixel(gloss, samples[3].X, samples[3].Y).a, 0, "the rim toward the light is lit");
            Assert.AreEqual(0, Pixel(gloss, samples[4].X, samples[4].Y).a, "the rim away from it is not");
            Assert.IsTrue(gloss.Pixels.All(p => p.r == 255), "the rim light is white");
        }

        [Test]
        public void Paint_Studs_PaintsOneStudPerGridPointAndLeavesTheCellsCornersClear()
        {
            var studs = ArtPainter.Paint(recipe, ArtSprite.Studs);

            Assert.AreEqual(recipe.StudsPerCellSide * recipe.StudsPerCellSide, CountStudCentres(studs), "one stud per grid point");
            Assert.AreEqual(0, Pixel(studs, 0.5, 0.5).a, "the cell's corner");
            Assert.AreEqual(0, Pixel(studs, recipe.CellPixels - 0.5, recipe.CellPixels - 0.5).a, "the opposite corner");
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
        /// <summary>
        /// How deep a sprite pixel centre <c>(x, y)</c> lies inside its body's
        /// edge, and the direction that edge faces on the board, worked out
        /// from the recipe's geometry: the edge at <c>e = q − gap/2</c>, an
        /// outer corner's arc about <c>e − radius</c>, an inside corner's
        /// notch about <c>e + its radius</c>, each mirrored by the place's signs.
        /// </summary>
        private (double depth, double outX, double outY) Geometry(string shape, int signX, int signY, double x, double y)
        {
            var e = recipe.QuarterPixels - recipe.BlockGapPixels / 2.0;
            switch (shape)
            {
                case "fill":
                    return (recipe.QuarterPixels, 0.0, 0.0);
                case "edge":
                    // An edge sprite's edge is its top, whichever side it is posed on.
                    return (e - y, signX, signY);
                case "outer":
                {
                    var c = e - recipe.CornerRadiusPixels;
                    var length = System.Math.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    return (recipe.CornerRadiusPixels - length, signX * (x - c) / length, signY * (y - c) / length);
                }

                default:
                {
                    var c = e + recipe.ConcaveRadiusPixels;
                    var length = System.Math.Sqrt((c - x) * (c - x) + (c - y) * (c - y));
                    return (length - recipe.ConcaveRadiusPixels, signX * (c - x) / length, signY * (c - y) / length);
                }
            }
        }

        /// <summary>
        /// The reference model's grey shade and white highlight
        /// <paramref name="depth"/> pixels inside an edge facing
        /// <c>(outX, outY)</c> (reference_render.py): the height rises as a
        /// quarter circle, its slope gives the normal, one light lights it by
        /// Blinn-Phong, and the occlusion is the recipe's falloff.
        /// </summary>
        private (double shade, double specular) Model(double depth, double outX, double outY)
        {
            double bevel = recipe.BevelPixels;
            var t = System.Math.Min(1.0, System.Math.Max(0.0, depth / bevel));

            // The height's slope, h' = H/B · (1 − t)/√(1 − (1 − t)²); the
            // normal is (h' · out, 1), normalised.
            var slope = t >= 1.0 ? 0.0 : recipe.BodyHeightPixels / bevel * (1.0 - t) / System.Math.Sqrt(1.0 - (1.0 - t) * (1.0 - t));
            var n = Normalize(slope * outX, slope * outY, 1.0);
            var l = Normalize(recipe.LightDirection.x, recipe.LightDirection.y, recipe.LightDirection.z);
            var h = Normalize(l.x, l.y, l.z + 1.0);

            var diffuse = System.Math.Max(0.0, n.x * l.x + n.y * l.y + n.z * l.z);
            var specular = recipe.SpecularStrength * System.Math.Pow(System.Math.Max(0.0, n.x * h.x + n.y * h.y + n.z * h.z), recipe.Shininess);
            var fade = SmoothStep(0.0, recipe.OcclusionFootPixels, depth);
            var occlusion = System.Math.Max(recipe.OcclusionMinimum, 1.0 - recipe.OcclusionFoot * (1.0 - fade));
            return ((recipe.Ambient + recipe.Diffuse * diffuse) * occlusion, specular);
        }

        /// <summary>
        /// The reference model's stud at pixel centre <c>(x, y)</c>, its grid
        /// point at <c>(cx, cy)</c> (reference_render.py, step 4): the tinted
        /// sprite's tone and coverage — the contact shadow, the wall and the
        /// top over one another — and the rim light's alpha over the top.
        /// </summary>
        private (double tone, double alpha, double rimLight) StudModel(double x, double y, double cx, double cy)
        {
            var radius = recipe.StudDiameterPixels / 2.0;
            double edge = recipe.StudEdgePixels;
            var co = recipe.StudContactOffsetPixels;
            var wo = recipe.StudWallOffsetPixels;
            var to = recipe.StudTopOffsetPixels;
            var light = recipe.StudRimLightDirection.normalized;

            var contact = 1.0 - SmoothStep(radius + recipe.StudContactInnerPixels, radius + recipe.StudContactOuterPixels, Distance(x, y, cx + co.x, cy + co.y));
            var wall = 1.0 - SmoothStep(radius - edge, radius + edge, Distance(x, y, cx + wo.x, cy + wo.y));
            var r = Distance(x, y, cx + to.x, cy + to.y);
            var top = 1.0 - SmoothStep(radius - edge, radius + edge, r);
            var rim = SmoothStep(radius - recipe.StudRimInnerPixels, radius - recipe.StudRimFullPixels, r)
                      * (1.0 - SmoothStep(radius - recipe.StudRimFadePixels, radius, r));
            var along = ((x - cx) * light.x + (y - cy) * light.y) / radius;
            var toward = System.Math.Max(0.0, along);
            var away = System.Math.Max(0.0, -along);

            var under = OverTone((recipe.StudWallTone, wall), (0.0, recipe.StudContactAlpha * contact));
            var stud = OverTone((recipe.StudTopTone * (1.0 - recipe.StudRimDarken * rim * away), top), under);
            var rimLight = recipe.StudRimLightAlpha * rim * System.Math.Pow(toward, recipe.StudRimLightExponent) * top;
            return (stud.tone, stud.alpha, rimLight);
        }

        /// <summary>
        /// How many pixels of an inside corner's shadow seam, from the cell's
        /// centre, lie clear of its notch: where the notch's rounded corner
        /// moves the shadow by less than half a byte, so a straight edge's
        /// shadow matches it to within the rounding tolerance.
        /// </summary>
        /// <remarks>
        /// The seam runs at <c>q − ½</c>; the offset carries the shadow's
        /// sample <paramref name="acrossShift"/> across it and
        /// <paramref name="alongShift"/> along it. The notch's corner is at
        /// <c>c = e + its radius</c> on both axes. When the sample lands a
        /// distance <c>d</c> short of <c>c</c> across, a point <c>s</c> short
        /// of <c>c</c> along sees the corner at <c>√(d² + s²)</c> instead of
        /// the edge at <c>s</c>, more by under <c>d² / 2s</c>; the softened
        /// step changes by at most <c>alpha · 255 · 1.5 / (2 · spread)</c>
        /// bytes a pixel, so the seam is clear while <c>s ≥ d² / 2ε</c>, with
        /// <c>ε</c> half a byte in pixels. Landing past <c>c</c>, the sample
        /// meets the notch only once it reaches <c>c</c> along.
        /// </remarks>
        private int SeamClearOfTheNotch(double acrossShift, double alongShift, int length)
        {
            double quarter = recipe.QuarterPixels;
            var corner = quarter - recipe.BlockGapPixels / 2.0 + recipe.ConcaveRadiusPixels;
            var bytesPerPixel = recipe.FloorShadowAlpha * 255.0 * 1.5 / (2.0 * recipe.FloorShadowSpreadPixels);
            var epsilon = 0.5 / bytesPerPixel;

            var shortOfCorner = corner - (quarter - 0.5 - acrossShift);
            var clearance = shortOfCorner > 0.0 ? shortOfCorner * shortOfCorner / (2.0 * epsilon) : 0.0;
            var limit = corner - clearance + alongShift;
            return (int)System.Math.Max(0.0, System.Math.Min(length, System.Math.Ceiling(limit - 0.5)));
        }

        /// <summary>A floor-shadow piece's alpha byte <paramref name="sd"/> pixels outside its offset edge, by the model.</summary>
        private double ShadowAlpha(double sd)
        {
            double spread = recipe.FloorShadowSpreadPixels;
            return recipe.FloorShadowAlpha * (1.0 - SmoothStep(-spread, spread, sd)) * 255.0;
        }

        /// <summary>One colour over another, as alpha blending composes them, in linear light.</summary>
        private static (double tone, double alpha) OverTone((double tone, double alpha) top, (double tone, double alpha) bottom)
        {
            var bottomShown = bottom.alpha * (1.0 - top.alpha);
            var alpha = top.alpha + bottomShown;
            return alpha <= 0.0 ? (top.tone, 0.0) : ((top.tone * top.alpha + bottom.tone * bottomShown) / alpha, alpha);
        }

        /// <summary>The byte a linear share is stored as in an sRGB texture.</summary>
        private static double Srgb(double linear)
        {
            var c = System.Math.Min(1.0, System.Math.Max(0.0, linear));
            return (c <= 0.0031308 ? c * 12.92 : 1.055 * System.Math.Pow(c, 1.0 / 2.4) - 0.055) * 255.0;
        }

        private static (double x, double y, double z) Normalize(double x, double y, double z)
        {
            var length = System.Math.Sqrt(x * x + y * y + z * z);
            return (x / length, y / length, z / length);
        }

        private static double Distance(double x, double y, double cx, double cy) =>
            System.Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        private static double SmoothStep(double from, double to, double value)
        {
            var t = System.Math.Min(1.0, System.Math.Max(0.0, (value - from) / (to - from)));
            return t * t * (3.0 - 2.0 * t);
        }

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
