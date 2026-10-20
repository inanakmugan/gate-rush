using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// The meta game's one shared reading of a wait (Module 32, D64). Every
    /// text of the front end, the level's interface and its panels is the
    /// <c>GateRush.UI</c> assembly's (Modules 35, 40 and 41); nothing here
    /// decides a rule.
    /// </summary>
    public static class MetaText
    {
        /// <summary>
        /// The whole seconds a wait reads as, rounded up as the countdown is
        /// (<see cref="TimeFormat.WholeSeconds"/>): what a refresh compares, so
        /// the text is rebuilt exactly when it would change.
        /// </summary>
        public static int WholeSeconds(TimeSpan wait) => TimeFormat.WholeSeconds((float)wait.TotalSeconds);
    }
}
