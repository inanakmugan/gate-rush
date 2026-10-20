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
    /// when nothing changed. Module 36's Not Enough Gold, Message and Test
    /// Purchase overlays stack and close with Back like the others, and Get
    /// Gold — selecting the Store — closes every overlay. Module 37's Win
    /// Streak and Streak Reward overlays stack and close with Back too, and
    /// Module 38's Profile opens over any tab and closes with Back.
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

        [TestCase(UiOverlay.NotEnoughGold)]
        [TestCase(UiOverlay.Message)]
        [TestCase(UiOverlay.TestPurchase)]
        public void Open_StoreOverlayOverLives_StacksOnTop(UiOverlay overlay)
        {
            navigation.Open(UiOverlay.Lives);
            changes = 0;

            navigation.Open(overlay);

            CollectionAssert.AreEqual(new[] { UiOverlay.Lives, overlay }, navigation.Overlays);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Back_NotEnoughGoldMessageAndTestPurchaseStacked_ClosesThemTopFirst()
        {
            navigation.SelectTab(UiTab.Store);
            navigation.Open(UiOverlay.NotEnoughGold);
            navigation.Open(UiOverlay.Message);
            navigation.Open(UiOverlay.TestPurchase);

            navigation.Back();
            var afterFirst = new[] { navigation.Overlays[0], navigation.Overlays[1] };
            navigation.Back();
            var afterSecond = new[] { navigation.Overlays[0] };
            navigation.Back();

            CollectionAssert.AreEqual(new[] { UiOverlay.NotEnoughGold, UiOverlay.Message }, afterFirst);
            CollectionAssert.AreEqual(new[] { UiOverlay.NotEnoughGold }, afterSecond);
            Assert.AreEqual(0, navigation.Overlays.Count);
            Assert.AreEqual(UiTab.Store, navigation.Tab);
        }

        [Test]
        public void SelectTab_GetGoldFromNotEnoughGoldOverLives_StoreWithNoOverlayInOneChange()
        {
            navigation.Open(UiOverlay.Lives);
            navigation.Open(UiOverlay.NotEnoughGold);
            changes = 0;

            navigation.SelectTab(UiTab.Store);

            Assert.AreEqual(UiTab.Store, navigation.Tab);
            Assert.AreEqual(0, navigation.Overlays.Count);
            Assert.AreEqual(1, changes);
        }

        [TestCase(UiOverlay.WinStreak)]
        [TestCase(UiOverlay.StreakReward)]
        public void Back_StreakOverlayOnHome_ClosesItBackToHome(UiOverlay overlay)
        {
            navigation.Open(overlay);
            changes = 0;

            var isBack = navigation.Back();

            Assert.IsTrue(isBack);
            Assert.AreEqual(0, navigation.Overlays.Count);
            Assert.AreEqual(UiTab.Home, navigation.Tab);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Back_StreakRewardOverWinStreak_ClosesTheRewardFirst()
        {
            navigation.Open(UiOverlay.WinStreak);
            navigation.Open(UiOverlay.StreakReward);
            var stacked = new[] { navigation.Overlays[0], navigation.Overlays[1] };

            navigation.Back();
            var afterFirst = new[] { navigation.Overlays[0] };
            navigation.Back();

            CollectionAssert.AreEqual(new[] { UiOverlay.WinStreak, UiOverlay.StreakReward }, stacked);
            CollectionAssert.AreEqual(new[] { UiOverlay.WinStreak }, afterFirst);
            Assert.AreEqual(0, navigation.Overlays.Count);
        }

        [Test]
        public void Back_ProfileOverATab_ClosesItAndKeepsTheTab([Values] UiTab tab)
        {
            navigation.SelectTab(tab);
            navigation.Open(UiOverlay.Profile);
            var opened = new[] { navigation.Overlays[0] };
            changes = 0;

            var isBack = navigation.Back();

            CollectionAssert.AreEqual(new[] { UiOverlay.Profile }, opened);
            Assert.IsTrue(isBack);
            Assert.AreEqual(0, navigation.Overlays.Count);
            Assert.AreEqual(tab, navigation.Tab, "the top bar is on every tab, and Back closes only Profile");
            Assert.AreEqual(1, changes);
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
