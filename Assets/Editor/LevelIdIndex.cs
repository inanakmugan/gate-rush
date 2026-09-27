using System;
using System.Collections.Generic;
using System.IO;
using GateRush.Serialization;

namespace GateRush.Editor
{
    /// <summary>
    /// The level id another level file declares, and that file's name — the
    /// input <see cref="DraftValidator"/> checks
    /// <see cref="DraftWarningCategory.LevelIdAlreadyUsed"/> against, gathered
    /// by <see cref="LevelIdIndex"/> so the validator itself reads no files.
    /// </summary>
    public readonly struct LevelFileId
    {
        /// <summary>The file's name, shown in the warning so the clash can be found.</summary>
        public string FileName { get; }

        /// <summary>The <c>levelId</c> the file declares.</summary>
        public int LevelId { get; }

        public LevelFileId(string fileName, int levelId)
        {
            FileName = fileName;
            LevelId = levelId;
        }
    }

    /// <summary>
    /// Turns the text of the level files on disk into the ids they declare,
    /// leaving out the file currently open. The window reads the files; this
    /// decides what they say, so the decision is testable without disk access.
    /// </summary>
    public static class LevelIdIndex
    {
        /// <summary>
        /// Parses each file's JSON to the DTO stage and collects its
        /// <c>levelId</c>. A file that does not parse is skipped without
        /// comment: it cannot claim an id, and opening it reports its problem.
        /// </summary>
        /// <param name="files">Each file's path and its text.</param>
        /// <param name="excludePath">
        /// The file the draft was loaded from or last saved to, or <c>null</c>
        /// for an unsaved draft. Compared as a full path, case-insensitively,
        /// because <c>Directory.GetFiles</c> and Unity's save panel spell the
        /// same file with different separators.
        /// </param>
        public static IReadOnlyList<LevelFileId> Build(
            IEnumerable<KeyValuePair<string, string>> files, string excludePath)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            var excluded = excludePath != null ? Path.GetFullPath(excludePath) : null;
            var ids = new List<LevelFileId>();

            foreach (var file in files)
            {
                if (excluded != null
                    && string.Equals(Path.GetFullPath(file.Key), excluded, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                LevelDto dto;
                try
                {
                    dto = LevelSerializer.ParseDto(file.Value, Path.GetFileName(file.Key));
                }
                catch (Exception)
                {
                    continue;
                }

                ids.Add(new LevelFileId(Path.GetFileName(file.Key), dto.levelId));
            }

            return ids;
        }
    }
}
