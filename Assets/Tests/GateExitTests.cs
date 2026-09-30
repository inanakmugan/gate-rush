using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 18's <see cref="GateExit"/>: how far a destroyed block
    /// passes, the clip that cuts it exactly at the gate's inner line and
    /// nowhere else — a lifted block on a border row keeps its lip and outline
    /// on every other side — the glow's band inside the gate, and the gate's
    /// outer line. Grid units; y grows upward.
    /// </summary>
    public class GateExitTests
    {
        private const int Width = 5;
        private const int Height = 4;
        private const float OutlineWidth = 0.07f;
        private const float Lip = 0.09f;
        private const float LiftScale = 1.06f;
        private const float Tolerance = 1e-5f;

        private static readonly Coord[] Horizontal1x2 = { new Coord(0, 0), new Coord(1, 0) };
        private static readonly Coord[] Vertical1x2 = { new Coord(0, 0), new Coord(0, 1) };
        private static readonly Coord[] LShape = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

        [TestCase(BoardEdge.Left, 2)]
        [TestCase(BoardEdge.Right, 2)]
        [TestCase(BoardEdge.Top, 2)]
        [TestCase(BoardEdge.Bottom, 2)]
        public void DepthCells_LShape_IsItsSpanAlongTheExit(BoardEdge edge, int expected)
        {
            var depth = GateExit.DepthCells(LShape, edge);

            Assert.AreEqual(expected, depth);
        }

        [Test]
        public void DepthCells_Horizontal1x2_IsTwoAcrossAndOneUpOrDown()
        {
            Assert.AreEqual(2, GateExit.DepthCells(Horizontal1x2, BoardEdge.Right));
            Assert.AreEqual(1, GateExit.DepthCells(Horizontal1x2, BoardEdge.Bottom));
        }

        [TestCase(BoardEdge.Top, 1f + Lip)]
        [TestCase(BoardEdge.Bottom, 1f)]
        [TestCase(BoardEdge.Left, 2f)]
        [TestCase(BoardEdge.Right, 2f)]
        public void PassCells_Horizontal1x2_IsItsDepthAndThroughTheTopTheTrailingLipToo(BoardEdge edge, float expected)
        {
            var pass = GateExit.PassCells(Horizontal1x2, edge, Lip);

            Assert.AreEqual(expected, pass, Tolerance);
        }

        [TestCase(BoardEdge.Top)]
        [TestCase(BoardEdge.Bottom)]
        [TestCase(BoardEdge.Left)]
        [TestCase(BoardEdge.Right)]
        public void MaskRect_EachExitEdge_SitsOnTheInnerLineThereAndPastTheGridElsewhere(BoardEdge edge)
        {
            const float Margin = 0.3f;

            var mask = GateExit.MaskRect(Width, Height, edge, Margin);

            Assert.AreEqual(edge == BoardEdge.Left ? 0f : -Margin, mask.xMin, Tolerance, "left");
            Assert.AreEqual(edge == BoardEdge.Right ? Width : Width + Margin, mask.xMax, Tolerance, "right");
            Assert.AreEqual(edge == BoardEdge.Bottom ? 0f : -Margin, mask.yMin, Tolerance, "bottom");
            Assert.AreEqual(edge == BoardEdge.Top ? Height : Height + Margin, mask.yMax, Tolerance, "top");
        }

        /// <summary>
        /// Each exit edge with a block on the border row or column beside it:
        /// right and left along the bottom row, top down the right column,
        /// bottom up the left column. The lifted block — its outline and lip,
        /// grown by the lift scale — must stay inside the clip on every side
        /// but the exit.
        /// </summary>
        [TestCase(BoardEdge.Right, 3, 0, false)]
        [TestCase(BoardEdge.Left, 0, 0, false)]
        [TestCase(BoardEdge.Top, 4, 2, true)]
        [TestCase(BoardEdge.Bottom, 0, 0, true)]
        public void MaskRect_LiftedBlockOnAnAdjacentBorderRow_KeepsItsLipAndOutlineInsideOnTheOtherSides(
            BoardEdge edge, int originX, int originY, bool isVertical)
        {
            var cells = isVertical ? Vertical1x2 : Horizontal1x2;
            var margin = GateExit.OverhangCells(cells, LiftScale, OutlineWidth, Lip);
            var lifted = LiftedBounds(cells, new Coord(originX, originY));

            var mask = GateExit.MaskRect(Width, Height, edge, margin);

            var hangsPastAnotherSide = (edge != BoardEdge.Left && lifted.xMin < 0f)
                                       || (edge != BoardEdge.Right && lifted.xMax > Width)
                                       || (edge != BoardEdge.Bottom && lifted.yMin < 0f)
                                       || (edge != BoardEdge.Top && lifted.yMax > Height);
            Assert.IsTrue(hangsPastAnotherSide, "the fixture's lifted block hangs past the grid beside its exit");
            if (edge != BoardEdge.Left)
            {
                Assert.GreaterOrEqual(lifted.xMin, mask.xMin - Tolerance, "left side kept");
            }

            if (edge != BoardEdge.Right)
            {
                Assert.LessOrEqual(lifted.xMax, mask.xMax + Tolerance, "right side kept");
            }

            if (edge != BoardEdge.Bottom)
            {
                Assert.GreaterOrEqual(lifted.yMin, mask.yMin - Tolerance, "bottom side, with the lip, kept");
            }

            if (edge != BoardEdge.Top)
            {
                Assert.LessOrEqual(lifted.yMax, mask.yMax + Tolerance, "top side kept");
            }
        }

        [TestCase(BoardEdge.Top)]
        [TestCase(BoardEdge.Bottom)]
        [TestCase(BoardEdge.Left)]
        [TestCase(BoardEdge.Right)]
        public void GlowRect_EachEdge_IsTheGatesSpanJustInsideItsInnerLine(BoardEdge edge)
        {
            const float Depth = 0.6f;

            var glow = GateExit.GlowRect(Width, Height, edge, 1, 2, Depth);

            var expected = edge == BoardEdge.Top ? new Rect(1f, Height - Depth, 2f, Depth)
                : edge == BoardEdge.Bottom ? new Rect(1f, 0f, 2f, Depth)
                : edge == BoardEdge.Left ? new Rect(0f, 1f, Depth, 2f)
                : new Rect(Width - Depth, 1f, Depth, 2f);
            AssertRect(expected, glow);
        }

        [TestCase(BoardEdge.Top)]
        [TestCase(BoardEdge.Bottom)]
        [TestCase(BoardEdge.Left)]
        [TestCase(BoardEdge.Right)]
        public void OuterLine_EachEdge_IsTheGridEdgePushedOutByTheFrameAcrossTheSpan(BoardEdge edge)
        {
            const float Thickness = 0.45f;

            GateExit.OuterLine(Width, Height, edge, 1, 2, Thickness, out var from, out var to);

            var expectedFrom = edge == BoardEdge.Top ? new Vector2(1f, Height + Thickness)
                : edge == BoardEdge.Bottom ? new Vector2(1f, -Thickness)
                : edge == BoardEdge.Left ? new Vector2(-Thickness, 1f)
                : new Vector2(Width + Thickness, 1f);
            var along = edge == BoardEdge.Top || edge == BoardEdge.Bottom ? Vector2.right : Vector2.up;
            Assert.AreEqual(expectedFrom.x, from.x, Tolerance);
            Assert.AreEqual(expectedFrom.y, from.y, Tolerance);
            Assert.AreEqual((expectedFrom + along * 2f).x, to.x, Tolerance);
            Assert.AreEqual((expectedFrom + along * 2f).y, to.y, Tolerance);
        }

        /// <summary>
        /// Where a lifted block at <paramref name="origin"/> reaches, in grid
        /// units: its outline quarters, which cover its face and lip, grown by
        /// the lift scale about the footprint's centre.
        /// </summary>
        private static Rect LiftedBounds(IReadOnlyList<Coord> cells, Coord origin)
        {
            var xMin = float.MaxValue;
            var yMin = float.MaxValue;
            var xMax = float.MinValue;
            var yMax = float.MinValue;
            foreach (var tile in BlockTiling.Compute(cells))
            {
                var rect = LiftOutline.QuarterRect(tile, OutlineWidth, Lip);
                xMin = Mathf.Min(xMin, rect.xMin);
                yMin = Mathf.Min(yMin, rect.yMin);
                xMax = Mathf.Max(xMax, rect.xMax);
                yMax = Mathf.Max(yMax, rect.yMax);
            }

            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;
            foreach (var cell in cells)
            {
                minX = Mathf.Min(minX, cell.X);
                minY = Mathf.Min(minY, cell.Y);
                maxX = Mathf.Max(maxX, cell.X);
                maxY = Mathf.Max(maxY, cell.Y);
            }

            var centre = new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);
            float Grow(float value, float about) => about + (value - about) * LiftScale;

            return Rect.MinMaxRect(
                origin.X + Grow(xMin, centre.x), origin.Y + Grow(yMin, centre.y),
                origin.X + Grow(xMax, centre.x), origin.Y + Grow(yMax, centre.y));
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.xMin, actual.xMin, Tolerance, "xMin");
            Assert.AreEqual(expected.yMin, actual.yMin, Tolerance, "yMin");
            Assert.AreEqual(expected.xMax, actual.xMax, Tolerance, "xMax");
            Assert.AreEqual(expected.yMax, actual.yMax, Tolerance, "yMax");
        }
    }
}
