using GateRush.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="LightingComparison"/> (Module 46): it renders the
    /// reference script's scene — four cells by three, with a 24-pixel margin
    /// — showing the script's floor where no block or shadow reaches and a
    /// lit block where one sits.
    /// </summary>
    public class LightingComparisonTests
    {
        // The reference scene's size and floor (reference_render.py).
        private const int Columns = 4;
        private const int Rows = 3;
        private const int MarginPixels = 24;
        private static readonly Color32 Floor = new Color32(0x28, 0x13, 0x50, 0xFF);

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
        public void Render_DefaultRecipe_IsTheReferenceScenesSize()
        {
            var image = LightingComparison.Render(recipe);

            Assert.AreEqual(Columns * recipe.CellPixels + 2 * MarginPixels, image.Width);
            Assert.AreEqual(Rows * recipe.CellPixels + 2 * MarginPixels, image.Height);
        }

        [Test]
        public void Render_DefaultRecipe_ShowsTheFloorInItsCornersAndABlockInsideIt()
        {
            var image = LightingComparison.Render(recipe);
            Color32 At(int x, int y) => image.Pixels[y * image.Width + x];

            foreach (var corner in new[] { At(0, 0), At(image.Width - 1, 0), At(0, image.Height - 1), At(image.Width - 1, image.Height - 1) })
            {
                Assert.AreEqual(Floor.r, corner.r, 1, "the floor's red");
                Assert.AreEqual(Floor.g, corner.g, 1, "the floor's green");
                Assert.AreEqual(Floor.b, corner.b, 1, "the floor's blue");
            }

            // The middle of the first cell of the top-left block, the red one.
            var red = At(MarginPixels + recipe.CellPixels / 2, image.Height - MarginPixels - recipe.CellPixels / 2);
            Assert.Greater(red.r, red.g, "a red block");
            Assert.Greater(red.r, red.b, "a red block");
        }
    }
}
