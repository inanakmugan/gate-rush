using System;
using System.Globalization;

namespace GateRush.Runtime
{
    /// <summary>
    /// The one check every configuration asset runs on its format strings
    /// (<see cref="RuntimeConfig"/>, Module 35's <c>UiConfig</c>): a format
    /// is usable when formatting it with the numbers its caller passes can
    /// never throw.
    /// </summary>
    public static class FormatStrings
    {
        // Any number will do: only whether formatting throws is asked.
        private const int SampleArgument = 1;

        /// <summary>
        /// True when <paramref name="format"/> formats with
        /// <paramref name="argumentCount"/> numbers: every placeholder it has
        /// is one of {0} to {argumentCount - 1}, and its braces are balanced.
        /// False for null. Whether the text is blank is the caller's own check.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="argumentCount"/> is negative.</exception>
        public static bool IsUsable(string format, int argumentCount)
        {
            if (argumentCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(argumentCount), argumentCount, "A format takes no fewer than zero arguments.");
            }

            if (format == null)
            {
                return false;
            }

            var arguments = new object[argumentCount];
            for (var i = 0; i < argumentCount; i++)
            {
                arguments[i] = SampleArgument;
            }

            try
            {
                string.Format(CultureInfo.InvariantCulture, format, arguments);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
