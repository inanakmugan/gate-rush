using UnityEditor;
using UnityEngine;
using GateRush.Solver;

namespace GateRush.Editor
{
    /// <summary>
    /// The editor-only, project-local tunables the Level Editor reads: the two
    /// solve budgets (D5) and the suggested-time-budget formula (D12). Kept in an
    /// asset so no number is fixed at a call site and the budgets can be edited
    /// in the window and persist.
    /// </summary>
    /// <remarks>
    /// The budget defaults are D5's: canonical 200,000 states / 5 s, exhaustive
    /// 1,000,000 / 15 s. The formula defaults are <c>base 10 + 3 per move</c>,
    /// rounded up to 5. None of the testable classes
    /// (<see cref="LevelSolveRunner"/>, <see cref="DraftMetrics"/>) depend on
    /// this type — the window reads it and passes the pieces — so this stays a
    /// thin data holder.
    /// </remarks>
    public sealed class LevelEditorSettings : ScriptableObject
    {
        [Header("Canonical solve budget (stage 1)")]
        [SerializeField] private int canonicalMaxDepth = 200;
        [SerializeField] private int canonicalMaxExploredStates = 200_000;
        [SerializeField] private long canonicalMaxWallClockMs = 5_000;

        [Header("Exhaustive solve budget (stage 2)")]
        [SerializeField] private int exhaustiveMaxDepth = 400;
        [SerializeField] private int exhaustiveMaxExploredStates = 1_000_000;
        [SerializeField] private long exhaustiveMaxWallClockMs = 15_000;

        [Header("Suggested time budget = base + perMove * moves + bonuses, rounded up")]
        [SerializeField] private int timeBudgetBaseSeconds = 10;
        [SerializeField] private int timeBudgetSecondsPerMove = 3;
        [SerializeField] private int timeBudgetRoundingSeconds = 5;

        public SearchBudget CanonicalBudget => new SearchBudget(
            canonicalMaxDepth, canonicalMaxExploredStates, canonicalMaxWallClockMs, MoveGenMode.Canonical);

        public SearchBudget ExhaustiveBudget => new SearchBudget(
            exhaustiveMaxDepth, exhaustiveMaxExploredStates, exhaustiveMaxWallClockMs, MoveGenMode.Exhaustive);

        public TimeBudgetFormula TimeBudget => new TimeBudgetFormula(
            timeBudgetBaseSeconds, timeBudgetSecondsPerMove, timeBudgetRoundingSeconds);

        private const string AssetPath = "Assets/Editor/LevelEditorSettings.asset";

        /// <summary>
        /// The project's settings asset, created on first use. Editor-only — a
        /// test that just needs default values calls
        /// <see cref="ScriptableObject.CreateInstance{T}()"/> instead.
        /// </summary>
        public static LevelEditorSettings GetOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LevelEditorSettings>(AssetPath);
            if (existing != null)
            {
                return existing;
            }

            var created = CreateInstance<LevelEditorSettings>();
            AssetDatabase.CreateAsset(created, AssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }
    }
}
