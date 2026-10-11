using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GateRush.Runtime;
using GateRush.Solver;

namespace GateRush.Editor
{
    /// <summary>
    /// The editor-only, project-local tunables the Level Editor reads: the
    /// Validate budgets (the quick optimal attempt, and the canonical and
    /// exhaustive budgets, D5), the suggested-time-budget formula (D12), and the
    /// window-layout proportions the docs/Modules/09a follow-up replaced fixed
    /// pixel constants with, how the board fits its canvas, and how the solver
    /// result line is presented; and how the canvas draws cells, shutters,
    /// elevators and a block's marks, with a reference to the game's own
    /// config for the marks' sprites (Module 24). Kept in an asset so no number is fixed at a call
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

        [Header("Game marks (Module 24): the padlock, key, time bonus and axis arrow, drawn with the game's own sprites, sizes and colours")]
        [Tooltip("The game's config (Assets/Config/RuntimeConfig.asset). Its sprites, mark sizes and colours are what the canvas draws; without it these marks are not drawn.")]
        [SerializeField] private RuntimeConfig runtimeConfig;

        [Tooltip("Side of the plate showing a padlock's required key count, in cells: above 0. It grows where the smallest font needs more.")]
        [SerializeField] private float lockCountPlateCells = 0.3f;

        [Tooltip("How far below the padlock's centre its key count sits, in cells: above 0.")]
        [SerializeField] private float lockCountDropCells = 0.22f;

        [Tooltip("Height of a time bonus's plate, in cells: above 0. It grows where the smallest font needs more.")]
        [SerializeField] private float bonusPlateHeightCells = 0.34f;

        [Tooltip("The clock's size as a fraction of the time-bonus plate's height: above 0, at most 1.")]
        [SerializeField] private float bonusIconSizeRatio = 0.75f;

        [Tooltip("Space round the clock and the text as a fraction of the time-bonus plate's height: above 0, below 0.5.")]
        [SerializeField] private float bonusPaddingRatio = 0.12f;

        [Tooltip("The time-bonus plate's rim as a fraction of its height: above 0, below 0.5.")]
        [SerializeField] private float bonusRimRatio = 0.08f;

        [Header("Canvas (Module 24): cell fills, and how shutters and elevators read on the grid")]
        [Tooltip("Fill of a cell nothing occupies.")]
        [SerializeField] private Color cellBackgroundColor = new Color(0.22f, 0.22f, 0.25f);

        [Tooltip("Fill of a static wall cell.")]
        [SerializeField] private Color wallCellColor = new Color(0.08f, 0.08f, 0.09f);

        [Tooltip("A shutter's hue (M5): its drag preview, and its flat fill and outline while the game's config or sprites are missing.")]
        [SerializeField] private Color shutterColor = new Color(0.42f, 0.58f, 0.95f);

        [Tooltip("How opaque a shutter's slats over its region are, multiplying the game's slat colour's alpha: above 0 and below 1, so the region reads and the blocks under it stay visible.")]
        [SerializeField] private float shutterOverlayAlpha = 0.35f;

        [Tooltip("Thickness of a shutter's outline, in pixels: above 0.")]
        [SerializeField] private float shutterOutlineThickness = 3f;

        [Tooltip("An elevator's hue (M9): its empty cells' tint, its border and its drag preview.")]
        [SerializeField] private Color elevatorColor = new Color(0.95f, 0.66f, 0.24f);

        [Tooltip("How far an empty cell in an elevator's region is tinted from the background toward the elevator's hue: 0 to 1.")]
        [SerializeField] private float elevatorCellTintStrength = 0.45f;

        [Tooltip("Thickness of an elevator region's border, in pixels: above 0.")]
        [SerializeField] private float regionBorderThickness = 1f;

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
        /// The game's config, whose sprites, mark sizes and colours the canvas
        /// draws a block's padlock, key, time bonus and axis arrow with
        /// (Module 24), so both screens show the same icon. Null until the
        /// owner assigns it; <see cref="AreGameMarksUsable"/> says whether
        /// it, and everything read from it, is there.
        /// </summary>
        public RuntimeConfig GameConfig => runtimeConfig;

        /// <summary>Side of the plate showing a padlock's required key count, in cells. Default 0.3.</summary>
        public float LockCountPlateCells => lockCountPlateCells;

        /// <summary>How far below the padlock's centre its key count sits, in cells. Default 0.22.</summary>
        public float LockCountDropCells => lockCountDropCells;

        /// <summary>Height of a time bonus's plate, in cells. Default 0.34.</summary>
        public float BonusPlateHeightCells => bonusPlateHeightCells;

        /// <summary>The clock's size as a fraction of the time-bonus plate's height. Default 0.75.</summary>
        public float BonusIconSizeRatio => bonusIconSizeRatio;

        /// <summary>Space round the clock and the text as a fraction of the time-bonus plate's height. Default 0.12.</summary>
        public float BonusPaddingRatio => bonusPaddingRatio;

        /// <summary>The time-bonus plate's rim as a fraction of its height. Default 0.08.</summary>
        public float BonusRimRatio => bonusRimRatio;

        /// <summary>Fill of a cell nothing occupies.</summary>
        public Color CellBackgroundColor => cellBackgroundColor;

        /// <summary>Fill of a static wall cell.</summary>
        public Color WallCellColor => wallCellColor;

        /// <summary>A shutter's hue (M5): its drag preview, and its flat fill and outline while the game's slats cannot be drawn (<see cref="AreGameMarksUsable"/>).</summary>
        public Color ShutterColor => shutterColor;

        /// <summary>How opaque a shutter's slats over its region are, multiplying the game's slat colour's alpha. Default 0.35.</summary>
        public float ShutterOverlayAlpha => shutterOverlayAlpha;

