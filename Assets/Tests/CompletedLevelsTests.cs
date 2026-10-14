using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 23's <see cref="CompletedLevels"/>: a marked level is
    /// completed and marking it again is not news; <see cref="CompletedLevels.Of"/>
    /// and <see cref="CompletedLevels.Ids"/> round-trip, ascending, with
    /// duplicates read as one (Module 29); the legacy text, now only read, is
    /// read from literal strings; and legacy data that cannot be read — empty,
    /// null or garbage — gives an empty set without throwing, with
    /// <see cref="CompletedLevels.TryFromData"/> telling garbage apart from
    /// nothing saved.
    /// </summary>
    public class CompletedLevelsTests
    {
        // Not contiguous, not in order, and with an id of 0: nothing may rely
        // on ids counting up from 1.
        private static readonly int[] SomeIds = { 7, 0, 3, 12 };

        [Test]
        public void IsCompleted_NothingMarked_IsFalse()
        {
            var levels = new CompletedLevels();

            var isCompleted = levels.IsCompleted(SomeIds[0]);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(0, levels.Count);
        }

        [Test]
        public void MarkCompleted_ALevel_CompletesThatLevelOnly()
        {
            var levels = new CompletedLevels();

            levels.MarkCompleted(SomeIds[0]);

            Assert.IsTrue(levels.IsCompleted(SomeIds[0]));
            Assert.IsFalse(levels.IsCompleted(SomeIds[1]));
        }

        [Test]
        public void MarkCompleted_Twice_ReportsNewlyCompletedOnce()
        {
            var levels = new CompletedLevels();

            var first = levels.MarkCompleted(SomeIds[0]);
            var second = levels.MarkCompleted(SomeIds[0]);

            Assert.IsTrue(first, "the first mark is news");
            Assert.IsFalse(second, "the second mark is not");
            Assert.AreEqual(1, levels.Count);
        }

        [Test]
        public void FromData_LegacyText_GivesThoseIds()
        {
            var read = CompletedLevels.FromData("7,0,3,12");

            Assert.AreEqual(SomeIds.Length, read.Count, "no level is added or lost");
            foreach (var id in SomeIds)
            {
                Assert.IsTrue(read.IsCompleted(id), $"level id {id}");
            }
        }

        [Test]
        public void Ids_SameSetMarkedInAnotherOrder_IsTheSameAscendingList()
        {
            var forward = new CompletedLevels();
            var backward = new CompletedLevels();
            for (var i = 0; i < SomeIds.Length; i++)
            {
                forward.MarkCompleted(SomeIds[i]);
                backward.MarkCompleted(SomeIds[SomeIds.Length - 1 - i]);
            }

            var forwardIds = forward.Ids;
            var backwardIds = backward.Ids;

            CollectionAssert.AreEqual(forwardIds, backwardIds);
            CollectionAssert.IsOrdered(forwardIds);
        }

        [Test]
        public void Ids_OfOf_RoundTripsAscending()
        {
            var levels = CompletedLevels.Of(SomeIds);

            var ids = levels.Ids;

            CollectionAssert.AreEqual(new[] { 0, 3, 7, 12 }, ids);
            CollectionAssert.AreEqual(ids, CompletedLevels.Of(ids).Ids);
        }

        [Test]
        public void Of_DuplicateIds_ReadsThemAsOne()
        {
            var levels = CompletedLevels.Of(new[] { 3, 1, 3, 1 });

            var ids = levels.Ids;

            CollectionAssert.AreEqual(new[] { 1, 3 }, ids);
            Assert.AreEqual(2, levels.Count);
        }

        [Test]
        public void Of_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CompletedLevels.Of(null));
        }

        [Test]
        public void LegacySaveKey_IsTheSpecified()
        {
            Assert.AreEqual("GateRush.CompletedLevels", CompletedLevels.LegacySaveKey);
        }

        [TestCase(null)]
        [TestCase("")]
        public void TryFromData_NothingSaved_IsReadableAndEmpty(string data)
        {
            var readable = CompletedLevels.TryFromData(data, out var levels);

            Assert.IsTrue(readable);
            Assert.IsNotNull(levels);
            Assert.AreEqual(0, levels.Count);
        }

        [TestCase("garbage")]
        [TestCase("1,two,3")]
        [TestCase("1,,3")]
        [TestCase("1;2;3")]
        [TestCase("1.5")]
        [TestCase(" ")]
        [TestCase("{\"levels\":[1,2]}")]
        [TestCase("99999999999999999999")]
        public void TryFromData_Garbage_IsUnreadableAndEmpty(string data)
        {
            var readable = CompletedLevels.TryFromData(data, out var levels);

            Assert.IsFalse(readable);
            Assert.IsNotNull(levels);
            Assert.AreEqual(0, levels.Count, "no part of unreadable data is trusted");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("garbage")]
        [TestCase("1,two,3")]
        public void FromData_EmptyNullOrGarbage_GivesAnEmptySetWithoutThrowing(string data)
        {
            CompletedLevels levels = null;

            Assert.DoesNotThrow(() => levels = CompletedLevels.FromData(data));

            Assert.IsNotNull(levels);
            Assert.AreEqual(0, levels.Count);
        }

        [Test]
        public void FromData_AnEmptySetReadFromGarbage_CanStillBeMarked()
        {
            var levels = CompletedLevels.FromData("garbage");

            var isNew = levels.MarkCompleted(SomeIds[0]);

            Assert.IsTrue(isNew);
            Assert.IsTrue(levels.IsCompleted(SomeIds[0]));
        }
    }
}
