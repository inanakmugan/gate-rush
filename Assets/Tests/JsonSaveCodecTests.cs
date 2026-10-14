using GateRush.Meta;
using GateRush.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 29's <see cref="JsonSaveCodec"/>: <c>formatVersion</c> is
    /// written first and the ids ascending; every save survives
    /// <see cref="JsonSaveCodec.Encode"/> and back; duplicate ids read as one;
    /// and text it would not have written — not JSON, empty, with a missing,
    /// non-positive or too new version — is refused with a problem and no
    /// data, never misread.
    /// </summary>
    public class JsonSaveCodecTests
    {
        // Version-1 text stays readable when the codec moves on, so these
        // literals name the version they were written in.
        private const int VersionOne = 1;

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

            var text = new JsonSaveCodec().Encode(new SaveData(completed, new SoundSetting()));

            var written = JsonUtility.FromJson<SaveDataDto>(text);
            CollectionAssert.AreEqual(new[] { 0, 3, 7, 12 }, written.completedLevelIds);
        }

        [Test]
        public void Encode_AnySave_WritesTheCurrentFormatVersion()
        {
            var text = new JsonSaveCodec().Encode(SaveData.CreateDefault());

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
            var save = new SaveData(completed, new SoundSetting { IsOn = isSoundOn });
            var codec = new JsonSaveCodec();

            var isReadable = codec.TryDecode(codec.Encode(save), out var read, out var problem);

            Assert.IsTrue(isReadable, problem);
            Assert.IsNull(problem);
            SaveFixture.AssertSameSave(save, read);
        }

        [Test]
        public void TryDecode_OfEncodeOfEveryField_GivesTheSameSave()
        {
            var save = SaveFixture.EveryField();
            var codec = new JsonSaveCodec();

            var isReadable = codec.TryDecode(codec.Encode(save), out var read, out var problem);

            Assert.IsTrue(isReadable, problem);
            SaveFixture.AssertSameSave(save, read);
        }

        [Test]
        public void TryDecode_DuplicateIds_ReadsThemAsOne()
        {
            var text = $"{{\"formatVersion\":{VersionOne},\"completedLevelIds\":[3,1,3,1],\"soundOn\":true}}";

            var isReadable = new JsonSaveCodec().TryDecode(text, out var read, out var problem);

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
            var isReadable = new JsonSaveCodec().TryDecode(text, out var read, out var problem);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            Assert.That(problem, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void TryDecode_VersionAboveCurrent_IsFalseWithAProblemAndNullData()
        {
            var tooNew = JsonSaveCodec.CurrentFormatVersion + 1;
            var text = $"{{\"formatVersion\":{tooNew},\"completedLevelIds\":[1],\"soundOn\":true}}";

            var isReadable = new JsonSaveCodec().TryDecode(text, out var read, out var problem);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            StringAssert.Contains(tooNew.ToString(), problem, "the problem names the version it refused");
        }
    }
}
