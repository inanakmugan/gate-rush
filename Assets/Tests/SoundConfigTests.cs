using System;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 25's <see cref="SoundConfig"/>: a fresh asset has an
    /// entry for every cue and nothing wrong but its missing clips, which are
    /// reported per cue and never thrown; and each value that can be wrong is
    /// reported by <see cref="SoundConfig.Problems"/>, naming its field or
    /// cue.
    /// </summary>
    /// <remarks>
    /// A freshly created config has no clips, so
    /// <see cref="SoundConfig.Problems"/> is never empty here; each test looks
    /// for, or rules out, the problems about its own subject alone.
    /// </remarks>
    public class SoundConfigTests
    {
        private const string CuesField = "cues";
        private const SoundCue SomeCue = SoundCue.Exit;

        private SoundConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SoundConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void Problems_FreshConfig_ReportsNoClipForEveryCueAndNothingElse()
        {
            var problems = config.Problems();

            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                Assert.That(problems, Has.Some.Contains($"entry for {cue} has no clip"), string.Join(" | ", problems));
            }

            // Every message here is a missing clip: the defaults are otherwise valid.
            Assert.That(problems, Has.All.Contains("has no clip"), string.Join(" | ", problems));
        }

        [Test]
        public void TryGetCue_FreshConfig_FindsEveryCueWithNoClips([Values] SoundCue cue)
        {
            var isFound = config.TryGetCue(cue, out var clips, out var volume, out var pitchMin, out var pitchMax);

            Assert.IsTrue(isFound);
            Assert.IsEmpty(clips);
            Assert.That(volume, Is.InRange(0f, 1f));
            Assert.That(pitchMin, Is.GreaterThan(0f));
            Assert.That(pitchMax, Is.GreaterThanOrEqualTo(pitchMin));
        }

        [Test]
        public void TryGetCue_CueWithoutAnEntry_IsFalseWithNoClips()
        {
            DeleteEntry(SomeCue);

            var isFound = config.TryGetCue(SomeCue, out var clips, out _, out _, out _);

            Assert.IsFalse(isFound);
            Assert.IsEmpty(clips);
        }

        [Test]
        public void Problems_CueWithoutAnEntry_IsReportedByName([Values] SoundCue cue)
        {
            DeleteEntry(cue);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"no entry for {cue}"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DuplicateEntry_IsReportedByName()
        {
            // The Peel entry is turned into a second Exit entry.
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(CuesField);
            entries.GetArrayElementAtIndex(IndexOf(entries, SoundCue.Peel)).FindPropertyRelative("cue").intValue =
                (int)SoundCue.Exit;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"2 entries for {SoundCue.Exit}"), string.Join(" | ", problems));
            Assert.That(problems, Has.Some.Contains($"no entry for {SoundCue.Peel}"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_CueWithOnlyAnEmptyClipSlot_IsReportedAsHavingNoClip()
        {
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(CuesField);
            entries.GetArrayElementAtIndex(IndexOf(entries, SomeCue)).FindPropertyRelative("clips").arraySize = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"entry for {SomeCue} has no clip"), string.Join(" | ", problems));
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void Problems_CueVolumeOutsideZeroToOne_IsReportedByName(float volume)
        {
            SetCueFloat(SomeCue, "volume", volume);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"entry for {SomeCue} has a volume outside 0 to 1"), string.Join(" | ", problems));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Problems_CuePitchAtOrBelowZero_IsReportedByName(float pitchMin)
        {
            SetCueFloat(SomeCue, "pitchMin", pitchMin);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"entry for {SomeCue} has a pitch at or below 0"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_CueMinimumPitchAboveItsMaximum_IsReportedByName()
        {
            SetCueFloat(SomeCue, "pitchMin", 1.2f);
            SetCueFloat(SomeCue, "pitchMax", 0.8f);

            var problems = config.Problems();

            Assert.That(
                problems, Has.Some.Contains($"entry for {SomeCue} has a minimum pitch above its maximum"),
                string.Join(" | ", problems));
        }

        [Test]
        public void Problems_CuePitchAboveTheEnginesLimit_IsReportedByName()
        {
            SetCueFloat(SomeCue, "pitchMax", 3.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"entry for {SomeCue} has a pitch above"), string.Join(" | ", problems));
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        public void Problems_MasterVolumeOutsideZeroToOne_IsReported(float volume)
        {
            SetFloat("masterVolume", volume);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Master Volume must be from 0 to 1"), string.Join(" | ", problems));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Problems_VoiceCountBelowOne_IsReported(int voiceCount)
        {
            SetInt("voiceCount", voiceCount);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Voice Count must be at least 1"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NegativeMinRepeatSeconds_IsReported()
        {
            SetFloat("minRepeatSeconds", -0.01f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Min Repeat Seconds must be at least 0"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NegativeTickFrom_IsReported()
        {
            SetInt("tickFromSeconds", -1);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Tick From Seconds may not be negative"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_TickFromZero_IsNotReported()
        {
            SetInt("tickFromSeconds", 0);

            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Tick From Seconds"), string.Join(" | ", problems));
        }

        [TestCase("exitPulseMilliseconds", "Exit Pulse Milliseconds")]
        [TestCase("winPulseMilliseconds", "Win Pulse Milliseconds")]
        [TestCase("lossPulseMilliseconds", "Loss Pulse Milliseconds")]
        public void Problems_NegativePulseLength_IsReportedByName(string field, string shownName)
        {
            SetInt(field, -1);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} may not be negative"), string.Join(" | ", problems));
        }

        [Test]
        public void PulseMilliseconds_EveryMoment_IsTheFieldOfThatMoment()
        {
            SetInt("exitPulseMilliseconds", 11);
            SetInt("winPulseMilliseconds", 22);
            SetInt("lossPulseMilliseconds", 33);

            var exit = config.PulseMilliseconds(HapticMoment.Exit);
            var win = config.PulseMilliseconds(HapticMoment.Win);
            var loss = config.PulseMilliseconds(HapticMoment.Loss);

            Assert.AreEqual(11, exit);
            Assert.AreEqual(22, win);
            Assert.AreEqual(33, loss);
        }

        private void DeleteEntry(SoundCue cue)
        {
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(CuesField);
            entries.DeleteArrayElementAtIndex(IndexOf(entries, cue));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetCueFloat(SoundCue cue, string field, float value)
        {
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(CuesField);
            entries.GetArrayElementAtIndex(IndexOf(entries, cue)).FindPropertyRelative(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetFloat(string field, float value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInt(string field, int value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The index of <paramref name="cue"/>'s entry in the serialized list.</summary>
        private static int IndexOf(SerializedProperty entries, SoundCue cue)
        {
            for (var i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("cue").intValue == (int)cue)
                {
                    return i;
                }
            }

            Assert.Fail($"the default config has an entry for {cue}");
            return -1;
        }
    }
}
