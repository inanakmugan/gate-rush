using System;
using GateRush.Meta;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 29's <see cref="SaveRepository"/> over a
    /// <see cref="MemorySaveStore"/> and the real <see cref="JsonSaveCodec"/>:
    /// a save survives a new repository; the old keys are carried over only
    /// while no save exists under the new key, which then always wins; one
    /// unreadable old key leaves the other; an unreadable or too new save gives
    /// the defaults and is left in the store; and a carried-over save whose
    /// write fails is still played.
    /// </summary>
    public class SaveRepositoryTests
    {
        private const string Unreadable = "garbage";

        [Test]
        public void Load_EmptyStore_GivesDefaultsFreshAndWritesNothing()
        {
            var store = new MemorySaveStore();

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Fresh, load.Outcome);
            SaveFixture.AssertSameSave(SaveData.CreateDefault(), load.Data);
            CollectionAssert.IsEmpty(load.Problems);
            CollectionAssert.IsEmpty(store.SavedKeys);
        }

        [Test]
        public void Load_SavedThroughAnotherRepository_GivesTheSavedDataAndLoaded()
        {
            var store = new MemorySaveStore();
            var saved = SaveFixture.EveryField();
            Repository(store).Save(saved);
            var savesBeforeLoad = store.SavedKeys.Count;

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Loaded, load.Outcome);
            SaveFixture.AssertSameSave(saved, load.Data);
            CollectionAssert.IsEmpty(load.Problems);
            Assert.AreEqual(savesBeforeLoad, store.SavedKeys.Count, "Load writes nothing");
        }

        [Test]
        public void Load_OnlyLegacyKeys_CarriesThemOverAndWritesTheNewKey()
        {
            var store = new MemorySaveStore();
            store.Seed(CompletedLevels.LegacySaveKey, "0,2");
            store.Seed(SoundSetting.LegacySaveKey, "off");

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.CarriedOver, load.Outcome);
            CollectionAssert.AreEqual(new[] { 0, 2 }, load.Data.Completed.Ids);
            Assert.IsFalse(load.Data.Sound.IsOn);
            CollectionAssert.IsEmpty(load.Problems);
            CollectionAssert.AreEqual(new[] { SaveRepository.SaveKey }, store.SavedKeys, "exactly one save, under the new key");
            Assert.IsTrue(store.TryLoad(SaveRepository.SaveKey, out var written));
            Assert.IsTrue(new JsonSaveCodec().TryDecode(written, out var writtenSave, out var problem), problem);
            SaveFixture.AssertSameSave(load.Data, writtenSave);
        }

        [Test]
        public void Load_LegacyKeysChangedAfterCarryOver_IsLoadedFromTheNewKey()
        {
            var store = new MemorySaveStore();
            store.Seed(CompletedLevels.LegacySaveKey, "0,2");
            store.Seed(SoundSetting.LegacySaveKey, "off");
            var carriedOver = Repository(store).Load();
            store.Seed(CompletedLevels.LegacySaveKey, "5");
            store.Seed(SoundSetting.LegacySaveKey, "on");

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Loaded, load.Outcome);
            SaveFixture.AssertSameSave(carriedOver.Data, load.Data);
        }

        [Test]
        public void Load_NewKeyBesideLegacyKeys_IgnoresTheLegacyKeys()
        {
            var store = new MemorySaveStore();
            var saved = SaveFixture.EveryField();
            store.Seed(SaveRepository.SaveKey, new JsonSaveCodec().Encode(saved));
            store.Seed(CompletedLevels.LegacySaveKey, "5");
            store.Seed(SoundSetting.LegacySaveKey, "on");

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Loaded, load.Outcome);
            SaveFixture.AssertSameSave(saved, load.Data);
            CollectionAssert.IsEmpty(store.SavedKeys);
        }

        [TestCase(Unreadable, "off", new int[0], false)]
        [TestCase("0,2", Unreadable, new[] { 0, 2 }, true)]
        public void Load_OneLegacyKeyUnreadable_KeepsTheOtherWithOneProblem(
            string completedData, string soundData, int[] expectedIds, bool expectedSoundOn)
        {
            var store = new MemorySaveStore();
            store.Seed(CompletedLevels.LegacySaveKey, completedData);
            store.Seed(SoundSetting.LegacySaveKey, soundData);

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.CarriedOver, load.Outcome);
            CollectionAssert.AreEqual(expectedIds, load.Data.Completed.Ids);
            Assert.AreEqual(expectedSoundOn, load.Data.Sound.IsOn);
            Assert.AreEqual(1, load.Problems.Count);
        }

        [TestCase("0,2", null, new[] { 0, 2 }, true)]
        [TestCase(null, "off", new int[0], false)]
        public void Load_OnlyOneLegacyKey_CarriesItOverWithNoProblem(
            string completedData, string soundData, int[] expectedIds, bool expectedSoundOn)
        {
            var store = new MemorySaveStore();
            if (completedData != null)
            {
                store.Seed(CompletedLevels.LegacySaveKey, completedData);
            }

            if (soundData != null)
            {
                store.Seed(SoundSetting.LegacySaveKey, soundData);
            }

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.CarriedOver, load.Outcome);
            CollectionAssert.AreEqual(expectedIds, load.Data.Completed.Ids);
            Assert.AreEqual(expectedSoundOn, load.Data.Sound.IsOn);
            CollectionAssert.IsEmpty(load.Problems);
            CollectionAssert.AreEqual(new[] { SaveRepository.SaveKey }, store.SavedKeys);
        }

        [Test]
        public void Load_UnreadableNewKey_GivesDefaultsUnreadableAndLeavesTheTextUnchanged()
        {
            var store = new MemorySaveStore();
            store.Seed(SaveRepository.SaveKey, Unreadable);
            store.Seed(CompletedLevels.LegacySaveKey, "0,2");
            store.Seed(SoundSetting.LegacySaveKey, "off");

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Unreadable, load.Outcome);
            SaveFixture.AssertSameSave(SaveData.CreateDefault(), load.Data);
            Assert.AreEqual(1, load.Problems.Count);
            AssertStoredUnchanged(store, Unreadable);
        }

        [Test]
        public void Load_FutureFormatVersion_IsUnreadableAndLeavesTheTextUnchanged()
        {
            var store = new MemorySaveStore();
            var future = $"{{\"formatVersion\":{JsonSaveCodec.CurrentFormatVersion + 1},\"completedLevelIds\":[1],\"soundOn\":false}}";
            store.Seed(SaveRepository.SaveKey, future);

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Unreadable, load.Outcome);
            SaveFixture.AssertSameSave(SaveData.CreateDefault(), load.Data);
            Assert.AreEqual(1, load.Problems.Count);
            AssertStoredUnchanged(store, future);
        }

        [Test]
        public void Load_CarryOverWriteFails_KeepsTheDataWithAProblem()
        {
            var store = new MemorySaveStore { FailsSaves = true };
            store.Seed(CompletedLevels.LegacySaveKey, "0,2");
            store.Seed(SoundSetting.LegacySaveKey, "off");

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.CarriedOver, load.Outcome);
            CollectionAssert.AreEqual(new[] { 0, 2 }, load.Data.Completed.Ids);
            Assert.IsFalse(load.Data.Sound.IsOn);
            Assert.AreEqual(1, load.Problems.Count);
            Assert.IsFalse(store.TryLoad(SaveRepository.SaveKey, out _), "nothing was written");
        }

        [Test]
        public void Save_AnySaveData_WritesOnceUnderTheSaveKey()
        {
            var store = new MemorySaveStore();
            var codec = new JsonSaveCodec();
            var save = SaveFixture.EveryField();

            new SaveRepository(store, codec).Save(save);

            CollectionAssert.AreEqual(new[] { SaveRepository.SaveKey }, store.SavedKeys);
            Assert.IsTrue(store.TryLoad(SaveRepository.SaveKey, out var written));
            Assert.AreEqual(codec.Encode(save), written);
        }

        [Test]
        public void Save_Null_Throws()
        {
            var repository = Repository(new MemorySaveStore());

            Assert.Throws<ArgumentNullException>(() => repository.Save(null));
        }

        [Test]
        public void Constructor_NullStoreOrCodec_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SaveRepository(null, new JsonSaveCodec()));
            Assert.Throws<ArgumentNullException>(() => new SaveRepository(new MemorySaveStore(), null));
        }

        [Test]
        public void SaveKey_IsTheSpecified()
        {
            Assert.AreEqual("GateRush.Save", SaveRepository.SaveKey);
        }

        private static SaveRepository Repository(MemorySaveStore store) => new SaveRepository(store, new JsonSaveCodec());

        private static void AssertStoredUnchanged(MemorySaveStore store, string expected)
        {
            Assert.IsTrue(store.TryLoad(SaveRepository.SaveKey, out var stored));
            Assert.AreEqual(expected, stored, "the unreadable save is left in the store");
            CollectionAssert.IsEmpty(store.SavedKeys, "Load writes nothing");
        }
    }
}
