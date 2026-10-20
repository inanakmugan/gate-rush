using System;
using GateRush.Platform;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 39's <see cref="LeaderboardResult"/>: a loaded result
    /// holds a copy of its entries in order; a failed one is not loaded and
    /// has no entries, simulated or not.
    /// </summary>
    public class LeaderboardResultTests
    {
        [Test]
        public void Loaded_Entries_AreCopiedInOrder()
        {
            var entries = new[]
            {
                new LeaderboardEntry(1, "A", "cow", 900, false),
                new LeaderboardEntry(2, "You", "bear", 500, true),
            };

            var result = LeaderboardResult.Loaded(entries, true);
            entries[0] = new LeaderboardEntry(1, "Changed", "cow", 0, false);

            Assert.IsTrue(result.IsLoaded);
            Assert.IsTrue(result.IsSimulated);
            Assert.AreEqual(2, result.Entries.Count);
            Assert.AreEqual("A", result.Entries[0].Name, "a copy, not the caller's array");
            Assert.AreEqual("You", result.Entries[1].Name);
        }

        [Test]
        public void Loaded_NullEntries_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LeaderboardResult.Loaded(null, false));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Failed_AnySource_IsNotLoadedAndHasNoEntries(bool isSimulated)
        {
            var result = LeaderboardResult.Failed(isSimulated);

            Assert.IsFalse(result.IsLoaded);
            Assert.AreEqual(isSimulated, result.IsSimulated);
            Assert.IsNotNull(result.Entries);
            Assert.AreEqual(0, result.Entries.Count);
        }
    }
}
