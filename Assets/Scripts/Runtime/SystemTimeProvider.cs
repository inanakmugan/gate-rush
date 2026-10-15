using System;
using GateRush.Platform;

namespace GateRush.Runtime
{
    /// <summary>
    /// The <see cref="ITimeProvider"/> of the game: the device's clock, in UTC
    /// (Module 30, D19). Nothing in the shipped game can set the time; tests
    /// use a clock of their own.
    /// </summary>
    public sealed class SystemTimeProvider : ITimeProvider
    {
        /// <inheritdoc />
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
