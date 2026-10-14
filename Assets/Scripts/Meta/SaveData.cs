using System;

namespace GateRush.Meta
{
    /// <summary>
    /// Everything the game keeps, as one save (Module 29, D56): saved whole
    /// under one key by <see cref="SaveRepository"/>, never a section alone,
    /// so no change is ever saved half. Modules 30 and 31 add their sections
    /// here.
    /// </summary>
    /// <remarks>
    /// The sections are the live objects the game changes: marking a level
    /// completed or flipping the sound changes this save, and the caller then
    /// saves it whole.
    /// </remarks>
    public sealed class SaveData
    {
        /// <summary>A save holding <paramref name="completed"/> and <paramref name="sound"/>.</summary>
        /// <exception cref="ArgumentNullException">A section is null.</exception>
        public SaveData(CompletedLevels completed, SoundSetting sound)
        {
            Completed = completed ?? throw new ArgumentNullException(nameof(completed));
            Sound = sound ?? throw new ArgumentNullException(nameof(sound));
        }

        /// <summary>The levels the player has won.</summary>
        public CompletedLevels Completed { get; }

        /// <summary>Whether sound and vibration are on.</summary>
        public SoundSetting Sound { get; }

        /// <summary>
        /// The save of a player who has never played: no level completed,
        /// sound on. Also what a save that cannot be read gives.
        /// </summary>
        public static SaveData CreateDefault() => new SaveData(new CompletedLevels(), new SoundSetting());
    }
}
