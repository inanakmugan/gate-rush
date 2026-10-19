using System;
using GateRush.UI;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 35's <see cref="UiNavigation"/>: it starts on Home with
    /// no overlay; selecting a tab closes every overlay; overlays stack, each
    /// at most once; Back closes the top overlay, then returns from Store or
    /// Leaderboard to Home, and on Home does nothing; and
    /// <see cref="UiNavigation.Changed"/> is raised once per change and never
    /// when nothing changed.
    /// </summary>
    public class UiNavigationTests
    {
        private UiNavigation navigation;
        private int changes;

        [SetUp]
        public void SetUp()
        {
            navigation = new UiNavigation();
            changes = 0;
            navigation.Changed += OnChanged;
        }

        [TearDown]
        public void TearDown()
        {
            navigation.Changed -= OnChanged;
        }

        [Test]
        public void New_StartsOnHomeWithNoOverlay()
        {
            var fresh = new UiNavigation();

            Assert.AreEqual(UiTab.Home, fresh.Tab);
            Assert.AreEqual(0, fresh.Overlays.Count);
        }

        [Test]
        public void SelectTab_WithOverlaysOpen_ClosesEveryOverlayInOneChange()
        {
            navigation.Open(UiOverlay.Settings);
            navigation.Open(UiOverlay.Levels);
            changes = 0;

            navigation.SelectTab(UiTab.Store);

            Assert.AreEqual(UiTab.Store, navigation.Tab);
            Assert.AreEqual(0, navigation.Overlays.Count);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SelectTab_CurrentTabWithOverlays_ClosesThem()
        {
            navigation.Open(UiOverlay.Lives);
            changes = 0;

            navigation.SelectTab(UiTab.Home);

            Assert.AreEqual(0, navigation.Overlays.Count);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SelectTab_CurrentTabWithNoOverlay_RaisesNothing()
        {
            navigation.SelectTab(UiTab.Home);

            Assert.AreEqual(0, changes);
        }

        [Test]
        public void Open_TwoOverlays_StacksTheNewestOnTop()
        {
            navigation.Open(UiOverlay.Settings);
            navigation.Open(UiOverlay.Levels);

            CollectionAssert.AreEqual(new[] { UiOverlay.Settings, UiOverlay.Levels }, navigation.Overlays);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Open_OverlayAlreadyOnTop_RaisesNothing()
        {
            navigation.Open(UiOverlay.Lives);
            changes = 0;

            navigation.Open(UiOverlay.Lives);

            CollectionAssert.AreEqual(new[] { UiOverlay.Lives }, navigation.Overlays);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void Open_OverlayLowerInTheStack_MovesItToTheTop()
        {
            navigation.Open(UiOverlay.Settings);
            navigation.Open(UiOverlay.Levels);
            changes = 0;

            navigation.Open(UiOverlay.Settings);

            CollectionAssert.AreEqual(new[] { UiOverlay.Levels, UiOverlay.Settings }, navigation.Overlays);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Back_WithTwoOverlays_ClosesTheTopThenTheNext()
        {
            navigation.SelectTab(UiTab.Store);
            navigation.Open(UiOverlay.Settings);
            navigation.Open(UiOverlay.Levels);
            changes = 0;

            var first = navigation.Back();
            var afterFirst = new[] { navigation.Overlays[0] };
            var second = navigation.Back();

            Assert.IsTrue(first);
            CollectionAssert.AreEqual(new[] { UiOverlay.Settings }, afterFirst);
            Assert.IsTrue(second);
            Assert.AreEqual(0, navigation.Overlays.Count);
            Assert.AreEqual(UiTab.Store, navigation.Tab, "closing overlays keeps the tab");
            Assert.AreEqual(2, changes);
        }

        [TestCase(UiTab.Store)]
        [TestCase(UiTab.Leaderboard)]
        public void Back_NoOverlayOnAnotherTab_ReturnsToHome(UiTab tab)
        {
            navigation.SelectTab(tab);
            changes = 0;

            var isBack = navigation.Back();

            Assert.IsTrue(isBack);
            Assert.AreEqual(UiTab.Home, navigation.Tab);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Back_NoOverlayOnHome_IsFalseAndRaisesNothing()
        {
            var isBack = navigation.Back();

            Assert.IsFalse(isBack);
            Assert.AreEqual(UiTab.Home, navigation.Tab);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void Reset_FromATabWithOverlays_HomeWithNoOverlayInOneChange()
        {
            navigation.SelectTab(UiTab.Leaderboard);
            navigation.Open(UiOverlay.Settings);
            changes = 0;

            navigation.Reset();

            Assert.AreEqual(UiTab.Home, navigation.Tab);
            Assert.AreEqual(0, navigation.Overlays.Count);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Reset_AlreadyHomeWithNoOverlay_RaisesNothing()
        {
            navigation.Reset();

            Assert.AreEqual(0, changes);
        }

        [Test]
        public void SelectTab_UndeclaredTab_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => navigation.SelectTab((UiTab)(-1)));
        }

        [Test]
        public void Open_UndeclaredOverlay_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => navigation.Open((UiOverlay)(-1)));
        }

        private void OnChanged()
        {
            changes++;
        }
    }
}
