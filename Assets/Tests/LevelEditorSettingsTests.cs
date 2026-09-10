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
    /// today's window is tuned for.
    /// </summary>
    public class LevelEditorSettingsTests
    {
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
    }
}
