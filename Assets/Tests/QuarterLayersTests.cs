using System;
using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="QuarterLayers"/> (Module 46): every quarter but a fill
    /// carries the contour and the body or light, in the tiles' order, and a
    /// quarter overlaps its neighbours only while it is opaque and neither a
    /// body nor a light piece.
    /// </summary>
    public class QuarterLayersTests
    {
        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Square2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };

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
        public void Contour_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => QuarterLayers.Contour(null));
        }

        [TestCase(QuarterLayer.Face)]
        [TestCase(QuarterLayer.Contour)]
        public void SeamOverlap_OpaqueColour_IsTheWholeOverlap(QuarterLayer layer)
        {
            const float overlap = 0.002f;

            var opaque = QuarterLayers.SeamOverlap(layer, new Color(0.3f, 0.6f, 0.9f, 1f), overlap);

            Assert.AreEqual(overlap, opaque);
        }

        [TestCase(0.3f)]
        [TestCase(0.999f)]
        [TestCase(0f)]
        public void SeamOverlap_TranslucentColour_IsNone(float alpha)
        {
            var translucent = QuarterLayers.SeamOverlap(QuarterLayer.Face, new Color(1f, 1f, 1f, alpha), 0.002f);

            Assert.AreEqual(0f, translucent);
        }

        [TestCase(QuarterLayer.Body)]
        [TestCase(QuarterLayer.ReversedLight)]
        public void SeamOverlap_BodyOrLightPieceInOpaqueWhite_IsNone(QuarterLayer layer)
        {
            var light = QuarterLayers.SeamOverlap(layer, Color.white, 0.002f);

            Assert.AreEqual(0f, light);
        }
    }
}
