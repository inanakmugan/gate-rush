using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// The single predicate every count-based unlock in the game is expressed
    /// through: a count-gated gate opening (M2), a count-gated block unfreezing
    /// (M3), and a shutter opening on either the total clear count or one
    /// colour's count (M5). All three ask the same question — <em>has this
    /// counter reached this value?</em> — and differ only in which counter, so
    /// they share one implementation rather than three inline comparisons that
    /// would drift (see <c>DECISIONS.md</c> D28, D31 for the same reasoning
    /// applied elsewhere).
    /// </summary>
    /// <remarks>
    /// This takes raw counters rather than a <see cref="BoardState"/> because its
    /// only caller, <c>MoveResolver.ReevaluateConditions</c>, runs <em>inside</em>
    /// the fixpoint loop where the counters live in the resolver's successor
    /// builder and no <see cref="BoardState"/> exists yet. A method on
    /// <see cref="BoardState"/> would be unusable at the one place it is needed.
    /// </remarks>
    public static class UnlockConditions
    {
        /// <summary>
        /// True when the counter this unlock watches has reached
        /// <paramref name="threshold"/>. When <paramref name="requiredColor"/> is
        /// null the counter is <paramref name="totalClearCount"/>; otherwise it is
        /// <paramref name="clearCountByColor"/> indexed by that colour.
        /// <para>
        /// A pure <c>counter &gt;= threshold</c> test: a threshold of zero or
        /// below is always met. Callers that must not act on an already-open
        /// unlock guard that separately — this predicate does not track state.
        /// </para>
        /// </summary>
        public static bool IsThresholdMet(
            int totalClearCount,
            IReadOnlyList<int> clearCountByColor,
            int threshold,
            BlockColor? requiredColor)
        {
            var counter = requiredColor.HasValue
                ? clearCountByColor[(int)requiredColor.Value]
                : totalClearCount;

            return counter >= threshold;
        }
    }
}
