using GateRush.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="EditorCellSpace"/> (Module 20): rectangles in cell
    /// units land on whole pixels, so two that share an edge meet exactly on
    /// screen — no gap for what is underneath to show through, no overlap —
    /// and a rectangle with nothing left inside the clip is not drawn.
    /// </summary>
    public class EditorCellSpaceTests
    {
        /// <summary>An odd cell size: half a cell is 12.5 pixels, so every half-cell seam is on a fractional pixel.</summary>
        private const float OddCellSize = 25f;

        private static readonly Vector2 Origin = new Vector2(40f, 300f);
        private static readonly Rect WideClip = new Rect(0f, 0f, 1000f, 1000f);

        [Test]
        public void TryToGui_RectsSharingAnEdgeOnAFractionalPixel_MeetExactly()
        {
            var space = new EditorCellSpace(Origin, OddCellSize, WideClip);
            var left = new Rect(0f, 0f, 0.5f, 0.5f);
            var right = new Rect(0.5f, 0f, 0.5f, 0.5f);
            var above = new Rect(0f, 0.5f, 0.5f, 0.5f);
            var seam = Origin.x + (left.xMax * OddCellSize);

            var isLeftDrawn = space.TryToGui(left, out var leftGui);
            var isRightDrawn = space.TryToGui(right, out var rightGui);
            var isAboveDrawn = space.TryToGui(above, out var aboveGui);

            Assert.AreNotEqual(Mathf.Round(seam), seam, "the shared edge is on a fractional pixel before snapping");
            Assert.IsTrue(isLeftDrawn && isRightDrawn && isAboveDrawn);
            Assert.AreEqual(leftGui.xMax, rightGui.xMin, "side by side: no gap and no overlap");
            Assert.AreEqual(aboveGui.yMax, leftGui.yMin, "one above the other (+Y is down on screen): no gap and no overlap");
            AssertWholePixels(leftGui);
            AssertWholePixels(rightGui);
            AssertWholePixels(aboveGui);
        }

        [Test]
        public void TryToGui_NothingLeftInsideTheClipAfterSnapping_IsFalse()
        {
            // The clip's left edge cuts the rectangle down to a sliver under
            // half a pixel wide, which snaps to no width at all.
            var clip = new Rect(Origin.x + OddCellSize - 0.2f, 0f, 1000f, 1000f);
            var space = new EditorCellSpace(Origin, OddCellSize, clip);
            var cell = new Rect(0f, 0f, 1f, 1f);
            var neighbour = new Rect(1f, 0f, 1f, 1f);

            var isSliverDrawn = space.TryToGui(cell, out _);
            var isNeighbourDrawn = space.TryToGui(neighbour, out var neighbourGui);

            Assert.IsFalse(isSliverDrawn);
            Assert.IsTrue(isNeighbourDrawn, "a rectangle well inside the same clip is still drawn");
            Assert.AreEqual(OddCellSize, neighbourGui.height);
        }

        private static void AssertWholePixels(Rect gui)
        {
            Assert.AreEqual(Mathf.Round(gui.xMin), gui.xMin, "xMin");
            Assert.AreEqual(Mathf.Round(gui.xMax), gui.xMax, "xMax");
            Assert.AreEqual(Mathf.Round(gui.yMin), gui.yMin, "yMin");
            Assert.AreEqual(Mathf.Round(gui.yMax), gui.yMax, "yMax");
        }
    }
}
