using System;
using System.Collections.Generic;
using System.IO;
using GateRush.Serialization;

namespace GateRush.Editor
{
    /// <summary>
    /// What another level file declares, and that file's name — the input
    /// <see cref="DraftValidator"/> checks
    /// <see cref="DraftWarningCategory.LevelIdAlreadyUsed"/> and
    /// <see cref="DraftWarningCategory.BoardIdenticalToOtherLevel"/> against,
    /// gathered by <see cref="LevelIdIndex"/> so the validator itself reads no
    /// files.
    /// </summary>
    public readonly struct LevelFileEntry
    {
        /// <summary>The file's name, shown in the warning so the clash can be found.</summary>
        public string FileName { get; }

        /// <summary>The <c>levelId</c> the file declares.</summary>
        public int LevelId { get; }

        /// <summary>
        /// The file's <see cref="LevelIdIndex.BoardSignature"/>: its board as
        /// JSON with the level's metadata zeroed, so two files match when their
        /// boards do, whatever their ids, rewards and budgets.
        /// </summary>
        public string BoardSignature { get; }

        public LevelFileEntry(string fileName, int levelId, string boardSignature)
        {
            FileName = fileName;
            LevelId = levelId;
            BoardSignature = boardSignature;
        }
    }

    /// <summary>
    /// Turns the text of the level files on disk into what they declare — their
    /// ids and board signatures — leaving out the file currently open. The
    /// window reads the files; this decides what they say, so the decision is
    /// testable without disk access.
    /// </summary>
    public static class LevelIdIndex
    {
        /// <summary>
        /// Parses each file's JSON to the DTO stage and collects its
        /// <c>levelId</c> and board signature. A file that does not parse is
        /// skipped without comment: it cannot claim an id, and opening it
        /// reports its problem.
        /// </summary>
        /// <remarks>
        /// The signature is taken from the file's DTO after a round trip
        /// through <see cref="LevelDraft"/>, the same path the open draft's
        /// signature takes. A raw parsed DTO can differ from what a draft writes
        /// back — a defaulted unreadable name, an absent array — so comparing it
        /// directly could miss the very file the draft was copied from.
        /// </remarks>
        /// <param name="files">Each file's path and its text.</param>
        /// <param name="excludePath">
        /// The file the draft was loaded from or last saved to, or <c>null</c>
        /// for an unsaved draft. Empty or whitespace also means "exclude
        /// nothing": it names no file, and <c>Path.GetFullPath</c> would throw
        /// on it. Otherwise compared as a full path, case-insensitively,
        /// because <c>Directory.GetFiles</c> and Unity's save panel spell the
        /// same file with different separators.
        /// </param>
        public static IReadOnlyList<LevelFileEntry> Build(
            IEnumerable<KeyValuePair<string, string>> files, string excludePath)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            var excluded = string.IsNullOrWhiteSpace(excludePath) ? null : Path.GetFullPath(excludePath);
            var entries = new List<LevelFileEntry>();

            foreach (var file in files)
            {
                if (excluded != null
                    && string.Equals(Path.GetFullPath(file.Key), excluded, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = Path.GetFileName(file.Key);
                LevelDto dto;
                string signature;
                try
                {
                    dto = LevelSerializer.ParseDto(file.Value, fileName);
                    signature = BoardSignature(LevelDraft.FromDto(dto).ToDto());
                }
                catch (Exception)
                {
                    continue;
                }

                entries.Add(new LevelFileEntry(fileName, dto.levelId, signature));
            }

            return entries;
        }

        /// <summary>
        /// The level's JSON with <c>levelId</c>, <c>goldReward</c> and
        /// <c>suggestedTimeBudgetSeconds</c> set to zero: equal for two levels
        /// exactly when their boards are, whatever metadata each carries. Both
        /// the other files and the open draft pass through this one function,
        /// each from a <see cref="LevelDraft.ToDto"/> result, so the two sides
        /// cannot be computed differently.
        /// </summary>
        /// <remarks>
        /// The three fields are zeroed on <paramref name="dto"/> itself and
        /// restored in a <c>finally</c>, rather than on a copy: a field-by-field
        /// copy would have to be extended by hand whenever <see cref="LevelDto"/>
        /// gains a field. This is safe only because every caller passes a DTO it
        /// has just created and nothing else holds, on the editor's main thread;
        /// a DTO shared with other code or another thread would briefly show
        /// zeroed metadata.
        /// </remarks>
        public static string BoardSignature(LevelDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            var levelId = dto.levelId;
            var goldReward = dto.goldReward;
            var timeBudget = dto.suggestedTimeBudgetSeconds;
            try
            {
                dto.levelId = 0;
                dto.goldReward = 0;
                dto.suggestedTimeBudgetSeconds = 0;
                return LevelSerializer.ToJson(dto);
            }
            finally
            {
                dto.levelId = levelId;
                dto.goldReward = goldReward;
                dto.suggestedTimeBudgetSeconds = timeBudget;
            }
        }
    }
}
