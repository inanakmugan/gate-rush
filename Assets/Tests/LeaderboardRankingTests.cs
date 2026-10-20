using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Platform;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 39's <see cref="LeaderboardRanking"/> (D73): demo players
    /// in any order come out by score descending with ranks 1 to N + 1; the
    /// player is placed by score, below every demo player with the same
    /// score; a player with 0 is last and one above everyone is first; only
    /// the player's entry is marked; tied demo players keep their order; and
    /// a player that cannot be shown throws.
    /// </summary>
    public class LeaderboardRankingTests
    {
        private const string Avatar = "cow";

        [Test]
        public void Rank_DemoPlayersInAnyOrder_ComeOutByScoreDescending()
        {
            var demo = new[] { Demo("C", 300), Demo("A", 900), Demo("D", 100), Demo("B", 600) };

            var entries = LeaderboardRanking.Rank(demo, Player(0));

            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D", "You" }, entries.Select(entry => entry.Name));
        }

        [Test]
        public void Rank_AnyTable_RanksOneToCountPlusOne()
        {
            var demo = new[] { Demo("A", 50), Demo("B", 500), Demo("C", 250) };

            var entries = LeaderboardRanking.Rank(demo, Player(300));

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, entries.Select(entry => entry.Rank));
        }

        [Test]
        public void Rank_PlayerBetweenScores_IsPlacedByScore()
        {
            var demo = new[] { Demo("A", 900), Demo("B", 600), Demo("C", 300) };

            var entries = LeaderboardRanking.Rank(demo, Player(750));

            Assert.AreEqual(2, PlayerEntry(entries).Rank);
        }

        [Test]
        public void Rank_PlayerTiedWithDemoPlayers_SitsBelowThem()
        {
            var demo = new[] { Demo("A", 900), Demo("B", 600), Demo("C", 600), Demo("D", 300) };

            var entries = LeaderboardRanking.Rank(demo, Player(600));

            CollectionAssert.AreEqual(new[] { "A", "B", "C", "You", "D" }, entries.Select(entry => entry.Name));
        }

        [Test]
        public void Rank_PlayerWithZero_IsLastEvenBelowADemoPlayerWithZero()
        {
            var demo = new[] { Demo("A", 0), Demo("B", 40) };

            var entries = LeaderboardRanking.Rank(demo, Player(0));

            Assert.AreEqual(entries.Count, PlayerEntry(entries).Rank);
            Assert.IsTrue(entries[entries.Count - 1].IsPlayer);
        }

        [Test]
        public void Rank_PlayerAboveEveryDemoPlayer_IsFirst()
        {
            var demo = new[] { Demo("A", 980), Demo("B", 40) };

            var entries = LeaderboardRanking.Rank(demo, Player(981));

            Assert.IsTrue(entries[0].IsPlayer);
            Assert.AreEqual(1, entries[0].Rank);
        }

        [Test]
        public void Rank_AnyTable_MarksOnlyThePlayersEntry()
        {
            var demo = new[] { Demo("You", 500), Demo("B", 400), Demo("C", 600) };

            var entries = LeaderboardRanking.Rank(demo, Player(450));

            Assert.AreEqual(1, entries.Count(entry => entry.IsPlayer));
            Assert.AreEqual(450, PlayerEntry(entries).Score, "the marked entry is the player's, not the demo player of the same name");
        }

        [Test]
        public void Rank_TiedDemoPlayers_KeepTheirConfigOrder()
        {
            var demo = new[] { Demo("First", 500), Demo("Second", 500), Demo("Third", 500) };

            var entries = LeaderboardRanking.Rank(demo, Player(0));

            CollectionAssert.AreEqual(new[] { "First", "Second", "Third", "You" }, entries.Select(entry => entry.Name));
        }

        [Test]
        public void Rank_NoDemoPlayers_ThePlayerIsFirstAndAlone()
        {
            var entries = LeaderboardRanking.Rank(Array.Empty<LeaderboardPlayer>(), Player(10));

            Assert.AreEqual(1, entries.Count);
            Assert.IsTrue(entries[0].IsPlayer);
            Assert.AreEqual(1, entries[0].Rank);
        }

        [Test]
        public void Rank_Entries_CarryTheirNameAvatarAndScore()
        {
            var demo = new[] { new LeaderboardPlayer("Mavi Kedi", "penguin", 760) };

            var entries = LeaderboardRanking.Rank(demo, new LeaderboardPlayer("Ayı", "bear", 750));

            Assert.AreEqual(("Mavi Kedi", "penguin", 760), (entries[0].Name, entries[0].AvatarId, entries[0].Score));
            Assert.AreEqual(("Ayı", "bear", 750), (entries[1].Name, entries[1].AvatarId, entries[1].Score));
        }

        [Test]
        public void Rank_NullDemoPlayers_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LeaderboardRanking.Rank(null, Player(0)));
        }

        [TestCase(null, Avatar, TestName = "Rank_PlayerWithNullName_Throws")]
        [TestCase("You", null, TestName = "Rank_PlayerWithNullAvatarId_Throws")]
        public void Rank_PlayerWithANullPart_Throws(string name, string avatarId)
        {
            var player = new LeaderboardPlayer(name, avatarId, 0);

            Assert.Throws<ArgumentException>(() => LeaderboardRanking.Rank(Array.Empty<LeaderboardPlayer>(), player));
        }

        [Test]
        public void Rank_PlayerWithNegativeScore_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LeaderboardRanking.Rank(Array.Empty<LeaderboardPlayer>(), Player(-1)));
        }

        private static LeaderboardPlayer Demo(string name, int score) => new LeaderboardPlayer(name, Avatar, score);

        private static LeaderboardPlayer Player(int score) => new LeaderboardPlayer("You", Avatar, score);

        private static LeaderboardEntry PlayerEntry(IReadOnlyList<LeaderboardEntry> entries) =>
            entries.Single(entry => entry.IsPlayer);
    }
}
