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
    /// on them; and a badge widens with its number's digits. Module 19: two
    /// marks sharing a block both sit on its footprint, a cell apart where it
    /// has room and half a cell apart, crowded, where it has not. Module 20:
    /// a row of one, two or three marks — the layer badge among them — stays
    /// on the footprint, and the badge clears a padlock on a 1×1.
    /// </summary>
    public class MarkLayoutTests
    {
        private const float Thickness = 0.2f;
        private const float EndInset = 0.1f;
        private const float Tolerance = 1e-5f;

        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Horizontal2 = { new Coord(0, 0), new Coord(1, 0) };
        private static readonly Coord[] Vertical2 = { new Coord(0, 0), new Coord(0, 1) };
        private static readonly Coord[] Horizontal3 ={ new Coord(0, 0), new Coord(1, 0), new Coord(2, 0) };
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

        private static IEnumerable<TestCaseData> PairFootprints()
        {
            yield return new TestCaseData((object)Single).SetName("PairedMarks_1x1_BothPointsLieOnTheFootprint");
            yield return new TestCaseData((object)Horizontal2).SetName("PairedMarks_Horizontal1x2_BothPointsLieOnTheFootprint");
            yield return new TestCaseData((object)Vertical2).SetName("PairedMarks_Vertical1x2_BothPointsLieOnTheFootprint");
            yield return new TestCaseData((object)Horizontal3).SetName("PairedMarks_Horizontal1x3_BothPointsLieOnTheFootprint");
            yield return new TestCaseData((object)Vertical3).SetName("PairedMarks_Vertical1x3_BothPointsLieOnTheFootprint");
            yield return new TestCaseData((object)Square).SetName("PairedMarks_2x2_BothPointsLieOnTheFootprint");
            yield return new TestCaseData((object)L).SetName("PairedMarks_L_BothPointsLieOnTheFootprint");
            yield return new TestCaseData((object)T).SetName("PairedMarks_T_BothPointsLieOnTheFootprint");
            yield return new TestCaseData((object)U).SetName("PairedMarks_U_BothPointsLieOnTheFootprint");
        }

        [Test]
        public void PairedMarks_OnA1x2_SitInItsTwoCellsAtFullSize()
        {
            var pair = MarkLayout.PairedMarks(Horizontal2);

            AssertPoint(new Vector2(0.5f, 0.5f), pair.First);
            AssertPoint(new Vector2(1.5f, 0.5f), pair.Second);
            Assert.IsFalse(pair.IsCrowded);
        }

        [Test]
        public void PairedMarks_OnAVertical1x2_SitOneAboveTheOtherAtFullSize()
        {
            var pair = MarkLayout.PairedMarks(Vertical2);

            AssertPoint(new Vector2(0.5f, 0.5f), pair.First);
            AssertPoint(new Vector2(0.5f, 1.5f), pair.Second);
            Assert.IsFalse(pair.IsCrowded);
        }

        [Test]
        public void PairedMarks_OnA1x1_ShareTheCellAtTheCrowdedScale()
        {
            var pair = MarkLayout.PairedMarks(Single);

            AssertPoint(new Vector2(0.5f - MarkLayout.CrowdedOffsetCells, 0.5f), pair.First);
            AssertPoint(new Vector2(0.5f + MarkLayout.CrowdedOffsetCells, 0.5f), pair.Second);
            Assert.IsTrue(pair.IsCrowded);
        }

        [Test]
        public void PairedMarks_OnAnL_StayOnTheBendCell()
        {
            var anchor = MarkLayout.Anchor(L);
            var bend = new Coord(Mathf.FloorToInt(anchor.x), Mathf.FloorToInt(anchor.y));

            var pair = MarkLayout.PairedMarks(L);

            Assert.IsTrue(pair.IsCrowded, "the L's bounding box has an empty corner beside the bend");
            Assert.AreEqual(bend, new Coord(Mathf.FloorToInt(pair.First.x), Mathf.FloorToInt(pair.First.y)));
            Assert.AreEqual(bend, new Coord(Mathf.FloorToInt(pair.Second.x), Mathf.FloorToInt(pair.Second.y)));
        }

        [TestCaseSource(nameof(PairFootprints))]
        public void PairedMarks_EveryFootprint_BothPointsLieOnTheFootprint(Coord[] cells)
        {
            var pair = MarkLayout.PairedMarks(cells);

            AssertOnFootprint(cells, pair.First, "first");
            AssertOnFootprint(cells, pair.Second, "second");

            // With room each mark has a whole cell; crowded, half of one.
            var apart = 2f * (pair.IsCrowded ? MarkLayout.CrowdedOffsetCells : MarkLayout.RoomyOffsetCells);
            Assert.AreEqual(apart, Vector2.Distance(pair.First, pair.Second), Tolerance);
        }

        private static IEnumerable<TestCaseData> RowFootprints()
        {
            yield return new TestCaseData((object)Single).SetName("Row_1x1_EverySlotLiesOnTheFootprint");
            yield return new TestCaseData((object)Horizontal2).SetName("Row_Horizontal1x2_EverySlotLiesOnTheFootprint");
            yield return new TestCaseData((object)Vertical2).SetName("Row_Vertical1x2_EverySlotLiesOnTheFootprint");
            yield return new TestCaseData((object)Horizontal3).SetName("Row_Horizontal1x3_EverySlotLiesOnTheFootprint");
            yield return new TestCaseData((object)Vertical3).SetName("Row_Vertical1x3_EverySlotLiesOnTheFootprint");
            yield return new TestCaseData((object)Square).SetName("Row_2x2_EverySlotLiesOnTheFootprint");
            yield return new TestCaseData((object)L).SetName("Row_L_EverySlotLiesOnTheFootprint");
            yield return new TestCaseData((object)T).SetName("Row_T_EverySlotLiesOnTheFootprint");
            yield return new TestCaseData((object)U).SetName("Row_U_EverySlotLiesOnTheFootprint");
        }

        [Test]
        public void Row_OneMark_IsTheAnchor()
        {
            var row = MarkLayout.Row(L, 1);

            Assert.AreEqual(1, row.Count);
            AssertPoint(MarkLayout.Anchor(L), row[0]);
            Assert.IsFalse(row.IsCrowded);
        }

        [Test]
        public void Row_ThreeMarksOnA1x3_SitOnePerCellAtFullSize()
        {
            var across = MarkLayout.Row(Horizontal3, 3);
            var up = MarkLayout.Row(Vertical3, 3);

            Assert.IsFalse(across.IsCrowded);
            AssertPoint(new Vector2(0.5f, 0.5f), across[0]);
            AssertPoint(new Vector2(1.5f, 0.5f), across[1]);
            AssertPoint(new Vector2(2.5f, 0.5f), across[2]);
            Assert.IsFalse(up.IsCrowded);
            AssertPoint(new Vector2(0.5f, 0.5f), up[0]);
            AssertPoint(new Vector2(0.5f, 1.5f), up[1]);
            AssertPoint(new Vector2(0.5f, 2.5f), up[2]);
        }

        [Test]
        public void Row_ThreeMarksOnA1x1_ShareTheCell()
        {
            var row = MarkLayout.Row(Single, 3);

            Assert.IsTrue(row.IsCrowded);
            Assert.AreEqual(3, row.Count);
            AssertPoint(new Vector2(0.5f - MarkLayout.CrowdedTripleOffsetCells, 0.5f), row[0]);
            AssertPoint(new Vector2(0.5f, 0.5f), row[1]);
            AssertPoint(new Vector2(0.5f + MarkLayout.CrowdedTripleOffsetCells, 0.5f), row[2]);
        }

        [TestCaseSource(nameof(RowFootprints))]
        public void Row_EveryFootprint_EverySlotLiesOnTheFootprint(Coord[] cells)
        {
            var slots = 0;

            for (var count = 1; count <= MarkLayout.MostMarksInARow; count++)
            {
                var row = MarkLayout.Row(cells, count);

                Assert.AreEqual(count, row.Count);
                for (var i = 0; i < row.Count; i++)
                {
                    AssertOnFootprint(cells, row[i], $"{i + 1} of {count}");
                    slots++;
                }
            }

            Assert.AreEqual(1 + 2 + 3, slots, "one, two and three marks were all placed");
        }

        [Test]
        public void Row_TwoMarks_IsWherePairedMarksPutsThem()
        {
            var row = MarkLayout.Row(Horizontal2, 2);
            var pair = MarkLayout.PairedMarks(Horizontal2);

            AssertPoint(pair.First, row[0]);
            AssertPoint(pair.Second, row[1]);
            Assert.AreEqual(pair.IsCrowded, row.IsCrowded);
        }

        [Test]
        public void Row_NoMarksOrFour_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MarkLayout.Row(Single, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MarkLayout.Row(Single, MarkLayout.MostMarksInARow + 1));
        }

        [Test]
        public void Row_LayerBadgeAndPadlockOnA1x1_DoNotOverlapAtTheDefaultSizes()
        {
            // A 1x1 layered locked block: the padlock takes the first slot
            // and the layer badge the second, both at the crowded scale. The
            // padlock's own count badge hangs on its body and is narrower
            // than it, so the padlock's width is the lock's.
            var config = ScriptableObject.CreateInstance<RuntimeConfig>();
            const int ThreeColours = 3;

            var row = MarkLayout.Row(Single, 2);
            var apart = row[1].x - row[0].x;
            var padlockHalf = config.PadlockSizeCells * 0.5f * config.CrowdedMarkScale;
            var badgeHalf = config.BadgeWidthCells(ThreeColours) * 0.5f * config.CrowdedMarkScale;
            var keyCountWidth = config.BadgeWidthCells(1);
            var padlockWidth = config.PadlockSizeCells;
            UnityEngine.Object.DestroyImmediate(config);

            Assert.IsTrue(row.IsCrowded, "a 1x1 has no room for a cell each");
            Assert.AreEqual(row[0].y, row[1].y, Tolerance, "the two sit side by side");
            Assert.LessOrEqual(keyCountWidth, padlockWidth, "the padlock's own badge stays within the padlock");
            Assert.LessOrEqual(padlockHalf + badgeHalf, apart, "the padlock's right side stops before the badge's left");
        }

        [Test]
        public void BonusBadgeWidth_MoreDigits_WidenIt()
        {
            const float height = 0.34f;
            const float digit = 0.15f;
            const float padding = 0.08f;
            const float icon = 0.24f;
            const float gap = 0.03f;

            var two = MarkLayout.BonusBadgeWidth(2, height, digit, padding, icon, gap);
            var three = MarkLayout.BonusBadgeWidth(3, height, digit, padding, icon, gap);

            Assert.AreEqual(icon + gap + 2 * digit + 2 * padding, two, Tolerance);
            Assert.AreEqual(digit, three - two, Tolerance, "each character adds a digit's width");
        }

        /// <summary>
        /// Asserts that every cell touching <paramref name="point"/> — one, two
        /// across a seam, or four round a corner — belongs to
        /// <paramref name="cells"/>.
        /// </summary>
        private static void AssertOnFootprint(Coord[] cells, Vector2 point, string label)
        {
            const float nudge = 0.01f;
            foreach (var dx in new[] { -nudge, nudge })
            {
                foreach (var dy in new[] { -nudge, nudge })
                {
                    var cell = new Coord(Mathf.FloorToInt(point.x + dx), Mathf.FloorToInt(point.y + dy));
                    CollectionAssert.Contains(cells, cell, $"the {label} mark at {point} touches {cell}");
                }
            }
        }

        private static void AssertPoint(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, Tolerance, "y");
        }
    }
}
