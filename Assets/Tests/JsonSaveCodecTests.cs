using System;
using System.Globalization;
using GateRush.Meta;
using GateRush.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Modules 29 and 30's <see cref="JsonSaveCodec"/>:
    /// <c>formatVersion</c> is written first and the ids ascending; every save
    /// survives <see cref="JsonSaveCodec.Encode"/> and back; duplicate ids read
    /// as one; a version-1 save reads with the starting gold and full lives of
    /// the rules it is given (D60); and text it would not have written — not
    /// JSON, empty, with a missing, non-positive or too new version, negative
    /// gold or lives, ticks outside a date's range — is refused with a problem
    /// and no data, never misread or corrected.
    /// </summary>
    public class JsonSaveCodecTests
    {
        // Text of an older version stays readable when the codec moves on, so
        // these literals name the version they were written in.
        private const int VersionOne = 1;
        private const int VersionTwo = 2;

        private const int Gold = 640;
        private const int LifetimeGold = 140;
        private const int SomeLives = 2;

        private static readonly long SomeTicks = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc).Ticks;

        private const string FormatVersionKey = "\"formatVersion\"";

        [Test]
        public void Encode_AnySave_WritesFormatVersionFirst()
        {
            var codec = new JsonSaveCodec();

            var text = codec.Encode(SaveFixture.EveryField());

            var firstKey = text.IndexOf('"');
            Assert.AreEqual(firstKey, text.IndexOf(FormatVersionKey), $"the first key in: {text}");
        }

        [Test]
        public void Encode_IdsMarkedOutOfOrder_WritesThemAscending()
        {
            var completed = new CompletedLevels();
            foreach (var id in new[] { 12, 0, 7, 3 })
            {
                completed.MarkCompleted(id);
            }

            var text = new JsonSaveCodec().Encode(new SaveData(completed, new SoundSetting(), Wallet.Starting(SaveFixture.Rules), Lives.Full(SaveFixture.Rules)));

            var written = JsonUtility.FromJson<SaveDataDto>(text);
            CollectionAssert.AreEqual(new[] { 0, 3, 7, 12 }, written.completedLevelIds);
        }

        [Test]
        public void Encode_AnySave_WritesTheCurrentFormatVersion()
        {
            var text = new JsonSaveCodec().Encode(SaveData.CreateDefault(SaveFixture.Rules));

            var written = JsonUtility.FromJson<SaveDataDto>(text);
            Assert.AreEqual(JsonSaveCodec.CurrentFormatVersion, written.formatVersion);
        }

        [TestCase(false, true)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(true, false)]
        public void TryDecode_OfEncode_GivesTheSameSave(bool hasCompletedLevels, bool isSoundOn)
        {
            var completed = hasCompletedLevels ? CompletedLevels.Of(new[] { 4, 1 }) : new CompletedLevels();
            var save = new SaveData(completed, new SoundSetting { IsOn = isSoundOn }, Wallet.Starting(SaveFixture.Rules), Lives.Full(SaveFixture.Rules));
            var codec = new JsonSaveCodec();

            var isReadable = codec.TryDecode(codec.Encode(save), SaveFixture.Rules, out var read, out var problem);

            Assert.IsTrue(isReadable, problem);
            Assert.IsNull(problem);
            SaveFixture.AssertSameSave(save, read);
        }

        [Test]
        public void TryDecode_OfEncodeOfEveryField_GivesTheSameSave()
        {
            var save = SaveFixture.EveryField();
            var codec = new JsonSaveCodec();

            var isReadable = codec.TryDecode(codec.Encode(save), SaveFixture.Rules, out var read, out var problem);

            Assert.IsTrue(isReadable, problem);
            SaveFixture.AssertSameSave(save, read);
        }

        [Test]
        public void TryDecode_DuplicateIds_ReadsThemAsOne()
        {
            var text = $"{{\"formatVersion\":{VersionOne},\"completedLevelIds\":[3,1,3,1],\"soundOn\":true}}";

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem);

            Assert.IsTrue(isReadable, problem);
            CollectionAssert.AreEqual(new[] { 1, 3 }, read.Completed.Ids);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("garbage")]
        [TestCase("[1,2]")]
        [TestCase("{\"completedLevelIds\":[1],\"soundOn\":true}")]
        [TestCase("{\"formatVersion\":0,\"completedLevelIds\":[1],\"soundOn\":true}")]
        [TestCase("{\"formatVersion\":-1,\"completedLevelIds\":[1],\"soundOn\":true}")]
        public void TryDecode_UnreadableText_IsFalseWithAProblemAndNullData(string text)
        {
            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            Assert.That(problem, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void TryDecode_VersionAboveCurrent_IsFalseWithAProblemAndNullData()
        {
            var tooNew = JsonSaveCodec.CurrentFormatVersion + 1;
            var text = $"{{\"formatVersion\":{tooNew},\"completedLevelIds\":[1],\"soundOn\":true}}";

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            StringAssert.Contains(tooNew.ToString(), problem, "the problem names the version it refused");
        }

        [Test]
        public void CurrentFormatVersion_IsTwo()
        {
            Assert.AreEqual(VersionTwo, JsonSaveCodec.CurrentFormatVersion);
        }

        [Test]
        public void TryDecode_VersionOneSave_ReadsLevelsSoundStartingGoldAndFullLives()
        {
            var text = $"{{\"formatVersion\":{VersionOne},\"completedLevelIds\":[0,1,3],\"soundOn\":false}}";

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem);

            Assert.IsTrue(isReadable, problem);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, read.Completed.Ids);
            Assert.IsFalse(read.Sound.IsOn);
            Assert.AreEqual(SaveFixture.Rules.StartingGold, read.Wallet.Balance, "starting gold, from the rules");
            Assert.AreEqual(0, read.Wallet.LifetimeEarned, "the starting gold is not earned");
            Assert.AreEqual(SaveFixture.Rules.LifeCap, read.Lives.StoredCount, "full lives, from the rules");
            SaveFixture.AssertSameSave(
                new SaveData(read.Completed, read.Sound, Wallet.Starting(SaveFixture.Rules), Lives.Full(SaveFixture.Rules)),
                read);
        }

        [Test]
        public void TryDecode_VersionTwoSave_ReadsGoldAndLives()
        {
            var text = VersionTwoText(Gold, LifetimeGold, SomeLives, SomeTicks);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem);

            Assert.IsTrue(isReadable, problem);
            Assert.AreEqual(Gold, read.Wallet.Balance);
            Assert.AreEqual(LifetimeGold, read.Wallet.LifetimeEarned);
            Assert.AreEqual(SomeLives, read.Lives.StoredCount);
            Assert.AreEqual(SomeTicks, read.Lives.RegenStartUtc.Ticks);
            Assert.AreEqual(DateTimeKind.Utc, read.Lives.RegenStartUtc.Kind);
        }

        [TestCase(-1, LifetimeGold, SomeLives, "gold")]
        [TestCase(Gold, -1, SomeLives, "lifetimeGold")]
        [TestCase(Gold, LifetimeGold, -1, "lives")]
        public void TryDecode_NegativeGoldLifetimeOrLives_IsFalseWithAProblem(int gold, int lifetimeGold, int lives, string field)
        {
            var text = VersionTwoText(gold, lifetimeGold, lives, SomeTicks);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            StringAssert.Contains(field, problem, "the problem names the field");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryDecode_TicksOutOfRange_IsFalseWithAProblem(bool isAboveMax)
        {
            var ticks = isAboveMax ? DateTime.MaxValue.Ticks + 1 : DateTime.MinValue.Ticks - 1;
            var text = VersionTwoText(Gold, LifetimeGold, SomeLives, ticks);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            StringAssert.Contains("livesRegenStartUtcTicks", problem, "the problem names the field");
        }

        [Test]
        public void TryDecode_LivesAboveCap_ReadsAsCap()
        {
            var text = VersionTwoText(Gold, LifetimeGold, SaveFixture.Rules.LifeCap + 1, SomeTicks);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem);

            Assert.IsTrue(isReadable, problem);
            Assert.AreEqual(SaveFixture.Rules.LifeCap, read.Lives.StoredCount);
        }

        [Test]
        public void TryDecode_NullRules_Throws()
        {
            var text = VersionTwoText(Gold, LifetimeGold, SomeLives, SomeTicks);

            Assert.Throws<ArgumentNullException>(() => new JsonSaveCodec().TryDecode(text, null, out _, out _));
        }

        private static string VersionTwoText(int gold, int lifetimeGold, int lives, long ticks) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{{\"formatVersion\":{0},\"completedLevelIds\":[1],\"soundOn\":true,\"gold\":{1},\"lifetimeGold\":{2},\"lives\":{3},\"livesRegenStartUtcTicks\":{4}}}",
                VersionTwo, gold, lifetimeGold, lives, ticks);
    }
}
