using System;
using System.IO;

namespace GateRush.Editor
{
    /// <summary>
    /// Whether the file the Level Editor has open is one the game can be asked
    /// to play (Module 21), and under what name. The game finds levels by file
    /// name in its levels folder, so a level never saved, or saved elsewhere,
    /// cannot be played however sound it is.
    /// </summary>
    public static class PlayableLevelFile
    {
        /// <summary>
        /// The level name the game knows the file <paramref name="assetPath"/>
        /// by — its file name without extension — when the file lies in
        /// <paramref name="levelsFolder"/> or a folder beneath it. Otherwise
        /// false, with <paramref name="reason"/> saying why as a sentence for
        /// the designer.
        /// </summary>
        /// <param name="assetPath">
        /// The file the draft was loaded from or last saved to; null or empty
        /// for a draft never saved. Either separator may be used, and case is
        /// ignored, because <c>Directory.GetFiles</c> and Unity's save panel
        /// spell the same file differently.
        /// </param>
        /// <param name="levelsFolder">The folder the game loads levels from, as a project path.</param>
        /// <exception cref="ArgumentException"><paramref name="levelsFolder"/> is null or empty.</exception>
        public static bool TryGetLevelName(string assetPath, string levelsFolder, out string levelName, out string reason)
        {
            if (string.IsNullOrEmpty(levelsFolder))
            {
                throw new ArgumentException("The levels folder must be named.", nameof(levelsFolder));
            }

            levelName = null;
            if (string.IsNullOrEmpty(assetPath))
            {
                reason = "This level has never been saved to a file. Save it first: Play runs the saved file.";
                return false;
            }

            var folder = Normalized(levelsFolder).TrimEnd('/') + "/";
            if (!Normalized(assetPath).StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"{Path.GetFileName(assetPath)} is not in {levelsFolder}, the only folder the game loads levels from. " +
                         "Save it there to play it.";
                return false;
            }

            levelName = Path.GetFileNameWithoutExtension(assetPath);
            reason = null;
            return true;
        }

        private static string Normalized(string path) => path.Replace('\\', '/');
    }
}
