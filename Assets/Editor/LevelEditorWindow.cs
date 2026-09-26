using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GateRush.Core;
using GateRush.Serialization;
using GateRush.Solver;
using UnityEditor;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// The Level Editor window. It draws the draft and routes clicks into it, and
    /// decides nothing: every rule — what is wrong with a level, whether it is
    /// solvable, whether a wave tiles, what a resize removes — is answered by
    /// <see cref="LevelDraft"/>, <see cref="DraftValidator"/>,
    /// <see cref="DraftMetrics"/>, <see cref="DraftTiling"/> and
    /// <see cref="ValidationPipeline"/>. If a rule ever needs to live here, it
    /// belongs in one of those instead.
    /// </summary>
    /// <remarks>
    /// <para><b>Validate runs off the main thread.</b> Everything the pipeline
    /// needs — the <see cref="LevelContext"/> and the budgets — is built on the
    /// main thread before it starts, and the pipeline itself touches no Unity
    /// API, so it runs on a thread-pool task. The window polls the task from
    /// <see cref="EditorApplication.update"/> and applies the result on the main
    /// thread. The only thing the worker writes that the window reads while it
    /// runs is the current stage, through <see cref="Interlocked"/>.</para>
    /// <para><b>One run at a time.</b> While a run is in progress the Validate
    /// button becomes Cancel. Editing, loading or starting a new level cancels
    /// the run, because its answer would describe a draft that no longer exists.
    /// Closing the window, entering Play Mode or recompiling scripts cancels it
    /// too (<see cref="OnDisable"/>); every search checks the cancellation token
    /// before each expansion, so the worker stops within one expansion.</para>
    /// </remarks>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/Resources/Levels";

        /// <summary>
        /// The palette shape preview's fixed footprint, in cells. Not a
        /// <see cref="LevelEditorSettings"/> tunable like the queue entry's free
        /// draw bound — this only sizes a passive preview, it never bounds what
        /// can be drawn (the board's own free draw is unbounded, same as today).
        /// </summary>
        private const int PaletteShapePreviewCells = 4;

        /// <summary>What a live drag is currently doing. Set on mouse down, cleared on mouse up or Escape; the draft itself is never touched until a legal mouse-up applies it (docs/Modules/09a, Session B, Part 2).</summary>
        private enum DragKind { None, BoardBlock, WaveBlock, RegionMove, RegionCreate, EdgeFeature }

        [MenuItem("Window/Gate Rush/Level Editor")]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        // -- working state --
        private LevelDraft draft;
        private string assetPath;
        private bool dirty;
        private LevelEditorSettings settings;

        private EditorTool tool = EditorTool.Select;
        private ShapePreset shape = ShapePreset.Single;
        private readonly List<Coord> freeCells = new List<Coord>();

        /// <summary>
        /// Generator queue entries the designer has explicitly put into Free
        /// mode this session. Session-only UI state, not part of the draft —
        /// mirrors <see cref="freeCells"/> in that sense. Without it the mode
        /// would have to be re-derived from the entry's current cells on every
        /// draw, which is exactly the bug this fixes: completing a shape that
        /// happens to match a preset mid-edit would flip the popup away from
        /// Free and hide the draw surface out from under the designer.
        /// </summary>
        private readonly HashSet<SpawnedBlockDraft> queueEntriesInFreeMode = new HashSet<SpawnedBlockDraft>();

        private DraftHistory history;

        private object selectionValue;

        /// <summary>
        /// Backed by <see cref="selectionValue"/> rather than a plain field so
        /// every existing assignment site keeps working unchanged while still
        /// telling <see cref="history"/> that a repeated focus key can no longer
        /// mean "still editing the same thing" (docs/Modules/09a, Session C):
        /// two different objects can present an identically shaped panel, so the
        /// same control id can land on two unrelated fields once the selection
        /// changes between them. Wave scope is covered transitively — entering
        /// or leaving it always clears the selection.
        /// </summary>
        private object selection
        {
            get => selectionValue;
            set
            {
                selectionValue = value;
                history?.BreakCoalescing();
            }
        }

        private int scopeElevator = -1;
        private int scopeWave = -1;

        // -- live drag (Session B, Part 2) --
        private DragKind dragKind = DragKind.None;

        private BlockDraft dragBoardBlock;
        private SpawnedBlockDraft dragWaveBlockTarget;
        private Coord dragGrabOffset;
        private Coord dragCandidateOrigin;
        private bool dragCandidateLegal;

        /// <summary>The gate or generator being dragged, and the whole placement it would land in — a drag can carry it onto a different edge, so the candidate is an edge and an offset together (docs/Modules/09a).</summary>
        private object dragEdgeFeature;
        private int dragEdgeGrabOffset;
        private BoardEdge dragEdgeCandidateEdge;
        private int dragEdgeCandidateOffset;
        private bool dragEdgeCandidateLegal;

        private EditorTool dragRegionTool;
        private ShutterDraft dragShutter;
        private ElevatorDraft dragElevator;
        private Coord dragRegionAnchorCell;
        private Coord dragRegionOriginalMin;
        private Coord dragRegionOriginalMax;
        private Coord dragRegionCandidateMin;
        private Coord dragRegionCandidateMax;

        // -- cached results of Step 3's logic (recomputed on edit) --
        private IReadOnlyList<DraftWarning> warnings = Array.Empty<DraftWarning>();
        private DraftMetrics metrics;
        private ValidationResult solve;

        // -- Validate on a worker thread --
        private Task<ValidationResult> validation;
        private CancellationTokenSource validationCancellation;
        private double validationStartedAt;
        private double lastValidationRepaint;

        /// <summary>The running stage, written by the worker through <see cref="Interlocked"/>.</summary>
        private int validationStage;

        /// <summary>Why the last run was cancelled, shown until the next run; null when it was not.</summary>
        private string validationCancelledReason;

        /// <summary>The last run's failure — a solver bug, never a verdict — shown as an error until the next run.</summary>
        private string validationError;

        private Dictionary<Type, Action<object>> inspectors;
        private Vector2 windowScroll;
        private Vector2 warningScroll;
        private Vector2 propertyScroll;
        private int newWidth = 6;
        private int newHeight = 6;

        private void OnEnable()
        {
            settings = LevelEditorSettings.GetOrCreate();
            if (draft == null)
            {
                draft = LevelDraft.NewEmpty(6, 6);
                history = new DraftHistory(settings.UndoStackDepth);
                history.Reset(draft.ToDto());
                SyncGridFields();
            }

            inspectors = new Dictionary<Type, Action<object>>
            {
                { typeof(BlockDraft), o => DrawBlockProperties((BlockDraft)o) },
                { typeof(GateDraft), o => DrawGateProperties((GateDraft)o) },
                { typeof(ShutterDraft), o => DrawShutterProperties((ShutterDraft)o) },
                { typeof(GeneratorDraft), o => DrawGeneratorProperties((GeneratorDraft)o) },
                { typeof(ElevatorDraft), o => DrawElevatorProperties((ElevatorDraft)o) },
                { typeof(SpawnedBlockDraft), o => DrawWaveBlockProperties((SpawnedBlockDraft)o) },
            };

            // A window disabled and re-enabled without a domain reload keeps its
            // fields, including a run that was cancelled on the way out: resume
            // polling so it is collected rather than left looking busy forever.
            if (validation != null)
            {
                EditorApplication.update -= PollValidation;
                EditorApplication.update += PollValidation;
            }

            Revalidate();
        }

        /// <summary>
        /// Stops a running Validate. Called when the window closes and before
        /// every domain reload — entering Play Mode, recompiling — so no worker
        /// is left searching a level nobody can see, and the reload is not held
        /// up by one.
        /// </summary>
        private void OnDisable()
        {
            CancelValidation("the Level Editor was closed or reloaded");
            EditorApplication.update -= PollValidation;
        }

        // -- the frame -----------------------------------------------

        private void OnGUI()
        {
            if (draft == null)
            {
                OnEnable();
            }

            // A text field keeps keyboard focus in IMGUI until something else
            // explicitly claims it. Clicking the grid, a toolbar button, an
            // outline list entry, a tool, or a shape preset does not — none of
            // that code touches keyboard focus — so without this, focus (and
            // EditorGUIUtility.editingTextField) would stick to whatever field
            // was last edited for the rest of the session. Two consequences:
            // the undo shortcut's editingTextField guard below would swallow
            // every future Ctrl+Z, and a value the designer typed and then
            // abandoned by clicking elsewhere would never commit.
            // EditorGUIUtility.editingTextField is the documented way to end
            // the active text edit — a recycled editor holds the typed string
            // and parses it into the value on focus loss, which setting this
            // false forces — before this frame routes the click to whatever it
            // actually landed on. One check here covers every click in the
            // window rather than repeating it at each place that handles one.
            if (Event.current.type == EventType.MouseDown)
            {
                GUIUtility.keyboardControl = 0;
                EditorGUIUtility.editingTextField = false;
            }

            DrawToolbar();
            DrawToolRow();
            DrawShapePaletteRow();

            // Item 4: the canvas below claims whatever height the layout system
            // actually has left after this scroll view's other content — no more
            // guessing the footer's height at the canvas. The footer never
            // shrinks to make room; it always draws at its natural size. When
            // even the canvas's floor (LevelEditorSettings.CanvasMinHeight) plus
            // the footer does not fit the window, this scroll view is what
            // guarantees the two never overlap or clip each other: it scrolls
            // instead, exactly as any content too tall for its view would.
            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawGridColumn();
            DrawPropertiesColumn();
            EditorGUILayout.EndHorizontal();

            DrawFooter();

            EditorGUILayout.EndScrollView();

            HandleGlobalKeyboardShortcuts();
        }

        /// <summary>
        /// Every draft mutation funnels through here: the level is dirty, any
        /// earlier solve result is stale, and the live warnings and metrics
        /// recompute (they are cheap — Module 09).
        /// </summary>
        private void Mutated()
        {
            dirty = true;
            DiscardSolve();
            history.Record(draft.ToDto(), GUIUtility.keyboardControl);
            Revalidate();
        }

        private void Revalidate()
        {
            warnings = new DraftValidator().Validate(draft);

            // solve is null on every path except immediately after a Validate
            // run finishes — DiscardSolve() clears it on every edit, and cancels
            // a run still in flight — so an out-of-date solution length can
            // never feed the suggested time budget. Only a fresh solve supplies
            // a move count, and a length not proven shortest travels separately
            // so the budget built from it is flagged rather than passed off as
            // the designed one.
            var solved = solve != null && solve.Verdict == LevelSolveVerdict.Solvable;
            var provenLength = solved ? solve.ProvenShortestLength : null;
            int? unprovenLength = solved && provenLength == null ? solve.Solution.Count : (int?)null;
            int? explored = null;
            int? stratum = null;
            if (solve != null)
            {
                explored = solve.Answering.ExploredStateCount;
                stratum = solve.Answering.PeakRetainedStateCount;
            }

            metrics = DraftMetrics.Compute(draft, settings.TimeBudget, provenLength, unprovenLength, explored, stratum);
        }

        // -- toolbar (new / open / save + breadcrumb) --------------

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(44)))
            {
                if (ConfirmDiscardIfDirty())
                {
                    NewLevel();
                }
            }

            if (GUILayout.Button("Open ▾", EditorStyles.toolbarButton, GUILayout.Width(56)))
            {
                ShowOpenMenu();
            }

            using (new EditorGUI.DisabledScope(assetPath == null))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(44)))
                {
                    Save(assetPath);
                }
            }

            if (GUILayout.Button("Save As", EditorStyles.toolbarButton, GUILayout.Width(56)))
            {
                SaveAs();
            }

            GUILayout.Space(12);
            GUILayout.Label(Breadcrumb(), EditorStyles.miniBoldLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Label("Grid", EditorStyles.miniLabel);
            newWidth = Mathf.Max(1, EditorGUILayout.IntField(newWidth, GUILayout.Width(34)));
            GUILayout.Label("x", EditorStyles.miniLabel);
            newHeight = Mathf.Max(1, EditorGUILayout.IntField(newHeight, GUILayout.Width(34)));
            if (GUILayout.Button("Resize", EditorStyles.toolbarButton, GUILayout.Width(52)))
            {
                RequestResize(newWidth, newHeight);
            }

            EditorGUILayout.EndHorizontal();

            if (InWaveScope())
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                if (GUILayout.Button("◂ Back to board", EditorStyles.toolbarButton, GUILayout.Width(120)))
                {
                    LeaveWaveScope();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private string Breadcrumb()
        {
            var name = assetPath != null ? Path.GetFileNameWithoutExtension(assetPath) : $"Level {draft.LevelId} (unsaved)";
            if (!InWaveScope())
            {
                return dirty ? name + " *" : name;
            }

            var elevator = draft.Elevators[scopeElevator];
            return $"{name} ▸ Elevator {elevator.Id} ▸ Wave {scopeWave + 1}";
        }

        // -- tool row + shape palette ----------------------------

        private void DrawToolRow()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            foreach (EditorTool candidate in Enum.GetValues(typeof(EditorTool)))
            {
                if (InWaveScope() && candidate != EditorTool.Select && candidate != EditorTool.Block)
                {
                    continue;
                }

                var pressed = GUILayout.Toggle(tool == candidate, candidate.ToString(), EditorStyles.toolbarButton);
                if (pressed && tool != candidate)
                {
                    tool = candidate;
                    freeCells.Clear();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // A3: the shape palette is its own row directly under the tool row, shown
        // only while the tool it governs is active.
        private void DrawShapePaletteRow()
        {
            if (tool != EditorTool.Block)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Shape", EditorStyles.miniLabel, GUILayout.Width(40));

            var picked = (ShapePreset)EditorGUILayout.EnumPopup(shape, EditorStyles.toolbarPopup, GUILayout.Width(120));
            if (picked != shape)
            {
                shape = picked;
                if (shape != ShapePreset.Free)
                {
                    freeCells.Clear();
                }
            }

            // A live preview of the currently selected shape — replaces relying
            // on the enum's text name alone to tell a shape's orientation apart,
            // which the four L rotations especially need. Free previews the
            // cells clicked so far, the same way the queue entry's does.
            var previewSide = PaletteShapePreviewCells * EditorGrid.PreviewCellSize;
            var previewRect = GUILayoutUtility.GetRect(previewSide, previewSide, GUILayout.Width(previewSide));
            var previewCells = shape == ShapePreset.Free ? freeCells : ShapePresets.Cells(shape);
            EditorGrid.DrawCellPreview(previewRect, previewCells, PreviewFillColor);

            if (shape == ShapePreset.Free)
            {
                GUILayout.Label("click cells, then place", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(freeCells.Count == 0))
                {
                    if (GUILayout.Button($"Place ({freeCells.Count})", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    {
                        PlaceFreeBlock();
                        GUIUtility.ExitGUI();
                    }

                    // Item 5: discards the pending cells instead of committing
                    // them. Escape does the same thing (HandleGlobalKeyboardShortcuts)
                    // when no drag is live.
                    if (GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        CancelFreeDraw();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // -- grid column --------------------------------------

        private void DrawGridColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));

            // The canvas takes all the width the properties column leaves and
            // whatever height the enclosing scroll view has left over once its
            // other content — the properties column and the footer — has its
            // own natural or floored size (item 4). EditorGridLayout centres the
            // (capped) grid inside whatever it is handed, both axes.
            var rect = GUILayoutUtility.GetRect(
                200f, settings.CanvasMinHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.18f));

            var padded = new Rect(rect.x + 24f, rect.y + 24f, rect.width - 48f, rect.height - 48f);
            int columns;
            int rows;
            GridBounds(out columns, out rows);
            var layout = new EditorGridLayout(padded, columns, rows);

            EditorGrid.DrawCells(layout, FillOf);
            DrawBlockOutlines(layout);

            if (!InWaveScope())
            {
                DrawRegionBorders(layout);
            }

            DrawSelectionOutline(layout);

            if (tool == EditorTool.Block && shape == ShapePreset.Free)
            {
                foreach (var c in freeCells)
                {
                    OutlineCellIfInside(layout, c, new Color(0.4f, 0.9f, 0.5f));
                }
            }

            if (!InWaveScope())
            {
                DrawEdgeMarkers(layout);
            }

            // Last, so a drag preview is never hidden by what it is being
            // dragged over. That matters most for an edge feature: the candidate
            // a designer must see refused is precisely the one landing on
            // another marker, which would otherwise be drawn on top of it.
            DrawDragPreview(layout);

            HandleGridInput(layout);

            EditorGUILayout.EndVertical();
        }

        private void GridBounds(out int columns, out int rows)
        {
            if (!InWaveScope())
            {
                columns = draft.Width;
                rows = draft.Height;
                return;
            }

            var elevator = draft.Elevators[scopeElevator];
            columns = Math.Max(1, elevator.Max.X - elevator.Min.X + 1);
            rows = Math.Max(1, elevator.Max.Y - elevator.Min.Y + 1);
        }

        private Color FillOf(Coord cell)
        {
            var background = new Color(0.22f, 0.22f, 0.25f);

            if (InWaveScope())
            {
                var wave = draft.Elevators[scopeElevator].Waves[scopeWave];
                foreach (var block in wave.Blocks)
                {
                    if (block.RegionOrigin.HasValue && DraftHitTest.Covers(block.RegionOrigin.Value, block.Cells, cell))
                    {
                        return Palette(FirstColor(block.ColorStack));
                    }
                }

                return background;
            }

            if (draft.StaticWalls.Contains(cell))
            {
                return new Color(0.08f, 0.08f, 0.09f);
            }

            foreach (var block in draft.Blocks)
            {
                if (DraftHitTest.Covers(block.StartOrigin, block.Cells, cell))
                {
                    return Palette(FirstColor(block.ColorStack));
                }
            }

            foreach (var shutter in draft.Shutters)
            {
                if (DraftHitTest.InRegion(shutter.Min, shutter.Max, cell))
                {
                    return Color.Lerp(background, ShutterTint, 0.5f);
                }
            }

            foreach (var elevator in draft.Elevators)
            {
                if (DraftHitTest.InRegion(elevator.Min, elevator.Max, cell))
                {
                    return Color.Lerp(background, ElevatorTint, 0.45f);
                }
            }

            return background;
        }

        private static readonly Color ShutterTint = new Color(0.42f, 0.58f, 0.95f);
        private static readonly Color ElevatorTint = new Color(0.95f, 0.66f, 0.24f);

        /// <summary>The stroke along a block's own outer edge — darker and thicker than <c>EditorGrid</c>'s cell lines, which run between every pair of cells including two inside one block.</summary>
        private static readonly Color BlockBoundaryColor = new Color(0f, 0f, 0f, 0.8f);

        private const float BlockBoundaryThickness = 1.5f;

        /// <summary>
        /// Makes each block read as one shape. <see cref="FillOf"/> colours a
        /// cell by its block's colour and has no way to say where one block
        /// stops, and <see cref="EditorGrid.DrawCells"/> rules a faint line
        /// between every pair of cells without knowing which block either
        /// belongs to. So two things are needed, in this order: the grid line at
        /// a seam <em>inside</em> a block is painted out, and a bold stroke is
        /// added at the block's true outer edge. Both sets come from
        /// <see cref="BlockOutline"/>, computed from the footprint itself
        /// (docs/Modules/09a).
        /// </summary>
        /// <remarks>
        /// The two passes are global rather than per block — every erasure, then
        /// every stroke — because a draft may hold two overlapping blocks, and
        /// one block's erasure must not land on another block's finished stroke.
        /// </remarks>
        private void DrawBlockOutlines(EditorGridLayout layout)
        {
            var footprints = ScopeFootprints();

            foreach (var footprint in footprints)
            {
                EraseInteriorSeams(layout, footprint.Cells, footprint.Origin, footprint.Fill);
            }

            foreach (var footprint in footprints)
            {
                DrawFootprintBoundary(layout, footprint.Cells, footprint.Origin);
            }
        }

        /// <summary>
        /// Every block drawn in the current scope, with the fill
        /// <see cref="FillOf"/> gave it. Gathered once so the erasure and stroke
        /// passes cannot disagree about which blocks are on screen.
        /// </summary>
        private List<(IReadOnlyList<Coord> Cells, Coord Origin, Color Fill)> ScopeFootprints()
        {
            var footprints = new List<(IReadOnlyList<Coord> Cells, Coord Origin, Color Fill)>();

            if (InWaveScope())
            {
                foreach (var block in draft.Elevators[scopeElevator].Waves[scopeWave].Blocks)
                {
                    if (block.RegionOrigin.HasValue)
                    {
                        footprints.Add((block.Cells, block.RegionOrigin.Value, Palette(FirstColor(block.ColorStack))));
                    }
                }

                return footprints;
            }

            foreach (var block in draft.Blocks)
            {
                footprints.Add((block.Cells, block.StartOrigin, Palette(FirstColor(block.ColorStack))));
            }

            return footprints;
        }

        /// <summary>
        /// Paints out the grid line at every seam that falls inside one block,
        /// in that block's own fill colour, so an L or a 1x3 reads as one shape
        /// instead of two or three.
        /// </summary>
        /// <remarks>
        /// Both cells either side of an interior seam repaint it, and only one
        /// of the two actually covers the original line:
        /// <see cref="EditorGrid.DrawCells"/> draws a cell's own top and left
        /// lines only, so the seam between cells A and B lives at whichever of
        /// them owns that side — B's left line, never A's right. A's repaint
        /// lands one pixel column short of it, on a pixel that was already
        /// correct. That is harmless here purely because A and B belong to the
        /// same block and so share its fill colour: the no-op is painting the
        /// right colour over the right colour, not painting nothing. Anything
        /// reusing this where the two sides may differ in colour would have to
        /// paint only the side that owns the line.
        /// </remarks>
        private static void EraseInteriorSeams(
            EditorGridLayout layout, IReadOnlyList<Coord> cells, Coord origin, Color fill)
        {
            foreach (var interior in BlockOutline.InteriorEdges(cells))
            {
                if (!TryCellRect(layout, origin + interior.Cell, out var r))
                {
                    continue;
                }

                EditorGUI.DrawRect(SideRect(r, interior.Side, EditorGrid.LineThickness), fill);
            }
        }

        private static void DrawFootprintBoundary(EditorGridLayout layout, IReadOnlyList<Coord> cells, Coord origin)
        {
            foreach (var boundary in BlockOutline.Edges(cells))
            {
                if (!TryCellRect(layout, origin + boundary.Cell, out var r))
                {
                    continue;
                }

                EditorGUI.DrawRect(SideRect(r, boundary.Side, BlockBoundaryThickness), BlockBoundaryColor);
            }
        }

        /// <summary>The screen rect of a cell, or false when it falls off the grid — a draft may hold a block partly outside, and only its on-screen part draws.</summary>
        private static bool TryCellRect(EditorGridLayout layout, Coord cell, out Rect rect)
        {
            if (cell.X < 0 || cell.X >= layout.Columns || cell.Y < 0 || cell.Y >= layout.Rows)
            {
                rect = default;
                return false;
            }

            rect = layout.CellRect(cell);
            return true;
        }

        /// <summary>
        /// A band of <paramref name="thickness"/> pixels along one side of a
        /// cell, always within the cell's own rect. Shared by the interior
        /// erasure and the boundary stroke so the two cannot end up describing
        /// the same side differently.
        /// </summary>
        private static Rect SideRect(Rect cell, Direction side, float thickness)
        {
            switch (side)
            {
                // +Y is up in cell space and down in GUI space, so Up is the
                // rect's top edge — the same flip EditorGridLayout.CellRect
                // makes, and the only place it matters here.
                case Direction.Up:
                    return new Rect(cell.x, cell.y, cell.width, thickness);
                case Direction.Down:
                    return new Rect(cell.x, cell.yMax - thickness, cell.width, thickness);
                case Direction.Left:
                    return new Rect(cell.x, cell.y, thickness, cell.height);
                default:
                    return new Rect(cell.xMax - thickness, cell.y, thickness, cell.height);
            }
        }

        private void DrawSelectionOutline(EditorGridLayout layout)
        {
            var color = new Color(1f, 0.85f, 0.2f);

            switch (selection)
            {
                case BlockDraft block when dragKind != DragKind.BoardBlock || !ReferenceEquals(block, dragBoardBlock):
                    foreach (var c in block.Cells)
                    {
                        OutlineCellIfInside(layout, block.StartOrigin + c, color);
                    }

                    break;
                case ShutterDraft shutter when dragKind != DragKind.RegionMove || !ReferenceEquals(shutter, dragShutter):
                    OutlineRegionRect(layout, shutter.Min, shutter.Max, color, 2f);
                    break;
                case ElevatorDraft elevator when dragKind != DragKind.RegionMove || !ReferenceEquals(elevator, dragElevator):
                    OutlineRegionRect(layout, elevator.Min, elevator.Max, color, 2f);
                    break;
            }
        }

        // Session B, Part 2: the live drag preview. The draft is untouched while
        // a drag is in progress (DraftDrag applies it only on a legal mouse-up),
        // so this is the only place a candidate position is ever visible.
        private void DrawDragPreview(EditorGridLayout layout)
        {
            switch (dragKind)
            {
                case DragKind.BoardBlock:
                    DrawBlockPreview(layout, dragBoardBlock.Cells, dragCandidateOrigin, dragCandidateLegal);
                    break;

                case DragKind.WaveBlock:
                    DrawBlockPreview(layout, dragWaveBlockTarget.Cells, dragCandidateOrigin, dragCandidateLegal);
                    break;

                case DragKind.RegionMove:
                case DragKind.RegionCreate:
                    var tint = dragRegionTool == EditorTool.Shutter ? ShutterTint : ElevatorTint;
                    OutlineRegionRect(layout, dragRegionCandidateMin, dragRegionCandidateMax, Color.Lerp(tint, Color.white, 0.4f), 2.5f);
                    break;

                case DragKind.EdgeFeature:
                {
                    // Drawn in lane 0, over whatever sits there: the preview is
                    // where the feature would land, and a candidate that lands on
                    // a neighbour is illegal anyway and reads red.
                    var span = EdgeFeatures.Of(dragEdgeFeature).Value;
                    var candidate = new EdgeFeatureSpan(
                        span.Owner, dragEdgeCandidateEdge, dragEdgeCandidateOffset, span.Width);
                    EditorGUI.DrawRect(
                        EdgeMarkerRect(layout, candidate, 0),
                        dragEdgeCandidateLegal ? new Color(0.4f, 0.95f, 0.5f, 0.75f) : new Color(0.95f, 0.3f, 0.3f, 0.75f));
                    break;
                }
            }
        }

        private static void DrawBlockPreview(EditorGridLayout layout, IReadOnlyList<Coord> cells, Coord origin, bool legal)
        {
            var fill = legal ? new Color(0.4f, 0.95f, 0.5f, 0.55f) : new Color(0.95f, 0.3f, 0.3f, 0.6f);
            foreach (var relative in cells)
            {
                var cell = origin + relative;
                if (cell.X < 0 || cell.X >= layout.Columns || cell.Y < 0 || cell.Y >= layout.Rows)
                {
                    continue; // an illegal candidate can sit outside the grid; only its on-screen part draws
                }

                EditorGUI.DrawRect(layout.CellRect(cell), fill);
            }
        }

        // A4: a gate's marker is a coloured bar and a generator's is a triangle
        // pointing inward in a neutral colour — a generator's queue can be any
        // mix of colours, so none of them is "its" colour. Functional, not final
        // art. Both kinds draw in one pass because lanes (below) are assigned
        // across all edge features at once: a gate and a generator can contend
        // for the same edge cells exactly as two generators can (M6).
        private void DrawEdgeMarkers(EditorGridLayout layout)
        {
            var neutral = new Color(0.78f, 0.78f, 0.82f);
            var spans = EdgeFeatures.Enumerate(draft);
            var lanes = EdgeFeatures.Lanes(spans);

            for (var i = 0; i < spans.Count; i++)
            {
                var rect = EdgeMarkerRect(layout, spans[i], lanes[i]);

                if (spans[i].Owner is GateDraft gate)
                {
                    EditorGUI.DrawRect(rect, GateMarker.Fill(Palette(gate.Color), gate.OpenAtClearCount));
                    if (GateMarker.IsThresholdGated(gate.OpenAtClearCount))
                    {
                        DrawMarkerLabel(rect, gate.OpenAtClearCount.Value.ToString());
                    }
                }
                else
                {
                    DrawInwardTriangle(rect, spans[i].Edge, neutral);
                }

                if (ReferenceEquals(selection, spans[i].Owner))
                {
                    EditorGrid.DrawOutline(rect, Color.white, 1.5f);
                }
            }
        }

        // D34: a marker spans its feature's authored Width. The generator's was
        // previously derived from the widest queued block, which D34 rejected —
        // there was nothing for the designer to set, nothing for the validator to
        // warn about, and the marker grew silently as the queue changed. A queue
        // entry too wide for this span is a warning
        // (GeneratorTooNarrowForQueuedBlock), not a reason to redraw the edge.
        //
        // The lane pushes a marker further from the grid when it shares edge
        // cells with one drawn before it. A draft is allowed to hold that
        // overlap — typing a wider Width into the properties panel authors one,
        // and Core rejects it only once ToContext runs — so without lanes the
        // wider marker simply covers its neighbour and the designer cannot see
        // what they did (docs/Modules/09a).
        private static Rect EdgeMarkerRect(EditorGridLayout layout, EdgeFeatureSpan span, int lane)
        {
            var depth = MarkerDepth(layout, span.Owner);
            var rect = EditorGrid.EdgeMarker(layout, span.Edge, span.Offset, span.Width, depth);
            return lane == 0 ? rect : PushedOutward(rect, span.Edge, lane * depth);
        }

        private static float MarkerDepth(EditorGridLayout layout, object owner) =>
            owner is GateDraft
                ? Mathf.Max(4f, layout.CellSize * 0.35f)
                : Mathf.Max(6f, layout.CellSize * 0.5f);

        /// <summary>Moves a marker rect away from the grid, perpendicular to the edge it hangs off.</summary>
        private static Rect PushedOutward(Rect r, BoardEdge edge, float distance)
        {
            switch (edge)
            {
                case BoardEdge.Bottom:
                    return new Rect(r.x, r.y + distance, r.width, r.height);
                case BoardEdge.Top:
                    return new Rect(r.x, r.y - distance, r.width, r.height);
                case BoardEdge.Left:
                    return new Rect(r.x - distance, r.y, r.width, r.height);
                default:
                    return new Rect(r.x + distance, r.y, r.width, r.height);
            }
        }

        // The label only ever sits on GateMarker.Frost, a fixed near-white, so
        // plain dark text reads without an outline.
        private static readonly Color MarkerLabelColor = new Color(0.12f, 0.14f, 0.2f);
        private const float MarkerLabelSizeRatio = 0.7f;
        private const int MarkerLabelMinFontSize = 7;

        private static GUIStyle markerLabelStyle;

        /// <summary>
        /// Draws <paramref name="text"/> centred on a marker rect, sized to the
        /// rect's short side — a Left/Right gate is only its depth wide. Skipped
        /// when that size would be unreadable; the fill alone still carries the
        /// signal at small cell sizes.
        /// </summary>
        private static void DrawMarkerLabel(Rect r, string text)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var fontSize = Mathf.RoundToInt(Mathf.Min(r.width, r.height) * MarkerLabelSizeRatio);
            if (fontSize < MarkerLabelMinFontSize)
            {
                return;
            }

            // Built lazily: EditorStyles is only available inside a GUI pass.
            if (markerLabelStyle == null)
            {
                markerLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                markerLabelStyle.normal.textColor = MarkerLabelColor;
            }

            markerLabelStyle.fontSize = fontSize;
            GUI.Label(r, text, markerLabelStyle);
        }

        private static void DrawInwardTriangle(Rect r, BoardEdge edge, Color color)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Vector3 a;
            Vector3 b;
            Vector3 apex;
            switch (edge)
            {
                case BoardEdge.Bottom:
                    a = new Vector3(r.xMin, r.yMax);
                    b = new Vector3(r.xMax, r.yMax);
                    apex = new Vector3(r.center.x, r.yMin);
                    break;
                case BoardEdge.Top:
                    a = new Vector3(r.xMin, r.yMin);
                    b = new Vector3(r.xMax, r.yMin);
                    apex = new Vector3(r.center.x, r.yMax);
                    break;
                case BoardEdge.Left:
                    a = new Vector3(r.xMin, r.yMin);
                    b = new Vector3(r.xMin, r.yMax);
                    apex = new Vector3(r.xMax, r.center.y);
                    break;
                default:
                    a = new Vector3(r.xMax, r.yMin);
                    b = new Vector3(r.xMax, r.yMax);
                    apex = new Vector3(r.xMin, r.center.y);
                    break;
            }

            var previous = Handles.color;
            Handles.color = color;
            Handles.DrawAAConvexPolygon(a, b, apex);
            Handles.color = previous;
        }

        // A4: a thin border in each region's own hue, always drawn, so a region's
        // extent reads even under a block or where two regions overlap.
        private void DrawRegionBorders(EditorGridLayout layout)
        {
            foreach (var shutter in draft.Shutters)
            {
                OutlineRegionRect(layout, shutter.Min, shutter.Max, ShutterTint, 1f);
            }

            foreach (var elevator in draft.Elevators)
            {
                OutlineRegionRect(layout, elevator.Min, elevator.Max, ElevatorTint, 1f);
            }
        }

        // -- grid input routing (places things; decides nothing) --

        private void HandleGridInput(EditorGridLayout layout)
        {
            // Requested every OnGUI pass, unconditionally, so IMGUI's control-ID
            // bookkeeping stays consistent across the Layout and non-Layout event
            // passes — the same reason every other custom-drag control in the
            // Unity editor calls GetControlID up front rather than only when a
            // drag happens to be live.
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;

            if (dragKind != DragKind.None && GUIUtility.hotControl == controlId)
            {
                // rawType, not type: once the pointer leaves the window, IMGUI
                // reports every mouse event as Ignore — type — while rawType
                // still says what it actually was. Without this a drag released
                // (or even just continued) outside the window never sees another
                // MouseDrag or MouseUp: the preview freezes at the last in-window
                // position and dragKind is never cleared.
                if (e.rawType == EventType.MouseDrag)
                {
                    UpdateDrag(layout, e.mousePosition);
                    e.Use();
                    return;
                }

                if (e.rawType == EventType.MouseUp)
                {
                    EndDrag();
                    GUIUtility.hotControl = 0;
                    e.Use();
                    GUIUtility.ExitGUI();
                    return;
                }

                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    CancelDrag();
                    GUIUtility.hotControl = 0;
                    e.Use();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            if (e.type != EventType.MouseDown || e.button != 0)
            {
                return;
            }

            if (!InWaveScope() && TrySelectEdgeMarker(layout, e.mousePosition, controlId))
            {
                e.Use();
                // Selecting changes which inspector the properties column draws,
                // so restart the layout pass cleanly. ExitGUI throws; the return
                // is for the reader, not the control flow.
                GUIUtility.ExitGUI();
                return;
            }

            if (!layout.TryPick(e.mousePosition, out var cell))
            {
                return;
            }

            if (TryBeginDrag(cell, controlId))
            {
                e.Use();
                // TryBeginDrag also selects (the grabbed block or region), which
                // changes what the properties column draws next — restart the
                // layout pass cleanly, the same reason the edge-marker branch
                // above does.
                GUIUtility.ExitGUI();
                return;
            }

            if (InWaveScope())
            {
                HandleWaveClick(cell);
            }
            else
            {
                HandleBoardClick(cell);
            }

            e.Use();
            GUIUtility.ExitGUI();
        }

        /// <summary>
        /// Whether a mouse-down at <paramref name="cell"/> begins a drag: grabbing
        /// an existing block (Select, Block or Wall), or grabbing/creating a
        /// shutter or elevator region (Shutter, Elevator). The press always
        /// selects immediately; whether anything actually moves is decided at
        /// <see cref="EndDrag"/>, by whether the candidate ever differed from
        /// where the drag started — so a press-and-release with no movement is a
        /// plain selection, on both a block and a region (docs/Modules/09a,
        /// Session B).
        /// </summary>
        private bool TryBeginDrag(Coord cell, int controlId)
        {
            if (InWaveScope())
            {
                if (tool != EditorTool.Select && tool != EditorTool.Block)
                {
                    return false;
                }

                var wave = draft.Elevators[scopeElevator].Waves[scopeWave];
                var block = wave.Blocks.FirstOrDefault(
                    b => b.RegionOrigin.HasValue && DraftHitTest.Covers(b.RegionOrigin.Value, b.Cells, cell));
                if (block == null)
                {
                    return false;
                }

                selection = block;
                dragKind = DragKind.WaveBlock;
                dragWaveBlockTarget = block;
                dragGrabOffset = cell - block.RegionOrigin.Value;
                dragCandidateOrigin = block.RegionOrigin.Value;
                dragCandidateLegal = true;
                GUIUtility.hotControl = controlId;
                return true;
            }

            var routing = DraftClickRouting.Route(draft, cell, tool);

            if (tool == EditorTool.Select || tool == EditorTool.Block || tool == EditorTool.Wall)
            {
                if (routing.SelectsExisting && routing.Target is BlockDraft block)
                {
                    BeginBoardBlockDrag(block, cell, controlId);
                    return true;
                }

                // Select is the default tool and drags whatever a press lands
                // on — a block above, or, failing that, a region: Route's own
                // block-first precedence already means a cell covered by both
                // reaches the branch above instead, so this cannot double up
                // with it. Block and Wall never drag a region; only Select and
                // the region tools themselves do.
                if (tool == EditorTool.Select && routing.SelectsExisting
                    && (routing.Target is ShutterDraft || routing.Target is ElevatorDraft))
                {
                    BeginRegionMoveDrag(routing.Target, cell, controlId);
                    return true;
                }

                return false;
            }

            if (tool == EditorTool.Shutter || tool == EditorTool.Elevator)
            {
                if (routing.SelectsExisting)
                {
                    BeginRegionMoveDrag(routing.Target, cell, controlId);
                }
                else
                {
                    dragRegionTool = tool;
                    dragRegionAnchorCell = cell;
                    dragShutter = null;
                    dragElevator = null;
                    dragKind = DragKind.RegionCreate;
                    dragRegionOriginalMin = cell;
                    dragRegionOriginalMax = cell;
                    dragRegionCandidateMin = cell;
                    dragRegionCandidateMax = cell;
                    GUIUtility.hotControl = controlId;
                }

                return true;
            }

            // Gate and Generator: a press on a grid cell places a feature on the
            // nearest edge and never drags. Dragging an existing one starts from
            // its marker instead, which sits outside the grid and is handled by
            // TrySelectEdgeMarker before this method is reached.
            return false;
        }

        private void BeginBoardBlockDrag(BlockDraft block, Coord cell, int controlId)
        {
            selection = block;
            dragKind = DragKind.BoardBlock;
            dragBoardBlock = block;
            dragGrabOffset = cell - block.StartOrigin;
            dragCandidateOrigin = block.StartOrigin;
            dragCandidateLegal = true;
            GUIUtility.hotControl = controlId;
        }

        /// <summary>
        /// Begins moving an existing shutter or elevator region — reached both
        /// from the Shutter/Elevator tools' own mouse-down and, per item 2, from
        /// Select's (a press on a region it does not cover with a block starts a
        /// move the same way).
        /// </summary>
        private void BeginRegionMoveDrag(object region, Coord cell, int controlId)
        {
            dragShutter = region as ShutterDraft;
            dragElevator = region as ElevatorDraft;
            dragRegionTool = dragShutter != null ? EditorTool.Shutter : EditorTool.Elevator;
            dragRegionAnchorCell = cell;
            dragKind = DragKind.RegionMove;
            dragRegionOriginalMin = dragShutter != null ? dragShutter.Min : dragElevator.Min;
            dragRegionOriginalMax = dragShutter != null ? dragShutter.Max : dragElevator.Max;
            dragRegionCandidateMin = dragRegionOriginalMin;
            dragRegionCandidateMax = dragRegionOriginalMax;
            selection = region;
            GUIUtility.hotControl = controlId;
        }

        private void UpdateDrag(EditorGridLayout layout, Vector2 mousePosition)
        {
            var pointerCell = layout.CellAtUnclamped(mousePosition);

            switch (dragKind)
            {
                case DragKind.BoardBlock:
                    dragCandidateOrigin = DraftDrag.CandidateOrigin(pointerCell, dragGrabOffset);
                    dragCandidateLegal = DraftDrag.IsLegalOnBoard(draft, dragBoardBlock, dragCandidateOrigin);
                    break;

                case DragKind.WaveBlock:
                {
                    var elevator = draft.Elevators[scopeElevator];
                    var wave = elevator.Waves[scopeWave];
                    dragCandidateOrigin = DraftDrag.CandidateOrigin(pointerCell, dragGrabOffset);
                    dragCandidateLegal = DraftDrag.IsLegalInWave(elevator, wave, dragWaveBlockTarget, dragCandidateOrigin);
                    break;
                }

                case DragKind.RegionCreate:
                    (dragRegionCandidateMin, dragRegionCandidateMax) =
                        DraftDrag.RegionCreateRect(dragRegionAnchorCell, pointerCell, draft.Width, draft.Height);
                    break;

                case DragKind.RegionMove:
                    (dragRegionCandidateMin, dragRegionCandidateMax) = DraftDrag.RegionMoveRect(
                        dragRegionOriginalMin, dragRegionOriginalMax, dragRegionAnchorCell, pointerCell, draft.Width, draft.Height);
                    break;

                case DragKind.EdgeFeature:
                {
                    // Re-asked every move rather than fixed at the press: the
                    // same nearest-edge rule that decides where a fresh gate or
                    // generator is placed also decides where a dragged one lands,
                    // so the two behave alike and the tie-break lives in one place.
                    dragEdgeCandidateEdge = EdgeFeatures.NearestEdge(draft, pointerCell);
                    var alongEdge = EdgeFeatures.AlongEdge(dragEdgeCandidateEdge, pointerCell);
                    dragEdgeCandidateOffset = DraftDrag.CandidateEdgeOffset(alongEdge, dragEdgeGrabOffset);
                    dragEdgeCandidateLegal = DraftDrag.IsLegalEdgeFeature(
                        draft, dragEdgeFeature, dragEdgeCandidateEdge, dragEdgeCandidateOffset);
                    break;
                }
            }

            Repaint();
        }

        private void EndDrag()
        {
            switch (dragKind)
            {
                case DragKind.BoardBlock:
                    if (dragCandidateOrigin != dragBoardBlock.StartOrigin
                        && DraftDrag.TryApplyBoard(draft, dragBoardBlock, dragCandidateOrigin))
                    {
                        Mutated();
                    }

                    break;

                case DragKind.WaveBlock:
                {
                    var elevator = draft.Elevators[scopeElevator];
                    var wave = elevator.Waves[scopeWave];
                    if (dragCandidateOrigin != dragWaveBlockTarget.RegionOrigin.Value
                        && DraftDrag.TryApplyWave(elevator, wave, dragWaveBlockTarget, dragCandidateOrigin))
                    {
                        Mutated();
                    }

                    break;
                }

                case DragKind.RegionMove:
                    if (dragRegionCandidateMin != dragRegionOriginalMin || dragRegionCandidateMax != dragRegionOriginalMax)
                    {
                        if (dragShutter != null)
                        {
                            dragShutter.Min = dragRegionCandidateMin;
                            dragShutter.Max = dragRegionCandidateMax;
                        }
                        else if (dragElevator != null)
                        {
                            dragElevator.Min = dragRegionCandidateMin;
                            dragElevator.Max = dragRegionCandidateMax;
                        }

                        Mutated();
                    }

                    break;

                case DragKind.RegionCreate:
                    if (dragRegionTool == EditorTool.Shutter)
                    {
                        AddShutter(dragRegionCandidateMin, dragRegionCandidateMax);
                    }
                    else
                    {
                        AddElevator(dragRegionCandidateMin, dragRegionCandidateMax);
                    }

                    break;

                case DragKind.EdgeFeature:
                {
                    var current = EdgeFeatures.Of(dragEdgeFeature).Value;
                    var moved = dragEdgeCandidateEdge != current.Edge || dragEdgeCandidateOffset != current.Offset;
                    if (moved && DraftDrag.TryApplyEdgeFeature(
                            draft, dragEdgeFeature, dragEdgeCandidateEdge, dragEdgeCandidateOffset))
                    {
                        Mutated();
                    }

                    break;
                }
            }

            dragKind = DragKind.None;
            Repaint();
        }

        /// <summary>Escape cancels a drag. The draft was never touched, so there is nothing to revert — only the local candidate state to discard.</summary>
        private void CancelDrag()
        {
            dragKind = DragKind.None;
            Repaint();
        }

        /// <summary>
        /// Items 5 and 6: Escape cancels a pending free-draw selection and
        /// Delete removes the current selection, called once per frame from the
        /// end of <see cref="OnGUI"/>. Session C, follow-up 2: Enter (either the
        /// main-row or numpad key) commits a pending free-draw selection through
        /// the same <see cref="PlaceFreeBlock"/> the Place button calls, so
        /// validity is handled in one place rather than duplicated here. Guarded
        /// by <see cref="EditorGUIUtility.editingTextField"/> so typing in a
        /// properties field — deleting a character while editing a threshold, or
        /// pressing Enter to commit a text field's own value — never reaches the
        /// selection or the free-draw pending cells instead of the field.
        /// A live drag's own Escape handling in <see cref="HandleGridInput"/>
        /// runs earlier in the same event and consumes it via
        /// <see cref="GUIUtility.ExitGUI"/>, which unwinds the rest of this
        /// frame's <see cref="OnGUI"/> before this method would run — so "a live
        /// drag wins" needs no explicit ordering here.
        /// </summary>
        private void HandleGlobalKeyboardShortcuts()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
            {
                return;
            }

            if (e.keyCode == KeyCode.Escape && freeCells.Count > 0)
            {
                CancelFreeDraw();
                e.Use();
                return;
            }

            if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && freeCells.Count > 0)
            {
                PlaceFreeBlock();
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Delete && selection != null)
            {
                DeleteSelection();
                e.Use();
                GUIUtility.ExitGUI();
            }

            // Session C (docs/Modules/09a): editor-only undo/redo over LevelDto
            // snapshots. Guarded by the same editingTextField check above so a
            // field's own native text-undo keeps working while it has focus.
            var modifier = e.control || e.command;
            if (modifier && e.keyCode == KeyCode.Z && !e.shift)
            {
                PerformUndo();
                e.Use();
                return;
            }

            if (modifier && (e.keyCode == KeyCode.Y || (e.keyCode == KeyCode.Z && e.shift)))
            {
                PerformRedo();
                e.Use();
            }
        }

        private void PerformUndo()
        {
            var target = history.Undo();
            if (target != null)
            {
                ApplyHistorySnapshot(target);
            }
        }

        private void PerformRedo()
        {
            var target = history.Redo();
            if (target != null)
            {
                ApplyHistorySnapshot(target);
            }
        }

        /// <summary>
        /// Rebuilds the draft from an undo/redo snapshot and clears every window
        /// field that held a reference into the draft <see cref="LevelDraft.FromDto"/>
        /// just replaced (docs/Modules/09a, Session C): the live drag, the
        /// free-draw staging cells, and the queue entries the designer had put
        /// into Free mode. The selection is the one field that is not simply
        /// dropped: undoing a properties-panel edit is the common case, and the
        /// edited object is still there — only clearing the selection would blank
        /// the panel and hide exactly what was just reverted. A
        /// <see cref="SelectionKey"/> captured from the old selection before the
        /// rebuild is resolved against the new draft afterward, so the selection
        /// carries over when the equivalent object survives and clears only when
        /// it does not (docs/Modules/09a, Session C, follow-up 1).
        /// <see cref="scopeElevator"/> and <see cref="scopeWave"/> are left alone
        /// — they are indices, not references, and <see cref="InWaveScope"/>
        /// already falls back to the board if the rebuilt draft no longer has
        /// that elevator or wave.
        /// </summary>
        private void ApplyHistorySnapshot(LevelDto dto)
        {
            var selectionKey = SelectionKey.Capture(selection, CurrentScopeWave());

            draft = LevelDraft.FromDto(dto);
            selection = selectionKey.Resolve(draft, CurrentScopeWave());
            dragKind = DragKind.None;
            freeCells.Clear();
            queueEntriesInFreeMode.Clear();
            dirty = true;
            DiscardSolve();
            SyncGridFields();
            Revalidate();
            Repaint();
        }

        /// <summary>
        /// Gate and generator markers sit on the edge, outside the grid, and are
        /// picked by their own rects — the same rects
        /// <see cref="DrawEdgeMarkers"/> draws, lane offsets included, so a
        /// marker pushed out to stay visible is still picked where it is seen.
        /// A press both selects and begins a drag along the edge, mirroring
        /// <see cref="TryBeginDrag"/> on a board block: whether anything
        /// actually moves is decided at <see cref="EndDrag"/>, so a
        /// press-and-release with no movement is a plain selection.
        /// </summary>
        private bool TrySelectEdgeMarker(EditorGridLayout layout, Vector2 mouse, int controlId)
        {
            var spans = EdgeFeatures.Enumerate(draft);
            var lanes = EdgeFeatures.Lanes(spans);

            for (var i = 0; i < spans.Count; i++)
            {
                if (!EdgeMarkerRect(layout, spans[i], lanes[i]).Contains(mouse))
                {
                    continue;
                }

                selection = spans[i].Owner;
                BeginEdgeFeatureDrag(layout, spans[i], mouse, controlId);
                return true;
            }

            return false;
        }

        private void BeginEdgeFeatureDrag(EditorGridLayout layout, EdgeFeatureSpan span, Vector2 mouse, int controlId)
        {
            // The marker hangs outside the grid, so the pointer's cell is off the
            // board on the axis perpendicular to the edge — CellAtUnclamped
            // extrapolates rather than failing, and only the along-edge axis is
            // read here, which is exactly the one that is still meaningful.
            var alongEdge = EdgeFeatures.AlongEdge(span.Edge, layout.CellAtUnclamped(mouse));

            dragKind = DragKind.EdgeFeature;
            dragEdgeFeature = span.Owner;

            // Clamped into the span because the grab offset is an index within
            // the marker, and it has to stay one: EditorGrid.EdgeMarker clamps
            // the rect it draws for a feature whose data does not fit its edge,
            // so a press on such a marker can otherwise yield an index outside
            // the span — which would then ride along onto every other edge the
            // drag visits.
            dragEdgeGrabOffset = Mathf.Clamp(alongEdge - span.Offset, 0, Mathf.Max(0, span.Width - 1));

            dragEdgeCandidateEdge = span.Edge;
            dragEdgeCandidateOffset = span.Offset;
            dragEdgeCandidateLegal = true;
            GUIUtility.hotControl = controlId;
        }

        // Session B, Part 1: routed through DraftClickRouting.Route, which scopes
        // the "existing thing wins" rule by tool — Select, Block and Wall treat a
        // block as a selection candidate; Gate and Generator never do; Shutter
        // and Elevator are handled entirely by TryBeginDrag (Session B, Part 2)
        // and never reach this method, since every Shutter/Elevator mouse-down
        // either selects an existing region of that kind or begins creating one.
        private void HandleBoardClick(Coord cell)
        {
            var routing = DraftClickRouting.Route(draft, cell, tool);
            if (routing.SelectsExisting)
            {
                selection = routing.Target;
                return;
            }

            switch (tool)
            {
                case EditorTool.Block:
                    PlaceOrSelectBlock(cell);
                    break;

                case EditorTool.Wall:
                    ToggleWall(cell);
                    break;

                case EditorTool.Gate:
                    AddGateOnEdgeNearest(cell);
                    break;

                case EditorTool.Generator:
                    AddGeneratorOnEdgeNearest(cell);
                    break;
            }
        }

        private void HandleWaveClick(Coord cell)
        {
            if (tool == EditorTool.Select)
            {
                var wave = draft.Elevators[scopeElevator].Waves[scopeWave];
                selection = wave.Blocks.FirstOrDefault(
                    b => b.RegionOrigin.HasValue && DraftHitTest.Covers(b.RegionOrigin.Value, b.Cells, cell));
                return;
            }

            PlaceOrSelectBlock(cell);
        }

        /// <summary>
        /// The Block tool's click, shared by the board and a wave (item 2): a
        /// free-draw cell toggles, a preset places — but only where its whole
        /// footprint is clear of other blocks and walls (item 1). A footprint
        /// that overlaps a block selects that block instead of stacking.
        /// </summary>
        private void PlaceOrSelectBlock(Coord cell)
        {
            if (shape == ShapePreset.Free)
            {
                ToggleFreeCell(cell);
                return;
            }

            var footprint = ShapePresets.Cells(shape);
            if (!FootprintClear(cell, footprint, out var occupant))
            {
                selection = occupant; // the block in the way, or null for a wall
                return;
            }

            if (InWaveScope())
            {
                var wave = CurrentScopeWave();
                var spawned = new SpawnedBlockDraft
                {
                    Id = NextId(wave.Blocks.Select(b => b.Id)),
                    Cells = new List<Coord>(footprint),
                    ColorStack = { BlockColor.Red },
                    RegionOrigin = cell,
                };
                wave.Blocks.Add(spawned);
                selection = spawned;
                Mutated();
            }
            else
            {
                AddBlock(cell, footprint);
            }
        }

        /// <summary>
        /// Whether a candidate block footprint at <paramref name="origin"/> lands
        /// clear of every other block (and, on the board, of walls). Reuses the
        /// board's own occupancy test — <see cref="DraftHitTest"/> — and the wave
        /// equivalent over the current wave's blocks.
        /// </summary>
        private bool FootprintClear(Coord origin, IReadOnlyList<Coord> cells, out object occupant)
        {
            occupant = null;
            foreach (var relative in cells)
            {
                if (!CellClearForBlock(origin + relative, out occupant))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether one cell can take part of a new block: inside the grid — or,
        /// in wave scope, inside the elevator's region — and clear of other
        /// blocks and of static walls. The bounds check goes through
        /// <see cref="DraftDrag"/>, which already answers the same question for a
        /// block being moved: a preset footprint reaching past the right edge and
        /// a dragged block reaching past it are one rule, not two (item 6,
        /// docs/Modules/09a). <paramref name="occupant"/> stays null when the
        /// cell is out of bounds or walled — there is nothing there to select.
        /// </summary>
        private bool CellClearForBlock(Coord at, out object occupant)
        {
            occupant = null;

            if (InWaveScope())
            {
                var elevator = draft.Elevators[scopeElevator];
                if (!DraftDrag.IsInsideRegion(elevator, at))
                {
                    return false;
                }

                var wave = elevator.Waves[scopeWave];
                occupant = wave.Blocks.FirstOrDefault(
                    b => b.RegionOrigin.HasValue && DraftHitTest.Covers(b.RegionOrigin.Value, b.Cells, at));
                return occupant == null;
            }

            if (!DraftDrag.IsInsideBoard(draft, at))
            {
                return false;
            }

            var hit = DraftHitTest.PickAt(draft, at);
            if (hit.Kind == DraftHitKind.Block)
            {
                occupant = hit.Target;
                return false;
            }

            return hit.Kind != DraftHitKind.Wall;
        }

        // -- draft mutations (id bookkeeping, list edits — not rules) --

        private void AddBlock(Coord origin, IReadOnlyList<Coord> cells)
        {
            var block = new BlockDraft
            {
                Id = NextId(draft.Blocks.Select(b => b.Id)),
                Cells = new List<Coord>(cells),
                ColorStack = { BlockColor.Red },
                StartOrigin = origin,
            };
            draft.Blocks.Add(block);
            selection = block;
            Mutated();
        }

        // Free draw stays a connected shape (item 3, earlier round): the first
        // cell is unconstrained; a later cell must touch the set orthogonally and
        // land clear of other blocks (item 1); a removal is refused if it would
        // split the remainder. Connectivity is asked of BlockShape — the
        // reporting side of BlockValidation — so a disconnected or overlapping
        // block can never be built here rather than caught later as a warning.
        private void ToggleFreeCell(Coord cell)
        {
            if (freeCells.Contains(cell))
            {
                var remainder = freeCells.Where(c => c != cell).ToList();
                if (remainder.Count == 0 || BlockShape.IsOrthogonallyConnected(remainder))
                {
                    freeCells.Remove(cell);
                }
            }
            else if ((freeCells.Count == 0 || freeCells.Any(c => BlockShape.AreOrthogonallyAdjacent(c, cell)))
                     && CellClearForBlock(cell, out _))
            {
                freeCells.Add(cell);
            }

            Repaint();
        }

        private void PlaceFreeBlock()
        {
            if (freeCells.Count == 0)
            {
                return;
            }

            var min = new Coord(freeCells.Min(c => c.X), freeCells.Min(c => c.Y));
            var normalised = freeCells.Select(c => c - min).ToList();

            if (InWaveScope())
            {
                var wave = CurrentScopeWave();
                var spawned = new SpawnedBlockDraft
                {
                    Id = NextId(wave.Blocks.Select(b => b.Id)),
                    Cells = normalised,
                    ColorStack = { BlockColor.Red },
                    RegionOrigin = min,
                };
                wave.Blocks.Add(spawned);
                selection = spawned;
                Mutated();
            }
            else
            {
                AddBlock(min, normalised);
            }

            freeCells.Clear();
        }

        /// <summary>Item 5: discards a pending free-draw selection without placing it.</summary>
        private void CancelFreeDraw()
        {
            freeCells.Clear();
            Repaint();
        }

        private void ToggleWall(Coord cell)
        {
            if (!draft.StaticWalls.Remove(cell))
            {
                draft.StaticWalls.Add(cell);
            }

            Mutated();
        }

        /// <summary>The width a newly placed gate or generator starts at; the designer widens it in the properties panel.</summary>
        private const int NewEdgeFeatureWidth = 1;

        private void AddGateOnEdgeNearest(Coord cell)
        {
            var edge = EdgeFeatures.NearestEdge(draft, cell);
            var offset = EdgeFeatures.AlongEdge(edge, cell);
            if (EdgeSpanTaken(edge, offset, NewEdgeFeatureWidth))
            {
                return;
            }

            var gate = new GateDraft
            {
                Id = NextId(draft.Gates.Select(g => g.Id)),
                Edge = edge,
                Offset = offset,
                Width = NewEdgeFeatureWidth,
                Color = BlockColor.Red,
            };
            draft.Gates.Add(gate);
            selection = gate;
            Mutated();
        }

        private void AddGeneratorOnEdgeNearest(Coord cell)
        {
            var edge = EdgeFeatures.NearestEdge(draft, cell);
            var offset = EdgeFeatures.AlongEdge(edge, cell);
            if (EdgeSpanTaken(edge, offset, NewEdgeFeatureWidth))
            {
                return;
            }

            var generator = new GeneratorDraft
            {
                Id = NextId(draft.Generators.Select(g => g.Id)),
                Edge = edge,
                Offset = offset,
                Width = NewEdgeFeatureWidth,
            };
            draft.Generators.Add(generator);
            selection = generator;
            Mutated();
        }

        /// <summary>
        /// Whether a gate or generator already holds the edge cells a new one
        /// would take, selecting it if so. M6 is explicit that edge features
        /// never overlap and that an overlap is a level data error, not a
        /// warning — so a click does not author one. Selecting what is in the way
        /// rather than refusing silently mirrors
        /// <see cref="PlaceOrSelectBlock"/>, where a footprint landing on a block
        /// selects that block instead of stacking on it.
        /// </summary>
        private bool EdgeSpanTaken(BoardEdge edge, int offset, int width)
        {
            var blocking = EdgeFeatures.Blocking(draft, edge, offset, width);
            if (blocking == null)
            {
                return false;
            }

            selection = blocking;
            return true;
        }

        // Session B, Part 2: created with the region a drag-draw describes — a
        // plain click (no movement) yields min == max, the same 1x1 region a
        // click alone produced before dragging existed.
        private void AddShutter(Coord min, Coord max)
        {
            var shutter = new ShutterDraft
            {
                Id = NextId(draft.Shutters.Select(s => s.Id)),
                Min = min,
                Max = max,
                Threshold = 1,
            };
            draft.Shutters.Add(shutter);
            selection = shutter;
            Mutated();
        }

        private void AddElevator(Coord min, Coord max)
        {
            var elevator = new ElevatorDraft
            {
                Id = NextId(draft.Elevators.Select(el => el.Id)),
                Min = min,
                Max = max,
            };
            draft.Elevators.Add(elevator);
            selection = elevator;
            Mutated();
        }

        private static int NextId(IEnumerable<int> existing)
        {
            var ids = existing.ToList();
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }

        // -- properties column --------------------------------

        private void DrawPropertiesColumn()
        {
            var width = settings.PropertiesColumnWidth.Resolve(position.width);
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            propertyScroll = EditorGUILayout.BeginScrollView(propertyScroll, GUILayout.ExpandHeight(true));

            DrawOutlineList();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

            // Field edits inside an inspector are caught here; button-driven list
            // edits (add/remove a layer, a queue entry, a wave) call Mutated()
            // themselves. Either way the window only routes the edit — the draft
            // and DraftValidator decide what it means.
            EditorGUI.BeginChangeCheck();

            if (selection != null && inspectors.TryGetValue(selection.GetType(), out var draw))
            {
                draw(selection);
            }
            else
            {
                EditorGUILayout.HelpBox("Select something in the grid or the list above.", MessageType.None);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Mutated();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawOutlineList()
        {
            SelectableList("Blocks", draft.Blocks, b => $"Block {b.Id}");
            SelectableList("Gates", draft.Gates, g => $"Gate {g.Id} ({g.Edge})");
            SelectableList("Shutters", draft.Shutters, s => $"Shutter {s.Id}");
            SelectableList("Generators", draft.Generators, g => $"Generator {g.Id}");
            SelectableList("Elevators", draft.Elevators, e => $"Elevator {e.Id}");
        }

        private void SelectableList<T>(string header, IReadOnlyList<T> items, Func<T, string> label) where T : class
        {
            if (items.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(header, EditorStyles.miniBoldLabel);
            foreach (var item in items)
            {
                var isSelected = ReferenceEquals(selection, item);
                if (GUILayout.Toggle(isSelected, label(item), EditorStyles.miniButton) && !isSelected)
                {
                    selection = item;
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawBlockProperties(BlockDraft block)
        {
            block.StartOrigin = CoordField("Start origin", block.StartOrigin);
            DrawColorStack(block.ColorStack);
            block.Axis = (MovementAxis)EditorGUILayout.EnumPopup("Axis", block.Axis);
            block.UnfreezeAtClearCount = NullableIntField("Unfreeze at clear #", block.UnfreezeAtClearCount);
            block.TimeBonusSeconds = EditorGUILayout.IntField("Time bonus (s)", block.TimeBonusSeconds);

            block.LockId = NullableIntField("Lock id", block.LockId);
            if (block.LockId.HasValue)
            {
                block.RequiredKeyCount = EditorGUILayout.IntField("Required keys", block.RequiredKeyCount);
            }

            block.KeyTargetLockId = NullableIntField("Key targets lock id", block.KeyTargetLockId);
            if (block.KeyTargetLockId.HasValue)
            {
                block.KeyEffect = (KeyEffect)EditorGUILayout.EnumPopup("Key effect", block.KeyEffect);
            }

            DeleteButton();
        }

        /// <summary>
        /// A wave block selected on the board of its wave (item 3): the same
        /// field set as a placed <see cref="BlockDraft"/>, minus <c>Axis</c>'s
        /// board-only framing. This is <see cref="DrawSpawnedBlockFields"/> with
        /// its position shown — a generator queue entry (<see cref="DrawGeneratorProperties"/>)
        /// draws the same fields without one, since generator output derives its
        /// position from the generator's edge and offset instead (M9).
        /// </summary>
        private void DrawWaveBlockProperties(SpawnedBlockDraft block)
        {
            DrawSpawnedBlockFields(block, showPosition: true, queueOwner: null);
            DeleteButton();
        }

        /// <summary>
        /// A <see cref="SpawnedBlockDraft"/>'s editable fields. Before this,
        /// nothing drew more than a colour and an axis for one — the generator
        /// queue's own two-field row — even though <c>DraftValidator</c>'s
        /// <c>EnumerateBlockLikes</c> has always inspected a queue entry's lock,
        /// key and axis fields; there was simply no way to set them. One function
        /// closes that gap for both a wave block and a queue entry rather than
        /// widening it with a second, divergent copy.
        /// </summary>
        /// <param name="queueOwner">
        /// The generator whose queue <paramref name="block"/> belongs to, or
        /// <c>null</c> for a wave block. Only a queue entry gets the shape
        /// picker, and its Free-draw grid is bounded by this generator's edge
        /// and width, so the owner is exactly the condition for drawing one.
        /// </param>
        private void DrawSpawnedBlockFields(SpawnedBlockDraft block, bool showPosition, GeneratorDraft queueOwner)
        {
            if (showPosition && block.RegionOrigin.HasValue)
            {
                block.RegionOrigin = CoordField("Position in region", block.RegionOrigin.Value);
            }

            // A board or wave block gets its shape from the palette at
            // placement time — clicking the grid. A queue entry is created by
            // a button and never touches the palette, so without this it can
            // only ever be the 1x1 "+ Add to queue" defaults to.
            if (queueOwner != null)
            {
                DrawQueueEntryShapeField(block, queueOwner);
            }

            DrawColorStack(block.ColorStack);
            block.Axis = (MovementAxis)EditorGUILayout.EnumPopup("Axis", block.Axis);
            block.UnfreezeAtClearCount = NullableIntField("Unfreeze at clear #", block.UnfreezeAtClearCount);
            block.TimeBonusSeconds = EditorGUILayout.IntField("Time bonus (s)", block.TimeBonusSeconds);

            block.LockId = NullableIntField("Lock id", block.LockId);
            if (block.LockId.HasValue)
            {
                block.RequiredKeyCount = EditorGUILayout.IntField("Required keys", block.RequiredKeyCount);
            }

            block.KeyTargetLockId = NullableIntField("Key targets lock id", block.KeyTargetLockId);
            if (block.KeyTargetLockId.HasValue)
            {
                block.KeyEffect = (KeyEffect)EditorGUILayout.EnumPopup("Key effect", block.KeyEffect);
            }
        }

        /// <summary>
        /// Every <see cref="ShapePreset"/> a queue entry can be given, in the
        /// palette's own order. <see cref="ShapePreset.Free"/> is included: the
        /// reason it was excluded in the previous round ("a queue entry has no
        /// grid to draw on") stopped being true once the shape preview
        /// (<see cref="EditorGrid.DrawCellPreview"/>) existed to draw on. With
        /// Free selected, that same preview area becomes the click surface —
        /// <see cref="DrawQueueEntryFreeDrawGrid"/> — bounded by the owning
        /// generator (<see cref="QueueEntryDrawBounds"/>) since a queue entry has
        /// no board to place on. The list itself is not filtered by that bound:
        /// a preset that does not fit the generator is flagged by the same
        /// <c>GeneratorTooNarrowForQueuedBlock</c> warning as a free-drawn shape.
        /// </summary>
        private static readonly ShapePreset[] QueueEntryShapePresets =
        {
            ShapePreset.Single, ShapePreset.Horizontal2, ShapePreset.Vertical2,
            ShapePreset.Horizontal3, ShapePreset.Vertical3, ShapePreset.Square2,
            ShapePreset.LNorthEast, ShapePreset.LNorthWest, ShapePreset.LSouthEast, ShapePreset.LSouthWest,
            ShapePreset.Free,
        };

        private static readonly string[] QueueEntryShapeLabels =
            QueueEntryShapePresets.Select(p => p.ToString()).ToArray();

        /// <summary>
        /// Free is a mode the designer explicitly chooses and stays in until
        /// they pick something else — tracked by <see cref="queueEntriesInFreeMode"/>,
        /// not re-derived from <see cref="SpawnedBlockDraft.Cells"/> on every
        /// draw. Deriving it was the bug: completing a shape mid-edit that
        /// happens to match a preset would flip the popup away from Free and
        /// hide the draw surface. <see cref="CurrentQueueEntryShape"/>'s preset
        /// lookup only labels an entry the designer has not put into Free mode.
        /// </summary>
        private void DrawQueueEntryShapeField(SpawnedBlockDraft entry, GeneratorDraft generator)
        {
            var current = queueEntriesInFreeMode.Contains(entry) ? ShapePreset.Free : CurrentQueueEntryShape(entry);
            var currentIndex = Array.IndexOf(QueueEntryShapePresets, current);
            var pickedIndex = EditorGUILayout.Popup("Shape", currentIndex, QueueEntryShapeLabels);
            var picked = QueueEntryShapePresets[pickedIndex];

            if (picked != current && picked != ShapePreset.Free)
            {
                // A real preset replaces Cells outright, same as always, and
                // leaves Free mode if the entry was in it.
                queueEntriesInFreeMode.Remove(entry);
                entry.Cells = new List<Coord>(ShapePresets.Cells(picked));
                current = picked;
            }
            else if (picked == ShapePreset.Free)
            {
                current = ShapePreset.Free; // Cells untouched — now editable by hand instead of by picking a different preset
            }

            if (current == ShapePreset.Free)
            {
                queueEntriesInFreeMode.Add(entry); // idempotent — this is the "stays in Free" half of the fix
                DrawQueueEntryFreeDrawGrid(entry, generator);
            }
            else
            {
                // A square as deep as the Free grid, so switching modes does not
                // make the box jump; the preview lays itself out from the shape's
                // own bounding box, so it need not track the generator's width.
                var side = Math.Max(1, settings.QueueEntryFreeDrawMaxDepth) * EditorGrid.PreviewCellSize;
                var rect = GUILayoutUtility.GetRect(side, side, GUILayout.Width(side));
                EditorGrid.DrawCellPreview(rect, entry.Cells, PreviewFillColor);
            }
        }

        /// <summary>
        /// The interactive draw surface Free selects — click a cell to add it,
        /// click again to remove it — reusing <see cref="EditorGrid.DrawCells"/>
        /// rather than the passive preview, since this one needs real click
        /// targets. Sized by <see cref="QueueEntryDrawBounds"/>: the generator's
        /// width along its edge, <see cref="LevelEditorSettings.QueueEntryFreeDrawMaxDepth"/>
        /// into the board, grown to keep any stale cell outside those caps
        /// visible. Cells in that grown margin draw dimmer and are remove-only.
        /// </summary>
        private void DrawQueueEntryFreeDrawGrid(SpawnedBlockDraft entry, GeneratorDraft generator)
        {
            var bounds = QueueEntryDrawBounds.For(
                generator.Edge, generator.Width, settings.QueueEntryFreeDrawMaxDepth, entry.Cells);
            var width = bounds.Columns * EditorGrid.PreviewCellSize;
            var height = bounds.Rows * EditorGrid.PreviewCellSize;
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width));
            var layout = new EditorGridLayout(rect, bounds.Columns, bounds.Rows);

            EditorGrid.DrawCells(layout, cell =>
                entry.Cells.Contains(cell) ? PreviewFillColor
                : bounds.AllowsNewCell(cell) ? FreeDrawBackground
                : FreeDrawOutOfBoundsBackground);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && layout.TryPick(e.mousePosition, out var clicked))
            {
                ToggleQueueEntryFreeCell(entry, clicked, bounds);
                e.Use();
                Mutated();
                Repaint();
            }
        }

        private static readonly Color PreviewFillColor = new Color(0.6f, 0.85f, 0.95f);
        private static readonly Color FreeDrawBackground = new Color(0.22f, 0.22f, 0.25f);
        private static readonly Color FreeDrawOutOfBoundsBackground = new Color(0.12f, 0.12f, 0.14f);

        /// <summary>
        /// Adds or removes <paramref name="cell"/> from <paramref name="entry"/>'s
        /// <c>Cells</c>, mirroring the board's own free draw
        /// (<see cref="ToggleFreeCell"/>) exactly on connectivity — the same
        /// <see cref="BlockShape"/> calls, not a second copy of the rule (D31).
        /// An add must also fall inside <paramref name="bounds"/>' caps; a
        /// removal never checks them, so a stale cell beyond them stays removable.
        /// </summary>
        /// <remarks>
        /// Deliberately does not normalise. Doing so on every click pinned the
        /// shape to the grid's bottom-left corner after the first cell — a click
        /// in the middle put the cell in the corner instead, only growth toward
        /// the corner's opposite side was reachable, and removing the current
        /// minimum shifted the whole remainder. Cells stay exactly where the
        /// designer put them, the same as the board's own <c>freeCells</c>
        /// staging buffer does; nothing downstream needs them normalised
        /// already, since <c>Core</c> normalises on construction regardless
        /// (D30) and this entry is never round-tripped through one just to
        /// preview its shape. <see cref="CurrentQueueEntryShape"/> normalises a
        /// copy when it needs to compare against a preset — that is the one
        /// place it actually matters.
        /// </remarks>
        private void ToggleQueueEntryFreeCell(SpawnedBlockDraft entry, Coord cell, QueueEntryDrawBounds bounds)
        {
            if (entry.Cells.Contains(cell))
            {
                var remainder = entry.Cells.Where(c => c != cell).ToList();
                if (remainder.Count == 0 || BlockShape.IsOrthogonallyConnected(remainder))
                {
                    entry.Cells = remainder;
                }
            }
            else if (bounds.AllowsNewCell(cell)
                && (entry.Cells.Count == 0 || entry.Cells.Any(c => BlockShape.AreOrthogonallyAdjacent(c, cell))))
            {
                entry.Cells = new List<Coord>(entry.Cells) { cell };
            }
        }

        /// <summary>
        /// The preset a normalised copy of <paramref name="entry"/>'s current
        /// <c>Cells</c> matches exactly (order-insensitive — a hand-drawn
        /// shape's cells are in click order, not a preset's canonical order),
        /// or <see cref="ShapePreset.Free"/> if nothing matches. Normalising
        /// only this copy — never <paramref name="entry"/>'s own <c>Cells</c> —
        /// is what lets the designer draw starting anywhere on the grid and
        /// still have the popup recognise a completed preset shape.
        /// </summary>
        private static ShapePreset CurrentQueueEntryShape(SpawnedBlockDraft entry)
        {
            var normalized = CellNormalization.Normalize(entry.Cells);
            foreach (var preset in QueueEntryShapePresets)
            {
                if (preset == ShapePreset.Free)
                {
                    continue; // Free has no fixed cell set to match against
                }

                if (CellsMatch(normalized, ShapePresets.Cells(preset)))
                {
                    return preset;
                }
            }

            return ShapePreset.Free;
        }

        private static bool CellsMatch(IReadOnlyList<Coord> a, IReadOnlyList<Coord> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            return new HashSet<Coord>(a).SetEquals(b);
        }

        private void DrawGateProperties(GateDraft gate)
        {
            gate.Edge = (BoardEdge)EditorGUILayout.EnumPopup("Edge", gate.Edge);
            gate.Offset = EditorGUILayout.IntField("Offset", gate.Offset);
            gate.Width = EditorGUILayout.IntField("Width", gate.Width);
            gate.Color = (BlockColor)EditorGUILayout.EnumPopup("Colour", gate.Color);
            gate.OpenAtClearCount = NullableIntField("Open at clear #", gate.OpenAtClearCount);

            DeleteButton();
        }

        private void DrawShutterProperties(ShutterDraft shutter)
        {
            var bounds = RegionBoundsFields(shutter.Min, shutter.Max);
            shutter.Min = bounds.Min;
            shutter.Max = bounds.Max;

            shutter.Threshold = EditorGUILayout.IntField("Threshold", shutter.Threshold);
            shutter.RequiredColor = NullableColorField("Required colour", shutter.RequiredColor);

            DeleteButton();
        }

        private void DrawGeneratorProperties(GeneratorDraft generator)
        {
            generator.Edge = (BoardEdge)EditorGUILayout.EnumPopup("Edge", generator.Edge);
            generator.Offset = EditorGUILayout.IntField("Offset", generator.Offset);

            // A plain IntField, like a gate's width: the [1, MaxWidth] bound is a
            // rule of the game, and Core throwing on a value outside it surfaces
            // as a DraftDoesNotFormValidLevel warning the designer can read.
            // Clamping in the widget would enforce the same rule silently, in a
            // second place, and teach nobody what it is.
            generator.Width = EditorGUILayout.IntField(
                $"Width (1-{GeneratorDefinition.MaxWidth})", generator.Width);

            EditorGUILayout.LabelField("Queue", EditorStyles.miniBoldLabel);
            for (var i = 0; i < generator.Queue.Count; i++)
            {
                var entry = generator.Queue[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Queue entry {i + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                // M6: the queue is "an explicit, ordered array" — the order is
                // the design, not an implementation detail. Move-up/move-down
                // is what lets a block be inserted in the middle without
                // deleting and re-authoring everything after it.
                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(22f)))
                    {
                        (generator.Queue[i - 1], generator.Queue[i]) = (generator.Queue[i], generator.Queue[i - 1]);
                        Mutated();
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUI.DisabledScope(i == generator.Queue.Count - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(22f)))
                    {
                        (generator.Queue[i + 1], generator.Queue[i]) = (generator.Queue[i], generator.Queue[i + 1]);
                        Mutated();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("x", GUILayout.Width(22f)))
                {
                    queueEntriesInFreeMode.Remove(entry);
                    generator.Queue.RemoveAt(i);
                    Mutated();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                // showPosition: false — generator output has no position to
                // author; it derives from the generator's own edge and offset.
                // queueOwner: generator — see DrawSpawnedBlockFields.
                DrawSpawnedBlockFields(entry, showPosition: false, queueOwner: generator);

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add to queue"))
            {
                generator.Queue.Add(new SpawnedBlockDraft
                {
                    Id = NextId(generator.Queue.Select(b => b.Id)),
                    Cells = new List<Coord>(ShapePresets.Cells(ShapePreset.Single)),
                    ColorStack = { BlockColor.Red },
                });
                Mutated();
                GUIUtility.ExitGUI();
            }

            DeleteButton();
        }

        private void DrawElevatorProperties(ElevatorDraft elevator)
        {
            var bounds = RegionBoundsFields(elevator.Min, elevator.Max);
            elevator.Min = bounds.Min;
            elevator.Max = bounds.Max;

            var area = Math.Max(0, elevator.Max.X - elevator.Min.X + 1) * Math.Max(0, elevator.Max.Y - elevator.Min.Y + 1);

            EditorGUILayout.LabelField("Waves", EditorStyles.miniBoldLabel);
            for (var w = 0; w < elevator.Waves.Count; w++)
            {
                var wave = elevator.Waves[w];
                var tiling = DraftTiling.Check(elevator, wave);
                var status = tiling == null ? "?" : tiling.IsExact ? "✓" : "⚠";
                var covered = area - (tiling?.UncoveredCells.Count ?? area);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Wave {w + 1}   {covered}/{area}   {status}");
                if (GUILayout.Button("Open", GUILayout.Width(48f)))
                {
                    EnterWaveScope(elevator, w);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("x", GUILayout.Width(22f)))
                {
                    elevator.Waves.RemoveAt(w);
                    Mutated();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add wave"))
            {
                elevator.Waves.Add(new WaveDraft());
                Mutated();
                GUIUtility.ExitGUI();
            }

            DeleteButton();
        }

        private void DrawColorStack(List<BlockColor> stack)
        {
            EditorGUILayout.LabelField("Colour stack (outer first)", EditorStyles.miniBoldLabel);
            for (var i = 0; i < stack.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                stack[i] = (BlockColor)EditorGUILayout.EnumPopup(stack[i]);
                if (stack.Count > 1 && GUILayout.Button("x", GUILayout.Width(22f)))
                {
                    stack.RemoveAt(i);
                    Mutated();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ layer"))
            {
                stack.Add(BlockColor.Blue);
                Mutated();
                GUIUtility.ExitGUI();
            }
        }

        // -- footer: warnings, metrics, solve ------------------

        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Warnings ({warnings.Count})", EditorStyles.boldLabel);
            var warningsHeight = settings.WarningsListHeight.Resolve(position.height);
            warningScroll = EditorGUILayout.BeginScrollView(warningScroll, GUILayout.Height(warningsHeight));
            foreach (var warning in warnings)
            {
                EditorGUILayout.LabelField("• " + warning.Message, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndScrollView();

            if (metrics != null)
            {
                var branching = metrics.OpeningBranchingFactor < 0 ? "n/a" : metrics.OpeningBranchingFactor.ToString();
                var ready = metrics.HasReadyOpeningMove ? "✓" : "✗";
                EditorGUILayout.LabelField(
                    $"Empty cells {metrics.EmptyCellCount} · Fill {metrics.FillRatio:P0} · " +
                    $"Opening branching {branching} · Ready move {ready}",
                    EditorStyles.miniLabel);
            }

            if (validationError != null)
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Error);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(IsValidating ? ValidationProgress() : SolveSummary(), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (IsValidating)
            {
                if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
                {
                    CancelValidation("you cancelled it");
                }
            }
            else if (GUILayout.Button("Validate", GUILayout.Width(80f)))
            {
                StartValidation();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private string SolveSummary()
        {
            if (solve == null)
            {
                return validationCancelledReason != null
                    ? $"Solver: cancelled — {validationCancelledReason}"
                    : "Solver: not run";
            }

            var answering = solve.Answering;
            var stage = $"{StageName(solve.AnsweredBy)}, {answering.ExploredStateCount} states, {answering.ElapsedMs}ms";

            switch (solve.Verdict)
            {
                case LevelSolveVerdict.Solvable:
                    var suggested = metrics?.SuggestedTimeBudgetSeconds;
                    var quality = solve.ProvenShortestLength.HasValue
                        ? "shortest"
                        : $"shortest ≥ {solve.LengthLowerBound}, not proven";
                    var budgetNote = metrics != null && metrics.IsTimeBudgetFromUnprovenLength
                        ? " (from unproven length)"
                        : string.Empty;
                    return $"Solver: solvable in {solve.Solution.Count}, {quality} ({stage})" +
                           (suggested.HasValue ? $"  ·  Suggested budget {suggested.Value}s{budgetNote}" : string.Empty);
                case LevelSolveVerdict.Unsolvable:
                    return $"Solver: unsolvable ({stage}) — {UnsolvableBasis()}";
                default:
                    return "Solver: indeterminate (budget reached) — " +
                           $"quick exhaustive A* {solve.Quick.ExploredStateCount} states/{solve.Quick.ElapsedMs}ms, " +
                           $"nearest-next-clear {answering.ExploredStateCount} states/{answering.ElapsedMs}ms";
            }
        }

        /// <summary>Why the pipeline believes an Unsolvable verdict.</summary>
        private string UnsolvableBasis()
        {
            switch (solve.CrossCheckOutcome)
            {
                case UnsolvableCrossCheck.Confirmed:
                    return "A* cross-check confirmed";
                case UnsolvableCrossCheck.Inconclusive:
                    return "A* cross-check inconclusive (ran out of budget without finding a solution)";
                default:
                    return "proven by exhaustive search";
            }
        }

        private static string StageName(ValidationStage stage)
        {
            switch (stage)
            {
                case ValidationStage.QuickOptimal:
                    return "quick exhaustive A*";
                case ValidationStage.NearestNextClear:
                    return "nearest-next-clear";
                default:
                    return "A* cross-check";
            }
        }

        private bool IsValidating => validation != null;

        private string ValidationProgress()
        {
            var stage = (ValidationStage)Interlocked.CompareExchange(ref validationStage, 0, 0);
            var elapsed = EditorApplication.timeSinceStartup - validationStartedAt;
            return $"Validating — {StageName(stage)}… {elapsed:F0}s";
        }

        /// <summary>
        /// How often, in seconds, the window repaints to advance the progress
        /// line while a run is in flight — often enough to read as live, rarely
        /// enough not to spend the main thread on redrawing.
        /// </summary>
        private const double ValidationRepaintIntervalSeconds = 0.25;

        /// <summary>
        /// Starts a Validate run on a worker thread. The context and budgets are
        /// built here, on the main thread, because <see cref="LevelEditorSettings"/>
        /// is a Unity object; the worker only ever touches plain C#.
        /// </summary>
        private void StartValidation()
        {
            if (IsValidating)
            {
                return;
            }

            LevelContext ctx;
            try
            {
                ctx = draft.ToContext();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Cannot solve", $"The draft is not a valid level:\n\n{e.Message}", "OK");
                return;
            }

            solve = null;
            validationError = null;
            validationCancelledReason = null;
            Revalidate();

            var quickBudget = settings.QuickBudget;
            var normalBudget = settings.ExhaustiveBudget;
            var canonicalBudget = settings.CanonicalBudget;
            var pipeline = new ValidationPipeline();

            validationCancellation = new CancellationTokenSource();
            var token = validationCancellation.Token;
            Interlocked.Exchange(ref validationStage, (int)ValidationStage.QuickOptimal);
            validationStartedAt = EditorApplication.timeSinceStartup;

            validation = Task.Run(
                () => pipeline.Run(
                    ctx, quickBudget, normalBudget, canonicalBudget, normalBudget,
                    stage => Interlocked.Exchange(ref validationStage, (int)stage),
                    token),
                token);

            EditorApplication.update -= PollValidation;
            EditorApplication.update += PollValidation;
        }

        /// <summary>
        /// Asks a running Validate to stop, recording why for the summary line.
        /// The worker notices at its next expansion; <see cref="PollValidation"/>
        /// then finishes the run on the main thread.
        /// </summary>
        private void CancelValidation(string reason)
        {
            if (validationCancellation == null || validationCancellation.IsCancellationRequested)
            {
                return;
            }

            validationCancelledReason = reason;
            validationCancellation.Cancel();
        }

        /// <summary>Main-thread poll of the worker, from <see cref="EditorApplication.update"/>.</summary>
        private void PollValidation()
        {
            if (validation == null)
            {
                EditorApplication.update -= PollValidation;
                return;
            }

            if (!validation.IsCompleted)
            {
                var now = EditorApplication.timeSinceStartup;
                if (now - lastValidationRepaint >= ValidationRepaintIntervalSeconds)
                {
                    lastValidationRepaint = now;
                    Repaint();
                }

                return;
            }

            var finished = validation;
            var wasCancelled = validationCancellation.IsCancellationRequested;
            validation = null;
            validationCancellation.Dispose();
            validationCancellation = null;
            EditorApplication.update -= PollValidation;

            FinishValidation(finished, wasCancelled);
            Repaint();
        }

        /// <summary>
        /// Applies a finished run. A solver failure is always shown, loudly — it
        /// is a bug, never a verdict, and must not be mistaken for one. A run that
        /// was asked to stop has its answer discarded even if it finished first:
        /// it describes a draft that has since changed.
        /// </summary>
        private void FinishValidation(Task<ValidationResult> finished, bool wasCancelled)
        {
            var failure = finished.IsFaulted ? finished.Exception?.GetBaseException() : null;

            if (failure is SolverDisagreementException disagreement)
            {
                validationError =
                    "Solver disagreement — neither verdict can be trusted, and this is a solver bug, not a " +
                    $"property of the level:\n{disagreement.Message}";
                Debug.LogError(disagreement);
                EditorUtility.DisplayDialog("Solver disagreement", validationError, "OK");
                return;
            }

            if (failure != null && !(failure is OperationCanceledException))
            {
                validationError = $"Solver error — the search could not complete, and this is a solver bug:\n{failure.Message}";
                Debug.LogException(failure);
                EditorUtility.DisplayDialog("Solver error", validationError, "OK");
                return;
            }

            if (wasCancelled || finished.IsCanceled || failure != null)
            {
                return;
            }

            solve = finished.Result;
            validationCancelledReason = null;
            Revalidate();
        }

        /// <summary>
        /// Forgets the last Validate answer because the draft changed, and
        /// cancels a run still in flight for the same reason.
        /// </summary>
        private void DiscardSolve()
        {
            solve = null;
            validationError = null;
            validationCancelledReason = null;
            CancelValidation("the level changed while it was running");
        }

        // -- files -------------------------------------------

        private void NewLevel()
        {
            draft = LevelDraft.NewEmpty(newWidth, newHeight);
            history.Reset(draft.ToDto());
            assetPath = null;
            dirty = false;
            DiscardSolve();
            selection = null;
            dragKind = DragKind.None;
            queueEntriesInFreeMode.Clear();
            LeaveWaveScope();
            SyncGridFields();
            Revalidate();
        }

        private void SyncGridFields()
        {
            newWidth = draft.Width;
            newHeight = draft.Height;
        }

        private void ShowOpenMenu()
        {
            var menu = new GenericMenu();
            if (Directory.Exists(LevelsFolder))
            {
                foreach (var path in Directory.GetFiles(LevelsFolder, "*.json").OrderBy(p => p))
                {
                    var captured = path;
                    menu.AddItem(new GUIContent(Path.GetFileName(path)), false, () =>
                    {
                        if (ConfirmDiscardIfDirty())
                        {
                            LoadFrom(captured);
                        }
                    });
                }
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent($"No .json files in {LevelsFolder}"));
            }

            menu.ShowAsContext();
        }

        private void LoadFrom(string path)
        {
            try
            {
                var dto = LevelSerializer.ParseDto(File.ReadAllText(path), Path.GetFileName(path));
                draft = LevelDraft.FromDto(dto);
                history.Reset(draft.ToDto());
                assetPath = path;
                dirty = false;
                DiscardSolve();
                selection = null;
                dragKind = DragKind.None;
                queueEntriesInFreeMode.Clear();
                LeaveWaveScope();
                SyncGridFields();
                Revalidate();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Cannot open level", e.Message, "OK");
            }
        }

        private void SaveAs()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save level", $"level-{draft.LevelId}", "json", "Choose a path under Assets/");
            if (!string.IsNullOrEmpty(path))
            {
                Save(path);
            }
        }

        private void Save(string path)
        {
            File.WriteAllText(path, LevelSerializer.ToJson(draft.ToDto()));
            AssetDatabase.Refresh();
            assetPath = path;
            dirty = false;
        }

        // -- resize (LevelDraft decides what is lost) ----------

        private void RequestResize(int width, int height)
        {
            if (InWaveScope())
            {
                return;
            }

            if (width == draft.Width && height == draft.Height)
            {
                return;
            }

            var impact = draft.PreviewResize(width, height);
            if (!impact.IsLossless)
            {
                var message =
                    $"Shrinking to {width}x{height} removes {impact.RemovedBlockIds.Count} block(s), " +
                    $"{impact.RemovedGateIds.Count} gate(s), {impact.RemovedShutterIds.Count} shutter(s), " +
                    $"{impact.RemovedGeneratorIds.Count} generator(s), {impact.RemovedElevatorIds.Count} elevator(s) " +
                    $"and {impact.RemovedStaticWalls.Count} wall(s). Continue?";
                if (!EditorUtility.DisplayDialog("Resize grid", message, "Continue", "Cancel"))
                {
                    return;
                }
            }

            draft.ApplyResize(width, height);
            selection = null;
            dragKind = DragKind.None;
            Mutated();
        }

        // -- scope -----------------------------------------

        private bool InWaveScope() =>
            scopeElevator >= 0 && scopeElevator < draft.Elevators.Count
            && scopeWave >= 0 && scopeWave < draft.Elevators[scopeElevator].Waves.Count;

        /// <summary>The wave the current scope points at in the current <see cref="draft"/>, or <c>null</c> outside wave scope.</summary>
        private WaveDraft CurrentScopeWave() =>
            InWaveScope() ? draft.Elevators[scopeElevator].Waves[scopeWave] : null;

        private void EnterWaveScope(ElevatorDraft elevator, int waveIndex)
        {
            scopeElevator = draft.Elevators.IndexOf(elevator);
            scopeWave = waveIndex;
            selection = null;
            dragKind = DragKind.None;
            tool = EditorTool.Select;
            freeCells.Clear(); // item 2: pending free-draw cells never cross a scope boundary
        }

        private void LeaveWaveScope()
        {
            scopeElevator = -1;
            scopeWave = -1;
            selection = null;
            dragKind = DragKind.None;
            freeCells.Clear();
        }

        // -- small shared bits --------------------------

        private bool ConfirmDiscardIfDirty()
        {
            return !dirty || EditorUtility.DisplayDialog(
                "Unsaved changes",
                "This level has unsaved changes. Discard them?",
                "Discard", "Cancel");
        }

        private void DeleteButton()
        {
            EditorGUILayout.Space();
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.5f, 0.5f);
            if (GUILayout.Button("Delete"))
            {
                DeleteSelection();
                GUIUtility.ExitGUI();
            }

            GUI.backgroundColor = previous;
        }

        /// <summary>
        /// Removes whatever is currently selected from the draft it belongs to
        /// and clears the selection. The one place that knows how to delete each
        /// selectable type — shared by <see cref="DeleteButton"/> and the Delete
        /// key (item 6) so there is one path, not two.
        /// </summary>
        private void DeleteSelection()
        {
            switch (selection)
            {
                case BlockDraft block:
                    draft.Blocks.Remove(block);
                    break;
                case GateDraft gate:
                    draft.Gates.Remove(gate);
                    break;
                case ShutterDraft shutter:
                    draft.Shutters.Remove(shutter);
                    break;
                case GeneratorDraft generator:
                    foreach (var entry in generator.Queue)
                    {
                        queueEntriesInFreeMode.Remove(entry);
                    }

                    draft.Generators.Remove(generator);
                    break;
                case ElevatorDraft elevator:
                    draft.Elevators.Remove(elevator);
                    break;
                case SpawnedBlockDraft waveBlock:
                    if (!RemoveWaveBlock(waveBlock))
                    {
                        return; // not held by any wave — nothing this method knows how to delete
                    }

                    break;
                default:
                    return;
            }

            selection = null;
            Mutated();
        }

        /// <summary>
        /// Removes <paramref name="block"/> from whichever wave actually holds
        /// it, searching every elevator's every wave rather than assuming the
        /// current scope. Today a <see cref="SpawnedBlockDraft"/> only ever
        /// becomes <see cref="selection"/> while in wave scope — <see cref="EnterWaveScope"/>
        /// and <see cref="LeaveWaveScope"/> both clear it — but that invariant is
        /// maintained by two methods far from here, and <see cref="SpawnedBlockDraft"/>
        /// is also what a generator's queue holds. Searching instead of indexing
        /// <see cref="scopeElevator"/>/<see cref="scopeWave"/> keeps the
        /// assumption local: should a queue entry ever become selectable, this
        /// simply does not find it in any wave rather than deleting from the
        /// wrong list or throwing.
        /// </summary>
        private bool RemoveWaveBlock(SpawnedBlockDraft block)
        {
            foreach (var elevator in draft.Elevators)
            {
                foreach (var wave in elevator.Waves)
                {
                    if (wave.Blocks.Remove(block))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OutlineCellIfInside(EditorGridLayout layout, Coord cell, Color color)
        {
            if (cell.X >= 0 && cell.X < layout.Columns && cell.Y >= 0 && cell.Y < layout.Rows)
            {
                EditorGrid.DrawOutline(layout.CellRect(cell), color);
            }
        }

        /// <summary>Draws a single outline around the region rectangle, clipped to the visible grid.</summary>
        private static void OutlineRegionRect(EditorGridLayout layout, Coord min, Coord max, Color color, float thickness)
        {
            if (max.X < 0 || min.X > layout.Columns - 1 || max.Y < 0 || min.Y > layout.Rows - 1)
            {
                return;
            }

            var loX = Mathf.Clamp(Mathf.Min(min.X, max.X), 0, layout.Columns - 1);
            var loY = Mathf.Clamp(Mathf.Min(min.Y, max.Y), 0, layout.Rows - 1);
            var hiX = Mathf.Clamp(Mathf.Max(min.X, max.X), 0, layout.Columns - 1);
            var hiY = Mathf.Clamp(Mathf.Max(min.Y, max.Y), 0, layout.Rows - 1);

            var a = layout.CellRect(new Coord(loX, loY));
            var b = layout.CellRect(new Coord(hiX, hiY));
            var rect = Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax));

            EditorGrid.DrawOutline(rect, color, thickness);
        }

        private static BlockColor FirstColor(IReadOnlyList<BlockColor> stack) =>
            stack.Count > 0 ? stack[0] : BlockColor.Red;

        private static Coord CoordField(string label, Coord value)
        {
            var v = EditorGUILayout.Vector2IntField(label, new Vector2Int(value.X, value.Y));
            return new Coord(v.x, v.y);
        }

        // A2: the four region-bound fields. Delayed so typing "1" on the way to
        // "15" is not clamped mid-keystroke; RegionBounds does the clamp and the
        // Min <= Max correction once the value commits.
        private (Coord Min, Coord Max) RegionBoundsFields(Coord min, Coord max)
        {
            EditorGUILayout.LabelField("Region", EditorStyles.miniBoldLabel);
            var minX = EditorGUILayout.DelayedIntField("Min X", min.X);
            var minY = EditorGUILayout.DelayedIntField("Min Y", min.Y);
            var maxX = EditorGUILayout.DelayedIntField("Max X", max.X);
            var maxY = EditorGUILayout.DelayedIntField("Max Y", max.Y);

            return RegionBounds.Clamped(new Coord(minX, minY), new Coord(maxX, maxY), draft.Width, draft.Height);
        }

        private static int? NullableIntField(string label, int? value)
        {
            EditorGUILayout.BeginHorizontal();
            var has = EditorGUILayout.ToggleLeft(label, value.HasValue, GUILayout.Width(150f));
            int? result = null;
            if (has)
            {
                result = EditorGUILayout.IntField(value ?? 0);
            }

            EditorGUILayout.EndHorizontal();
            return result;
        }

        private static BlockColor? NullableColorField(string label, BlockColor? value)
        {
            EditorGUILayout.BeginHorizontal();
            var has = EditorGUILayout.ToggleLeft(label, value.HasValue, GUILayout.Width(150f));
            BlockColor? result = null;
            if (has)
            {
                result = (BlockColor)EditorGUILayout.EnumPopup(value ?? BlockColor.Red);
            }

            EditorGUILayout.EndHorizontal();
            return result;
        }

        private static Color Palette(BlockColor color)
        {
            switch (color)
            {
                case BlockColor.Red: return new Color(0.88f, 0.29f, 0.29f);
                case BlockColor.Blue: return new Color(0.29f, 0.49f, 0.88f);
                case BlockColor.Green: return new Color(0.32f, 0.74f, 0.4f);
                case BlockColor.Yellow: return new Color(0.93f, 0.83f, 0.31f);
                case BlockColor.Purple: return new Color(0.6f, 0.36f, 0.8f);
                case BlockColor.Orange: return new Color(0.93f, 0.6f, 0.27f);
                case BlockColor.Pink: return new Color(0.93f, 0.5f, 0.72f);
                case BlockColor.Cyan: return new Color(0.33f, 0.79f, 0.83f);
                default: return Color.gray;
            }
        }
    }
}
