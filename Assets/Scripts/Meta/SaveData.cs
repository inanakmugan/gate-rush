using System;

namespace GateRush.Meta
{
    /// <summary>
    /// Everything the game keeps, as one save (Module 29, D56): saved whole
    /// under one key by <see cref="SaveRepository"/>, never a section alone,
    /// so no change is ever saved half. Module 30 added the wallet and the
    /// lives; Module 31 the jokers, the win streak and the campaign position;
    /// Module 38 the player's profile and the haptics setting.
    /// </summary>
    /// <remarks>
    /// The sections are the live objects the game changes: marking a level
    /// completed, flipping the sound, earning gold or losing a life changes
    /// this save, and the caller then saves it whole.
    /// </remarks>
    public sealed class SaveData
    {
        /// <summary>A save holding the given sections.</summary>
        /// <exception cref="ArgumentNullException">A section is null.</exception>
        public SaveData(
            CompletedLevels completed,
            SoundSetting sound,
            Wallet wallet,
            Lives lives,
            JokerInventory jokers,
            WinStreak streak,
            CampaignProgress campaign,
            PlayerProfile profile,
            HapticsSetting haptics)
        {
            Completed = completed ?? throw new ArgumentNullException(nameof(completed));
            Sound = sound ?? throw new ArgumentNullException(nameof(sound));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Lives = lives ?? throw new ArgumentNullException(nameof(lives));
            Jokers = jokers ?? throw new ArgumentNullException(nameof(jokers));
            Streak = streak ?? throw new ArgumentNullException(nameof(streak));
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
        }

        /// <summary>The levels the player has won.</summary>
        public CompletedLevels Completed { get; }

        /// <summary>Whether sound is on.</summary>
        public SoundSetting Sound { get; }

        /// <summary>The player's gold: the balance and the lifetime gold earned.</summary>
        public Wallet Wallet { get; }

        /// <summary>The player's lives.</summary>
        public Lives Lives { get; }

        /// <summary>The jokers the player holds.</summary>
        public JokerInventory Jokers { get; }

        /// <summary>The campaign win streak and the best one.</summary>
        public WinStreak Streak { get; }

        /// <summary>Where the player is in the campaign.</summary>
        public CampaignProgress Campaign { get; }

        /// <summary>The player's name and avatar.</summary>
        public PlayerProfile Profile { get; }

        /// <summary>Whether vibration is on.</summary>
        public HapticsSetting Haptics { get; }

        /// <summary>
        /// The save of a player who has never played, by
        /// <paramref name="rules"/>: no level completed, sound on, the
        /// starting gold, full lives, the starting jokers, no streak, the
        /// campaign at its first level, the default name and avatar, and
        /// haptics on. Also what a save that cannot be read
        /// gives, and what an older save takes for the sections it lacks
        /// (D60).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static SaveData CreateDefault(MetaRules rules) =>
            new SaveData(
                new CompletedLevels(),
                new SoundSetting(),
                Wallet.Starting(rules),
                Lives.Full(rules),
                JokerInventory.Starting(rules),
                WinStreak.Starting(),
                CampaignProgress.Starting(),
                PlayerProfile.Starting(rules),
                new HapticsSetting());
    }
}
