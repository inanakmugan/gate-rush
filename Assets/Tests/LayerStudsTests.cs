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
    /// Covers <see cref="LayerStuds"/> (Module 46): a layered block's inner
    /// block holds as many studs as fit its own grid, centred, and none where
    /// its arms are narrower than a stud — a 1×1, a 1×2 and an L hold none, a
    /// 2×2 holds 3×3 and a 2×3 holds 3×5, at the shipped spacing and clearance.
    /// </summary>
    public class LayerStudsTests
    {
        // The config's defaults: the window 0.31 and its rim 0.02, the lip
        // 0.1, the inner grid and the clearance.
        private const float InnerInset = 0.33f;
        private const float Lip = 0.1f;
        private const float Spacing = 0.42f;
        private const float Clearance = 0.18f;
        private const float Tolerance = 1e-4f;

        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Horizontal2 = { new Coord(0, 0), new Coord(1, 0) };
        private static readonly Coord[] Vertical2 = { new Coord(0, 0), new Coord(0, 1) };
        private static readonly Coord[] Square2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };
        private static readonly Coord[] Ell = { new Coord(0, 0), new Coord(0, 1), new Coord(0, 2), new Coord(1, 0) };

        [Test]
        public void Centers_SingleCellAndOneWidePairs_AreEmpty()
        {
            Assert.IsEmpty(Centers(Single), "1×1");
            Assert.IsEmpty(Centers(Horizontal2), "1×2 lying");
            Assert.IsEmpty(Centers(Vertical2), "1×2 standing");
        }

        [Test]
        public void Centers_Ell_IsEmptyAsItsArmsAreOneCellWide()
        {
            var centers = Centers(Ell);

            Assert.IsEmpty(centers);
        }

        [Test]
        public void Centers_TwoByTwo_IsAThreeByThreeGridCentredOnTheInnerBlock()
        {
            var centers = Centers(Square2);

            // The inner block runs from the inset to 2 less the inset across,
            // and from the inset plus the lip to 2 less the inset up.
            var middleX = 1f;
            var middleY = (InnerInset + Lip + 2f - InnerInset) * 0.5f;

            Assert.AreEqual(9, centers.Count);
            foreach (var row in new[] { -1, 0, 1 })
            {
                foreach (var column in new[] { -1, 0, 1 })
                {
                    var expected = new Vector2(middleX + column * Spacing, middleY + row * Spacing);
                    Assert.IsTrue(centers.Any(c => (c - expected).magnitude < Tolerance), $"a stud at {expected}");
                }
            }
        }

        [Test]
        public void Centers_TwoByThree_IsThreeAcrossAndFiveUp()
        {
            var cells = new[]
            {
                new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1), new Coord(0, 2), new Coord(1, 2)
            };

            var centers = Centers(cells);

            Assert.AreEqual(15, centers.Count);
        }

        [Test]
        public void Centers_NullCells_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LayerStuds.Centers(null, InnerInset, Lip, Spacing, Clearance));
        }

        [Test]
        public void Centers_NonPositiveSpacing_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LayerStuds.Centers(Square2, InnerInset, Lip, 0f, Clearance));
        }

        private static IReadOnlyList<Vector2> Centers(Coord[] cells) =>
            LayerStuds.Centers(cells, InnerInset, Lip, Spacing, Clearance);
    }
}
