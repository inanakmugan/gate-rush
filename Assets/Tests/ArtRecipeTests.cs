using GateRush.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ArtRecipe.Problems"/>: the default recipe generates,
    /// and each constraint that keeps the pieces meeting at their seams and the
    /// state sprites (Module 16) drawable is reported, naming its field, when
    /// broken. Module 46: the default occlusion falloff follows the reference
    /// script's blur at a bevel's foot.
    /// </summary>
    public class ArtRecipeTests
    {
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
        public void Problems_DefaultRecipe_IsEmpty()
        {
            var problems = recipe.Problems();

            Assert.IsEmpty(problems);
        }

        [TestCase("concaveRadiusPixels", 6f, "Concave Radius Pixels")]
        [TestCase("cornerRadiusPixels", 63f, "Corner Radius Pixels")]
        [TestCase("rimWidthPixels", 60f, "Rim Width Pixels")]
        [TestCase("bevelPixels", 0f, "Bevel Pixels")]
        [TestCase("bevelPixels", 30f, "Bevel Pixels")]
        [TestCase("cornerRadiusPixels", 10f, "Bevel Pixels")]
        [TestCase("bodyHeightPixels", 0f, "Body Height Pixels")]
        [TestCase("shininess", 0.5f, "Shininess")]
        [TestCase("occlusionFootPixels", 0f, "Occlusion Foot Pixels")]
        [TestCase("occlusionFootPixels", 20f, "Occlusion Foot Pixels")]
        [TestCase("shadeHeadroom", 1f, "Shade Headroom")]
        [TestCase("floorShadowSpreadPixels", 0f, "Floor Shadow Spread Pixels")]
        [TestCase("floorShadowSpreadPixels", 70f, "Floor Shadow Spread Pixels")]
        [TestCase("studDiameterPixels", 130f, "Stud Diameter Pixels")]
        [TestCase("studDiameterPixels", 62f, "Stud Diameter Pixels")]
        [TestCase("studEdgePixels", 0f, "Stud Edge Pixels")]
        [TestCase("studContactInnerPixels", 7f, "Stud Contact Inner Pixels")]
        [TestCase("studRimFullPixels", 4f, "Stud Rim")]
        [TestCase("studRimLightExponent", 0f, "Stud Rim Light Exponent")]
        [TestCase("studSpacingPixels", 40f, "Stud Spacing Pixels")]
        [TestCase("studSpacingPixels", 80f, "Stud Spacing Pixels")]
        [TestCase("gateArrowOutlinePixels", 0f, "Gate Arrow Outline Pixels")]
        [TestCase("gateArrowOutlinePixels", 6f, "Gate Arrow Outline Pixels")]
        [TestCase("axisArrowHeadLengthPixels", 96f, "Axis Arrow Head Length Pixels")]
        [TestCase("vignetteInner", 1f, "Vignette Inner")]
        [TestCase("frostInsetPixels", 5f, "Frost Inset Pixels")]
        [TestCase("panelCornerRadiusPixels", 40f, "Panel Corner Radius Pixels")]
        [TestCase("chainLinkWallPixels", 20f, "Chain Link Wall Pixels")]
        [TestCase("padlockShackleRadius", 0.5f, "Padlock Shackle Radius")]
        [TestCase("keyGemDiameterPixels", 40f, "gem inside hole")]
        [TestCase("slatGapPixels", 40f, "Slat Gap Pixels")]
        [TestCase("doorLineWidthPixels", 70f, "Door Line Width Pixels")]
        [TestCase("clockRimThickness", 0.6f, "Clock Rim Thickness")]
        [TestCase("clockMinuteHandLength", 0.45f, "Clock Minute Hand Length")]
        [TestCase("clockHandThickness", 0f, "Clock Hand Thickness")]
        [TestCase("restartThickness", 0f, "Restart Thickness")]
        [TestCase("restartRadius", 0.45f, "Restart Radius")]
        [TestCase("restartHeadWidth", 0.05f, "Restart Head Width")]
        [TestCase("restartHeadLength", 0.4f, "Restart Head Length")]
        [TestCase("cubeCornerPixels", 30f, "Cube Corner Pixels")]
        [TestCase("cubeRimPixels", -1f, "Cube Rim Pixels")]
        [TestCase("cubeHighlightDiameterPixels", 40f, "Cube Highlight Diameter Pixels")]
        [TestCase("shardStreakWidthPixels", 0f, "Shard Streak Width Pixels")]
        [TestCase("gateGlowHold", 1f, "Gate Glow Hold")]
        [TestCase("gateGlowHold", -0.1f, "Gate Glow Hold")]
        [TestCase("sparkleWaist", 0f, "Sparkle Waist")]
        [TestCase("sparkleWaist", 0.25f, "Sparkle Waist")]
        public void Problems_FloatOutOfRange_IsReportedByName(string field, float value, string label)
        {
            // Some fields feed more than one constraint — frost's inset depends
            // on the corner radius, the rounded box reuses the face's outline
            // and rim — so breaking one may report a dependent problem too. The
            // field's own problem must be among them; that the default recipe
            // reports none at all is Problems_DefaultRecipe_IsEmpty.
            SetFloat(field, value);

            var problems = recipe.Problems();

            Assert.That(problems, Has.Some.Contains(label), string.Join(" | ", problems));
        }

        [TestCase(127)]
        [TestCase(8)]
        public void Problems_CellPixelsOddOrTooSmall_IsReported(int cellPixels)
        {
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("cellPixels").intValue = cellPixels;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = recipe.Problems();

            Assert.That(problems, Has.Some.Contains("Cell Pixels"), string.Join(" | ", problems));
        }

        [TestCase("clockPixels", "Clock Pixels")]
        [TestCase("restartPixels", "Restart Pixels")]
        public void Problems_IconPixelsTooSmall_IsReported(string field, string label)
        {
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty(field).intValue = 8;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = recipe.Problems();

            Assert.That(problems, Has.Some.Contains(label), string.Join(" | ", problems));
        }

        [TestCase("cubePixels", "Cube Pixels")]
        [TestCase("shardPixels", "Shard Pixels")]
        [TestCase("gateGlowPixels", "Gate Glow Pixels")]
        [TestCase("sparklePixels", "Sparkle Pixels")]
        public void Problems_EffectPixelsTooSmall_IsReported(string field, string label)
        {
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty(field).intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = recipe.Problems();

            Assert.That(problems, Has.Some.Contains(label), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_ShardPointsClockwise_IsReported()
        {
            var serialized = new SerializedObject(recipe);
            var points = serialized.FindProperty("shardPoints");
            var first = points.GetArrayElementAtIndex(1).vector2Value;
            points.GetArrayElementAtIndex(1).vector2Value = points.GetArrayElementAtIndex(3).vector2Value;
            points.GetArrayElementAtIndex(3).vector2Value = first;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = recipe.Problems();

            Assert.That(problems, Has.Some.Contains("Shard Points"), string.Join(" | ", problems));
        }

        [TestCase(0, TestName = "Problems_StudsPerCellSideZero_IsReported")]
        [TestCase(3, TestName = "Problems_ThreeStudsPerCellSideAtTheDefaultDiameter_IsReported")]
        [TestCase(4, TestName = "Problems_FourStudsPerCellSideCloserThanTheDefaultDiameter_IsReported")]
        public void Problems_StudsPerCellSideOutOfRange_IsReported(int perSide)
        {
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("studsPerCellSide").intValue = perSide;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = recipe.Problems();

            Assert.That(problems, Has.Some.Contains("Studs Per Cell Side"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_LightBehindTheBoard_IsReported()
        {
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("lightDirection").vector3Value = new Vector3(-0.45f, 0.55f, -0.7f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = recipe.Problems();

            Assert.That(problems, Has.Some.Contains("Light Direction"), string.Join(" | ", problems));
        }

        [Test]
        public void OcclusionPair_DefaultRecipe_MatchesTheReferenceBlurBesideANeighbour()
        {
            // The reference darkens a bevel's foot by how much a blur of the
            // height rises above it: AO = clamp(1 − (blur(h) − h) · k, 0.4, 1),
            // a Gaussian of 6 px, k 0.06 (reference_render.py). Across a
            // straight edge, with the next block's bevel the gap away as on
            // the board, the blur is one-dimensional. The recipe's falloff
            // must follow it to within a few hundredths, foot to flat.
            const double Sigma = 6.0;
            const double K = 0.06;
            const double Floor = 0.4;
            const double Tolerance = 0.03;
            const double Step = 0.05;
            const double Reach = 40.0;
            double gap = recipe.BlockGapPixels;
            double bevel = recipe.BevelPixels;
            double bodyHeight = recipe.BodyHeightPixels;

            double Height(double depth)
            {
                // Inside this body, in the gap, or inside the neighbour.
                var into = depth > 0.0 ? depth : depth < -gap ? -depth - gap : 0.0;
                var t = System.Math.Min(1.0, into / bevel);
                return into <= 0.0 ? 0.0 : System.Math.Sqrt(1.0 - (1.0 - t) * (1.0 - t)) * bodyHeight;
            }

            var compared = 0;
            foreach (var depth in new[] { 0.25, 0.5, 1.0, 1.5, 2.0, 3.0, 6.0 })
            {
                var sum = 0.0;
                var weights = 0.0;
                for (var x = -Reach; x <= Reach; x += Step)
                {
                    var weight = System.Math.Exp(-x * x / (2.0 * Sigma * Sigma));
                    sum += weight * Height(depth + x);
                    weights += weight;
                }

                var reference = System.Math.Max(Floor, System.Math.Min(1.0, 1.0 - (sum / weights - Height(depth)) * K));
                var t = System.Math.Min(1.0, depth / recipe.OcclusionFootPixels);
                var recipes = System.Math.Max(recipe.OcclusionMinimum, 1.0 - recipe.OcclusionFoot * (1.0 - t * t * (3.0 - 2.0 * t)));

                Assert.AreEqual(reference, recipes, Tolerance, $"the occlusion {depth} px in");
                compared++;
            }

            Assert.AreEqual(7, compared);
        }

        private void SetFloat(string field, float value)
        {
            var serialized = new SerializedObject(recipe);
            var property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"the recipe has a serialized field '{field}'");
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
