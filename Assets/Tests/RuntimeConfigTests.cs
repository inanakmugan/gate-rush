using GateRush.Meta;
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
    /// have their own checks, and the defaults pass them. Module 41: the
    /// result panel's and the confirmation's values are gone; the card's
    /// theme — its font and sprites assigned, a bordered panel sprite, the
    /// padding, the close button's cross and the pop — is checked by name.
    /// Module 43: the New Joker card's titles, sentences (the Clock's with
    /// the rules' seconds) and subtitle are the specified ones; a missing,
    /// blank, duplicated or unusable entry, an empty subtitle, unassigned
    /// Joker Icons, an icon size that is not positive and a transparent icon
    /// colour are each reported by name.
    /// </summary>
    /// <remarks>
    /// A freshly created config has no sprites assigned, so
    /// <see cref="RuntimeConfig.Problems"/> is never empty here; each test
    /// looks for, or rules out, the problems about its own subject alone.
    /// </remarks>
    public class RuntimeConfigTests
    {
        private const string EntriesField = "mechanicIntroductions";
        private const string JokerEntriesField = "jokerIntroductions";

        // The rules' Clock seconds the spec's sentence is written with.
        private const int ClockSeconds = 20;

        // A small stand-in for the card's panel sprite; only its border is read.
        private const int PanelTextureSize = 64;
        private const float PanelPixelsPerUnit = 100f;
        private const float PanelBorder = 16f;

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

        [TestCase(JokerKind.Clock, "Clock!")]
        [TestCase(JokerKind.Rocket, "Rocket!")]
        [TestCase(JokerKind.Broom, "Broom!")]
        public void NewJokerTitle_Default_IsTheJokersNameWithAnExclamation(JokerKind kind, string expected)
        {
            var title = config.NewJokerTitle(kind);

            Assert.AreEqual(expected, title);
        }

        [TestCase(JokerKind.Clock, "Tap it to add 20 seconds to the clock.")]
        [TestCase(JokerKind.Rocket, "Tap the Rocket, then a block, to clear its colour.")]
        [TestCase(JokerKind.Broom, "Tap the Broom, then a block, to clear that colour from every block.")]
        public void NewJokerText_DefaultWithTwentySeconds_IsTheSpecifiedSentence(JokerKind kind, string expected)
        {
            var text = config.NewJokerText(kind, ClockSeconds);

            Assert.AreEqual(expected, text);
        }

        [Test]
        public void NewJokerSubtitle_Default_IsNewJokerUnlocked()
        {
            Assert.AreEqual("New Joker Unlocked!", config.NewJokerSubtitle);
        }

        [Test]
        public void Problems_DefaultJokerIntroductions_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Joker Introductions"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("New Joker"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_JokerWithoutAnEntry_IsReportedByName([Values] JokerKind kind)
        {
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(JokerEntriesField);
            entries.DeleteArrayElementAtIndex(IndexOf(entries, kind));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"Joker Introductions has no entry for {kind}"), string.Join(" | ", problems));
        }

        [TestCase("title", "has no title")]
        [TestCase("text", "has no text")]
        public void Problems_BlankJokerTitleOrText_IsReportedByName(string field, string complaint)
        {
            const JokerKind kind = JokerKind.Rocket;
            SetJokerEntryString(kind, field, "  ");

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"entry for {kind} {complaint}"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_JokerTextWithAPlaceholderPastTheSeconds_IsReportedByName()
        {
            const JokerKind kind = JokerKind.Clock;
            SetJokerEntryString(kind, "text", "Adds {1} seconds.");

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"entry for {kind} must have at most the placeholder"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DuplicateJokerEntry_IsReportedByName()
        {
            // The Broom's entry is turned into a second Rocket entry.
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(JokerEntriesField);
            entries.GetArrayElementAtIndex(IndexOf(entries, JokerKind.Broom)).FindPropertyRelative("joker").intValue = (int)JokerKind.Rocket;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"2 entries for {JokerKind.Rocket}"), string.Join(" | ", problems));
            Assert.That(problems, Has.Some.Contains($"Joker Introductions has no entry for {JokerKind.Broom}"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_EmptyNewJokerSubtitle_IsReportedByName()
        {
            SetString("newJokerSubtitle", " ");

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("New Joker Subtitle may not be empty"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_JokerIconsUnassigned_IsReportedByName()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Joker Icons is not assigned"), string.Join(" | ", problems));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Problems_NewJokerIconSizeNotPositive_IsReportedByName(float size)
        {
            SetFloat("newJokerIconSizeUnits", size);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("New Joker Icon Size Units must be positive"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_TransparentNewJokerIconColor_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("newJokerIconColor").colorValue = Color.clear;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("New Joker Icon Color must not be fully transparent"), string.Join(" | ", problems));
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

        [Test]
        public void Problems_DefaultGeneratorPreviewValues_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Generator Preview"), string.Join(" | ", problems));
        }

        [TestCase("generatorPreviewFillAlpha", 0f)]
        [TestCase("generatorPreviewFillAlpha", 1.5f)]
        [TestCase("generatorPreviewRimAlpha", 0f)]
        [TestCase("generatorPreviewRimAlpha", 1.5f)]
        public void Problems_GeneratorPreviewAlphaOutOfRange_IsReportedByName(string field, float value)
        {
            SetFloat(field, value);

            var problems = config.Problems();

            Assert.That(
                problems,
                Has.Some.Contains("Generator Preview Fill Alpha and Generator Preview Rim Alpha must be above 0 and at most 1"),
                string.Join(" | ", problems));
        }

        [Test]
        public void Problems_GeneratorPreviewInsetAndRimReachingHalfACell_IsReportedByName()
        {
            // With the default rim above 0, an inset of half a cell alone
            // leaves the cell no fill.
            SetFloat("generatorPreviewInsetCells", 0.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Generator Preview Inset Cells must be at least 0"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_GeneratorPreviewCornerNotAboveTheRim_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("generatorPreviewCornerCells").floatValue =
                serialized.FindProperty("generatorPreviewRimCells").floatValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(
                problems, Has.Some.Contains("Generator Preview Corner Cells must be above Generator Preview Rim Cells"),
                string.Join(" | ", problems));
        }

        [TestCase("generatorPreviewFadeInSeconds", "Generator Preview Fade In Seconds")]
        [TestCase("generatorPreviewFadeOutSeconds", "Generator Preview Fade Out Seconds")]
        public void Problems_NonPositiveGeneratorPreviewFade_IsReportedByName(string field, string shownName)
        {
            SetFloat(field, 0f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be positive"), string.Join(" | ", problems));
        }

        [TestCase("generatorPreviewRimOrder", "floorOrder")]
        [TestCase("generatorPreviewRimOrder", "elevatorBorderOrder")]
        [TestCase("generatorPreviewRimOrder", "generatorPreviewOrder")]
        public void Problems_GeneratorPreviewRimOrderNotInItsPlace_IsReportedByName(string field, string sameAs)
        {
            SetOrderEqualTo(field, sameAs);

            var problems = config.Problems();

            Assert.That(
                problems, Has.Some.Contains("Generator Preview Rim Order must be above Floor Order and Elevator Border Order"),
                string.Join(" | ", problems));
        }

        [TestCase("blockLipOrder")]
        [TestCase("shutterOrder")]
        [TestCase("liftedBlockOrder")]
        public void Problems_GeneratorPreviewOrderNotBelowWhatCoversIt_IsReportedByName(string sameAs)
        {
            SetOrderEqualTo("generatorPreviewOrder", sameAs);

            var problems = config.Problems();

            Assert.That(
                problems,
                Has.Some.Contains("Generator Preview Order must be below Block Lip Order, Shutter Order and Lifted Block Order"),
                string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DefaultMetaWiringValues_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            // Every message about Module 32's values names one of these.
            Assert.That(problems, Has.None.Contains("Meta "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Format"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Confirm "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("is empty"), string.Join(" | ", problems));
        }

        [Test]
        public void Defaults_MetaTexts_AreTheSpecified()
        {
            // The confirmation's texts moved to the UI Config (Module 41).
            Assert.AreEqual("All levels played. Play any level from Settings.", config.CampaignDoneNotice);
        }

        [TestCase("campaignDoneNotice", "Campaign Done Notice")]
        public void Problems_EmptyMetaText_IsReportedByName(string field, string shownName)
        {
            SetString(field, "  ");

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} is empty"), string.Join(" | ", problems));
        }

        // ----- Module 41: the introduction card's theme -------------------------
        // The result panel's and the confirmation's values left with them.

        [Test]
        public void Problems_DefaultIntroLookValues_ReportNothingButTheUnassignedAssets()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Intro Panel Padding Units"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Intro Close Size Units"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Intro Pop"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("needs a border"), string.Join(" | ", problems));
        }

        [TestCase("Intro Font")]
        [TestCase("Intro Panel Sprite")]
        [TestCase("Intro Close Sprite")]
        [TestCase("Intro Close Cross Sprite")]
        public void Problems_UnassignedIntroLookAsset_IsReportedByName(string shownName)
        {
            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} is not assigned"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_IntroPanelSpriteWithoutBorder_IsReportedByName()
        {
            var texture = new Texture2D(PanelTextureSize, PanelTextureSize);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, PanelTextureSize, PanelTextureSize), Vector2.zero);
            try
            {
                SetObject("introPanelSprite", sprite);

                var problems = config.Problems();

                Assert.That(problems, Has.Some.Contains("Intro Panel Sprite is drawn 9-sliced"), string.Join(" | ", problems));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Problems_IntroPanelSpriteWithBorder_ReportsNothingAboutIt()
        {
            var texture = new Texture2D(PanelTextureSize, PanelTextureSize);
            var sprite = Sprite.Create(
                texture, new Rect(0f, 0f, PanelTextureSize, PanelTextureSize), Vector2.zero, PanelPixelsPerUnit, 0u,
                SpriteMeshType.FullRect, Vector4.one * PanelBorder);
            try
            {
                SetObject("introPanelSprite", sprite);

                var problems = config.Problems();

                Assert.That(problems, Has.None.Contains("Intro Panel Sprite"), string.Join(" | ", problems));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Problems_NegativeIntroPanelPadding_IsReportedByName()
        {
            SetFloat("introPanelPaddingUnits", -1f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Intro Panel Padding Units must be at least 0"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_IntroCloseCrossLargerThanTheButton_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            var size = serialized.FindProperty("introCloseSizeUnits").floatValue;
            serialized.FindProperty("introCloseCrossSizeUnits").floatValue = size + 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Intro Close Cross Size Units positive and at most it"), string.Join(" | ", problems));
        }

        [TestCase("introPopSeconds", "Intro Pop Seconds")]
        [TestCase("introPopStartScale", "Intro Pop Start Scale")]
        public void Problems_NonPositiveIntroPop_IsReportedByName(string field, string shownName)
        {
            SetFloat(field, 0f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be positive"), string.Join(" | ", problems));
        }

        // ----- Module 34: the joker break ---------------------------------------
        // The joker bar's values left with the bar in Module 40.

        [Test]
        public void Problems_DefaultJokerBreakValue_ReportsNothingAboutIt()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Break Fade"), string.Join(" | ", problems));
        }

        [TestCase("breakFadeSeconds", "Break Fade Seconds")]
        public void Problems_NonPositiveJokerBreakTime_IsReportedByName(string field, string shownName)
        {
            SetFloat(field, 0f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be positive"), string.Join(" | ", problems));
        }

        /// <summary>Sets the sorting order <paramref name="field"/> to the value <paramref name="sameAs"/> has.</summary>
        private void SetOrderEqualTo(string field, string sameAs)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).intValue = serialized.FindProperty(sameAs).intValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetString(string field, string value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetObject(string field, Object value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        /// <summary>The index of <paramref name="kind"/>'s New Joker entry in the serialized list.</summary>
        private static int IndexOf(SerializedProperty entries, JokerKind kind)
        {
            for (var i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("joker").intValue == (int)kind)
                {
                    return i;
                }
            }

            Assert.Fail($"the default config has a New Joker entry for {kind}");
            return -1;
        }

        private void SetJokerEntryString(JokerKind kind, string field, string value)
        {
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(JokerEntriesField);
            entries.GetArrayElementAtIndex(IndexOf(entries, kind)).FindPropertyRelative(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
