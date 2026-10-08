using System;
using GateRush.Platform;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The <see cref="ISaveStore"/> of the player: <c>PlayerPrefs</c>, which
    /// works on every target, WebGL included, where it lives in the browser's
    /// IndexedDB. The one place in the project that touches
    /// <c>PlayerPrefs</c>.
    /// </summary>
    /// <remarks>
    /// On itch.io the IndexedDB path changes with every upload, so what is
    /// saved survives reloads of one upload and starts empty after the next
    /// (D51). A store that outlives uploads can replace this one behind the
    /// interface.
    /// </remarks>
    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
        public bool TryLoad(string key, out string value)
        {
            RequireKey(key);
            if (!PlayerPrefs.HasKey(key))
            {
                value = null;
                return false;
            }

            value = PlayerPrefs.GetString(key);
            return true;
        }

        /// <summary>
        /// Saves <paramref name="value"/> under <paramref name="key"/> and
        /// flushes at once: without the flush a WebGL tab closed before the
        /// page unloads cleanly would lose the write.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        public void Save(string key, string value)
        {
            RequireKey(key);
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        private static void RequireKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("A save key may not be null or empty.", nameof(key));
            }
        }
    }
}
