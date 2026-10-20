using System.Linq;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 39's <see cref="SimulatedLeaderboardConfig"/> (D73): a
    /// fresh asset holds twenty demo players with unique names and scores
    /// from 40 to 980 and reports no problem; a refused name, an empty avatar
    /// id, a negative score and an empty table are reported by name; and an
    /// avatar id the catalog lacks is named for the bootstrap to log.
    /// </summary>
    public class SimulatedLeaderboardConfigTests
    {
        private const string PlayersField = "players";
        private const string PlayersLabel = "Players";
        private const int DemoCount = 20;
        private const int LowestScore = 40;
        private const int HighestScore = 980;

        private SimulatedLeaderboardConfig config;
        private AvatarCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulatedLeaderboardConfig>();
            catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Problems_FreshConfig_IsEmptyAndHoldsTwentyPlayers()
        {
            var problems = config.Problems();

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
            Assert.AreEqual(DemoCount, config.Players().Length);
        }

        [Test]
        public void Players_FreshConfig_ScoresSpreadFrom40To980()
        {
            var scores = config.Players().Select(player => player.Score).ToList();

            Assert.AreEqual(LowestScore, scores.Min());
            Assert.AreEqual(HighestScore, scores.Max());
        }

        [Test]
        public void Players_FreshConfig_NamesAreUnique()
        {
            var names = config.Players().Select(player => player.Name).ToList();

            CollectionAssert.AllItemsAreUnique(names);
        }

        [TestCase("Bad!Name", "cow", 10, "Name 'Bad!Name' is refused", TestName = "Problems_RefusedName_IsReported")]
        [TestCase("Fine", "", 10, "Avatar Id is empty", TestName = "Problems_EmptyAvatarId_IsReported")]
        [TestCase("Fine", "cow", -1, "Score -1 is negative", TestName = "Problems_NegativeScore_IsReported")]
        public void Problems_PlayerWithAProblem_IsReported(string name, string avatarId, int score, string expected)
        {
            SetPlayers((name, avatarId, score));

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{PlayersLabel} element 0: {expected}"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_EmptyTable_IsReported()
        {
            SetPlayers();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("no demo player"), string.Join(" | ", problems));
        }

        [Test]
        public void MissingAvatars_IdNotInCatalog_IsNamed()
        {
            SetCatalogIds("cow");
            SetPlayers(("Known", "cow", 10), ("Unknown", "lion", 20));

            var missing = config.MissingAvatars(catalog);

            Assert.AreEqual(1, missing.Count, string.Join(" | ", missing));
            StringAssert.Contains("element 1: Avatar Id 'lion'", missing[0]);
        }

        [Test]
        public void MissingAvatars_EveryIdInCatalog_NamesNothing()
        {
            SetCatalogIds("cow", "duck");
            SetPlayers(("A", "cow", 10), ("B", "duck", 20));

            var missing = config.MissingAvatars(catalog);

            CollectionAssert.IsEmpty(missing, string.Join(" | ", missing));
        }

        private void SetPlayers(params (string name, string avatarId, int score)[] players)
        {
            using (var serialized = new SerializedObject(config))
            {
                var entries = serialized.FindProperty(PlayersField);
                Assert.IsNotNull(entries, $"SimulatedLeaderboardConfig has no serialized field '{PlayersField}'.");
                entries.arraySize = players.Length;
                for (var i = 0; i < players.Length; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("name").stringValue = players[i].name;
                    entry.FindPropertyRelative("avatarId").stringValue = players[i].avatarId;
                    entry.FindPropertyRelative("score").intValue = players[i].score;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Gives the catalog <paramref name="ids"/>, with no sprites: only the ids are read here.</summary>
        private void SetCatalogIds(params string[] ids)
        {
            using (var serialized = new SerializedObject(catalog))
            {
                var avatars = serialized.FindProperty("avatars");
                Assert.IsNotNull(avatars, "AvatarCatalog has no serialized field 'avatars'.");
                avatars.arraySize = ids.Length;
                for (var i = 0; i < ids.Length; i++)
                {
                    avatars.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue = ids[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
