namespace GateRush.Runtime
{
    /// <summary>
    /// A moment the game vibrates at, where the platform can (Module 25). Each
    /// has its pulse length in <see cref="SoundConfig"/>.
    /// </summary>
    public enum HapticMoment
    {
        /// <summary>A block is destroyed through its gate: a short pulse.</summary>
        Exit,

        /// <summary>The win panel opens: a longer pulse.</summary>
        Win,

        /// <summary>The loss panel opens: a longer pulse.</summary>
        Loss
    }
}
