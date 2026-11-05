using System;
using System.Collections.Generic;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers the backup of an unreadable save (review fix B3) over a codec
    /// that reads only what it wrote, so these tests need no engine: the
    /// first save after an unreadable load copies the old text to
    /// <see cref="SaveRepository.BackupKeyPrefix"/> before it writes; a
    /// backup key holding another save is never replaced; a failed copy
    /// writes nothing and is tried again; and a readable save is never copied.
    /// </summary>
    public partial class SaveRepositoryTests
    {
        private const string Corrupt = "{\"formatVersion\":4,\"gold\":";

        [Test]
        public void Load_CorruptSave_WritesNoBackupYet()
        {
            var store = new MemorySaveStore();
            store.Seed(SaveRepository.SaveKey, Corrupt);

            var load = StubRepository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Unreadable, load.Outcome);
            CollectionAssert.IsEmpty(store.SavedKeys, "Load writes nothing");
            StringAssert.Contains(SaveRepository.BackupKeyPrefix, load.Problems[0], "the problem says where the text will be kept");
        }

        [Test]
        public void Save_AfterCorruptLoad_CopiesTheTextBeforeReplacingIt()
        {
            var store = new MemorySaveStore();
            store.Seed(SaveRepository.SaveKey, Corrupt);
            var repository = StubRepository(store);
            var load = repository.Load();

            repository.Save(load.Data);
            repository.Save(load.Data);

            Assert.IsTrue(store.TryLoad(SaveRepository.BackupKeyPrefix, out var backup));
            Assert.AreEqual(Corrupt, backup);
            Assert.IsTrue(store.TryLoad(SaveRepository.SaveKey, out var saved));
            Assert.AreEqual(StubCodec.Encoded, saved);
            CollectionAssert.AreEqual(
                new[] { SaveRepository.BackupKeyPrefix, SaveRepository.SaveKey, SaveRepository.SaveKey },
                store.SavedKeys,
                "copied once, before the first save");
        }

        [Test]
        public void Save_AfterCorruptLoadWithAnOlderBackup_KeepsBothBackups()
        {
            var store = new MemorySaveStore();
            const string older = "an older unreadable save";
            store.Seed(SaveRepository.BackupKeyPrefix, older);
            store.Seed(SaveRepository.SaveKey, Corrupt);
            var repository = StubRepository(store);

            repository.Save(repository.Load().Data);

            Assert.IsTrue(store.TryLoad(SaveRepository.BackupKeyPrefix, out var first));
            Assert.AreEqual(older, first, "an existing backup is never replaced");
            Assert.IsTrue(store.TryLoad(SaveRepository.BackupKeyPrefix + ".2", out var second));
            Assert.AreEqual(Corrupt, second);
        }

        [Test]
        public void Save_AfterCorruptLoadAlreadyBackedUp_WritesNoSecondCopy()
        {
            var store = new MemorySaveStore();
            store.Seed(SaveRepository.BackupKeyPrefix, Corrupt);
            store.Seed(SaveRepository.SaveKey, Corrupt);
            var repository = StubRepository(store);

            repository.Save(repository.Load().Data);

            CollectionAssert.AreEqual(new[] { SaveRepository.SaveKey }, store.SavedKeys);
            Assert.IsFalse(store.TryLoad(SaveRepository.BackupKeyPrefix + ".2", out _));
        }

        [Test]
        public void Save_AfterCorruptLoadWhenTheCopyFails_ThrowsAndLeavesTheSaveThenRetries()
        {
            var store = new MemorySaveStore();
            store.Seed(SaveRepository.SaveKey, Corrupt);
            var repository = StubRepository(store);
            var data = repository.Load().Data;
            store.FailsSaves = true;

            Assert.Throws<InvalidOperationException>(() => repository.Save(data));
            store.FailsSaves = false;
            Assert.IsTrue(store.TryLoad(SaveRepository.SaveKey, out var untouched));
            Assert.AreEqual(Corrupt, untouched, "nothing replaced the save while it was not backed up");

            repository.Save(data);

            Assert.IsTrue(store.TryLoad(SaveRepository.BackupKeyPrefix, out var backup));
            Assert.AreEqual(Corrupt, backup, "the copy was tried again");
        }

        [Test]
        public void Save_AfterReadableLoad_WritesNoBackup()
        {
            var store = new MemorySaveStore();
            store.Seed(SaveRepository.SaveKey, StubCodec.Encoded);
            var repository = StubRepository(store);

            repository.Save(repository.Load().Data);

            CollectionAssert.AreEqual(new[] { SaveRepository.SaveKey }, store.SavedKeys);
        }

        private static SaveRepository StubRepository(MemorySaveStore store) =>
            new SaveRepository(store, new StubCodec(), SaveFixture.Rules);

        /// <summary>Writes one fixed text and reads only that text, as a new player's save.</summary>
        private sealed class StubCodec : ISaveCodec
        {
            public const string Encoded = "stub save";

            public string Encode(SaveData data) => Encoded;

            public bool TryDecode(string text, MetaRules rules, out SaveData data, out string problem, out IReadOnlyList<string> resets)
            {
                resets = Array.Empty<string>();
                if (text == Encoded)
                {
                    data = SaveData.CreateDefault(rules);
                    problem = null;
                    return true;
                }

                data = null;
                problem = "it is corrupt.";
                return false;
            }
        }
    }
}
