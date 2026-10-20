using System;

namespace GateRush.UI
{
    /// <summary>
    /// Where the Leaderboard's list scrolls to show the player's row
    /// (Module 39): the row's centre at the viewport's centre when it can be,
    /// held within the list's ends. Plain arithmetic, so it is tested without
    /// a layout pass.
    /// </summary>
    public static class LeaderboardScroll
    {
        /// <summary>
        /// The content's offset from the top, 0 showing its top: the row whose
        /// top lies <paramref name="rowTop"/> below the content's top and is
        /// <paramref name="rowHeight"/> tall, centred in a viewport
        /// <paramref name="viewportHeight"/> tall, and clamped from 0 to the
        /// furthest the content scrolls. Content no taller than the viewport
        /// does not scroll: 0.
        /// </summary>
        public static float CenteredOffset(float rowTop, float rowHeight, float contentHeight, float viewportHeight)
        {
            var furthest = Math.Max(0f, contentHeight - viewportHeight);
            var centred = rowTop + rowHeight / 2f - viewportHeight / 2f;
            return Math.Min(Math.Max(centred, 0f), furthest);
        }
    }
}
