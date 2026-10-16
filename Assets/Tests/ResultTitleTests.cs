using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ResultTitle"/>: the level is the last one exactly
    /// when the catalog holds it and has no next level after it — the same
    /// query that hides Next in free play.
    /// </summary>
    public class ResultTitleTests
    {
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

        private static LevelCatalog Catalog() =>
            new LevelCatalog(new[] { ("level-0", 0), ("level-1", 1), ("level-3", 3), ("level-4", 4) });
    }
}
