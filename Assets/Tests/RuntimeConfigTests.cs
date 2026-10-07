using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers the introduction cards' texts in <see cref="RuntimeConfig"/>
    /// (Module 19): every <see cref="LevelMechanic"/> has one entry with a
    /// title and a text, and a missing, blank or duplicated entry is a problem
    /// naming the mechanic. Module 20: the layered-block values and orders
    /// have their own checks, and the defaults pass them.
    /// </summary>
    /// <remarks>
    /// A freshly created config has no sprites assigned, so
    /// <see cref="RuntimeConfig.Problems"/> is never empty here; each test
    /// looks for, or rules out, the problems about its own subject alone.
    /// </remarks>
    public class RuntimeConfigTests
    {
        private const string EntriesField = "mechanicIntroductions";

        private RuntimeConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<RuntimeConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Introduction_EveryMechanic_HasANonEmptyTitleAndText()
        {
            var checkedMechanics = 0;

            foreach (var mechanic in LevelMechanics.All)
            {
                var title = config.IntroductionTitle(mechanic);
                var text = config.IntroductionText(mechanic);
                var subtitle = config.IntroductionSubtitle(mechanic);

                Assert.IsFalse(string.IsNullOrWhiteSpace(title), $"{mechanic}: title");
                Assert.IsFalse(string.IsNullOrWhiteSpace(text), $"{mechanic}: text");
                Assert.IsFalse(string.IsNullOrWhiteSpace(subtitle), $"{mechanic}: subtitle");
                checkedMechanics++;
            }

            Assert.AreEqual(System.Enum.GetValues(typeof(LevelMechanic)).Length, checkedMechanics, "every mechanic is checked");
        }

        [Test]
        public void IntroductionSubtitle_HowToPlay_HasItsOwn()
        {
            var howToPlay = config.IntroductionSubtitle(LevelMechanic.HowToPlay);
            var other = config.IntroductionSubtitle(LevelMechanic.IceBlock);

            Assert.AreNotEqual(other, howToPlay);
        }

        [Test]
        public void Problems_DefaultIntroductions_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Mechanic Introductions"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_MechanicWithoutAnEntry_IsReportedByName([Values] LevelMechanic mechanic)
        {
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(EntriesField);
            entries.DeleteArrayElementAtIndex(IndexOf(entries, mechanic));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"no entry for {mechanic}"), string.Join(" | ", problems));
        }

        [TestCase("title", "has no title")]
        [TestCase("text", "has no text")]
        public void Problems_BlankTitleOrText_IsReportedByName(string field, string complaint)
        {
            const LevelMechanic mechanic = LevelMechanic.Shutter;
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(EntriesField);
            entries.GetArrayElementAtIndex(IndexOf(entries, mechanic)).FindPropertyRelative(field).stringValue = "  ";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"entry for {mechanic} {complaint}"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DuplicateEntry_IsReportedByName()
        {
            // The Ice Door entry is turned into a second Ice Block entry.
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(EntriesField);
            entries.GetArrayElementAtIndex(IndexOf(entries, LevelMechanic.IceDoor)).FindPropertyRelative("mechanic").intValue =
                (int)LevelMechanic.IceBlock;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"2 entries for {LevelMechanic.IceBlock}"), string.Join(" | ", problems));
            Assert.That(problems, Has.Some.Contains($"no entry for {LevelMechanic.IceDoor}"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DefaultLayerValues_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            // Every layered-block message names a "Layer …" field, and every
            // mark overlap message says what "would overlap".
            Assert.That(problems, Has.None.Contains("Layer "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("would overlap"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Crowded"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DefaultLipValues_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Lip Offset Cells"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_LipOffsetPlusLayerInsetReachingHalfACell_IsReportedByName()
        {
            // Half a cell of lip alone: with any inset above 0, the default's
            // included, the two together pass it.
            SetFloat("lipOffsetCells", 0.5f);

            var problems = config.Problems();

            Assert.That(
                problems, Has.Some.Contains("Lip Offset Cells plus Layer Inset Cells must be below 0.5"),
                string.Join(" | ", problems));
        }

        [Test]
        public void Problems_LayerInsetOfHalfACell_IsReportedByName()
        {
            SetFloat("layerInsetCells", 0.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Layer Inset Cells must be above 0 and below 0.5"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_LayerEdgeNotBelowTheInset_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            var inset = serialized.FindProperty("layerInsetCells").floatValue;
            serialized.FindProperty("layerEdgeCells").floatValue = inset;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Layer Edge Cells must be at least 0 and below Layer Inset Cells"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_StudOrderNotAboveLayerOrder_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("studOrder").intValue = serialized.FindProperty("layerOrder").intValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Stud Order above Layer Order"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_CrowdedTripleMarkScaleAboveThePairs_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("crowdedTripleMarkScale").floatValue =
                serialized.FindProperty("crowdedMarkScale").floatValue + 0.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Crowded Triple Mark Scale must be above 0 and at most Crowded Mark Scale"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DefaultPeelValues_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Peel "), string.Join(" | ", problems));
            Assert.That(config.PeelCubeFraction, Is.InRange(0f, 1f));
            Assert.That(config.PeelBumpCells, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Problems_PeelCubeFractionAboveOne_IsReportedByName()
        {
            SetFloat("peelCubeFraction", 1.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Peel Cube Fraction must be from 0 to 1"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_PeelBumpOfHalfACell_IsReportedByName()
        {
            SetFloat("peelBumpCells", 0.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Peel Bump Cells must be at least 0 and below 0.5"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DefaultPullValues_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Pull "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Capture Range Cells"), string.Join(" | ", problems));
            Assert.DoesNotThrow(() => config.CreateDragSettings());
        }

        [Test]
        public void Problems_PullGlowAlphaAboveOne_IsReportedByName()
        {
            SetFloat("pullGlowAlpha", 1.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Pull Glow Alpha must be from 0 to 1"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DefaultNudgeValue_ReportsNothingAboutIt()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Nudge Max Cells"), string.Join(" | ", problems));
            Assert.That(config.CreateDragSettings().NudgeMaxCells, Is.InRange(0f, DragSettings.MaxNudgeCellsExclusive));
        }

        [TestCase(-0.1f)]
        [TestCase(DragSettings.MaxNudgeCellsExclusive)]
        public void Problems_NudgeMaxCellsOutOfRange_IsReportedByName(float value)
        {
            SetFloat("nudgeMaxCells", value);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Nudge Max Cells must be at least 0 and below"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_PullGlowOrderNotBelowTheBlockLip_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("pullGlowOrder").intValue = serialized.FindProperty("blockLipOrder").intValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Pull Glow Order must be below Block Lip Order"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_CaptureRangeBeyondThePullRange_IsReportedByName()
        {
            SetFloat("captureRangeCells", 0.9f);

            var problems = config.Problems();

            Assert.That(
                problems, Has.Some.Contains("Capture Range Cells must be positive and at most Pull Range Cells"),
                string.Join(" | ", problems));
        }

        private void SetFloat(string field, float value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The index of <paramref name="mechanic"/>'s entry in the serialized list.</summary>
        private static int IndexOf(SerializedProperty entries, LevelMechanic mechanic)
        {
            for (var i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("mechanic").intValue == (int)mechanic)
                {
                    return i;
                }
            }

            Assert.Fail($"the default config has an entry for {mechanic}");
            return -1;
        }
    }
}
