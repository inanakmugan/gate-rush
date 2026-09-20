using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BlockOutline"/> (docs/Modules/09a): the boundary of a
    /// block's own footprint, which is what lets two same-coloured blocks sitting
    /// side by side read as two blocks in the editor.
    /// </summary>
    public class BlockOutlineTests
    {
        private static BlockOutline.BoundaryEdge Edge(int x, int y, Direction side) =>
            new BlockOutline.BoundaryEdge(new Coord(x, y), side);

        private static void AssertSame(
            IReadOnlyList<BlockOutline.BoundaryEdge> actual,
            params BlockOutline.BoundaryEdge[] expected)
        {
            CollectionAssert.AreEqual(
                expected.Select(e => e.ToString()).ToList(),
                actual.Select(e => e.ToString()).ToList());
        }

        [Test]
        public void Edges_SingleCell_IsAllFourSidesInTheDocumentedOrder()
        {
            var edges = BlockOutline.Edges(new[] { new Coord(0, 0) });

            AssertSame(
                edges,
                Edge(0, 0, Direction.Up),
                Edge(0, 0, Direction.Down),
                Edge(0, 0, Direction.Left),
                Edge(0, 0, Direction.Right));
        }

        [Test]
        public void Edges_HorizontalPair_OmitsTheSeamBetweenTheTwoCells()
        {
            // The whole point: the side where the two cells meet is inside one
            // block and must not be stroked, while the grid line there is drawn
            // regardless by EditorGrid.
            var edges = BlockOutline.Edges(new[] { new Coord(0, 0), new Coord(1, 0) });

            AssertSame(
                edges,
                Edge(0, 0, Direction.Up),
                Edge(0, 0, Direction.Down),
                Edge(0, 0, Direction.Left),
                Edge(1, 0, Direction.Up),
                Edge(1, 0, Direction.Down),
                Edge(1, 0, Direction.Right));
        }

        [Test]
        public void Edges_LShape_HasThePerimeterOfAnLTromino()
        {
            var edges = BlockOutline.Edges(new[] { new Coord(0, 0), new Coord(0, 1), new Coord(1, 0) });

            Assert.AreEqual(8, edges.Count);
            CollectionAssert.DoesNotContain(
                edges.Select(e => e.ToString()).ToList(), Edge(0, 0, Direction.Up).ToString());
            CollectionAssert.DoesNotContain(
                edges.Select(e => e.ToString()).ToList(), Edge(0, 0, Direction.Right).ToString());
        }

        [Test]
        public void Edges_FootprintIsRelative_SoAnAbuttingNeighbourCannotErodeIt()
        {
            // Two 1x1 blocks sitting side by side are two footprints of {(0,0)}
            // at different origins; the origin never reaches here. That is why a
            // block keeps the side it shares with a neighbour — the case the
            // colour fill alone cannot distinguish — and why one 1x2 block, a
            // single footprint, loses its internal seam instead.
            var oneCell = BlockOutline.Edges(new[] { new Coord(0, 0) });
            var twoCells = BlockOutline.Edges(new[] { new Coord(0, 0), new Coord(1, 0) });

            Assert.AreEqual(4, oneCell.Count);
            Assert.AreEqual(6, twoCells.Count);

            // Two separate cells stroke the side between them twice; one 1x2
            // block strokes it not at all. That difference of two sides is the
            // seam, and is the whole visual difference the outline adds.
            Assert.AreEqual(2, (oneCell.Count * 2) - twoCells.Count);
        }

        [Test]
        public void Edges_VerticalPair_StrokesTheOuterEndsAndNotTheJoin()
        {
            var edges = BlockOutline.Edges(new[] { new Coord(0, 0), new Coord(0, 1) });

            AssertSame(
                edges,
                Edge(0, 0, Direction.Down),
                Edge(0, 0, Direction.Left),
                Edge(0, 0, Direction.Right),
                Edge(0, 1, Direction.Up),
                Edge(0, 1, Direction.Left),
                Edge(0, 1, Direction.Right));
        }

        [Test]
        public void Edges_NoCells_IsEmpty()
        {
            Assert.AreEqual(0, BlockOutline.Edges(new Coord[0]).Count);
            Assert.AreEqual(0, BlockOutline.Edges(null).Count);
        }

        // -- InteriorEdges ---------------------------------------------

        [Test]
        public void InteriorEdges_SingleCell_IsEmpty()
        {
            Assert.AreEqual(0, BlockOutline.InteriorEdges(new[] { new Coord(0, 0) }).Count);
        }

        [Test]
        public void InteriorEdges_HorizontalPair_IsExactlyTheSeamFromBothSides()
        {
            // Both cells report the side they share. The editor repaints both in
            // the block's fill colour; only the one that owns the grid line
            // actually covers it, and the other is a no-op on a pixel already
            // showing that colour.
            var interior = BlockOutline.InteriorEdges(new[] { new Coord(0, 0), new Coord(1, 0) });

            AssertSame(
                interior,
                Edge(0, 0, Direction.Right),
                Edge(1, 0, Direction.Left));
        }

        [Test]
        public void InteriorEdges_LShape_ReportsEveryInternalSide()
        {
            var interior = BlockOutline.InteriorEdges(
                new[] { new Coord(0, 0), new Coord(0, 1), new Coord(1, 0) });

            AssertSame(
                interior,
                Edge(0, 0, Direction.Up),
                Edge(0, 0, Direction.Right),
                Edge(0, 1, Direction.Down),
                Edge(1, 0, Direction.Left));
        }

        [TestCase(1, 1)]
        [TestCase(1, 3)]
        [TestCase(2, 2)]
        public void EdgesAndInteriorEdges_PartitionEverySideOfEveryCell(int width, int height)
        {
            // The two sets are complements, and the editor relies on that: a side
            // in neither is a grid line left inside a block, and a side in both
            // is an erasure painted over a stroke.
            var cells = new List<Coord>();
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    cells.Add(new Coord(x, y));
                }
            }

            var boundary = BlockOutline.Edges(cells).Select(e => e.ToString()).ToList();
            var interior = BlockOutline.InteriorEdges(cells).Select(e => e.ToString()).ToList();

            Assert.AreEqual(cells.Count * 4, boundary.Count + interior.Count);
            CollectionAssert.IsEmpty(boundary.Intersect(interior).ToList());
        }

        [Test]
        public void InteriorEdges_NoCells_IsEmpty()
        {
            Assert.AreEqual(0, BlockOutline.InteriorEdges(new Coord[0]).Count);
            Assert.AreEqual(0, BlockOutline.InteriorEdges(null).Count);
        }
    }
}
