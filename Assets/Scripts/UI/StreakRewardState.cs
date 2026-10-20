namespace GateRush.UI
{
    /// <summary>Where one reward of the win streak's table stands against the current streak (Module 37).</summary>
    public enum StreakRewardState
    {
        /// <summary>Reached in the current streak: its wins are at most the streak.</summary>
        Collected,

        /// <summary>The first reward not reached yet.</summary>
        Next,

        /// <summary>Not reached, and not the next one.</summary>
        Ahead
    }
}
