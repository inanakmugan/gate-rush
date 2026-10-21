namespace GateRush.Runtime
{
    /// <summary>
    /// Decides what a joker button shows and whether it can be pressed
    /// (Modules 34, 40 and 43, D65, D66, D74, D76): the plain rules behind the
    /// level's joker bar. The counts, prices, unlock levels and whether a
    /// joker is unlocked are the caller's, from <c>MetaGame</c>,
    /// <c>MetaRules</c> and <see cref="FreePlayJokers"/>; the text on the
    /// badge is formatted by the level's interface.
    /// </summary>
    /// <remarks>
    /// <c>isUsable</c> is the caller's answer to "could this joker be used at
    /// all now": the level is being played, nothing holds it — no
    /// introduction card, confirmation or in-level overlay — and, for the
    /// Clock, it has a countdown. A button that is not usable is never
    /// pressable, whatever is held.
    /// </remarks>
    public static class JokerButtons
    {
        /// <summary>
        /// An unlocked campaign button: the count while some are held; with
        /// none held, the price, pressable whatever the balance. When the
        /// balance covers the price, pressing buys one when it is used (D65);
        /// when it does not, the button carries the shortfall and a press
        /// opens Not Enough Gold (D74). Pulses when <paramref name="isMarked"/>
        /// (D76).
        /// </summary>
        public static JokerButton Campaign(int held, int price, int balance, bool isUsable, bool isMarked)
        {
            if (held > 0)
            {
                return new JokerButton(JokerBadge.Count, held, isUsable, 0, isMarked);
            }

            var shortfall = price > balance ? price - balance : 0;
            return new JokerButton(JokerBadge.Price, price, isUsable, shortfall, isMarked);
        }

        /// <summary>
        /// A campaign button whose joker is still locked (D76): the badge
        /// shows <paramref name="unlockLevel"/>, and it is never pressable,
        /// never short of gold and never marked, so a press can neither use,
        /// select nor buy it nor open Not Enough Gold.
        /// </summary>
        public static JokerButton Locked(int unlockLevel) =>
            new JokerButton(JokerBadge.Locked, unlockLevel, false, 0, false);

        /// <summary>
        /// A free-play button (D66): how many of the level's allowance are
        /// left, pressable while any are; never topped up, so never short of
        /// gold, and never locked or marked.
        /// </summary>
        public static JokerButton FreePlay(int left, bool isUsable) =>
            new JokerButton(JokerBadge.Count, left, isUsable && left > 0, 0, false);
    }
}