        /// <summary>Thickness of a shutter's outline, in pixels. Default 3.</summary>
        public float ShutterOutlineThickness => shutterOutlineThickness;

        /// <summary>An elevator's hue (M9): its empty cells' tint, its border and its drag preview.</summary>
        public Color ElevatorColor => elevatorColor;

        /// <summary>How far an empty cell in an elevator's region is tinted toward <see cref="ElevatorColor"/>. Default 0.45.</summary>
        public float ElevatorCellTintStrength => elevatorCellTintStrength;

        /// <summary>Thickness of an elevator region's border, in pixels. Default 1.</summary>
        public float RegionBorderThickness => regionBorderThickness;

        /// <summary>
        /// True when the values behind a block's Module 20 look — the ice
        /// colour, the frozen count, the layer inset and the layer number —
        /// are usable. It reads those fields alone: a missing
        /// <see cref="GameConfig"/> or a bad Module 24 value never turns it
        /// off.
        /// </summary>
        public bool AreBlockMarksUsable => BlockLookProblems().Count == 0;

        /// <summary>
        /// True when the padlock, key, time bonus and axis arrow can be drawn,
        /// and a shutter in the game's slats (Module 24):
        /// <see cref="GameConfig"/> is assigned with the sprites they need,
        /// and the marks' own sizes here are usable.
        /// </summary>
        public bool AreGameMarksUsable => GameMarkProblems().Count == 0;

        /// <summary>
        /// Every reason the canvas cannot be drawn as configured, as messages
        /// naming the field, each once; empty when it can. The window shows
        /// them above the grid. They fall in three groups: the Module 20
        /// block look (<see cref="AreBlockMarksUsable"/>), the game marks
        /// (<see cref="AreGameMarksUsable"/>), and the canvas's own values,
        /// which are reported but still drawn as set.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = BlockLookProblems();
            problems.AddRange(GameMarkProblems());

            if (!(shutterOverlayAlpha > 0f && shutterOverlayAlpha < 1f))
            {
                problems.Add($"{name}: Shutter Overlay Alpha must be above 0 and below 1, so a shutter's region reads and the blocks under it stay visible.");
            }

            if (!(shutterOutlineThickness > 0f))
            {
                problems.Add($"{name}: Shutter Outline Thickness must be above 0.");
            }

            if (!(elevatorCellTintStrength >= 0f && elevatorCellTintStrength <= 1f))
            {
                problems.Add($"{name}: Elevator Cell Tint Strength must be from 0 to 1.");
            }

            if (!(regionBorderThickness > 0f))
            {
                problems.Add($"{name}: Region Border Thickness must be above 0.");
            }

            return problems;
        }

        /// <summary>
        /// Why the padlock, key, time bonus and axis arrow cannot be drawn,
        /// nor a shutter in the game's slats: the game's config or one of
        /// its sprites is missing, or one of the marks' sizes here is
        /// unusable. Empty when they can.
        /// </summary>
        private List<string> GameMarkProblems()
        {
            var problems = new List<string>();

            if (runtimeConfig == null)
            {
                problems.Add(
                    $"{name}: Runtime Config is not assigned, so padlocks, keys, time bonuses and axis arrows are not drawn on the canvas and shutters show as a flat fill. " +
                    "Assign Assets/Config/RuntimeConfig.asset.");
            }
            else
            {
                AddMissingSprite(problems, runtimeConfig.PadlockSprite, "Padlock Sprite");
                AddMissingSprite(problems, runtimeConfig.KeyBodySprite, "Key Body Sprite");
                AddMissingSprite(problems, runtimeConfig.KeyGemSprite, "Key Gem Sprite");
                AddMissingSprite(problems, runtimeConfig.ClockSprite, "Clock Sprite");
                AddMissingSprite(problems, runtimeConfig.AxisArrowSprite, "Axis Arrow Sprite");
                AddMissingSprite(problems, runtimeConfig.ShutterSlatsSprite, "Shutter Slats Sprite");
            }

            if (!(lockCountPlateCells > 0f))
            {
                problems.Add($"{name}: Lock Count Plate Cells must be above 0.");
            }

            if (!(lockCountDropCells > 0f))
            {
                problems.Add($"{name}: Lock Count Drop Cells must be above 0.");
            }

            if (!(bonusPlateHeightCells > 0f))
            {
                problems.Add($"{name}: Bonus Plate Height Cells must be above 0.");
            }

            if (!(bonusIconSizeRatio > 0f && bonusIconSizeRatio <= 1f))
            {
                problems.Add($"{name}: Bonus Icon Size Ratio must be above 0 and at most 1.");
            }

            if (!(bonusPaddingRatio > 0f && bonusPaddingRatio < 0.5f))
            {
                problems.Add($"{name}: Bonus Padding Ratio must be above 0 and below 0.5.");
            }

            if (!(bonusRimRatio > 0f && bonusRimRatio < 0.5f))
            {
                problems.Add($"{name}: Bonus Rim Ratio must be above 0 and below 0.5.");
            }

            return problems;
        }

        private void AddMissingSprite(List<string> problems, Sprite sprite, string field)
        {
            if (sprite == null)
            {
                problems.Add(
                    $"{name}: Runtime Config '{runtimeConfig.name}' has no {field}, so padlocks, keys, time bonuses and axis arrows are not drawn on the canvas and shutters show as a flat fill.");
            }
        }

        /// <summary>
        /// Why a block's Module 20 look — ice, frozen count, layer inset and
        /// layer number — cannot be drawn as configured. Empty when it can.
        /// </summary>
        private List<string> BlockLookProblems()
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
