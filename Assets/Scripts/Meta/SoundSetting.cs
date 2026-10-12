namespace GateRush.Meta
{
    /// <summary>
    /// Whether the player wants sound and vibration (Module 25): one switch
    /// for both, on until the player turns it off. Kept beside
    /// <see cref="CompletedLevels"/> until Phase 6 builds a settings screen.
    /// </summary>
    /// <remarks>
    /// <para><b>Data.</b> <see cref="ToData"/> writes <c>on</c> or
    /// <c>off</c>.</para>
    /// <para><b>Unreadable data reads as on.</b> Saved data comes back from
    /// outside the game and may be anything; reading it never throws.
    /// <see cref="TryFromData"/> says whether it could be read, so the caller
    /// — this assembly does not log — can report it.</para>
    /// </remarks>
    public sealed class SoundSetting
    {
        /// <summary>The key the setting is saved under in an <c>ISaveStore</c>.</summary>
        public const string SaveKey = "GateRush.SoundOn";

        private const string OnData = "on";
        private const string OffData = "off";

        /// <summary>True when sound and vibration are wanted. Starts true.</summary>
        public bool IsOn { get; set; } = true;

        /// <summary>The setting as text, for <see cref="TryFromData"/> to read back.</summary>
        public string ToData() => IsOn ? OnData : OffData;

        /// <summary>
        /// Reads <paramref name="data"/> into <paramref name="setting"/>,
        /// which is never null. False when the data cannot be read — anything
        /// <see cref="ToData"/> would not have written — and
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
