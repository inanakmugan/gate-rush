using System;
using System.Globalization;
using GateRush.Meta;

namespace GateRush.Runtime
{
    /// <summary>
    /// Decides what a joker button shows and whether it can be pressed
    /// (Module 34, D65, D66): the plain rules behind <see cref="JokerBar"/>.
    /// Every text comes from the config's names and format strings; the
    /// counts and prices are the caller's, from <c>MetaGame</c>,
    /// <c>MetaRules</c> and <see cref="FreePlayJokers"/>.
    /// </summary>
    /// <remarks>
    /// <c>isUsable</c> is the caller's answer to "could this joker be used at
    /// all now": the level is being played, no introduction card or
    /// confirmation holds it, and, for the Clock, it has a countdown. A button
    /// that is not usable is never pressable, whatever is held.
    /// </remarks>
    public static class JokerButtons
    {
        /// <summary>
        /// A campaign button: the name and the count while some are held;
        /// with none held, the name and the price, pressable only when
        /// <paramref name="balance"/> covers it — pressing then buys one and
        /// uses it at once (D65).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="texts"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public static JokerButton Campaign(JokerKind kind, int held, int price, int balance, bool isUsable, RuntimeConfig texts)
        {
            RequireTexts(texts);
            var name = texts.JokerName(kind);
            if (held > 0)
            {
                return new JokerButton(Format(texts.JokerCountFormat, name, held), isUsable);
            }

            return new JokerButton(Format(texts.JokerPriceFormat, name, price), isUsable && balance >= price);
        }

        /// <summary>
        /// A free-play button (D66): the name and how many of the level's
        /// allowance are left, pressable while any are; never topped up.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="texts"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public static JokerButton FreePlay(JokerKind kind, int left, bool isUsable, RuntimeConfig texts)
        {
            RequireTexts(texts);
            return new JokerButton(Format(texts.JokerCountFormat, texts.JokerName(kind), left), isUsable && left > 0);
        }

        private static string Format(string format, string name, int number) =>
            string.Format(CultureInfo.InvariantCulture, format, name, number);

        private static void RequireTexts(RuntimeConfig texts)
        {
            if (texts == null)
            {
                throw new ArgumentNullException(nameof(texts));
            }
        }
    }
}
