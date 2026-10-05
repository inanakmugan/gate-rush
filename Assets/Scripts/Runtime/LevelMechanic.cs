namespace GateRush.Runtime
{
    /// <summary>
    /// What a level can introduce to the player (Module 19), in the order
    /// their cards show. <see cref="HowToPlay"/> is not content a level holds:
    /// it belongs to the first level, whatever that level contains.
    /// </summary>
    public enum LevelMechanic
    {
        /// <summary>The base mechanic (M1): drag a block out through a gate of its colour.</summary>
        HowToPlay,

        /// <summary>A count-gated block (M3).</summary>
        IceBlock,

        /// <summary>A count-gated gate (M2).</summary>
        IceDoor,

        /// <summary>A block with two or more colours (M4).</summary>
        LayeredBlock,

        /// <summary>An axis-restricted block (M7).</summary>
        OneWayBlock,

        /// <summary>A locked block and its keys (M8); one card covers both.</summary>
        LockAndKey,

        /// <summary>A shutter (M5).</summary>
        Shutter,

        /// <summary>A generator (M6).</summary>
        Generator,

        /// <summary>An elevator (M9).</summary>
        Elevator,

        /// <summary>A block with a time bonus (M10).</summary>
        TimeBonus
    }
}
