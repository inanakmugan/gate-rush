using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 18's <see cref="BurstLayout"/>: the same seed lays out the
    /// same pieces, the count never exceeds the cap, and every piece moves out
    /// through the gate's edge — or, in a radial burst, away from the centre.
    /// </summary>
    public class BurstLayoutTests
    {
        private const int PerCell = 4;
        private const int Cap = 16;
        private const float Spread = 35f;

        private static readonly Coord[] LShape = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

        [Test]
        public void Pieces_SameSeed_GiveTheSamePieces()
        {
            var seed = BurstLayout.Seed(BurstKind.Exit, 3, 7);

            var first = BurstLayout.Pieces(Areas(LShape), Vector2.right, Vector2.zero, seed, Settings());
            var second = BurstLayout.Pieces(Areas(LShape), Vector2.right, Vector2.zero, BurstLayout.Seed(BurstKind.Exit, 3, 7), Settings());

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Pieces_DifferentSeeds_GiveDifferentPieces()
        {
            var first = BurstLayout.Pieces(Areas(LShape), Vector2.right, Vector2.zero, BurstLayout.Seed(BurstKind.Exit, 3, 7), Settings());

            var second = BurstLayout.Pieces(Areas(LShape), Vector2.right, Vector2.zero, BurstLayout.Seed(BurstKind.Exit, 3, 8), Settings());

            CollectionAssert.AreNotEqual(first, second);
        }

        [Test]
        public void Pieces_ManyCells_NeverExceedTheCap()
        {
            var cells = new List<Coord>();
            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    cells.Add(new Coord(x, y));
                }
            }

            var pieces = BurstLayout.Pieces(Areas(cells), Vector2.up, Vector2.zero, 1, Settings());

            Assert.Greater(PerCell * cells.Count, Cap, "the fixture asks for more pieces than the cap");
            Assert.AreEqual(Cap, pieces.Count);
        }

        [Test]
        public void Pieces_FewCells_ArePerCellTimesCells()
        {
            var pieces = BurstLayout.Pieces(Areas(LShape), Vector2.up, Vector2.zero, 1, Settings());

            Assert.AreEqual(PerCell * LShape.Length, pieces.Count);
        }

        [TestCase(BoardEdge.Top)]
        [TestCase(BoardEdge.Bottom)]
        [TestCase(BoardEdge.Left)]
        [TestCase(BoardEdge.Right)]
        public void Pieces_EveryEdge_EveryPieceMovesOutThroughThatEdge(BoardEdge edge)
        {
            var outward = Outward(edge);
            var checkedPieces = 0;

            for (var seed = 0; seed < 50; seed++)
            {
                foreach (var piece in BurstLayout.Pieces(Areas(LShape), outward, Vector2.zero, seed, Settings()))
                {
                    Assert.Greater(Vector2.Dot(piece.Travel, outward), 0f, $"seed {seed}: {piece}");
                    checkedPieces++;
                }
            }

            Assert.GreaterOrEqual(checkedPieces, 50 * PerCell * LShape.Length);
        }

        [Test]
        public void Pieces_Radial_EveryPieceMovesAwayFromTheCentre()
        {
            var centre = new Vector2(1f, 1f);
            var checkedPieces = 0;

            for (var seed = 0; seed < 50; seed++)
            {
                foreach (var piece in BurstLayout.Pieces(Areas(LShape), null, centre, seed, Settings()))
                {
                    Assert.Greater(Vector2.Dot(piece.Travel, piece.Start - centre), 0f, $"seed {seed}: {piece}");
                    checkedPieces++;
                }
            }

            Assert.GreaterOrEqual(checkedPieces, 50 * PerCell * LShape.Length);
        }

        [Test]
        public void Pieces_Always_StartInsideTheirAreasWithSizesAndDelaysInRange()
        {
            var settings = Settings();

            var pieces = BurstLayout.Pieces(Areas(LShape), Vector2.left, Vector2.zero, 11, settings);

            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                var cell = LShape[i % LShape.Length];
                Assert.IsTrue(new Rect(cell.X, cell.Y, 1f, 1f).Contains(piece.Start), $"piece {i} in its cell");
                Assert.That(piece.Size, Is.InRange(settings.SizeMinCells, settings.SizeMaxCells));
                Assert.That(piece.Travel.magnitude, Is.InRange(settings.TravelMinCells - 1e-4f, settings.TravelMaxCells + 1e-4f));
                Assert.That(piece.DelaySeconds, Is.InRange(0f, settings.MaxDelaySeconds));
            }
        }

        // A 5x4 board with a gate at offset 1, two cells wide, under a frame
        // 0.45 cells thick; a three-cell block passes in 0.36 s.
        private const int BoardWidth = 5;
        private const int BoardHeight = 4;
        private const int GateOffset = 1;
        private const int GateSpan = 2;
        private const float FrameThickness = 0.45f;
        private const int BlockCells = 3;
        private const float PassSeconds = 0.36f;

        [Test]
        public void Stream_SameSeed_GivesTheSamePieces()
        {
            var first = Stream(BoardEdge.Bottom, BlockCells, BurstLayout.Seed(BurstKind.Exit, 2, 5));

            var second = Stream(BoardEdge.Bottom, BlockCells, BurstLayout.Seed(BurstKind.Exit, 2, 5));

            CollectionAssert.AreEqual(first, second);
        }

        [TestCase(BoardEdge.Top)]
        [TestCase(BoardEdge.Bottom)]
        [TestCase(BoardEdge.Left)]
        [TestCase(BoardEdge.Right)]
        public void Stream_EveryEdge_EveryStartLiesOnTheGatesOuterLineWithinTheSpan(BoardEdge edge)
        {
            // The outer line: the grid's edge pushed outward by the frame's
            // thickness, across the gate's span.
            const float Tolerance = 1e-5f;
            var checkedPieces = 0;

            for (var seed = 0; seed < 50; seed++)
            {
                foreach (var piece in Stream(edge, BlockCells, seed))
                {
                    var along = edge == BoardEdge.Left || edge == BoardEdge.Right ? piece.Start.y : piece.Start.x;
                    var across = edge == BoardEdge.Left || edge == BoardEdge.Right ? piece.Start.x : piece.Start.y;
                    var line = edge == BoardEdge.Bottom ? -FrameThickness
                        : edge == BoardEdge.Left ? -FrameThickness
                        : edge == BoardEdge.Top ? BoardHeight + FrameThickness
                        : BoardWidth + FrameThickness;

                    Assert.AreEqual(line, across, Tolerance, $"seed {seed}: {piece} on the outer line");
                    Assert.That(along, Is.InRange(GateOffset - Tolerance, GateOffset + GateSpan + Tolerance), $"seed {seed}: {piece} within the span");
                    checkedPieces++;
                }
            }

            Assert.GreaterOrEqual(checkedPieces, 50 * PerCell * BlockCells);
        }

        [Test]
        public void Stream_EveryDelay_LiesWithinThePassDuration()
        {
            var checkedPieces = 0;

            for (var seed = 0; seed < 50; seed++)
            {
                foreach (var piece in Stream(BoardEdge.Right, BlockCells, seed))
                {
                    Assert.That(piece.DelaySeconds, Is.InRange(0f, PassSeconds), $"seed {seed}: {piece}");
                    checkedPieces++;
                }
            }

            Assert.GreaterOrEqual(checkedPieces, 50 * PerCell * BlockCells);
        }

        [Test]
        public void Stream_Delays_FillEverySliceOfThePass()
        {
            var pieces = Stream(BoardEdge.Top, BlockCells, 4);
            var slices = new int[pieces.Count];

            foreach (var piece in pieces)
            {
                var slice = Mathf.Min((int)(piece.DelaySeconds / PassSeconds * pieces.Count), pieces.Count - 1);
                slices[slice]++;
            }

            CollectionAssert.AreEqual(Filled(pieces.Count, 1), slices, "one start in each n-th of the pass: a stream, not a burst");
        }

        [TestCase(BoardEdge.Top)]
        [TestCase(BoardEdge.Bottom)]
        [TestCase(BoardEdge.Left)]
        [TestCase(BoardEdge.Right)]
        public void Stream_EveryEdge_EveryTravelPointsOutward(BoardEdge edge)
        {
            var outward = Outward(edge);
            var checkedPieces = 0;

            for (var seed = 0; seed < 50; seed++)
            {
                foreach (var piece in Stream(edge, BlockCells, seed))
                {
                    Assert.Greater(Vector2.Dot(piece.Travel, outward), 0f, $"seed {seed}: {piece}");
                    checkedPieces++;
                }
            }

            Assert.GreaterOrEqual(checkedPieces, 50 * PerCell * BlockCells);
        }

        [Test]
        public void Stream_ManyCells_NeverExceedsTheCap()
        {
            const int ManyCells = 9;

            var pieces = Stream(BoardEdge.Left, ManyCells, 1);

            Assert.Greater(PerCell * ManyCells, Cap, "the fixture asks for more pieces than the cap");
            Assert.AreEqual(Cap, pieces.Count);
        }

        [Test]
        public void Stream_FewCells_IsPerCellTimesCells()
        {
            var pieces = Stream(BoardEdge.Left, BlockCells, 1);

            Assert.AreEqual(PerCell * BlockCells, pieces.Count);
        }

        private static IReadOnlyList<BurstPiece> Stream(BoardEdge edge, int cells, int seed) =>
            BurstLayout.Stream(
                BoardWidth, BoardHeight, edge, GateOffset, GateSpan, FrameThickness, cells, PassSeconds, seed, Settings());

        private static int[] Filled(int count, int value)
        {
            var values = new int[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = value;
            }

            return values;
        }

        private static BurstSettings Settings() =>
            new BurstSettings(PerCell, Cap, 0.16f, 0.26f, 0.8f, 1.6f, Spread, 270f, 0.04f);

        private static Rect[] Areas(IReadOnlyList<Coord> cells)
        {
            var areas = new Rect[cells.Count];
            for (var i = 0; i < cells.Count; i++)
            {
                areas[i] = new Rect(cells[i].X, cells[i].Y, 1f, 1f);
            }

            return areas;
        }

        private static Vector2 Outward(BoardEdge edge)
        {
            switch (edge)
            {
                case BoardEdge.Top:
                    return Vector2.up;
                case BoardEdge.Bottom:
                    return Vector2.down;
                case BoardEdge.Left:
                    return Vector2.left;
                default:
                    return Vector2.right;
            }
        }
    }
}
