namespace GateRush.UI
{
    /// <summary>
    /// Which <see cref="UiConfig"/> colour a <see cref="UiTint"/> gives its
    /// graphic: the theme's colours set from code (D69). Colours that never
    /// change and are not part of the theme stay on the prefabs.
    /// </summary>
    public enum UiColorRole
    {
        /// <summary>The flat colour behind every tab (<see cref="UiConfig.BackgroundColor"/>).</summary>
        Background,

        /// <summary>A white icon on a button or a selected tab (<see cref="UiConfig.IconColor"/>).</summary>
        Icon,

        /// <summary>A tab's icon while another tab is selected (<see cref="UiConfig.IdleTabIconColor"/>).</summary>
        IdleTabIcon,

        /// <summary>The gold's coin (<see cref="UiConfig.CoinColor"/>).</summary>
        Coin,

        /// <summary>The lives' heart (<see cref="UiConfig.HeartColor"/>).</summary>
        Heart,

        /// <summary>The win streak's fire, the bar's fill and the next reward's highlight (<see cref="UiConfig.StreakColor"/>).</summary>
        Streak,

        /// <summary>The ring around the selected joker's button in a level (<see cref="UiConfig.JokerSelectedColor"/>).</summary>
        JokerSelected
    }
}
