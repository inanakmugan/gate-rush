using System;
using System.Collections.Generic;
using GateRush.Platform;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Plays the game's sound cues and vibration pulses (Module 25). It
    /// decides nothing about when: the bootstrap calls it for the moments
    /// the views announce. Every level, count and length it uses is
    /// <see cref="SoundConfig"/>'s.
    /// </summary>
    /// <remarks>
    /// <para><b>Voices.</b> <see cref="Initialize"/> builds a pool of
    /// <see cref="AudioSource"/>s, one child each, as many as the config's
    /// voice count. A cue takes the first voice that is not playing; when all
    /// are, the one started longest ago is cut for it. There is no
    /// <c>AudioMixer</c>: a voice's volume is the master volume times its
    /// cue's.</para>
    /// <para><b>Variation.</b> A cue's clip is picked at random among those
    /// assigned, and its pitch within the cue's range, from a
    /// <see cref="System.Random"/> this player owns, seeded from the config —
    /// never <c>UnityEngine.Random</c>'s shared state.</para>
    /// <para><b>Repeats.</b> The same cue does not start again within the
    /// config's minimum repeat interval, measured on unscaled time, the clock
    /// the countdown and the UI run on.</para>
    /// <para><b>Silence is not an error.</b> Before <see cref="Initialize"/>,
    /// while disabled, with <see cref="IsOn"/> false, and for a cue with no
    /// clip, <see cref="Play"/> does nothing; it never throws.</para>
    /// <para><b>Sound and vibration are independent</b> (Module 38): cues
    /// follow <see cref="IsOn"/>, pulses <see cref="IsHapticsOn"/>.</para>
    /// <para><b>Stopping.</b> Every voice stops on <see cref="StopVoices"/>,
    /// when <see cref="IsOn"/> goes false, and when this component is
    /// disabled or destroyed. Nothing here is static.</para>
    /// </remarks>
    public sealed class SoundPlayer : MonoBehaviour
    {
        private readonly List<AudioSource> voices = new List<AudioSource>();

        // Per voice, the number of the start it last played: the lowest is
        // the voice started longest ago.
        private readonly List<long> voiceStarts = new List<long>();

        // Per cue, the unscaled time it last started.
        private readonly Dictionary<SoundCue, float> lastStarted = new Dictionary<SoundCue, float>();

        // The assigned clips of the cue being played; kept to allocate nothing per cue.
        private readonly List<AudioClip> assignedClips = new List<AudioClip>();

        private SoundConfig config;
        private IHaptics haptics;
        private System.Random random;
        private long startCount;
        private bool isOn = true;

        /// <summary>
        /// True while vibration is wanted; starts true. Pulses follow this,
        /// never <see cref="IsOn"/>: sound off leaves vibration on, and the
        /// other way round.
        /// </summary>
        public bool IsHapticsOn { get; set; } = true;

        /// <summary>
        /// True while sound is wanted; starts true. Setting it false stops
        /// every voice at once; setting it true affects the next cue. Pulses
        /// do not follow it (<see cref="IsHapticsOn"/>).
        /// </summary>
        public bool IsOn
        {
            get => isOn;
            set
            {
                isOn = value;
                if (!isOn)
                {
                    StopVoices();
                }
            }
        }

        /// <summary>
        /// Builds the voices from <paramref name="config"/>, replacing any
        /// built before, and takes <paramref name="haptics"/> for the pulses.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public void Initialize(SoundConfig config, IHaptics haptics)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));

            StopVoices();
            for (var i = 0; i < voices.Count; i++)
            {
                if (voices[i] != null)
                {
                    Destroy(voices[i].gameObject);
                }
            }

            voices.Clear();
            voiceStarts.Clear();
            startCount = 0;
            random = new System.Random(config.RandomSeed);

            for (var i = 0; i < config.VoiceCount; i++)
            {
                var voice = new GameObject($"Voice {i}");
                voice.transform.SetParent(transform, false);
                var source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                voices.Add(source);
                voiceStarts.Add(0);
            }
        }

        /// <summary>
        /// Plays <paramref name="cue"/>: one of its clips, at a pitch within
        /// its range. Does nothing while <see cref="IsOn"/> is false, within
        /// the minimum repeat interval of the same cue's last start, and for
        /// a cue with no clip.
        /// </summary>
        public void Play(SoundCue cue)
        {
            if (!isOn || config == null || !isActiveAndEnabled || voices.Count == 0)
            {
                return;
            }

            if (!config.TryGetCue(cue, out var clips, out var volume, out var pitchMin, out var pitchMax))
            {
                return;
            }

            var now = Time.unscaledTime;
            if (lastStarted.TryGetValue(cue, out var last) && now - last < config.MinRepeatSeconds)
            {
                return;
            }

            assignedClips.Clear();
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                {
                    assignedClips.Add(clips[i]);
                }
            }

            if (assignedClips.Count == 0)
            {
                return;
            }

            var clip = assignedClips[random.Next(assignedClips.Count)];
            var pitch = pitchMin + (pitchMax - pitchMin) * (float)random.NextDouble();
            assignedClips.Clear();

            var index = PickVoice();
            var source = voices[index];
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = config.MasterVolume * volume;
            source.Play();

            startCount++;
            voiceStarts[index] = startCount;
            lastStarted[cue] = now;
        }

        /// <summary>
        /// Vibrates for the length the config gives <paramref name="moment"/>,
        /// where the platform can. Does nothing while <see cref="IsHapticsOn"/>
        /// is false or before <see cref="Initialize"/>, whatever <see cref="IsOn"/> is.
        /// </summary>
        public void Pulse(HapticMoment moment)
        {
            if (!IsHapticsOn || config == null || haptics == null || !isActiveAndEnabled)
            {
                return;
            }

            haptics.Pulse(config.PulseMilliseconds(moment));
        }

        /// <summary>
        /// Stops every voice at once and forgets when each cue last started,
        /// so the next cue plays whatever was cut — for a restart, a level
        /// change and the way to the menu.
        /// </summary>
        public void StopVoices()
        {
            for (var i = 0; i < voices.Count; i++)
            {
                // A voice may already be gone while the scene is torn down.
                if (voices[i] != null)
                {
                    voices[i].Stop();
                }
            }

            lastStarted.Clear();
        }

        private void OnDisable()
        {
            StopVoices();
        }

        private void OnDestroy()
        {
            StopVoices();
        }

        /// <summary>
        /// The voice the next cue plays on: the first that is not playing,
        /// otherwise the one started longest ago. Called with at least one
        /// voice.
        /// </summary>
        private int PickVoice()
        {
            var oldest = 0;
            for (var i = 0; i < voices.Count; i++)
            {
                if (voices[i] == null || !voices[i].isPlaying)
                {
                    return i;
                }

                if (voiceStarts[i] < voiceStarts[oldest])
                {
                    oldest = i;
                }
            }

            return oldest;
        }
    }
}
