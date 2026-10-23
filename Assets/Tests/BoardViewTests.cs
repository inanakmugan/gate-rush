using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BoardView"/>'s layout rules shared with its recipes
    /// and effects (Module 46, pass 2): a machine's screen splits along its
    /// longer side into the next block's area and the count's, and an
    /// elevator's two doors stand side by side in the recess inside its rim.
    /// </summary>
    public class BoardViewTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void SplitScreen_Wide_PutsTheCountAtTheRightAndShrinksTheBlocksPartAboutItsCentre()
        {
            var screen = new Rect(0f, 0f, 2f, 0.5f);

            var (block, count) = BoardView.SplitScreen(screen, 0.25f, 0.5f);

            Assert.AreEqual(1.5f, count.xMin, Tolerance, "the count's quarter at the right");
            Assert.AreEqual(2f, count.xMax, Tolerance);
            Assert.AreEqual(0.5f, count.height, Tolerance, "the count is the screen's height");
            Assert.AreEqual(0.25f, block.height, Tolerance, "the block's area half its part's height");
            Assert.AreEqual(0.75f, block.center.x, Tolerance, "the block's area centred on its part");
            Assert.AreEqual(0.75f, block.width, Tolerance, "half its part's width");
        }

        [Test]
        public void SplitScreen_Upright_PutsTheCountAtTheBottom()
        {
            var screen = new Rect(0f, 0f, 0.5f, 2f);

            var (block, count) = BoardView.SplitScreen(screen, 0.25f, 1f);

            Assert.AreEqual(0f, count.yMin, Tolerance, "the count at the bottom");
            Assert.AreEqual(0.5f, count.yMax, Tolerance);
            Assert.AreEqual(0.5f, block.yMin, Tolerance, "the block above it");
            Assert.AreEqual(2f, block.yMax, Tolerance);
        }

        [Test]
        public void ElevatorDoorRects_TwoByTwo_StandInTheRecessEitherSideOfTheSeamWithTheGapRoundEach()
        {
            var config = ScriptableObject.CreateInstance<RuntimeConfig>();
            try
            {
                var region = new Rect(0f, 0f, 2f, 2f);
                var inset = config.ElevatorInnerInsetCells;
                var gap = config.ElevatorDoorGapCells;

                var (left, right) = BoardView.ElevatorDoorRects(config, region);

                Assert.AreEqual(inset.x + gap, left.xMin, Tolerance, "the left door inside the recess's left side");
                Assert.AreEqual(1f - gap, left.xMax, Tolerance, "short of the seam by the gap");
                Assert.AreEqual(1f + gap, right.xMin, Tolerance, "the right door past the seam by the gap");
                Assert.AreEqual(2f - inset.x - gap, right.xMax, Tolerance, "inside the recess's right side");
                Assert.AreEqual(inset.y + gap, left.yMin, Tolerance, "above the recess's bottom");
                Assert.AreEqual(2f - inset.y - gap, left.yMax, Tolerance, "below its top");
                Assert.AreEqual(left.size, right.size, "the same size");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
