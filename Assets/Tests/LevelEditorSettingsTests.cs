using GateRush.Editor;
using GateRush.Solver;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="LevelEditorSettings"/>'s defaults: the two solve
    /// budgets are D5's numbers, the time-budget formula is
    /// <c>base 10 + 3 per move, rounded to 5</c>, and the window-layout
    /// proportions and floors (docs/Modules/09a follow-up, item 4) match what
    /// today's window is tuned for. Module 20: the block mark values have
    /// their documented defaults, which <see cref="LevelEditorSettings.Problems"/>
    /// accepts. Module 24: a missing <c>RuntimeConfig</c> reference and a
    /// shutter overlay alpha of 0 or 1 are reported, and neither turns off the
    /// Module 20 block look.
    /// </summary>
    public class LevelEditorSettingsTests
    {
        [Test]
        public void Defaults_QuickBudget_IsASmallExhaustiveAttempt()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            Assert.AreEqual(MoveGenMode.Exhaustive, settings.QuickBudget.Mode);
            Assert.AreEqual(100_000, settings.QuickBudget.MaxExploredStates);
            Assert.AreEqual(2_000, settings.QuickBudget.MaxWallClockMs);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_SolveBudgets_AreD5sNumbers()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            Assert.AreEqual(MoveGenMode.Canonical, settings.CanonicalBudget.Mode);
            Assert.AreEqual(200_000, settings.CanonicalBudget.MaxExploredStates);
            Assert.AreEqual(5_000, settings.CanonicalBudget.MaxWallClockMs);

            Assert.AreEqual(MoveGenMode.Exhaustive, settings.ExhaustiveBudget.Mode);
            Assert.AreEqual(1_000_000, settings.ExhaustiveBudget.MaxExploredStates);
            Assert.AreEqual(15_000, settings.ExhaustiveBudget.MaxWallClockMs);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_TimeBudgetFormula_IsBaseTenPlusThreePerMoveRoundedToFive()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            var formula = settings.TimeBudget;

            Assert.AreEqual(10, formula.Base);
            Assert.AreEqual(3, formula.PerMove);
            Assert.AreEqual(5, formula.Rounding);
            Assert.AreEqual(35, formula.Suggest(8, 0));

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_WindowLayout_MatchesTheDocumentedProportionsAndFloors()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            Assert.AreEqual(0.2f, settings.PropertiesColumnWidth.Ratio);
            Assert.AreEqual(300f, settings.PropertiesColumnWidth.Floor);
            Assert.AreEqual(0.08f, settings.WarningsListHeight.Ratio);
            Assert.AreEqual(70f, settings.WarningsListHeight.Floor);
            Assert.AreEqual(220f, settings.CanvasMinHeight);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_UndoStackDepth_Is50()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            Assert.AreEqual(50, settings.UndoStackDepth);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_BoardFit_IsOneCellMarginAnd16To96Pixels()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            var fit = settings.BoardFit;

            Assert.AreEqual(1f, fit.MarginCells);
            Assert.AreEqual(16f, fit.MinCellSize);
            Assert.AreEqual(96f, fit.MaxCellSize);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_SolverLineColor_IsDistinctForEveryKind()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();
            var kinds = (SolverLineKind[])System.Enum.GetValues(typeof(SolverLineKind));

            for (var i = 0; i < kinds.Length; i++)
            {
                for (var j = i + 1; j < kinds.Length; j++)
                {
                    Assert.AreNotEqual(
                        settings.SolverLineColor(kinds[i]), settings.SolverLineColor(kinds[j]),
                        $"{kinds[i]} and {kinds[j]} share a tint");
                }
            }

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_WarningIconSize_Is12()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            Assert.AreEqual(12f, settings.WarningIconSize);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_ValidateButton_Is120By28()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            Assert.AreEqual(120f, settings.ValidateButtonWidth);
            Assert.AreEqual(28f, settings.ValidateButtonHeight);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_BlockMarks_AreTheDocumentedValues()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            Assert.AreEqual(0.2f, settings.BlockInsetCells);
            Assert.AreEqual(0.5f, settings.BlockNumberPlateCells);
            Assert.AreEqual(0.7f, settings.BlockNumberSizeRatio);
            Assert.AreEqual(7, settings.BlockNumberMinFontSize);
            Assert.AreNotEqual(settings.BlockNumberColor, settings.BlockNumberPlateColor, "a number reads on its plate");

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_Canvas_KeepTheColoursTheWindowHardcoded()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            Assert.AreEqual(new Color(0.22f, 0.22f, 0.25f), settings.CellBackgroundColor);
            Assert.AreEqual(new Color(0.08f, 0.08f, 0.09f), settings.WallCellColor);
            Assert.AreEqual(new Color(0.42f, 0.58f, 0.95f), settings.ShutterColor);
            Assert.AreEqual(new Color(0.95f, 0.66f, 0.24f), settings.ElevatorColor);
            Assert.AreEqual(0.45f, settings.ElevatorCellTintStrength);
            Assert.AreEqual(1f, settings.RegionBorderThickness);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Problems_DefaultsWithoutARuntimeConfig_ReportOnlyTheMissingReference()
        {
            // A fresh instance has no reference: the owner assigns it on the
            // asset. Every other default is usable.
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            var problems = settings.Problems();

            Assert.AreEqual(1, problems.Count, string.Join(" | ", problems));
            Assert.That(problems, Has.Some.Contains("Runtime Config is not assigned"), string.Join(" | ", problems));

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void AreBlockMarksUsable_WithoutARuntimeConfig_IsStillTrue()
        {
            // The Module 20 look — ice, frozen count, layer inset and number —
            // reads its own fields alone, so a missing reference leaves it
            // drawn; only the game's marks go.
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            var isLookUsable = settings.AreBlockMarksUsable;
            var areGameMarksUsable = settings.AreGameMarksUsable;

            Assert.IsNull(settings.GameConfig);
            Assert.IsTrue(isLookUsable);
            Assert.IsFalse(areGameMarksUsable);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void AreBlockMarksUsable_BadBlockInset_IsFalse_AndEveryProblemIsListedOnce()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();
            var serialized = new UnityEditor.SerializedObject(settings);
            serialized.FindProperty("blockInsetCells").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = settings.Problems();
            var isLookUsable = settings.AreBlockMarksUsable;

            Assert.IsFalse(isLookUsable);
            Assert.That(problems, Has.Some.Contains("Block Inset Cells"), string.Join(" | ", problems));
            Assert.That(problems, Has.Some.Contains("Runtime Config"), string.Join(" | ", problems));
            CollectionAssert.AllItemsAreUnique(problems, "the window shows each problem once");

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Problems_RuntimeConfigAssignedWithoutSprites_NamesTheSpritesNotTheReference()
        {
            // A bare config has no generated art assigned.
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();
            var config = ScriptableObject.CreateInstance<GateRush.Runtime.RuntimeConfig>();
            var serialized = new UnityEditor.SerializedObject(settings);
            serialized.FindProperty("runtimeConfig").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = settings.Problems();
            var isLookUsable = settings.AreBlockMarksUsable;
            var areGameMarksUsable = settings.AreGameMarksUsable;

            Assert.AreSame(config, settings.GameConfig);
            Assert.That(problems, Has.None.Contains("is not assigned"), string.Join(" | ", problems));
            foreach (var sprite in new[]
                     {
                         "Padlock Sprite", "Key Body Sprite", "Key Gem Sprite", "Clock Sprite", "Axis Arrow Sprite",
                         "Shutter Slats Sprite",
                     })
            {
                Assert.That(problems, Has.Some.Contains(sprite), string.Join(" | ", problems));
            }

            Assert.AreEqual(6, problems.Count, "the five game-mark sprites and the slats, nothing else: " + string.Join(" | ", problems));
            Assert.IsTrue(isLookUsable);
            Assert.IsFalse(areGameMarksUsable, "a mark's sprite is missing");

            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(config);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void Problems_ShutterOverlayAlphaAtEitherEnd_IsReportedByName(float alpha)
        {
            // At 0 the region does not read; at 1 it hides the blocks under it.
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();
            var serialized = new UnityEditor.SerializedObject(settings);
            serialized.FindProperty("shutterOverlayAlpha").floatValue = alpha;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = settings.Problems();
            var isLookUsable = settings.AreBlockMarksUsable;

            Assert.That(problems, Has.Some.Contains("Shutter Overlay Alpha"), string.Join(" | ", problems));
            Assert.IsTrue(isLookUsable, "a canvas value never hides the block look");

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Problems_BlockInsetOfHalfACell_IsReportedByName()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();
            var serialized = new UnityEditor.SerializedObject(settings);
            serialized.FindProperty("blockInsetCells").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var problems = settings.Problems();

            Assert.That(problems, Has.Some.Contains("Block Inset Cells"), string.Join(" | ", problems));

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Defaults_QueueEntryFreeDrawMaxDepth_Is4()
        {
            var settings = ScriptableObject.CreateInstance<LevelEditorSettings>();

            Assert.AreEqual(4, settings.QueueEntryFreeDrawMaxDepth);

            Object.DestroyImmediate(settings);
        }
    }
}
