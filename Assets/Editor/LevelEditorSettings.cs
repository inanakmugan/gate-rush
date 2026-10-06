using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GateRush.Solver;

namespace GateRush.Editor
{
    /// <summary>
    /// The editor-only, project-local tunables the Level Editor reads: the
    /// Validate budgets (the quick optimal attempt, and the canonical and
    /// exhaustive budgets, D5), the suggested-time-budget formula (D12), and the
    /// window-layout proportions the docs/Modules/09a follow-up replaced fixed
    /// pixel constants with, how the board fits its canvas, and how the solver
    /// result line is presented. Kept in an asset so no number is fixed at a call
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
        [Header("Quick optimal attempt (Validate's first step): exhaustive A*, small, to prove easy levels shortest fast")]
        [SerializeField] private int quickMaxDepth = 400;
        [SerializeField] private int quickMaxExploredStates = 100_000;
        [SerializeField] private long quickMaxWallClockMs = 2_000;

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

        [Header("Generator queue entry free draw (docs/Modules/09a follow-up, D34): bounded along the generator's edge by its width, and into the board by this depth")]
        [SerializeField] private int queueEntryFreeDrawMaxDepth = 4;

        [Header("Undo (docs/Modules/09a, Session C): depth of the level editor's undo stack. A level's DTO is a few kilobytes, so memory is not a consideration.")]
        [SerializeField] private int undoStackDepth = 50;

        [Header("Board fit: the grid fills the canvas, keeping this many cells of margin (room for edge markers), with each cell clamped to this pixel range")]
        [SerializeField] private float boardMarginCells = 1f;
        [SerializeField] private float boardMinCellSize = 16f;
        [SerializeField] private float boardMaxCellSize = 96f;

        [Header("Solver result line: tint per verdict, and the Validate button's size")]
        [SerializeField] private Color solverSolvableColor = new Color(0.4f, 0.85f, 0.45f);
        [SerializeField] private Color solverUnsolvableColor = new Color(0.95f, 0.42f, 0.38f);
        [SerializeField] private Color solverIndeterminateColor = new Color(0.95f, 0.75f, 0.3f);
        [SerializeField] private Color solverNotRunColor = new Color(0.7f, 0.7f, 0.72f);
        [SerializeField] private float validateButtonWidth = 120f;
        [SerializeField] private float validateButtonHeight = 28f;

        [Header("Warnings list: the icon's drawn size in pixels, matched to the mini label's line height")]
        [SerializeField] private float warningIconSize = 12f;

        [Header("Block marks (Module 20): how layered and frozen blocks read on the grid, in a wave's scope and in a generator's queue")]
        [Tooltip("How far a layered block's second colour sits inside its footprint, in cells: above 0, below 0.5.")]
        [SerializeField] private float blockInsetCells = 0.2f;

        [Tooltip("Fill of a block that starts frozen (M3), whatever its own colours.")]
        [SerializeField] private Color frozenBlockColor = new Color(0.80f, 0.91f, 0.98f);

        [Tooltip("Side of the plate behind a block's number (a frozen block's count, a deep stack's depth), in cells: above 0, at most 1. It grows where the smallest font needs more.")]
        [SerializeField] private float blockNumberPlateCells = 0.5f;

        [Tooltip("The number's font size as a fraction of its plate's side: above 0, at most 1.")]
        [SerializeField] private float blockNumberSizeRatio = 0.7f;

        [Tooltip("The smallest font a block's number is drawn in, in pixels: at least 1. On small cells the plate grows to hold it.")]
        [SerializeField] private int blockNumberMinFontSize = 7;

        [SerializeField] private Color blockNumberColor = new Color(0.12f, 0.14f, 0.20f);
        [SerializeField] private Color blockNumberPlateColor = new Color(1f, 1f, 1f, 0.85f);

        /// <summary>
        /// The quick optimal attempt Validate makes first: exhaustive A* at a
        /// small budget. Easy levels finish inside it with a proven answer; the
        /// rest fall through to nearest-next-clear. Default 100,000 states / 2 s.
        /// </summary>
        public SearchBudget QuickBudget => new SearchBudget(
            quickMaxDepth, quickMaxExploredStates, quickMaxWallClockMs, MoveGenMode.Exhaustive);

        /// <summary>
        /// Canonical A*: the first stage of the cross-check that corroborates a
        /// nearest-next-clear Unsolvable.
        /// </summary>
        public SearchBudget CanonicalBudget => new SearchBudget(
            canonicalMaxDepth, canonicalMaxExploredStates, canonicalMaxWallClockMs, MoveGenMode.Canonical);

