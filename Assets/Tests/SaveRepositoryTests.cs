using System;
using GateRush.Meta;
using GateRush.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Modules 29 to 31's <see cref="SaveRepository"/> over a
    /// <see cref="MemorySaveStore"/> and the real <see cref="JsonSaveCodec"/>:
    /// a save survives a new repository; the old keys are carried over only
    /// while no save exists under the new key, which then always wins; one
    /// unreadable old key leaves the other; an unreadable or too new save gives
    /// the defaults and is left in the store; a carried-over save whose
    /// write fails is still played; a fresh store, a carry-over and a
    /// version-1 save all give a new player's gold, lives, jokers, streak and
    /// campaign by the rules; and a version-2 save keeps its gold and lives
    /// with a new player's jokers, streak and campaign. Module 38: a version-4
    /// save with a refused name is still loaded, with one problem naming the
    /// field, and a carried-over save gets a new player's profile with haptics on.
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
            SaveFixture.AssertSameSave(SaveData.CreateDefault(SaveFixture.Rules), load.Data);
            AssertNewPlayerMetaSections(load.Data);
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
            AssertNewPlayerMetaSections(load.Data);
            CollectionAssert.IsEmpty(load.Problems);
            CollectionAssert.AreEqual(new[] { SaveRepository.SaveKey }, store.SavedKeys, "exactly one save, under the new key");
            Assert.IsTrue(store.TryLoad(SaveRepository.SaveKey, out var written));
            Assert.IsTrue(new JsonSaveCodec().TryDecode(written, SaveFixture.Rules, out var writtenSave, out var problem, out _), problem);
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
            SaveFixture.AssertSameSave(SaveData.CreateDefault(SaveFixture.Rules), load.Data);
            Assert.AreEqual(1, load.Problems.Count);
            StringAssert.Contains(SaveRepository.SaveKey, load.Problems[0], "the problem names the key it could not read");
            AssertStoredUnchanged(store, Unreadable);
        }

        [Test]
        public void Load_VersionOneSave_IsLoadedWithNewPlayerMetaSections()
        {
            var store = new MemorySaveStore();
            store.Seed(SaveRepository.SaveKey, "{\"formatVersion\":1,\"completedLevelIds\":[0,1,3],\"soundOn\":false}");

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Loaded, load.Outcome);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, load.Data.Completed.Ids);
            Assert.IsFalse(load.Data.Sound.IsOn);
            AssertNewPlayerMetaSections(load.Data);
            CollectionAssert.IsEmpty(load.Problems);
            CollectionAssert.IsEmpty(store.SavedKeys, "Load writes nothing; the next change writes the current version");
        }

        [Test]
        public void Load_VersionTwoSave_IsLoadedWithNewPlayerStreakCampaignAndJokers()
        {
            var store = new MemorySaveStore();
            store.Seed(
                SaveRepository.SaveKey,
                "{\"formatVersion\":2,\"completedLevelIds\":[0,1,3],\"soundOn\":false,\"gold\":640,\"lifetimeGold\":140,\"lives\":2,\"livesRegenStartUtcTicks\":638640000000000000}");

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Loaded, load.Outcome);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, load.Data.Completed.Ids);
            Assert.IsFalse(load.Data.Sound.IsOn);
            Assert.AreEqual(640, load.Data.Wallet.Balance);
            Assert.AreEqual(140, load.Data.Wallet.LifetimeEarned);
            Assert.AreEqual(2, load.Data.Lives.StoredCount);
            AssertNewPlayerStreakCampaignAndJokers(load.Data);
            CollectionAssert.IsEmpty(load.Problems);
        }

        [Test]
        public void Load_FutureFormatVersion_IsUnreadableAndLeavesTheTextUnchanged()
        {
            var store = new MemorySaveStore();
            var future = $"{{\"formatVersion\":{JsonSaveCodec.CurrentFormatVersion + 1},\"completedLevelIds\":[1],\"soundOn\":false}}";
            store.Seed(SaveRepository.SaveKey, future);

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Unreadable, load.Outcome);
            SaveFixture.AssertSameSave(SaveData.CreateDefault(SaveFixture.Rules), load.Data);
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
        public void Load_VersionFourSaveWithARefusedName_IsLoadedWithAProblemNamingTheField()
        {
            var store = new MemorySaveStore();
            var dto = JsonUtility.FromJson<SaveDataDto>(new JsonSaveCodec().Encode(SaveFixture.EveryField()));
            dto.playerName = "Ali!";
            var text = JsonUtility.ToJson(dto);
            store.Seed(SaveRepository.SaveKey, text);

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.Loaded, load.Outcome);
            Assert.AreEqual(MetaRulesFixture.DefaultPlayerName, load.Data.Profile.Name);
            Assert.AreEqual(SaveFixture.EveryFieldAvatarId, load.Data.Profile.AvatarId, "the rest of the save is read");
            Assert.AreEqual(1, load.Problems.Count);
            StringAssert.Contains(SaveRepository.SaveKey, load.Problems[0], "the problem names the key");
            StringAssert.Contains("playerName", load.Problems[0], "the problem names the field");
            AssertStoredUnchanged(store, text);
        }

        [Test]
        public void Load_OnlyLegacyKeysWithSoundOff_HasANewPlayersProfileAndHapticsOn()
        {
            var store = new MemorySaveStore();
            store.Seed(SoundSetting.LegacySaveKey, "off");

            var load = Repository(store).Load();

            Assert.AreEqual(SaveLoadOutcome.CarriedOver, load.Outcome);
            Assert.AreEqual(MetaRulesFixture.DefaultPlayerName, load.Data.Profile.Name);
            Assert.AreEqual(MetaRulesFixture.DefaultAvatarId, load.Data.Profile.AvatarId);
            Assert.IsTrue(load.Data.Haptics.IsOn, "vibration no longer follows the old sound key");
        }

        [Test]
        public void Save_AnySaveData_WritesOnceUnderTheSaveKey()
        {
            var store = new MemorySaveStore();
            var codec = new JsonSaveCodec();
            var save = SaveFixture.EveryField();

            new SaveRepository(store, codec, SaveFixture.Rules).Save(save);

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
        public void Constructor_NullStoreCodecOrRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SaveRepository(null, new JsonSaveCodec(), SaveFixture.Rules));
            Assert.Throws<ArgumentNullException>(() => new SaveRepository(new MemorySaveStore(), null, SaveFixture.Rules));
            Assert.Throws<ArgumentNullException>(() => new SaveRepository(new MemorySaveStore(), new JsonSaveCodec(), null));
        }

        [Test]
        public void SaveKey_IsTheSpecified()
        {
            Assert.AreEqual("GateRush.Save", SaveRepository.SaveKey);
        }

        private static SaveRepository Repository(MemorySaveStore store) =>
            new SaveRepository(store, new JsonSaveCodec(), SaveFixture.Rules);

        /// <summary>
        /// Asserts the meta sections a new player gets by
        /// <see cref="SaveFixture.Rules"/>: the starting gold, none of it
        /// earned, lives at the cap, and the streak, campaign and jokers of
        /// <see cref="AssertNewPlayerStreakCampaignAndJokers"/>.
        /// </summary>
        private static void AssertNewPlayerMetaSections(SaveData data)
        {
            Assert.AreEqual(SaveFixture.Rules.StartingGold, data.Wallet.Balance, "starting gold");
            Assert.AreEqual(0, data.Wallet.LifetimeEarned, "lifetime gold");
            Assert.AreEqual(SaveFixture.Rules.LifeCap, data.Lives.StoredCount, "full lives");
            AssertNewPlayerStreakCampaignAndJokers(data);
        }

        /// <summary>
        /// Asserts no streak and no best, the campaign at its start, and the
        /// starting jokers of <see cref="SaveFixture.Rules"/>.
        /// </summary>
        private static void AssertNewPlayerStreakCampaignAndJokers(SaveData data)
        {
            Assert.AreEqual(0, data.Streak.Current, "no streak");
            Assert.AreEqual(0, data.Streak.Best, "no best streak");
            Assert.AreEqual(0, data.Campaign.Position, "the campaign at its start");
            foreach (JokerKind kind in Enum.GetValues(typeof(JokerKind)))
            {
                Assert.AreEqual(SaveFixture.Rules.StartingJokers(kind), data.Jokers.CountOf(kind), $"starting {kind} jokers");
            }
        }

        private static void AssertStoredUnchanged(MemorySaveStore store, string expected)
        {
            Assert.IsTrue(store.TryLoad(SaveRepository.SaveKey, out var stored));
            Assert.AreEqual(expected, stored, "the unreadable save is left in the store");
            CollectionAssert.IsEmpty(store.SavedKeys, "Load writes nothing");
        }
    }
}
