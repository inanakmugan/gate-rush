using System;
using System.Collections.Generic;
using GateRush.Platform;

namespace GateRush.Tests
{
    /// <summary>
    /// An <see cref="ISaveStore"/> in a dictionary, for tests. It records the
    /// key of every <see cref="Save"/>, so a test can tell what a call wrote,
    /// and can be told to fail its saves, as a full <c>PlayerPrefs</c> would.
    /// <see cref="Seed"/> puts in what an earlier build left behind without
    /// counting as a save.
    /// </summary>
    public sealed class MemorySaveStore : ISaveStore
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();
        private readonly List<string> savedKeys = new List<string>();

        /// <summary>The key of every <see cref="Save"/> made, failed ones included, in order.</summary>
        public IReadOnlyList<string> SavedKeys => savedKeys;

        /// <summary>When true, <see cref="Save"/> throws and stores nothing.</summary>
        public bool FailsSaves { get; set; }

        /// <summary>Stores <paramref name="value"/> under <paramref name="key"/> without recording a save.</summary>
        public void Seed(string key, string value) => values[key] = value;

        /// <inheritdoc />
        public bool TryLoad(string key, out string value) => values.TryGetValue(key, out value);

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException"><see cref="FailsSaves"/> is true.</exception>
        public void Save(string key, string value)
        {
            savedKeys.Add(key);
            if (FailsSaves)
            {
                throw new InvalidOperationException("The test store fails its saves.");
            }

            values[key] = value;
        }
    }
}