        /// <summary>
        /// The normal exhaustive budget: nearest-next-clear's own, and the second
        /// stage of its A* cross-check.
        /// </summary>
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
        /// How far a generator queue entry's free draw extends into the board.
        /// The grid is sized per axis (<see cref="QueueEntryDrawBounds"/>): the
        /// axis along the generator's edge is capped at the generator's own
        /// width (D34), and the axis into the board at this value. A block's
        /// extent into the board is unconstrained by the rules (M6), so this is
        /// a draw-surface bound only; the default of 4 covers an I-tetromino
        /// pushed straight in. Default 4.
        /// </summary>
        public int QueueEntryFreeDrawMaxDepth => queueEntryFreeDrawMaxDepth;

        /// <summary>The Level Editor undo stack's depth (docs/Modules/09a, Session C). Default 50.</summary>
        public int UndoStackDepth => undoStackDepth;

        /// <summary>
        /// How the main board fits its canvas (<see cref="EditorGridLayout.Fit"/>).
        /// The margin is in cells so the edge markers, which are sized in cells,
        /// stay inside the canvas at any size. Default 1 cell, 16–96 px.
        /// </summary>
        public GridFit BoardFit => new GridFit(boardMarginCells, boardMinCellSize, boardMaxCellSize);

        /// <summary>The solver line's tint for <paramref name="kind"/>.</summary>
        public Color SolverLineColor(SolverLineKind kind)
        {
            switch (kind)
            {
                case SolverLineKind.Solvable:
                    return solverSolvableColor;
                case SolverLineKind.Unsolvable:
                    return solverUnsolvableColor;
                case SolverLineKind.Indeterminate:
                    return solverIndeterminateColor;
                case SolverLineKind.NotRun:
                    return solverNotRunColor;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown solver line kind.");
            }
        }

        /// <summary>The Validate (and Cancel) button's width. Default 120.</summary>
        public float ValidateButtonWidth => validateButtonWidth;

        /// <summary>The Validate (and Cancel) button's height. Default 28.</summary>
        public float ValidateButtonHeight => validateButtonHeight;

        /// <summary>
        /// The square size each warning line's icon is drawn at. The built-in
        /// icon that loads is the large variant, so without this it draws at
        /// about twice the text's height. Default 12, the mini label's line height.
        /// </summary>
        public float WarningIconSize => warningIconSize;

        /// <summary>How far a layered block's second colour sits inside its footprint, in cells (<see cref="EditorLayerInset"/>). Default 0.2.</summary>
        public float BlockInsetCells => blockInsetCells;

        /// <summary>The fill of a block that starts frozen (<see cref="EditorBlockLook.IsFrozen"/>).</summary>
        public Color FrozenBlockColor => frozenBlockColor;

        /// <summary>Side of the plate behind a block's number, in cells. Default 0.5.</summary>
        public float BlockNumberPlateCells => blockNumberPlateCells;

        /// <summary>A block number's font size as a fraction of its plate's side. Default 0.7.</summary>
        public float BlockNumberSizeRatio => blockNumberSizeRatio;

        /// <summary>The smallest font a block's number is drawn in, in pixels. Default 7.</summary>
        public int BlockNumberMinFontSize => blockNumberMinFontSize;

        /// <summary>Colour of a block's number.</summary>
        public Color BlockNumberColor => blockNumberColor;

        /// <summary>Colour of the plate behind a block's number, which keeps it readable on any block colour.</summary>
        public Color BlockNumberPlateColor => blockNumberPlateColor;

        /// <summary>
        /// Every reason the block marks cannot be drawn as configured, as
        /// messages naming the field; empty when they can. The window shows
        /// them above the grid.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();

            if (!(blockInsetCells > 0f && blockInsetCells < 0.5f))
            {
                problems.Add($"{name}: Block Inset Cells must be above 0 and below 0.5, so a layered block keeps a visible second colour.");
            }

            if (!(blockNumberPlateCells > 0f && blockNumberPlateCells <= 1f))
            {
                problems.Add($"{name}: Block Number Plate Cells must be above 0 and at most 1.");
            }

            if (!(blockNumberSizeRatio > 0f && blockNumberSizeRatio <= 1f))
            {
                problems.Add($"{name}: Block Number Size Ratio must be above 0 and at most 1.");
            }

            if (blockNumberMinFontSize < 1)
            {
                problems.Add($"{name}: Block Number Min Font Size must be at least 1.");
            }

            return problems;
        }

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
