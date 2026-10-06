using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Runtime;
using GateRush.Serialization;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 21's <see cref="PlayLevelList"/>: the Play Level window
    /// lists the levels in <see cref="LevelCatalog"/>'s order with its numbers
    /// — the HUD's — and lists a file that does not load last, with its error
    /// and no number.
    /// </summary>
    public class PlayLevelListTests
    {
        private static string LevelJson(int levelId) =>
            LevelSerializer.ToJson(Ctx(3, 1, new[] { Block(1, new Coord(1, 0)) }, levelId: levelId));

        [Test]
        public void Build_Rows_MatchCatalogNamesAndNumbers()
        {
            // Names against id order, so the rows cannot follow the names or the input.
            var roster = LevelRoster.Read(new[] { ("level-b", LevelJson(4)), ("level-c", LevelJson(1)), ("level-a", LevelJson(3)) });

            var list = PlayLevelList.Build(roster);

            var catalog = roster.Catalog;
            CollectionAssert.AreEqual(catalog.Names, list.Rows.Select(r => r.Name).ToArray());
            foreach (var row in list.Rows)
            {
                Assert.IsTrue(catalog.TryGetNumber(row.Name, out var number));
                Assert.AreEqual(number, row.Number);
                Assert.IsTrue(row.IsPlayable);
            }

            Assert.IsNull(list.OrderError);
        }

        [Test]
        public void Build_NonContiguousIds_NumbersArePositions()
        {
            var roster = LevelRoster.Read(new[] { ("level-0", LevelJson(0)), ("level-3", LevelJson(3)), ("level-9", LevelJson(9)) });

            var list = PlayLevelList.Build(roster);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, list.Rows.Select(r => r.Number).ToArray());
            Assert.AreEqual("3 — level-9", list.Rows[2].Label);
        }

        [Test]
        public void Build_UnloadableLevel_IsListedLastWithItsError()
        {
            // By name the broken file would come first.
            var roster = LevelRoster.Read(new[] { ("a-broken", ""), ("level-1", LevelJson(1)), ("level-0", LevelJson(0)) });

            var list = PlayLevelList.Build(roster);

            Assert.AreEqual(3, list.Rows.Count);
            var last = list.Rows[2];
            Assert.AreEqual("a-broken", last.Name);
            Assert.IsFalse(last.IsPlayable);
            Assert.AreEqual(roster.Failed[0].Error, last.Error);
            Assert.AreEqual(PlayLevelList.NoNumber, last.Number);
            Assert.AreEqual("a-broken", last.Label);
        }

        [Test]
        public void Build_NoCatalog_RowsHaveNoNumber()
        {
            var roster = LevelRoster.Read(new[] { ("level-b", LevelJson(1)), ("level-a", LevelJson(1)) });

            var list = PlayLevelList.Build(roster);

            Assert.AreEqual(roster.CatalogError, list.OrderError);
            Assert.IsNotNull(list.OrderError);
            CollectionAssert.AreEqual(new[] { "level-a", "level-b" }, list.Rows.Select(r => r.Name).ToArray());
            foreach (var row in list.Rows)
            {
                Assert.AreEqual(PlayLevelList.NoNumber, row.Number);
                Assert.IsTrue(row.IsPlayable);
            }
        }
    }
}
