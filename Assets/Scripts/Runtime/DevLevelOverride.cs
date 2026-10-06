using System;
using System.Collections.Generic;

namespace GateRush.Runtime
{
    /// <summary>
    /// The level the editor tools asked the next Play session to start on
    /// (Module 21): a one-shot name, kept in the editor's own
    /// <c>SessionState</c> so it survives the domain reload entering Play Mode
    /// may cause and is gone when Unity closes. It is never written into the
    /// scene, so testing a level leaves <c>Level.unity</c> unchanged and a
    /// plain Play starts where a build starts.
    /// </summary>
    /// <remarks>
    /// Holds no field of its own: the store is the editor's. Outside the
    /// editor only <see cref="TryTake"/> exists, and it always answers false,
    /// so a build carries no override.
    /// </remarks>
    public static class DevLevelOverride
    {
#if UNITY_EDITOR
        /// <summary>The one <c>SessionState</c> key the override is kept under.</summary>
        private const string SessionKey = "GateRush.DevLevelOverride";

        /// <summary>
        /// Asks the next Play session to start on the level file
        /// <paramref name="levelName"/>, replacing any earlier request.
        /// Editor only.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="levelName"/> is null or empty.</exception>
        public static void Set(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
            {
                throw new ArgumentException("The override needs the name of a level file.", nameof(levelName));
            }

            UnityEditor.SessionState.SetString(SessionKey, levelName);
        }

        /// <summary>Drops a request nobody took. Editor only.</summary>
        public static void Clear()
        {
            UnityEditor.SessionState.EraseString(SessionKey);
        }

        /// <summary>
        /// Whether the taken name <paramref name="requested"/> is one of the
        /// level files that load, <paramref name="loadable"/>. When it is not,
        /// <paramref name="warning"/> names it, so the caller can report the
        /// override and play its own level instead. Editor only.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="requested"/> or <paramref name="loadable"/> is null.</exception>
        public static bool TryResolve(string requested, ICollection<string> loadable, out string warning)
        {
            if (requested == null)
            {
                throw new ArgumentNullException(nameof(requested));
            }

            if (loadable == null)
            {
                throw new ArgumentNullException(nameof(loadable));
            }

            if (loadable.Contains(requested))
            {
                warning = null;
                return true;
            }

            warning = $"The start-level override names '{requested}', which is not a level file that loads; the override is ignored.";
            return false;
        }
#endif

        /// <summary>
        /// The level the editor tools asked for, once: reading it clears it,
        /// so the Play session after this one starts on the scene's level
        /// again. Always false outside the editor.
        /// </summary>
        public static bool TryTake(out string levelName)
        {
#if UNITY_EDITOR
            var stored = UnityEditor.SessionState.GetString(SessionKey, string.Empty);
            UnityEditor.SessionState.EraseString(SessionKey);
            if (stored.Length > 0)
            {
                levelName = stored;
                return true;
            }
#endif
            levelName = null;
            return false;
        }
    }
}
