using System;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BoardLayout"/>: grid and world positions convert both
    /// ways, and the fitted camera shows the board plus its frame across the
    /// screen's width less the side margins, with the HUD bands — fractions of
    /// the screen's height — kept free above and below it (Module 15).
    /// </summary>
    public class BoardLayoutTests
    {
        private const int Width = 5;
        private const int Height = 3;
        private const float CellSize = 1.5f;
        private const float Frame = 0.4f;
        private const float SideMargin = 0.3f;
        private const float TopBand = 0.14f;
        private const float BottomBand = 0.10f;
        private const float Tolerance = 1e-4f;

        private const float FramedHalfWidth = (Width * 0.5f + Frame) * CellSize;
        private const float FramedHalfHeight = (Height * 0.5f + Frame) * CellSize;

        [Test]
        public void WorldToCell_OfEveryCellCentreOnANonSquareBoard_RoundTripsToTheCell()
        {
            var layout = new BoardLayout(Width, Height, CellSize, Frame);

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

        [Test]
        public void FitOrthographicSize_PortraitAspect_BoardPlusFrameFillsTheWidthLessTheSideMargins()
        {
            const float aspect = 9f / 19.5f;
            var layout = new BoardLayout(Width, Height, CellSize, Frame);

            var size = layout.FitOrthographicSize(aspect, SideMargin, TopBand, BottomBand);

            var visibleHalfWidth = size * aspect;
            Assert.AreEqual(FramedHalfWidth + SideMargin * CellSize, visibleHalfWidth, Tolerance);
        }

        [Test]
        public void FitOrthographicSize_PortraitAspect_KeepsBothBandsFreeWithTheBoardCentredBetweenThem()
        {
            const float aspect = 9f / 19.5f;
            var layout = new BoardLayout(Width, Height, CellSize, Frame);

            var size = layout.FitOrthographicSize(aspect, SideMargin, TopBand, BottomBand);
            var cameraY = layout.CameraCenterOffset(size, TopBand, BottomBand).y;

            var screenTop = cameraY + size;
            var screenBottom = cameraY - size;
            var freeTop = screenTop - TopBand * 2f * size;
            var freeBottom = screenBottom + BottomBand * 2f * size;
            Assert.LessOrEqual(FramedHalfHeight, freeTop + Tolerance, "the top band stays free");
            Assert.GreaterOrEqual(-FramedHalfHeight, freeBottom - Tolerance, "the bottom band stays free");
            Assert.AreEqual(0f, (freeTop + freeBottom) * 0.5f, Tolerance, "the board is centred between the bands");
        }

        [TestCase(2f)]
        [TestCase(0.9f)]
        public void FitOrthographicSize_LandscapeOrShortAspect_HeightDecidesAndEverythingStaysVisible(float aspect)
        {
            // A board as wide as it is tall: at these aspects the bands leave
            // too little height for the width to decide.
            var layout = new BoardLayout(Height, Height, CellSize, Frame);
            var widthFit = (FramedHalfHeight + SideMargin * CellSize) / aspect;

            var size = layout.FitOrthographicSize(aspect, SideMargin, TopBand, BottomBand);
            var cameraY = layout.CameraCenterOffset(size, TopBand, BottomBand).y;

            Assert.Greater(size, widthFit, "the height decides");
            var freeTop = cameraY + size - TopBand * 2f * size;
            var freeBottom = cameraY - size + BottomBand * 2f * size;
            Assert.AreEqual(FramedHalfHeight, freeTop, Tolerance, "the board fills the height between the bands");
            Assert.AreEqual(-FramedHalfHeight, freeBottom, Tolerance, "the board fills the height between the bands");
            Assert.GreaterOrEqual(size * aspect + Tolerance, FramedHalfHeight + SideMargin * CellSize, "the whole width stays visible");
        }

        [Test]
        public void CameraCenterOffset_EqualBands_IsZero()
        {
            var layout = new BoardLayout(Width, Height, CellSize, Frame);

            var offset = layout.CameraCenterOffset(4f, 0.1f, 0.1f);

            Assert.AreEqual(0f, offset.x, Tolerance);
            Assert.AreEqual(0f, offset.y, Tolerance);
        }

        [Test]
        public void FitOrthographicSize_ReachOnTheLeft_WidensTheViewByItOnThatSideOnly()
        {
            // A generator machine on the left edge reaches this far beyond the
            // frame (D48); the right side keeps its plain margin.
            const float aspect = 9f / 19.5f;
            const float reach = 1.2f;
            var layout = new BoardLayout(Width, Height, CellSize, Frame, new EdgeReach(reach, 0f, 0f, 0f));

            var size = layout.FitOrthographicSize(aspect, SideMargin, TopBand, BottomBand);
            var cameraX = layout.CameraCenterOffset(size, TopBand, BottomBand).x;

            var visibleLeft = cameraX - size * aspect;
            var visibleRight = cameraX + size * aspect;
            Assert.AreEqual(-(FramedHalfWidth + (reach + SideMargin) * CellSize), visibleLeft, Tolerance, "the machine and the margin show on the left");
            Assert.AreEqual(FramedHalfWidth + SideMargin * CellSize, visibleRight, Tolerance, "the right side is as without a machine");
        }

        [Test]
        public void FitOrthographicSize_NoReach_FitsAsWithoutMachines()
        {
            const float aspect = 9f / 19.5f;
            var plain = new BoardLayout(Width, Height, CellSize, Frame);
            var noReach = new BoardLayout(Width, Height, CellSize, Frame, new EdgeReach(0f, 0f, 0f, 0f));

            var plainSize = plain.FitOrthographicSize(aspect, SideMargin, TopBand, BottomBand);
            var noReachSize = noReach.FitOrthographicSize(aspect, SideMargin, TopBand, BottomBand);
            var offset = noReach.CameraCenterOffset(noReachSize, TopBand, BottomBand);

            Assert.AreEqual(plainSize, noReachSize, Tolerance);
            Assert.AreEqual(0f, offset.x, Tolerance);
            Assert.AreEqual(plain.CameraCenterOffset(plainSize, TopBand, BottomBand).y, offset.y, Tolerance);
        }

        [Test]
        public void FitOrthographicSize_ReachAtTheTopInAShortAspect_KeepsTheMachineBetweenTheBands()
        {
            // The height decides here; the machine above the frame must still
            // end below the top band.
            const float aspect = 2f;
            const float reach = 1.2f;
            var layout = new BoardLayout(Height, Height, CellSize, Frame, new EdgeReach(0f, 0f, 0f, reach));

            var size = layout.FitOrthographicSize(aspect, SideMargin, TopBand, BottomBand);
            var cameraY = layout.CameraCenterOffset(size, TopBand, BottomBand).y;

            var freeTop = cameraY + size - TopBand * 2f * size;
            var freeBottom = cameraY - size + BottomBand * 2f * size;
            Assert.AreEqual(FramedHalfHeight + reach * CellSize, freeTop, Tolerance, "the machine reaches the top band");
            Assert.AreEqual(-FramedHalfHeight, freeBottom, Tolerance, "the frame reaches the bottom band");
        }

        [TestCase(0f, SideMargin, TopBand, BottomBand, "Frame Thickness Cells")]
        [TestCase(1.5f, SideMargin, TopBand, BottomBand, "Frame Thickness Cells")]
        [TestCase(Frame, -0.1f, TopBand, BottomBand, "Side Margin Cells")]
        [TestCase(Frame, SideMargin, -0.1f, BottomBand, "Top Band Screen Fraction")]
        [TestCase(Frame, SideMargin, 0.6f, 0.4f, "Top Band Screen Fraction")]
        [TestCase(Frame, SideMargin, float.NaN, BottomBand, "Top Band Screen Fraction")]
        public void Problems_ValueOutOfRange_NamesTheFieldAndTheLayoutRefusesIt(
            float frame, float side, float top, float bottom, string field)
        {
            var problems = BoardLayout.Problems(frame, side, top, bottom);

            Assert.AreEqual(1, problems.Count);
            StringAssert.StartsWith(field, problems[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BoardLayout(Width, Height, CellSize, frame).FitOrthographicSize(1f, side, top, bottom));
        }

        [Test]
        public void Problems_DefaultLikeValues_AreEmpty()
        {
            var problems = BoardLayout.Problems(Frame, SideMargin, TopBand, BottomBand);

            Assert.IsEmpty(problems);
        }

        [Test]
        public void FitOrthographicSize_NonPositiveAspect_Throws()
        {
            var layout = new BoardLayout(Width, Height, CellSize, Frame);

            Assert.Throws<ArgumentOutOfRangeException>(() => layout.FitOrthographicSize(0f, SideMargin, TopBand, BottomBand));
        }
    }
}
