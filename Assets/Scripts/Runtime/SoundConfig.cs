using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Every tunable value of the game's sound and vibration (Module 25): each
    /// cue's clips, volume and pitch range, the master volume, how many sounds
    /// play at once, how soon a cue may repeat, when the timer starts to tick,
    /// and the vibration lengths. Nothing in <see cref="SoundPlayer"/>
    /// hardcodes one of these.
    /// </summary>
    /// <remarks>
    /// <para><b>Clips are assigned by hand.</b> A fresh asset has none, and
    /// <see cref="Problems"/> says so for every cue; the game then runs
    /// silently. A cue with no clip is never an error at play time.</para>
    /// <para>The clips come from Kenney's CC0 packs under
    /// <c>Assets/Audio/Sfx</c>, imported Force To Mono, Decompress On Load,
    /// Preload Audio Data on.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "SoundConfig", menuName = "Gate Rush/Sound Config")]
    public sealed class SoundConfig : ScriptableObject
    {
        /// <summary>
        /// The highest pitch an <see cref="AudioSource"/> plays at. Not a
        /// tunable: it is the engine's limit, and a higher value is clamped
        /// to it unheard.
        /// </summary>
        private const float HighestPitch = 3f;

        [Header("Cues")]
        [Tooltip("One entry per cue: its clips (one is picked at random each time), its volume from 0 to 1, and the range its pitch is picked from (1 is the clip as recorded; above 0, at most 3).")]
        [SerializeField] private CueSound[] cues =
        {
            new CueSound(SoundCue.Lift, 0.8f, 0.95f, 1.05f),
            new CueSound(SoundCue.Drop, 0.8f, 0.95f, 1.05f),
            new CueSound(SoundCue.Exit, 1f, 0.95f, 1.05f),
            new CueSound(SoundCue.Peel, 1f, 0.95f, 1.05f),
            new CueSound(SoundCue.IceBreak, 1f, 0.95f, 1.05f),
            new CueSound(SoundCue.ShutterOpen, 1f, 0.95f, 1.05f),
            new CueSound(SoundCue.Unlock, 1f, 0.95f, 1.05f),
            new CueSound(SoundCue.Spawn, 0.9f, 0.95f, 1.05f),
            new CueSound(SoundCue.CountPop, 0.5f, 0.95f, 1.05f),
            new CueSound(SoundCue.TimeBonus, 1f, 1f, 1f),
            new CueSound(SoundCue.TimerTick, 0.7f, 1f, 1f),
            new CueSound(SoundCue.Win, 1f, 1f, 1f),
            new CueSound(SoundCue.Loss, 1f, 1f, 1f),
            new CueSound(SoundCue.UiTap, 0.7f, 1f, 1f)
        };

        [Header("Levels and overlap")]
        [Tooltip("Multiplies every cue's volume, from 0 to 1.")]
        [SerializeField] private float masterVolume = 0.8f;

        [Tooltip("How many sounds can play at once: at least 1. When all are busy the one started longest ago is cut for the new one.")]
        [SerializeField] private int voiceCount = 8;

        [Tooltip("Seconds within which the same cue does not start again: at least 0. 0 lets a cue restart at once.")]
        [SerializeField] private float minRepeatSeconds = 0.05f;

        [Tooltip("Seeds the picking of clips and pitches. Any value is valid; the same value gives the same picks in the same order.")]
        [SerializeField] private int randomSeed = 2025;

        [Header("Timer")]
        [Tooltip("The timer ticks once for each whole second from this one down to 1: at least 0. 0 turns the ticking off.")]
        [SerializeField] private int tickFromSeconds = 5;

        [Header("Vibration (milliseconds; 0 turns a pulse off)")]
        [Tooltip("Length of the pulse when a block is destroyed through its gate.")]
        [SerializeField] private int exitPulseMilliseconds = 20;

        [Tooltip("Length of the pulse when the win panel opens.")]
        [SerializeField] private int winPulseMilliseconds = 60;

        [Tooltip("Length of the pulse when the loss panel opens.")]
        [SerializeField] private int lossPulseMilliseconds = 80;

        /// <summary>Multiplies every cue's volume, from 0 to 1.</summary>
        public float MasterVolume => masterVolume;

        /// <summary>How many sounds can play at once.</summary>
        public int VoiceCount => voiceCount;

        /// <summary>Seconds within which the same cue does not start again.</summary>
        public float MinRepeatSeconds => minRepeatSeconds;

        /// <summary>Seeds the picking of clips and pitches.</summary>
        public int RandomSeed => randomSeed;

        /// <summary>The first whole second of the countdown that ticks, counting down (<see cref="TimerTicks"/>).</summary>
        public int TickFromSeconds => tickFromSeconds;

        /// <summary>The length of the pulse at <paramref name="moment"/>, in milliseconds; 0 for no pulse.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="moment"/> is not a member of <see cref="HapticMoment"/>.</exception>
        public int PulseMilliseconds(HapticMoment moment)
        {
            switch (moment)
            {
                case HapticMoment.Exit:
                    return exitPulseMilliseconds;
                case HapticMoment.Win:
                    return winPulseMilliseconds;
                case HapticMoment.Loss:
                    return lossPulseMilliseconds;
                default:
                    throw new ArgumentOutOfRangeException(nameof(moment), moment, "Not a haptic moment.");
            }
        }

        /// <summary>
        /// The sound of <paramref name="cue"/>: its clips — some of which may
        /// be unassigned — its volume before the master volume, and its pitch
        /// range. False, with no clips, when the cue has no entry; see
        /// <see cref="Problems"/>. Never throws: a cue that cannot be played
        /// is silent.
        /// </summary>
        public bool TryGetCue(
            SoundCue cue, out IReadOnlyList<AudioClip> clips, out float volume, out float pitchMin, out float pitchMax)
        {
            for (var i = 0; cues != null && i < cues.Length; i++)
            {
                var entry = cues[i];
                if (entry != null && entry.Cue == cue)
                {
                    clips = entry.Clips;
                    volume = entry.Volume;
                    pitchMin = entry.PitchMin;
                    pitchMax = entry.PitchMax;
                    return true;
                }
            }

            clips = Array.Empty<AudioClip>();
            volume = 0f;
            pitchMin = 0f;
            pitchMax = 0f;
            return false;
        }

        /// <summary>
        /// Everything wrong with this asset, each message naming its field or
        /// cue; empty when it is usable as it is. A cue with no clip is
        /// reported, and plays nothing.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();

            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                AddCueProblems(problems, cue);
            }

            if (!(masterVolume >= 0f && masterVolume <= 1f))
            {
                problems.Add($"{name}: Master Volume must be from 0 to 1.");
            }

            if (voiceCount < 1)
            {
                problems.Add($"{name}: Voice Count must be at least 1.");
            }

            if (!(minRepeatSeconds >= 0f) || float.IsInfinity(minRepeatSeconds))
            {
                problems.Add($"{name}: Min Repeat Seconds must be at least 0 and finite.");
            }

            if (tickFromSeconds < 0)
            {
                problems.Add($"{name}: Tick From Seconds may not be negative.");
            }

            AddIfNegative(problems, exitPulseMilliseconds, "Exit Pulse Milliseconds");
            AddIfNegative(problems, winPulseMilliseconds, "Win Pulse Milliseconds");
            AddIfNegative(problems, lossPulseMilliseconds, "Loss Pulse Milliseconds");

            return problems;
        }

        /// <summary>The problems of <paramref name="cue"/>'s entry: missing, duplicated, without a clip, or with a level out of range.</summary>
        private void AddCueProblems(List<string> problems, SoundCue cue)
        {
            var entries = 0;
            CueSound first = null;
            for (var i = 0; cues != null && i < cues.Length; i++)
            {
                if (cues[i] != null && cues[i].Cue == cue)
                {
                    first = first ?? cues[i];
                    entries++;
                }
            }

            if (entries == 0)
            {
                problems.Add($"{name}: Cues has no entry for {cue}.");
                return;
            }

            if (entries > 1)
            {
                problems.Add($"{name}: Cues has {entries} entries for {cue}; it needs exactly one.");
            }

            var assigned = 0;
            var clips = first.Clips;
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                {
                    assigned++;
                }
            }

            if (assigned == 0)
            {
                problems.Add($"{name}: Cues entry for {cue} has no clip; the cue is silent.");
            }
            else if (assigned < clips.Count)
            {
                problems.Add($"{name}: Cues entry for {cue} has an empty clip slot; it is skipped.");
            }

            if (!(first.Volume >= 0f && first.Volume <= 1f))
            {
                problems.Add($"{name}: Cues entry for {cue} has a volume outside 0 to 1.");
            }

            if (!(first.PitchMin > 0f))
            {
                problems.Add($"{name}: Cues entry for {cue} has a pitch at or below 0.");
            }

            if (!(first.PitchMax >= first.PitchMin))
            {
                problems.Add($"{name}: Cues entry for {cue} has a minimum pitch above its maximum.");
            }

            if (!(first.PitchMax <= HighestPitch))
            {
                problems.Add($"{name}: Cues entry for {cue} has a pitch above {HighestPitch}.");
            }
        }

        private void AddIfNegative(List<string> problems, int value, string field)
        {
            if (value < 0)
            {
                problems.Add($"{name}: {field} may not be negative.");
            }
        }

        /// <summary>One cue's sound: its clips and its levels.</summary>
        [Serializable]
        private sealed class CueSound
        {
            [SerializeField] private SoundCue cue;
            [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
            [SerializeField] private float volume;
            [SerializeField] private float pitchMin;
            [SerializeField] private float pitchMax;

            public CueSound(SoundCue cue, float volume, float pitchMin, float pitchMax)
            {
                this.cue = cue;
                this.volume = volume;
                this.pitchMin = pitchMin;
                this.pitchMax = pitchMax;
            }

            public SoundCue Cue => cue;

            /// <summary>Never null; an element may be, for a slot left unassigned.</summary>
            public IReadOnlyList<AudioClip> Clips => clips ?? Array.Empty<AudioClip>();

            public float Volume => volume;

            public float PitchMin => pitchMin;

            public float PitchMax => pitchMax;
        }
    }
}
