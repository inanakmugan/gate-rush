using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// The shared save fixtures. <see cref="EveryField"/> sets every field of
    /// <see cref="SaveData"/> away from its default, and
    /// <see cref="AssertSameSave"/> compares every field: a section added later
    /// must be added to both, or the round trips that use them stop proving
    /// that nothing is lost on the way through the codec.
    /// <see cref="Rules"/> are the rules every save test runs with.
    /// </summary>
    public static class SaveFixture
    {
        /// <summary>
        /// The rules of the save tests. Deliberately not the values of a fresh
        /// <c>MetaConfig</c>, so a codec or repository that used numbers of
        /// its own instead of these rules would fail (D60).
        /// </summary>
        public static readonly MetaRules Rules = MetaRulesFixture.Build();

        /// <summary>
        /// A save with no field at its default: ids that are not contiguous,
        /// not in order and include 0; sound off; gold and lifetime gold that
        /// differ from each other and from <see cref="Rules"/>' starting gold;
        /// lives below the cap, waiting since a moment that is not the default;
        /// joker counts that differ from each other and from the starting
        /// ones; a streak below a best that differ; a campaign past its
        /// start; a name with Turkish letters and an avatar, neither the
        /// rules' default; and haptics off.
        /// </summary>
        public static SaveData EveryField() =>
            new SaveData(
                CompletedLevels.Of(new[] { 7, 0, 3, 12 }),
                new SoundSetting { IsOn = false },
                new Wallet(640, 140),
                new Lives(Rules, 2, new DateTime(2026, 10, 15, 12, 34, 56, DateTimeKind.Utc)),
                new JokerInventory(1, 4, 6),
                new WinStreak(2, 6),
                new CampaignProgress(4),
                new PlayerProfile(EveryFieldName, EveryFieldAvatarId),
                new HapticsSetting { IsOn = false });

        /// <summary>The name <see cref="EveryField"/> holds: Turkish letters Fredoka lacks, and a space.</summary>
        public const string EveryFieldName = "Ayşe Nur İğde";

        /// <summary>The avatar id <see cref="EveryField"/> holds.</summary>
        public const string EveryFieldAvatarId = "panda";

        /// <summary>
        /// A save holding <paramref name="completed"/> and <paramref name="sound"/>,
        /// and every other section a new player's by <see cref="Rules"/>.
        /// </summary>
        public static SaveData NewPlayerSave(CompletedLevels completed, SoundSetting sound) =>
            new SaveData(
                completed,
                sound,
                Wallet.Starting(Rules),
                Lives.Full(Rules),
                JokerInventory.Starting(Rules),
                WinStreak.Starting(),
                CampaignProgress.Starting(),
                PlayerProfile.Starting(Rules),
                new HapticsSetting());

        /// <summary>Asserts that <paramref name="actual"/> holds what <paramref name="expected"/> holds, field by field.</summary>
        public static void AssertSameSave(SaveData expected, SaveData actual)
        {
            Assert.IsNotNull(actual);
            CollectionAssert.AreEqual(expected.Completed.Ids, actual.Completed.Ids, "completed level ids");
            Assert.AreEqual(expected.Sound.IsOn, actual.Sound.IsOn, "sound on");
            Assert.AreEqual(expected.Wallet.Balance, actual.Wallet.Balance, "gold");
            Assert.AreEqual(expected.Wallet.LifetimeEarned, actual.Wallet.LifetimeEarned, "lifetime gold");
            Assert.AreEqual(expected.Lives.StoredCount, actual.Lives.StoredCount, "lives");
            Assert.AreEqual(expected.Lives.RegenStartUtc, actual.Lives.RegenStartUtc, "lives regen start");
            Assert.AreEqual(expected.Lives.RegenStartUtc.Kind, actual.Lives.RegenStartUtc.Kind, "lives regen start kind");
            foreach (JokerKind kind in Enum.GetValues(typeof(JokerKind)))
            {
                Assert.AreEqual(expected.Jokers.CountOf(kind), actual.Jokers.CountOf(kind), $"{kind} jokers");
            }

            Assert.AreEqual(expected.Streak.Current, actual.Streak.Current, "win streak");
            Assert.AreEqual(expected.Streak.Best, actual.Streak.Best, "best win streak");
            Assert.AreEqual(expected.Campaign.Position, actual.Campaign.Position, "campaign position");
            Assert.AreEqual(expected.Profile.Name, actual.Profile.Name, "player name");
            Assert.AreEqual(expected.Profile.AvatarId, actual.Profile.AvatarId, "avatar id");
            Assert.AreEqual(expected.Haptics.IsOn, actual.Haptics.IsOn, "haptics on");
        }
    }
}
