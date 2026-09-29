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
    /// gates as coloured arrowed frame segments, generators, blocks, shutters
    /// and elevators, plus a count label wherever a count matters to the
    /// player. What is shown is decided by <see cref="VisibilityLayer"/>; this
    /// component only turns that into sprites and labels, all sized and
    /// coloured from <see cref="RuntimeConfig"/>. It also shows what happens
    /// between redraws: a dragged block floating at a continuous position and
    /// settling into its cell on release (Module 13), and a cleared block
    /// leaving through its gate.
    /// </summary>
    /// <remarks>
    /// <para>Everything is rebuilt from scratch on every <see cref="Rebuild"/>.
    /// Positions are local to this transform: the board is centred on it.</para>
    /// <para><b>Shapes from quarters (Module 15).</b> Blocks, the frame and open
    /// gates are drawn from the generated quarter sprites, laid out by
    /// <see cref="BlockTiling"/> and <see cref="FrameTiling"/> and tinted at
    /// runtime. Each is drawn twice: a darker copy offset downward (the lip),
    /// then the face. Frozen, locked and key-carrying blocks, shutters,
    /// generators, elevators and closed gates keep their 2.1 placeholder
    /// marks, placed on the new board; 2.4b redraws them.</para>
    /// <para><b>Blocks.</b> Each shown block gets one root at its origin's
    /// corner holding everything it draws: a <c>Lip</c> group, a <c>Face</c>
    /// group (quarters plus studs and gloss, or the axis arrow), beneath
    /// squares, labels and badges. The drag, the settle and the exit effect
    /// move or scale that root alone, so every part follows it.</para>
    /// <para><b>Presenting a move.</b> <see cref="Present"/> holds the new
    /// state back until the dragged block has settled, then plays the clear
    /// effects, and only then redraws from the new state and reports done.
    /// <see cref="IsBusy"/> is true for all of that, so input can never start
    /// a drag from a stale picture.</para>
    /// <para><b>Tweens.</b> DOTween keeps static state, and Enter Play Mode
    /// runs without a domain reload. Every tween this view starts carries it as
    /// its id and is killed by <see cref="Rebuild"/>, <see cref="Clear"/>, and
    /// when the view is disabled or destroyed; nothing it starts outlives it.
    /// </para>
    /// </remarks>
    public sealed class BoardView : MonoBehaviour
    {
        private readonly Dictionary<int, DrawnBlock> drawnBlocks = new Dictionary<int, DrawnBlock>();

        private RuntimeConfig config;
        private LevelContext ctx;
        private BoardLayout layout;
        private VisibilityLayer visibility;
        private BoardState drawnState;

        // Static per level, so tiled once in Initialize.
        private IReadOnlyList<QuarterTile> frameTiles;
        private IReadOnlyList<QuarterTile>[] gateTiles;
        private Func<QuarterTile, Rect> blockQuarterRect;
        private Func<QuarterTile, Rect> frameQuarterRect;

        private int draggedBlock = -1;

        // Null whenever no settle is playing: cleared on completion and on
        // every kill, or IsBusy would hold for good and lock input.
        private Tween settleTween;

        // A presentation holds its state and callback from Present until it
        // finishes; the effects flag says whether its sequence has started.
        private bool isPresenting;
        private bool areEffectsPlaying;
        private BoardState presentedState;
        private IReadOnlyList<ClearedBlock> presentedClears;
        private Action presentationDone;

        /// <summary>
        /// True while a released block is settling or a move is being
        /// presented. Input must not start a drag while it holds.
        /// </summary>
        public bool IsBusy => settleTween != null || isPresenting;

        /// <summary>Binds the view to one level. Call before the first <see cref="Rebuild"/>.</summary>
        public void Initialize(RuntimeConfig config, LevelContext ctx, BoardLayout layout, VisibilityLayer visibility)
        {
            StopAnimations();
            this.config = config;
            this.ctx = ctx;
            this.layout = layout;
            this.visibility = visibility;

            frameTiles = FrameTiling.Compute(ctx);
            gateTiles = new IReadOnlyList<QuarterTile>[ctx.Gates.Count];
            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                gateTiles[g] = BlockTiling.Compute(FrameTiling.GateCells(ctx, g));
            }

            // A block's quarters are local to its root, which sits at the
            // origin's corner; the frame's and gates' are in grid units.
            blockQuarterRect = tile => CellRectToLocal(BlockTiling.QuarterRect(tile));
            frameQuarterRect = tile => GridRectToLocal(
                FrameTiling.QuarterRect(tile, ctx.Width, ctx.Height, layout.FrameThicknessCells));
        }

        /// <summary>
        /// Stops every animation, abandons any presentation in progress without
        /// reporting it done, discards everything drawn and draws
        /// <paramref name="state"/>.
        /// </summary>
        public void Rebuild(BoardState state)
        {
            Clear();
            drawnState = state;

            DrawFloor();
            DrawFrame();
            DrawGates(state);
            DrawGenerators(state);
            DrawBlocks(state);
            DrawShutters(state);
            DrawElevators(state);
        }

        /// <summary>
        /// Starts showing a drag of block <paramref name="blockIndex"/>:
        /// <see cref="ShowDragged"/> and <see cref="Settle"/> after this move
        /// that block.
        /// </summary>
        public void BeginDrag(int blockIndex)
        {
            KillSettle();
            draggedBlock = blockIndex;
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
        /// <paramref name="origin"/>. A block already there — a push in place,
        /// or a release exactly on a cell — is placed at once with no tween, so
        /// a clear effect waiting on the settle starts without delay.
        /// <see cref="IsBusy"/> holds while the tween plays.
        /// </summary>
        public void Settle(Coord origin)
        {
            KillSettle();
            if (!drawnBlocks.TryGetValue(draggedBlock, out var block))
            {
                return;
            }

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
        /// <paramref name="origin"/> at once, stopping any settle — for a drag
        /// that is cancelled or whose move was rejected. Does nothing for a
        /// block that is not drawn.
        /// </summary>
        public void Snap(int blockIndex, Coord origin)
        {
            KillSettle();

            if (drawnBlocks.TryGetValue(blockIndex, out var block))
            {
                block.Root.localPosition = OriginToLocal(origin);
            }
        }

        /// <summary>
        /// Shows the result of a move: waits for the dragged block to settle,
        /// plays the effect of every block in
        /// <paramref name="clears"/>, then redraws from <paramref name="state"/>
        /// and calls <paramref name="onDone"/>. <see cref="IsBusy"/> holds
        /// throughout. A <see cref="Rebuild"/> before then abandons it, and
        /// <paramref name="onDone"/> is never called.
        /// </summary>
        public void Present(BoardState state, IReadOnlyList<ClearedBlock> clears, Action onDone)
        {
            isPresenting = true;
            areEffectsPlaying = false;
            presentedState = state ?? throw new ArgumentNullException(nameof(state));
            presentedClears = clears ?? throw new ArgumentNullException(nameof(clears));
            presentationDone = onDone;

            if (settleTween == null)
            {
                PlayEffects();
            }
        }

        /// <summary>Stops every animation and destroys everything drawn.</summary>
        public void Clear()
        {
            StopAnimations();
            drawnBlocks.Clear();
            drawnState = null;
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private void OnEnable()
        {
            // Disabling killed the tweens mid-way. A presentation that was
            // interrupted finishes now; otherwise the half-animated picture is
            // replaced by the state it was drawn from.
            if (isPresenting)
            {
                FinishPresentation();
            }
            else if (drawnState != null)
            {
                Rebuild(drawnState);
            }
        }

        private void OnDisable()
        {
            // The presentation, if any, is kept so OnEnable can finish it.
            DOTween.Kill(this);
            settleTween = null;
            areEffectsPlaying = false;
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
            settleTween = null;
        }

        private void StopAnimations()
        {
            DOTween.Kill(this);
            settleTween = null;
            draggedBlock = -1;
            isPresenting = false;
            areEffectsPlaying = false;
            presentedState = null;
            presentedClears = null;
            presentationDone = null;
        }

        /// <summary>
        /// Stops a settle where it is. A presentation waiting on it starts its
        /// effects rather than waiting for a completion that will never come.
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
        /// presentation waiting for it starts its effects.
        /// </summary>
        private void OnSettled()
        {
            settleTween = null;
            if (isPresenting && !areEffectsPlaying)
            {
                PlayEffects();
            }
        }

        private void PlayEffects()
        {
            areEffectsPlaying = true;
            Sequence effects = null;

            for (var i = 0; i < presentedClears.Count; i++)
            {
                var clear = presentedClears[i];
                if (!drawnBlocks.TryGetValue(clear.BlockIndex, out var block))
                {
                    continue;
                }

                effects = effects ?? DOTween.Sequence().SetId(this);
                effects.Insert(0f, clear.IsDestroyed ? ExitEffect(block, clear.GateEdge) : PeelEffect(block, clear.ExposedColor.Value));
            }

            if (effects == null)
            {
                FinishPresentation();
                return;
            }

            effects.OnComplete(FinishPresentation);
        }

        private void FinishPresentation()
        {
            var state = presentedState;
            var done = presentationDone;

            Rebuild(state);
            done?.Invoke();
        }

        /// <summary>
        /// A destroyed block shrinks around its footprint's centre and fades
        /// while travelling toward, and through, the gate it was cleared at.
        /// The fade reaches every renderer and label under the block's root:
        /// lip, face, studs, gloss, arrow, marks.
        /// </summary>
        private Tween ExitEffect(DrawnBlock block, BoardEdge gateEdge)
        {
            var root = block.Root;
            Vector2 start = root.localPosition;
            var center = block.FootprintCenter;
            var travel = EdgeOutward(gateEdge) * CellsToWorld(config.ClearTravelCells);
            var fade = new Fade(root);

            // Scaling the root about its own position moves the footprint's
            // centre from start + center to start + scale * center; shifting the
            // root by center * t (t = 1 - scale) keeps the centre fixed, and the
            // travel carries it toward the gate.
            return DOVirtual.Float(0f, 1f, config.ClearSeconds, t =>
                {
                    var scale = 1f - t;
                    root.localScale = new Vector3(scale, scale, 1f);
                    root.localPosition = start + center * t + travel * t;
                    fade.Apply(1f - t);
                })
                .SetEase(config.ClearEase);
        }

        /// <summary>
        /// A surviving layered block peels its removed outer colour: a new face
        /// in the exposed colour is drawn beneath, the lip takes the exposed
        /// colour at once, and the old face — lifted above every part of the new
        /// one by a sorting group — shrinks about the footprint's centre and
        /// fades away.
        /// </summary>
        private Tween PeelEffect(DrawnBlock block, BlockColor exposed)
        {
            for (var i = 0; i < block.BeneathSquares.Count; i++)
            {
                block.BeneathSquares[i].gameObject.SetActive(false);
            }

            var exposedFill = config.BlockFill(exposed);
            var lipFill = config.LipFill(exposedFill);
            foreach (var lip in block.Lip.GetComponentsInChildren<SpriteRenderer>())
            {
                lip.color = lipFill;
            }

            var peeling = block.Face;
            var group = peeling.gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = config.PeelOrder;
            block.Face = DrawFace(block, exposedFill);

            var center = block.FootprintCenter;
            var fade = new Fade(peeling);

            // The face group sits at the root's origin; the same shift as the
            // exit effect keeps the footprint's centre fixed while it shrinks.
            return DOVirtual.Float(0f, 1f, config.PeelSeconds, t =>
                {
                    var scale = 1f - t;
                    peeling.localScale = new Vector3(scale, scale, 1f);
                    peeling.localPosition = center * t;
                    fade.Apply(1f - t);
                })
                .SetEase(config.PeelEase);
        }

        private static Vector2 EdgeOutward(BoardEdge edge)
        {
            switch (edge)
            {
                case BoardEdge.Top:
                    return Vector2.up;
                case BoardEdge.Bottom:
                    return Vector2.down;
                case BoardEdge.Left:
                    return Vector2.left;
                default:
                    return Vector2.right;
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
                transform, "Floor underlay",
                GridToLocal(new Vector2(ctx.Width * 0.5f, ctx.Height * 0.5f)),
                new Vector2(CellsToWorld(ctx.Width + 2f * underlay), CellsToWorld(ctx.Height + 2f * underlay)),
                config.FloorColor, config.FloorUnderlayOrder);

            var floor = AddGroup(transform, "Floor", Vector2.zero);
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
            var lip = AddGroup(transform, "Frame lip", LipOffset());
            AddQuarters(lip, frameTiles, frameQuarterRect, config.LipFill(config.FrameColor), config.FrameLipOrder);

            var face = AddGroup(transform, "Frame", Vector2.zero);
            AddQuarters(face, frameTiles, frameQuarterRect, config.FrameColor, config.FrameOrder);
        }

        /// <summary>
        /// An open gate is its own segment of the frame, in the gate's colour,
        /// with a lip, a face and a white arrow pointing out of the board. A
        /// closed gate (M2) keeps its colourless bar and count, filling the
        /// hole the frame leaves for it; 2.4b gives it ice.
        /// </summary>
        private void DrawGates(BoardState state)
        {
            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                var gate = ctx.Gates[g];
                var visual = visibility.Gate(state, g);
                var span = GridRectToLocal(FrameTiling.EdgeSpanRect(
                    ctx.Width, ctx.Height, gate.Edge, gate.Offset, gate.Width, layout.FrameThicknessCells));

                if (!visual.IsOpen)
                {
                    AddSquare(transform, $"Gate {gate.Id}", span.center, span.size, config.ClosedGateColor, config.EdgeFeatureOrder);
                    AddLabel(transform, $"Gate {gate.Id} count", span.center, visual.OpensInClears, config.BadgeLabelFontSize, config.LabelColor);
                    continue;
                }

                var fill = config.BlockFill(visual.Color.Value);
                var root = AddGroup(transform, $"Gate {gate.Id}", Vector2.zero);
                var lip = AddGroup(root, "Lip", LipOffset());
                AddQuarters(lip, gateTiles[g], frameQuarterRect, config.LipFill(fill), config.FrameLipOrder);
                AddQuarters(root, gateTiles[g], frameQuarterRect, fill, config.FrameOrder);

                var arrow = CellsToWorld(config.GateArrowSizeCells);
                AddSprite(
                    root, "Arrow", config.GateArrowSprite, span.center, new Vector2(arrow, arrow),
                    config.GateArrowColor, config.GateArrowOrder,
                    Vector2.SignedAngle(Vector2.up, EdgeOutward(gate.Edge)));
            }
        }

        /// <summary>
        /// A generator keeps its placeholder bar and queue count, drawn on the
        /// frame at the frame's thickness; the frame runs on beneath it.
        /// </summary>
        private void DrawGenerators(BoardState state)
        {
            for (var g = 0; g < ctx.Generators.Count; g++)
            {
                var queued = visibility.GeneratorQueued(state, g);
                if (queued <= 0)
                {
                    // M6: an exhausted generator is destroyed.
                    continue;
                }

                var generator = ctx.Generators[g];
                var span = GridRectToLocal(FrameTiling.EdgeSpanRect(
                    ctx.Width, ctx.Height, generator.Edge, generator.Offset, generator.Width, layout.FrameThicknessCells));
                AddSquare(transform, $"Generator {generator.Id}", span.center, span.size, config.GeneratorColor, config.EdgeFeatureOrder);
                AddLabel(transform, $"Generator {generator.Id} count", span.center, queued, config.BadgeLabelFontSize, config.LabelColor);
            }
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

                var root = new GameObject($"Block {i}").transform;
                root.SetParent(transform, false);
                root.localPosition = OriginToLocal(state.Origins[i]);

                var spec = ctx.SpecAt(i);
                var cells = spec.Cells;
                var tiles = BlockTiling.Compute(cells);

                // M3: a frozen block shows its shape in the frozen tint, never
                // its colour; studs or its axis arrow still show.
                var fill = visual.IsFrozen ? config.FrozenTint : config.BlockFill(visual.OuterColor.Value);

                var lip = AddGroup(root, "Lip", LipOffset());
                AddQuarters(lip, tiles, blockQuarterRect, config.LipFill(fill), config.BlockLipOrder);

                var drawn = new DrawnBlock(root, FootprintCenterInBlock(cells), lip, cells, tiles, spec.Axis);
                drawn.Face = DrawFace(drawn, fill);
                drawnBlocks[i] = drawn;

                if (visual.BeneathColor.HasValue)
                {
                    var beneath = config.BlockFill(visual.BeneathColor.Value);
                    for (var c = 0; c < cells.Count; c++)
                    {
                        drawn.BeneathSquares.Add(AddSquare(
                            root, $"Beneath {cells[c]}", CellCenterInBlock(cells[c]), new Vector2(beneathSide, beneathSide),
                            beneath, config.BeneathColorOrder));
                    }
                }

                // Labels and badges sit on the first cell of the footprint.
                DrawBlockMarks(root, cells[0], visual);
            }
        }

        /// <summary>
        /// A block's face under its root: the quarter tiles, then either studs
        /// with their gloss on every cell or, for an axis-restricted block
        /// (M7), one double-headed arrow along its axis instead.
        /// </summary>
        private Transform DrawFace(DrawnBlock block, Color fill)
        {
            var face = AddGroup(block.Root, "Face", Vector2.zero);
            AddQuarters(face, block.Tiles, blockQuarterRect, fill, config.BlockOrder);

            if (block.Axis != MovementAxis.Free)
            {
                DrawAxisArrow(face, block);
                return face;
            }

            var cellSize = new Vector2(layout.CellSize, layout.CellSize);
            for (var c = 0; c < block.Cells.Count; c++)
            {
                var cell = block.Cells[c];
                var center = CellCenterInBlock(cell);
                AddSprite(face, $"Studs {cell}", config.StudsSprite, center, cellSize, fill, config.StudOrder);
                AddSprite(face, $"Gloss {cell}", config.StudGlossSprite, center, cellSize, config.GlossColor, config.GlossOrder);
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
            FootprintBounds(block.Cells, out var minX, out var maxX, out var minY, out var maxY);
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
            renderer.sortingOrder = config.AxisArrowOrder;
        }

        private void DrawBlockMarks(Transform root, Coord labelCell, BlockVisual visual)
        {
            var center = CellCenterInBlock(labelCell);
            if (visual.IsFrozen)
            {
                AddLabel(root, "Frozen count", center, visual.FrozenRemaining, config.LabelFontSize, config.LabelColor);
            }
            else if (visual.LayerNumeral.HasValue)
            {
                AddLabel(root, "Layer count", center, visual.LayerNumeral.Value, config.LabelFontSize, config.LabelColor);
            }

            // Badges hug the top corners of the label cell: lock left, key right.
            var inset = 0.5f - config.BadgeInsetCells - config.BadgeSize * 0.5f;
            var badge = new Vector2(CellsToWorld(config.BadgeSize), CellsToWorld(config.BadgeSize));

            if (visual.LockId.HasValue)
            {
                config.TryGetBadgeColor(visual.LockId.Value, out var lockColor);
                var lockCenter = center + new Vector2(-CellsToWorld(inset), CellsToWorld(inset));
                AddSquare(root, "Lock badge", lockCenter, badge, lockColor, config.BadgeOrder);
                AddLabel(root, "Lock count", lockCenter, visual.KeysStillRequired, config.BadgeLabelFontSize, config.LabelColor);
            }

            if (visual.KeyTargetLockId.HasValue)
            {
                config.TryGetBadgeColor(visual.KeyTargetLockId.Value, out var keyColor);
                var keyCenter = center + new Vector2(CellsToWorld(inset), CellsToWorld(inset));
                AddSquare(root, "Key badge", keyCenter, badge, keyColor, config.BadgeOrder);
            }
        }

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
                var center = RegionCenter(shutter.Min, shutter.Max);
                AddSquare(transform, $"Shutter {shutter.Id}", center, RegionSize(shutter.Min, shutter.Max), config.ShutterColor, config.ShutterOrder);

                var labelColor = visual.CountsColor.HasValue ? config.BlockFill(visual.CountsColor.Value) : config.LightLabelColor;
                AddLabel(transform, $"Shutter {shutter.Id} count", center, visual.OpensInClears, config.LabelFontSize, labelColor);
            }
        }

        private void DrawElevators(BoardState state)
        {
            var thickness = CellsToWorld(config.ElevatorOutlineThickness);

            for (var e = 0; e < ctx.Elevators.Count; e++)
            {
                var wavesToCome = visibility.ElevatorWavesToCome(state, e);
                if (wavesToCome <= 0 && !state.ElevatorWaveActive[e])
                {
                    // M9: after its final wave is cleared, the elevator is destroyed.
                    continue;
                }

                var elevator = ctx.Elevators[e];
                var center = RegionCenter(elevator.Min, elevator.Max);
                var size = RegionSize(elevator.Min, elevator.Max);
                var halfX = (size.x - thickness) * 0.5f;
                var halfY = (size.y - thickness) * 0.5f;
                var elevatorName = $"Elevator {elevator.Id}";

                AddSquare(transform, elevatorName + " top", center + new Vector2(0f, halfY), new Vector2(size.x, thickness), config.ElevatorOutlineColor, config.ElevatorOrder);
                AddSquare(transform, elevatorName + " bottom", center - new Vector2(0f, halfY), new Vector2(size.x, thickness), config.ElevatorOutlineColor, config.ElevatorOrder);
                AddSquare(transform, elevatorName + " left", center - new Vector2(halfX, 0f), new Vector2(thickness, size.y), config.ElevatorOutlineColor, config.ElevatorOrder);
                AddSquare(transform, elevatorName + " right", center + new Vector2(halfX, 0f), new Vector2(thickness, size.y), config.ElevatorOutlineColor, config.ElevatorOrder);

                if (wavesToCome > 0)
                {
                    var topLeft = layout.CellCenter(new Coord(elevator.Min.X, elevator.Max.Y));
                    AddLabel(transform, elevatorName + " count", topLeft, wavesToCome, config.BadgeLabelFontSize, config.LightLabelColor);
                }
            }
        }

        /// <summary>
        /// Draws <paramref name="tiles"/> under <paramref name="parent"/>, one
        /// renderer per quarter, each posed as its tile says and enlarged by
        /// the seam overlap. The one path for block faces and lips, the frame
        /// and its lip, and open gates and their lips; only the arguments
        /// differ.
        /// </summary>
        /// <param name="rectOf">Where a tile goes, in <paramref name="parent"/>'s local world units.</param>
        private void AddQuarters(
            Transform parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, Color color, int order)
        {
            var overlap = CellsToWorld(config.SeamOverlapCells) * 2f;
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var rect = rectOf(tile);
                var size = rect.size + new Vector2(overlap, overlap);

                // A rotated sprite's own x runs along the board's y.
                var spriteFrameSize = tile.IsRotated ? new Vector2(size.y, size.x) : size;
                var renderer = AddSprite(
                    parent, tile.ToString(), config.QuarterSprite(tile.SpriteKind), rect.center, spriteFrameSize,
                    color, order, tile.IsRotated ? QuarterTile.EdgeAlongYRotationDegrees : 0f);
                renderer.flipX = tile.FlipX;
                renderer.flipY = tile.FlipY;
            }
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

        /// <summary>A rectangle in cell units relative to a block's origin, in its root's local world units.</summary>
        private Rect CellRectToLocal(Rect cells) =>
            new Rect(cells.min * layout.CellSize, cells.size * layout.CellSize);

        /// <summary>Where a block's root sits for <paramref name="origin"/>: the origin cell's lower-left corner.</summary>
        private Vector2 OriginToLocal(Coord origin) => GridToLocal(new Vector2(origin.X, origin.Y));

        /// <summary>The centre of a footprint's bounding box relative to its block's root.</summary>
        private Vector2 FootprintCenterInBlock(IReadOnlyList<Coord> cells)
        {
            FootprintBounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            return new Vector2(CellsToWorld((minX + maxX + 1) * 0.5f), CellsToWorld((minY + maxY + 1) * 0.5f));
        }

        /// <summary>The lowest and highest cell coordinates of a footprint on each axis.</summary>
        private static void FootprintBounds(IReadOnlyList<Coord> cells, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;
            for (var i = 0; i < cells.Count; i++)
            {
                minX = Math.Min(minX, cells[i].X);
                maxX = Math.Max(maxX, cells[i].X);
                minY = Math.Min(minY, cells[i].Y);
                maxY = Math.Max(maxY, cells[i].Y);
            }
        }

        private float CellsToWorld(float cells) => cells * layout.CellSize;

        /// <summary>A footprint cell's centre relative to its block's root, which sits at the origin's corner.</summary>
        private Vector2 CellCenterInBlock(Coord cell) =>
            new Vector2(CellsToWorld(cell.X + 0.5f), CellsToWorld(cell.Y + 0.5f));

        private Vector2 RegionCenter(Coord min, Coord max) =>
            GridToLocal(new Vector2((min.X + max.X + 1) * 0.5f, (min.Y + max.Y + 1) * 0.5f));

        private Vector2 RegionSize(Coord min, Coord max) =>
            new Vector2(CellsToWorld(max.X - min.X + 1), CellsToWorld(max.Y - min.Y + 1));

        /// <summary>A tinted copy of the config's plain square, for the placeholder shapes.</summary>
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
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = config.SpriteMaterial;
            renderer.color = color;
            renderer.sortingOrder = order;

            var bounds = sprite.bounds.size;
            go.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
            return renderer;
        }

        private void AddLabel(Transform parent, string name, Vector2 localCenter, int value, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;

            var label = go.AddComponent<TextMeshPro>();
            if (config.LabelFont != null)
            {
                label.font = config.LabelFont;
            }

            label.text = value.ToString(CultureInfo.InvariantCulture);
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = new Vector2(layout.CellSize, layout.CellSize);
            label.sortingOrder = config.LabelOrder;
        }

        /// <summary>A drawn block: its root, and the parts its effects animate.</summary>
        private sealed class DrawnBlock
        {
            public DrawnBlock(
                Transform root, Vector2 footprintCenter, Transform lip,
                IReadOnlyList<Coord> cells, IReadOnlyList<QuarterTile> tiles, MovementAxis axis)
            {
                Root = root;
                FootprintCenter = footprintCenter;
                Lip = lip;
                Cells = cells;
                Tiles = tiles;
                Axis = axis;
            }

            /// <summary>Sits at the origin's corner; moving it moves the whole block.</summary>
            public Transform Root { get; }

            /// <summary>The centre of the footprint's bounding box, relative to <see cref="Root"/>.</summary>
            public Vector2 FootprintCenter { get; }

            /// <summary>The lip's quarters, offset below the face.</summary>
            public Transform Lip { get; }

            /// <summary>The face group: quarters plus studs and gloss, or the axis arrow. A peel draws a new one.</summary>
            public Transform Face { get; set; }

            /// <summary>The footprint, relative to the origin.</summary>
            public IReadOnlyList<Coord> Cells { get; }

            /// <summary>The footprint's quarter tiles, shared by lip and face.</summary>
            public IReadOnlyList<QuarterTile> Tiles { get; }

            /// <summary>The block's movement axis: a restricted one shows an arrow instead of studs.</summary>
            public MovementAxis Axis { get; }

            /// <summary>The beneath-colour squares of a layered block (M4); empty otherwise.</summary>
            public List<SpriteRenderer> BeneathSquares { get; } = new List<SpriteRenderer>();
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
