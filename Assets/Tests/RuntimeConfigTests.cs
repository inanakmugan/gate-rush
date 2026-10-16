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

        [Test]
        public void Problems_DefaultMenuValues_ReportNothingAboutThem()
        {
            var problems = config.Problems();

            // Every message about the menu, the HUD's menu button and the
            // result panel's new line names one of these.
            Assert.That(problems, Has.None.Contains("Menu "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Level Select "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Level Tile "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Level Tick "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Result "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("is empty"), string.Join(" | ", problems));
        }

        [Test]
        public void Defaults_EndOfSetTexts_AreTheSpecified()
        {
            Assert.AreEqual("All Levels Played", config.AllDoneTitle);
            Assert.AreEqual("Thanks for playing!", config.AllDoneMessage);
        }

        [TestCase("gameTitle", "Game Title")]
        [TestCase("playLabel", "Play Label")]
        [TestCase("levelsLabel", "Levels Label")]
        [TestCase("soundOnLabel", "Sound On Label")]
        [TestCase("soundOffLabel", "Sound Off Label")]
        [TestCase("levelSelectTitle", "Level Select Title")]
        [TestCase("backLabel", "Back Label")]
        [TestCase("menuLabel", "Menu Label")]
        [TestCase("allDoneMessage", "All Done Message")]
        public void Problems_EmptyNewText_IsReportedByName(string field, string shownName)
        {
            SetString(field, "  ");

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} is empty"), string.Join(" | ", problems));
        }

        [TestCase("menuTitleHeightUnits", "Menu Title Height Units")]
        [TestCase("menuTitleWidthUnits", "Menu Title Width Units")]
        [TestCase("menuTitleFontSize", "Menu Title Font Size")]
        [TestCase("levelSelectTitleFontSize", "Level Select Title Font Size")]
        [TestCase("menuButtonCornerUnits", "Menu Button Corner Units")]
        [TestCase("menuButtonFontSize", "Menu Button Font Size")]
        [TestCase("levelTileSizeUnits", "Level Tile Size Units")]
        [TestCase("levelTileCornerUnits", "Level Tile Corner Units")]
        [TestCase("levelTileFontSize", "Level Tile Font Size")]
        [TestCase("levelTickSizeUnits", "Level Tick Size Units")]
        [TestCase("levelTickThicknessUnits", "Level Tick Thickness Units")]
        [TestCase("resultMessageHeightUnits", "Result Message Height Units")]
        [TestCase("resultMessageFontSize", "Result Message Font Size")]
        public void Problems_NonPositiveNewSize_IsReportedByName(string field, string shownName)
        {
            SetFloat(field, 0f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be positive"), string.Join(" | ", problems));
        }

        [TestCase("menuButtonSizeUnits", "Menu Button Size Units")]
        [TestCase("hudMenuBarSizeUnits", "Hud Menu Bar Size Units")]
        public void Problems_NonPositiveNewVectorSize_IsReportedByName(string field, string shownName)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).vector2Value = new Vector2(0f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be positive"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NoLevelSelectColumns_IsReportedByName()
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("levelSelectColumns").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Level Select Columns must be at least 1"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_ResultPanelTooShortForTwoButtonRows_IsReportedByName()
        {
            // One button row's height: room for the buttons the panel had
            // before Menu got a row of its own, and no more.
            var serialized = new SerializedObject(config);
            var buttonHeight = serialized.FindProperty("resultButtonSizeUnits").vector2Value.y;
            var panel = serialized.FindProperty("resultPanelSizeUnits");
            panel.vector2Value = new Vector2(panel.vector2Value.x, buttonHeight);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("two rows of them"), string.Join(" | ", problems));
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
            Assert.That(problems, Has.None.Contains("Menu Notice "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("Menu Wide "), string.Join(" | ", problems));
            Assert.That(problems, Has.None.Contains("is empty"), string.Join(" | ", problems));
        }

        [Test]
        public void Defaults_MetaTexts_AreTheSpecified()
        {
            Assert.AreEqual("Leave level?", config.LeaveLevelTitle);
            Assert.AreEqual("Restart level?", config.RestartLevelTitle);
            Assert.AreEqual("You will lose a life.", config.LoseLifeMessage);
            Assert.AreEqual("Lose a life", config.LoseLifeLabel);
            Assert.AreEqual("Keep playing", config.KeepPlayingLabel);
            Assert.AreEqual("No lives left", config.NoLivesTitle);
            Assert.AreEqual("Free Play", config.FreePlayLabel);
            Assert.AreEqual("All levels played. Play any level from Settings.", config.CampaignDoneNotice);
            Assert.AreEqual("Settings", config.SettingsLabel);
        }

        [TestCase("settingsLabel", "Settings Label")]
        [TestCase("settingsTitle", "Settings Title")]
        [TestCase("freePlayLabel", "Free Play Label")]
        [TestCase("campaignDoneNotice", "Campaign Done Notice")]
        [TestCase("noLivesTitle", "No Lives Title")]
        [TestCase("leaveLevelTitle", "Leave Level Title")]
        [TestCase("restartLevelTitle", "Restart Level Title")]
        [TestCase("loseLifeMessage", "Lose Life Message")]
        [TestCase("loseLifeLabel", "Lose Life Label")]
        [TestCase("keepPlayingLabel", "Keep Playing Label")]
        [TestCase("goldFormat", "Gold Format")]
        [TestCase("livesFullFormat", "Lives Full Format")]
        [TestCase("livesWaitingFormat", "Lives Waiting Format")]
        [TestCase("goldEarnedFormat", "Gold Earned Format")]
        [TestCase("nextLifeFormat", "Next Life Format")]
        [TestCase("refillFormat", "Refill Format")]
        public void Problems_EmptyMetaText_IsReportedByName(string field, string shownName)
        {
            SetString(field, "  ");

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} is empty"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_EmptyMetaSeparator_IsReportedByName()
        {
            SetString("metaSeparator", string.Empty);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Meta Separator is empty"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_MetaSeparatorOfSpacesOnly_IsAllowed()
        {
            SetString("metaSeparator", "   ");

            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Meta Separator"), string.Join(" | ", problems));
        }

        // Each format is given one placeholder more than it is formatted with.
        [TestCase("goldFormat", "Gold Format", "{1}")]
        [TestCase("livesFullFormat", "Lives Full Format", "{0}/{2}")]
        [TestCase("livesWaitingFormat", "Lives Waiting Format", "{0}/{1} {3}")]
        [TestCase("goldEarnedFormat", "Gold Earned Format", "+{1}")]
        [TestCase("nextLifeFormat", "Next Life Format", "{1}")]
        [TestCase("refillFormat", "Refill Format", "({1})")]
        [TestCase("goldFormat", "Gold Format", "Gold {0")]
        public void Problems_UnusableMetaFormat_IsReportedByName(string field, string shownName, string format)
        {
            SetString(field, format);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be a format"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_LivesWaitingFormatUsingEveryPlaceholder_IsAllowed()
        {
            SetString("livesWaitingFormat", "{2} {1} {0}");

            var problems = config.Problems();

            Assert.That(problems, Has.None.Contains("Lives Waiting Format"), string.Join(" | ", problems));
        }

        [TestCase("menuMetaHeightUnits", "Menu Meta Height Units")]
        [TestCase("menuMetaFontSize", "Menu Meta Font Size")]
        [TestCase("menuNoticeHeightUnits", "Menu Notice Height Units")]
        [TestCase("menuNoticeFontSize", "Menu Notice Font Size")]
        [TestCase("hudMetaHeightUnits", "Hud Meta Height Units")]
        [TestCase("hudMetaFontSize", "Hud Meta Font Size")]
        public void Problems_NonPositiveMetaSize_IsReportedByName(string field, string shownName)
        {
            SetFloat(field, 0f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be positive"), string.Join(" | ", problems));
        }

        [TestCase("menuWideButtonSizeUnits", "Menu Wide Button Size Units")]
        [TestCase("confirmPanelSizeUnits", "Confirm Panel Size Units")]
        public void Problems_NonPositiveMetaVectorSize_IsReportedByName(string field, string shownName)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).vector2Value = new Vector2(0f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} must be positive"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_NegativeHudMetaGap_IsReportedByName()
        {
            SetFloat("hudMetaGapUnits", -1f);

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Hud Meta Gap Units must be at least 0"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_ConfirmPanelTooShort_IsReportedByName()
        {
            // Two stacked buttons' height alone: no room for the line, the
            // title or the padding.
            var serialized = new SerializedObject(config);
            var buttonHeight = serialized.FindProperty("menuButtonSizeUnits").vector2Value.y;
            var panel = serialized.FindProperty("confirmPanelSizeUnits");
            panel.vector2Value = new Vector2(panel.vector2Value.x, 2f * buttonHeight);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("Confirm Panel Size Units must hold"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_ResultPanelWithoutRoomForBothLines_IsReportedByName()
        {
            // Exactly the height that held the title and one line before the
            // gold line: no longer enough for the gold and the closing message.
            var serialized = new SerializedObject(config);
            var button = serialized.FindProperty("resultButtonSizeUnits").vector2Value;
            var gap = serialized.FindProperty("resultButtonGapUnits").floatValue;
            var padding = serialized.FindProperty("resultPaddingUnits").floatValue;
            var line = serialized.FindProperty("resultMessageHeightUnits").floatValue;
            var panel = serialized.FindProperty("resultPanelSizeUnits");
            panel.vector2Value = new Vector2(panel.vector2Value.x, 2f * button.y + gap + 3f * padding + 1.5f * line);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains("two rows of them"), string.Join(" | ", problems));
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
