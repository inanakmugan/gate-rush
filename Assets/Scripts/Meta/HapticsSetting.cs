namespace GateRush.Meta
{
    /// <summary>
    /// Whether the player wants vibration (Module 38, D71): a section of
    /// <see cref="SaveData"/>, independent of <see cref="SoundSetting"/>.
    /// On for a new player and for a save older than format version 4; before
    /// that version vibration followed the sound, and that is not carried over.
    /// </summary>
    public sealed class HapticsSetting
    {
        /// <summary>True when vibration is wanted. Starts true.</summary>
        public bool IsOn { get; set; } = true;
    }
}
