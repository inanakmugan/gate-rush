using System;
using System.Text;

namespace GateRush.Meta
{
    /// <summary>
    /// The player's name and avatar (Module 38, D71): a section of
    /// <see cref="SaveData"/>. The name follows the name rule of
    /// <see cref="TryNormalizeName"/>; the avatar is kept by its id, never by
    /// its position, and which ids exist is the interface's question.
    /// </summary>
    /// <remarks>
    /// The rule's limits are fixed by the spec, not tuned, so they are
    /// constants here; only the defaults a new player gets are
    /// <see cref="MetaRules"/>'.
    /// </remarks>
    public sealed class PlayerProfile
    {
        /// <summary>The fewest characters a name may have, after trimming and collapsing.</summary>
        public const int MinNameLength = 1;

        /// <summary>The most characters a name may have, after trimming and collapsing.</summary>
        public const int MaxNameLength = 16;

        private const char Space = ' ';
        private const char Hyphen = '-';
        private const char Underscore = '_';
        private const char Period = '.';

        /// <summary>
        /// A profile named <paramref name="name"/>, kept in its normalized
        /// form, with the avatar <paramref name="avatarId"/>, as a save
        /// restores it.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is refused by the name rule, or
        /// <paramref name="avatarId"/> is null, empty or white space.
        /// </exception>
        public PlayerProfile(string name, string avatarId)
        {
            if (!TryNormalizeName(name, out var normalized))
            {
                throw new ArgumentException($"The name '{name}' is refused by the name rule.", nameof(name));
            }

            RequireAvatarId(avatarId);
            Name = normalized;
            AvatarId = avatarId;
        }

        /// <summary>The player's name; always one the name rule accepts, in its normalized form.</summary>
        public string Name { get; private set; }

        /// <summary>The id of the chosen avatar; never null, empty or white space.</summary>
        public string AvatarId { get; private set; }

        /// <summary>
        /// The name rule (Module 38). <paramref name="raw"/> is trimmed, runs
        /// of spaces inside it are collapsed to one, and the result must be
        /// <see cref="MinNameLength"/> to <see cref="MaxNameLength"/>
        /// characters, each a letter of any alphabet, a digit, a space,
        /// <c>-</c>, <c>_</c> or <c>.</c>. Anything else — another symbol, a
        /// tab inside the name, an emoji — refuses the whole name, which is
        /// never changed beyond trimming and collapsing. True gives that name
        /// in <paramref name="name"/>; false leaves it null.
        /// </summary>
        /// <remarks>
        /// Each character is one UTF-16 unit: a letter outside the Basic
        /// Multilingual Plane, and a combining mark such as a decomposed
        /// dot above, are refused. The length is counted after collapsing.
        /// </remarks>
        public static bool TryNormalizeName(string raw, out string name)
        {
            name = null;
            if (raw == null)
            {
                return false;
            }

            var trimmed = raw.Trim();
            var built = new StringBuilder(trimmed.Length);
            for (var i = 0; i < trimmed.Length; i++)
            {
                var c = trimmed[i];
                if (c == Space)
                {
                    if (built.Length > 0 && built[built.Length - 1] == Space)
                    {
                        continue;
                    }

                    built.Append(c);
                }
                else if (char.IsLetter(c) || char.IsDigit(c) || c == Hyphen || c == Underscore || c == Period)
                {
                    built.Append(c);
                }
                else
                {
                    return false;
                }
            }

            if (built.Length < MinNameLength || built.Length > MaxNameLength)
            {
                return false;
            }

            name = built.ToString();
            return true;
        }

        /// <summary>
        /// The profile of a new player, and of a save older than format
        /// version 4: <paramref name="rules"/>' default name and avatar (D60, D71).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static PlayerProfile Starting(MetaRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return new PlayerProfile(rules.DefaultPlayerName, rules.DefaultAvatarId);
        }

        /// <summary>
        /// Sets the name to <paramref name="raw"/> as the name rule normalizes
        /// it. False, with the name unchanged, when the rule refuses it. True
        /// also when the normalized name is the one already held.
        /// </summary>
        public bool TrySetName(string raw)
        {
            if (!TryNormalizeName(raw, out var normalized))
            {
                return false;
            }

            Name = normalized;
            return true;
        }

        /// <summary>Chooses the avatar <paramref name="avatarId"/>. Whether the catalog has it is not checked here.</summary>
        /// <exception cref="ArgumentException"><paramref name="avatarId"/> is null, empty or white space; the avatar is unchanged.</exception>
        public void SetAvatar(string avatarId)
        {
            RequireAvatarId(avatarId);
            AvatarId = avatarId;
        }

        private static void RequireAvatarId(string avatarId)
        {
            if (string.IsNullOrWhiteSpace(avatarId))
            {
                throw new ArgumentException("An avatar id may not be empty.", nameof(avatarId));
            }
        }
    }
}
