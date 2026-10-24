using GateRush.Editor;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="MechanicsComparison"/> (Module 46, pass 2): it
    /// composes the mechanics reference script's sheet — four slots by two,
    /// each two cells and a 40-pixel gap, with a 20-pixel margin — showing the
    /// script's floor where nothing is drawn and its blocks in their slots,
    /// the same pixels every time.
    /// </summary>
    public class MechanicsComparisonTests
    {
        // The reference sheet's layout and floor (mechanics_reference.py).
        private const int SlotGapPixels = 40;
        private const int MarginPixels = 20;
        private static readonly Color32 Floor = new Color32(0x28, 0x13, 0x50, 0xFF);

        private ArtRecipe recipe;
        private RuntimeConfig config;

        [SetUp]
        public void SetUp()
        {
            recipe = ScriptableObject.CreateInstance<ArtRecipe>();
            config = ScriptableObject.CreateInstance<RuntimeConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Render_DefaultRecipe_IsTheReferenceSheetsSize()
        {
            var slot = 2 * recipe.CellPixels + SlotGapPixels;

            var image = MechanicsComparison.Render(recipe, config);

            Assert.AreEqual(4 * slot + MarginPixels, image.Width);
            Assert.AreEqual(2 * slot + MarginPixels, image.Height);
        }

        [Test]
        public void Render_DefaultRecipe_ShowsTheFloorInItsCornersAndTheRedBlockInItsSlot()
        {
            var image = MechanicsComparison.Render(recipe, config);
            Color32 TopDown(int x, int y) => image.Pixels[(image.Height - 1 - y) * image.Width + x];

            foreach (var corner in new[] { TopDown(0, 0), TopDown(image.Width - 1, 0), TopDown(0, image.Height - 1), TopDown(image.Width - 1, image.Height - 1) })
            {
                Assert.AreEqual(Floor.r, corner.r, 1, "the floor's red");
                Assert.AreEqual(Floor.g, corner.g, 1, "the floor's green");
                Assert.AreEqual(Floor.b, corner.b, 1, "the floor's blue");
            }

            // The middle of the plain red block's first cell, in the fourth slot of the top row.
            var slot = 2 * recipe.CellPixels + SlotGapPixels;
            var red = TopDown(MarginPixels + 3 * slot + recipe.CellPixels / 2, MarginPixels + recipe.CellPixels / 2);
            Assert.Greater(red.r, red.g, "a red block");
            Assert.Greater(red.r, red.b, "a red block");
        }

        [Test]
        public void Render_SameRecipeTwice_GivesTheSamePixels()
        {
            var first = MechanicsComparison.Render(recipe, config);
            var second = MechanicsComparison.Render(recipe, config);

            CollectionAssert.AreEqual(first.Pixels, second.Pixels);
        }

        [Test]
        public void RenderTimeBonus_DefaultRecipe_IsTheReferenceImagesSize()
        {
            var image = MechanicsComparison.RenderTimeBonus(recipe, config);

            Assert.AreEqual((TimeBonusWidth, TimeBonusHeight), (image.Width, image.Height), "time_bonus_reference.png is 1204 by 346");
        }

        [Test]
        public void RenderTimeBonus_DefaultRecipe_ShowsItsFloorInTheCornersAndTheGreenCoinLeftOfTheFirstBlocksMiddle()
        {
            var image = MechanicsComparison.RenderTimeBonus(recipe, config);
            Color32 TopDown(int x, int y) => image.Pixels[(image.Height - 1 - y) * image.Width + x];

            foreach (var corner in new[] { TopDown(0, 0), TopDown(image.Width - 1, 0), TopDown(0, image.Height - 1), TopDown(image.Width - 1, image.Height - 1) })
            {
                Assert.AreEqual(TimeBonusFloor.r, corner.r, 1, "the floor's red");
                Assert.AreEqual(TimeBonusFloor.g, corner.g, 1, "the floor's green");
                Assert.AreEqual(TimeBonusFloor.b, corner.b, 1, "the floor's blue");
            }

            // The first block, red, at (20, 30): its coin where the "+10"
            // group centred on its middle puts it, the rim below the face
            // green; where the "+10" would be, the red block, as the label is
            // left out.
            var cell = recipe.CellPixels;
            var middle = MarginPixels + cell;
            var coinX = middle + Mathf.RoundToInt(config.TimeBonusCoinCenterCells("+10") * cell);
            var coinY = TimeBonusTopPixels + cell / 2;
            var rim = TopDown(coinX, coinY + (int)((recipe.CoinRadiusPixels + recipe.CoinFaceRadiusPixels) / 2f));
            Assert.Greater(rim.g, rim.r, "the coin's green rim");
            Assert.Greater(rim.g, rim.b, "the coin's green rim");
            var labelMiddle = config.TimeBonusLabelStartCells("+10") + config.TimeBonusLabelAdvanceCells("+10") / 2f;
            var seconds = TopDown(middle + Mathf.RoundToInt(labelMiddle * cell), coinY);
            Assert.Greater(seconds.r, seconds.g, "the red block where the seconds go");
            Assert.Greater(seconds.r, seconds.b, "the red block where the seconds go");
        }

        [Test]
        public void RenderTimeBonus_SameRecipeTwice_GivesTheSamePixels()
        {
            var first = MechanicsComparison.RenderTimeBonus(recipe, config);
            var second = MechanicsComparison.RenderTimeBonus(recipe, config);

            CollectionAssert.AreEqual(first.Pixels, second.Pixels);
        }

        // time_bonus_reference.py's canvas, its top margin and its floor.
        private const int TimeBonusWidth = 1204;
        private const int TimeBonusHeight = 346;
        private const int TimeBonusTopPixels = 30;
        private static readonly Color32 TimeBonusFloor = new Color32(0x2A, 0x24, 0x60, 0xFF);
    }
}
