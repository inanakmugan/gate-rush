using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 11's <see cref="BoardLayout"/>: grid and world positions
    /// convert both ways, and the fitted camera shows the whole board plus its
    /// margin whatever the screen's shape.
    /// </summary>
    public class BoardLayoutTests
    {
        private const int Width = 5;
        private const int Height = 3;
        private const float CellSize = 1.5f;
        private const float MarginCells = 1f;
        private const float Tolerance = 1e-4f;

        [Test]
        public void WorldToCell_OfEveryCellCentreOnANonSquareBoard_RoundTripsToTheCell()
        {
            var layout = new BoardLayout(Width, Height, CellSize);

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var cell = new Coord(x, y);

                    var roundTrip = layout.WorldToCell(layout.CellCenter(cell));

                    Assert.AreEqual(cell, roundTrip);
                }
            }
        }

        [TestCase(0.5f, TestName = "FitOrthographicSize_PortraitAspect_ContainsBoardAndMargin")]
        [TestCase(2f, TestName = "FitOrthographicSize_LandscapeAspect_ContainsBoardAndMargin")]
        public void FitOrthographicSize_ContainsBoardAndMarginAndIsTightInOneDirection(float aspect)
        {
            var layout = new BoardLayout(Width, Height, CellSize);
            var neededHalfHeight = (Height * 0.5f + MarginCells) * CellSize;
            var neededHalfWidth = (Width * 0.5f + MarginCells) * CellSize;

            var size = layout.FitOrthographicSize(aspect, MarginCells);

            var visibleHalfHeight = size;
            var visibleHalfWidth = size * aspect;
            Assert.GreaterOrEqual(visibleHalfHeight + Tolerance, neededHalfHeight);
            Assert.GreaterOrEqual(visibleHalfWidth + Tolerance, neededHalfWidth);
            Assert.IsTrue(
                Mathf.Abs(visibleHalfHeight - neededHalfHeight) < Tolerance
                || Mathf.Abs(visibleHalfWidth - neededHalfWidth) < Tolerance,
                "the tighter direction should fit exactly, not leave extra room");
        }
    }
}
