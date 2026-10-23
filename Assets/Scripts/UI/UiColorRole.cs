namespace GateRush.UI
{
    /// <summary>
    /// Which <see cref="UiConfig"/> colour a <see cref="UiTint"/> gives its
    /// graphic: the theme's colours set from code (D69). Colours that never
    /// change and are not part of the theme stay on the prefabs.
    /// </summary>
    /// <remarks>
    /// The values are saved in prefabs, so each keeps its number. The coin,
    /// heart and joker badge roles (3, 4 and 7) went with Module 45: those
    /// sprites are drawn in their own colours and are not tinted. A tint
    /// still holding one is reported by <see cref="UiTint.AddMissing"/>. The
    /// scrim role takes 8, so no old badge tint can be read as a scrim.
    /// </remarks>
    public enum UiColorRole
    {
        /// <summary>The flat colour behind every tab (<see cref="UiConfig.BackgroundColor"/>).</summary>
        Background = 0,

        /// <summary>A white icon on a button or a selected tab (<see cref="UiConfig.IconColor"/>).</summary>
        Icon = 1,

        /// <summary>A tab's icon while another tab is selected (<see cref="UiConfig.IdleTabIconColor"/>).</summary>
        IdleTabIcon = 2,

        /// <summary>The highlight behind the next reward's marker on the streak bar (<see cref="UiConfig.StreakColor"/>).</summary>
        Streak = 5,

        /// <summary>The ring around the selected joker's button in a level (<see cref="UiConfig.JokerSelectedColor"/>).</summary>
        JokerSelected = 6,

        /// <summary>The dim behind every overlay's panel (<see cref="UiConfig.ScrimColor"/>).</summary>
        Scrim = 8
    }
}
