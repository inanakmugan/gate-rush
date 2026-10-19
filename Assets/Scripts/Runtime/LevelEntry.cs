namespace GateRush.Runtime
{
    /// <summary>One level as the front end's level grid shows it (Module 35, D58).</summary>
    /// <remarks>
    /// There is no "locked" here, on purpose: every level of free play is
    /// playable whatever is completed (D50, D58).
    /// </remarks>
    public readonly struct LevelEntry
    {
        /// <summary>A level as the grid shows it.</summary>
        public LevelEntry(string name, int number, bool isCompleted)
        {
            Name = name;
            Number = number;
            IsCompleted = isCompleted;
        }

        /// <summary>The level's file name, which starts it.</summary>
        public string Name { get; }

        /// <summary>The number the player sees: the level's 1-based position in the level order.</summary>
        public int Number { get; }

        /// <summary>True when the level has been won, in either mode; it carries a mark.</summary>
        public bool IsCompleted { get; }
    }
}
