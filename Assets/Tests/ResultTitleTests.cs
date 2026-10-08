using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 18's <see cref="ResultTitle"/>: the win after the last
    /// level in the catalog reads the all-done title, and every other win the
    /// win title — decided by the same query that hides Next.
    /// </summary>
    public class ResultTitleTests
    {
        private const string Win = "Level Complete";
        private const string AllDone = "All Levels Played";

        [Test]
        public void ForWin_LastLevel_IsTheAllDoneTitle()
        {
            var catalog = Catalog();

            var title = ResultTitle.ForWin(catalog, "level-4", 4, Win, AllDone);

            Assert.IsFalse(catalog.TryGetNext(4, out _), "level-4 has no next level");
            Assert.AreEqual(AllDone, title);
        }

        [Test]
        public void ForWin_EarlierLevel_IsTheWinTitle()
        {
            var title = ResultTitle.ForWin(Catalog(), "level-1", 1, Win, AllDone);

            Assert.AreEqual(Win, title);
        }

        [Test]
        public void ForWin_LevelOutsideTheCatalog_IsTheWinTitle()
        {
            var title = ResultTitle.ForWin(Catalog(), "scratch", 9, Win, AllDone);

            Assert.AreEqual(Win, title);
        }

        [Test]
        public void ForWin_NoCatalog_IsTheWinTitle()
        {
            var title = ResultTitle.ForWin(null, "level-4", 4, Win, AllDone);

            Assert.AreEqual(Win, title);
        }

        [Test]
        public void IsLastLevel_HighestId_IsTrue()
        {
            var isLast = ResultTitle.IsLastLevel(Catalog(), "level-4", 4);

            Assert.IsTrue(isLast);
        }

        [Test]
        public void IsLastLevel_EarlierLevelOutsideLevelOrNoCatalog_IsFalse()
        {
            var earlier = ResultTitle.IsLastLevel(Catalog(), "level-3", 3);
            var outside = ResultTitle.IsLastLevel(Catalog(), "scratch", 9);
            var noCatalog = ResultTitle.IsLastLevel(null, "level-4", 4);

            Assert.IsFalse(earlier, "a level with a next one");
            Assert.IsFalse(outside, "a level the catalog does not hold");
            Assert.IsFalse(noCatalog, "no catalog");
        }

        /// <summary>
        /// The closing message shows exactly when the all-done title does
        /// (Module 23): both go by <see cref="ResultTitle.IsLastLevel"/>.
        /// </summary>
        [TestCase("level-0", 0)]
        [TestCase("level-3", 3)]
        [TestCase("level-4", 4)]
        [TestCase("scratch", 9)]
        public void ForWin_IsTheAllDoneTitle_ExactlyWhenIsLastLevel(string levelName, int levelId)
        {
            var catalog = Catalog();

            var title = ResultTitle.ForWin(catalog, levelName, levelId, Win, AllDone);
            var isLast = ResultTitle.IsLastLevel(catalog, levelName, levelId);

            Assert.AreEqual(isLast, title == AllDone);
        }

        private static LevelCatalog Catalog() =>
            new LevelCatalog(new[] { ("level-0", 0), ("level-1", 1), ("level-3", 3), ("level-4", 4) });
    }
}
