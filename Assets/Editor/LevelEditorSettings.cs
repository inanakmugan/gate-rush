using UnityEditor;
using UnityEngine;
using GateRush.Solver;

namespace GateRush.Editor
{
    /// <summary>
    /// The editor-only, project-local tunables the Level Editor reads: the two
    /// solve budgets (D5), the suggested-time-budget formula (D12), and the
    /// window-layout proportions the docs/Modules/09a follow-up replaced fixed
    /// pixel constants with. Kept in an asset so no number is fixed at a call
    /// site and every one of them can be edited in the window and persist.
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

        [Header("Window layout (docs/Modules/09a follow-up): ratio of the window's own size, floored so a small window stays usable")]
        [SerializeField] private float propertiesColumnWidthRatio = 0.2f;
        [SerializeField] private float propertiesColumnMinWidth = 300f;
        [SerializeField] private float warningsListHeightRatio = 0.08f;
        [SerializeField] private float warningsListMinHeight = 70f;
        [SerializeField] private float canvasMinHeight = 220f;

        [Header("Generator queue entry free draw (docs/Modules/09a follow-up): a queue entry has no board to place on, so its free draw is bounded to a fixed square rather than the grid's own size")]
        [SerializeField] private int queueEntryFreeDrawGridSize = 5;

        public SearchBudget CanonicalBudget => new SearchBudget(
            canonicalMaxDepth, canonicalMaxExploredStates, canonicalMaxWallClockMs, MoveGenMode.Canonical);

        public SearchBudget ExhaustiveBudget => new SearchBudget(
            exhaustiveMaxDepth, exhaustiveMaxExploredStates, exhaustiveMaxWallClockMs, MoveGenMode.Exhaustive);

        public TimeBudgetFormula TimeBudget => new TimeBudgetFormula(
            timeBudgetBaseSeconds, timeBudgetSecondsPerMove, timeBudgetRoundingSeconds);

        public ProportionalSize PropertiesColumnWidth => new ProportionalSize(propertiesColumnWidthRatio, propertiesColumnMinWidth);

        public ProportionalSize WarningsListHeight => new ProportionalSize(warningsListHeightRatio, warningsListMinHeight);

        /// <summary>
        /// The canvas's own floor, honoured by <c>GetRect</c> alongside
        /// <c>ExpandHeight</c> so the canvas claims whatever the window's layout
        /// actually has left rather than a guess at the footer's height. The
        /// footer never shrinks below its natural size to make room for this —
        /// see <see cref="LevelEditorWindow.OnGUI"/>'s outer scroll view, which is
        /// what keeps the two from ever overlapping when a window is too short
        /// for both.
        /// </summary>
        public float CanvasMinHeight => canvasMinHeight;

        /// <summary>
        /// The side length of the fixed square a generator queue entry's free
        /// draw is bounded to. A 5x5 default covers a T, an S/Z, a 2x3 or a plus
        /// shape; a generator sits on a board edge and pushes inward, so nothing
        /// a realistic level would need is larger than that anyway.
        /// </summary>
        public int QueueEntryFreeDrawGridSize => queueEntryFreeDrawGridSize;

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
