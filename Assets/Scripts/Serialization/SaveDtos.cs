using System;

namespace GateRush.Serialization
{
    // ---------------------------------------------------------------------------
    // Save schema — one worked example
    //
    // The whole save is one JSON object under one key (D56). formatVersion comes
    // first and is read on its own before anything else is trusted (D17), so a
    // save from a newer build is refused rather than misread. Ids are written
    // ascending; an id written twice reads as one.
    //
    // {
    //     "formatVersion": 2,
    //     "completedLevelIds": [0, 1, 3],
    //     "soundOn": true,
    //     "gold": 640,
    //     "lifetimeGold": 140,
    //     "lives": 3,
    //     "livesRegenStartUtcTicks": 638640000000000000
    // }
    //
    // gold is the spendable balance and lifetimeGold all gold ever earned (D22).
    // lives is the count as last settled and livesRegenStartUtcTicks the UTC
    // moment, in DateTime ticks, the wait for the next life started (D59);
    // it means nothing while lives is at the cap.
    //
    // Version 2 added the four fields after soundOn (Module 30). A version-1
    // save has none of them and reads with the starting gold and full lives,
    // built from the game's rules (D60). In version 2 a negative count or
    // ticks outside DateTime's range make the save unreadable.
    //
    // A later section (the streak and the inventory in Module 31) adds its
    // fields here and raises formatVersion.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The save as <c>JsonUtility</c> writes and reads it. Fields are written
    /// in declaration order, which is why <see cref="formatVersion"/> is first.
    /// Public fields because <c>JsonUtility</c> requires them.
    /// </summary>
    [Serializable]
    public sealed class SaveDataDto
    {
        /// <summary>The schema version the save was written in; positive.</summary>
        public int formatVersion;

        /// <summary>The ids of the completed levels, ascending. Missing reads as none.</summary>
        public int[] completedLevelIds;

        /// <summary>True when sound and vibration are on.</summary>
        public bool soundOn;

        /// <summary>The spendable gold; never negative. Since version 2.</summary>
        public int gold;

        /// <summary>All the gold ever earned; never negative. Since version 2.</summary>
        public int lifetimeGold;

        /// <summary>The count of lives as last settled; never negative. Since version 2.</summary>
        public int lives;

        /// <summary>
        /// When the wait for the next life started, as UTC
        /// <see cref="DateTime.Ticks"/>, within <see cref="DateTime"/>'s
        /// range. Since version 2.
        /// </summary>
        public long livesRegenStartUtcTicks;
    }

    /// <summary>
    /// The one field read before the rest of a save is trusted: a save of a
    /// newer version may have fields of another shape, and must be reported
    /// as too new, not as text that failed to parse.
    /// </summary>
    [Serializable]
    internal sealed class SaveHeaderDto
    {
        /// <summary>The schema version the save was written in; 0 when missing.</summary>
        public int formatVersion;
    }
}
