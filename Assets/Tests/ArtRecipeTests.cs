using GateRush.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ArtRecipe.Problems"/>: the default recipe generates,
    /// and each constraint that keeps the quarter pieces meeting at their seams
    /// is reported, naming its field, when broken.
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
        public void Problems_FloatOutOfRange_IsReportedByName(string field, float value, string label)
        {
            SetFloat(field, value);

            var problems = recipe.Problems();

            Assert.AreEqual(1, problems.Count, string.Join(" | ", problems));
            StringAssert.Contains(label, problems[0]);
        }

        [TestCase(127)]
        [TestCase(8)]
        public void Problems_CellPixelsOddOrTooSmall_IsReported(int cellPixels)
        {
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("cellPixels").intValue = cellPixels;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = recipe.Problems();

            Assert.IsTrue(problems.Count >= 1);
            StringAssert.Contains("Cell Pixels", problems[0]);
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
