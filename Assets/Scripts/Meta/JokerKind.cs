using System;

namespace GateRush.Meta
{
    /// <summary>
    /// The three jokers (MECHANICS, Jokers): a Clock adds time, a Rocket
    /// clears one block's colour, a Broom clears a colour from every block
    /// showing it. Held in the campaign's inventory (D61) and spent through
    /// <c>MetaGame.TryUseJoker</c> (D65).
    /// </summary>
    public enum JokerKind
    {
        /// <summary>Adds seconds to the countdown.</summary>
        Clock,

        /// <summary>Clears the colour of one block.</summary>
        Rocket,

        /// <summary>Clears one colour from every block showing it.</summary>
        Broom,
    }

    /// <summary>
    /// The range of <see cref="JokerKind"/>, for the types that keep one value
    /// per kind. A value outside it — a cast from an int — is a bug in the
    /// caller and throws.
    /// </summary>
    internal static class JokerKinds
    {
        /// <summary>The first kind, for loops over every kind.</summary>
        public const JokerKind First = JokerKind.Clock;

        /// <summary>The last kind, for loops over every kind.</summary>
        public const JokerKind Last = JokerKind.Broom;

        /// <summary>True when <paramref name="kind"/> is one of the declared kinds.</summary>
        public static bool IsDefined(JokerKind kind) => kind >= First && kind <= Last;

        /// <summary>The exception for a <paramref name="kind"/> that is not one of the declared kinds.</summary>
        public static ArgumentOutOfRangeException Undefined(JokerKind kind, string paramName) =>
            new ArgumentOutOfRangeException(paramName, kind, $"{(int)kind} is not a joker kind.");
    }
}
