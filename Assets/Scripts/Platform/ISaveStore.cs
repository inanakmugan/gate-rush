namespace GateRush.Platform
{
    /// <summary>
    /// Persists string blobs by key. The one seam between what the game
    /// remembers and where a platform keeps it: <c>PlayerPrefs</c> in the
    /// player, an in-memory dictionary in tests. Nothing above this interface
    /// knows which.
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>
        /// Reads what was saved under <paramref name="key"/>. False, with a
        /// null <paramref name="value"/>, when nothing was ever saved there.
        /// </summary>
        bool TryLoad(string key, out string value);

        /// <summary>
        /// Saves <paramref name="value"/> under <paramref name="key"/>,
        /// replacing what was there, durably: once this returns, a closed tab
        /// or a killed app keeps it.
        /// </summary>
        void Save(string key, string value);
    }
}
