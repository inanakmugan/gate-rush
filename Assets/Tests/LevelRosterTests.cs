using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using GateRush.Serialization;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 21's <see cref="LevelRoster"/>, the one reading of the
    /// level files the game and the Play Level window share: a file that does
    /// not load is left out with its error, the first of two files with one
    /// name is kept, two files sharing an id leave no order, and each level's
    /// mechanics sit in the catalog's order.
    /// </summary>
    public class LevelRosterTests
    {
        private static readonly BlockColor[] TwoColours = { BlockColor.Red, BlockColor.Blue };

        /// <summary>A 3x1 level with one plain block, as its file's text.</summary>
        private static string PlainLevel(int levelId) => LevelSerializer.ToJson(PlainContext(levelId));

        private static LevelContext PlainContext(int levelId) =>
            Ctx(3, 1, new[] { Block(1, new Coord(1, 0)) }, levelId: levelId);

        /// <summary>A 3x1 level whose one block is layered, so it contains a mechanic the plain level does not.</summary>
        private static LevelContext LayeredContext(int levelId) =>
            Ctx(3, 1, new[] { Block(1, new Coord(1, 0), colors: TwoColours) }, levelId: levelId);

        [Test]
        public void Read_FilesThatLoad_AreLoadedAndOrderedById()
        {
            var files = new[] { ("level-b", PlainLevel(4)), ("level-a", PlainLevel(7)), ("level-c", PlainLevel(2)) };

            var roster = LevelRoster.Read(files);

            CollectionAssert.AreEqual(new[] { "level-b", "level-a", "level-c" }, roster.Loaded);
            CollectionAssert.AreEqual(new[] { "level-c", "level-b", "level-a" }, roster.Catalog.Names);
            Assert.IsEmpty(roster.Failed);
            Assert.IsNull(roster.CatalogError);
        }

        [Test]
        public void Read_FileThatFailsToParse_IsListedAsFailedAndLeftOutOfTheOrder()
        {
            var files = new[] { ("level-0", PlainLevel(0)), ("broken", ""), ("level-1", PlainLevel(1)) };

            var roster = LevelRoster.Read(files);

            Assert.AreEqual(1, roster.Failed.Count);
            Assert.AreEqual("broken", roster.Failed[0].Name);
            Assert.IsFalse(roster.Failed[0].IsDuplicateName);
            Assert.IsNotEmpty(roster.Failed[0].Error);
            CollectionAssert.DoesNotContain(roster.Loaded, "broken");
            CollectionAssert.AreEqual(new[] { "level-0", "level-1" }, roster.Catalog.Names);
        }

        [Test]
        public void Read_TwoFilesWithOneName_KeepsTheFirst()
        {
            // The second would be number 1 if it were the one kept.
            var files = new[] { ("level-a", PlainLevel(5)), ("level-b", PlainLevel(3)), ("level-a", PlainLevel(1)) };

            var roster = LevelRoster.Read(files);
            var found = roster.Catalog.TryGetNumber("level-a", out var number);

            CollectionAssert.AreEqual(new[] { "level-a", "level-b" }, roster.Loaded);
            Assert.IsTrue(found);
            Assert.AreEqual(2, number);
            Assert.IsTrue(roster.Failed.Any(f => f.Name == "level-a" && f.IsDuplicateName));
        }

        [Test]
        public void Read_TwoFilesSharingAnId_HasNoCatalogAndSaysWhy()
        {
            var files = new[] { ("level-a", PlainLevel(1)), ("level-b", PlainLevel(1)) };

            var roster = LevelRoster.Read(files);

            Assert.IsNull(roster.Catalog);
            StringAssert.Contains("level-a", roster.CatalogError);
            StringAssert.Contains("level-b", roster.CatalogError);
            Assert.IsEmpty(roster.MechanicsInOrder);

            // Each still loads: the game plays them, without a number.
            CollectionAssert.AreEqual(new[] { "level-a", "level-b" }, roster.Loaded);
        }

        [Test]
        public void Read_MechanicsInOrder_FollowCatalogNames()
        {
            // Given against id order, so the mechanics cannot follow the input's order.
            var layered = LayeredContext(9);
            var plain = PlainContext(2);
            var files = new[] { ("layered", LevelSerializer.ToJson(layered)), ("plain", LevelSerializer.ToJson(plain)) };

            var roster = LevelRoster.Read(files);

            CollectionAssert.AreEqual(new[] { "plain", "layered" }, roster.Catalog.Names);
            Assert.AreEqual(2, roster.MechanicsInOrder.Count);
            CollectionAssert.AreEqual(LevelMechanics.Of(plain), roster.MechanicsInOrder[0]);
            CollectionAssert.AreEqual(LevelMechanics.Of(layered), roster.MechanicsInOrder[1]);
            CollectionAssert.Contains(roster.MechanicsInOrder[1], LevelMechanic.LayeredBlock);
        }

        [Test]
        public void TryParse_TextThatIsNotALevel_FailsWithAnError()
        {
            var parsed = LevelRoster.TryParse("", "broken", out var ctx, out var error);

            Assert.IsFalse(parsed);
            Assert.IsNull(ctx);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryParse_LevelText_GivesTheLevel()
        {
            var parsed = LevelRoster.TryParse(PlainLevel(6), "level-6", out var ctx, out var error);

            Assert.IsTrue(parsed);
            Assert.AreEqual(6, ctx.LevelId);
            Assert.IsNull(error);
        }
    }
}
