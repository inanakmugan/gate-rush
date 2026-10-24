using System;
using System.Collections.Generic;
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
    /// the drawn state — its floor and frame drawn once per drawing of the
    /// level and kept, everything else redrawn on every move (pass 3) — and
    /// <c>Effects</c>, holding <c>Debris</c> — cubes and ice shards still in
    /// flight — <c>Stage</c>, the temporary pieces of a presentation, and
    /// <c>Preview</c>, the generator preview of a drag (Module 26).
    /// Positions are local to this transform: the board is centred on it, and
    /// both groups sit at its origin.</para>
    /// <para><b>Shapes from quarters (Module 15).</b> Blocks, the frame, gates
    /// and the next block on a generator's screen are drawn from the generated
    /// quarter sprites, laid out by <see cref="BlockTiling"/> and
    /// <see cref="FrameTiling"/> and tinted at runtime. Each is drawn as a
    /// darker copy (the lip), then the face. The frame's and a gate's lip is
    /// offset downward. A block's is not: it fills the footprint's quarters,
    /// and the face over it is raised on its downward sides
    /// (<see cref="FaceShape"/>), so the lip shows as a strip along the
    /// block's lower outline and nothing of a resting block is drawn outside
    /// its cells. Since Module 46 (D79) blocks, the frame and gates also
    /// carry an untinted outline under the lip — the contour pieces, whole
    /// silhouettes the face pieces sit inside — and an untinted shine over
    /// the face along their top and bottom edges
    /// (<see cref="QuarterLayers"/>).</para>
    /// <para><b>State (Module 16, D48; lit in Module 46's second pass).</b> A
    /// frozen block and a closed gate lie under ice, with frost on a block,
    /// and a count; a locked block carries tube chains along each row of its
    /// cells and a gold padlock with the keys still required on it; a
    /// key-carrying block carries a gold key whose gem is its lock's colour
    /// (D47); a closed shutter is cylinder slats in a gold rim with a count; a
    /// generator is a metal machine outside the frame whose screen shows its
    /// next block and its count; an elevator is a rim with hazard strips and
    /// a pair of doors under its blocks. Counts on blocks, gates and shutters
    /// sit on one badge (<see cref="BoardRecipes{TGroup, TPiece}.Badge"/>);
    /// each count is recorded by its owner so it can pop.</para>
    /// <para><b>Shared recipes (Module 28).</b> How a block, a mark, a gate,
    /// a shutter's panel, an elevator's doors and a generator's machine are
    /// composed is <see cref="BoardRecipes{TGroup, TPiece}"/>', which the
    /// introduction cards draw with too. This view is the recipes' surface
    /// (<see cref="IRecipeSurface{TGroup, TPiece}"/>): it turns each piece
    /// they ask for into the renderer its own helpers make. The floor, the
    /// frame, which marks a block gets and everything a move animates are
    /// drawn here alone.</para>
    /// <para><b>Blocks.</b> Each shown block gets a root at its origin's corner
    /// and, under it, a <c>Body</c> holding everything it draws: a
    /// <c>Contour</c> group, a <c>Lip</c> group, a <c>Face</c> group
    /// (quarters, the shine, then studs and gloss or the axis arrow, a
    /// layered block's <c>Inner</c> window, and a frozen block's ice and
    /// frost), then chains, padlock
    /// or key, badges and labels. The drag, the settle and the exit move the root; the lift,
    /// a spawn and a rise scale the body about the footprint's centre, so the
    /// two never overwrite each other.</para>
    /// <para><b>Layered blocks (Modules 20 and 46, M4).</b> A block with a
    /// colour beneath is drawn in its outer colour darkened, with a recessed
    /// window set into it over its studs: its own quarter tiles drawn again,
    /// each pulled inward by <see cref="FaceShape"/>, as a dark rim and,
    /// inside it, a smaller block in the colour beneath
    /// with its own studs (<see cref="LayerStuds"/>). A peel re-poses these
    /// same renderers.</para>
    /// <para><b>Tweens.</b> DOTween keeps static state, and Enter Play Mode
    /// runs without a domain reload. Every tween of a presentation, a lift or
    /// a settle carries this view as its id. Debris tweens carry
    /// <see cref="debrisId"/>, an object this view owns, so the view's own
    /// redraws — which kill the presentation's tweens — leave debris flying.
    /// <see cref="Rebuild"/>, <see cref="Clear"/>, disabling and destroying
    /// kill both ids; nothing this view starts outlives it.</para>
    /// </remarks>
    public sealed partial class BoardView : MonoBehaviour, IRecipeSurface<Transform, SpriteRenderer>
    {
        private readonly Dictionary<int, DrawnBlock> drawnBlocks = new Dictionary<int, DrawnBlock>();

        // Every badge drawn, by what it counts, so a count pop can find it.
        private readonly Dictionary<CountPop, Transform> badges = new Dictionary<CountPop, Transform>();

        // Per elevator, its doors and divider, hidden while a wave rises.
        private readonly Dictionary<int, GameObject[]> elevatorDoors = new Dictionary<int, GameObject[]>();

        // Per elevator, the group its whole lift is drawn under, which sinks
        // away as debris once its last wave has cleared (Module 47).
        private readonly Dictionary<int, Transform> elevatorRoots = new Dictionary<int, Transform>();

        // The debris renderers, made once per view and config and lent to
        // every cube, shard, sparkle and gate glow (Module 47). Its root sits
        // beside the view's groups and outlives Clear.
        private DebrisPool pool;

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
        private BurstSettings breakCubeBurst;
        private BurstSettings breakSparkleBurst;
        // The recipes the introduction cards share (Module 28), drawing on
        // this view as their surface.
        private BoardRecipes<Transform, SpriteRenderer> recipes;

        // Static per level, so worked out once in Initialize.
        private IReadOnlyList<QuarterTile> frameTiles;
        private IReadOnlyList<QuarterTile>[] gateTiles;
        private MachinePlacement[] machines;
        private Func<QuarterTile, Rect> blockQuarterRect;
        private Func<QuarterTile, Rect> frameQuarterRect;

        // A gate's quarters for its recipe: in cells, in this view's frame.
        private Func<QuarterTile, Rect> gateQuarterCells;

        private Transform board;

        // The floor's and the frame's groups under Board, in drawing order:
        // drawn once per drawing of the level and kept by every Redraw, as
        // nothing a move does changes them (pass 3). Empty until drawn.
        private readonly List<Transform> floorAndFrame = new List<Transform>();

        // The first of the frame's groups, before which elevators sit among
        // Board's children.
        private Transform frameContour;

        private Transform debris;
        private Transform stage;
        private Transform preview;

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

            // Another level, layout or config: the floor and frame drawn for
            // the last one no longer fit, whatever draws next.
            DiscardFloorAndFrame();
            this.config = config;
            this.ctx = ctx;
            this.layout = layout;
            this.visibility = visibility;
            cubeBurst = config.CreateCubeBurst();
            shardBurst = config.CreateShardBurst();
            breakCubeBurst = config.CreateBreakCubeBurst();
            breakSparkleBurst = config.CreateBreakSparkleBurst();            recipes = new BoardRecipes<Transform, SpriteRenderer>(config, this);
            EnsurePool();

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
            gateQuarterCells = tile => GridRectToViewCells(
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
        /// <see cref="Settle"/> after this move that block. Every generator
        /// whose next block would land on a cell it stands on previews that
        /// block there (<see cref="ShowGeneratorPreview"/>) until
        /// <see cref="Settle"/> or <see cref="Snap"/>.
        /// </summary>
        public void BeginDrag(int blockIndex)
        {
            KillSettle();
            ReleasePullGlow();
            draggedBlock = blockIndex;
            if (drawnBlocks.TryGetValue(blockIndex, out var block))
            {
                RemoveGateClip(block);
                Lift(block);
                ShowGeneratorPreview(blockIndex);
            }
        }

        /// <summary>
        /// Shows the dragged block at the continuous origin
        /// <paramref name="position"/>, in cell units, and the pull on it —
        /// fed every frame from <see cref="DragController.Position"/> and
        /// <see cref="DragController.Pull"/>. The pull's nudge into the gate's
        /// mouth is drawn on top of the position, clipped at the gate's inner
        /// line for as long as the block's footprint crosses it
        /// (<see cref="ClipDraggedAtGate"/>), and the gate glows
        /// (<see cref="ShowPullGlow"/>). A block that is not drawn is not
        /// moved.
        /// </summary>
        public void ShowDragged(Vector2 position, PullTarget? pull)
        {
            if (drawnBlocks.TryGetValue(draggedBlock, out var block))
            {
                var drawn = pull.HasValue ? position + pull.Value.Nudge : position;
                block.Root.localPosition = GridToLocal(drawn);
                ClipDraggedAtGate(block, drawn, pull);
            }

            ShowPullGlow(pull);
        }

        /// <summary>
        /// Tweens the dragged block from where it was released into the cell
        /// <paramref name="origin"/>, and drops its lift. A block already there
        /// — a push in place, or a release exactly on a cell — is placed at once
        /// with no tween, so a presentation waiting on the settle starts without
        /// delay. <see cref="IsBusy"/> holds while the tween plays; the drop
        /// does not hold it. The pull's glow, if any, fades out, and so does
        /// the generator preview. A block
        /// released while nudged into its gate's mouth stays clipped at the
        /// gate's inner line until the settle is over.
        /// </summary>
        public void Settle(Coord origin)
        {
            KillSettle();
            ReleasePullGlow();
            HideGeneratorPreview();
            if (!drawnBlocks.TryGetValue(draggedBlock, out var block))
            {
                return;
            }

            Drop(block);

            var target = OriginToLocal(origin);
            if ((Vector2)block.Root.localPosition == target)
            {
                block.Root.localPosition = target;
                RemoveGateClip(block);
                return;
            }

            settleTween = block.Root
                .DOLocalMove(target, config.SettleSeconds)
                .SetEase(config.SettleEase)
                .SetId(this)
                .OnUpdate(() => HoldGateClip(block))
                .OnComplete(OnSettled);
        }

        /// <summary>
        /// Shows block <paramref name="blockIndex"/> at
        /// <paramref name="origin"/> at once, stopping any settle and dropping
        /// its lift — for a drag that is cancelled or whose move was rejected.
        /// The generator preview, if any, fades out. A block that is not
        /// drawn is not moved.
        /// </summary>
        public void Snap(int blockIndex, Coord origin)
        {
            KillSettle();
            ReleasePullGlow();
            HideGeneratorPreview();

            if (drawnBlocks.TryGetValue(blockIndex, out var block))
            {
                block.Root.localPosition = OriginToLocal(origin);
                RemoveGateClip(block);
                Drop(block);
            }
        }

        /// <summary>
        /// Stops every animation, debris included, and destroys everything
        /// drawn; the debris pool's renderers are only taken back, for the next
        /// drawing to lend again.
        /// </summary>
        public void Clear()
        {
            StopAnimations();
            ForgetDrawing();
            drawnState = null;

            // Destroy is deferred to the end of the frame, so the groups are
            // forgotten here and created afresh by the next drawing: nothing new
            // lands in a group that is about to go.
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (pool == null || child != pool.Root)
                {
                    DestroyDrawn(child.gameObject);
                }
            }
            board = null;
            floorAndFrame.Clear();
            frameContour = null;
            debris = null;
            stage = null;
            preview = null;
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
            ForgetPullGlow();
            ForgetGeneratorPreview();

            // Every debris tween is dead, so nothing returns a renderer late.
            pool?.ReturnAll();
            DestroyChildren(debris);
            DestroyChildren(stage);
            DestroyChildren(preview);
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
            ForgetPullGlow();
            ForgetGeneratorPreview();
            ResetPresentation();

            // Every debris tween is dead, so nothing returns a renderer late.
            pool?.ReturnAll();

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
        /// The settle is over, finished or killed: the settled block's drag
        /// clip goes, input may resume, and a presentation waiting for it
        /// starts its stages.
        /// </summary>
        private void OnSettled()
        {
            settleTween = null;
            if (drawnBlocks.TryGetValue(draggedBlock, out var settled))
            {
                RemoveGateClip(settled);
            }

            if (isPresenting && !hasStagesStarted)
            {
                PlayLeave();
            }
        }

        /// <summary>
        /// Draws <paramref name="state"/> into the <c>Board</c> group, creating
        /// the view's groups first when they do not exist. The floor and the
        /// frame are drawn only when the group has none yet; the rest — what a
        /// move can change — every time. Board's children always end in the
        /// same order: floor underlay, floor, elevators, the frame's three
        /// groups, gates, generators, blocks, shutters.
        /// </summary>
        private void Draw(BoardState state)
        {
            EnsureGroups();
            drawnState = state;

            if (floorAndFrame.Count == 0)
            {
                DrawFloor();
                DrawElevators(state);
                DrawFrame();
            }
            else
            {
                // Drawn after the kept frame, so moved back before it.
                var firstElevator = board.childCount;
                DrawElevators(state);
                for (var i = firstElevator; i < board.childCount; i++)
                {
                    board.GetChild(i).SetSiblingIndex(frameContour.GetSiblingIndex());
                }
            }

            DrawGates(state);
            DrawGenerators(state);
            DrawBlocks(state);
            DrawShutters(state);
        }

        /// <summary>
        /// The view's own redraw between the two stages of a presentation —
        /// what every move runs: stops the presentation's and the lift's
        /// tweens, which may still move parts of the old drawing, and replaces
        /// everything under <c>Board</c> but the floor and the frame, which no
        /// move changes and stay the same objects (pass 3). Debris — another
        /// id, another group — flies on, and a generator preview still fading
        /// out, whose tween went with the others, fades on
        /// (<see cref="ResumeGeneratorPreviewFade"/>).
        /// </summary>
        internal void Redraw(BoardState state)
        {
            DOTween.Kill(this);
            settleTween = null;
            draggedBlock = -1;
            ResumeGeneratorPreviewFade();
            ForgetDrawing();

            if (board != null)
            {
                for (var i = board.childCount - 1; i >= 0; i--)
                {
                    var child = board.GetChild(i);
                    if (!floorAndFrame.Contains(child))
                    {
                        DestroyDrawn(child.gameObject);
                    }
                }
            }

            Draw(state);
        }

        /// <summary>
        /// Destroys the floor and the frame, if drawn, so the next drawing
        /// draws them afresh: for <see cref="Initialize"/>, which may bind
        /// another level.
        /// </summary>
        private void DiscardFloorAndFrame()
        {
            for (var i = 0; i < floorAndFrame.Count; i++)
            {
                if (floorAndFrame[i] != null)
                {
                    DestroyDrawn(floorAndFrame[i].gameObject);
                }
            }

            floorAndFrame.Clear();
            frameContour = null;
        }

        /// <summary>Forgets every lookup into the current drawing.</summary>
        private void ForgetDrawing()
        {
            drawnBlocks.Clear();
            badges.Clear();
            elevatorDoors.Clear();
            elevatorRoots.Clear();
        }

        /// <summary>
        /// Makes the debris pool on first use, and again only when the config
        /// asks for another size or material — on binding a level, never in
        /// play. The old pool's renderers go with its root.
        /// </summary>
        private void EnsurePool()
        {
            if (pool != null && pool.Root != null && pool.Size == config.DebrisPoolSize && pool.Material == config.SpriteMaterial)
            {
                return;
            }

            if (pool != null && pool.Root != null)
            {
                DestroyDrawn(pool.Root.gameObject);
            }

            pool = new DebrisPool(transform, config.DebrisPoolSize, config.SpriteMaterial);
        }

        private void EnsureGroups()
        {
            if (board == null)
            {
                board = AddGroup(transform, "Board", Vector2.zero);
                board.SetAsFirstSibling();
            }

            if (debris == null || stage == null || preview == null)
            {
                var effects = AddGroup(transform, "Effects", Vector2.zero);
                debris = AddGroup(effects, "Debris", Vector2.zero);
                stage = AddGroup(effects, "Stage", Vector2.zero);
                preview = AddGroup(effects, "Preview", Vector2.zero);
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
                DestroyDrawn(parent.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Destroys a drawn object: at the end of the frame in Play Mode, as
        /// always; at once outside it, where <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> does
        /// nothing — the Edit Mode tests that draw and redraw a board.
        /// </summary>
        private static void DestroyDrawn(GameObject drawn)
        {
            if (Application.isPlaying)
            {
                Destroy(drawn);
            }
            else
            {
                DestroyImmediate(drawn);
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
            var square = AddSquare(
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

            floorAndFrame.Add(square.transform);
            floorAndFrame.Add(floor);
        }

        /// <summary>
        /// The frame ring and every wall cell, a raised wall lit like a block
        /// (Module 46): its shadow on the floor, its lit body and its
        /// highlight, faded by the frame's tint. The shadow keeps the
        /// outline's group name, <c>Frame contour</c>.
        /// </summary>
        private void DrawFrame()
        {
            var contour = AddGroup(board, "Frame contour", Vector2.zero);
            var margin = CellsToWorld(config.ShadowMarginCells);
            AddQuarters(
                contour, QuarterLayers.Edged(frameTiles), tile => FaceShape.ShadowQuarterRect(frameQuarterRect(tile), tile, margin),
                QuarterLayer.Shadow, Color.white, config.FrameContourOrder);

            var face = AddGroup(board, "Frame", Vector2.zero);
            AddQuarters(face, frameTiles, frameQuarterRect, QuarterLayer.Lit, config.FrameColor, config.FrameOrder);

            var shine = AddGroup(board, "Frame shine", Vector2.zero);
            AddQuarters(shine, frameTiles, frameQuarterRect, QuarterLayer.Body, config.FrameLightColor, config.FrameShineOrder);

            frameContour = contour;
            floorAndFrame.Add(contour);
            floorAndFrame.Add(face);
            floorAndFrame.Add(shine);
        }

        /// <summary>
        /// A gate is its own segment of the frame, a lit body. Open, it is in
        /// the gate's colour with a white arrow pointing out of the board.
        /// Closed (M2), it lies under ice with the clears still needed on a
        /// badge at its middle, and no arrow.
        /// </summary>
        private void DrawGates(BoardState state)
        {
            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                var gate = ctx.Gates[g];
                var visual = visibility.Gate(state, g);

                var root = AddGroup(board, $"Gate {gate.Id}", Vector2.zero);
                var badge = recipes.Gate(
                    root, gate, gateTiles[g], gateQuarterCells,
                    GridRectToViewCells(FrameTiling.EdgeSpanRect(
                        ctx.Width, ctx.Height, gate.Edge, gate.Offset, gate.Width, layout.FrameThicknessCells)),
                    visual.IsOpen ? visual.Color.Value : (BlockColor?)null, visual.OpensInClears);

                if (badge != null)
                {
                    badges[new CountPop(CountKind.Gate, g)] = badge;
                }
            }
        }

        /// <summary>
        /// A generator with blocks still queued is a machine outside the frame
        /// (D48): a raised metal body, a recessed glass screen showing the next
        /// block in miniature — in ice when it would spawn frozen (M3) — and the
        /// queued count at the screen's end. An exhausted one is destroyed (M6)
        /// and draws nothing.
        /// </summary>
        private void DrawGenerators(BoardState state)
        {
            for (var g = 0; g < ctx.Generators.Count; g++)
            {
                var visual = visibility.Generator(state, g);
                if (!visual.IsShown)
                {
                    continue;
                }

                var placement = machines[g];
                var root = AddGroup(board, $"Generator {ctx.Generators[g].Id}", Vector2.zero);
                var fill = visual.IsNextFrozen ? config.IceColor : config.BlockFill(visual.NextColor.Value);

                badges[new CountPop(CountKind.Generator, g)] = recipes.Machine(
                    root, GridRectToViewCells(placement.Body), GridRectToViewCells(placement.Screen), visual.NextCells, fill,
                    visual.Queued);
            }
        }

        /// <summary>
        /// A machine's <paramref name="screen"/> split along its longer side
        /// (Module 46, pass 2): the area the next block is shown in — its part
        /// of the screen shrunk about its centre to
        /// <paramref name="fillShare"/> — and at the screen's end, the right,
        /// or the bottom on an upright screen, the
        /// <paramref name="countShare"/> that holds the queued count. The one
        /// split for the drawn machine and for a spawned block growing out of
        /// it.
        /// </summary>
        public static (Rect block, Rect count) SplitScreen(Rect screen, float countShare, float fillShare)
        {
            Rect part;
            Rect count;
            if (screen.width >= screen.height)
            {
                var split = screen.xMax - screen.width * countShare;
                part = Rect.MinMaxRect(screen.xMin, screen.yMin, split, screen.yMax);
                count = Rect.MinMaxRect(split, screen.yMin, screen.xMax, screen.yMax);
            }
            else
            {
                var line = screen.yMin + screen.height * countShare;
                part = Rect.MinMaxRect(screen.xMin, line, screen.xMax, screen.yMax);
                count = Rect.MinMaxRect(screen.xMin, screen.yMin, screen.xMax, line);
            }

            var size = part.size * fillShare;
            return (new Rect(part.center - size * 0.5f, size), count);
        }

        /// <summary>
        /// Where an elevator's two doors stand in <paramref name="region"/>
        /// (Module 46, pass 2): each half of the recess inside the rim
        /// (<see cref="RuntimeConfig.ElevatorInnerInsetCells"/>), less the gap
        /// round it (<see cref="RuntimeConfig.ElevatorDoorGapCells"/>). The one
        /// rule for the drawn doors and for those that slide as a wave rises.
        /// </summary>
        public static (Rect left, Rect right) ElevatorDoorRects(RuntimeConfig config, Rect region)
        {
            var inset = config.ElevatorInnerInsetCells;
            var gap = config.ElevatorDoorGapCells;
            var inner = new Rect(region.x + inset.x, region.y + inset.y, region.width - 2f * inset.x, region.height - 2f * inset.y);
            var width = Mathf.Max(0f, inner.width * 0.5f - 2f * gap);
            var height = Mathf.Max(0f, inner.height - 2f * gap);
            return (
                new Rect(inner.xMin + gap, inner.yMin + gap, width, height),
                new Rect(inner.center.x + gap, inner.yMin + gap, width, height));
        }

        /// <summary>
        /// The units per cell at which <paramref name="cells"/> fit a
        /// machine's <paramref name="screen"/> — in the screen's own units —
        /// and the centre of their bounding box in cells. The one fit for the
        /// miniature and for a spawned block growing out of it.
        /// </summary>
        internal static float MiniatureFit(IReadOnlyList<Coord> cells, Rect screen, out Vector2 shapeCenter)
        {
            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            var spanX = maxX + 1 - minX;
            var spanY = maxY + 1 - minY;
            shapeCenter = new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);
            return Mathf.Min(screen.width / spanX, screen.height / spanY);
        }

        private void DrawBlocks(BoardState state)
        {
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

                // The shadow, the lit body and what it carries are the
                // shared recipe's (BoardRecipes.Block). M3: a frozen block
                // shows no colour, so VisibilityLayer gives it none beneath
                // either and it gets no inner shape.
                var beneath = visual.BeneathColor.HasValue ? config.BlockFill(visual.BeneathColor.Value) : (Color?)null;
                var parts = recipes.Block(body, cells, tiles, fill, beneath, visual.IsFrozen, spec.Axis);

                var drawn = new DrawnBlock(root, body, FootprintCenterInBlock(cells), cells, tiles, spec.Axis, visual.IsFrozen)
                {
                    FaceQuarters = parts.Face,
                    ShineQuarters = parts.Shine,
                    Studs = parts.Studs,
                    Layer = parts.Layer,
                };

                drawnBlocks[i] = drawn;

                DrawBlockMarks(i, body, cells, visual);
            }
        }

        /// <summary>
        /// Poses <paramref name="quarters"/> of <paramref name="layer"/> —
        /// drawn from <paramref name="tiles"/>, in their order — as a shape
        /// <paramref name="insetCells"/> inside the block's body
        /// (<see cref="FaceShape"/>); at 0 they are the body. What a peel
        /// re-poses the window's rim and inner block with.
        /// </summary>
        private void PoseInset(List<SpriteRenderer> quarters, IReadOnlyList<QuarterTile> tiles, QuarterLayer layer, float insetCells)
        {
            for (var i = 0; i < tiles.Count; i++)
            {
                PoseQuarter(quarters[i], tiles[i], layer, CellRectToLocal(FaceShape.QuarterRect(tiles[i], insetCells)));
            }
        }

        /// <summary>
        /// Poses <paramref name="quarters"/> — a layered block's inner
        /// highlight, drawn from <paramref name="tiles"/>, in their order —
        /// over a shape <paramref name="insetCells"/> inside the block's body,
        /// each keeping to its own span along the edge
        /// (<see cref="FaceShape.LightQuarterRect"/>).
        /// </summary>
        private void PoseShineInset(List<SpriteRenderer> quarters, IReadOnlyList<QuarterTile> tiles, float insetCells)
        {
            for (var i = 0; i < tiles.Count; i++)
            {
                var rect = FaceShape.LightQuarterRect(tiles[i], tiles, insetCells);
                PoseQuarter(quarters[i], tiles[i], QuarterLayer.Body, CellRectToLocal(rect));
            }
        }

        /// <summary>
        /// Everything block <paramref name="blockIndex"/> carries on top of its
        /// face, under its body:
        /// <list type="bullet">
        /// <item>frozen (M3): the clears still needed on a badge at the
        /// footprint's bottom-right corner (<see cref="MarkLayout.CornerBadge"/>);</item>
        /// <item>locked (M8): chains and a padlock with the keys still required
        /// (<see cref="DrawLock"/>);</item>
        /// <item>carrying a key: a gold key whose gem is its lock's colour
        /// (D47);</item>
        /// <item>carrying a time bonus (M10): the clock coin and the outlined
        /// "+N", with nothing behind them, fitted to the block
        /// (<see cref="BoardRecipes{TGroup, TPiece}.TimeBonusMark"/>);</item>
        /// <item>layered deeper than two (M4): the colours left on a badge at
        /// the footprint's bottom-right corner, which a frozen block never
        /// shows, so the two corner badges never meet.</item>
        /// </list>
        /// Where the padlock or key and the time-bonus mark go, alone or
        /// sharing the block, is <see cref="MarksOf"/>'s.
        /// </summary>
        private void DrawBlockMarks(int blockIndex, Transform body, IReadOnlyList<Coord> cells, BlockVisual visual)
        {
            var corner = MarkLayout.CornerBadge(cells, config.BlockBadgeInsetCells);
            if (visual.IsFrozen)
            {
                badges[new CountPop(CountKind.Frozen, blockIndex)] =
                    recipes.Badge(body, corner, visual.FrozenRemaining, config.CountBadgeRimColor);
            }

            var marks = MarksOf(cells, visual.IsLocked || visual.KeyMarkColor.HasValue, visual.TimeBonusSeconds);

            if (visual.IsLocked)
            {
                badges[new CountPop(CountKind.Padlock, blockIndex)] =
                    DrawLock(body, cells, marks, visual.KeysStillRequired);
            }

            if (visual.KeyMarkColor.HasValue)
            {
                recipes.Key(body, marks.Icon, marks.Scale, config.BlockFill(visual.KeyMarkColor.Value));
            }

            if (visual.TimeBonusSeconds > 0)
            {
                recipes.TimeBonusMark(body, marks.Bonus, marks.BonusScale, visual.TimeBonusSeconds);
            }

            if (visual.LayerNumeral.HasValue)
            {
                var badge = recipes.Badge(body, corner, visual.LayerNumeral.Value, config.CountBadgeRimColor);
                badge.name = "Layer count";
            }
        }

        /// <summary>
        /// Where a block's padlock or key and its time-bonus mark sit,
        /// relative to its body in cells, and how large. One of them alone
        /// sits at the footprint's anchor at full size; two take a row's
        /// slots, in that order, and where the row is crowded shrink
        /// (<see cref="RuntimeConfig.MarkScale"/>). The board's count badges
        /// sit in the bottom-right corner (Module 46), so they neither take a
        /// slot nor raise the marks, as the Level Editor's layout still does.
        /// The decision is <see cref="MarkLayout.Slots"/>'; this only adds the
        /// scales. The time-bonus mark of <paramref name="bonusSeconds"/>, when
        /// above 0, has its own: wider than a cell at full size (Module 46,
        /// pass 2), it is fitted to the width it has on the block
        /// (<see cref="MarkLayout.BonusRoomCells"/>,
        /// <see cref="RuntimeConfig.TimeBonusMarkScale"/>). The one rule for a
        /// block's drawing and for the lock that fades away when it opens, so
        /// the fading padlock is where the drawn one was.
        /// </summary>
        private BlockMarks MarksOf(IReadOnlyList<Coord> cells, bool hasIcon, int bonusSeconds)
        {
            var hasBonus = bonusSeconds > 0;
            var slots = MarkLayout.Slots(cells, false, hasIcon, false, hasBonus, config.FrozenMarkRaiseCells);
            var bonusScale = hasBonus
                ? config.TimeBonusMarkScale(
                    MarkLayout.BonusRoomCells(cells, hasIcon), slots.Crowding, config.TimeBonusMarkText(bonusSeconds))
                : 0f;
            return new BlockMarks(slots.Icon, slots.Layers, slots.Bonus, config.MarkScale(slots.Crowding), bonusScale);
        }

        /// <summary>
        /// A lock (M8) under <paramref name="parent"/>, whose frame is a block
        /// body's: chains, and a gold padlock where <paramref name="marks"/>
        /// puts the block's icon, at their scale, with
        /// <paramref name="keysStillRequired"/> on a badge on its body, or no
        /// badge when that is null (<see cref="BoardRecipes{TGroup, TPiece}.Lock"/>).
        /// For a locked block and for the lock that fades away when it opens.
        /// </summary>
        /// <returns>The badge, or null when none was drawn.</returns>
        private Transform DrawLock(Transform parent, IReadOnlyList<Coord> cells, BlockMarks marks, int? keysStillRequired) =>
            recipes.Lock(parent, cells, marks.Icon, marks.Scale, keysStillRequired);

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
        /// is the colour a colour-bound shutter counts
        /// (<see cref="BoardRecipes{TGroup, TPiece}.ShutterPanel"/>). For a
        /// closed shutter and for the panel that lifts off when it opens.
        /// </summary>
        /// <returns>The badge, or null when none was drawn.</returns>
        private Transform DrawShutterPanel(Transform parent, int shutterIndex, ShutterVisual? badgeVisual)
        {
            var shutter = ctx.Shutters[shutterIndex];
            var region = RegionViewCells(shutter.Min, shutter.Max);
            if (!badgeVisual.HasValue)
            {
                return recipes.ShutterPanel(parent, region, null, config.CountBadgeRimColor);
            }

            var visual = badgeVisual.Value;
            var rim = visual.CountsColor.HasValue ? config.BlockFill(visual.CountsColor.Value) : config.CountBadgeRimColor;
            return recipes.ShutterPanel(parent, region, visual.OpensInClears, rim);
        }

        /// <summary>
        /// An elevator (M9), until its final wave has been cleared: its rim
        /// with hazard strips round its region, and two doors in the recess
        /// inside it, above the floor and below the blocks, so a wave stands
        /// on the doors. How many waves remain is never shown (D48).
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
                var root = AddGroup(board, $"Elevator {elevator.Id}", Vector2.zero);
                var parts = recipes.Elevator(root, RegionViewCells(elevator.Min, elevator.Max));

                elevatorDoors[e] = new[] { parts.Doors.gameObject };
                elevatorRoots[e] = root;
            }
        }

        // ----- The recipes' surface (IRecipeSurface) -----
        //
        // Each takes cells in its parent's frame and hands them, in world
        // units, to the helper the rest of this view draws with, so a piece a
        // recipe draws is the renderer that helper has always made.

        Transform IRecipeSurface<Transform, SpriteRenderer>.Group(Transform parent, string name, Vector2 center, float scale)
        {
            var group = AddGroup(parent, name, center * layout.CellSize);
            group.localScale = new Vector3(scale, scale, 1f);
            return group;
        }

        List<SpriteRenderer> IRecipeSurface<Transform, SpriteRenderer>.Quarters(
            Transform parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, QuarterLayer layer, Color color,
            int order) =>
            AddQuarters(parent, tiles, tile => CellRectToLocal(rectOf(tile)), layer, color, order);

        SpriteRenderer IRecipeSurface<Transform, SpriteRenderer>.Sprite(
            Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size, Color color, int order,
            float rotationDegrees, SpriteLook look)
        {
            var renderer = AddSprite(parent, name, sprite, center * layout.CellSize, size * layout.CellSize, color, order, rotationDegrees);
            renderer.sharedMaterial = MaterialOf(look);
            return renderer;
        }

        SpriteRenderer IRecipeSurface<Transform, SpriteRenderer>.Sliced(
            Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size, float border, Color color, int order,
            SpriteLook look)
        {
            var renderer = AddSliced(parent, name, sprite, center * layout.CellSize, size * layout.CellSize, CellsToWorld(border), color, order);
            if (renderer != null)
            {
                renderer.sharedMaterial = MaterialOf(look);
            }

            return renderer;
        }

        SpriteRenderer IRecipeSurface<Transform, SpriteRenderer>.Tiled(
            Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size, float tileCells,
            bool tileIsMeasuredByHeight, Color color, int order, SpriteLook look)
        {
            var bounds = sprite.bounds.size;
            var scale = CellsToWorld(tileCells) / (tileIsMeasuredByHeight ? bounds.y : bounds.x);
            var renderer = AddTiled(parent, name, sprite, center * layout.CellSize, size * layout.CellSize, scale, color, order);
            renderer.sharedMaterial = MaterialOf(look);
            return renderer;
        }

        /// <summary>
        /// The strip is scaled uniformly to its thickness and stretched only
        /// in its sliced middle, along x or along y; never turned.
        /// </summary>
        SpriteRenderer IRecipeSurface<Transform, SpriteRenderer>.Strip(
            Transform parent, string name, Sprite sprite, Vector2 center, float length, float thickness, bool isAlongY,
            Color color, int order, SpriteLook look)
        {
            var spriteSize = sprite.bounds.size;
            var scale = CellsToWorld(thickness) / (isAlongY ? spriteSize.x : spriteSize.y);

            var renderer = AddRenderer(parent, name, sprite, center * layout.CellSize, color, order, 0f);
            renderer.sharedMaterial = MaterialOf(look);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.drawMode = SpriteDrawMode.Sliced;
            var along = CellsToWorld(length) / scale;
            renderer.size = isAlongY ? new Vector2(spriteSize.x, along) : new Vector2(along, spriteSize.y);
            return renderer;
        }

        void IRecipeSurface<Transform, SpriteRenderer>.Label(
            Transform parent, string name, Vector2 center, string text, float boardFontSize, Color color, bool isShadowed) =>
            AddLabel(
                parent, name, center * layout.CellSize, text, boardFontSize, color, config.LabelFont,
                isShadowed ? config.LabelShadowMaterial : null, false);

        void IRecipeSurface<Transform, SpriteRenderer>.OutlinedLabel(
            Transform parent, string name, Vector2 start, string text, float boardFontSize, Color color)
        {
            var hasOutline = config.HasLabelOutline;
            AddLabel(
                parent, name, start * layout.CellSize, text, boardFontSize, color,
                hasOutline ? config.LabelOutlineFont : config.LabelFont,
                hasOutline ? config.LabelOutlineMaterial : config.LabelShadowMaterial, true);
        }

        /// <summary>The material a SpriteRenderer drawing a sprite as <paramref name="look"/> uses.</summary>
        private Material MaterialOf(SpriteLook look)
        {
            switch (look)
            {
                case SpriteLook.Lit:
                    return config.LitMaterial;
                case SpriteLook.Highlight:
                    return config.QuarterMaterial(QuarterLayer.Body);
                default:
                    return config.SpriteMaterial;
            }
        }

        /// <summary>
        /// Draws <paramref name="tiles"/> under <paramref name="parent"/>, one
        /// renderer per quarter, each with its layer's material and posed as
        /// its tile says (<see cref="PoseQuarter"/>). The one path for block
        /// bodies, highlights and shadows, the frame's, gates', the next block
        /// on a generator's screen, and a grabbed block's outline; only the
        /// arguments differ.
        /// </summary>
        /// <param name="rectOf">Where a tile goes, in <paramref name="parent"/>'s local world units.</param>
        /// <param name="layer">Which generated set the quarters are drawn with.</param>
        private List<SpriteRenderer> AddQuarters(
            Transform parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, QuarterLayer layer, Color color,
            int order)
        {
            var renderers = new List<SpriteRenderer>(tiles.Count);
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var renderer = AddRenderer(
                    parent, tile.ToString(), config.QuarterSprite(layer, tile), Vector2.zero, color, order,
                    tile.IsRotated ? QuarterTile.EdgeAlongYRotationDegrees : 0f);
                renderer.sharedMaterial = config.QuarterMaterial(layer);
                renderer.flipX = tile.FlipX;
                renderer.flipY = tile.FlipY;
                PoseQuarter(renderer, tile, layer, rectOf(tile));
                renderers.Add(renderer);
            }

            return renderers;
        }

        /// <summary>
        /// Places a quarter renderer of <paramref name="layer"/> over
        /// <paramref name="rect"/>, enlarged by the seam overlap while it is
        /// opaque and not at all while it is translucent
        /// (<see cref="QuarterLayers.SeamOverlap"/>), so a light piece or a
        /// fading quarter is never blended twice at a seam. A rotated
        /// sprite's own x runs along the board's y.
        /// </summary>
        private void PoseQuarter(SpriteRenderer renderer, QuarterTile tile, QuarterLayer layer, Rect rect)
        {
            var overlap = CellsToWorld(QuarterLayers.SeamOverlap(layer, renderer.color, config.SeamOverlapCells)) * 2f;
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

        private Vector2 GridToLocal(Vector2 grid) => layout.GridToWorld(grid);

        /// <summary>A rectangle in grid units, in this view's local world units.</summary>
        private Rect GridRectToLocal(Rect grid) =>
            new Rect(GridToLocal(grid.min), grid.size * layout.CellSize);

        /// <summary>A rectangle in cell units relative to a block's origin, in its body's local world units.</summary>
        private Rect CellRectToLocal(Rect cells) =>
            new Rect(cells.min * layout.CellSize, cells.size * layout.CellSize);

        /// <summary>Where a block's root sits for <paramref name="origin"/>: the origin cell's lower-left corner.</summary>
        private Vector2 OriginToLocal(Coord origin) => GridToLocal(new Vector2(origin.X, origin.Y));

        /// <summary>
        /// A grid position in cells in this view's frame, whose origin is the
        /// board's centre: <see cref="GridToLocal"/> before the cell size, by
        /// the same arithmetic. What a recipe drawing straight under the
        /// board's groups is given.
        /// </summary>
        private Vector2 GridToViewCells(Vector2 grid) =>
            new Vector2(grid.x - layout.Width * 0.5f, grid.y - layout.Height * 0.5f);

        /// <summary>A rectangle in grid units, in cells in this view's frame.</summary>
        private Rect GridRectToViewCells(Rect grid) => new Rect(GridToViewCells(grid.min), grid.size);

        /// <summary>The region from <paramref name="min"/> to <paramref name="max"/> inclusive, in cells in this view's frame.</summary>
        private Rect RegionViewCells(Coord min, Coord max) =>
            GridRectToViewCells(new Rect(min.X, min.Y, max.X - min.X + 1, max.Y - min.Y + 1));

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

        /// <summary>A tinted copy of the config's plain square: the floor's backing.</summary>
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

        /// <summary>
        /// A label in <paramref name="font"/> at <paramref name="localPosition"/>:
        /// centred on it, or starting at it — its left edge, at its middle
        /// height — when <paramref name="isFromLeft"/>. Drawn with
        /// <paramref name="material"/> when one is given, made from that font:
        /// the label shadow material, the font's own with its underlay on,
        /// which every shadowed label shares (Module 46, pass 2), or the
        /// outline material of a time-bonus mark's seconds. The font is set
        /// first, as setting it replaces a material made from another font's
        /// atlas.
        /// </summary>
        private void AddLabel(
            Transform parent, string name, Vector2 localPosition, string text, float fontSize, Color color, TMP_FontAsset font,
            Material material, bool isFromLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<TextMeshPro>();
            label.font = font;
            if (material != null)
            {
                label.fontSharedMaterial = material;
            }

            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = isFromLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.pivot = new Vector2(isFromLeft ? 0f : 0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(layout.CellSize, layout.CellSize);
            label.rectTransform.localPosition = localPosition;
            label.sortingOrder = config.LabelOrder;
        }

        /// <summary>Where a block's padlock or key, its layer badge and its time-bonus mark sit, in cells relative to its body, and their scale.</summary>
        private readonly struct BlockMarks
        {
            public BlockMarks(Vector2 icon, Vector2 layers, Vector2 bonus, float scale, float bonusScale)
            {
                Icon = icon;
                Layers = layers;
                Bonus = bonus;
                Scale = scale;
                BonusScale = bonusScale;
            }

            /// <summary>The centre of the padlock or key.</summary>
            public Vector2 Icon { get; }

            /// <summary>The centre of the layer badge (M4).</summary>
            public Vector2 Layers { get; }

            /// <summary>The centre of the time-bonus mark.</summary>
            public Vector2 Bonus { get; }

            /// <summary>1, or a crowded scale when the marks share a block without room for each.</summary>
            public float Scale { get; }

            /// <summary>The time-bonus mark's scale, fitted to its width on the block; 0 on a block without one.</summary>
            public float BonusScale { get; }
        }

        /// <summary>A drawn block: its root, its body, and the parts its effects animate.</summary>
        private sealed class DrawnBlock
        {
            public DrawnBlock(
                Transform root, Transform body, Vector2 footprintCenter,
                IReadOnlyList<Coord> cells, IReadOnlyList<QuarterTile> tiles, MovementAxis axis, bool isIce)
            {
                Root = root;
                Body = body;
                FootprintCenter = footprintCenter;
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

            /// <summary>The lit body's quarter renderers, in the block's colour (darkened when layered), in <see cref="Tiles"/> order.</summary>
            public List<SpriteRenderer> FaceQuarters { get; set; }

            /// <summary>The body's highlight renderers, in <see cref="Tiles"/> order.</summary>
            public List<SpriteRenderer> ShineQuarters { get; set; }

            /// <summary>A layered block's window (M4, Module 46); null when it shows no colour beneath.</summary>
            public LayerParts<SpriteRenderer> Layer { get; set; }

            /// <summary>The footprint, relative to the origin.</summary>
            public IReadOnlyList<Coord> Cells { get; }

            /// <summary>The footprint's quarter tiles, shared by the shadow, the lit body, its highlight and the outline.</summary>
            public IReadOnlyList<QuarterTile> Tiles { get; }

            /// <summary>The block's movement axis: a restricted one shows an arrow instead of studs.</summary>
            public MovementAxis Axis { get; }

            /// <summary>True for a frozen block (M3): its body is in the ice colour, under the ice and its frost.</summary>
            public bool IsIce { get; }

            /// <summary>
            /// The tinted per-cell stud renderers, in the face's colour; empty
            /// for a block without studs: ice, or axis-restricted.
            /// </summary>
            public List<SpriteRenderer> Studs { get; set; }

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

            /// <summary>The mask clipping the block at a gate's inner line (<see cref="ClipAtGate"/>); null when it is not clipped.</summary>
            public Transform GateClip { get; set; }

            /// <summary>The edge whose inner line <see cref="GateClip"/> cuts at.</summary>
            public BoardEdge GateClipEdge { get; set; }

            /// <summary>Where <see cref="GateClip"/> stays on the board, in the view's frame, while the block moves.</summary>
            public Vector2 GateClipCenter { get; set; }

            /// <summary>The labels the clip hid, to show again when it goes.</summary>
            public List<GameObject> GateClipHiddenLabels { get; } = new List<GameObject>();
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
            public void Apply(float opacity) => Apply(opacity, 0f);

            /// <summary>
            /// Sets every sprite's colour <paramref name="whiten"/> of the way
            /// from its original toward white — a joker break's flash (Module
            /// 47) — and every part to <paramref name="opacity"/> times its
            /// original alpha.
            /// </summary>
            public void Apply(float opacity, float whiten)
            {
                for (var i = 0; i < sprites.Length; i++)
                {
                    var color = Color.Lerp(spriteColors[i], Color.white, whiten);
                    color.a = spriteColors[i].a * opacity;
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
