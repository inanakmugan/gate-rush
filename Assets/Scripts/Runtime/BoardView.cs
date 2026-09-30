using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using GateRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace GateRush.Runtime
{
    /// <summary>
    /// Draws one board state: the floor, the frame with walls drawn as frame,
    /// gates, generator machines, elevators, blocks with their marks, and
    /// shutters, each count on the shared badge. What is shown is decided by
    /// <see cref="VisibilityLayer"/>; this component only turns that into
    /// sprites and labels, all sized and coloured from
    /// <see cref="RuntimeConfig"/>. It also shows what happens between redraws:
    /// a grabbed block lifting with an outline, floating at a continuous
    /// position and settling into its cell on release (Module 13), and
    /// everything a move changes (Module 18, in <c>BoardView.Effects.cs</c>).
    /// </summary>
    /// <remarks>
    /// <para><b>Hierarchy.</b> Under this transform sit two groups: <c>Board</c>,
    /// the drawn state, rebuilt from scratch on every redraw, and
    /// <c>Effects</c>, holding <c>Debris</c> — cubes and ice shards still in
    /// flight — and <c>Stage</c>, the temporary pieces of a presentation.
    /// Positions are local to this transform: the board is centred on it, and
    /// both groups sit at its origin.</para>
    /// <para><b>Shapes from quarters (Module 15).</b> Blocks, the frame, gates
    /// and the next block on a generator's screen are drawn from the generated
    /// quarter sprites, laid out by <see cref="BlockTiling"/> and
    /// <see cref="FrameTiling"/> and tinted at runtime. Each is drawn twice: a
    /// darker copy offset downward (the lip), then the face.</para>
    /// <para><b>State (Module 16, D48).</b> A frozen block and a closed gate are
    /// ice with frost and a count; a locked block carries a chain along each
    /// row of its cells and a gold padlock with the keys still required; a
    /// key-carrying block carries a gold key whose gem is its lock's colour
    /// (D47); a closed shutter is slats with a border and a count; a generator
    /// is a machine outside the frame whose screen shows its next block; an
    /// elevator is a pair of lift doors under its blocks. Every count sits on
    /// one badge (<see cref="DrawBadge"/>), recorded by its owner so it can
    /// pop.</para>
    /// <para><b>Blocks.</b> Each shown block gets a root at its origin's corner
    /// and, under it, a <c>Body</c> holding everything it draws: a <c>Lip</c>
    /// group, a <c>Face</c> group (quarters plus studs and gloss, frost, or
    /// the axis arrow), then beneath squares, chains, padlock or key, badges
    /// and labels. The drag, the settle and the exit move the root; the lift,
    /// a spawn and a rise scale the body about the footprint's centre, so the
    /// two never overwrite each other.</para>
    /// <para><b>Tweens.</b> DOTween keeps static state, and Enter Play Mode
    /// runs without a domain reload. Every tween of a presentation, a lift or
    /// a settle carries this view as its id. Debris tweens carry
    /// <see cref="debrisId"/>, an object this view owns, so the view's own
    /// redraws — which kill the presentation's tweens — leave debris flying.
    /// <see cref="Rebuild"/>, <see cref="Clear"/>, disabling and destroying
    /// kill both ids; nothing this view starts outlives it.</para>
    /// </remarks>
    public sealed partial class BoardView : MonoBehaviour
    {
        private readonly Dictionary<int, DrawnBlock> drawnBlocks = new Dictionary<int, DrawnBlock>();

        // Every badge drawn, by what it counts, so a count pop can find it.
        private readonly Dictionary<CountPop, Transform> badges = new Dictionary<CountPop, Transform>();

        // Per elevator, its doors and divider, hidden while a wave rises.
        private readonly Dictionary<int, GameObject[]> elevatorDoors = new Dictionary<int, GameObject[]>();

        // The id of every debris tween. Owned by this view and never shared,
        // so killing it reaches this view's debris and nothing else.
        private readonly object debrisId = new object();

        private RuntimeConfig config;
        private LevelContext ctx;
        private BoardLayout layout;
        private VisibilityLayer visibility;
        private BoardState drawnState;
        private BurstSettings cubeBurst;
        private BurstSettings shardBurst;

        // Static per level, so worked out once in Initialize.
        private IReadOnlyList<QuarterTile> frameTiles;
        private IReadOnlyList<QuarterTile>[] gateTiles;
        private MachinePlacement[] machines;
        private Func<QuarterTile, Rect> blockQuarterRect;
        private Func<QuarterTile, Rect> frameQuarterRect;

        private Transform board;
        private Transform debris;
        private Transform stage;

        private int draggedBlock = -1;

        // Null whenever no settle is playing: cleared on completion and on
        // every kill, or IsBusy would hold for good and lock input.
        private Tween settleTween;

        /// <summary>
        /// True while a released block is settling or a move is being
        /// presented. Input must not start a drag while it holds. Debris still
        /// in flight does not count.
        /// </summary>
        public bool IsBusy => settleTween != null || isPresenting;

        /// <summary>
        /// Binds the view to one level. Call before the first
        /// <see cref="Rebuild"/>. <paramref name="machine"/> must be the rule
        /// <paramref name="layout"/>'s reach came from, so every machine drawn
        /// is inside the fitted view.
        /// </summary>
        public void Initialize(
            RuntimeConfig config, LevelContext ctx, BoardLayout layout, VisibilityLayer visibility, GeneratorMachine machine)
        {
            StopAnimations();
            this.config = config;
            this.ctx = ctx;
            this.layout = layout;
            this.visibility = visibility;
            cubeBurst = config.CreateCubeBurst();
            shardBurst = config.CreateShardBurst();

            frameTiles = FrameTiling.Compute(ctx);
            gateTiles = new IReadOnlyList<QuarterTile>[ctx.Gates.Count];
            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                gateTiles[g] = BlockTiling.Compute(FrameTiling.GateCells(ctx, g));
            }

            machines = new MachinePlacement[ctx.Generators.Count];
            for (var g = 0; g < ctx.Generators.Count; g++)
            {
                machines[g] = machine.Place(ctx.Width, ctx.Height, ctx.Generators[g]);
            }

            // A block's quarters are local to its body, which sits at the
            // origin's corner; the frame's and gates' are in grid units.
            blockQuarterRect = tile => CellRectToLocal(BlockTiling.QuarterRect(tile));
            frameQuarterRect = tile => GridRectToLocal(
                FrameTiling.QuarterRect(tile, ctx.Width, ctx.Height, layout.FrameThicknessCells));
        }

        /// <summary>
        /// Stops every animation, debris included, abandons any presentation in
        /// progress without reporting it done, discards everything drawn and
        /// draws <paramref name="state"/>. For a restart or a new level.
        /// </summary>
        public void Rebuild(BoardState state)
        {
            Clear();
            Draw(state);
        }

        /// <summary>
        /// Starts showing a drag of block <paramref name="blockIndex"/>: it
        /// lifts, with its outline, and <see cref="ShowDragged"/> and
        /// <see cref="Settle"/> after this move that block.
        /// </summary>
        public void BeginDrag(int blockIndex)
        {
            KillSettle();
            draggedBlock = blockIndex;
            if (drawnBlocks.TryGetValue(blockIndex, out var block))
            {
                Lift(block);
            }
        }

        /// <summary>
        /// Shows the dragged block at the continuous origin
        /// <paramref name="origin"/>, in cell units — fed every frame from
        /// <see cref="DragController.Position"/>. Does nothing when the block is
        /// not drawn.
        /// </summary>
        public void ShowDragged(Vector2 origin)
        {
            if (drawnBlocks.TryGetValue(draggedBlock, out var block))
            {
                block.Root.localPosition = GridToLocal(origin);
            }
        }

        /// <summary>
        /// Tweens the dragged block from where it was released into the cell
        /// <paramref name="origin"/>, and drops its lift. A block already there
        /// — a push in place, or a release exactly on a cell — is placed at once
        /// with no tween, so a presentation waiting on the settle starts without
        /// delay. <see cref="IsBusy"/> holds while the tween plays; the drop
        /// does not hold it.
        /// </summary>
        public void Settle(Coord origin)
        {
            KillSettle();
            if (!drawnBlocks.TryGetValue(draggedBlock, out var block))
            {
                return;
            }

            Drop(block);

            var target = OriginToLocal(origin);
            if ((Vector2)block.Root.localPosition == target)
            {
                block.Root.localPosition = target;
                return;
            }

            settleTween = block.Root
                .DOLocalMove(target, config.SettleSeconds)
                .SetEase(config.SettleEase)
                .SetId(this)
                .OnComplete(OnSettled);
        }

        /// <summary>
        /// Shows block <paramref name="blockIndex"/> at
        /// <paramref name="origin"/> at once, stopping any settle and dropping
        /// its lift — for a drag that is cancelled or whose move was rejected.
        /// Does nothing for a block that is not drawn.
        /// </summary>
        public void Snap(int blockIndex, Coord origin)
        {
            KillSettle();

            if (drawnBlocks.TryGetValue(blockIndex, out var block))
            {
                block.Root.localPosition = OriginToLocal(origin);
                Drop(block);
            }
        }

        /// <summary>Stops every animation, debris included, and destroys everything drawn.</summary>
        public void Clear()
        {
            StopAnimations();
            ForgetDrawing();
            drawnState = null;

            // Destroy is deferred to the end of the frame, so the groups are
            // forgotten here and created afresh by the next drawing: nothing new
            // lands in a group that is about to go.
            DestroyChildren(transform);
            board = null;
            debris = null;
            stage = null;
        }

        private void OnEnable()
        {
            // Disabling killed the tweens mid-way. Presentations that were
            // waiting on blocks passing through their gates are done: those
            // blocks went with the debris. A presentation that was interrupted
            // finishes now, at its end state; otherwise the half-animated
            // picture is replaced by the state it was drawn from.
            ReleaseDoneAfterPasses();
            if (isPresenting)
            {
                FinishPresentationAtOnce();
            }
            else if (drawnState != null)
            {
                Rebuild(drawnState);
            }
        }

        private void OnDisable()
        {
            // The presentation, if any, is kept so OnEnable can finish it, and
            // so are presentations waiting on passes, for OnEnable to report.
            // Debris and temporary pieces go now; a disabled view shows no
            // half-flown cube and no half-passed block.
            DOTween.Kill(this);
            DOTween.Kill(debrisId);
            settleTween = null;
            hasStagesStarted = false;
            passesInFlight = 0;
            DestroyChildren(debris);
            DestroyChildren(stage);
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
            DOTween.Kill(debrisId);
            settleTween = null;
        }

        private void StopAnimations()
        {
            DOTween.Kill(this);
            DOTween.Kill(debrisId);
            settleTween = null;
            draggedBlock = -1;
            ResetPresentation();

            // The passes died with their tweens; presentations waiting on them
            // are abandoned, never reported, like the presentation itself.
            passesInFlight = 0;
            doneAfterPasses.Clear();
        }

        /// <summary>
        /// Stops a settle where it is. A presentation waiting on it starts its
        /// stages rather than waiting for a completion that will never come.
        /// </summary>
        private void KillSettle()
        {
            if (settleTween == null)
            {
                return;
            }

            settleTween.Kill();
            OnSettled();
        }

        /// <summary>
        /// The settle is over, finished or killed: input may resume, and a
        /// presentation waiting for it starts its stages.
        /// </summary>
        private void OnSettled()
        {
            settleTween = null;
            if (isPresenting && !hasStagesStarted)
            {
                PlayLeave();
            }
        }

        /// <summary>
        /// Draws <paramref name="state"/> into a fresh <c>Board</c> group,
        /// creating the view's groups first when they do not exist.
        /// </summary>
        private void Draw(BoardState state)
        {
            EnsureGroups();
            drawnState = state;

            DrawFloor();
            DrawElevators(state);
            DrawFrame();
            DrawGates(state);
            DrawGenerators(state);
            DrawBlocks(state);
            DrawShutters(state);
        }

        /// <summary>
        /// The view's own redraw between the two stages of a presentation:
        /// stops the presentation's and the lift's tweens, which may still move
        /// parts of the old drawing, and replaces the <c>Board</c> group. Debris
        /// — another id, another group — flies on.
        /// </summary>
        private void Redraw(BoardState state)
        {
            DOTween.Kill(this);
            settleTween = null;
            draggedBlock = -1;
            ForgetDrawing();

            if (board != null)
            {
                Destroy(board.gameObject);
                board = null;
            }

            Draw(state);
        }

        /// <summary>Forgets every lookup into the current drawing.</summary>
        private void ForgetDrawing()
        {
            drawnBlocks.Clear();
            badges.Clear();
            elevatorDoors.Clear();
        }

        private void EnsureGroups()
        {
            if (board == null)
            {
                board = AddGroup(transform, "Board", Vector2.zero);
                board.SetAsFirstSibling();
            }

            if (debris == null || stage == null)
            {
                var effects = AddGroup(transform, "Effects", Vector2.zero);
                debris = AddGroup(effects, "Debris", Vector2.zero);
                stage = AddGroup(effects, "Stage", Vector2.zero);
            }
        }

        private static void DestroyChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            // No reparenting first: OnDisable calls this, and Unity refuses
            // hierarchy changes while a parent is being deactivated.
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// One floor tile per grid cell, walls included — the gap around a wall
        /// drawn as frame then reads as a groove in the floor — over a backing
        /// that reaches under the frame, so no background shows between them.
        /// </summary>
        private void DrawFloor()
        {
            var underlay = config.FloorUnderlayCells;
            AddSquare(
                board, "Floor underlay",
                GridToLocal(new Vector2(ctx.Width * 0.5f, ctx.Height * 0.5f)),
                new Vector2(CellsToWorld(ctx.Width + 2f * underlay), CellsToWorld(ctx.Height + 2f * underlay)),
                config.FloorColor, config.FloorUnderlayOrder);

            var floor = AddGroup(board, "Floor", Vector2.zero);
            var tile = new Vector2(layout.CellSize, layout.CellSize);
            for (var y = 0; y < ctx.Height; y++)
            {
                for (var x = 0; x < ctx.Width; x++)
                {
                    var cell = new Coord(x, y);
                    AddSprite(floor, $"Floor {cell}", config.FloorSprite, layout.CellCenter(cell), tile, config.FloorColor, config.FloorOrder);
                }
            }
        }

        /// <summary>The frame ring and every wall cell, lip then face.</summary>
        private void DrawFrame()
        {
            var lip = AddGroup(board, "Frame lip", LipOffset());
            AddQuarters(lip, frameTiles, frameQuarterRect, config.LipFill(config.FrameColor), config.FrameLipOrder);

            var face = AddGroup(board, "Frame", Vector2.zero);
            AddQuarters(face, frameTiles, frameQuarterRect, config.FrameColor, config.FrameOrder);
        }

        /// <summary>
        /// A gate is its own segment of the frame, with a lip and a face. Open,
        /// it is in the gate's colour with a white arrow pointing out of the
        /// board. Closed (M2), it is ice with frost on each of its cells and the
        /// clears still needed on a badge at its middle, and no arrow.
        /// </summary>
        private void DrawGates(BoardState state)
        {
            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                var gate = ctx.Gates[g];
                var visual = visibility.Gate(state, g);
                var span = GateSpanLocal(g);

                var fill = visual.IsOpen ? config.BlockFill(visual.Color.Value) : config.IceColor;
                var root = AddGroup(board, $"Gate {gate.Id}", Vector2.zero);
                var lip = AddGroup(root, "Lip", LipOffset());
                AddQuarters(lip, gateTiles[g], frameQuarterRect, config.LipFill(fill), config.FrameLipOrder);
                AddQuarters(root, gateTiles[g], frameQuarterRect, fill, config.FrameOrder);

                if (visual.IsOpen)
                {
                    var arrow = CellsToWorld(config.GateArrowSizeCells);
                    AddSprite(
                        root, "Arrow", config.GateArrowSprite, span.center, new Vector2(arrow, arrow),
                        config.GateArrowColor, config.GateMarkOrder,
                        Vector2.SignedAngle(Vector2.up, GateExit.Outward(gate.Edge)));
                    continue;
                }

                for (var i = 0; i < gate.Width; i++)
                {
                    var cell = GridRectToLocal(GateCellGridRect(g, i));
                    AddSprite(root, $"Frost {i}", config.FrostSprite, cell.center, cell.size, config.FrostColor, config.GateMarkOrder);
                }

                badges[new CountPop(CountKind.Gate, g)] =
                    DrawBadge(root, span.center, visual.OpensInClears, config.BadgeRimColor);
            }
        }

        /// <summary>
        /// A generator with blocks still queued is a machine outside the frame
        /// (D48): a body with a lip, a dark screen showing the next block in
        /// miniature — in ice when it would spawn frozen (M3) — and the queued
        /// count on a badge at its outer end. An exhausted one is destroyed
        /// (M6) and draws nothing.
        /// </summary>
        private void DrawGenerators(BoardState state)
        {
            var corner = CellsToWorld(config.MachineCornerCells);
            for (var g = 0; g < ctx.Generators.Count; g++)
            {
                var visual = visibility.Generator(state, g);
                if (!visual.IsShown)
                {
                    continue;
                }

                var placement = machines[g];
                var body = GridRectToLocal(placement.Body);
                var screen = GridRectToLocal(placement.Screen);
                var root = AddGroup(board, $"Generator {ctx.Generators[g].Id}", Vector2.zero);

                var lip = AddGroup(root, "Lip", LipOffset());
                AddSliced(lip, "Body", config.RoundedRectSprite, body.center, body.size, corner, config.LipFill(config.MachineColor), config.MachineLipOrder);
                AddSliced(root, "Body", config.RoundedRectSprite, body.center, body.size, corner, config.MachineColor, config.MachineOrder);
                AddSliced(root, "Screen", config.RoundedRectSprite, screen.center, screen.size, corner, config.MachineScreenColor, config.MachineScreenOrder);

                var fill = visual.IsNextFrozen ? config.IceColor : config.BlockFill(visual.NextColor.Value);
                DrawMiniature(root, visual.NextCells, screen, fill);

                badges[new CountPop(CountKind.Generator, g)] =
                    DrawBadge(root, GridToLocal(placement.BadgeCenter), visual.Queued, config.BadgeRimColor);
            }
        }

        /// <summary>
        /// The next block on a machine's screen: its quarters and lip, scaled
        /// uniformly to fit <paramref name="screen"/> (<see cref="MiniatureFit"/>)
        /// and centred on it. Blocks never rotate, so it shows the block as it
        /// will arrive. No studs and no marks: it is a preview of shape and
        /// colour.
        /// </summary>
        private void DrawMiniature(Transform parent, IReadOnlyList<Coord> cells, Rect screen, Color fill)
        {
            var perCell = MiniatureFit(cells, screen, out var shapeCenter);
            var tiles = BlockTiling.Compute(cells);

            Rect RectOf(QuarterTile tile)
            {
                var cellRect = BlockTiling.QuarterRect(tile);
                return new Rect(screen.center + (cellRect.min - shapeCenter) * perCell, cellRect.size * perCell);
            }

            var miniature = AddGroup(parent, "Next block", Vector2.zero);
            var lip = AddGroup(miniature, "Lip", LipOffset() * (perCell / layout.CellSize));
            AddQuarters(lip, tiles, RectOf, config.LipFill(fill), config.MiniatureLipOrder);
            AddQuarters(miniature, tiles, RectOf, fill, config.MiniatureOrder);
        }

        /// <summary>
        /// The world units per cell at which <paramref name="cells"/> fit a
        /// machine's <paramref name="screen"/>, and the centre of their bounding
        /// box in cells. The one fit for the miniature and for a spawned block
        /// growing out of it.
        /// </summary>
        private static float MiniatureFit(IReadOnlyList<Coord> cells, Rect screen, out Vector2 shapeCenter)
        {
            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            var spanX = maxX + 1 - minX;
            var spanY = maxY + 1 - minY;
            shapeCenter = new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);
            return Mathf.Min(screen.width / spanX, screen.height / spanY);
        }

        private void DrawBlocks(BoardState state)
        {
            var beneathSide = CellsToWorld(config.BeneathColorSize);

            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                var visual = visibility.Block(state, i);
                if (!visual.IsShown)
                {
                    continue;
                }

                var root = AddGroup(board, $"Block {i}", OriginToLocal(state.Origins[i]));
                var body = AddGroup(root, "Body", Vector2.zero);

                var spec = ctx.SpecAt(i);
                var cells = spec.Cells;
                var tiles = BlockTiling.Compute(cells);

                // M3: a frozen block is ice, never its colour.
                var fill = visual.IsFrozen ? config.IceColor : config.BlockFill(visual.OuterColor.Value);

                var lip = AddGroup(body, "Lip", LipOffset());
                AddQuarters(lip, tiles, blockQuarterRect, config.LipFill(fill), config.BlockLipOrder);

                var drawn = new DrawnBlock(root, body, FootprintCenterInBlock(cells), lip, cells, tiles, spec.Axis, visual.IsFrozen);
                drawn.Face = DrawFace(drawn, fill);
                drawnBlocks[i] = drawn;

                if (visual.BeneathColor.HasValue)
                {
                    var beneath = config.BlockFill(visual.BeneathColor.Value);
                    for (var c = 0; c < cells.Count; c++)
                    {
                        drawn.BeneathSquares.Add(AddSquare(
                            body, $"Beneath {cells[c]}", CellCenterInBlock(cells[c]), new Vector2(beneathSide, beneathSide),
                            beneath, config.BeneathColorOrder));
                    }
                }

                DrawBlockMarks(i, body, cells, visual);
            }
        }

        /// <summary>
        /// A block's face under its body: the quarter tiles, then frost on
        /// every cell when it is ice, studs with their gloss on every cell when
        /// it is not, and — for an axis-restricted block (M7), ice or not —
        /// one double-headed arrow along its axis instead of studs.
        /// </summary>
        private Transform DrawFace(DrawnBlock block, Color fill)
        {
            var face = AddGroup(block.Body, "Face", Vector2.zero);
            AddQuarters(face, block.Tiles, blockQuarterRect, fill, config.BlockOrder);

            var cellSize = new Vector2(layout.CellSize, layout.CellSize);
            for (var c = 0; c < block.Cells.Count; c++)
            {
                var cell = block.Cells[c];
                var center = CellCenterInBlock(cell);
                if (block.IsIce)
                {
                    AddSprite(face, $"Frost {cell}", config.FrostSprite, center, cellSize, config.FrostColor, config.StudOrder);
                }
                else if (block.Axis == MovementAxis.Free)
                {
                    AddSprite(face, $"Studs {cell}", config.StudsSprite, center, cellSize, fill, config.StudOrder);
                    AddSprite(face, $"Gloss {cell}", config.StudGlossSprite, center, cellSize, config.GlossColor, config.GlossOrder);
                }
            }

            if (block.Axis != MovementAxis.Free)
            {
                DrawAxisArrow(face, block);
            }

            return face;
        }

        /// <summary>
        /// The M7 arrow: a 9-sliced sprite through the centre of the
        /// footprint's bounding box, along the block's axis, stopping
        /// <see cref="RuntimeConfig.AxisArrowEndInsetCells"/> short of each end.
        /// It is scaled uniformly to its thickness and stretched only in its
        /// sliced middle, so the heads keep their shape at any length.
        /// </summary>
        private void DrawAxisArrow(Transform face, DrawnBlock block)
        {
            MarkLayout.Bounds(block.Cells, out var minX, out var maxX, out var minY, out var maxY);
            var isHorizontal = block.Axis == MovementAxis.HorizontalOnly;
            var alongCells = isHorizontal ? maxX + 1 - minX : maxY + 1 - minY;
            var length = CellsToWorld(alongCells - 2f * config.AxisArrowEndInsetCells);

            var sprite = config.AxisArrowSprite;
            var spriteSize = sprite.bounds.size;
            var scale = CellsToWorld(config.AxisArrowThicknessCells) / spriteSize.y;

            var go = new GameObject("Axis arrow");
            go.transform.SetParent(face, false);
            go.transform.localPosition = block.FootprintCenter;
            go.transform.localRotation = Quaternion.Euler(
                0f, 0f, isHorizontal ? 0f : Vector2.SignedAngle(Vector2.right, Vector2.up));
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = config.SpriteMaterial;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(length / scale, spriteSize.y);
            renderer.color = config.AxisArrowColor;
            renderer.sortingOrder = config.GlossOrder;
        }

        /// <summary>
        /// Everything block <paramref name="blockIndex"/> carries on top of its
        /// face, under its body:
        /// <list type="bullet">
        /// <item>frozen (M3): the clears still needed on a badge at the
        /// footprint's anchor (<see cref="MarkLayout.Anchor"/>);</item>
        /// <item>locked (M8): chains and a padlock with the keys still required
        /// (<see cref="DrawLock"/>);</item>
        /// <item>carrying a key: a gold key at the anchor whose gem is its
        /// lock's colour (D47);</item>
        /// <item>layered (M4): the remaining-colour numeral on its first cell,
        /// as before.</item>
        /// </list>
        /// A frozen block's padlock or key is raised above its frozen badge so
        /// the two do not overlap.
        /// </summary>
        private void DrawBlockMarks(int blockIndex, Transform body, IReadOnlyList<Coord> cells, BlockVisual visual)
        {
            var anchor = MarkLayout.Anchor(cells) * layout.CellSize;
            if (visual.IsFrozen)
            {
                badges[new CountPop(CountKind.Frozen, blockIndex)] =
                    DrawBadge(body, anchor, visual.FrozenRemaining, config.BadgeRimColor);
            }

            var icon = IconPosition(cells, visual.IsFrozen);

            if (visual.IsLocked)
            {
                badges[new CountPop(CountKind.Padlock, blockIndex)] =
                    DrawLock(body, cells, icon, visual.KeysStillRequired);
            }

            if (visual.KeyMarkColor.HasValue)
            {
                var bounds = config.KeyBodySprite.bounds.size;
                var length = CellsToWorld(config.KeySizeCells);
                var size = new Vector2(length, length * bounds.y / bounds.x);
                AddSprite(body, "Key", config.KeyBodySprite, icon, size, config.KeyColor, config.IconOrder, config.KeyRotationDegrees);
                AddSprite(
                    body, "Key gem", config.KeyGemSprite, icon, size, config.BlockFill(visual.KeyMarkColor.Value),
                    config.KeyGemOrder, config.KeyRotationDegrees);
            }

            if (visual.LayerNumeral.HasValue)
            {
                AddLabel(body, "Layer count", CellCenterInBlock(cells[0]), visual.LayerNumeral.Value, config.LabelFontSize, config.LabelColor);
            }
        }

        /// <summary>
        /// Where a block's padlock or key sits, relative to its body: the
        /// footprint's anchor, raised above the frozen badge on a frozen block.
        /// </summary>
        private Vector2 IconPosition(IReadOnlyList<Coord> cells, bool isFrozen)
        {
            var anchor = MarkLayout.Anchor(cells) * layout.CellSize;
            return isFrozen ? anchor + new Vector2(0f, CellsToWorld(config.FrozenMarkRaiseCells)) : anchor;
        }

        /// <summary>
        /// A lock (M8) under <paramref name="parent"/>, whose frame is a block
        /// body's: a chain along each row of <paramref name="cells"/>, and a
        /// gold padlock at <paramref name="icon"/> with
        /// <paramref name="keysStillRequired"/> on a badge on its body, or no
        /// badge when that is null. The one drawing of a lock, for a locked
        /// block and for the lock that fades away when it opens.
        /// </summary>
        /// <returns>The badge, or null when none was drawn.</returns>
        private Transform DrawLock(Transform parent, IReadOnlyList<Coord> cells, Vector2 icon, int? keysStillRequired)
        {
            var chains = AddGroup(parent, "Chains", Vector2.zero);
            var strips = MarkLayout.ChainStrips(cells, config.ChainThicknessCells, config.ChainEndInsetCells);
            var sprite = config.ChainSprite;
            var scale = CellsToWorld(config.ChainThicknessCells) / sprite.bounds.size.y;
            for (var s = 0; s < strips.Count; s++)
            {
                var strip = CellRectToLocal(strips[s]);
                AddTiled(chains, $"Chain {s}", sprite, strip.center, strip.size, scale, config.ChainColor, config.ChainOrder);
            }

            var padlock = CellsToWorld(config.PadlockSizeCells);
            AddSprite(parent, "Padlock", config.PadlockSprite, icon, new Vector2(padlock, padlock), config.PadlockColor, config.IconOrder);
            if (!keysStillRequired.HasValue)
            {
                return null;
            }

            return DrawBadge(
                parent, icon - new Vector2(0f, CellsToWorld(config.PadlockBadgeDropCells)),
                keysStillRequired.Value, config.BadgeRimColor);
        }

        /// <summary>
        /// Every closed shutter (M5); the blocks under one are not drawn at all.
        /// </summary>
        private void DrawShutters(BoardState state)
        {
            for (var s = 0; s < ctx.Shutters.Count; s++)
            {
                var visual = visibility.Shutter(state, s);
                if (!visual.IsClosed)
                {
                    continue;
                }

                var shutter = ctx.Shutters[s];
                var root = AddGroup(board, $"Shutter {shutter.Id}", Vector2.zero);
                var badge = DrawShutterPanel(root, s, visual);
                badges[new CountPop(CountKind.Shutter, s)] = badge;
            }
        }

        /// <summary>
        /// Shutter <paramref name="shutterIndex"/>'s panel under
        /// <paramref name="parent"/>, in the view's frame: slats over its whole
        /// region and a border, and — when <paramref name="badgeVisual"/> is
        /// given — the clears still needed on a badge at its centre, whose rim
        /// is the colour a colour-bound shutter counts. The one drawing of a
        /// shutter, for a closed one and for the panel that lifts off when it
        /// opens.
        /// </summary>
        /// <returns>The badge, or null when none was drawn.</returns>
        private Transform DrawShutterPanel(Transform parent, int shutterIndex, ShutterVisual? badgeVisual)
        {
            var shutter = ctx.Shutters[shutterIndex];
            var center = RegionCenter(shutter.Min, shutter.Max);
            var size = RegionSize(shutter.Min, shutter.Max);

            AddTiled(
                parent, "Slats", config.ShutterSlatsSprite, center, size, CellTileScale(config.ShutterSlatsSprite),
                config.ShutterSlatColor, config.ShutterOrder);
            AddSliced(
                parent, "Border", config.RingSprite, center, size, CellsToWorld(config.ShutterBorderCells),
                config.ShutterBorderColor, config.ShutterBorderOrder);

            if (!badgeVisual.HasValue)
            {
                return null;
            }

            var visual = badgeVisual.Value;
            var rim = visual.CountsColor.HasValue ? config.BlockFill(visual.CountsColor.Value) : config.BadgeRimColor;
            return DrawBadge(parent, center, visual.OpensInClears, rim);
        }

        /// <summary>
        /// An elevator (M9), until its final wave has been cleared: lift doors
        /// over its region with a divider down the middle and a border, above
        /// the floor and below the blocks, so a wave stands on the doors. How
        /// many waves remain is never shown (D48).
        /// </summary>
        private void DrawElevators(BoardState state)
        {
            for (var e = 0; e < ctx.Elevators.Count; e++)
            {
                if (!visibility.ElevatorPresent(state, e))
                {
                    continue;
                }

                var elevator = ctx.Elevators[e];
                var center = RegionCenter(elevator.Min, elevator.Max);
                var size = RegionSize(elevator.Min, elevator.Max);
                var root = AddGroup(board, $"Elevator {elevator.Id}", Vector2.zero);

                var doors = AddTiled(
                    root, "Doors", config.DoorPanelSprite, center, size, CellTileScale(config.DoorPanelSprite),
                    config.ElevatorDoorColor, config.ElevatorDoorOrder);

                var dividerWidth = CellsToWorld(config.ElevatorDividerCells);
                var divider = AddSliced(
                    root, "Divider", config.RoundedRectSprite, center, new Vector2(dividerWidth, size.y), dividerWidth * 0.5f,
                    config.ElevatorDividerColor, config.ElevatorDividerOrder);
                AddSliced(
                    root, "Border", config.RingSprite, center, size, CellsToWorld(config.ElevatorBorderCells),
                    config.ElevatorBorderColor, config.ElevatorBorderOrder);

                elevatorDoors[e] = divider != null
                    ? new[] { doors.gameObject, divider.gameObject }
                    : new[] { doors.gameObject };
            }
        }

        /// <summary>
        /// The one count badge (D48): a rounded box in <paramref name="rim"/>
        /// with a slightly smaller one in the badge colour on it, and
        /// <paramref name="value"/> in cream on top. It widens with the number
        /// of digits (<see cref="MarkLayout.BadgeWidth"/>).
        /// </summary>
        /// <returns>The badge's group, centred on <paramref name="center"/>: scaling it pops the badge.</returns>
        private Transform DrawBadge(Transform parent, Vector2 center, int value, Color rim)
        {
            var height = CellsToWorld(config.BadgeHeightCells);
            var width = CellsToWorld(config.BadgeWidthCells(value));
            var rimThickness = CellsToWorld(config.BadgeRimCells);
            var inner = new Vector2(width - 2f * rimThickness, height - 2f * rimThickness);

            var badge = AddGroup(parent, "Badge", center);
            AddSliced(badge, "Rim", config.RoundedRectSprite, Vector2.zero, new Vector2(width, height), height * 0.5f, rim, config.BadgeRimOrder);
            AddSliced(badge, "Fill", config.RoundedRectSprite, Vector2.zero, inner, inner.y * 0.5f, config.BadgeColor, config.BadgeOrder);
            AddLabel(badge, "Count", Vector2.zero, value, config.BadgeLabelFontSize, config.BadgeTextColor);
            return badge;
        }

        /// <summary>
        /// Draws <paramref name="tiles"/> under <paramref name="parent"/>, one
        /// renderer per quarter, each posed as its tile says
        /// (<see cref="PoseQuarter"/>). The one path for block faces and lips,
        /// the frame and its lip, gates and their lips, the next block on a
        /// generator's screen, and a grabbed block's outline; only the
        /// arguments differ.
        /// </summary>
        /// <param name="rectOf">Where a tile goes, in <paramref name="parent"/>'s local world units.</param>
        private List<SpriteRenderer> AddQuarters(
            Transform parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, Color color, int order)
        {
            var renderers = new List<SpriteRenderer>(tiles.Count);
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var renderer = AddRenderer(
                    parent, tile.ToString(), config.QuarterSprite(tile.SpriteKind), Vector2.zero, color, order,
                    tile.IsRotated ? QuarterTile.EdgeAlongYRotationDegrees : 0f);
                renderer.flipX = tile.FlipX;
                renderer.flipY = tile.FlipY;
                PoseQuarter(renderer, tile, rectOf(tile));
                renderers.Add(renderer);
            }

            return renderers;
        }

        /// <summary>
        /// Places a quarter renderer over <paramref name="rect"/>, enlarged by
        /// the seam overlap. A rotated sprite's own x runs along the board's y.
        /// </summary>
        private void PoseQuarter(SpriteRenderer renderer, QuarterTile tile, Rect rect)
        {
            var overlap = CellsToWorld(config.SeamOverlapCells) * 2f;
            var size = rect.size + new Vector2(overlap, overlap);
            var frameSize = tile.IsRotated ? new Vector2(size.y, size.x) : size;
            var bounds = renderer.sprite.bounds.size;
            renderer.transform.localPosition = rect.center;
            renderer.transform.localScale = new Vector3(frameSize.x / bounds.x, frameSize.y / bounds.y, 1f);
        }

        private static Transform AddGroup(Transform parent, string name, Vector2 localPosition)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = localPosition;
            return group;
        }

        /// <summary>Where a lip group sits relative to its face: straight down by the lip offset.</summary>
        private Vector2 LipOffset() => new Vector2(0f, -CellsToWorld(config.LipOffsetCells));

        private Vector2 GridToLocal(Vector2 grid) => layout.GridToWorld(grid);

        /// <summary>A rectangle in grid units, in this view's local world units.</summary>
        private Rect GridRectToLocal(Rect grid) =>
            new Rect(GridToLocal(grid.min), grid.size * layout.CellSize);

        /// <summary>A rectangle in cell units relative to a block's origin, in its body's local world units.</summary>
        private Rect CellRectToLocal(Rect cells) =>
            new Rect(cells.min * layout.CellSize, cells.size * layout.CellSize);

        /// <summary>Where a block's root sits for <paramref name="origin"/>: the origin cell's lower-left corner.</summary>
        private Vector2 OriginToLocal(Coord origin) => GridToLocal(new Vector2(origin.X, origin.Y));

        /// <summary>A gate's whole span on the frame, in this view's local world units.</summary>
        private Rect GateSpanLocal(int gateIndex)
        {
            var gate = ctx.Gates[gateIndex];
            return GridRectToLocal(FrameTiling.EdgeSpanRect(
                ctx.Width, ctx.Height, gate.Edge, gate.Offset, gate.Width, layout.FrameThicknessCells));
        }

        /// <summary>Cell <paramref name="i"/> of a gate's span on the frame, in grid units.</summary>
        private Rect GateCellGridRect(int gateIndex, int i)
        {
            var gate = ctx.Gates[gateIndex];
            return FrameTiling.EdgeSpanRect(ctx.Width, ctx.Height, gate.Edge, gate.Offset + i, 1, layout.FrameThicknessCells);
        }

        /// <summary>The centre of a footprint's bounding box relative to its block's root.</summary>
        private Vector2 FootprintCenterInBlock(IReadOnlyList<Coord> cells)
        {
            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            return new Vector2(CellsToWorld((minX + maxX + 1) * 0.5f), CellsToWorld((minY + maxY + 1) * 0.5f));
        }

        private float CellsToWorld(float cells) => cells * layout.CellSize;

        /// <summary>A footprint cell's centre relative to its block's root, which sits at the origin's corner.</summary>
        private Vector2 CellCenterInBlock(Coord cell) =>
            new Vector2(CellsToWorld(cell.X + 0.5f), CellsToWorld(cell.Y + 0.5f));

        private Vector2 RegionCenter(Coord min, Coord max) =>
            GridToLocal(new Vector2((min.X + max.X + 1) * 0.5f, (min.Y + max.Y + 1) * 0.5f));

        private Vector2 RegionSize(Coord min, Coord max) =>
            new Vector2(CellsToWorld(max.X - min.X + 1), CellsToWorld(max.Y - min.Y + 1));

        /// <summary>The uniform scale at which one tile of a cell-sized sprite covers one cell.</summary>
        private float CellTileScale(Sprite sprite) => layout.CellSize / sprite.bounds.size.x;

        /// <summary>A tinted copy of the config's plain square: the floor's backing and beneath squares.</summary>
        private SpriteRenderer AddSquare(Transform parent, string name, Vector2 localCenter, Vector2 size, Color color, int order) =>
            AddSprite(parent, name, config.CellSprite, localCenter, size, color, order);

        /// <summary>
        /// A tinted <paramref name="sprite"/> centred on
        /// <paramref name="localCenter"/> and turned by
        /// <paramref name="rotationDegrees"/>, scaled so that before the turn it
        /// covers <paramref name="size"/> world units, whatever the sprite's own
        /// pixels per unit.
        /// </summary>
        private SpriteRenderer AddSprite(
            Transform parent, string name, Sprite sprite, Vector2 localCenter, Vector2 size, Color color, int order,
            float rotationDegrees = 0f)
        {
            var renderer = AddRenderer(parent, name, sprite, localCenter, color, order, rotationDegrees);
            var bounds = sprite.bounds.size;
            renderer.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
            return renderer;
        }

        /// <summary>
        /// A 9-sliced <paramref name="sprite"/> covering <paramref name="size"/>
        /// world units, scaled uniformly so its slice border draws
        /// <paramref name="border"/> wide — a rounded box's corner, a ring's
        /// width — at any size. The border is capped at half the smaller side,
        /// so the corners never overlap. Draws nothing, and returns null, when
        /// the border is not positive.
        /// </summary>
        private SpriteRenderer AddSliced(
            Transform parent, string name, Sprite sprite, Vector2 localCenter, Vector2 size, float border, Color color, int order)
        {
            var drawn = Mathf.Min(border, Mathf.Min(size.x, size.y) * 0.5f);
            if (!(drawn > 0f))
            {
                return null;
            }

            var spriteBorder = sprite.border.x / sprite.pixelsPerUnit;
            var scale = drawn / spriteBorder;
            var renderer = AddRenderer(parent, name, sprite, localCenter, color, order, 0f);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size / scale;
            return renderer;
        }

        /// <summary>
        /// <paramref name="sprite"/> repeated to cover <paramref name="size"/>
        /// world units, each tile drawn at <paramref name="scale"/> times its
        /// own size: slats and doors over a region, a chain along a row.
        /// </summary>
        private SpriteRenderer AddTiled(
            Transform parent, string name, Sprite sprite, Vector2 localCenter, Vector2 size, float scale, Color color, int order)
        {
            var renderer = AddRenderer(parent, name, sprite, localCenter, color, order, 0f);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size / scale;
            return renderer;
        }

        private SpriteRenderer AddRenderer(
            Transform parent, string name, Sprite sprite, Vector2 localCenter, Color color, int order, float rotationDegrees)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = config.SpriteMaterial;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void AddLabel(Transform parent, string name, Vector2 localCenter, int value, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;

            var label = go.AddComponent<TextMeshPro>();
            label.font = config.LabelFont;
            label.text = value.ToString(CultureInfo.InvariantCulture);
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = new Vector2(layout.CellSize, layout.CellSize);
            label.sortingOrder = config.LabelOrder;
        }

        /// <summary>A drawn block: its root, its body, and the parts its effects animate.</summary>
        private sealed class DrawnBlock
        {
            public DrawnBlock(
                Transform root, Transform body, Vector2 footprintCenter, Transform lip,
                IReadOnlyList<Coord> cells, IReadOnlyList<QuarterTile> tiles, MovementAxis axis, bool isIce)
            {
                Root = root;
                Body = body;
                FootprintCenter = footprintCenter;
                Lip = lip;
                Cells = cells;
                Tiles = tiles;
                Axis = axis;
                IsIce = isIce;
            }

            /// <summary>Sits at the origin's corner; moving it moves the whole block.</summary>
            public Transform Root { get; }

            /// <summary>Holds every part; scaled about <see cref="FootprintCenter"/> by the lift, a spawn and a rise.</summary>
            public Transform Body { get; }

            /// <summary>The centre of the footprint's bounding box, relative to <see cref="Root"/> and <see cref="Body"/>.</summary>
            public Vector2 FootprintCenter { get; }

            /// <summary>The lip's quarters, offset below the face.</summary>
            public Transform Lip { get; }

            /// <summary>The face group: quarters plus studs and gloss, frost, or the axis arrow. A peel draws a new one.</summary>
            public Transform Face { get; set; }

            /// <summary>The footprint, relative to the origin.</summary>
            public IReadOnlyList<Coord> Cells { get; }

            /// <summary>The footprint's quarter tiles, shared by lip, face and outline.</summary>
            public IReadOnlyList<QuarterTile> Tiles { get; }

            /// <summary>The block's movement axis: a restricted one shows an arrow instead of studs.</summary>
            public MovementAxis Axis { get; }

            /// <summary>True for a frozen block (M3): its face is ice with frost, without studs.</summary>
            public bool IsIce { get; }

            /// <summary>The beneath-colour squares of a layered block (M4); empty otherwise.</summary>
            public List<SpriteRenderer> BeneathSquares { get; } = new List<SpriteRenderer>();

            /// <summary>How far the block is lifted, from 0 (resting) to 1 (held).</summary>
            public float LiftAmount { get; set; }

            /// <summary>The lift or drop playing on this block; null when none is.</summary>
            public Tween LiftTween { get; set; }

            /// <summary>The sorting group that lifts the block above the board while it is lifted; null at rest.</summary>
            public SortingGroup LiftGroup { get; set; }

            /// <summary>The outline's group while lifted; null at rest.</summary>
            public Transform Outline { get; set; }

            /// <summary>The outline's quarter renderers, in <see cref="Tiles"/> order.</summary>
            public List<SpriteRenderer> OutlineQuarters { get; set; }
        }

        /// <summary>
        /// Every sprite and label under one root with the colour it was drawn
        /// in, so a fade can scale their alpha from the original rather than
        /// compounding frame on frame.
        /// </summary>
        private sealed class Fade
        {
            private readonly SpriteRenderer[] sprites;
            private readonly Color[] spriteColors;
            private readonly TMP_Text[] labels;
            private readonly float[] labelAlphas;

            public Fade(Transform root)
            {
                sprites = root.GetComponentsInChildren<SpriteRenderer>();
                spriteColors = new Color[sprites.Length];
                for (var i = 0; i < sprites.Length; i++)
                {
                    spriteColors[i] = sprites[i].color;
                }

                labels = root.GetComponentsInChildren<TMP_Text>();
                labelAlphas = new float[labels.Length];
                for (var i = 0; i < labels.Length; i++)
                {
                    labelAlphas[i] = labels[i].alpha;
                }
            }

            /// <summary>Sets every part to <paramref name="opacity"/> times its original alpha.</summary>
            public void Apply(float opacity)
            {
                for (var i = 0; i < sprites.Length; i++)
                {
                    var color = spriteColors[i];
                    color.a *= opacity;
                    sprites[i].color = color;
                }

                for (var i = 0; i < labels.Length; i++)
                {
                    labels[i].alpha = labelAlphas[i] * opacity;
                }
            }
        }
    }
}
