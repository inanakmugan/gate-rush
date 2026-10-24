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
    /// Joker Icons, an icon size that is not positive or larger than the
    /// illustration area, and a transparent icon colour are each reported by
    /// name; the icon is 360 units, leaving 216 clear above and below it. Module 46: the default palette and
    /// board colours are the measured ones; translucent faces and backdrops,
    /// highlight alphas past 1, shadow and highlight orders out of place, and
    /// unassigned lit-body sprites and materials are each reported by name;
    /// each quarter layer resolves to its own sprite — the lit, highlight and
    /// shadow layers by the quarter's place, the reversed lit layer by the
    /// opposite place — and to its own material; the shadow's margin is read
    /// from its sprite.
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
        public void NewJokerIcon_Default_Is360WithA216UnitGapAboveAndBelow()
        {
            var size = config.NewJokerIconSizeUnits;
            var gap = config.NewJokerIconGapUnits;

            Assert.AreEqual(360f, size);
            Assert.AreEqual((config.IntroIllustrationSizeUnits.y - size) * 0.5f + config.IntroSpacingUnits, gap, "the area's margin around the centred icon plus the row spacing");
            Assert.AreEqual(216f, gap);
        }

        [Test]
        public void Problems_NewJokerIconLargerThanTheIllustrationArea_IsReportedByName()
        {
            var area = config.IntroIllustrationSizeUnits;
            SetFloat("newJokerIconSizeUnits", Mathf.Min(area.x, area.y) + 1f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("New Joker Icon Size Units must be at most the shorter side"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NewJokerIconFillingTheIllustrationArea_IsNotReported()
        {
            var area = config.IntroIllustrationSizeUnits;
            SetFloat("newJokerIconSizeUnits", Mathf.Min(area.x, area.y));

            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("New Joker Icon Size Units"), string.Join(" | ", problems));
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
            // Below half a cell on its own, but with the rim the inner block
            // would vanish.
            SetFloat("layerInsetCells", 0.49f);

            var problems = config.Problems();

            Assert.That(
                problems, Has.Some.Contains("Layer Inset Cells and Layer Edge Cells together must be below 0.5"),
                string.Join(" | ", problems));
        }

        [TestCase(1.5f)]
        [TestCase(-0.1f)]
        public void Problems_LayerEdgeDarkenOutOfRange_IsReportedByName(float value)
        {
            SetFloat("layerEdgeDarken", value);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Layer Edge Darken must be from 0 to 1"), string.Join(" | ", problems));
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
        public void LayerEdgeFill_AnOuterColour_IsItDarkenedByTheEdgeShare()
        {
            var outer = new Color(0.8f, 0.4f, 0.2f, 1f);
            var serialized = new SerializedObject(config);
            var edgeShare = serialized.FindProperty("layerEdgeDarken").floatValue;

            var edgeFill = config.LayerEdgeFill(outer);

            Assert.AreEqual(new Color(outer.r * edgeShare, outer.g * edgeShare, outer.b * edgeShare, 1f), edgeFill);
            Assert.Less(edgeFill.r, outer.r, "the rim is darker than the surround, which is the outer colour itself");
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
        [TestCase("generatorPreviewRimOrder", "elevatorHandleOrder")]
        [TestCase("generatorPreviewRimOrder", "generatorPreviewOrder")]
        public void Problems_GeneratorPreviewRimOrderNotInItsPlace_IsReportedByName(string field, string sameAs)
        {
            SetOrderEqualTo(field, sameAs);

            var problems = config.Problems();

            Assert.That(
                problems, Has.Some.Contains("Generator Preview Rim Order must be above Floor Order and Elevator Handle Order"),
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

        // Module 47, pass 3: the card shrinks a little as it closes, as every
        // overlay's close does.
        [TestCase(0.7f)]
        [TestCase(1.1f)]
        public void Problems_IntroCloseScaleOutOfRange_IsReportedByName(float value)
        {
            SetFloat("introCloseScale", value);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Intro Close Scale"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_DefaultIntroCloseScale_ReportsNothingAboutIt()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Intro Close Scale"), string.Join(" | ", problems));
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

        // ----- Module 47: the debris, the joker break, the pool, the elevator --

        [TestCase("Shard Color")]
        [TestCase("Break Flash")]
        [TestCase("Break Cube")]
        [TestCase("Break Sparkle")]
        [TestCase("Debris Pool")]
        [TestCase("Elevator Sink")]
        public void Problems_DefaultEffectValues_ReportNothingAboutThem(string shownName)
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains(shownName), string.Join(" | ", problems));
        }

        [TestCase("breakFlashStrength", 1.5f, "Break Flash Strength")]
        [TestCase("breakFlashSeconds", 0f, "Break Flash Seconds")]
        [TestCase("breakFlashSeconds", 3f, "Break Flash Seconds")]
        [TestCase("breakCubeSpreadDegrees", 90f, "Break Cube Spread Degrees")]
        [TestCase("breakCubeMaxDelaySeconds", -1f, "Break Cube Max Delay Seconds")]
        [TestCase("breakSparkleSeconds", 0f, "Break Sparkle Seconds")]
        [TestCase("breakSparkleEndScale", 1.5f, "Break Sparkle End Scale")]
        [TestCase("elevatorSinkSeconds", 0f, "Elevator Sink Seconds")]
        [TestCase("elevatorSinkEndScale", 1f, "Elevator Sink End Scale")]
        public void Problems_EffectFloatOutOfRange_IsReportedByName(string field, float value, string shownName)
        {
            SetFloat(field, value);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(shownName), string.Join(" | ", problems));
        }

        [TestCase("breakCubeCountPerCell", 0, "Break Cube Count Per Cell")]
        [TestCase("breakCubeCountPerCell", 9, "Break Cube Count Per Cell")]
        [TestCase("breakCubeCap", 41, "Break Cube Cap")]
        [TestCase("breakCubeMoveCap", 10, "Break Cube Move Cap")]
        [TestCase("breakCubeMoveCap", 129, "Break Cube Move Cap")]
        [TestCase("breakSparkleCount", -1, "Break Sparkle Count")]
        [TestCase("breakSparkleCount", 9, "Break Sparkle Count")]
        [TestCase("breakSparkleMoveCap", 33, "Break Sparkle Move Cap")]
        [TestCase("debrisPoolSize", 50, "Debris Pool Size")]
        [TestCase("debrisPoolSize", 513, "Debris Pool Size")]
        public void Problems_EffectCountOutOfRange_IsReportedByName(string field, int value, string shownName)
        {
            SetInt(field, value);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(shownName), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_CubeHighlightsOnWithTheDefaultPool_ReportsThePoolTooSmall()
        {
            // A highlighted cube takes two renderers, so the pool must hold
            // twice the cubes.
            var serialized = new SerializedObject(config);
            serialized.FindProperty("drawCubeHighlights").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(config.DebrisPoolNeed, Is.GreaterThan(config.DebrisPoolSize));
            Assert.That(problems, Has.Some.Contains("Debris Pool Size"), string.Join(" | ", problems));
        }

        [Test]
        public void DebrisPoolNeed_Default_IsOneExitOneJokerClearOneShardBurstAndAGlow()
        {
            var serialized = new SerializedObject(config);
            int Int(string field) => serialized.FindProperty(field).intValue;

            var need = config.DebrisPoolNeed;

            Assert.AreEqual(Int("cubeCap") + Int("breakCubeMoveCap") + Int("breakSparkleMoveCap") + Int("shardCap") + 1, need);
            Assert.That(config.DebrisPoolSize, Is.GreaterThanOrEqualTo(need), "the default pool holds them");
        }

        [Test]
        public void CreateBreakCubeBurst_Default_IsTheBreaksOwnCountCapTravelAndSpreadWithTheExitCubesSize()
        {
            var serialized = new SerializedObject(config);

            var settings = config.CreateBreakCubeBurst();

            Assert.AreEqual(serialized.FindProperty("breakCubeCountPerCell").intValue, settings.CountPerArea);
            Assert.AreEqual(serialized.FindProperty("breakCubeCap").intValue, settings.Cap);
            Assert.AreEqual(serialized.FindProperty("breakCubeTravelCells").vector2Value.x, settings.TravelMinCells);
            Assert.AreEqual(serialized.FindProperty("breakCubeTravelCells").vector2Value.y, settings.TravelMaxCells);
            Assert.AreEqual(serialized.FindProperty("breakCubeSpreadDegrees").floatValue, settings.SpreadDegrees);
            Assert.AreEqual(serialized.FindProperty("cubeSizeCells").vector2Value.x, settings.SizeMinCells);
            Assert.AreEqual(serialized.FindProperty("cubeSizeCells").vector2Value.y, settings.SizeMaxCells);
        }

        [Test]
        public void CreateBreakSparkleBurst_Default_CapsAtTheSparkleCountAndNeverTurns()
        {
            var serialized = new SerializedObject(config);

            var settings = config.CreateBreakSparkleBurst();

            Assert.AreEqual(serialized.FindProperty("breakSparkleCount").intValue, settings.Cap);
            Assert.AreEqual(0f, settings.SpinDegrees);
        }

        [Test]
        public void ShardSpriteAt_EachIndex_CyclesThroughTheThreeShards()
        {
            var texture = new Texture2D(2, 2);
            var sprites = new Sprite[RuntimeConfig.ShardSpriteCount];
            try
            {
                var fields = new[] { "shardSprite", "shard2Sprite", "shard3Sprite" };
                for (var i = 0; i < sprites.Length; i++)
                {
                    sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.zero);
                    SetObject(fields[i], sprites[i]);
                }

                for (var index = 0; index < 2 * sprites.Length; index++)
                {
                    Assert.AreSame(sprites[index % sprites.Length], config.ShardSpriteAt(index), $"piece {index}");
                }
            }
            finally
            {
                foreach (var sprite in sprites)
                {
                    Object.DestroyImmediate(sprite);
                }

                Object.DestroyImmediate(texture);
            }
        }

        private void SetInt(string field, int value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
                (Actual: config.FrameColor, Expected: new Color32(0x48, 0x3C, 0xA8, 0xFF), Name: "frame, the reference game's (Module 46, pass 2)"),
                (Actual: config.FloorColor, Expected: new Color32(0x31, 0x18, 0x5F, 0xFF), Name: "floor"),
                (Actual: config.BackgroundTop, Expected: new Color32(0x2A, 0x24, 0x60, 0xFF), Name: "background top, the reference game's"),
                (Actual: config.BackgroundBottom, Expected: new Color32(0x16, 0x12, 0x36, 0xFF), Name: "background bottom, darker"),
            };

            foreach (var colour in colours)
            {
                Assert.AreEqual(colour.Expected, (Color32)colour.Actual, colour.Name);
            }
        }

        [Test]
        public void Defaults_FrameAndBackground_TheFrameIsLighterThanTheBackgroundsTopAndTheTopLighterThanTheBottom()
        {
            // So the board's edge reads against the background (Module 46, pass 2).
            Assert.Greater(config.FrameColor.grayscale, config.BackgroundTop.grayscale, "the frame over the background's top");
            Assert.Greater(config.BackgroundTop.grayscale, config.BackgroundBottom.grayscale, "the gradient darkens downward");
        }

        [Test]
        public void Defaults_Key_IsDrawnAtTheScriptsSizeAndLaidOutAtIt()
        {
            // The key is painted at 1.2 times the script's bow and shaft; the
            // board draws it at 1 / 1.2, and lays it out 0.6 of a cell long.
            Assert.AreEqual(1f / 1.2f, config.KeyScale, 1e-6f, "Key Scale");
            Assert.AreEqual(0.6f, config.KeySizeCells, 1e-6f, "Key Size Cells");
            Assert.That(config.Problems(), Has.None.Contains("Key Scale"), "the default passes");
        }

        [Test]
        public void Defaults_MechanicColours_AreTheReferenceScripts()
        {
            // mechanics_reference.py's colours, one per painted or tinted part.
            var colours = new[]
            {
                (Actual: config.IceTintColor, Expected: new Color32(0xD8, 0xF3, 0xFF, 0xFF), Name: "ice tint"),
                (Actual: config.IceRimColor, Expected: new Color32(0xE6, 0xF7, 0xFF, 0xFF), Name: "ice rim"),
                (Actual: config.CountBadgeColor, Expected: new Color32(0xE5, 0x39, 0x35, 0xFF), Name: "badge face"),
                (Actual: config.CountBadgeRimColor, Expected: new Color32(0xFF, 0xC5, 0x31, 0xFF), Name: "badge rim"),
                (Actual: config.ChainSilverColor, Expected: new Color32(0xBF, 0xC3, 0xDA, 0xFF), Name: "chain and shackle"),
                (Actual: config.LockGoldColor, Expected: new Color32(0xFF, 0xC5, 0x31, 0xFF), Name: "padlock and key"),
                (Actual: config.KeyholeColor, Expected: new Color32(0x6B, 0x43, 0x00, 0xFF), Name: "keyhole"),
                // time_bonus_reference.py, option D: the green rim and hands.
                (Actual: config.CoinColor, Expected: new Color32(0x2B, 0xC2, 0x3A, 0xFF), Name: "coin rim"),
                (Actual: config.CoinFaceColor, Expected: new Color32(0xFF, 0xF4, 0xC4, 0xFF), Name: "coin face"),
                (Actual: config.CoinHandColor, Expected: new Color32(0x1E, 0x5A, 0x12, 0xFF), Name: "coin hands"),
                (Actual: config.LabelOutlineColor, Expected: new Color32(0x2A, 0x16, 0x50, 0xFF), Name: "the seconds' outline"),
                (Actual: config.ShutterRimColor, Expected: new Color32(0xF4, 0xA3, 0x00, 0xFF), Name: "shutter rim"),
                (Actual: config.ShutterCylinderColor, Expected: new Color32(0x4B, 0x3B, 0xC0, 0xFF), Name: "shutter slats"),
                (Actual: config.MachineMetalColor, Expected: new Color32(0x8E, 0x9A, 0xC0, 0xFF), Name: "machine metal"),
                (Actual: config.MachineGlassColor, Expected: new Color32(0x0B, 0x1E, 0x5E, 0xFF), Name: "machine glass"),
                (Actual: config.MachineRivetColor, Expected: new Color32(0xC7, 0xCE, 0xE6, 0xFF), Name: "rivets"),
                (Actual: config.MachineButtonColor, Expected: new Color32(0xFF, 0x7A, 0x00, 0xFF), Name: "button"),
                (Actual: config.MachineCountColor, Expected: new Color32(0x7F, 0xE3, 0xFF, 0xFF), Name: "screen count"),
                (Actual: config.ElevatorRimColor, Expected: new Color32(0xFB, 0x6F, 0x00, 0xFF), Name: "elevator rim"),
                (Actual: config.HazardColor, Expected: new Color32(0xFF, 0xC5, 0x31, 0xFF), Name: "hazard light"),
                (Actual: config.HazardDarkColor, Expected: new Color32(0x3A, 0x23, 0x70, 0xFF), Name: "hazard dark"),
                (Actual: config.ElevatorRecessColor, Expected: new Color32(0x12, 0x0A, 0x2E, 0xFF), Name: "recess"),
                (Actual: config.ElevatorPanelColor, Expected: new Color32(0x5B, 0x45, 0xB0, 0xFF), Name: "doors"),
                (Actual: config.ElevatorHandleColor, Expected: new Color32(0xD7, 0xDA, 0xEC, 0xFF), Name: "handles"),
            };

            foreach (var colour in colours)
            {
                Assert.AreEqual(colour.Expected, (Color32)colour.Actual, colour.Name);
            }

            Assert.AreEqual(Color.white, config.CountTextColor, "the digits are white");
        }

        [Test]
        public void Problems_DefaultMechanicValues_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("must be at least 0 and below 0.5; run"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Badge Height Cells and Badge Digit Width Cells"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Machine Screen Count Share"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Machine Miniature Fill Share"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Machine Count Room"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Elevator Rim Order"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Machine Order, Machine Screen Order"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Ice Order must be above"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Key Gem Highlight Order"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Font Size must be positive"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Label Shadow Softness"), string.Join(" | ", problems));
        }

        [TestCase("lockGoldColor", "Lock Gold Color")]
        [TestCase("iceTintColor", "Ice Tint Color")]
        [TestCase("countBadgeColor", "Count Badge Color")]
        [TestCase("elevatorPanelColor", "Elevator Panel Color")]
        public void Problems_TranslucentMechanicColour_IsReportedByName(string field, string shownName)
        {
            SetAlpha(new SerializedObject(config), field, 0.5f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be opaque"), string.Join(" | ", problems));
        }

        [TestCase("iceOrder", "glossOrder", "Ice Order must be above Gloss Order")]
        [TestCase("gateIceOrder", "frameShineOrder", "Gate Ice Order above Frame Shine Order")]
        [TestCase("badgeHighlightOrder", "labelOrder", "Badge Highlight Order above Badge Order and below Label Order")]
        [TestCase("elevatorHazardOrder", "elevatorRimOrder", "Elevator Rim Order, Elevator Hazard Order")]
        [TestCase("miniatureStudOrder", "miniatureOrder", "Miniature Order, Miniature Stud Order")]
        public void Problems_MechanicOrderOutOfPlace_IsReportedByName(string field, string sameAs, string complaint)
        {
            SetOrderEqualTo(field, sameAs);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(complaint), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_UnassignedMechanicSpritesAndShadowMaterial_AreReportedByName()
        {
            var problems = config.Problems();

            foreach (var shownName in new[]
                     {
                         "Ice Outer Top Left Sprite", "Ice Fill Sprite", "Ice Streaks Sprite", "Ice Streaks Wide Sprite",
                         "Badge Rim Sprite", "Badge Face Sprite", "Badge Highlight Sprite", "Lock Sprite", "Gold Key Sprite",
                         "Gem Sprite", "Gem Highlight Sprite", "Bonus Coin Sprite", "One Way Arrow Sprite",
                         "One Way Arrow Vertical Highlight Sprite", "Shutter Rim Sprite", "Shutter Cylinders Sprite",
                         "Machine Body Sprite", "Machine Screen Sprite", "Machine Vent Sprite", "Machine Button Sprite",
                         "Elevator Rim Sprite", "Elevator Hazard Left Sprite", "Elevator Hazard Right Sprite",
                         "Elevator Door Sprite", "Elevator Handle Sprite", "Label Shadow Material", "Label Outline Font",
                         "Label Outline Material"
                     })
            {
                Assert.That(problems, Has.Some.Contains($"{shownName} is not assigned"), shownName);
            }
        }

        [Test]
        public void Defaults_TimeBonusMark_AreTheReferencesMeasuresAtACellOf128Pixels()
        {
            // time_bonus_reference_b.py: a coin of radius 34, a gap of 4, the
            // seconds at the count digits' 34 pixels, 2 low, with a 3-pixel
            // stroke.
            Assert.AreEqual(68f / 128f, config.BonusCoinCells, 1e-6f, "the coin's diameter");
            Assert.AreEqual(4f / 128f, config.TimeBonusLabelGapCells, 1e-6f, "the gap");
            Assert.AreEqual(2f / 128f, config.TimeBonusLabelDropCells, 1e-6f, "how low the seconds sit");
            Assert.AreEqual(2.65625f, config.CountFontSize, 1e-6f, "the seconds at the count digits' 34 pixels");
            Assert.AreEqual(3f / 128f, config.LabelOutlineStrokeCells, 1e-6f, "the stroke");
            Assert.AreEqual(0.14f, config.MarkFitMarginCells, 1e-6f, "the key's margin");
        }

        [Test]
        public void TimeBonusMarkWidthCells_IsTheCoinTheGapAndTheSecondsAdvance_WithNoPlate()
        {
            var text = config.TimeBonusMarkText(5);

            var width = config.TimeBonusMarkWidthCells(text);

            Assert.AreEqual(
                config.BonusCoinCells + config.TimeBonusLabelGapCells + config.TimeBonusLabelAdvanceCells(text), width, 1e-6f,
                "the outline not counted");
        }

        [TestCase(5)]
        [TestCase(10)]
        [TestCase(RuntimeConfig.WidestBonusSecondsChecked)]
        public void TimeBonusCoinAndLabel_AnyValue_AreCentredOnTheAnchorWithTheGapBetween(int seconds)
        {
            var text = config.TimeBonusMarkText(seconds);

            var coin = config.TimeBonusCoinCenterCells(text);
            var start = config.TimeBonusLabelStartCells(text);

            var coinLeft = coin - config.BonusCoinCells / 2f;
            var labelEnd = start + config.TimeBonusLabelAdvanceCells(text);
            Assert.AreEqual(-coinLeft, labelEnd, 1e-6f, "the coin's left edge and the advance's end either side of the anchor");
            Assert.AreEqual(config.TimeBonusLabelGapCells, start - (coin + config.BonusCoinCells / 2f), 1e-6f, "the gap after the coin");
        }

        [Test]
        public void TimeBonusCoinCenterCells_LongerText_MovesTheCoinLeft()
        {
            var five = config.TimeBonusCoinCenterCells(config.TimeBonusMarkText(5));

            var ninetyNine = config.TimeBonusCoinCenterCells(config.TimeBonusMarkText(RuntimeConfig.WidestBonusSecondsChecked));

            Assert.Less(ninetyNine, five, "the group recentres for each value");
        }

        [Test]
        public void TimeBonusLabelAdvanceCells_LabelFont_IsTheGlyphsAdvancesAtTheCountDigitsSize()
        {
            // Lilita One's "+", "1" and "0" advance 42.734375, 28.828125 and
            // 45.375 at a sampling size of 71; a label of Count Font Size has
            // an em of a tenth of it, in cells.
            const int samplingPointSize = 71;
            const int atlasPadding = 17;
            const int atlasSize = 512;
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(LilitaOnePath);
            Assert.IsNotNull(ttf, LilitaOnePath);
            var font = TMPro.TMP_FontAsset.CreateFontAsset(
                ttf, samplingPointSize, atlasPadding, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, atlasSize, atlasSize,
                TMPro.AtlasPopulationMode.Dynamic, false);
            try
            {
                Assert.IsTrue(font.TryAddCharacters("+10"), "the glyphs render");
                SetObject("labelFont", font);

                var advance = config.TimeBonusLabelAdvanceCells("+10");

                var expected = (42.734375f + 28.828125f + 45.375f) / samplingPointSize * config.CountFontSize
                               * RuntimeConfig.WorldUnitsPerFontSize / config.CellSize;
                Assert.AreEqual(expected, advance, 0.5f / 128f, "56 pixels of 128, within half a pixel");
            }
            finally
            {
                Object.DestroyImmediate(font.material);
                Object.DestroyImmediate(font.atlasTexture);
                Object.DestroyImmediate(font);
            }
        }

        [Test]
        public void TimeBonusLabelAdvanceCells_NoFont_IsTheCharacterWidthPerCharacter()
        {
            var advance = config.TimeBonusLabelAdvanceCells("+10");

            Assert.AreEqual(3f * 19f / 128f, advance, 1e-6f);
        }

        private const string LilitaOnePath = "Assets/ThirdParty/Fonts/LilitaOne/LilitaOne-Regular.ttf";

        [Test]
        public void TimeBonusMarkScale_TwoCellsOfRoom_IsFullSize()
        {
            var text = config.TimeBonusMarkText(RuntimeConfig.WidestBonusSecondsChecked);

            var scale = config.TimeBonusMarkScale(2f, MarkCrowding.Roomy, text);

            Assert.AreEqual(1f, scale);
        }

        [TestCase(5)]
        [TestCase(10)]
        [TestCase(RuntimeConfig.WidestBonusSecondsChecked)]
        public void TimeBonusMarkScale_OneCellOfRoom_ShrinksTheGroupToFitInsideTheMargins(int seconds)
        {
            var text = config.TimeBonusMarkText(seconds);
            var half = MarkLayout.SharedMarkRoomCells / 2f - config.MarkFitMarginCells;

            var scale = config.TimeBonusMarkScale(MarkLayout.SharedMarkRoomCells, MarkCrowding.Roomy, text);

            Assert.Less(scale, 1f, "the full-size group is wider than a cell");
            var reach = config.TimeBonusMarkWidthCells(text) / 2f + config.LabelOutlineStrokeCells;
            Assert.AreEqual(reach, config.TimeBonusMarkReachCells(text), 1e-6f, "half the group, the outline at both ends");
            Assert.AreEqual(half, reach * scale, 1e-6f, "both sides touch the margin");
        }

        [Test]
        public void TimeBonusMarkScale_CrowdedPair_IsTheCrowdedScaleTimesTheFit()
        {
            var text = config.TimeBonusMarkText(10);
            var fitted = config.TimeBonusMarkScale(MarkLayout.SharedMarkRoomCells, MarkCrowding.Roomy, text);

            var crowded = config.TimeBonusMarkScale(MarkLayout.SharedMarkRoomCells, MarkCrowding.CrowdedPair, text);

            Assert.AreEqual(config.CrowdedMarkScale * fitted, crowded, 1e-6f);
        }

        [Test]
        public void Problems_DefaultTimeBonusMark_ReportNothingAboutIt()
        {
            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("time-bonus"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Mark Fit Margin Cells"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Label Outline Characters"), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Label Outline Atlas Padding"), string.Join(" | ", problems));
            foreach (var field in new[]
                     {
                         "Bonus Coin Cells", "Time Bonus Mark Character Width Cells", "Label Outline Stroke Cells",
                         "Time Bonus Label Gap Cells", "Label Outline Color"
                     })
            {
                Assert.That(problems, Has.None.Contains($": {field} must"), field);
            }
        }

        [TestCase(0.5f)]
        [TestCase(-0.01f)]
        public void Problems_MarkFitMarginOutOfRange_IsReported(float margin)
        {
            SetFloat("markFitMarginCells", margin);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Mark Fit Margin Cells must be at least 0 and below half a cell"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_OutlineCharactersMissingADigit_NamesIt()
        {
            SetString("labelOutlineCharacters", "+012345678");

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Label Outline Characters must hold every character a time-bonus mark shows; it lacks \"9\""), string.Join(" | ", problems));
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
        public void Problems_UnassignedLitBodySpritesAndMaterials_AreReportedByName()
        {
            var problems = config.Problems();

            foreach (var shownName in new[]
                     {
                         "Face Outer Top Left Sprite", "Face Outer Top Right Sprite", "Face Outer Bottom Left Sprite",
                         "Face Outer Bottom Right Sprite", "Face Concave Top Left Sprite", "Face Concave Top Right Sprite",
                         "Face Concave Bottom Left Sprite", "Face Concave Bottom Right Sprite", "Face Edge Top Sprite",
                         "Face Edge Bottom Sprite", "Face Edge Left Sprite", "Face Edge Right Sprite", "Face Fill Sprite",
                         "Body Outer Top Left Sprite", "Body Outer Top Right Sprite", "Body Outer Bottom Left Sprite",
                         "Body Outer Bottom Right Sprite", "Body Concave Top Left Sprite", "Body Concave Top Right Sprite",
                         "Body Concave Bottom Left Sprite", "Body Concave Bottom Right Sprite", "Body Edge Top Sprite",
                         "Body Edge Bottom Sprite", "Body Edge Left Sprite", "Body Edge Right Sprite", "Body Fill Sprite",
                         "Shadow Outer Top Left Sprite", "Shadow Outer Top Right Sprite", "Shadow Outer Bottom Left Sprite",
                         "Shadow Outer Bottom Right Sprite", "Shadow Concave Top Left Sprite", "Shadow Concave Top Right Sprite",
                         "Shadow Concave Bottom Left Sprite", "Shadow Concave Bottom Right Sprite", "Shadow Edge Top Sprite",
                         "Shadow Edge Bottom Sprite", "Shadow Edge Left Sprite", "Shadow Edge Right Sprite",
                         "Lit Material", "Specular Material", "Ui Lit Material", "Ui Specular Material",
                         "Gate Arrow Outline Sprite", "Single Stud Sprite", "Single Stud Gloss Sprite"
                     })
            {
                Assert.That(problems, Has.Some.Contains(shownName), shownName);
            }
        }

        [Test]
        public void QuarterSprite_Plain_IsThePlainSpriteForThePiece()
        {
            var sprites = AssignQuarterSprites();

            try
            {
                Assert.AreSame(sprites["quarterOuterSprite"], config.QuarterSprite(QuarterLayer.Plain, Tile(Quarter.BottomLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["quarterEdgeSprite"], config.QuarterSprite(QuarterLayer.Plain, Tile(Quarter.TopRight, QuarterPiece.EdgeAlongY)));
                Assert.AreSame(sprites["quarterFillSprite"], config.QuarterSprite(QuarterLayer.Plain, Tile(Quarter.TopRight, QuarterPiece.Fill)));
                Assert.AreSame(sprites["quarterConcaveSprite"], config.QuarterSprite(QuarterLayer.Plain, Tile(Quarter.TopLeft, QuarterPiece.ConcaveCorner)));
            }
            finally
            {
                DestroySprites(sprites);
            }
        }

        [Test]
        public void QuarterSprite_Lit_IsTheFaceForTheQuartersPlace()
        {
            var sprites = AssignQuarterSprites();

            try
            {
                Assert.AreSame(sprites["faceOuterTopLeftSprite"], config.QuarterSprite(QuarterLayer.Lit, Tile(Quarter.TopLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["faceOuterBottomRightSprite"], config.QuarterSprite(QuarterLayer.Lit, Tile(Quarter.BottomRight, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["faceConcaveTopRightSprite"], config.QuarterSprite(QuarterLayer.Lit, Tile(Quarter.TopRight, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["faceEdgeBottomSprite"], config.QuarterSprite(QuarterLayer.Lit, Tile(Quarter.BottomLeft, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["faceEdgeLeftSprite"], config.QuarterSprite(QuarterLayer.Lit, Tile(Quarter.TopLeft, QuarterPiece.EdgeAlongY)));
                Assert.AreSame(sprites["faceFillSprite"], config.QuarterSprite(QuarterLayer.Lit, Tile(Quarter.TopRight, QuarterPiece.Fill)), "the flat top's");
            }
            finally
            {
                DestroySprites(sprites);
            }
        }

        [Test]
        public void QuarterSprite_Shadow_IsTheShadowForTheQuartersPlaceAndNoneForAFill()
        {
            var sprites = AssignQuarterSprites();

            try
            {
                Assert.AreSame(sprites["shadowOuterBottomLeftSprite"], config.QuarterSprite(QuarterLayer.Shadow, Tile(Quarter.BottomLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["shadowConcaveBottomRightSprite"], config.QuarterSprite(QuarterLayer.Shadow, Tile(Quarter.BottomRight, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["shadowEdgeTopSprite"], config.QuarterSprite(QuarterLayer.Shadow, Tile(Quarter.TopRight, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["shadowEdgeRightSprite"], config.QuarterSprite(QuarterLayer.Shadow, Tile(Quarter.BottomRight, QuarterPiece.EdgeAlongY)));
                Assert.IsNull(config.QuarterSprite(QuarterLayer.Shadow, Tile(Quarter.TopRight, QuarterPiece.Fill)), "a fill casts no shadow past the shape");
            }
            finally
            {
                DestroySprites(sprites);
            }
        }

        [Test]
        public void QuarterMaterial_EachLayer_IsItsMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            var materials = new Dictionary<string, Material>();
            var serialized = new SerializedObject(config);
            foreach (var field in new[] { "spriteMaterial", "litMaterial", "specularMaterial", "uiLitMaterial", "uiSpecularMaterial" })
            {
                materials[field] = new Material(shader);
                serialized.FindProperty(field).objectReferenceValue = materials[field];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                Assert.AreSame(materials["spriteMaterial"], config.QuarterMaterial(QuarterLayer.Plain));
                Assert.AreSame(materials["spriteMaterial"], config.QuarterMaterial(QuarterLayer.Shadow));
                Assert.AreSame(materials["litMaterial"], config.QuarterMaterial(QuarterLayer.Lit));
                Assert.AreSame(materials["litMaterial"], config.QuarterMaterial(QuarterLayer.ReversedLit));
                Assert.AreSame(materials["specularMaterial"], config.QuarterMaterial(QuarterLayer.Body));
                Assert.IsNull(config.QuarterUiMaterial(QuarterLayer.Plain), "UI's default");
                Assert.IsNull(config.QuarterUiMaterial(QuarterLayer.Shadow), "UI's default");
                Assert.AreSame(materials["uiLitMaterial"], config.QuarterUiMaterial(QuarterLayer.Lit));
                Assert.AreSame(materials["uiLitMaterial"], config.QuarterUiMaterial(QuarterLayer.ReversedLit));
                Assert.AreSame(materials["uiSpecularMaterial"], config.QuarterUiMaterial(QuarterLayer.Body));
            }
            finally
            {
                foreach (var material in materials.Values)
                {
                    Object.DestroyImmediate(material);
                }
            }
        }

        [Test]
        public void ShadowMarginCells_ShadowWiderThanAQuarter_IsTheWideningAsAShareOfACell()
        {
            // A quarter is 64 pixels, half a 128-pixel cell; a shadow corner
            // widened by 14 reaches 14/128 of a cell past it.
            const int Quarter = 64;
            const int Widening = 14;
            var texture = new Texture2D(Quarter + Widening, Quarter + Widening);
            var fill = Sprite.Create(texture, new Rect(0f, 0f, Quarter, Quarter), Vector2.one * 0.5f);
            var shadow = Sprite.Create(texture, new Rect(0f, 0f, Quarter + Widening, Quarter + Widening), Vector2.one * 0.5f);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("quarterFillSprite").objectReferenceValue = fill;
            serialized.FindProperty("shadowOuterTopLeftSprite").objectReferenceValue = shadow;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                Assert.AreEqual(Widening / (2f * Quarter), config.ShadowMarginCells, 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(fill);
                Object.DestroyImmediate(shadow);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void QuarterSprite_Body_IsTheSpriteForTheQuartersPlace()
        {
            var sprites = AssignQuarterSprites();

            try
            {
                Assert.AreSame(sprites["bodyOuterTopLeftSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.TopLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["bodyOuterTopRightSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.TopRight, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["bodyOuterBottomLeftSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.BottomLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["bodyOuterBottomRightSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.BottomRight, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["bodyConcaveTopLeftSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.TopLeft, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["bodyConcaveTopRightSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.TopRight, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["bodyConcaveBottomLeftSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.BottomLeft, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["bodyConcaveBottomRightSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.BottomRight, QuarterPiece.ConcaveCorner)));

                // An edge along x faces up or down whichever side of its cell
                // it lies on along x; an edge along y faces left or right.
                Assert.AreSame(sprites["bodyEdgeTopSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.TopLeft, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["bodyEdgeTopSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.TopRight, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["bodyEdgeBottomSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.BottomRight, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["bodyEdgeLeftSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.BottomLeft, QuarterPiece.EdgeAlongY)));
                Assert.AreSame(sprites["bodyEdgeRightSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.TopRight, QuarterPiece.EdgeAlongY)));
                Assert.AreSame(sprites["bodyEdgeRightSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.BottomRight, QuarterPiece.EdgeAlongY)));

                Assert.AreSame(sprites["bodyFillSprite"], config.QuarterSprite(QuarterLayer.Body, Tile(Quarter.TopRight, QuarterPiece.Fill)), "the flat top's highlight");
            }
            finally
            {
                DestroySprites(sprites);
            }
        }

        [Test]
        public void QuarterSprite_ReversedLit_IsTheFaceForTheOppositePlace()
        {
            var sprites = AssignQuarterSprites();

            try
            {
                Assert.AreSame(sprites["faceOuterBottomRightSprite"], config.QuarterSprite(QuarterLayer.ReversedLit, Tile(Quarter.TopLeft, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["faceOuterTopLeftSprite"], config.QuarterSprite(QuarterLayer.ReversedLit, Tile(Quarter.BottomRight, QuarterPiece.OuterCorner)));
                Assert.AreSame(sprites["faceConcaveBottomLeftSprite"], config.QuarterSprite(QuarterLayer.ReversedLit, Tile(Quarter.TopRight, QuarterPiece.ConcaveCorner)));
                Assert.AreSame(sprites["faceEdgeBottomSprite"], config.QuarterSprite(QuarterLayer.ReversedLit, Tile(Quarter.TopLeft, QuarterPiece.EdgeAlongX)));
                Assert.AreSame(sprites["faceEdgeLeftSprite"], config.QuarterSprite(QuarterLayer.ReversedLit, Tile(Quarter.TopRight, QuarterPiece.EdgeAlongY)));
                Assert.AreSame(sprites["faceFillSprite"], config.QuarterSprite(QuarterLayer.ReversedLit, Tile(Quarter.TopRight, QuarterPiece.Fill)), "the flat top is the same either way");
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
                         "faceOuterTopLeftSprite", "faceOuterTopRightSprite", "faceOuterBottomLeftSprite",
                         "faceOuterBottomRightSprite", "faceConcaveTopLeftSprite", "faceConcaveTopRightSprite",
                         "faceConcaveBottomLeftSprite", "faceConcaveBottomRightSprite", "faceEdgeTopSprite",
                         "faceEdgeBottomSprite", "faceEdgeLeftSprite", "faceEdgeRightSprite", "faceFillSprite",
                         "bodyOuterTopLeftSprite", "bodyOuterTopRightSprite", "bodyOuterBottomLeftSprite",
                         "bodyOuterBottomRightSprite", "bodyConcaveTopLeftSprite", "bodyConcaveTopRightSprite",
                         "bodyConcaveBottomLeftSprite", "bodyConcaveBottomRightSprite", "bodyEdgeTopSprite",
                         "bodyEdgeBottomSprite", "bodyEdgeLeftSprite", "bodyEdgeRightSprite", "bodyFillSprite",
                         "shadowOuterTopLeftSprite", "shadowOuterTopRightSprite", "shadowOuterBottomLeftSprite",
                         "shadowOuterBottomRightSprite", "shadowConcaveTopLeftSprite", "shadowConcaveTopRightSprite",
                         "shadowConcaveBottomLeftSprite", "shadowConcaveBottomRightSprite", "shadowEdgeTopSprite",
                         "shadowEdgeBottomSprite", "shadowEdgeLeftSprite", "shadowEdgeRightSprite"
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
