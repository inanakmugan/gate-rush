using System;
using GateRush.Meta;
using UnityEngine;

namespace GateRush.Serialization
{
    /// <summary>
    /// The <see cref="ISaveCodec"/> of the game: the save as JSON through
    /// <see cref="JsonUtility"/> and the DTOs in <c>SaveDtos.cs</c> (D17, D56).
    /// It only encodes and decodes; what an unreadable save gives is
    /// <see cref="SaveRepository"/>'s rule.
    /// </summary>
    /// <remarks>
    /// <para><b>The version first.</b> <see cref="TryDecode"/> reads
    /// <c>formatVersion</c> alone, through a header DTO, and refuses a missing,
    /// non-positive or too new one before the rest of the text is parsed.</para>
    /// <para><b>Adding a version.</b> Raise <see cref="CurrentFormatVersion"/>,
    /// add the section's fields to <see cref="SaveDataDto"/>, and read the
    /// section in <see cref="FromDto"/> behind the version that introduced it.
    /// <see cref="FromDto"/> is the one place versions are told apart.</para>
    /// <para><b>A limit.</b> <see cref="JsonUtility"/> is lenient: within
    /// readable JSON, a field of the wrong type can read as its default rather
    /// than fail. What it reports as an error is caught and refused.</para>
    /// </remarks>
    public sealed class JsonSaveCodec : ISaveCodec
    {
        /// <summary>
        /// The newest schema version this build reads, and the one it writes.
        /// A save of any version above it is refused, never misread.
        /// </summary>
        public const int CurrentFormatVersion = 1;

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
        public string Encode(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var ids = data.Completed.Ids;
            var completedLevelIds = new int[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                completedLevelIds[i] = ids[i];
            }

            var dto = new SaveDataDto
            {
                formatVersion = CurrentFormatVersion,
                completedLevelIds = completedLevelIds,
                soundOn = data.Sound.IsOn,
            };
            return JsonUtility.ToJson(dto, prettyPrint: true);
        }

        /// <inheritdoc />
        public bool TryDecode(string text, out SaveData data, out string problem)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                problem = "the save is empty.";
                return false;
            }

            if (!TryParse(text, out SaveHeaderDto header, out problem))
            {
                return false;
            }

            var version = header.formatVersion;
            if (version <= 0)
            {
                problem = $"its formatVersion is missing or not positive ({version}).";
                return false;
            }

            if (version > CurrentFormatVersion)
            {
                problem = $"its formatVersion {version} is newer than this build reads ({CurrentFormatVersion}).";
                return false;
            }

            if (!TryParse(text, out SaveDataDto dto, out problem))
            {
                return false;
            }

            data = FromDto(dto, version);
            return true;
        }

        /// <summary>
        /// The save <paramref name="dto"/> holds, read as the
        /// <paramref name="version"/> it was written in — the one place
        /// versions are told apart. Every section of version 1 is read
        /// whatever the version, since no version is older.
        /// </summary>
        private static SaveData FromDto(SaveDataDto dto, int version)
        {
            var completed = CompletedLevels.Of(dto.completedLevelIds ?? Array.Empty<int>());
            var sound = new SoundSetting { IsOn = dto.soundOn };
            return new SaveData(completed, sound);
        }

        private static bool TryParse<T>(string text, out T dto, out string problem) where T : class
        {
            try
            {
                dto = JsonUtility.FromJson<T>(text);
            }
            catch (Exception e)
            {
                dto = null;
                problem = $"the text is not valid JSON ({e.Message}).";
                return false;
            }

            if (dto == null)
            {
                problem = "the text is not a JSON object.";
                return false;
            }

            problem = null;
            return true;
        }
    }
}
