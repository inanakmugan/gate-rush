using System;
using System.Collections.Generic;
using GateRush.Platform;

namespace GateRush.Meta
{
    /// <summary>
    /// Loads and saves the one <see cref="SaveData"/> (Module 29, D56): under
    /// one key, <see cref="SaveKey"/>, as text from an <see cref="ISaveCodec"/>.
    /// The rules for what a missing, unreadable or too new save gives, and how
    /// the old keys of Modules 23 and 25 are carried over, live here; the codec
    /// only encodes and decodes.
    /// </summary>
    /// <remarks>
    /// <para><b>Loading</b>, in this order:</para>
    /// <list type="number">
    /// <item>A save under <see cref="SaveKey"/> is decoded:
    /// <see cref="SaveLoadOutcome.Loaded"/>. One that cannot be read gives the
    /// defaults and <see cref="SaveLoadOutcome.Unreadable"/>, and nothing is
    /// written, so its text stays until the next change replaces it.</item>
    /// <item>Otherwise the old keys, <see cref="CompletedLevels.LegacySaveKey"/>
    /// and <see cref="SoundSetting.LegacySaveKey"/>, are read when either
    /// exists. One that cannot be read gives its default and a problem; the
    /// other is kept. Every section they never held — the wallet, the lives,
    /// the jokers, the streak and the campaign — is a new player's. The save is written under <see cref="SaveKey"/> at once:
    /// <see cref="SaveLoadOutcome.CarriedOver"/>.</item>
    /// <item>Otherwise the defaults and <see cref="SaveLoadOutcome.Fresh"/>,
    /// and nothing is written.</item>
    /// </list>
    /// <para>Once a save exists under <see cref="SaveKey"/> the old keys are
    /// never read again, whatever they hold. They are not deleted:
    /// <see cref="ISaveStore"/> has no delete.</para>
    /// <para><b>What throws.</b> Nothing the store returns makes
    /// <see cref="Load"/> throw. A store that throws while reading is the
    /// caller's to handle. A store that throws while the carried-over save is
    /// written is caught, because this is the one write the caller never makes
    /// and could not repeat: the save is still returned, with a problem, and
    /// lasts until the game is closed. This assembly cannot name a platform's
    /// exception type, so any exception from that one write is caught.</para>
    /// </remarks>
    public sealed class SaveRepository
    {
        /// <summary>The one key the whole save is kept under.</summary>
        public const string SaveKey = "GateRush.Save";

        private readonly ISaveStore store;
        private readonly ISaveCodec codec;
        private readonly MetaRules rules;

        /// <summary>
        /// A repository keeping the save in <paramref name="store"/>, as text
        /// from <paramref name="codec"/>. Every default it gives — a new
        /// player's, an unreadable save's, and that of a section an older save
        /// lacks (D60) — is built from <paramref name="rules"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="store"/>, <paramref name="codec"/> or <paramref name="rules"/> is null.</exception>
        public SaveRepository(ISaveStore store, ISaveCodec codec, MetaRules rules)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>
        /// The save, by the loading rules in the class remarks. Never throws
        /// for anything the store returns.
        /// </summary>
        public SaveLoad Load()
        {
            if (store.TryLoad(SaveKey, out var text))
            {
                return Decode(text);
            }

            var hasCompleted = store.TryLoad(CompletedLevels.LegacySaveKey, out var completedData);
            var hasSound = store.TryLoad(SoundSetting.LegacySaveKey, out var soundData);
            if (!hasCompleted && !hasSound)
            {
                return new SaveLoad(SaveData.CreateDefault(rules), SaveLoadOutcome.Fresh, Array.Empty<string>());
            }

            return CarryOver(hasCompleted, completedData, hasSound, soundData);
        }

        /// <summary>
        /// Writes the whole of <paramref name="data"/> under
        /// <see cref="SaveKey"/>, in one <see cref="ISaveStore.Save"/>. What
        /// the store throws reaches the caller.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
        public void Save(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            store.Save(SaveKey, codec.Encode(data));
        }

        private SaveLoad Decode(string text)
        {
            if (codec.TryDecode(text, rules, out var data, out var problem))
            {
                return new SaveLoad(data, SaveLoadOutcome.Loaded, Array.Empty<string>());
            }

            var problems = new[]
            {
                $"The save under '{SaveKey}' cannot be read: {problem} The defaults of a new player are used; " +
                "the saved text is left as it is until the next change replaces it.",
            };
            return new SaveLoad(SaveData.CreateDefault(rules), SaveLoadOutcome.Unreadable, problems);
        }

        private SaveLoad CarryOver(bool hasCompleted, string completedData, bool hasSound, string soundData)
        {
            var problems = new List<string>();

            var completed = new CompletedLevels();
            if (hasCompleted && !CompletedLevels.TryFromData(completedData, out completed))
            {
                problems.Add(
                    $"The completed levels saved under '{CompletedLevels.LegacySaveKey}' cannot be read ('{completedData}'); " +
                    "no level counts as completed.");
            }

            var sound = new SoundSetting();
            if (hasSound && !SoundSetting.TryFromData(soundData, out sound))
            {
                problems.Add(
                    $"The sound setting saved under '{SoundSetting.LegacySaveKey}' cannot be read ('{soundData}'); sound is on.");
            }

            var data = new SaveData(
                completed,
                sound,
                Wallet.Starting(rules),
                Lives.Full(rules),
                JokerInventory.Starting(rules),
                WinStreak.Starting(),
                CampaignProgress.Starting());
            try
            {
                Save(data);
            }
            catch (Exception e)
            {
                problems.Add(
                    $"The save was read from the old keys, but writing it under '{SaveKey}' failed ({e.Message}); " +
                    "it is kept until the game is closed, and until a save succeeds the old keys are read again at the next start.");
            }

            return new SaveLoad(data, SaveLoadOutcome.CarriedOver, problems);
        }
    }
}
