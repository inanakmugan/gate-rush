using System.Collections.Generic;
using System.Globalization;
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
    /// </summary>
    /// <remarks>
    /// <para>Everything is rebuilt from scratch on every <see cref="Rebuild"/> —
    /// acceptable for phase 2.1, where a state changes at most once per
    /// release.</para>
    /// <para>Positions are local to this transform: the board is centred on it.
    /// Each shown block gets one root placed at its origin's corner with one
    /// sprite per cell beneath it, so the drag preview moves a block by moving
    /// that root alone (<see cref="ShowDragOrigin"/>).</para>
    /// </remarks>
    public sealed class BoardView : MonoBehaviour
    {
        private readonly Dictionary<int, Transform> blockRoots = new Dictionary<int, Transform>();

        private RuntimeConfig config;
        private LevelContext ctx;
        private BoardLayout layout;
        private VisibilityLayer visibility;

        /// <summary>Binds the view to one level. Call before the first <see cref="Rebuild"/>.</summary>
        public void Initialize(RuntimeConfig config, LevelContext ctx, BoardLayout layout, VisibilityLayer visibility)
        {
            this.config = config;
            this.ctx = ctx;
            this.layout = layout;
            this.visibility = visibility;
        }

        /// <summary>Discards everything drawn and draws <paramref name="state"/>.</summary>
        public void Rebuild(BoardState state)
        {
            Clear();

            DrawCells();
            DrawGates(state);
            DrawGenerators(state);
            DrawBlocks(state);
            DrawShutters(state);
            DrawElevators(state);
        }

        /// <summary>
        /// Shows block <paramref name="blockIndex"/> at <paramref name="origin"/>
        /// without touching anything else — the drag preview. Does nothing for a
        /// block that is not drawn.
        /// </summary>
        public void ShowDragOrigin(int blockIndex, Coord origin)
        {
            if (blockRoots.TryGetValue(blockIndex, out var root))
            {
                root.localPosition = GridToLocal(new Vector2(origin.X, origin.Y));
            }
        }

        /// <summary>Destroys everything drawn.</summary>
        public void Clear()
        {
            blockRoots.Clear();
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
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

                var origin = state.Origins[i];
                var root = new GameObject($"Block {i}").transform;
                root.SetParent(transform, false);
                root.localPosition = GridToLocal(new Vector2(origin.X, origin.Y));
                blockRoots[i] = root;

                var fill = visual.IsFrozen ? config.FrozenTint : config.BlockFill(visual.OuterColor.Value);
                var cells = ctx.SpecAt(i).Cells;
                var rects = BlockCellRects.Compute(cells, config.CellGap);
                for (var c = 0; c < cells.Count; c++)
                {
                    AddSprite(
                        root, $"Cell {cells[c]}", rects[c].center * layout.CellSize, rects[c].size * layout.CellSize,
                        fill, config.BlockOrder);

                    var center = CellCenterInBlock(cells[c]);

                    if (visual.BeneathColor.HasValue)
                    {
                        AddSprite(
                            root, $"Beneath {cells[c]}", center, new Vector2(beneathSide, beneathSide),
                            config.BlockFill(visual.BeneathColor.Value), config.BeneathColorOrder);
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
        private void AddSprite(Transform parent, string name, Vector2 localCenter, Vector2 size, Color color, int order)
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
    }
}
