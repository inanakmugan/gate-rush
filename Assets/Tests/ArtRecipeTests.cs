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
    /// broken.
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
        [TestCase("cornerRadiusPixels", 60f, "Corner Radius Pixels")]
        [TestCase("rimWidthPixels", 60f, "Rim Width Pixels")]
        [TestCase("studDiameterPixels", 60f, "Stud Diameter Pixels")]
        [TestCase("studSpacingPixels", 100f, "Stud Spacing Pixels")]
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
