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
    //     "formatVersion": 3,
    //     "completedLevelIds": [0, 1, 3],
    //     "soundOn": true,
    //     "gold": 640,
    //     "lifetimeGold": 140,
    //     "lives": 3,
    //     "livesRegenStartUtcTicks": 638640000000000000,
    //     "winStreak": 2,
    //     "bestWinStreak": 6,
    //     "campaignPosition": 4,
    //     "clocks": 1,
    //     "rockets": 0,
    //     "brooms": 2
    // }
    //
    // gold is the spendable balance and lifetimeGold all gold ever earned (D22).
    // lives is the count as last settled and livesRegenStartUtcTicks the UTC
    // moment, in DateTime ticks, the wait for the next life started (D59);
    // it means nothing while lives is at the cap. winStreak counts campaign
    // wins in a row and bestWinStreak the highest it reached (D61);
    // campaignPosition is the index of the campaign level in the level order
    // (D63); clocks, rockets and brooms are the jokers held.
    //
    // Version 2 added the four fields from gold to livesRegenStartUtcTicks
    // (Module 30), version 3 the six after them (Module 31). A save of an
    // older version has none of a later version's fields and reads with a new
    // player's values for them, built from the game's rules (D60): the
    // starting gold, full lives, no streak, the campaign at its start and the
    // starting jokers. From version 2 a negative count or ticks outside
    // DateTime's range, and from version 3 a negative new field or a
    // bestWinStreak below winStreak, make the save unreadable.
    //
    // A later section adds its fields here and raises formatVersion.
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

        /// <summary>Campaign wins in a row; never negative. Since version 3.</summary>
        public int winStreak;

        /// <summary>The highest <see cref="winStreak"/> reached; never below it. Since version 3.</summary>
        public int bestWinStreak;

        /// <summary>The index of the campaign level in the level order; never negative. Since version 3.</summary>
        public int campaignPosition;

        /// <summary>Clock jokers held; never negative. Since version 3.</summary>
        public int clocks;

        /// <summary>Rocket jokers held; never negative. Since version 3.</summary>
        public int rockets;

        /// <summary>Broom jokers held; never negative. Since version 3.</summary>
        public int brooms;
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
