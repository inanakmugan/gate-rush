namespace GateRush.Meta
{
    /// <summary>
    /// Whether the player wants sound and vibration (Module 25): one switch
    /// for both, on until the player turns it off. Kept as a section of
    /// <see cref="SaveData"/> (Module 29) until Phase 6 builds a settings
    /// screen.
    /// </summary>
    /// <remarks>
    /// <para><b>Legacy data.</b> Before Module 29 the setting was saved on its
    /// own under <see cref="LegacySaveKey"/>, as <c>on</c> or <c>off</c>. That
    /// format is now only read, by <see cref="SaveRepository"/> when it carries
    /// an old save over; nothing writes it any more.</para>
    /// <para><b>Unreadable legacy data reads as on.</b> Saved data comes back
    /// from outside the game and may be anything; reading it never throws.
    /// <see cref="TryFromData"/> says whether it could be read, so the caller
    /// — this assembly does not log — can report it.</para>
    /// </remarks>
    public sealed class SoundSetting
    {
        /// <summary>
        /// The key the setting was saved under on its own before Module 29.
        /// Read only while no save exists under <see cref="SaveRepository.SaveKey"/>.
        /// </summary>
        public const string LegacySaveKey = "GateRush.SoundOn";

        private const string OnData = "on";
        private const string OffData = "off";

        /// <summary>True when sound and vibration are wanted. Starts true.</summary>
        public bool IsOn { get; set; } = true;

        /// <summary>
        /// Reads the legacy <paramref name="data"/> into
        /// <paramref name="setting"/>, which is never null. False when the
        /// data cannot be read — anything but <c>on</c> or <c>off</c> — and
        /// <paramref name="setting"/> is then on. Null and empty data are
        /// readable and mean nothing was chosen yet: on.
        /// </summary>
        public static bool TryFromData(string data, out SoundSetting setting)
        {
            setting = new SoundSetting();
            if (string.IsNullOrEmpty(data) || data == OnData)
            {
                return true;
            }

            if (data == OffData)
            {
                setting.IsOn = false;
                return true;
            }

            return false;
        }
    }
}
