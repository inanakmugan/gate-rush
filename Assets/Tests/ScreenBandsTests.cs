using System;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 17's <see cref="ScreenBands"/>: the HUD band starts at the
    /// safe area's top and spans its width, the camera keeps free each band
    /// plus the unsafe strip on its side — the bands alone when the safe area
    /// is the whole screen, and when insets would leave no room — and content
    /// is scaled down only when it does not fit.
    /// </summary>
    public class ScreenBandsTests
    {
        private const float Tolerance = 1e-5f;
        private const int Width = 1000;
        private const int Height = 2000;
        private const float TopBand = 0.14f;
        private const float BottomBand = 0.1f;

        private static readonly Rect FullScreen = new Rect(0f, 0f, Width, Height);

        // A notch of 100 px at the top and a home indicator of 100 px at the bottom.
        private static readonly Rect Notched = new Rect(0f, 100f, Width, Height - 200f);

        [Test]
        public void HudBandAnchors_FullSafeArea_IsTheTopBandAcrossTheScreen()
        {
            var band = ScreenBands.HudBandAnchors(FullScreen, Width, Height, TopBand);

            AssertRect(Rect.MinMaxRect(0f, 1f - TopBand, 1f, 1f), band);
        }

        [Test]
        public void HudBandAnchors_Notch_StartsAtTheSafeTop()
        {
            var band = ScreenBands.HudBandAnchors(Notched, Width, Height, TopBand);

            AssertRect(Rect.MinMaxRect(0f, 0.95f - TopBand, 1f, 0.95f), band);
        }

        [Test]
        public void HudBandAnchors_SideInsets_SpansTheSafeWidth()
        {
            var safe = new Rect(50f, 0f, Width - 100f, Height);

            var band = ScreenBands.HudBandAnchors(safe, Width, Height, TopBand);

            Assert.AreEqual(0.05f, band.xMin, Tolerance);
            Assert.AreEqual(0.95f, band.xMax, Tolerance);
        }

        [Test]
        public void SafeAnchors_SafeAreaPastTheScreen_IsClampedToIt()
        {
            var safe = new Rect(-10f, -10f, Width + 20f, Height + 20f);

            var anchors = ScreenBands.SafeAnchors(safe, Width, Height);

            AssertRect(Rect.MinMaxRect(0f, 0f, 1f, 1f), anchors);
        }

        [Test]
        public void TryAddInsets_FullSafeArea_KeepsTheBandsUnchanged()
        {
            var isAdded = ScreenBands.TryAddInsets(TopBand, BottomBand, FullScreen, Height, out var top, out var bottom);

            Assert.IsTrue(isAdded);
            Assert.AreEqual(TopBand, top, Tolerance);
            Assert.AreEqual(BottomBand, bottom, Tolerance);
        }

        [Test]
        public void TryAddInsets_Notch_AddsTheUnsafeStripToEachBand()
        {
            var isAdded = ScreenBands.TryAddInsets(TopBand, BottomBand, Notched, Height, out var top, out var bottom);

            Assert.IsTrue(isAdded);
            Assert.AreEqual(TopBand + 0.05f, top, Tolerance);
            Assert.AreEqual(BottomBand + 0.05f, bottom, Tolerance);
        }

        [Test]
        public void TryAddInsets_InsetsLeaveNoRoom_FailsAndGivesTheBandsAlone()
        {
            // 0.5 + 0.05 and 0.4 + 0.05 sum to 1: no room for the board.
            var isAdded = ScreenBands.TryAddInsets(0.5f, 0.4f, Notched, Height, out var top, out var bottom);

            Assert.IsFalse(isAdded);
            Assert.AreEqual(0.5f, top, Tolerance);
            Assert.AreEqual(0.4f, bottom, Tolerance);
        }

        [Test]
        public void HudBandAnchors_Notch_EndsWhereTheCameraFitsTheBoardsTop()
        {
            // The two share one rule: the band's bottom is the screen height
            // less the band the camera keeps free at the top.
            ScreenBands.TryAddInsets(TopBand, BottomBand, Notched, Height, out var top, out _);

            var band = ScreenBands.HudBandAnchors(Notched, Width, Height, TopBand);

            Assert.AreEqual(1f - top, band.yMin, Tolerance);
        }

        [Test]
        public void ContentScale_ContentFits_IsOne()
        {
            var scale = ScreenBands.ContentScale(new Vector2(1080f, 269f), new Vector2(920f, 120f));

            Assert.AreEqual(1f, scale);
        }

        [Test]
        public void ContentScale_TooWide_ShrinksToTheWidth()
        {
            var scale = ScreenBands.ContentScale(new Vector2(460f, 269f), new Vector2(920f, 120f));

            Assert.AreEqual(0.5f, scale, Tolerance);
        }

        [Test]
        public void ContentScale_TooShort_ShrinksToTheHeight()
        {
            var scale = ScreenBands.ContentScale(new Vector2(1080f, 60f), new Vector2(920f, 120f));

            Assert.AreEqual(0.5f, scale, Tolerance);
        }

        [Test]
        public void ContentScale_NoRoom_IsZero()
        {
            var scale = ScreenBands.ContentScale(Vector2.zero, new Vector2(920f, 120f));

            Assert.AreEqual(0f, scale);
        }

        [Test]
        public void ContentScale_EmptyContent_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenBands.ContentScale(Vector2.one, Vector2.zero));
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.xMin, actual.xMin, Tolerance, "xMin");
            Assert.AreEqual(expected.yMin, actual.yMin, Tolerance, "yMin");
            Assert.AreEqual(expected.xMax, actual.xMax, Tolerance, "xMax");
            Assert.AreEqual(expected.yMax, actual.yMax, Tolerance, "yMax");
        }
    }
}
