namespace GateRush.UI
{
    /// <summary>The pages that open over a tab, newest on top (Modules 35 to 38).</summary>
    public enum UiOverlay
    {
        /// <summary>The sound and haptics toggles and the Levels button.</summary>
        Settings,

        /// <summary>The level grid: free play (D58).</summary>
        Levels,

        /// <summary>The lives, the time to the next and the refill (D62).</summary>
        Lives,

        /// <summary>The shortfall of a purchase refused for want of gold, and Get Gold (D72).</summary>
        NotEnoughGold,

        /// <summary>A title, a line and an OK button: a purchase that failed.</summary>
        Message,

        /// <summary>The simulated store's confirmation of a gold pack, Buy or Cancel (D72).</summary>
        TestPurchase,

        /// <summary>The win streak's ladder: every reward, the current and best streaks, and the rule (D61).</summary>
        WinStreak,

        /// <summary>The streak rewards campaign wins gave since the last announcement, and Collect.</summary>
        StreakReward,

        /// <summary>The player's name and avatar, the avatar grid and four statistics (D71).</summary>
        Profile
    }
}
