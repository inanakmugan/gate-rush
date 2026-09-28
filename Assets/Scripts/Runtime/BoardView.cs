using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using GateRush.Core;
using TMPro;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Draws one board state with placeholder visuals: grid, walls, gates,
    /// generators, blocks, shutters and elevators, plus a count label wherever a
    /// count matters to the player. What is shown is decided by
    /// <see cref="VisibilityLayer"/>; this component only turns that into
    /// sprites and labels, all sized and coloured from <see cref="RuntimeConfig"/>.
    /// It also animates the two things that happen between redraws: a dragged
    /// block walking cell by cell (<see cref="StepPlayback"/>), and a cleared
    /// block leaving through its gate.
    /// </summary>
    /// <remarks>
    /// <para>Everything is rebuilt from scratch on every <see cref="Rebuild"/>.
    /// Positions are local to this transform: the board is centred on it. Each
    /// shown block gets one root placed at its origin's corner with one sprite
    /// per cell beneath it, so a step moves a block by moving that root alone.
    /// </para>
    /// <para><b>Presenting a move.</b> <see cref="Present"/> holds the new
    /// state back until the dragged block's queued steps have played, then
    /// plays the clear effects, and only then redraws from the new state and
    /// reports done. <see cref="IsBusy"/> is true for all of that, so input can
    /// never start a drag from a stale picture.</para>
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

        private StepPlayback playback;
        private int steppingBlock = -1;
        private Tween stepTween;

        // A presentation holds its state and callback from Present until it
        // finishes; the effects flag says whether its sequence has started.
        private bool isPresenting;
        private bool areEffectsPlaying;
        private BoardState presentedState;
        private IReadOnlyList<ClearedBlock> presentedClears;
        private Action presentationDone;

        /// <summary>
        /// True while queued steps are still playing or a move is being
        /// presented. Input must not start a drag while it holds.
        /// </summary>
        public bool IsBusy => stepTween != null || isPresenting;

        /// <summary>Binds the view to one level. Call before the first <see cref="Rebuild"/>.</summary>
        public void Initialize(RuntimeConfig config, LevelContext ctx, BoardLayout layout, VisibilityLayer visibility)
        {
            StopAnimations();
            this.config = config;
            this.ctx = ctx;
            this.layout = layout;
            this.visibility = visibility;
            playback = new StepPlayback(config.StepSeconds, config.MaxLagSteps);
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

            DrawCells();
            DrawGates(state);
            DrawGenerators(state);
            DrawBlocks(state);
            DrawShutters(state);
            DrawElevators(state);
        }

        /// <summary>
        /// Starts playing the steps of a drag of block
        /// <paramref name="blockIndex"/>: each <see cref="EnqueueStep"/> after
        /// this moves that block.
        /// </summary>
        public void BeginDrag(int blockIndex)
        {
            KillStepTween();
            steppingBlock = blockIndex;
        }

        /// <summary>
        /// Queues one single-cell step of the dragged block, to be shown after
        /// every step queued before it — fed from <see cref="DragController.Stepped"/>.
        /// </summary>
        public void EnqueueStep(Coord origin)
        {
            if (steppingBlock < 0)
            {
                return;
            }

            playback.Enqueue(origin);
            if (stepTween == null)
            {
                PlayNextStep();
            }
        }

        /// <summary>
        /// Shows block <paramref name="blockIndex"/> at
        /// <paramref name="origin"/> at once, dropping any steps still queued —
        /// for a drag that is cancelled or whose move was rejected. Does nothing
        /// for a block that is not drawn.
        /// </summary>
        public void Snap(int blockIndex, Coord origin)
        {
            KillStepTween();

            if (drawnBlocks.TryGetValue(blockIndex, out var block))
            {
                block.Root.localPosition = OriginToLocal(origin);
            }
        }

        /// <summary>
        /// Shows the result of a move: waits for the dragged block's queued
        /// steps to finish, plays the effect of every block in
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

            if (stepTween == null)
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
            stepTween = null;
            playback?.Clear();
            areEffectsPlaying = false;
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        private void StopAnimations()
        {
            DOTween.Kill(this);
            stepTween = null;
            playback?.Clear();
            steppingBlock = -1;
            isPresenting = false;
            areEffectsPlaying = false;
            presentedState = null;
            presentedClears = null;
            presentationDone = null;
        }

        private void KillStepTween()
        {
            stepTween?.Kill();
            stepTween = null;
            playback?.Clear();
        }

        /// <summary>
        /// Tweens the dragged block one cell to the next queued origin and
        /// chains itself on completion. When the queue is empty the playback
        /// rests, and a presentation waiting for it starts its effects.
        /// </summary>
        private void PlayNextStep()
        {
            if (!drawnBlocks.TryGetValue(steppingBlock, out var block)
                || !playback.TryDequeue(out var origin, out var seconds))
            {
                stepTween = null;
                playback.Clear();
                if (isPresenting && !areEffectsPlaying)
                {
                    PlayEffects();
                }

                return;
            }

            stepTween = block.Root
                .DOLocalMove(OriginToLocal(origin), seconds)
                .SetEase(config.StepEase)
                .SetId(this)
                .OnComplete(PlayNextStep);
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
        /// A surviving layered block peels its removed outer colour: each outer
        /// cell shrinks about its own centre and fades, uncovering a full cell
        /// of the exposed colour beneath it.
        /// </summary>
        private Tween PeelEffect(DrawnBlock block, BlockColor exposed)
        {
            for (var i = 0; i < block.BeneathSquares.Count; i++)
            {
                block.BeneathSquares[i].gameObject.SetActive(false);
            }

            var fills = block.Fills;
            var scales = new Vector3[fills.Count];
            var colors = new Color[fills.Count];
            for (var i = 0; i < fills.Count; i++)
            {
                var fill = fills[i];
                var under = Instantiate(fill, fill.transform.parent);
                under.color = config.BlockFill(exposed);
                under.sortingOrder = config.BlockOrder;
                fill.sortingOrder = config.PeelOrder;
                scales[i] = fill.transform.localScale;
                colors[i] = fill.color;
            }

            return DOVirtual.Float(0f, 1f, config.PeelSeconds, t =>
                {
                    for (var i = 0; i < fills.Count; i++)
                    {
                        fills[i].transform.localScale = scales[i] * (1f - t);
                        var color = colors[i];
                        color.a *= 1f - t;
                        fills[i].color = color;
                    }
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

        private void DrawCells()
        {
            var side = CellsToWorld(1f - config.CellGap);
            for (var y = 0; y < ctx.Height; y++)
            {
                for (var x = 0; x < ctx.Width; x++)
                {
                    var cell = new Coord(x, y);
                    var fill = ctx.IsStaticWall(cell) ? config.WallColor : config.EmptyCellColor;
                    AddSprite(transform, $"Cell {cell}", layout.CellCenter(cell), new Vector2(side, side), fill, config.CellOrder);
                }
            }
        }

        private void DrawGates(BoardState state)
        {
            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                var gate = ctx.Gates[g];
                var visual = visibility.Gate(state, g);
                var fill = visual.IsOpen ? config.BlockFill(visual.Color.Value) : config.ClosedGateColor;
                var center = DrawEdgeBar($"Gate {gate.Id}", gate.Edge, gate.Offset, gate.Width, fill);

                if (!visual.IsOpen)
                {
                    AddLabel(transform, $"Gate {gate.Id} count", center, visual.OpensInClears, config.BadgeLabelFontSize, config.LabelColor);
                }
            }
        }

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
                var center = DrawEdgeBar($"Generator {generator.Id}", generator.Edge, generator.Offset, generator.Width, config.GeneratorColor);
                AddLabel(transform, $"Generator {generator.Id} count", center, queued, config.BadgeLabelFontSize, config.LabelColor);
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

                var fill = visual.IsFrozen ? config.FrozenTint : config.BlockFill(visual.OuterColor.Value);
                var cells = ctx.SpecAt(i).Cells;
                var rects = BlockCellRects.Compute(cells, config.CellGap);
                var drawn = new DrawnBlock(root, FootprintCenterInBlock(cells));
                drawnBlocks[i] = drawn;

                for (var c = 0; c < cells.Count; c++)
                {
                    drawn.Fills.Add(AddSprite(
                        root, $"Cell {cells[c]}", rects[c].center * layout.CellSize, rects[c].size * layout.CellSize,
                        fill, config.BlockOrder));

                    var center = CellCenterInBlock(cells[c]);

                    if (visual.BeneathColor.HasValue)
                    {
                        drawn.BeneathSquares.Add(AddSprite(
                            root, $"Beneath {cells[c]}", center, new Vector2(beneathSide, beneathSide),
                            config.BlockFill(visual.BeneathColor.Value), config.BeneathColorOrder));
                    }
                }

                // Labels and badges sit on the first cell of the footprint.
                DrawBlockMarks(root, cells[0], visual);
            }
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
            var inset = (1f - config.CellGap - config.BadgeSize) * 0.5f;
            var badge = new Vector2(CellsToWorld(config.BadgeSize), CellsToWorld(config.BadgeSize));

            if (visual.LockId.HasValue)
            {
                config.TryGetBadgeColor(visual.LockId.Value, out var lockColor);
                var lockCenter = center + new Vector2(-CellsToWorld(inset), CellsToWorld(inset));
                AddSprite(root, "Lock badge", lockCenter, badge, lockColor, config.BadgeOrder);
                AddLabel(root, "Lock count", lockCenter, visual.KeysStillRequired, config.BadgeLabelFontSize, config.LabelColor);
            }

            if (visual.KeyTargetLockId.HasValue)
            {
                config.TryGetBadgeColor(visual.KeyTargetLockId.Value, out var keyColor);
                var keyCenter = center + new Vector2(CellsToWorld(inset), CellsToWorld(inset));
                AddSprite(root, "Key badge", keyCenter, badge, keyColor, config.BadgeOrder);
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
                AddSprite(transform, $"Shutter {shutter.Id}", center, RegionSize(shutter.Min, shutter.Max), config.ShutterColor, config.ShutterOrder);

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

                AddSprite(transform, elevatorName + " top", center + new Vector2(0f, halfY), new Vector2(size.x, thickness), config.ElevatorOutlineColor, config.ElevatorOrder);
                AddSprite(transform, elevatorName + " bottom", center - new Vector2(0f, halfY), new Vector2(size.x, thickness), config.ElevatorOutlineColor, config.ElevatorOrder);
                AddSprite(transform, elevatorName + " left", center - new Vector2(halfX, 0f), new Vector2(thickness, size.y), config.ElevatorOutlineColor, config.ElevatorOrder);
                AddSprite(transform, elevatorName + " right", center + new Vector2(halfX, 0f), new Vector2(thickness, size.y), config.ElevatorOutlineColor, config.ElevatorOrder);

                if (wavesToCome > 0)
                {
                    var topLeft = layout.CellCenter(new Coord(elevator.Min.X, elevator.Max.Y));
                    AddLabel(transform, elevatorName + " count", topLeft, wavesToCome, config.BadgeLabelFontSize, config.LightLabelColor);
                }
            }
        }

        /// <summary>
        /// Draws a bar just outside the board along <paramref name="edge"/>,
        /// covering <paramref name="width"/> cells from <paramref name="offset"/>
        /// (measured along the edge), and returns its local centre. The one
        /// shape for both gates and generators: edge features never overlap
        /// (M6), so their bars never do either.
        /// </summary>
        private Vector2 DrawEdgeBar(string name, BoardEdge edge, int offset, int width, Color fill)
        {
            var t = config.EdgeBarThickness;
            var along = offset + width * 0.5f;
            Vector2 centerGrid;
            Vector2 sizeCells;

            switch (edge)
            {
                case BoardEdge.Bottom:
                    centerGrid = new Vector2(along, -t * 0.5f);
                    sizeCells = new Vector2(width, t);
                    break;
                case BoardEdge.Top:
                    centerGrid = new Vector2(along, ctx.Height + t * 0.5f);
                    sizeCells = new Vector2(width, t);
                    break;
                case BoardEdge.Left:
                    centerGrid = new Vector2(-t * 0.5f, along);
                    sizeCells = new Vector2(t, width);
                    break;
                default:
                    centerGrid = new Vector2(ctx.Width + t * 0.5f, along);
                    sizeCells = new Vector2(t, width);
                    break;
            }

            var center = GridToLocal(centerGrid);
            AddSprite(transform, name, center, sizeCells * layout.CellSize, fill, config.EdgeFeatureOrder);
            return center;
        }

        private Vector2 GridToLocal(Vector2 grid) => layout.GridToWorld(grid);

        /// <summary>Where a block's root sits for <paramref name="origin"/>: the origin cell's lower-left corner.</summary>
        private Vector2 OriginToLocal(Coord origin) => GridToLocal(new Vector2(origin.X, origin.Y));

        /// <summary>The centre of a footprint's bounding box relative to its block's root.</summary>
        private Vector2 FootprintCenterInBlock(IReadOnlyList<Coord> cells)
        {
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            for (var i = 0; i < cells.Count; i++)
            {
                minX = Math.Min(minX, cells[i].X);
                maxX = Math.Max(maxX, cells[i].X);
                minY = Math.Min(minY, cells[i].Y);
                maxY = Math.Max(maxY, cells[i].Y);
            }

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

        /// <summary>
        /// A tinted copy of the config's square sprite, scaled to
        /// <paramref name="size"/> world units whatever the sprite's own pixels
        /// per unit.
        /// </summary>
        private SpriteRenderer AddSprite(Transform parent, string name, Vector2 localCenter, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;

            var sprite = config.CellSprite;
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

        /// <summary>A drawn block: its root, and the parts its clear effect animates.</summary>
        private sealed class DrawnBlock
        {
            public DrawnBlock(Transform root, Vector2 footprintCenter)
            {
                Root = root;
                FootprintCenter = footprintCenter;
            }

            /// <summary>Sits at the origin's corner; moving it moves the whole block.</summary>
            public Transform Root { get; }

            /// <summary>The centre of the footprint's bounding box, relative to <see cref="Root"/>.</summary>
            public Vector2 FootprintCenter { get; }

            /// <summary>One sprite per footprint cell, in the outer colour (or the frozen tint).</summary>
            public List<SpriteRenderer> Fills { get; } = new List<SpriteRenderer>();

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
