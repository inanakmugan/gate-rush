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
    /// Covers <see cref="QuarterLayers"/> (Module 46): every quarter but a fill
    /// carries the contour, and the shine goes on the outer corners, edges
    /// along x and concave corners of the top half of a cell for the top
    /// edges and of the bottom half for the bottom ones — never on a side
    /// edge or a fill — in the tiles' order.
    /// </summary>
    public class QuarterLayersTests
    {
        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Vertical2 = { new Coord(0, 0), new Coord(0, 1) };
        private static readonly Coord[] Square2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };

        // A bar three high with a foot to its right at the bottom: its inside
        // corner is the bottom cell's top-right quarter.
        private static readonly Coord[] Ell = { new Coord(0, 0), new Coord(0, 1), new Coord(0, 2), new Coord(1, 0) };

        [Test]
        public void Contour_SingleCell_IsAllFourQuartersInOrder()
        {
            var tiles = BlockTiling.Compute(Single);

            var contour = QuarterLayers.Contour(tiles);

            CollectionAssert.AreEqual(tiles, contour);
        }

        [Test]
        public void Contour_TwoByTwo_LeavesOutTheFourFills()
        {
            var tiles = BlockTiling.Compute(Square2);

            var contour = QuarterLayers.Contour(tiles);

            CollectionAssert.AreEqual(tiles.Where(tile => tile.Piece != QuarterPiece.Fill).ToList(), contour);
            Assert.AreEqual(tiles.Count - 4, contour.Count);
        }

        [Test]
        public void Shine_SingleCell_IsTheTopCornersOnTopAndTheBottomCornersBelow()
        {
            var tiles = BlockTiling.Compute(Single);

            var top = QuarterLayers.Shine(tiles, isTop: true);
            var bottom = QuarterLayers.Shine(tiles, isTop: false);

            CollectionAssert.AreEqual(new[] { Quarter.TopLeft, Quarter.TopRight }, top.Select(tile => tile.Corner));
            CollectionAssert.AreEqual(new[] { Quarter.BottomLeft, Quarter.BottomRight }, bottom.Select(tile => tile.Corner));
        }

        [Test]
        public void Shine_VerticalPair_LeavesOutTheSideEdges()
        {
            var tiles = BlockTiling.Compute(Vertical2);

            var top = QuarterLayers.Shine(tiles, isTop: true);
            var bottom = QuarterLayers.Shine(tiles, isTop: false);

            Assert.IsTrue(top.All(tile => tile.Cell == new Coord(0, 1) && tile.Piece == QuarterPiece.OuterCorner), "the top cell's corners");
            Assert.AreEqual(2, top.Count);
            Assert.IsTrue(bottom.All(tile => tile.Cell == new Coord(0, 0) && tile.Piece == QuarterPiece.OuterCorner), "the bottom cell's corners");
            Assert.AreEqual(2, bottom.Count);
        }

        [Test]
        public void Shine_TwoByTwo_IsTheTopRowOnTopAndTheBottomRowBelow()
        {
            var tiles = BlockTiling.Compute(Square2);

            var top = QuarterLayers.Shine(tiles, isTop: true);
            var bottom = QuarterLayers.Shine(tiles, isTop: false);

            Assert.AreEqual(4, top.Count);
            Assert.IsTrue(top.All(tile => tile.Cell.Y == 1 && tile.SignY > 0), "the top cells' top quarters");
            Assert.AreEqual(4, bottom.Count);
            Assert.IsTrue(bottom.All(tile => tile.Cell.Y == 0 && tile.SignY < 0), "the bottom cells' bottom quarters");
            Assert.IsFalse(top.Concat(bottom).Any(tile => tile.Piece == QuarterPiece.Fill || tile.Piece == QuarterPiece.EdgeAlongY));
        }

        [Test]
        public void Shine_Ell_LightsTheFootsTopUpToTheInsideCorner()
        {
            var tiles = BlockTiling.Compute(Ell);

            var top = QuarterLayers.Shine(tiles, isTop: true);

            var inside = new QuarterTile(new Coord(0, 0), Quarter.TopRight, QuarterPiece.ConcaveCorner);
            CollectionAssert.Contains(top, inside, "the inside corner");
            CollectionAssert.Contains(top, new QuarterTile(new Coord(1, 0), Quarter.TopLeft, QuarterPiece.EdgeAlongX), "the foot's top edge");
            CollectionAssert.Contains(top, new QuarterTile(new Coord(1, 0), Quarter.TopRight, QuarterPiece.OuterCorner), "the foot's corner");
            CollectionAssert.DoesNotContain(
                top, new QuarterTile(new Coord(0, 1), Quarter.BottomRight, QuarterPiece.EdgeAlongY), "the bar's side above the corner");
            Assert.IsTrue(top.All(tile => tile.SignY > 0));
        }

        [Test]
        public void ContourAndShine_Null_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => QuarterLayers.Contour(null));
            Assert.Throws<ArgumentNullException>(() => QuarterLayers.Shine(null, isTop: true));
        }

        [Test]
        public void SeamOverlap_OpaqueColour_IsTheWholeOverlap()
        {
            const float overlap = 0.002f;

            var opaque = QuarterLayers.SeamOverlap(new Color(0.3f, 0.6f, 0.9f, 1f), overlap);

            Assert.AreEqual(overlap, opaque);
        }

        [TestCase(0.3f)]
        [TestCase(0.999f)]
        [TestCase(0f)]
        public void SeamOverlap_TranslucentColour_IsNone(float alpha)
        {
            var translucent = QuarterLayers.SeamOverlap(new Color(1f, 1f, 1f, alpha), 0.002f);

            Assert.AreEqual(0f, translucent);
        }

        [Test]
        public void Shine_Ell_KeepsTheTilesOrder()
        {
            var tiles = BlockTiling.Compute(Ell);

            var top = QuarterLayers.Shine(tiles, isTop: true);

            var indices = top.Select(tile => IndexOf(tiles, tile)).ToList();
            CollectionAssert.IsOrdered(indices);
        }

        private static int IndexOf(IReadOnlyList<QuarterTile> tiles, QuarterTile tile)
        {
            for (var i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].Equals(tile))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
