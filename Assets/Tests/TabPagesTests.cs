using System;
using System.Linq;
using GateRush.UI;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="TabPages"/> (Module 47): a selection fades one page in
    /// over the shown one; however fast the tabs are pressed, at most two
    /// pages are active and exactly one once the fade has finished; selecting
    /// the shown page changes nothing; and a selection at once shows one page.
    /// </summary>
    public class TabPagesTests
    {
        private static readonly UiTab[] Tabs = { UiTab.Store, UiTab.Home, UiTab.Leaderboard };

        private TabPages pages;

        [SetUp]
        public void SetUp()
        {
            pages = new TabPages();
        }

        [Test]
        public void New_ShowsHomeAlone()
        {
            Assert.AreEqual(UiTab.Home, pages.Shown);
            Assert.IsFalse(pages.IsFading);
            Assert.AreEqual(1, ActiveCount());
        }

        [Test]
        public void Select_AnotherTabAnimated_FadesItInOverTheShownOne()
        {
            var fades = pages.Select(UiTab.Store, true);

            Assert.IsTrue(fades);
            Assert.AreEqual(UiTab.Home, pages.Shown, "the old page stays under the new one");
            Assert.AreEqual(UiTab.Store, pages.Incoming);
            Assert.AreEqual(2, ActiveCount());
        }

        [Test]
        public void Finish_AfterAFade_LeavesTheNewPageAlone()
        {
            pages.Select(UiTab.Store, true);

            pages.Finish();

            Assert.AreEqual(UiTab.Store, pages.Shown);
            Assert.IsFalse(pages.IsFading);
            Assert.IsTrue(pages.IsActive(UiTab.Store));
            Assert.AreEqual(1, ActiveCount());
        }

        [Test]
        public void Select_DuringAFade_FinishesItFirst()
        {
            pages.Select(UiTab.Store, true);

            pages.Select(UiTab.Leaderboard, true);

            Assert.AreEqual(UiTab.Store, pages.Shown, "the page coming in became the one shown");
            Assert.AreEqual(UiTab.Leaderboard, pages.Incoming);
            Assert.IsFalse(pages.IsActive(UiTab.Home), "the first page is gone");
            Assert.AreEqual(2, ActiveCount());
        }

        [Test]
        public void Select_BackToTheShownPageDuringAFade_FadesItInOverTheOneComingIn()
        {
            pages.Select(UiTab.Store, true);

            var fades = pages.Select(UiTab.Home, true);

            Assert.IsTrue(fades);
            Assert.AreEqual(UiTab.Store, pages.Shown);
            Assert.AreEqual(UiTab.Home, pages.Incoming);
            Assert.AreEqual(2, ActiveCount());
        }

        [Test]
        public void Select_TheShownPage_ChangesNothing()
        {
            var fades = pages.Select(UiTab.Home, true);

            Assert.IsFalse(fades);
            Assert.IsFalse(pages.IsFading);
            Assert.AreEqual(1, ActiveCount());
        }

        [Test]
        public void Select_NotAnimated_ShowsThatPageAloneAtOnce()
        {
            pages.Select(UiTab.Store, true);

            var fades = pages.Select(UiTab.Leaderboard, false);

            Assert.IsFalse(fades);
            Assert.AreEqual(UiTab.Leaderboard, pages.Shown);
            Assert.IsFalse(pages.IsFading);
            Assert.AreEqual(1, ActiveCount());
        }

        [Test]
        public void Select_ManyFastPresses_NeverMoreThanTwoActiveAndOneAfterFinish()
        {
            var presses = new[]
            {
                UiTab.Store, UiTab.Leaderboard, UiTab.Home, UiTab.Home, UiTab.Store, UiTab.Home, UiTab.Leaderboard, UiTab.Store
            };

            foreach (var tab in presses)
            {
                pages.Select(tab, true);
                Assert.That(ActiveCount(), Is.LessThanOrEqualTo(2), $"after selecting {tab}");
                Assert.IsTrue(pages.IsActive(tab), $"{tab} is shown or coming in");
            }

            pages.Finish();

            Assert.AreEqual(1, ActiveCount());
            Assert.AreEqual(UiTab.Store, pages.Shown, "the last press wins");
        }

        [Test]
        public void Finish_WithNoFade_DoesNothing()
        {
            pages.Finish();

            Assert.AreEqual(UiTab.Home, pages.Shown);
            Assert.IsFalse(pages.IsFading);
        }

        [Test]
        public void Select_UndeclaredTab_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => pages.Select((UiTab)99, true));
        }

        private int ActiveCount() => Tabs.Count(tab => pages.IsActive(tab));
    }
}
