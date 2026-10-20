using System;
using System.Globalization;
using GateRush.Meta;
using GateRush.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Modules 29 to 31's <see cref="JsonSaveCodec"/>:
    /// <c>formatVersion</c> is written first and the ids ascending; every save
    /// survives <see cref="JsonSaveCodec.Encode"/> and back; duplicate ids read
    /// as one; version-1 and version-2 saves read the sections they lack as a
    /// new player has them, by the rules they are given (D60); a version-3
    /// save reads its streak, campaign and jokers; and text it would not have
    /// written — not JSON, empty, with a missing, non-positive or too new
    /// version, negative gold, lives, streaks, position or jokers, a best
    /// streak below the streak, ticks outside a date's range — is refused
    /// with a problem and no data, never misread or corrected. Module 38: a
    /// version-4 save reads its name, avatar and haptics; versions 1 to 3 read
    /// them as a new player has them, haptics on whatever the sound; and a
    /// version-4 name the rule refuses or an empty avatar id reads as the
    /// rules' default and is reported as a reset, not refused.
    /// </summary>
    public class JsonSaveCodecTests
    {
        // Text of an older version stays readable when the codec moves on, so
        // these literals name the version they were written in.
        private const int VersionOne = 1;
        private const int VersionTwo = 2;
        private const int VersionThree = 3;
        private const int VersionFour = 4;

        private const int Gold = 640;
        private const int LifetimeGold = 140;
        private const int SomeLives = 2;

        private const int Streak = 2;
        private const int BestStreak = 6;
        private const int Position = 4;
        private const int Clocks = 1;
        private const int Rockets = 5;
        private const int Brooms = 7;

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

            var text = new JsonSaveCodec().Encode(SaveFixture.NewPlayerSave(completed, new SoundSetting()));

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
            var save = SaveFixture.NewPlayerSave(completed, new SoundSetting { IsOn = isSoundOn });
            var codec = new JsonSaveCodec();

            var isReadable = codec.TryDecode(codec.Encode(save), SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsTrue(isReadable, problem);
            Assert.IsNull(problem);
            SaveFixture.AssertSameSave(save, read);
        }

        [Test]
        public void TryDecode_OfEncodeOfEveryField_GivesTheSameSave()
        {
            var save = SaveFixture.EveryField();
            var codec = new JsonSaveCodec();

            var isReadable = codec.TryDecode(codec.Encode(save), SaveFixture.Rules, out var read, out var problem, out var resets);

            Assert.IsTrue(isReadable, problem);
            CollectionAssert.IsEmpty(resets, "a saved name and avatar are read as they are");
            SaveFixture.AssertSameSave(save, read);
        }

        [Test]
        public void TryDecode_DuplicateIds_ReadsThemAsOne()
        {
            var text = $"{{\"formatVersion\":{VersionOne},\"completedLevelIds\":[3,1,3,1],\"soundOn\":true}}";

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

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
            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            Assert.That(problem, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void TryDecode_VersionAboveCurrent_IsFalseWithAProblemAndNullData()
        {
            var tooNew = JsonSaveCodec.CurrentFormatVersion + 1;
            var text = $"{{\"formatVersion\":{tooNew},\"completedLevelIds\":[1],\"soundOn\":true}}";

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            StringAssert.Contains(tooNew.ToString(), problem, "the problem names the version it refused");
        }

        [Test]
        public void CurrentFormatVersion_IsFour()
        {
            Assert.AreEqual(VersionFour, JsonSaveCodec.CurrentFormatVersion);
        }

        [Test]
        public void TryDecode_VersionFourSave_ReadsNameAvatarAndHaptics()
        {
            var text = VersionFourText(SaveFixture.EveryFieldName, SaveFixture.EveryFieldAvatarId, false);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out var resets);

            Assert.IsTrue(isReadable, problem);
            CollectionAssert.IsEmpty(resets);
            Assert.AreEqual(SaveFixture.EveryFieldName, read.Profile.Name);
            Assert.AreEqual(SaveFixture.EveryFieldAvatarId, read.Profile.AvatarId);
            Assert.IsFalse(read.Haptics.IsOn);
            Assert.AreEqual(Streak, read.Streak.Current, "the earlier sections are still read");
        }

        [TestCase(VersionOne)]
        [TestCase(VersionTwo)]
        [TestCase(VersionThree)]
        public void TryDecode_OlderVersionWithSoundOff_ReadsANewPlayersProfileAndHapticsOn(int version)
        {
            var text = OlderText(version);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out var resets);

            Assert.IsTrue(isReadable, problem);
            CollectionAssert.IsEmpty(resets, "a missing section is a default, not a reset");
            Assert.IsFalse(read.Sound.IsOn, "the sound is kept");
            Assert.IsTrue(read.Haptics.IsOn, "vibration no longer follows the sound");
            Assert.AreEqual(MetaRulesFixture.DefaultPlayerName, read.Profile.Name, "the rules' default name");
            Assert.AreEqual(MetaRulesFixture.DefaultAvatarId, read.Profile.AvatarId, "the rules' default avatar");
            CollectionAssert.AreEqual(new[] { 1 }, read.Completed.Ids, "the completed levels are kept");
        }

        [Test]
        public void TryDecode_VersionThreeSaveWithLaterFields_IgnoresThem()
        {
            var text = VersionThreeText(Streak, BestStreak, Position, Clocks, Rockets, Brooms).TrimEnd('}')
                + ",\"playerName\":\"x!\",\"avatarId\":\"\",\"hapticsOn\":false}";

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out var resets);

            Assert.IsTrue(isReadable, problem);
            CollectionAssert.IsEmpty(resets);
            SaveFixture.AssertSameSave(
                new SaveData(
                    read.Completed, read.Sound, read.Wallet, read.Lives, read.Jokers, read.Streak, read.Campaign,
                    PlayerProfile.Starting(SaveFixture.Rules), new HapticsSetting()),
                read);
        }

        [TestCase("\"\"")]
        [TestCase("\"x!\"")]
        [TestCase("\"abcdefghijklmnopq\"")]
        [TestCase(null)]
        public void TryDecode_VersionFourRefusedName_ReadsTheDefaultNameAndReportsIt(string nameJson)
        {
            var text = VersionFourText(nameJson, SaveFixture.EveryFieldAvatarId, false, isNameRaw: true);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out var resets);

            Assert.IsTrue(isReadable, problem);
            Assert.AreEqual(MetaRulesFixture.DefaultPlayerName, read.Profile.Name);
            Assert.AreEqual(SaveFixture.EveryFieldAvatarId, read.Profile.AvatarId, "the avatar is still read");
            Assert.IsFalse(read.Haptics.IsOn, "haptics are still read");
            Assert.That(resets, Has.Exactly(1).Contains("playerName"), string.Join(" | ", resets));
        }

        [TestCase("")]
        [TestCase("  ")]
        [TestCase(null)]
        public void TryDecode_VersionFourEmptyAvatarId_ReadsTheDefaultAvatarAndReportsIt(string avatarId)
        {
            var text = VersionFourText(SaveFixture.EveryFieldName, avatarId, true);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out var resets);

            Assert.IsTrue(isReadable, problem);
            Assert.AreEqual(MetaRulesFixture.DefaultAvatarId, read.Profile.AvatarId);
            Assert.AreEqual(SaveFixture.EveryFieldName, read.Profile.Name, "the name is still read");
            Assert.That(resets, Has.Exactly(1).Contains("avatarId"), string.Join(" | ", resets));
        }

        [Test]
        public void TryDecode_VersionOneSave_ReadsLevelsSoundAndANewPlayersOtherSections()
        {
            var text = $"{{\"formatVersion\":{VersionOne},\"completedLevelIds\":[0,1,3],\"soundOn\":false}}";

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsTrue(isReadable, problem);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, read.Completed.Ids);
            Assert.IsFalse(read.Sound.IsOn);
            Assert.AreEqual(SaveFixture.Rules.StartingGold, read.Wallet.Balance, "starting gold, from the rules");
            Assert.AreEqual(0, read.Wallet.LifetimeEarned, "the starting gold is not earned");
            Assert.AreEqual(SaveFixture.Rules.LifeCap, read.Lives.StoredCount, "full lives, from the rules");
            SaveFixture.AssertSameSave(SaveFixture.NewPlayerSave(read.Completed, read.Sound), read);
        }

        [Test]
        public void TryDecode_VersionTwoSave_ReadsGoldAndLivesWithNewPlayerStreakCampaignAndJokers()
        {
            var text = VersionTwoText(Gold, LifetimeGold, SomeLives, SomeTicks);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsTrue(isReadable, problem);
            Assert.AreEqual(Gold, read.Wallet.Balance);
            Assert.AreEqual(LifetimeGold, read.Wallet.LifetimeEarned);
            Assert.AreEqual(SomeLives, read.Lives.StoredCount);
            Assert.AreEqual(SomeTicks, read.Lives.RegenStartUtc.Ticks);
            Assert.AreEqual(DateTimeKind.Utc, read.Lives.RegenStartUtc.Kind);
            AssertNewPlayerStreakCampaignAndJokers(read);
        }

        [Test]
        public void TryDecode_VersionTwoSaveWithLaterFields_IgnoresThem()
        {
            var text = VersionTwoText(Gold, LifetimeGold, SomeLives, SomeTicks).TrimEnd('}')
                + ",\"winStreak\":-1,\"bestWinStreak\":-1,\"campaignPosition\":-1,\"clocks\":-1,\"rockets\":-1,\"brooms\":-1}";

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsTrue(isReadable, problem);
            AssertNewPlayerStreakCampaignAndJokers(read);
        }

        [Test]
        public void TryDecode_VersionThreeSave_ReadsStreakCampaignAndJokers()
        {
            var text = VersionThreeText(Streak, BestStreak, Position, Clocks, Rockets, Brooms);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsTrue(isReadable, problem);
            Assert.AreEqual(Gold, read.Wallet.Balance);
            Assert.AreEqual(SomeLives, read.Lives.StoredCount);
            Assert.AreEqual(Streak, read.Streak.Current);
            Assert.AreEqual(BestStreak, read.Streak.Best);
            Assert.AreEqual(Position, read.Campaign.Position);
            Assert.AreEqual(Clocks, read.Jokers.CountOf(JokerKind.Clock));
            Assert.AreEqual(Rockets, read.Jokers.CountOf(JokerKind.Rocket));
            Assert.AreEqual(Brooms, read.Jokers.CountOf(JokerKind.Broom));
        }

        [TestCase(-1, BestStreak, Position, Clocks, Rockets, Brooms, "winStreak")]
        [TestCase(Streak, -1, Position, Clocks, Rockets, Brooms, "bestWinStreak")]
        [TestCase(Streak, BestStreak, -1, Clocks, Rockets, Brooms, "campaignPosition")]
        [TestCase(Streak, BestStreak, Position, -1, Rockets, Brooms, "clocks")]
        [TestCase(Streak, BestStreak, Position, Clocks, -1, Brooms, "rockets")]
        [TestCase(Streak, BestStreak, Position, Clocks, Rockets, -1, "brooms")]
        public void TryDecode_NegativeVersionThreeField_IsFalseWithAProblem(
            int winStreak, int bestWinStreak, int campaignPosition, int clocks, int rockets, int brooms, string field)
        {
            var text = VersionThreeText(winStreak, bestWinStreak, campaignPosition, clocks, rockets, brooms);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            StringAssert.Contains(field, problem, "the problem names the field");
        }

        [Test]
        public void TryDecode_BestStreakBelowStreak_IsFalseWithAProblem()
        {
            var text = VersionThreeText(BestStreak, Streak, Position, Clocks, Rockets, Brooms);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            StringAssert.Contains("bestWinStreak", problem, "the problem names the field");
        }

        [TestCase(-1, LifetimeGold, SomeLives, "gold")]
        [TestCase(Gold, -1, SomeLives, "lifetimeGold")]
        [TestCase(Gold, LifetimeGold, -1, "lives")]
        public void TryDecode_NegativeGoldLifetimeOrLives_IsFalseWithAProblem(int gold, int lifetimeGold, int lives, string field)
        {
            var text = VersionTwoText(gold, lifetimeGold, lives, SomeTicks);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

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

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsFalse(isReadable);
            Assert.IsNull(read);
            StringAssert.Contains("livesRegenStartUtcTicks", problem, "the problem names the field");
        }

        [Test]
        public void TryDecode_LivesAboveCap_ReadsAsCap()
        {
            var text = VersionTwoText(Gold, LifetimeGold, SaveFixture.Rules.LifeCap + 1, SomeTicks);

            var isReadable = new JsonSaveCodec().TryDecode(text, SaveFixture.Rules, out var read, out var problem, out _);

            Assert.IsTrue(isReadable, problem);
            Assert.AreEqual(SaveFixture.Rules.LifeCap, read.Lives.StoredCount);
        }

        [Test]
        public void TryDecode_NullRules_Throws()
        {
            var text = VersionTwoText(Gold, LifetimeGold, SomeLives, SomeTicks);

            Assert.Throws<ArgumentNullException>(() => new JsonSaveCodec().TryDecode(text, null, out _, out _, out _));
        }

        /// <summary>Asserts the streak, campaign and jokers a new player gets by <see cref="SaveFixture.Rules"/>.</summary>
        private static void AssertNewPlayerStreakCampaignAndJokers(SaveData read)
        {
            Assert.AreEqual(0, read.Streak.Current, "no streak");
            Assert.AreEqual(0, read.Streak.Best, "no best streak");
            Assert.AreEqual(0, read.Campaign.Position, "the campaign at its start");
            foreach (JokerKind kind in Enum.GetValues(typeof(JokerKind)))
            {
                Assert.AreEqual(SaveFixture.Rules.StartingJokers(kind), read.Jokers.CountOf(kind), $"starting {kind} jokers, from the rules");
            }
        }

        private static string VersionTwoText(int gold, int lifetimeGold, int lives, long ticks) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{{\"formatVersion\":{0},\"completedLevelIds\":[1],\"soundOn\":true,\"gold\":{1},\"lifetimeGold\":{2},\"lives\":{3},\"livesRegenStartUtcTicks\":{4}}}",
                VersionTwo, gold, lifetimeGold, lives, ticks);

        private static string VersionThreeText(int winStreak, int bestWinStreak, int campaignPosition, int clocks, int rockets, int brooms) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{{\"formatVersion\":{0},\"completedLevelIds\":[1],\"soundOn\":true,\"gold\":{1},\"lifetimeGold\":{2},\"lives\":{3},\"livesRegenStartUtcTicks\":{4}," +
                "\"winStreak\":{5},\"bestWinStreak\":{6},\"campaignPosition\":{7},\"clocks\":{8},\"rockets\":{9},\"brooms\":{10}}}",
                VersionThree, Gold, LifetimeGold, SomeLives, SomeTicks, winStreak, bestWinStreak, campaignPosition, clocks, rockets, brooms);

        /// <summary>
        /// A version-4 save with the version-3 fields of <see cref="VersionThreeText"/>
        /// and the given profile. A null name or avatar id leaves its field
        /// out; <paramref name="isNameRaw"/> writes the name as the JSON given,
        /// quotes included, rather than quoting it.
        /// </summary>
        private static string VersionFourText(string name, string avatarId, bool isHapticsOn, bool isNameRaw = false)
        {
            var text = VersionThreeText(Streak, BestStreak, Position, Clocks, Rockets, Brooms)
                .Replace($"\"formatVersion\":{VersionThree}", $"\"formatVersion\":{VersionFour}")
                .TrimEnd('}');
            if (name != null)
            {
                text += isNameRaw ? $",\"playerName\":{name}" : $",\"playerName\":\"{name}\"";
            }

            if (avatarId != null)
            {
                text += $",\"avatarId\":\"{avatarId}\"";
            }

            return text + (isHapticsOn ? ",\"hapticsOn\":true}" : ",\"hapticsOn\":false}");
        }

        /// <summary>A save of <paramref name="version"/>, 1 to 3, with level 1 completed and sound off.</summary>
        private static string OlderText(int version)
        {
            var text = version == VersionOne
                ? $"{{\"formatVersion\":{VersionOne},\"completedLevelIds\":[1],\"soundOn\":true}}"
                : version == VersionTwo
                    ? VersionTwoText(Gold, LifetimeGold, SomeLives, SomeTicks)
                    : VersionThreeText(Streak, BestStreak, Position, Clocks, Rockets, Brooms);
            return text.Replace("\"soundOn\":true", "\"soundOn\":false");
        }
    }
}
