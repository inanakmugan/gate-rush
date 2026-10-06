using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 20's <see cref="LayerInset"/>: each quarter of a layered
    /// block's inner shape moves in only on the sides that face out of the
    /// block, and an edge reaches along its run, so the quarters together
    /// cover exactly the footprint shrunk by the inset — one piece over a
    /// 2×2, and one piece through an L's bend.
    /// </summary>
    public class LayerInsetTests
    {
        private const float Inset = 0.1f;
        private const float Tolerance = 1e-5f;

        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Horizontal1x2 = { new Coord(0, 0), new Coord(1, 0) };
        private static readonly Coord[] Vertical1x2 = { new Coord(0, 0), new Coord(0, 1) };
        private static readonly Coord[] Square2x2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };
        private static readonly Coord[] LShape = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

        [Test]
        public void QuarterRect_Fill_IsUnchanged()
        {
            var tile = TileAt(Square2x2, new Coord(0, 0), Quarter.TopRight);

            var rect = LayerInset.QuarterRect(tile, Inset);

            Assert.AreEqual(QuarterPiece.Fill, tile.Piece);
            AssertRect(BlockTiling.QuarterRect(tile), rect);
        }

        [Test]
        public void QuarterRect_OuterCorner_MovesInOnBothOutwardSides()
        {
            var tile = TileAt(Single, new Coord(0, 0), Quarter.TopRight);

            var rect = LayerInset.QuarterRect(tile, Inset);

            Assert.AreEqual(QuarterPiece.OuterCorner, tile.Piece);
            AssertRect(Rect.MinMaxRect(0.5f, 0.5f, 1f - Inset, 1f - Inset), rect);
        }

        [Test]
        public void QuarterRect_EdgeAlongX_MovesInOnlyOnItsYSideAndReachesAlongItsRun()
        {
            var tile = TileAt(Horizontal1x2, new Coord(0, 0), Quarter.TopRight);

            var rect = LayerInset.QuarterRect(tile, Inset);

            // Its top faces out of the block and moves in; its right faces the
            // block's other cell and reaches into it; its inner sides stay.
            Assert.AreEqual(QuarterPiece.EdgeAlongX, tile.Piece);
            AssertRect(Rect.MinMaxRect(0.5f, 0.5f, 1f + Inset, 1f - Inset), rect);
        }

        [Test]
        public void QuarterRect_EdgeAlongY_MovesInOnlyOnItsXSideAndReachesAlongItsRun()
        {
            var tile = TileAt(Vertical1x2, new Coord(0, 0), Quarter.TopLeft);

            var rect = LayerInset.QuarterRect(tile, Inset);

            Assert.AreEqual(QuarterPiece.EdgeAlongY, tile.Piece);
            AssertRect(Rect.MinMaxRect(Inset, 0.5f, 0.5f, 1f + Inset), rect);
        }

        [Test]
        public void QuarterRect_Concave_MovesInFromItsCorner()
        {
            var tile = TileAt(LShape, new Coord(0, 0), Quarter.TopRight);

            var rect = LayerInset.QuarterRect(tile, Inset);

            Assert.AreEqual(QuarterPiece.ConcaveCorner, tile.Piece);
            AssertRect(Rect.MinMaxRect(0.5f, 0.5f, 1f - Inset, 1f - Inset), rect);
        }

        [Test]
        public void QuarterRect_ZeroInset_IsTheQuartersOwnRect()
        {
            var tiles = BlockTiling.Compute(LShape);

            var rects = tiles.Select(t => LayerInset.QuarterRect(t, 0f)).ToList();

            Assert.AreEqual(LShape.Length * 4, rects.Count);
            for (var i = 0; i < tiles.Count; i++)
            {
                AssertRect(BlockTiling.QuarterRect(tiles[i]), rects[i]);
            }
        }

        [Test]
        public void QuarterRects_TwoByTwo_CoverExactlyTheFootprintShrunkByTheInset()
        {
            var rects = InsetRects(Square2x2);

            var samples = AssertCoverExactlyTheShrunkFootprint(Square2x2, rects);

            // The central decision: one piece, not four. The middle of the
            // block, where its four cells meet, and the middle of each seam
            // between two cells are all covered.
            Assert.Greater(samples.Inside, 0, "the sample reaches the inner shape");
            Assert.Greater(samples.Outside, 0, "the sample reaches the rim and the block's surroundings");
            AssertCovered(rects, new Vector2(1f, 1f), "where the four cells meet");
            AssertCovered(rects, new Vector2(1f, 0.5f), "the seam between the bottom cells");
            AssertCovered(rects, new Vector2(0.5f, 1f), "the seam between the left cells");
            Assert.IsTrue(IsOneRegion(Square2x2, rects), "the inner shape is one connected region");
        }

        [Test]
        public void QuarterRects_LShape_CoverExactlyTheShrunkLAndAreConnectedThroughTheBend()
        {
            var rects = InsetRects(LShape);

            var samples = AssertCoverExactlyTheShrunkFootprint(LShape, rects);

            // Both sides of the bend: the strips a pulled-in concave corner
            // leaves beside it, which only the neighbouring edges' reach
            // covers. The square at the bend's corner itself is rim.
            Assert.Greater(samples.Inside, 0, "the sample reaches the inner shape");
            Assert.Greater(samples.Outside, 0, "the sample reaches the rim and the block's surroundings");
            AssertCovered(rects, new Vector2(1f - Inset * 0.5f, 0.75f), "the strip toward the right arm");
            AssertCovered(rects, new Vector2(0.75f, 1f - Inset * 0.5f), "the strip toward the upper arm");
            Assert.IsFalse(
                rects.Any(r => r.Contains(new Vector2(1f - Inset * 0.5f, 1f - Inset * 0.5f))),
                "the corner of the bend is rim, in the outer colour");
            Assert.IsTrue(IsOneRegion(LShape, rects), "the inner shape is one connected region");
        }

        private static List<Rect> InsetRects(IReadOnlyList<Coord> cells) =>
            BlockTiling.Compute(cells).Select(t => LayerInset.QuarterRect(t, Inset)).ToList();

        /// <summary>
        /// Samples the footprint's bounding box and its surroundings at the
        /// middles of an even number of steps — which never land on a cell's
        /// side, its middle, or an inset line — and asserts that a point lies in
        /// some rectangle exactly when it is in the footprint shrunk by the
        /// inset: at least the inset from everything outside the footprint.
        /// </summary>
        /// <returns>How many samples fell inside the inner shape and how many outside it.</returns>
        private static (int Inside, int Outside) AssertCoverExactlyTheShrunkFootprint(Coord[] cells, List<Rect> rects)
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
                    var expected = IsInShrunkFootprint(cells, point);
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
        /// True when the square reaching the inset from <paramref name="point"/>
        /// on every side lies wholly on the footprint: every cell one of its
        /// corners falls in is a footprint cell. The inset is below half a
        /// cell, so the corners are enough.
        /// </summary>
        private static bool IsInShrunkFootprint(Coord[] cells, Vector2 point)
        {
            foreach (var dx in new[] { -Inset, Inset })
            {
                foreach (var dy in new[] { -Inset, Inset })
                {
                    var cell = new Coord(Mathf.FloorToInt(point.x + dx), Mathf.FloorToInt(point.y + dy));
                    if (!cells.Contains(cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// True when the covered points of a fine grid over the footprint form
        /// one 4-connected region: a flood fill from the first covered point
        /// reaches every other.
        /// </summary>
        private static bool IsOneRegion(Coord[] cells, List<Rect> rects)
        {
            const int PerCell = 24;
            var width = (cells.Max(c => c.X) + 1) * PerCell;
            var height = (cells.Max(c => c.Y) + 1) * PerCell;
            var covered = new bool[width, height];
            var total = 0;
            var start = (X: -1, Y: -1);

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var point = new Vector2((x + 0.5f) / PerCell, (y + 0.5f) / PerCell);
                    covered[x, y] = rects.Any(r => r.Contains(point));
                    if (covered[x, y])
                    {
                        total++;
                        start = (x, y);
                    }
                }
            }

            Assert.Greater(total, 0, "the inner shape covers something");

            var reached = 0;
            var pending = new Stack<(int X, int Y)>();
            pending.Push(start);
            covered[start.X, start.Y] = false;
            while (pending.Count > 0)
            {
                var at = pending.Pop();
                reached++;
                foreach (var step in new[] { (X: 1, Y: 0), (X: -1, Y: 0), (X: 0, Y: 1), (X: 0, Y: -1) })
                {
                    var x = at.X + step.X;
                    var y = at.Y + step.Y;
                    if (x >= 0 && x < width && y >= 0 && y < height && covered[x, y])
                    {
                        covered[x, y] = false;
                        pending.Push((x, y));
                    }
                }
            }

            return reached == total;
        }

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
