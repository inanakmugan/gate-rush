using System.Collections.Generic;
using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="QueueEntryDrawBounds"/>: a queue entry's Free-draw grid
    /// is capped at the generator's width along its edge and at the configured
    /// depth into the board, grows to keep a stale out-of-cap cell visible and
    /// clickable, and never touches the entry's cells.
    /// </summary>
    public class QueueEntryDrawBoundsTests
    {
        private const int MaxDepth = 4;

        private static Coord C(int x, int y) => new Coord(x, y);

        private static readonly List<Coord> NoCells = new List<Coord>();

        [Test]
        public void For_TopEdgeWidth1_ColumnsCappedAtWidth()
        {
            var bounds = QueueEntryDrawBounds.For(BoardEdge.Top, 1, MaxDepth, NoCells);

            Assert.AreEqual(1, bounds.Columns);
        }

        [Test]
        public void For_BottomEdgeWidth2_ColumnsCappedAtWidth()
        {
            var bounds = QueueEntryDrawBounds.For(BoardEdge.Bottom, 2, MaxDepth, NoCells);

            Assert.AreEqual(2, bounds.Columns);
        }

        [Test]
        public void For_LeftEdgeWidth2_RowsCappedAtWidth()
        {
            var bounds = QueueEntryDrawBounds.For(BoardEdge.Left, 2, MaxDepth, NoCells);

            Assert.AreEqual(2, bounds.Rows);
        }

        [Test]
        public void For_RightEdgeWidth1_RowsCappedAtWidth()
        {
            var bounds = QueueEntryDrawBounds.For(BoardEdge.Right, 1, MaxDepth, NoCells);

            Assert.AreEqual(1, bounds.Rows);
        }

        [Test]
        public void For_TopEdge_RowsCappedAtMaxDepth()
        {
            var bounds = QueueEntryDrawBounds.For(BoardEdge.Top, 2, MaxDepth, NoCells);

            Assert.AreEqual(MaxDepth, bounds.Rows);
        }

        [Test]
        public void For_LeftEdge_ColumnsCappedAtMaxDepth()
        {
            var bounds = QueueEntryDrawBounds.For(BoardEdge.Left, 2, MaxDepth, NoCells);

            Assert.AreEqual(MaxDepth, bounds.Columns);
        }

        [Test]
        public void For_WidthOutsideCoreRange_ClampsTheDrawSurfaceOnly()
        {
            var tooWide = QueueEntryDrawBounds.For(BoardEdge.Top, 50, MaxDepth, NoCells);
            var zero = QueueEntryDrawBounds.For(BoardEdge.Top, 0, MaxDepth, NoCells);

            Assert.AreEqual(GeneratorDefinition.MaxWidth, tooWide.Columns);
            Assert.AreEqual(1, zero.Columns);
        }

        [Test]
        public void For_CellBeyondDepthCap_GrowsThatAxis()
        {
            var cells = new List<Coord> { C(0, 0), C(0, 1), C(0, 2), C(0, 3), C(0, 4) };

            var bounds = QueueEntryDrawBounds.For(BoardEdge.Top, 1, MaxDepth, cells);

            Assert.AreEqual(5, bounds.Rows);
            Assert.AreEqual(1, bounds.Columns);
        }

        [Test]
        public void For_WidthShrunkAfterDrawing_KeepsOversizedCellVisibleAndClickable()
        {
            // Drawn as a horizontal domino on a width-2 generator, then the width dropped to 1.
            var cells = new List<Coord> { C(0, 0), C(1, 0) };

            var bounds = QueueEntryDrawBounds.For(BoardEdge.Top, 1, MaxDepth, cells);
            var layout = new EditorGridLayout(new Rect(0f, 0f, 400f, 400f), bounds.Columns, bounds.Rows);
            var stale = C(1, 0);

            Assert.AreEqual(2, bounds.Columns);
            Assert.IsTrue(layout.TryPick(layout.CellRect(stale).center, out var picked));
            Assert.AreEqual(stale, picked);
        }

        [Test]
        public void For_WidthShrunkAfterDrawing_DoesNotTouchCells()
        {
            var cells = new List<Coord> { C(0, 0), C(1, 0) };
            var before = new List<Coord>(cells);

            QueueEntryDrawBounds.For(BoardEdge.Top, 1, MaxDepth, cells);

            CollectionAssert.AreEqual(before, cells);
        }

        [Test]
        public void AllowsNewCell_InsideCaps_ReturnsTrue()
        {
            var bounds = QueueEntryDrawBounds.For(BoardEdge.Left, 2, MaxDepth, NoCells);

            Assert.IsTrue(bounds.AllowsNewCell(C(0, 0)));
            Assert.IsTrue(bounds.AllowsNewCell(C(MaxDepth - 1, 1)));
        }

        [Test]
        public void AllowsNewCell_OutsideCaps_ReturnsFalse()
        {
            // Width shrunk to 1 with a stale cell at (1,0): the grid grows to two
            // columns, but the grown column is remove-only.
            var cells = new List<Coord> { C(0, 0), C(1, 0) };

            var bounds = QueueEntryDrawBounds.For(BoardEdge.Top, 1, MaxDepth, cells);

            Assert.IsFalse(bounds.AllowsNewCell(C(1, 1)));
            Assert.IsFalse(bounds.AllowsNewCell(C(1, 0)));
            Assert.IsFalse(bounds.AllowsNewCell(C(0, MaxDepth)));
            Assert.IsFalse(bounds.AllowsNewCell(C(-1, 0)));
        }
    }
}
