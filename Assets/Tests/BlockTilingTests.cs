using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BlockTiling"/>: every footprint cell is split into four
    /// quarters, each showing the piece its neighbours call for, so a block
    /// reads as one rounded shape (Module 15).
    /// </summary>
    public class BlockTilingTests
    {
        private static Coord C(int x, int y) => new Coord(x, y);

        private static QuarterPiece PieceAt(IReadOnlyList<QuarterTile> tiles, Coord cell, Quarter corner) =>
            tiles.Single(t => t.Cell == cell && t.Corner == corner).Piece;

        [Test]
        public void Compute_SingleCell_IsFourOuterCorners()
        {
            var cells = new[] { C(0, 0) };

            var tiles = BlockTiling.Compute(cells);

            Assert.AreEqual(4, tiles.Count);
            Assert.IsTrue(tiles.All(t => t.Piece == QuarterPiece.OuterCorner));
        }

        [Test]
        public void Compute_HorizontalOneByTwo_HasOuterCornersAtItsEndsAndEdgesAlongXWhereTheCellsMeet()
        {
            var cells = new[] { C(0, 0), C(1, 0) };

            var tiles = BlockTiling.Compute(cells);

            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 0), Quarter.BottomLeft));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 0), Quarter.TopLeft));
            Assert.AreEqual(QuarterPiece.EdgeAlongX, PieceAt(tiles, C(0, 0), Quarter.BottomRight));
            Assert.AreEqual(QuarterPiece.EdgeAlongX, PieceAt(tiles, C(0, 0), Quarter.TopRight));
            Assert.AreEqual(QuarterPiece.EdgeAlongX, PieceAt(tiles, C(1, 0), Quarter.BottomLeft));
            Assert.AreEqual(QuarterPiece.EdgeAlongX, PieceAt(tiles, C(1, 0), Quarter.TopLeft));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(1, 0), Quarter.BottomRight));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(1, 0), Quarter.TopRight));
        }

        [Test]
        public void Compute_VerticalOneByTwo_MirrorsTheHorizontalWithEdgesAlongY()
        {
            var cells = new[] { C(0, 0), C(0, 1) };

            var tiles = BlockTiling.Compute(cells);

            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 0), Quarter.BottomLeft));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 0), Quarter.BottomRight));
            Assert.AreEqual(QuarterPiece.EdgeAlongY, PieceAt(tiles, C(0, 0), Quarter.TopLeft));
            Assert.AreEqual(QuarterPiece.EdgeAlongY, PieceAt(tiles, C(0, 0), Quarter.TopRight));
            Assert.AreEqual(QuarterPiece.EdgeAlongY, PieceAt(tiles, C(0, 1), Quarter.BottomLeft));
            Assert.AreEqual(QuarterPiece.EdgeAlongY, PieceAt(tiles, C(0, 1), Quarter.BottomRight));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 1), Quarter.TopLeft));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 1), Quarter.TopRight));
        }

        [Test]
        public void Compute_TwoByTwo_HasFourOuterCornersAndFillsTheMiddleQuarters()
        {
            var cells = new[] { C(0, 0), C(1, 0), C(0, 1), C(1, 1) };

            var tiles = BlockTiling.Compute(cells);

            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 0), Quarter.BottomLeft));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(1, 0), Quarter.BottomRight));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(0, 1), Quarter.TopLeft));
            Assert.AreEqual(QuarterPiece.OuterCorner, PieceAt(tiles, C(1, 1), Quarter.TopRight));
            Assert.AreEqual(QuarterPiece.Fill, PieceAt(tiles, C(0, 0), Quarter.TopRight));
            Assert.AreEqual(QuarterPiece.Fill, PieceAt(tiles, C(1, 0), Quarter.TopLeft));
            Assert.AreEqual(QuarterPiece.Fill, PieceAt(tiles, C(0, 1), Quarter.BottomRight));
            Assert.AreEqual(QuarterPiece.Fill, PieceAt(tiles, C(1, 1), Quarter.BottomLeft));
            Assert.AreEqual(4, tiles.Count(t => t.Piece == QuarterPiece.OuterCorner));
            Assert.AreEqual(4, tiles.Count(t => t.Piece == QuarterPiece.Fill));
        }

        [Test]
        public void Compute_LShape_HasExactlyOneConcaveCornerAtTheInsideOfTheBend()
        {
            var cells = new[] { C(0, 0), C(1, 0), C(0, 1) };

            var tiles = BlockTiling.Compute(cells);

            var concave = tiles.Where(t => t.Piece == QuarterPiece.ConcaveCorner).ToList();
            Assert.AreEqual(1, concave.Count);
            Assert.AreEqual(C(0, 0), concave[0].Cell);
            Assert.AreEqual(Quarter.TopRight, concave[0].Corner);
        }

        [Test]
        public void Compute_EveryPresetShape_GivesEveryCellExactlyFourQuartersOnePerCorner()
        {
            var presets = Enum.GetValues(typeof(ShapePreset)).Cast<ShapePreset>().Where(p => p != ShapePreset.Free).ToList();
            Assert.GreaterOrEqual(presets.Count, 10, "every fixed preset should be checked");

            foreach (var preset in presets)
            {
                var cells = ShapePresets.Cells(preset);

                var tiles = BlockTiling.Compute(cells);

                Assert.AreEqual(cells.Count * 4, tiles.Count, $"{preset}: four quarters per cell");
                foreach (var cell in cells)
                {
                    var corners = tiles.Where(t => t.Cell == cell).Select(t => t.Corner).OrderBy(c => c).ToList();
                    CollectionAssert.AreEqual(
                        new[] { Quarter.BottomLeft, Quarter.BottomRight, Quarter.TopLeft, Quarter.TopRight },
                        corners,
                        $"{preset}: cell {cell} has one quarter per corner");
                }
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Compute_RotatedLShape_ProducesTheSamePiecesInRotatedPositions(int quarterTurns)
        {
            var cells = new[] { C(0, 0), C(1, 0), C(0, 1) };
            var rotatedCells = cells.Select(c => Rotate(c, quarterTurns)).ToArray();

            var tiles = BlockTiling.Compute(cells);
            var rotatedTiles = BlockTiling.Compute(rotatedCells);

            foreach (var tile in tiles)
            {
                var expected = new QuarterTile(
                    Rotate(tile.Cell, quarterTurns),
                    Rotate(tile.Corner, quarterTurns),
                    Rotate(tile.Piece, quarterTurns));
                CollectionAssert.Contains(rotatedTiles, expected, $"{tile} turned {quarterTurns} quarter(s)");
            }

            Assert.AreEqual(tiles.Count, rotatedTiles.Count);
        }

        [Test]
        public void Compute_NullCells_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BlockTiling.Compute(null));
        }

        /// <summary>A quarter turn counter-clockwise, applied <paramref name="turns"/> times: (x, y) → (−y, x).</summary>
        private static Coord Rotate(Coord cell, int turns)
        {
            for (var i = 0; i < turns; i++)
            {
                cell = new Coord(-cell.Y, cell.X);
            }

            return cell;
        }

        private static Quarter Rotate(Quarter corner, int turns)
        {
            var sx = QuarterTile.SignXOf(corner);
            var sy = QuarterTile.SignYOf(corner);
            for (var i = 0; i < turns; i++)
            {
                var turnedX = -sy;
                sy = sx;
                sx = turnedX;
            }

            if (sx > 0)
            {
                return sy > 0 ? Quarter.TopRight : Quarter.BottomRight;
            }

            return sy > 0 ? Quarter.TopLeft : Quarter.BottomLeft;
        }

        /// <summary>An odd number of quarter turns swaps which axis an edge runs along; every other piece keeps its kind.</summary>
        private static QuarterPiece Rotate(QuarterPiece piece, int turns)
        {
            if (turns % 2 == 0)
            {
                return piece;
            }

            switch (piece)
            {
                case QuarterPiece.EdgeAlongX:
                    return QuarterPiece.EdgeAlongY;
                case QuarterPiece.EdgeAlongY:
                    return QuarterPiece.EdgeAlongX;
                default:
                    return piece;
            }
        }
    }
}
