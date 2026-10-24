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
    /// casts a shadow past the shape, in the tiles' order, and a quarter
    /// overlaps its neighbours only while it is opaque and neither a shadow,
    /// a highlight nor the ice (Module 46, pass 2).
    /// </summary>
    public class QuarterLayersTests
    {
        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Square2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };

        [Test]
        public void Edged_SingleCell_IsAllFourQuartersInOrder()
        {
            var tiles = BlockTiling.Compute(Single);

            var edged = QuarterLayers.Edged(tiles);

            CollectionAssert.AreEqual(tiles, edged);
        }

        [Test]
        public void Edged_TwoByTwo_LeavesOutTheFourFills()
        {
            var tiles = BlockTiling.Compute(Square2);

            var edged = QuarterLayers.Edged(tiles);

            CollectionAssert.AreEqual(tiles.Where(tile => tile.Piece != QuarterPiece.Fill).ToList(), edged);
            Assert.AreEqual(tiles.Count - 4, edged.Count);
        }

        [Test]
        public void Edged_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => QuarterLayers.Edged(null));
        }

        [TestCase(QuarterLayer.Plain)]
        [TestCase(QuarterLayer.Lit)]
        [TestCase(QuarterLayer.ReversedLit)]
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
            var translucent = QuarterLayers.SeamOverlap(QuarterLayer.Lit, new Color(1f, 1f, 1f, alpha), 0.002f);

            Assert.AreEqual(0f, translucent);
        }

        [TestCase(QuarterLayer.Shadow)]
        [TestCase(QuarterLayer.Body)]
        [TestCase(QuarterLayer.Ice)]
        public void SeamOverlap_ShadowOrHighlightInOpaqueWhite_IsNone(QuarterLayer layer)
        {
            var piece = QuarterLayers.SeamOverlap(layer, Color.white, 0.002f);

            Assert.AreEqual(0f, piece);
        }
    }
}
