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
    /// section in <see cref="TryFromDto"/> behind the version that introduced
    /// it. <see cref="TryFromDto"/> is the one place versions are told
    /// apart.</para>
    /// <para><b>Older versions (D60).</b> A section the text's version did not
    /// have takes a new player's value, built from the rules it is given
    /// through the section's own factory — <see cref="Wallet.Starting"/>,
    /// <see cref="Lives.Full"/>, <see cref="JokerInventory.Starting"/>,
    /// <see cref="WinStreak.Starting"/>, <see cref="CampaignProgress.Starting"/>
    /// — never from numbers of this codec's. Values
    /// this codec would never write, such as negative gold, make the save
    /// unreadable; they are not corrected.</para>
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
        public const int CurrentFormatVersion = 3;

        /// <summary>The version that added the wallet and the lives (Module 30).</summary>
        private const int WalletAndLivesVersion = 2;

        /// <summary>The version that added the win streak, the campaign position and the jokers (Module 31).</summary>
        private const int MetaGameVersion = 3;

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
                gold = data.Wallet.Balance,
                lifetimeGold = data.Wallet.LifetimeEarned,
                lives = data.Lives.StoredCount,
                livesRegenStartUtcTicks = data.Lives.RegenStartUtc.Ticks,
                winStreak = data.Streak.Current,
                bestWinStreak = data.Streak.Best,
                campaignPosition = data.Campaign.Position,
                clocks = data.Jokers.CountOf(JokerKind.Clock),
                rockets = data.Jokers.CountOf(JokerKind.Rocket),
                brooms = data.Jokers.CountOf(JokerKind.Broom),
            };
            return JsonUtility.ToJson(dto, prettyPrint: true);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public bool TryDecode(string text, MetaRules rules, out SaveData data, out string problem)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

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

            return TryFromDto(dto, version, rules, out data, out problem);
        }

        /// <summary>
        /// Reads the save <paramref name="dto"/> holds as the
        /// <paramref name="version"/> it was written in — the one place
        /// versions are told apart. Every section of version 1 is read
        /// whatever the version, since no version is older. A section added
        /// by a version above <paramref name="version"/> is a new player's,
        /// by <paramref name="rules"/> (D60); one the version has is read, and
        /// a value this codec would never write gives false with
        /// <paramref name="problem"/> naming the field.
        /// </summary>
        private static bool TryFromDto(SaveDataDto dto, int version, MetaRules rules, out SaveData data, out string problem)
        {
            data = null;
            var completed = CompletedLevels.Of(dto.completedLevelIds ?? Array.Empty<int>());
            var sound = new SoundSetting { IsOn = dto.soundOn };

            Wallet wallet;
            Lives lives;
            if (version < WalletAndLivesVersion)
            {
                wallet = Wallet.Starting(rules);
                lives = Lives.Full(rules);
            }
            else if (!TryReadWalletAndLives(dto, rules, out wallet, out lives, out problem))
            {
                return false;
            }

            JokerInventory jokers;
            WinStreak streak;
            CampaignProgress campaign;
            if (version < MetaGameVersion)
            {
                jokers = JokerInventory.Starting(rules);
                streak = WinStreak.Starting();
                campaign = CampaignProgress.Starting();
            }
            else if (!TryReadMetaGame(dto, out jokers, out streak, out campaign, out problem))
            {
                return false;
            }

            data = new SaveData(completed, sound, wallet, lives, jokers, streak, campaign);
            problem = null;
            return true;
        }

        /// <summary>
        /// Reads the wallet and the lives. False, with
        /// <paramref name="problem"/> naming the field, for a negative amount
        /// or count, or ticks outside <see cref="DateTime"/>'s range.
        /// </summary>
        private static bool TryReadWalletAndLives(SaveDataDto dto, MetaRules rules, out Wallet wallet, out Lives lives, out string problem)
        {
            wallet = null;
            lives = null;
            if (IsNegative(dto.gold, nameof(dto.gold), out problem)
                || IsNegative(dto.lifetimeGold, nameof(dto.lifetimeGold), out problem)
                || IsNegative(dto.lives, nameof(dto.lives), out problem))
            {
                return false;
            }

            var ticks = dto.livesRegenStartUtcTicks;
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            {
                problem = $"its {nameof(dto.livesRegenStartUtcTicks)} {ticks} is outside the range of a date.";
                return false;
            }

            // Every value is checked above, so neither constructor can throw.
            wallet = new Wallet(dto.gold, dto.lifetimeGold);
            lives = new Lives(rules, dto.lives, new DateTime(ticks, DateTimeKind.Utc));
            return true;
        }

        /// <summary>
        /// Reads the jokers, the win streak and the campaign position. False,
        /// with <paramref name="problem"/> naming the field, for a negative
        /// value or a best streak below the streak.
        /// </summary>
        private static bool TryReadMetaGame(
            SaveDataDto dto, out JokerInventory jokers, out WinStreak streak, out CampaignProgress campaign, out string problem)
        {
            jokers = null;
            streak = null;
            campaign = null;
            if (IsNegative(dto.winStreak, nameof(dto.winStreak), out problem)
                || IsNegative(dto.bestWinStreak, nameof(dto.bestWinStreak), out problem)
                || IsNegative(dto.campaignPosition, nameof(dto.campaignPosition), out problem)
                || IsNegative(dto.clocks, nameof(dto.clocks), out problem)
                || IsNegative(dto.rockets, nameof(dto.rockets), out problem)
                || IsNegative(dto.brooms, nameof(dto.brooms), out problem))
            {
                return false;
            }

            if (dto.bestWinStreak < dto.winStreak)
            {
                problem = $"its {nameof(dto.bestWinStreak)} {dto.bestWinStreak} is below its {nameof(dto.winStreak)} {dto.winStreak}.";
                return false;
            }

            // Every value is checked above, so no constructor can throw.
            jokers = new JokerInventory(dto.clocks, dto.rockets, dto.brooms);
            streak = new WinStreak(dto.winStreak, dto.bestWinStreak);
            campaign = new CampaignProgress(dto.campaignPosition);
            return true;
        }

        private static bool IsNegative(int value, string field, out string problem)
        {
            problem = value < 0 ? $"its {field} {value} is negative." : null;
            return value < 0;
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
