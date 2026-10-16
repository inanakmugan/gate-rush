using System;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 23's <see cref="MenuLevels"/>: the level select lists
    /// every level with its number and its completed mark whatever is
    /// completed — no level is locked (D50).
    /// </summary>
    /// <remarks>
    /// The catalog's ids are 0, 1, 3 and 4, given out of order: ids need not
    /// be contiguous, and the order is by id, not by file.
    /// </remarks>
    public class MenuLevelsTests
    {
        private static readonly (string name, int levelId)[] Levels =
        {
            ("level-d", 4), ("level-a", 0), ("level-c", 3), ("level-b", 1)
        };

        private static readonly string[] NamesInOrder = { "level-a", "level-b", "level-c", "level-d" };
        private static readonly int[] IdsInOrder = { 0, 1, 3, 4 };

        [Test]
        public void Entries_NothingCompleted_ListsEveryLevelInOrderWithItsNumber()
        {
            var catalog = Catalog();

            var entries = MenuLevels.Entries(catalog, new CompletedLevels());

            Assert.AreEqual(NamesInOrder.Length, entries.Count, "every catalog level is listed");
            for (var i = 0; i < entries.Count; i++)
            {
                Assert.AreEqual(NamesInOrder[i], entries[i].Name, $"entry {i}");
                Assert.IsTrue(catalog.TryGetNumber(entries[i].Name, out var number), $"entry {i} is a catalog level");
                Assert.AreEqual(number, entries[i].Number, $"entry {i} carries the catalog's number");
                Assert.IsFalse(entries[i].IsCompleted, $"entry {i}");
            }
        }

        [Test]
        public void Entries_SomeCompleted_MarksExactlyThose()
        {
            var completed = Completed(IdsInOrder[1], IdsInOrder[3]);

            var entries = MenuLevels.Entries(Catalog(), completed);

            Assert.AreEqual(IdsInOrder.Length, entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                Assert.AreEqual(completed.IsCompleted(IdsInOrder[i]), entries[i].IsCompleted, $"entry {i}");
            }
        }

        /// <summary>
        /// The central decision (D50): whatever is completed — nothing, the
        /// first levels, only later ones, everything — the select lists the
        /// same levels, each one startable. An entry has no locked state; what
        /// it can carry is the name that starts it, and every entry carries
        /// one the catalog holds.
        /// </summary>
        [TestCase(new int[0])]
        [TestCase(new[] { 0 })]
        [TestCase(new[] { 0, 1 })]
        [TestCase(new[] { 4 })]
        [TestCase(new[] { 1, 3 })]
        [TestCase(new[] { 0, 1, 3, 4 })]
        public void Entries_WhateverIsCompleted_EveryLevelIsListedAndPlayable(int[] completedIds)
        {
            var catalog = Catalog();

            var entries = MenuLevels.Entries(catalog, Completed(completedIds));

            Assert.AreEqual(NamesInOrder.Length, entries.Count, "no level is left out of the select");
            for (var i = 0; i < entries.Count; i++)
            {
                Assert.AreEqual(NamesInOrder[i], entries[i].Name, $"entry {i} starts its own level");
                Assert.IsTrue(catalog.TryGetNumber(entries[i].Name, out _), $"entry {i} names a level that can be started");
            }
        }

        [Test]
        public void Entries_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => MenuLevels.Entries(null, new CompletedLevels()));
            Assert.Throws<ArgumentNullException>(() => MenuLevels.Entries(Catalog(), null));
        }

        private static LevelCatalog Catalog() => new LevelCatalog(Levels);

        private static CompletedLevels Completed(params int[] ids)
        {
            var completed = new CompletedLevels();
            foreach (var id in ids)
            {
                completed.MarkCompleted(id);
            }

            return completed;
        }
    }
}
