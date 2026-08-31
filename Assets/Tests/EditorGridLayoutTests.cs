using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="EditorGridLayout"/> — the maths the main board and an elevator
    /// wave's region both run. <see cref="EditorGridLayout.CellRect"/> and
    /// <see cref="EditorGridLayout.TryPick"/> must be exact inverses, and the grid's
    /// bottom-left origin must map to GUI space's top-left correctly.
    /// </summary>
    public class EditorGridLayoutTests
    {
        [Test]
        public void CellRectThenTryPick_RoundTripsEveryCell()
        {
            var layout = new EditorGridLayout(new Rect(10f, 20f, 400f, 300f), 5, 4);

            for (var x = 0; x < 5; x++)
            {
                for (var y = 0; y < 4; y++)
                {
                    var rect = layout.CellRect(new Coord(x, y));

                    Assert.IsTrue(layout.TryPick(rect.center, out var picked));
                    Assert.AreEqual(new Coord(x, y), picked);
                }
            }
        }

        [Test]
        public void CellRect_BottomLeftCellIsLowestOnScreen()
        {
            var layout = new EditorGridLayout(new Rect(0f, 0f, 100f, 100f), 4, 4);

            var bottomLeft = layout.CellRect(new Coord(0, 0));
            var topLeft = layout.CellRect(new Coord(0, 3));

            Assert.Less(topLeft.y, bottomLeft.y); // +Y (grid) is up, which is a smaller GUI y
            Assert.AreEqual(topLeft.x, bottomLeft.x);
        }

        [Test]
        public void TryPick_PointOutsideTheCellArea_ReturnsFalse()
        {
            var layout = new EditorGridLayout(new Rect(0f, 0f, 100f, 100f), 4, 4);

            Assert.IsFalse(layout.TryPick(new Vector2(-5f, 50f), out _));
            Assert.IsFalse(layout.TryPick(new Vector2(50f, 999f), out _));
        }

        [Test]
        public void Constructor_NonSquareOffer_ProducesSquareCellsWithinIt()
        {
            var layout = new EditorGridLayout(new Rect(0f, 0f, 500f, 200f), 4, 4, maxCellSize: 64f);

            Assert.AreEqual(50f, layout.CellSize); // limited by the 200 height / 4 rows
            Assert.LessOrEqual(layout.Area.width, 500f);
        }

        [Test]
        public void Constructor_HugeArea_CapsTheCellSizeAndCentresTheGrid()
        {
            var available = new Rect(0f, 0f, 2000f, 2000f);

            var layout = new EditorGridLayout(available, 4, 4, maxCellSize: 64f);

            Assert.AreEqual(64f, layout.CellSize);
            Assert.LessOrEqual(layout.CellSize, 64f);

            // 4 * 64 = 256 wide, centred in 2000 → left margin (2000 - 256) / 2.
            Assert.AreEqual(available.center.x, layout.Area.center.x, 0.5f);
            Assert.AreEqual(available.center.y, layout.Area.center.y, 0.5f);
        }

        [Test]
        public void Constructor_CellNeverExceedsTheCap_AcrossManySizes()
        {
            for (var side = 100f; side <= 3000f; side += 137f)
            {
                var layout = new EditorGridLayout(new Rect(0f, 0f, side, side), 5, 5, maxCellSize: 48f);
                Assert.LessOrEqual(layout.CellSize, 48f);
            }
        }

        // -- EditorGrid.EdgeMarker (item 3: clamp, never a garbage rect) --

        [Test]
        public void EdgeMarker_GateThatFits_SitsOverTheRightCellsBelowTheBoard()
        {
            var layout = new EditorGridLayout(new Rect(0f, 0f, 600f, 600f), 6, 6, maxCellSize: 100f);

            var rect = EditorGrid.EdgeMarker(layout, BoardEdge.Bottom, offset: 2, cells: 2, depth: 8f);

            Assert.AreEqual(layout.CellRect(new Coord(2, 0)).x, rect.x, 0.01f);
            Assert.AreEqual(2 * layout.CellSize, rect.width, 0.01f);
            Assert.AreEqual(layout.Area.yMax, rect.y, 0.01f);
            Assert.AreEqual(8f, rect.height, 0.01f);
        }

        [Test]
        public void EdgeMarker_OffsetPastTheEdge_IsClampedOnScreen()
        {
            var layout = new EditorGridLayout(new Rect(0f, 0f, 600f, 600f), 6, 6, maxCellSize: 100f);

            var rect = EditorGrid.EdgeMarker(layout, BoardEdge.Bottom, offset: 20, cells: 1, depth: 8f);

            Assert.GreaterOrEqual(rect.x, layout.Area.x - 0.01f);
            Assert.LessOrEqual(rect.xMax, layout.Area.xMax + 0.01f);
        }

        [Test]
        public void EdgeMarker_RunWiderThanTheEdge_IsClampedToTheEdgeLength()
        {
            var layout = new EditorGridLayout(new Rect(0f, 0f, 600f, 600f), 6, 6, maxCellSize: 100f);

            var rect = EditorGrid.EdgeMarker(layout, BoardEdge.Left, offset: 0, cells: 99, depth: 8f);

            // 6 rows * cell size, and the whole marker stays within the board's vertical span.
            Assert.AreEqual(6 * layout.CellSize, rect.height, 0.01f);
            Assert.GreaterOrEqual(rect.y, layout.Area.y - 0.01f);
            Assert.LessOrEqual(rect.yMax, layout.Area.yMax + 0.01f);
        }
    }
}
