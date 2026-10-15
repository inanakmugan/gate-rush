using System;

namespace GateRush.Meta
{
    /// <summary>
    /// Everything the game keeps, as one save (Module 29, D56): saved whole
    /// under one key by <see cref="SaveRepository"/>, never a section alone,
    /// so no change is ever saved half. Module 30 added the wallet and the
    /// lives; Module 31 adds its sections here.
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
        public SaveData(CompletedLevels completed, SoundSetting sound, Wallet wallet, Lives lives)
        {
            Completed = completed ?? throw new ArgumentNullException(nameof(completed));
            Sound = sound ?? throw new ArgumentNullException(nameof(sound));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Lives = lives ?? throw new ArgumentNullException(nameof(lives));
        }

        /// <summary>The levels the player has won.</summary>
        public CompletedLevels Completed { get; }

        /// <summary>Whether sound and vibration are on.</summary>
        public SoundSetting Sound { get; }

        /// <summary>The player's gold: the balance and the lifetime gold earned.</summary>
        public Wallet Wallet { get; }

        /// <summary>The player's lives.</summary>
        public Lives Lives { get; }

        /// <summary>
        /// The save of a player who has never played, by
        /// <paramref name="rules"/>: no level completed, sound on, the
        /// starting gold and full lives. Also what a save that cannot be read
        /// gives, and what an older save takes for the sections it lacks
        /// (D60).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static SaveData CreateDefault(MetaRules rules) =>
            new SaveData(new CompletedLevels(), new SoundSetting(), Wallet.Starting(rules), Lives.Full(rules));
    }
}
