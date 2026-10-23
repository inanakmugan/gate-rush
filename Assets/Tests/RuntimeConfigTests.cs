using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
    /// colour are each reported by name. Module 46: the default palette and
    /// board colours are the measured ones; translucent faces and backdrops,
    /// light alphas past 1, outline and light orders out of place, a negative
    /// block lip and unassigned outline and light sprites are each reported
    /// by name; each quarter layer resolves to its own sprite, a light layer
    /// by the quarter's place and the reversed one by the opposite place.
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

        // Green's luminance stays this far above Red's (Module 46: about 0.27
        // against 0.20).
        private const float MinRedGreenLuminanceGap = 0.05f;

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
            Assert.That(problems, Has.None.Contains("Block Lip Cells"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_BlockLipPlusLayerInsetReachingHalfACell_IsReportedByName()
        {
            // Half a cell of lip alone: with any inset above 0, the default's
            // included, the two together pass it.
            SetFloat("blockLipCells", 0.5f);

            var problems = config.Problems();

            Assert.That(
                problems, Has.Some.Contains("Block Lip Cells plus Layer Inset Cells must be below 0.5"),
                string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NegativeBlockLip_IsReportedByName()
        {
            SetFloat("blockLipCells", -0.01f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Block Lip Cells may not be negative"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_LayerInsetOfHalfACell_IsReportedByName()
        {
            SetFloat("layerInsetCells", 0.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Layer Inset Cells must be above 0 and below 0.5"), string.Join(" | ", problems));
        }

        [TestCase(-0.01f)]
        [TestCase(0.5f)]
        public void Problems_BlockBadgeInsetOffItsCornerCell_IsReportedByName(float value)
        {
            SetFloat("blockBadgeInsetCells", value);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Block Badge Inset Cells must be at least 0 and below 0.5"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NegativeLayerEdge_IsReportedByName()
        {
            SetFloat("layerEdgeCells", -0.01f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Layer Edge Cells may not be negative"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_WindowLeavingTheInnerBlockNoHeight_IsReportedByName()
        {
            // Below half a cell on its own, but with the rim and the lip the
            // inner block would vanish.
            SetFloat("layerInsetCells", 0.45f);

            var problems = config.Problems();

            Assert.That(
                problems, Has.Some.Contains("Layer Inset Cells, Layer Edge Cells and Block Lip Cells together must be below 0.5"),
                string.Join(" | ", problems));
        }

        [TestCase("layeredOuterDarken", 1.5f)]
        [TestCase("layerEdgeDarken", -0.1f)]
        public void Problems_LayerDarkenOutOfRange_IsReportedByName(string field, float value)
        {
            SetFloat(field, value);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Layered Outer Darken and Layer Edge Darken must be from 0 to 1"), string.Join(" | ", problems));
        }

        [TestCase("layerStudSpacingCells")]
        [TestCase("layerStudClearanceCells")]
        public void Problems_NonPositiveLayerStudSpacingOrClearance_IsReportedByName(string field)
        {
            SetFloat(field, 0f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Layer Stud Spacing Cells and Layer Stud Clearance Cells must be above 0"), string.Join(" | ", problems));
        }

        [TestCase("studOrder", "layerEdgeOrder")]
        [TestCase("layerEdgeOrder", "glossOrder")]
        [TestCase("layerOrder", "layerEdgeOrder")]
        [TestCase("layerShineOrder", "layerStudOrder")]
        [TestCase("layerGlossOrder", "chainOrder")]
        public void Problems_LayeredOrderOutOfPlace_IsReportedByName(string field, string sameAs)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).intValue = serialized.FindProperty(sameAs).intValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("must rise in that order between Block Order and Chain Order"), string.Join(" | ", problems));
        }

        [Test]
        public void LayeredFills_AnOuterColour_AreItDarkenedByEachShare()
        {
            var outer = new Color(0.8f, 0.4f, 0.2f, 1f);
            var serialized = new SerializedObject(config);
            var outerShare = serialized.FindProperty("layeredOuterDarken").floatValue;
            var edgeShare = serialized.FindProperty("layerEdgeDarken").floatValue;

            var outerFill = config.LayeredOuterFill(outer);
            var edgeFill = config.LayerEdgeFill(outer);

            Assert.AreEqual(new Color(outer.r * outerShare, outer.g * outerShare, outer.b * outerShare, 1f), outerFill);
            Assert.AreEqual(new Color(outer.r * edgeShare, outer.g * edgeShare, outer.b * edgeShare, 1f), edgeFill);
            Assert.Less(edgeFill.r, outerFill.r, "the rim is darker than the surround");
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
        public void Problems_PullGlowOrderNotBelowTheBlockContour_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("pullGlowOrder").intValue = serialized.FindProperty("blockContourOrder").intValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Pull Glow Order must be below Block Contour Order"), string.Join(" | ", problems));
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
        [TestCase("Intro Text Box Sprite")]
        [TestCase("Intro Close Sprite")]
        [TestCase("Intro Close Cross Sprite")]
        public void Problems_UnassignedIntroLookAsset_IsReportedByName(string shownName)
        {
            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} is not assigned"), string.Join(" | ", problems));
        }

        [TestCase("introPanelSprite", "Intro Panel Sprite")]
        [TestCase("introTextBoxSprite", "Intro Text Box Sprite")]
        public void Problems_SlicedIntroSpriteWithoutBorder_IsReportedByName(string field, string shownName)
        {
            var texture = new Texture2D(PanelTextureSize, PanelTextureSize);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, PanelTextureSize, PanelTextureSize), Vector2.zero);
            try
            {
                SetObject(field, sprite);

                var problems = config.Problems();

                Assert.That(problems, Has.Some.Contains($"{shownName} is drawn 9-sliced"), string.Join(" | ", problems));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [TestCase("introPanelSprite", "Intro Panel Sprite")]
        [TestCase("introTextBoxSprite", "Intro Text Box Sprite")]
        public void Problems_SlicedIntroSpriteWithBorder_ReportsNothingAboutIt(string field, string shownName)
        {
            var texture = new Texture2D(PanelTextureSize, PanelTextureSize);
            var sprite = Sprite.Create(
                texture, new Rect(0f, 0f, PanelTextureSize, PanelTextureSize), Vector2.zero, PanelPixelsPerUnit, 0u,
                SpriteMeshType.FullRect, Vector4.one * PanelBorder);
            try
            {
                SetObject(field, sprite);

                var problems = config.Problems();

                Assert.That(problems, Has.None.Contains(shownName), string.Join(" | ", problems));
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

        [Test]
        public void BlockFill_DefaultPalette_IsTheMeasuredFaceOfEveryColour()
        {
            // The faces' middles measured from the reference screenshots
            // (Module 46), Green lightened at its hue to a luminance clear of
            // Red's. A face is white there, so the palette entry is the
            // colour the player sees.
            var expected = new Dictionary<BlockColor, Color32>
            {
                { BlockColor.Red, new Color32(0xF0, 0x2A, 0x1E, 0xFF) },
                { BlockColor.Blue, new Color32(0x00, 0x54, 0xF6, 0xFF) },
                { BlockColor.Green, new Color32(0x07, 0xA5, 0x15, 0xFF) },
                { BlockColor.Yellow, new Color32(0xF6, 0xBA, 0x00, 0xFF) },
                { BlockColor.Purple, new Color32(0xA2, 0x36, 0xF6, 0xFF) },
                { BlockColor.Orange, new Color32(0xF6, 0x66, 0x0C, 0xFF) },
                { BlockColor.Pink, new Color32(0xF6, 0x3C, 0xA2, 0xFF) },
                { BlockColor.Cyan, new Color32(0x06, 0xA8, 0xF6, 0xFF) },
            };

            var palette = new SerializedObject(config).FindProperty("blockPalette");

            Assert.AreEqual(Enum.GetValues(typeof(BlockColor)).Length, palette.arraySize, "one entry per colour");
            foreach (BlockColor color in Enum.GetValues(typeof(BlockColor)))
            {
                Assert.AreEqual(expected[color], (Color32)config.BlockFill(color), color.ToString());
            }
        }

        [Test]
        public void BlockFill_DefaultRedAndGreen_DifferInLuminanceForRedGreenColourBlindness()
        {
            // Relative luminance, from linear channels. Red and green that
            // match in it differ only in a hue the commonest colour blindness
            // cannot see.
            float Luminance(Color color)
            {
                var linear = color.linear;
                return 0.2126f * linear.r + 0.7152f * linear.g + 0.0722f * linear.b;
            }

            var red = Luminance(config.BlockFill(BlockColor.Red));
            var green = Luminance(config.BlockFill(BlockColor.Green));

            Assert.GreaterOrEqual(green - red, MinRedGreenLuminanceGap, $"red {red:0.000}, green {green:0.000}");
        }

        [Test]
        public void Defaults_BoardColours_AreTheMeasuredOnes()
        {
            var colours = new[]
            {
                (Actual: config.ContourColor, Expected: new Color32(0x1C, 0x0B, 0x3A, 0xFF), Name: "contour"),
                (Actual: config.FrameColor, Expected: new Color32(0x49, 0x21, 0x84, 0xFF), Name: "frame"),
                (Actual: config.FloorColor, Expected: new Color32(0x31, 0x18, 0x5F, 0xFF), Name: "floor"),
                (Actual: config.BackgroundTop, Expected: new Color32(0x60, 0x2D, 0xA0, 0xFF), Name: "background top"),
                (Actual: config.BackgroundBottom, Expected: new Color32(0x20, 0x0E, 0x3C, 0xFF), Name: "background bottom"),
            };

            foreach (var colour in colours)
            {
                Assert.AreEqual(colour.Expected, (Color32)colour.Actual, colour.Name);
            }
        }

        [Test]
        public void Problems_DefaultBoardLookValues_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("must be opaque"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("alpha must be from 0 to 1"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Contour Order"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Shine Order"), string.Join(" | ", problems));
        }

        [TestCase("contourColor", "Contour Color")]
        [TestCase("floorColor", "Floor Color")]
        [TestCase("frameColor", "Frame Color")]
        [TestCase("backgroundTop", "Background Top")]
        [TestCase("backgroundBottom", "Background Bottom")]
        public void Problems_TranslucentBoardColour_IsReportedByName(string field, string shownName)
        {
            SetAlpha(new SerializedObject(config), field, 0.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be opaque"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_TranslucentPaletteEntry_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            var entry = serialized.FindProperty("blockPalette").GetArrayElementAtIndex((int)BlockColor.Green);
            var color = entry.colorValue;
            color.a = 0.5f;
            entry.colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Block Palette entry 2 (Green) must be opaque"), string.Join(" | ", problems));
        }

        [TestCase("blockLightColor", "Block Light Color")]
        [TestCase("frameLightColor", "Frame Light Color")]
        public void Problems_LightAlphaAboveOne_IsReportedByName(string field, string shownName)
        {
            SetAlpha(new SerializedObject(config), field, 1.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName}'s alpha must be from 0 to 1"), string.Join(" | ", problems));
        }

        [TestCase("frameContourOrder", "frameLipOrder", "Frame Contour Order must be below Frame Lip Order")]
        [TestCase("frameShineOrder", "frameOrder", "Frame Shine Order must be above Frame Order")]
        [TestCase("gateArrowOutlineOrder", "frameShineOrder", "Gate Arrow Outline Order above Frame Shine Order")]
        [TestCase("gateArrowOutlineOrder", "gateMarkOrder", "Gate Mark Order above Gate Arrow Outline Order")]
        [TestCase("blockContourOrder", "gateMarkOrder", "Block Contour Order must be above Gate Mark Order and below Block Lip Order")]
        [TestCase("blockContourOrder", "blockLipOrder", "Block Contour Order must be above Gate Mark Order and below Block Lip Order")]
        [TestCase("blockShineOrder", "blockOrder", "must rise in that order between Block Order and Chain Order")]
        [TestCase("blockShineOrder", "studOrder", "must rise in that order between Block Order and Chain Order")]
        [TestCase("outlineOrder", "blockContourOrder", "Outline Order must be below Block Contour Order")]
        public void Problems_BoardLookOrderOutOfPlace_IsReportedByName(string field, string sameAs, string complaint)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).intValue = serialized.FindProperty(sameAs).intValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(complaint), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_UnassignedOutlineAndLightSprites_AreReportedByName()
        {
            var problems = config.Problems();

            foreach (var shownName in new[]
                     {
                         "Contour Outer Sprite", "Contour Edge Sprite", "Contour Concave Sprite",
                         "Light Outer Top Left Sprite", "Light Outer Top Right Sprite", "Light Outer Bottom Left Sprite",
                         "Light Outer Bottom Right Sprite", "Light Concave Top Left Sprite", "Light Concave Top Right Sprite",
                         "Light Concave Bottom Left Sprite", "Light Concave Bottom Right Sprite", "Light Edge Top Sprite",
                         "Light Edge Bottom Sprite", "Light Edge Left Sprite", "Light Edge Right Sprite",
                         "Gate Arrow Outline Sprite", "Single Stud Sprite", "Single Stud Gloss Sprite"
                     })
            {
                Assert.That(problems, Has.Some.Contains(shownName), shownName);
            }
        }

        [Test]
        public void QuarterSprite_FaceAndContour_IsThatLayersSpriteForThePiece()
        {
            var sprites = AssignQuarterSprites();

            try
            {
                Assert.AreSame(sprites["quarterOuterSprite"], config.QuarterSprite(QuarterLayer.Face, Tile(Quarter.BottomLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["quarterEdgeSprite"], config.QuarterSprite(QuarterLayer.Face, Tile(Quarter.TopRight, QuarterPiece.EdgeAlongY)));
                Assert.AreSame(sprites["quarterFillSprite"], config.QuarterSprite(QuarterLayer.Face, Tile(Quarter.TopRight, QuarterPiece.Fill)));
                Assert.AreSame(sprites["quarterConcaveSprite"], config.QuarterSprite(QuarterLayer.Face, Tile(Quarter.TopLeft, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["contourOuterSprite"], config.QuarterSprite(QuarterLayer.Contour, Tile(Quarter.TopRight, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["contourEdgeSprite"], config.QuarterSprite(QuarterLayer.Contour, Tile(Quarter.BottomLeft, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["quarterFillSprite"], config.QuarterSprite(QuarterLayer.Contour, Tile(Quarter.TopRight, QuarterPiece.Fill)), "a fill's contour is the face fill");
                Assert.AreSame(sprites["contourConcaveSprite"], config.QuarterSprite(QuarterLayer.Contour, Tile(Quarter.BottomRight, QuarterPiece.ConcaveCorner)));
            }
            finally
            {
                DestroySprites(sprites);
            }
        }

        [Test]
        public void QuarterSprite_Light_IsTheSpriteForTheQuartersPlace()
        {
            var sprites = AssignQuarterSprites();

            try
            {
                Assert.AreSame(sprites["lightOuterTopLeftSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.TopLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["lightOuterTopRightSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.TopRight, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["lightOuterBottomLeftSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.BottomLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["lightOuterBottomRightSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.BottomRight, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["lightConcaveTopLeftSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.TopLeft, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["lightConcaveTopRightSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.TopRight, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["lightConcaveBottomLeftSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.BottomLeft, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["lightConcaveBottomRightSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.BottomRight, QuarterPiece.ConcaveCorner)));

                // An edge along x faces up or down whichever side of its cell
                // it lies on along x; an edge along y faces left or right.
                Assert.AreSame(sprites["lightEdgeTopSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.TopLeft, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["lightEdgeTopSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.TopRight, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["lightEdgeBottomSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.BottomRight, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["lightEdgeLeftSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.BottomLeft, QuarterPiece.EdgeAlongY)));
                Assert.AreSame(sprites["lightEdgeRightSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.TopRight, QuarterPiece.EdgeAlongY)));
                Assert.AreSame(sprites["lightEdgeRightSprite"], config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.BottomRight, QuarterPiece.EdgeAlongY)));

                Assert.IsNull(config.QuarterSprite(QuarterLayer.Light, Tile(Quarter.TopRight, QuarterPiece.Fill)), "a fill carries no light");
            }
            finally
            {
                DestroySprites(sprites);
            }
        }

        [Test]
        public void QuarterSprite_ReversedLight_IsTheSpriteForTheOppositePlace()
        {
            var sprites = AssignQuarterSprites();

            try
            {
                Assert.AreSame(sprites["lightOuterBottomRightSprite"], config.QuarterSprite(QuarterLayer.ReversedLight, Tile(Quarter.TopLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["lightOuterTopLeftSprite"], config.QuarterSprite(QuarterLayer.ReversedLight, Tile(Quarter.BottomRight, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["lightConcaveBottomLeftSprite"], config.QuarterSprite(QuarterLayer.ReversedLight, Tile(Quarter.TopRight, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["lightEdgeBottomSprite"], config.QuarterSprite(QuarterLayer.ReversedLight, Tile(Quarter.TopLeft, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["lightEdgeLeftSprite"], config.QuarterSprite(QuarterLayer.ReversedLight, Tile(Quarter.TopRight, QuarterPiece.EdgeAlongY)));
                Assert.IsNull(config.QuarterSprite(QuarterLayer.ReversedLight, Tile(Quarter.TopRight, QuarterPiece.Fill)), "a fill carries no light");
            }
            finally
            {
                DestroySprites(sprites);
            }
        }

        /// <summary>A quarter of cell (0, 0); <see cref="RuntimeConfig.QuarterSprite"/> reads only its corner and piece.</summary>
        private static QuarterTile Tile(Quarter corner, QuarterPiece piece) => new QuarterTile(new Coord(0, 0), corner, piece);

        /// <summary>A distinct sprite on every quarter sprite field of the config, keyed by field, sharing one texture.</summary>
        private Dictionary<string, Sprite> AssignQuarterSprites()
        {
            var texture = new Texture2D(4, 4);
            var sprites = new Dictionary<string, Sprite>();
            var serialized = new SerializedObject(config);
            foreach (var field in new[]
                     {
                         "quarterOuterSprite", "quarterEdgeSprite", "quarterFillSprite", "quarterConcaveSprite",
                         "contourOuterSprite", "contourEdgeSprite", "contourConcaveSprite",
                         "lightOuterTopLeftSprite", "lightOuterTopRightSprite", "lightOuterBottomLeftSprite",
                         "lightOuterBottomRightSprite", "lightConcaveTopLeftSprite", "lightConcaveTopRightSprite",
                         "lightConcaveBottomLeftSprite", "lightConcaveBottomRightSprite", "lightEdgeTopSprite",
                         "lightEdgeBottomSprite", "lightEdgeLeftSprite", "lightEdgeRightSprite"
                     })
            {
                sprites[field] = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.one * 0.5f);
                var property = serialized.FindProperty(field);
                Assert.IsNotNull(property, $"the config has a serialized field '{field}'");
                property.objectReferenceValue = sprites[field];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return sprites;
        }

        /// <summary>Destroys the sprites of <see cref="AssignQuarterSprites"/> and their shared texture.</summary>
        private static void DestroySprites(Dictionary<string, Sprite> sprites)
        {
            Texture2D texture = null;
            foreach (var sprite in sprites.Values)
            {
                texture = sprite.texture;
                Object.DestroyImmediate(sprite);
            }

            Object.DestroyImmediate(texture);
        }

        private static void SetAlpha(SerializedObject serialized, string field, float alpha)
        {
            var property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"the config has a serialized field '{field}'");
            var color = property.colorValue;
            color.a = alpha;
            property.colorValue = color;
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
