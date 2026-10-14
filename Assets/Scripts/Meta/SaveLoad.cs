using System;
using System.Collections.Generic;

namespace GateRush.Meta
{
    /// <summary>Where the save <see cref="SaveRepository.Load"/> gave came from.</summary>
    public enum SaveLoadOutcome
    {
        /// <summary>Nothing was saved, under the new key or the old ones: the defaults.</summary>
        Fresh,

        /// <summary>Read from the save under <see cref="SaveRepository.SaveKey"/>.</summary>
        Loaded,

        /// <summary>
        /// No save existed under the new key, so it was read from the old keys
        /// of Modules 23 and 25.
        /// </summary>
        CarriedOver,

        /// <summary>
        /// The save under the new key could not be read: the defaults, with a
        /// problem saying why. The text is left in the store.
        /// </summary>
        Unreadable,
    }

    /// <summary>
    /// What <see cref="SaveRepository.Load"/> gave: the save, where it came
    /// from, and what went wrong on the way. This assembly does not log;
    /// <see cref="Problems"/> are for the caller to report.
    /// </summary>
    public sealed class SaveLoad
    {
        internal SaveLoad(SaveData data, SaveLoadOutcome outcome, IReadOnlyList<string> problems)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Outcome = outcome;
            Problems = problems ?? throw new ArgumentNullException(nameof(problems));
        }

        /// <summary>The save to play with. Never null: what could not be read is at its default.</summary>
        public SaveData Data { get; }

        /// <summary>Where <see cref="Data"/> came from.</summary>
        public SaveLoadOutcome Outcome { get; }

        /// <summary>
        /// One sentence per thing that could not be read or written, each
        /// naming its key and what was used instead. Empty, never null, when
        /// nothing went wrong.
        /// </summary>
        public IReadOnlyList<string> Problems { get; }
    }
}
