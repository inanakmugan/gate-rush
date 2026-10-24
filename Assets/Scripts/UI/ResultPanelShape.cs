using GateRush.Runtime;

namespace GateRush.UI
{
    /// <summary>
    /// The result panel's size for what it shows (the UI polish pass): each
    /// shape has its own size, written by the UI builder, so the space between
    /// the title, the middle and the buttons stays the same whatever shows.
    /// </summary>
    public enum ResultPanelShape
    {
        /// <summary>A win with the gold row or the closing message in the middle: the campaign's panel.</summary>
        Reward,

        /// <summary>A win with nothing in the middle: the title over the buttons, wide enough for all three.</summary>
        Compact,

        /// <summary>A loss that cost nothing: the hourglass in the middle.</summary>
        Loss,

        /// <summary>A loss that cost a life: the hourglass and, under it, the heart with "-1".</summary>
        LifeLost
    }

    /// <summary>
    /// Which <see cref="ResultPanelShape"/> a <see cref="LevelResult"/> takes:
    /// read from what the panel shows, not from the mode, so a campaign win
    /// whose gold could not be reported, with nothing in its middle, takes
    /// the compact panel too.
    /// </summary>
    public static class ResultPanelShapes
    {
        /// <summary>The shape of the panel showing <paramref name="result"/>.</summary>
        public static ResultPanelShape ShapeOf(LevelResult result)
        {
            if (result.IsWin)
            {
                return result.GoldEarned.HasValue || result.HasClosingMessage ? ResultPanelShape.Reward : ResultPanelShape.Compact;
            }

            return result.LostLife ? ResultPanelShape.LifeLost : ResultPanelShape.Loss;
        }
    }
}
