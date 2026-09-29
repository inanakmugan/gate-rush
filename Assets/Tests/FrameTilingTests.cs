using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="FrameTiling"/>: the ring around the grid and every wall
    /// cell are tiled as one shape, so walls join the frame and notches round
    /// inward; gates are holes in it; and the ring is squeezed to the frame's
    /// thickness (Module 15).
    /// </summary>
    public class FrameTilingTests
    {
        private const float Thickness = 0.4f;
        private const float Tolerance = 1e-5f;

        private static Coord C(int x, int y) => new Coord(x, y);

        private static QuarterPiece PieceAt(IReadOnlyList<QuarterTile> tiles, Coord cell, Quarter corner) =>
            tiles.Single(t => t.Cell == cell && t.Corner == corner).Piece;

        [Test]
        public void Compute_RectangularBoardWithoutWalls_FramesAllFourSidesWithOuterCornersAtTheBoardCorners()
        {
            var ctx = Fixture.Ctx(3, 2);

            var tiles = FrameTiling.Compute(ctx);

            var cells = new HashSet<Coord>(tiles.Select(t => t.Cell));
            for (var x = -1; x <= 3; x++)
            {
                Assert.IsTrue(cells.Contains(C(x, -1)), $"bottom ring cell ({x}, -1)");
                Assert.IsTrue(cells.Contains(C(x, 2)), $"top ring cell ({x}, 2)");
            }

            for (var y = -1; y <= 2; y++)
            {
                Assert.IsTrue(cells.Contains(C(-1, y)), $"left ring cell (-1, {y})");
                Assert.IsTrue(cells.Contains(C(3, y)), $"right ring cell (3, {y})");
            }

            Assert.IsFalse(cells.Any(ctx.IsInsideGrid), "no playable cell is frame");
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(-1, -1), Quarter.BottomLeft));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(3, -1), Quarter.BottomRight));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(-1, 2), Quarter.TopLeft));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(3, 2), Quarter.TopRight));
            Assert.AreEqual(4, tiles.Count(t => t.Piece == QuarterPiece.OuterCorner), "only the four board corners are outer corners");
            Assert.AreEqual(QuarterPiece.ConcaveCorner, PieceAt(tiles, C(-1, -1), Quarter.TopRight));
            Assert.AreEqual(QuarterPiece.ConcaveCorner, PieceAt(tiles, C(3, 2), Quarter.BottomLeft));
            Assert.AreEqual(4, tiles.Count(t => t.Piece == QuarterPiece.ConcaveCorner), "the ring's four inside corners are concave");
        }

        [Test]
        public void Compute_WallCellInsideTheBoard_IsFramedAsAPieceOfItsOwn()
        {
            var ctx = Fixture.Ctx(4, 4, staticWalls: new[] { C(1, 1) });

            var tiles = FrameTiling.Compute(ctx);

            var wall = tiles.Where(t => t.Cell == C(1, 1)).ToList();
            Assert.AreEqual(4, wall.Count);
            Assert.IsTrue(wall.All(t => t.Piece == QuarterPiece.OuterCorner));
        }

        [Test]
        public void Compute_WallCellTouchingTheEdge_JoinsTheFrame()
        {
            var ctx = Fixture.Ctx(4, 3, staticWalls: new[] { C(0, 1) });

            var tiles = FrameTiling.Compute(ctx);

            Assert.AreEqual(QuarterPiece.EdgeAlongX, PieceAt(tiles, C(0, 1), Quarter.BottomLeft), "the wall runs on from the frame");
            Assert.AreEqual(QuarterPiece.EdgeAlongX, PieceAt(tiles, C(0, 1), Quarter.TopLeft), "the wall runs on from the frame");
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 1), Quarter.BottomRight));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 1), Quarter.TopRight));
            Assert.AreEqual(QuarterPiece.ConcaveCorner, PieceAt(tiles, C(-1, 1), Quarter.BottomRight), "the frame bends into the wall below it");
            Assert.AreEqual(QuarterPiece.ConcaveCorner, PieceAt(tiles, C(-1, 1), Quarter.TopRight), "the frame bends into the wall above it");
        }

        [Test]
        public void Compute_NotchInTheBoardsOutline_ProducesAConcaveCornerInTheFrame()
        {
            var ctx = Fixture.Ctx(3, 3, staticWalls: new[] { C(0, 0) });

            var tiles = FrameTiling.Compute(ctx);

            Assert.AreEqual(QuarterPiece.ConcaveCorner, PieceAt(tiles, C(-1, 0), Quarter.TopRight));
            Assert.AreEqual(QuarterPiece.ConcaveCorner, PieceAt(tiles, C(0, -1), Quarter.TopRight));
            Assert.AreEqual(QuarterPiece.Fill, PieceAt(tiles, C(-1, -1), Quarter.TopRight), "the notch fills the ring's corner");
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 0), Quarter.TopRight), "the notch's own corner faces the board");
        }

        [Test]
        public void Compute_Gate_LeavesItsSpanOutAndTheFrameEndsInOuterCornersBesideIt()
        {
            var ctx = Fixture.Ctx(3, 2, gates: new[] { Fixture.Gate(0, BoardEdge.Bottom, 1, 1, BlockColor.Red) });

            var tiles = FrameTiling.Compute(ctx);

            CollectionAssert.AreEqual(new[] { C(1, -1) }, FrameTiling.GateCells(ctx, 0));
            Assert.IsFalse(tiles.Any(t => t.Cell == C(1, -1)), "the gate's ring cell is not frame");
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, -1), Quarter.BottomRight));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, -1), Quarter.TopRight));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(2, -1), Quarter.BottomLeft));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(2, -1), Quarter.TopLeft));
        }

        [Test]
        public void QuarterRect_RingQuarter_IsHalfTheThicknessAcrossTheEdgeAndHalfACellAlongIt()
        {
            var tile = new QuarterTile(C(-1, 0), Quarter.TopRight, QuarterPiece.EdgeAlongY);

            var rect = FrameTiling.QuarterRect(tile, 3, 2, Thickness);

            AssertRect(-Thickness * 0.5f, 0.5f, 0f, 1f, rect);
        }

        [Test]
        public void QuarterRect_RingCorner_IsSqueezedOnBothAxes()
        {
            var tile = new QuarterTile(C(3, 2), Quarter.TopRight, QuarterPiece.OuterCorner);

            var rect = FrameTiling.QuarterRect(tile, 3, 2, Thickness);

            AssertRect(3f + Thickness * 0.5f, 2f + Thickness * 0.5f, 3f + Thickness, 2f + Thickness, rect);
        }

        [Test]
        public void QuarterRect_CellInsideTheGrid_MatchesTheBlockQuarter()
        {
            var tile = new QuarterTile(C(1, 1), Quarter.BottomLeft, QuarterPiece.OuterCorner);

            var rect = FrameTiling.QuarterRect(tile, 3, 2, Thickness);

            Assert.AreEqual(BlockTiling.QuarterRect(tile), rect);
        }

        [Test]
        public void QuarterRect_CellBeyondTheRing_Throws()
        {
            var tile = new QuarterTile(C(-2, 0), Quarter.TopRight, QuarterPiece.OuterCorner);

            Assert.Throws<ArgumentOutOfRangeException>(() => FrameTiling.QuarterRect(tile, 3, 2, Thickness));
        }

        [TestCase(BoardEdge.Bottom)]
        [TestCase(BoardEdge.Top)]
        [TestCase(BoardEdge.Left)]
        [TestCase(BoardEdge.Right)]
        public void EdgeSpanRect_OfAGate_CoversExactlyItsQuarters(BoardEdge edge)
        {
            var ctx = Fixture.Ctx(4, 3, gates: new[] { Fixture.Gate(0, edge, 1, 2, BlockColor.Red) });
            var quarters = BlockTiling.Compute(FrameTiling.GateCells(ctx, 0))
                .Select(t => FrameTiling.QuarterRect(t, ctx.Width, ctx.Height, Thickness))
                .ToList();

            var span = FrameTiling.EdgeSpanRect(ctx.Width, ctx.Height, edge, 1, 2, Thickness);

            AssertRect(
                quarters.Min(r => r.xMin), quarters.Min(r => r.yMin), quarters.Max(r => r.xMax), quarters.Max(r => r.yMax),
                span);
            Assert.AreEqual(span.width * span.height, quarters.Sum(r => r.width * r.height), Tolerance, "the quarters tile the span");
        }

        private static void AssertRect(float xMin, float yMin, float xMax, float yMax, Rect actual)
        {
            Assert.AreEqual(xMin, actual.xMin, Tolerance, "xMin");
            Assert.AreEqual(yMin, actual.yMin, Tolerance, "yMin");
            Assert.AreEqual(xMax, actual.xMax, Tolerance, "xMax");
            Assert.AreEqual(yMax, actual.yMax, Tolerance, "yMax");
        }
    }
}
