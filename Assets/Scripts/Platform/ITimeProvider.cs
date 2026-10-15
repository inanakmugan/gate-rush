using System;

namespace GateRush.Platform
{
    /// <summary>
    /// The current time (Module 30, D19). The one seam between the rules that
    /// depend on real-world time — lives coming back (D20, D59) — and the
    /// clock that answers: the system clock in the game, a clock a test moves
    /// by hand. Nothing above this interface knows which.
    /// </summary>
    /// <remarks>
    /// Only the current UTC time: the frame delta stays with Unity in
    /// <c>Runtime</c>. The caller asks once and passes the answer on, so
    /// <c>GateRush.Meta</c> never reads the system clock itself.
    /// </remarks>
    public interface ITimeProvider
    {
        /// <summary>The current time, always of <see cref="DateTimeKind.Utc"/>.</summary>
        DateTime UtcNow { get; }
    }
}
