using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="FaceShape"/>: a block's face is its footprint's
    /// quarters with only the sides that face down out of the block raised by
    /// the lip, so the quarters together cover exactly the footprint less a
    /// lip-tall strip along its lower outline, stay inside the footprint's
    /// cells, and stay one piece round a bend that opens downward; and a shape
    /// set into the face is that, shrunk by the inset on every outer side.
    /// </summary>
    public class FaceShapeTests
    {
        private const float Inset = 0.1f;
        private const float Lip = 0.09f;
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

            var rect = FaceShape.QuarterRect(tile, Inset, Lip);

            Assert.AreEqual(QuarterPiece.Fill, tile.Piece);
            AssertRect(BlockTiling.QuarterRect(tile), rect);
        }

        [Test]
        public void QuarterRect_TopOuterCorner_IsUnchangedWithoutInset()
        {
            var tile = TileAt(Single, new Coord(0, 0), Quarter.TopRight);

            var rect = FaceShape.QuarterRect(tile, 0f, Lip);

            Assert.AreEqual(QuarterPiece.OuterCorner, tile.Piece);
            AssertRect(BlockTiling.QuarterRect(tile), rect);
        }

        [Test]
        public void QuarterRect_BottomOuterCorner_RisesByTheLipOnItsBottomSideOnly()
        {
            var tile = TileAt(Single, new Coord(0, 0), Quarter.BottomRight);

            var rect = FaceShape.QuarterRect(tile, 0f, Lip);

            Assert.AreEqual(QuarterPiece.OuterCorner, tile.Piece);
            AssertRect(Rect.MinMaxRect(0.5f, Lip, 1f, 0.5f), rect);
        }

        [Test]
        public void QuarterRect_BottomEdgeAlongX_RisesByTheLip()
        {
            var tile = TileAt(Horizontal1x2, new Coord(1, 0), Quarter.BottomLeft);

            var rect = FaceShape.QuarterRect(tile, 0f, Lip);

            Assert.AreEqual(QuarterPiece.EdgeAlongX, tile.Piece);
            AssertRect(Rect.MinMaxRect(1f, Lip, 1.5f, 0.5f), rect);
        }

        [Test]
        public void QuarterRect_TopEdgeAlongY_ReachesUpByTheLip()
        {
            var tile = TileAt(Vertical1x2, new Coord(0, 0), Quarter.TopLeft);

            var rect = FaceShape.QuarterRect(tile, 0f, Lip);

            // Its left faces out of the block sideways and stays; its top
            // faces the block's other cell and reaches into it, under where a
            // raised concave corner there would leave a strip.
            Assert.AreEqual(QuarterPiece.EdgeAlongY, tile.Piece);
            AssertRect(Rect.MinMaxRect(0f, 0.5f, 0.5f, 1f + Lip), rect);
        }

        [Test]
        public void QuarterRect_BottomEdgeAlongY_IsUnchangedWithoutInset()
        {
            var tile = TileAt(Vertical1x2, new Coord(0, 1), Quarter.BottomLeft);

            var rect = FaceShape.QuarterRect(tile, 0f, Lip);

            Assert.AreEqual(QuarterPiece.EdgeAlongY, tile.Piece);
            AssertRect(BlockTiling.QuarterRect(tile), rect);
        }

        [Test]
        public void QuarterRect_BottomConcave_RisesByTheLipAndKeepsItsXSide()
        {
            var tile = TileAt(HangingL, new Coord(0, 1), Quarter.BottomRight);

            var rect = FaceShape.QuarterRect(tile, 0f, Lip);

            Assert.AreEqual(QuarterPiece.ConcaveCorner, tile.Piece);
            AssertRect(Rect.MinMaxRect(0.5f, 1f + Lip, 1f, 1.5f), rect);
        }

        [Test]
        public void QuarterRect_BottomOuterCornerWithInset_MovesInByTheInsetAndOnItsBottomByTheLipToo()
        {
            var tile = TileAt(Single, new Coord(0, 0), Quarter.BottomRight);

            var rect = FaceShape.QuarterRect(tile, Inset, Lip);

            AssertRect(Rect.MinMaxRect(0.5f, Inset + Lip, 1f - Inset, 0.5f), rect);
        }

        [Test]
        public void QuarterRect_ZeroLip_IsLayerInsetsRect()
        {
            var compared = 0;
            foreach (var cells in Fixtures)
            {
                foreach (var tile in BlockTiling.Compute(cells))
                {
                    var rect = FaceShape.QuarterRect(tile, Inset, 0f);

                    AssertRect(LayerInset.QuarterRect(tile, Inset), rect);
                    compared++;
                }
            }

            Assert.AreEqual(Fixtures.Sum(cells => cells.Length) * 4, compared);
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
                    var rect = FaceShape.QuarterRect(tile, 0f, Lip);

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
        public void QuarterRects_HangingL_CoverExactlyTheFootprintLessItsLipStrip()
        {
            var rects = Rects(HangingL, 0f);

            var samples = AssertCoverExactlyTheFace(HangingL, rects, 0f);

            // The strip under the raised concave corner, which only the reach
            // of the edge below covers; and the lip's own strips, under the
            // column and under the hanging arm, which nothing may cover.
            Assert.Greater(samples.Inside, 0, "the sample reaches the face");
            Assert.Greater(samples.Outside, 0, "the sample reaches the lip strip and the block's surroundings");
            AssertCovered(rects, new Vector2(0.75f, 1f + Lip * 0.5f), "under the bend's concave corner");
            Assert.IsFalse(rects.Any(r => r.Contains(new Vector2(0.5f, Lip * 0.5f))), "the lip strip under the column");
            Assert.IsFalse(rects.Any(r => r.Contains(new Vector2(1.5f, 1f + Lip * 0.5f))), "the lip strip under the hanging arm");
        }

        [Test]
        public void QuarterRects_HangingLWithInset_CoverExactlyTheFaceShrunkByTheInset()
        {
            var rects = Rects(HangingL, Inset);

            var samples = AssertCoverExactlyTheFace(HangingL, rects, Inset);

            Assert.Greater(samples.Inside, 0, "the sample reaches the inner shape");
            Assert.Greater(samples.Outside, 0, "the sample reaches the rim, the lip strip and the block's surroundings");
            AssertCovered(rects, new Vector2(0.75f, 1f + (Inset + Lip) * 0.5f), "under the bend's concave corner");
            AssertCovered(rects, new Vector2(1f, 1.5f), "through the bend, where the column meets the arm");
        }

        [Test]
        public void QuarterRects_LShapeWithInset_CoverExactlyTheFaceShrunkByTheInset()
        {
            var rects = Rects(LShape, Inset);

            var samples = AssertCoverExactlyTheFace(LShape, rects, Inset);

            Assert.Greater(samples.Inside, 0, "the sample reaches the inner shape");
            Assert.Greater(samples.Outside, 0, "the sample reaches the rim, the lip strip and the block's surroundings");
        }

        [Test]
        public void ContentRise_IsHalfTheLip()
        {
            var rise = FaceShape.ContentRise(Lip);

            // Centred on a one-cell face: as far from the face's bottom, the
            // lip's top, as from the cell's top.
            Assert.AreEqual(0.5f + rise - Lip, 1f - (0.5f + rise), Tolerance);
        }

        private static List<Rect> Rects(IReadOnlyList<Coord> cells, float inset) =>
            BlockTiling.Compute(cells).Select(t => FaceShape.QuarterRect(t, inset, Lip)).ToList();

        /// <summary>
        /// Samples the footprint's bounding box and its surroundings at the
        /// middles of an even number of steps — which never land on a cell's
        /// side, its middle, an inset line or a lip line — and asserts that a
        /// point lies in some rectangle exactly when it is on the face shrunk
        /// by <paramref name="inset"/> (<see cref="IsOnFace"/>).
        /// </summary>
        /// <returns>How many samples fell on the shape and how many off it.</returns>
        private static (int Inside, int Outside) AssertCoverExactlyTheFace(Coord[] cells, List<Rect> rects, float inset)
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
                    var expected = IsOnFace(cells, point, inset);
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
        /// <paramref name="point"/> to each side and above, and
        /// <paramref name="inset"/> plus the lip below: every cell a corner of
        /// that box falls in is a footprint cell. Inset and lip together are
        /// below half a cell, so the corners are enough.
        /// </summary>
        private static bool IsOnFace(Coord[] cells, Vector2 point, float inset)
        {
            foreach (var dx in new[] { -inset, inset })
            {
                foreach (var dy in new[] { -(inset + Lip), inset })
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
