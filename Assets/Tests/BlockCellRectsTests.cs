using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BlockCellRects"/>: a block's cells are inset only on
    /// sides facing outside its own footprint, so a multi-cell block draws as
    /// one shape while separate blocks keep the gap between them.
    /// </summary>
    public class BlockCellRectsTests
    {
        private const float Gap = 0.1f;
        private const float Half = Gap * 0.5f;
        private const float Tolerance = 1e-5f;

        private static void AssertRect(float xMin, float yMin, float xMax, float yMax, Rect actual, string label)
        {
            Assert.AreEqual(xMin, actual.xMin, Tolerance, $"{label} xMin");
            Assert.AreEqual(yMin, actual.yMin, Tolerance, $"{label} yMin");
            Assert.AreEqual(xMax, actual.xMax, Tolerance, $"{label} xMax");
            Assert.AreEqual(yMax, actual.yMax, Tolerance, $"{label} yMax");
        }

        [Test]
        public void Compute_SingleCell_IsInsetOnAllFourSides()
        {
            var cells = new[] { new Coord(0, 0) };

            var rects = BlockCellRects.Compute(cells, Gap);

            AssertRect(Half, Half, 1 - Half, 1 - Half, rects[0], "cell (0,0)");
        }

        [Test]
        public void Compute_VerticalOneByTwo_HasNoInsetOnTheSharedSide()
        {
            var cells = new[] { new Coord(0, 0), new Coord(0, 1) };

            var rects = BlockCellRects.Compute(cells, Gap);

            AssertRect(Half, Half, 1 - Half, 1, rects[0], "lower cell");
            AssertRect(Half, 1, 1 - Half, 2 - Half, rects[1], "upper cell");
        }

        [Test]
        public void Compute_LShape_CellsTouchAlongSharedSidesAndTheOuterBoundaryKeepsTheInset()
        {
            // (0,1)
            // (0,0) (1,0)
            var cells = new[] { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

            var rects = BlockCellRects.Compute(cells, Gap);

            AssertRect(Half, Half, 1, 1, rects[0], "corner cell");
            AssertRect(1, Half, 2 - Half, 1 - Half, rects[1], "right cell");
            AssertRect(Half, 1, 1 - Half, 2 - Half, rects[2], "upper cell");
            Assert.AreEqual(rects[0].xMax, rects[1].xMin, Tolerance, "corner and right cells touch");
            Assert.AreEqual(rects[0].yMax, rects[2].yMin, Tolerance, "corner and upper cells touch");
        }

        [Test]
        public void Compute_TwoSeparateAdjacentBlocks_EachKeepsItsInsetOnTheFacingSide()
        {
            // Two 1x1 blocks at grid (0,0) and (1,0); each footprint is
            // computed on its own and placed at its origin.
            var single = new[] { new Coord(0, 0) };
            var leftOrigin = new Vector2(0f, 0f);
            var rightOrigin = new Vector2(1f, 0f);

            var left = BlockCellRects.Compute(single, Gap)[0];
            var right = BlockCellRects.Compute(single, Gap)[0];

            var leftFacingEdge = leftOrigin.x + left.xMax;
            var rightFacingEdge = rightOrigin.x + right.xMin;
            Assert.AreEqual(1 - Half, leftFacingEdge, Tolerance);
            Assert.AreEqual(1 + Half, rightFacingEdge, Tolerance);
            Assert.AreEqual(Gap, rightFacingEdge - leftFacingEdge, Tolerance, "the full gap shows between them");
        }
    }
}
