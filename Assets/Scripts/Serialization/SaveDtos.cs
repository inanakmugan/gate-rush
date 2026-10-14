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
    //     "formatVersion": 1,
    //     "completedLevelIds": [0, 1, 3],
    //     "soundOn": true
    // }
    //
    // A later section (the wallet and lives in Module 30, the streak and the
    // inventory in Module 31) adds its fields here and raises formatVersion.
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
