using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// What the result panel shows at the end of a level (Module 41): the
    /// outcome, the gold a campaign win paid, whether Next and Restart are
    /// offered, and whether the closing message shows after the last
    /// campaign level. Built by the bootstrap from what it decided; the panel
    /// only draws it.
    /// </summary>
    /// <remarks>
    /// <para>A campaign win offers no Restart: a won campaign level is never
    /// replayed (D58). Free play pays nothing, so its win has no gold. A loss
    /// offers Restart and nothing else; whether a campaign restart has a life
    /// for it is decided when it is pressed (D57). Menu is always
    /// offered.</para>
    /// </remarks>
    public readonly struct LevelResult
    {
        private LevelResult(bool isWin, int? goldEarned, bool hasNext, bool hasRestart, bool hasClosingMessage)
        {
            IsWin = isWin;
            GoldEarned = goldEarned;
            HasNext = hasNext;
            HasRestart = hasRestart;
            HasClosingMessage = hasClosingMessage;
        }

        /// <summary>True for a won level, false for a lost one.</summary>
        public bool IsWin { get; }

        /// <summary>The gold a campaign win paid, shown beside the coin; null for no gold line.</summary>
        public int? GoldEarned { get; }

        /// <summary>True when Next is offered: a level follows the one won.</summary>
        public bool HasNext { get; }

        /// <summary>True when Restart is offered.</summary>
        public bool HasRestart { get; }

        /// <summary>True after the last campaign level: the closing message shows.</summary>
        public bool HasClosingMessage { get; }

        /// <summary>
        /// A campaign win: <paramref name="goldEarned"/> beside the coin — null
        /// when the win could not be reported, which shows no gold line — Next
        /// when <paramref name="hasNext"/>, and otherwise the closing message,
        /// since the campaign has no level left. No Restart (D58).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="goldEarned"/> is negative.</exception>
        public static LevelResult CampaignWin(int? goldEarned, bool hasNext)
        {
            if (goldEarned < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goldEarned), goldEarned, "A win cannot pay negative gold.");
            }

            return new LevelResult(true, goldEarned, hasNext, false, !hasNext);
        }

        /// <summary>A free-play win: Next when <paramref name="hasNext"/>, Restart, no gold and no closing message (D58).</summary>
        public static LevelResult FreePlayWin(bool hasNext) => new LevelResult(true, null, hasNext, true, false);

        /// <summary>A loss: Restart, and no Next, gold or closing message.</summary>
        public static LevelResult Loss() => new LevelResult(false, null, false, true, false);
    }
}
