using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 18's <see cref="LiftOutline"/>: each outline quarter grows
    /// only on the sides that face out of the block — a bottom side by the lip
    /// too — so the quarters together cover a band round the whole footprint,
    /// the inside of an L's bend included.
    /// </summary>
    public class LiftOutlineTests
    {
        private const float Width = 0.07f;
        private const float Lip = 0.09f;

        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Horizontal1x2 = { new Coord(0, 0), new Coord(1, 0) };
        private static readonly Coord[] Vertical1x2 = { new Coord(0, 0), new Coord(0, 1) };
        private static readonly Coord[] Square2x2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };
        private static readonly Coord[] LShape = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

        [Test]
        public void QuarterRect_OuterCorner_GrowsOnBothOuterSides()
        {
            var tile = TileAt(Single, new Coord(0, 0), Quarter.TopRight);

            var rect = LiftOutline.QuarterRect(tile, Width, Lip);

            Assert.AreEqual(QuarterPiece.OuterCorner, tile.Piece);
            AssertRect(new Rect(0.5f, 0.5f, 0.5f + Width, 0.5f + Width), rect);
        }

        [Test]
        public void QuarterRect_EdgeAlongX_GrowsOnlyOnItsYSide()
        {
            var tile = TileAt(Horizontal1x2, new Coord(0, 0), Quarter.TopRight);

            var rect = LiftOutline.QuarterRect(tile, Width, Lip);

            Assert.AreEqual(QuarterPiece.EdgeAlongX, tile.Piece);
            AssertRect(new Rect(0.5f, 0.5f, 0.5f, 0.5f + Width), rect);
        }

        [Test]
        public void QuarterRect_EdgeAlongY_GrowsOnlyOnItsXSide()
        {
            var tile = TileAt(Vertical1x2, new Coord(0, 0), Quarter.TopLeft);

            var rect = LiftOutline.QuarterRect(tile, Width, Lip);

            Assert.AreEqual(QuarterPiece.EdgeAlongY, tile.Piece);
            AssertRect(new Rect(-Width, 0.5f, 0.5f + Width, 0.5f), rect);
        }

        [Test]
        public void QuarterRect_Fill_DoesNotGrow()
        {
            var tile = TileAt(Square2x2, new Coord(0, 0), Quarter.TopRight);

            var rect = LiftOutline.QuarterRect(tile, Width, Lip);

            Assert.AreEqual(QuarterPiece.Fill, tile.Piece);
            AssertRect(BlockTiling.QuarterRect(tile), rect);
        }

        [Test]
        public void QuarterRect_Concave_GrowsTowardItsCorner()
        {
            var tile = TileAt(LShape, new Coord(0, 0), Quarter.TopRight);

            var rect = LiftOutline.QuarterRect(tile, Width, Lip);

            Assert.AreEqual(QuarterPiece.ConcaveCorner, tile.Piece);
            AssertRect(new Rect(0.5f, 0.5f, 0.5f + Width, 0.5f + Width), rect);
        }

        [Test]
        public void QuarterRect_BottomSide_GrowsByTheLipToo()
        {
            var tile = TileAt(Horizontal1x2, new Coord(1, 0), Quarter.BottomLeft);

            var rect = LiftOutline.QuarterRect(tile, Width, Lip);

            Assert.AreEqual(QuarterPiece.EdgeAlongX, tile.Piece);
            AssertRect(new Rect(1f, -Width - Lip, 0.5f, 0.5f + Width + Lip), rect);
        }

        [Test]
        public void QuarterRects_LShape_CoverEveryPointWithinTheWidthOfTheFootprintIncludingTheBend()
        {
            var rects = BlockTiling.Compute(LShape).Select(t => LiftOutline.QuarterRect(t, Width, 0f)).ToList();
            var cells = LShape.Select(c => new Rect(c.X, c.Y, 1f, 1f)).ToList();
            const int Steps = 120;
            var sampled = 0;

            // Sample the footprint's surroundings finely; every point within
            // the outline's reach of a footprint cell must lie in some quarter.
            for (var i = 0; i <= Steps; i++)
            {
                for (var j = 0; j <= Steps; j++)
                {
                    var point = new Vector2(-0.2f + 2.4f * i / Steps, -0.2f + 2.4f * j / Steps);
                    var distance = cells.Min(cell => DistanceTo(cell, point));
                    if (distance > Width * 0.99f)
                    {
                        continue;
                    }

                    sampled++;
                    Assert.IsTrue(rects.Any(r => r.Contains(point)), $"{point} is {distance} from the L and uncovered");
                }
            }

            Assert.Greater(sampled, Steps * Steps / 4, "the sample reaches most of the L and its band");
            Assert.IsTrue(rects.Any(r => r.Contains(new Vector2(1f + Width * 0.5f, 1f + Width * 0.5f))), "the bend's outer square");
        }

        private static QuarterTile TileAt(IReadOnlyList<Coord> cells, Coord cell, Quarter corner) =>
            BlockTiling.Compute(cells).Single(t => t.Cell == cell && t.Corner == corner);

        private static float DistanceTo(Rect rect, Vector2 point)
        {
            var dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
            var dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            const float Tolerance = 1e-5f;
            Assert.AreEqual(expected.xMin, actual.xMin, Tolerance, "xMin");
            Assert.AreEqual(expected.yMin, actual.yMin, Tolerance, "yMin");
            Assert.AreEqual(expected.xMax, actual.xMax, Tolerance, "xMax");
            Assert.AreEqual(expected.yMax, actual.yMax, Tolerance, "yMax");
        }
    }
}
