using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="FaceShape"/>: a block's body is its footprint's
    /// quarters; a shape set into it is that, shrunk by the inset on every
    /// outer side, and its quarters together cover exactly that shape, stay
    /// inside the footprint's cells and stay one piece round a bend; a
    /// highlight's quarters keep to their own span along a straight run; and a
    /// floor shadow's quarters widen only on the sides that face out.
    /// </summary>
    public class FaceShapeTests
    {
        private const float Inset = 0.1f;
        private const float Margin = 0.12f;
        private const float Tolerance = 1e-5f;

        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Horizontal1x2 = { new Coord(0, 0), new Coord(1, 0) };
        private static readonly Coord[] Vertical1x2 = { new Coord(0, 0), new Coord(0, 1) };
        private static readonly Coord[] Square2x2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };
        private static readonly Coord[] LShape = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

        /// <summary>An L whose arm hangs over nothing: its bend opens downward, at the lower right of cell (0, 1).</summary>
        private static readonly Coord[] HangingL = { new Coord(0, 0), new Coord(0, 1), new Coord(1, 1) };

        private static readonly Coord[][] Fixtures = { Single, Horizontal1x2, Vertical1x2, Square2x2, LShape, HangingL };

        [Test]
        public void QuarterRect_Fill_IsUnchanged()
        {
            var tile = TileAt(Square2x2, new Coord(0, 0), Quarter.TopRight);

            var rect = FaceShape.QuarterRect(tile, Inset);

            Assert.AreEqual(QuarterPiece.Fill, tile.Piece);
            AssertRect(BlockTiling.QuarterRect(tile), rect);
        }

        [Test]
        public void QuarterRect_EveryQuarterWithoutInset_IsItsOwnQuarter()
        {
            var compared = 0;
            foreach (var cells in Fixtures)
            {
                foreach (var tile in BlockTiling.Compute(cells))
                {
                    AssertRect(BlockTiling.QuarterRect(tile), FaceShape.QuarterRect(tile, 0f));
                    compared++;
                }
            }

            Assert.AreEqual(Fixtures.Sum(cells => cells.Length) * 4, compared);
        }

        [Test]
        public void QuarterRect_OuterCornerWithInset_MovesInByTheInsetOnBothOutwardSides()
        {
            var tile = TileAt(Single, new Coord(0, 0), Quarter.BottomRight);

            var rect = FaceShape.QuarterRect(tile, Inset);

            AssertRect(Rect.MinMaxRect(0.5f, Inset, 1f - Inset, 0.5f), rect);
        }

        [Test]
        public void QuarterRect_EdgeAlongYWithInset_MovesInOnItsSideAndReachesOnAlongItsRun()
        {
            var tile = TileAt(Vertical1x2, new Coord(0, 0), Quarter.TopLeft);

            var rect = FaceShape.QuarterRect(tile, Inset);

            // Its left faces out of the block sideways and moves in; its top
            // faces the block's other cell and reaches into it, where a
            // pulled-in concave corner there would leave a strip.
            Assert.AreEqual(QuarterPiece.EdgeAlongY, tile.Piece);
            AssertRect(Rect.MinMaxRect(Inset, 0.5f, 0.5f, 1f + Inset), rect);
        }

        [Test]
        public void QuarterRects_EveryFixture_StayInsideTheFootprintsCells()
        {
            // Just inside each corner of a rectangle: a rectangle may end on a
            // cell's side, and that side belongs to the next cell.
            const float Nudge = 1e-3f;
            var checkedRects = 0;

            foreach (var cells in Fixtures)
            {
                foreach (var tile in BlockTiling.Compute(cells))
                {
                    var rect = FaceShape.QuarterRect(tile, Inset);

                    Assert.Greater(rect.width, 0f, $"{tile} keeps a width");
                    Assert.Greater(rect.height, 0f, $"{tile} keeps a height");
                    foreach (var x in new[] { rect.xMin + Nudge, rect.xMax - Nudge })
                    {
                        foreach (var y in new[] { rect.yMin + Nudge, rect.yMax - Nudge })
                        {
                            Assert.IsTrue(IsInFootprint(cells, new Vector2(x, y)), $"{tile}: ({x}, {y}) is outside the footprint");
                        }
                    }

                    checkedRects++;
                }
            }

            Assert.AreEqual(Fixtures.Sum(cells => cells.Length) * 4, checkedRects);
        }

        [Test]
        public void QuarterRects_HangingLWithInset_CoverExactlyTheBodyShrunkByTheInset()
        {
            var rects = Rects(HangingL, Inset);

            var samples = AssertCoverExactlyTheBody(HangingL, rects, Inset);

            Assert.Greater(samples.Inside, 0, "the sample reaches the inner shape");
            Assert.Greater(samples.Outside, 0, "the sample reaches the rim and the block's surroundings");
            AssertCovered(rects, new Vector2(0.75f, 1f + Inset * 0.5f), "under the bend's concave corner");
            AssertCovered(rects, new Vector2(1f, 1.5f), "through the bend, where the column meets the arm");
        }

        [Test]
        public void QuarterRects_LShapeWithInset_CoverExactlyTheBodyShrunkByTheInset()
        {
            var rects = Rects(LShape, Inset);

            var samples = AssertCoverExactlyTheBody(LShape, rects, Inset);

            Assert.Greater(samples.Inside, 0, "the sample reaches the inner shape");
            Assert.Greater(samples.Outside, 0, "the sample reaches the rim and the block's surroundings");
        }

        [Test]
        public void LightQuarterRect_EdgeOnAStraightRun_KeepsToItsOwnQuarterAlongIt()
        {
            // The left cell's top-right edge runs on into the right cell's
            // top-left edge: the highlight stops at the cells' boundary
            // instead of overlapping its neighbour by the inset.
            var tiles = BlockTiling.Compute(Horizontal1x2);
            var tile = TileAt(Horizontal1x2, new Coord(0, 0), Quarter.TopRight);
            var body = FaceShape.QuarterRect(tile, Inset);

            var light = FaceShape.LightQuarterRect(tile, tiles, Inset);

            AssertRect(Rect.MinMaxRect(0.5f, body.yMin, 1f, body.yMax), light);
            Assert.AreEqual(1f + Inset, body.xMax, Tolerance, "the body itself reaches on by the inset");
        }

        [Test]
        public void LightQuarterRect_EdgeBesideAConcaveCorner_KeepsTheBodysReach()
        {
            // The foot's top-left edge runs into the inside corner of the L,
            // which is pulled in: the reach covers the strip between them.
            var tiles = BlockTiling.Compute(LShape);
            var tile = TileAt(LShape, new Coord(1, 0), Quarter.TopLeft);

            var light = FaceShape.LightQuarterRect(tile, tiles, Inset);

            Assert.AreEqual(QuarterPiece.EdgeAlongX, tile.Piece);
            AssertRect(FaceShape.QuarterRect(tile, Inset), light);
        }

        [Test]
        public void LightQuarterRect_SideOnAStraightRun_KeepsToItsOwnQuarterUpIt()
        {
            // The bottom cell's top-right side runs on into the top cell's
            // bottom-right side: the body reaches up by the inset, the
            // highlight stops at the cells' boundary.
            var tiles = BlockTiling.Compute(Vertical1x2);
            var tile = TileAt(Vertical1x2, new Coord(0, 0), Quarter.TopRight);
            var body = FaceShape.QuarterRect(tile, Inset);

            var light = FaceShape.LightQuarterRect(tile, tiles, Inset);

            Assert.AreEqual(QuarterPiece.EdgeAlongY, tile.Piece);
            AssertRect(Rect.MinMaxRect(body.xMin, 0.5f, body.xMax, 1f), light);
            Assert.AreEqual(1f + Inset, body.yMax, Tolerance, "the body itself reaches on by the inset");
        }

        [Test]
        public void LightQuarterRect_SideBelowAConcaveCorner_KeepsTheBodysReach()
        {
            // The bar's right side runs up into the bend that opens downward:
            // the reach covers the strip below the pulled-in corner.
            var tiles = BlockTiling.Compute(HangingL);
            var tile = TileAt(HangingL, new Coord(0, 0), Quarter.TopRight);

            var light = FaceShape.LightQuarterRect(tile, tiles, Inset);

            Assert.AreEqual(QuarterPiece.EdgeAlongY, tile.Piece);
            AssertRect(FaceShape.QuarterRect(tile, Inset), light);
        }

        [Test]
        public void ShadowQuarterRect_OuterCorner_WidensOnBothOutwardSides()
        {
            var tile = TileAt(Single, new Coord(0, 0), Quarter.BottomRight);

            var rect = FaceShape.ShadowQuarterRect(BlockTiling.QuarterRect(tile), tile, Margin);

            AssertRect(Rect.MinMaxRect(0.5f, -Margin, 1f + Margin, 0.5f), rect);
        }

        [Test]
        public void ShadowQuarterRect_Edges_WidenOnlyOnTheirOutwardSide()
        {
            var alongX = TileAt(Horizontal1x2, new Coord(0, 0), Quarter.TopRight);
            var alongY = TileAt(Vertical1x2, new Coord(0, 0), Quarter.TopLeft);

            var acrossX = FaceShape.ShadowQuarterRect(BlockTiling.QuarterRect(alongX), alongX, Margin);
            var acrossY = FaceShape.ShadowQuarterRect(BlockTiling.QuarterRect(alongY), alongY, Margin);

            Assert.AreEqual(QuarterPiece.EdgeAlongX, alongX.Piece);
            AssertRect(Rect.MinMaxRect(0.5f, 0.5f, 1f, 1f + Margin), acrossX);
            Assert.AreEqual(QuarterPiece.EdgeAlongY, alongY.Piece);
            AssertRect(Rect.MinMaxRect(-Margin, 0.5f, 0.5f, 1f), acrossY);
        }

        [Test]
        public void ShadowQuarterRect_ConcaveCornerAndFill_AreUnchanged()
        {
            var concave = TileAt(LShape, new Coord(0, 0), Quarter.TopRight);
            var fill = TileAt(Square2x2, new Coord(0, 0), Quarter.TopRight);

            Assert.AreEqual(QuarterPiece.ConcaveCorner, concave.Piece);
            AssertRect(BlockTiling.QuarterRect(concave), FaceShape.ShadowQuarterRect(BlockTiling.QuarterRect(concave), concave, Margin));
            Assert.AreEqual(QuarterPiece.Fill, fill.Piece);
            AssertRect(BlockTiling.QuarterRect(fill), FaceShape.ShadowQuarterRect(BlockTiling.QuarterRect(fill), fill, Margin));
        }

        private static List<Rect> Rects(IReadOnlyList<Coord> cells, float inset) =>
            BlockTiling.Compute(cells).Select(t => FaceShape.QuarterRect(t, inset)).ToList();

        /// <summary>
        /// Samples the footprint's bounding box and its surroundings at the
        /// middles of an even number of steps — which never land on a cell's
        /// side, its middle or an inset line — and asserts that a point lies
        /// in some rectangle exactly when it is on the body shrunk by
        /// <paramref name="inset"/> (<see cref="IsOnBody"/>).
        /// </summary>
        /// <returns>How many samples fell on the shape and how many off it.</returns>
        private static (int Inside, int Outside) AssertCoverExactlyTheBody(Coord[] cells, List<Rect> rects, float inset)
        {
            const int Steps = 96;
            const float From = -0.5f;
            const float Span = 3f;
            var inside = 0;
            var outside = 0;

            for (var i = 0; i < Steps; i++)
            {
                for (var j = 0; j < Steps; j++)
                {
                    var point = new Vector2(From + Span * (i + 0.5f) / Steps, From + Span * (j + 0.5f) / Steps);
                    var expected = IsOnBody(cells, point, inset);
                    if (expected)
                    {
                        inside++;
                    }
                    else
                    {
                        outside++;
                    }

                    Assert.AreEqual(expected, rects.Any(r => r.Contains(point)), $"{point}");
                }
            }

            return (inside, outside);
        }

        /// <summary>
        /// True when the footprint reaches <paramref name="inset"/> from
        /// <paramref name="point"/> on every side: every cell a corner of that
        /// box falls in is a footprint cell. The inset is below half a cell,
        /// so the corners are enough.
        /// </summary>
        private static bool IsOnBody(Coord[] cells, Vector2 point, float inset)
        {
            foreach (var dx in new[] { -inset, inset })
            {
                foreach (var dy in new[] { -inset, inset })
                {
                    if (!IsInFootprint(cells, new Vector2(point.x + dx, point.y + dy)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsInFootprint(Coord[] cells, Vector2 point) =>
            cells.Contains(new Coord(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y)));

        private static void AssertCovered(List<Rect> rects, Vector2 point, string what)
        {
            Assert.IsTrue(rects.Any(r => r.Contains(point)), $"{what}, at {point}, is covered");
        }

        private static QuarterTile TileAt(IReadOnlyList<Coord> cells, Coord cell, Quarter corner) =>
            BlockTiling.Compute(cells).Single(t => t.Cell == cell && t.Corner == corner);

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.xMin, actual.xMin, Tolerance, "xMin");
            Assert.AreEqual(expected.yMin, actual.yMin, Tolerance, "yMin");
            Assert.AreEqual(expected.xMax, actual.xMax, Tolerance, "xMax");
            Assert.AreEqual(expected.yMax, actual.yMax, Tolerance, "yMax");
        }
    }
}
