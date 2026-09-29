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
    /// Covers <see cref="MarkLayout"/> (Module 16): a block's marks sit on its
    /// footprint, never over a neighbour in an empty corner of its bounding
    /// box; a locked block's chains run along each row of its cells and stay
    /// on them; and a badge widens with its number's digits.
    /// </summary>
    public class MarkLayoutTests
    {
        private const float Thickness = 0.2f;
        private const float EndInset = 0.1f;
        private const float Tolerance = 1e-5f;

        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Horizontal3 = { new Coord(0, 0), new Coord(1, 0), new Coord(2, 0) };
        private static readonly Coord[] Vertical3 = { new Coord(0, 0), new Coord(0, 1), new Coord(0, 2) };
        private static readonly Coord[] Square = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };
        private static readonly Coord[] L = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };
        private static readonly Coord[] T = { new Coord(0, 1), new Coord(1, 1), new Coord(2, 1), new Coord(1, 0) };

        /// <summary>A U: its bottom row holds two runs with a gap between them.</summary>
        private static readonly Coord[] U =
        {
            new Coord(0, 0), new Coord(2, 0), new Coord(0, 1), new Coord(1, 1), new Coord(2, 1)
        };

        private static IEnumerable<TestCaseData> Footprints()
        {
            yield return new TestCaseData((object)Single).SetName("ChainStrips_1x1_EveryStripStaysOnItsOwnRunAndEveryCellIsChainedOnce");
            yield return new TestCaseData((object)Horizontal3).SetName("ChainStrips_Horizontal1x3_EveryStripStaysOnItsOwnRunAndEveryCellIsChainedOnce");
            yield return new TestCaseData((object)Vertical3).SetName("ChainStrips_Vertical1x3_EveryStripStaysOnItsOwnRunAndEveryCellIsChainedOnce");
            yield return new TestCaseData((object)Square).SetName("ChainStrips_2x2_EveryStripStaysOnItsOwnRunAndEveryCellIsChainedOnce");
            yield return new TestCaseData((object)L).SetName("ChainStrips_L_EveryStripStaysOnItsOwnRunAndEveryCellIsChainedOnce");
            yield return new TestCaseData((object)T).SetName("ChainStrips_T_EveryStripStaysOnItsOwnRunAndEveryCellIsChainedOnce");
            yield return new TestCaseData((object)U).SetName("ChainStrips_U_EveryStripStaysOnItsOwnRunAndEveryCellIsChainedOnce");
        }

        [Test]
        public void Anchor_1x1_IsTheCellsCentre()
        {
            var anchor = MarkLayout.Anchor(Single);

            AssertPoint(new Vector2(0.5f, 0.5f), anchor);
        }

        [Test]
        public void Anchor_Horizontal1x2_IsTheSeamBetweenItsCells()
        {
            var anchor = MarkLayout.Anchor(new[] { new Coord(0, 0), new Coord(1, 0) });

            AssertPoint(new Vector2(1f, 0.5f), anchor);
        }

        [Test]
        public void Anchor_T_IsTheCentreOfItsBoundingBox()
        {
            // (1.5, 1) lies on the seam between the T's stem and its bar.
            var anchor = MarkLayout.Anchor(T);

            AssertPoint(new Vector2(1.5f, 1f), anchor);
        }

        [Test]
        public void Anchor_L_IsTheCentreOfOneOfItsCellsNotTheEmptyCornerOfItsBoundingBox()
        {
            var anchor = MarkLayout.Anchor(L);

            var cell = new Coord(Mathf.FloorToInt(anchor.x), Mathf.FloorToInt(anchor.y));
            CollectionAssert.Contains(L, cell, "the anchor lies on the footprint");
            AssertPoint(new Vector2(cell.X + 0.5f, cell.Y + 0.5f), anchor);
        }

        [TestCaseSource(nameof(Footprints))]
        public void ChainStrips_EveryStripStaysOnItsOwnRunAndEveryCellIsChainedOnce(Coord[] cells)
        {
            var strips = MarkLayout.ChainStrips(cells, Thickness, EndInset);

            var chained = new List<Coord>();
            foreach (var strip in strips)
            {
                var row = Mathf.FloorToInt(strip.center.y);
                Assert.AreEqual(row + 0.5f, strip.center.y, Tolerance, "through the middle of its row");
                Assert.AreEqual(Thickness, strip.height, Tolerance);

                var first = Mathf.RoundToInt(strip.xMin - EndInset);
                var last = Mathf.RoundToInt(strip.xMax + EndInset) - 1;
                Assert.AreEqual(first + EndInset, strip.xMin, Tolerance, "inset from the run's start");
                Assert.AreEqual(last + 1 - EndInset, strip.xMax, Tolerance, "inset from the run's end");
                for (var x = first; x <= last; x++)
                {
                    var cell = new Coord(x, row);
                    CollectionAssert.Contains(cells, cell, $"a strip over {cell} stays on the footprint");
                    chained.Add(cell);
                }
            }

            CollectionAssert.AreEquivalent(cells, chained, "every cell is under exactly one strip");
        }

        [Test]
        public void ChainStrips_URow_WithAGap_GetsOneStripPerRun()
        {
            var strips = MarkLayout.ChainStrips(U, Thickness, EndInset);

            Assert.AreEqual(3, strips.Count, "two runs in the bottom row, one in the top");
            Assert.AreEqual(2, strips.Count(s => s.center.y < 1f));
        }

        [Test]
        public void ChainStrips_EndInsetOfHalfACell_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MarkLayout.ChainStrips(Single, Thickness, 0.5f));
        }

        [Test]
        public void BadgeWidth_OneDigit_IsAsWideAsItIsTall_AndMoreDigitsWidenIt()
        {
            const float height = 0.34f;
            const float digit = 0.15f;
            const float padding = 0.08f;

            var one = MarkLayout.BadgeWidth(7, height, digit, padding);
            var two = MarkLayout.BadgeWidth(12, height, digit, padding);
            var three = MarkLayout.BadgeWidth(100, height, digit, padding);

            Assert.AreEqual(height, one, Tolerance);
            Assert.Greater(three, two);
            Assert.GreaterOrEqual(two, one);
            Assert.AreEqual(3 * digit + 2 * padding, three, Tolerance);
        }

        private static void AssertPoint(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, Tolerance, "y");
        }
    }
}
