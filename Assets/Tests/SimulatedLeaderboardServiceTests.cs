using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Platform;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 39's <see cref="SimulatedLeaderboardService"/> (D73):
    /// <c>Fetch</c> answers at once, loaded and simulated, with the demo
    /// players and the player; <c>done</c> is called exactly once and the
    /// service is never busy; null arguments throw; and the demo table is
    /// checked and copied.
    /// </summary>
    public class SimulatedLeaderboardServiceTests
    {
        private const int DemoCount = 20;
        private const string Avatar = "cow";

        /// <summary>Demo tables each with one problem, named by the problem.</summary>
        public static IEnumerable<TestCaseData> TablesWithAProblem()
        {
            yield return new TestCaseData((object)new[] { new LeaderboardPlayer("Bad!Name", Avatar, 10) }, "Name 'Bad!Name' is refused")
                .SetName("ProblemsOf_RefusedName_IsReported");
            yield return new TestCaseData((object)new[] { new LeaderboardPlayer("Fine", " ", 10) }, "Avatar Id is empty")
                .SetName("ProblemsOf_EmptyAvatarId_IsReported");
            yield return new TestCaseData((object)new[] { new LeaderboardPlayer("Fine", Avatar, -1) }, "Score -1 is negative")
                .SetName("ProblemsOf_NegativeScore_IsReported");
            yield return new TestCaseData((object)Array.Empty<LeaderboardPlayer>(), "no demo player")
                .SetName("ProblemsOf_EmptyTable_IsReported");
        }

        [Test]
        public void Fetch_Player_AnswersAtOnceLoadedAndSimulated()
        {
            var service = new SimulatedLeaderboardService(DemoTable());
            LeaderboardResult answered = null;

            service.Fetch(Player(), result => answered = result);

            Assert.IsNotNull(answered, "answered before Fetch returned");
            Assert.IsTrue(answered.IsLoaded);
            Assert.IsTrue(answered.IsSimulated);
        }

        [Test]
        public void Fetch_TwentyDemoPlayers_GivesTwentyOneEntriesOneOfThemThePlayers()
        {
            var service = new SimulatedLeaderboardService(DemoTable());
            LeaderboardResult answered = null;

            service.Fetch(Player(), result => answered = result);

            Assert.AreEqual(DemoCount + 1, answered.Entries.Count);
            Assert.AreEqual(1, answered.Entries.Count(entry => entry.IsPlayer));
        }

        [Test]
        public void Fetch_Once_CallsDoneExactlyOnce()
        {
            var service = new SimulatedLeaderboardService(DemoTable());
            var calls = 0;

            service.Fetch(Player(), _ => calls++);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void IsBusy_BeforeAndDuringDone_IsFalse()
        {
            var service = new SimulatedLeaderboardService(DemoTable());
            var wasBusyInDone = true;

            var wasBusyBefore = service.IsBusy;
            service.Fetch(Player(), _ => wasBusyInDone = service.IsBusy);

            Assert.IsFalse(wasBusyBefore);
            Assert.IsFalse(wasBusyInDone);
        }

        [Test]
        public void Fetch_NullDone_Throws()
        {
            var service = new SimulatedLeaderboardService(DemoTable());

            Assert.Throws<ArgumentNullException>(() => service.Fetch(Player(), null));
        }

        [Test]
        public void Fetch_PlayerWithNullName_ThrowsWithoutAnswering()
        {
            var service = new SimulatedLeaderboardService(DemoTable());
            var calls = 0;

            Assert.Throws<ArgumentException>(() => service.Fetch(new LeaderboardPlayer(null, Avatar, 0), _ => calls++));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Constructor_NullTable_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SimulatedLeaderboardService(null));
        }

        [Test]
        public void Constructor_TableWithAProblem_Throws()
        {
            var table = new[] { new LeaderboardPlayer("Fine", Avatar, -5) };

            Assert.Throws<ArgumentException>(() => new SimulatedLeaderboardService(table));
        }

        [Test]
        public void Constructor_Table_IsCopied()
        {
            var table = new[] { new LeaderboardPlayer("Kept", Avatar, 100) };
            var service = new SimulatedLeaderboardService(table);
            LeaderboardResult answered = null;

            table[0] = new LeaderboardPlayer("Changed", Avatar, 100);
            service.Fetch(Player(), result => answered = result);

            // The player's 750 ranks above the demo player's 100.
            Assert.AreEqual("Kept", answered.Entries.Single(entry => !entry.IsPlayer).Name);
        }

        [Test]
        public void ProblemsOf_ValidTable_ReportsNothing()
        {
            var problems = SimulatedLeaderboardService.ProblemsOf(DemoTable());

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
        }

        [TestCaseSource(nameof(TablesWithAProblem))]
        public void ProblemsOf_TableWithAProblem_IsReported(LeaderboardPlayer[] table, string expected)
        {
            var problems = SimulatedLeaderboardService.ProblemsOf(table);

            Assert.That(problems, Has.Some.Contains(expected), string.Join(" | ", problems));
        }

        private static LeaderboardPlayer[] DemoTable() =>
            Enumerable.Range(0, DemoCount).Select(i => new LeaderboardPlayer($"Demo {i}", Avatar, i * 10)).ToArray();

        private static LeaderboardPlayer Player() => new LeaderboardPlayer("You", "bear", 750);
    }
}
