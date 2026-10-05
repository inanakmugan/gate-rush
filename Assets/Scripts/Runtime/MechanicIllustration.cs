using System;
using System.Collections.Generic;
using System.Globalization;
using GateRush.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// Draws a mechanic on the canvas for its introduction card (Module 19),
    /// as it looks on the board: the same generated sprites, placed by the same
    /// layout rules — <see cref="BlockTiling"/>, <see cref="FrameTiling"/>,
    /// <see cref="GeneratorMachine"/>, <see cref="MarkLayout"/> — and sized by
    /// the board's own cell-relative values in <see cref="RuntimeConfig"/>,
    /// times <see cref="RuntimeConfig.IntroCellUnits"/>. Retuning the board
    /// retunes the cards.
    /// </summary>
    /// <remarks>
    /// <para><b>A twin of <see cref="BoardView"/>'s recipes.</b> The canvas is
    /// a screen-space overlay, where a <c>SpriteRenderer</c> cannot draw, so
    /// every piece here is a uGUI <see cref="Image"/>. The layout rules and
    /// config values are shared, but the composing recipes — a block's lip,
    /// face and studs, the count badge, a lock's chains and padlock, a key, the
    /// time-bonus mark, a gate, a shutter's panel, a generator's machine and
    /// miniature, an elevator's doors — are written a second time here. They
    /// mirror <c>BoardView.cs</c>'s <c>Draw…</c> methods and <b>must change
    /// with them</b>: a change to how the board draws one of these is not done
    /// until the same change is made here.</para>
    /// <para><b>Order.</b> uGUI draws in hierarchy order, so each piece is
    /// added in the board's sorting order: lip, face, studs or frost, gloss or
    /// arrow, beneath squares, chains, padlock or key, gem, badge rim, badge
    /// fill, label.</para>
    /// <para><b>Units.</b> Every position and size in this class is in cells,
    /// in the scene's own frame; <see cref="AddRect"/> alone converts to canvas
    /// units. Nothing here is a raycast target: a tap on an illustration falls
    /// through to the card's backdrop.</para>
    /// </remarks>
    public sealed class MechanicIllustration
    {
        /// <summary>
        /// A world-space TextMeshPro label draws one tenth as large per font
        /// unit as a canvas label does. TextMeshPro's constant, not a tunable:
        /// it turns the board's label sizes into the canvas's.
        /// </summary>
        private const float WorldFontUnitsPerCanvasFontUnit = 0.1f;

        /// <summary>The side, in cells, of the square region a shutter or an elevator covers on its card.</summary>
        private const int RegionCells = 2;

        /// <summary>The width, in cells, of the Ice Door card's gate and of the Generator card's machine.</summary>
        private const int EdgeFeatureCells = 2;

        /// <summary>The Lock &amp; Key card shows one key, so its lock asks for one.</summary>
        private const int KeysOnTheLockCard = 1;

        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        /// <summary>A 1×1 block: the How to Play card's block and the key's carrier.</summary>
        private static readonly IReadOnlyList<Coord> Single = new[] { new Coord(0, 0) };

        /// <summary>A horizontal 1×2 block: every other card's block.</summary>
        private static readonly IReadOnlyList<Coord> Horizontal2 = new[] { new Coord(0, 0), new Coord(1, 0) };

        private readonly RuntimeConfig config;
        private readonly float referencePixelsPerUnit;
        private readonly float unitsPerCell;

        /// <summary>An illustrator for one canvas.</summary>
        /// <param name="config">The board's and the card's values.</param>
        /// <param name="referencePixelsPerUnit">The canvas's reference pixels per unit, which sliced and tiled images are sized against.</param>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public MechanicIllustration(RuntimeConfig config, float referencePixelsPerUnit)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.referencePixelsPerUnit = referencePixelsPerUnit;
            unitsPerCell = config.IntroCellUnits;
        }

        /// <summary>How a block's face is finished.</summary>
        private enum BlockFace
        {
            /// <summary>Studs with their gloss on every cell.</summary>
            Studs,

            /// <summary>Frost on every cell (M3); the fill is the ice colour.</summary>
            Ice,

            /// <summary>One double-headed arrow along the block instead of studs (M7).</summary>
            AxisArrow
        }

        /// <summary>
        /// Draws <paramref name="mechanic"/> under <paramref name="area"/>,
        /// centred on it and scaled down uniformly when it is larger than
        /// <see cref="RuntimeConfig.IntroIllustrationSizeUnits"/>.
        /// </summary>
        /// <returns>The illustration's root, for the caller to destroy when the card changes.</returns>
        public RectTransform Build(RectTransform area, LevelMechanic mechanic)
        {
            var root = UiBuilder.CreateRect("Art", area);
            UiBuilder.PlaceCentered(root, Vector2.zero);

            var bounds = Draw(root, mechanic);

            var scale = ScreenBands.ContentScale(config.IntroIllustrationSizeUnits, bounds.size * unitsPerCell);
            root.localScale = new Vector3(scale, scale, 1f);
            root.anchoredPosition = -bounds.center * unitsPerCell * scale;
            return root;
        }

        /// <summary>Draws the scene for <paramref name="mechanic"/> and returns what it covers, in cells.</summary>
        private Rect Draw(RectTransform root, LevelMechanic mechanic)
        {
            var block = config.BlockFill(config.IntroBlockColor);
            switch (mechanic)
            {
                case LevelMechanic.HowToPlay:
                    return DrawHowToPlay(root);

                case LevelMechanic.IceBlock:
                {
                    var body = DrawBlock(root, "Ice block", Vector2.zero, Horizontal2, config.IceColor, BlockFace.Ice);
                    DrawBadge(body, MarkLayout.Anchor(Horizontal2), config.IntroCount, config.BadgeRimColor);
                    return BlockBounds(Vector2.zero, Horizontal2);
                }

                case LevelMechanic.IceDoor:
                    return DrawIceDoor(root);

                case LevelMechanic.LayeredBlock:
                {
                    var body = DrawBlock(root, "Layered block", Vector2.zero, Horizontal2, block, BlockFace.Studs);
                    var side = config.BeneathColorSize;
                    for (var c = 0; c < Horizontal2.Count; c++)
                    {
                        AddSprite(
                            body, $"Beneath {Horizontal2[c]}", config.CellSprite, CellCenter(Horizontal2[c]), new Vector2(side, side),
                            config.BlockFill(config.IntroSecondColor));
                    }

                    return BlockBounds(Vector2.zero, Horizontal2);
                }

                case LevelMechanic.OneWayBlock:
                    DrawBlock(root, "One-way block", Vector2.zero, Horizontal2, block, BlockFace.AxisArrow);
                    return BlockBounds(Vector2.zero, Horizontal2);

                case LevelMechanic.LockAndKey:
                    return DrawLockAndKey(root);

                case LevelMechanic.Shutter:
                    return DrawShutter(root);

                case LevelMechanic.Generator:
                    return DrawGenerator(root);

                case LevelMechanic.Elevator:
                    return DrawElevator(root);

                default:
                {
                    var body = DrawBlock(root, "Time-bonus block", Vector2.zero, Horizontal2, block, BlockFace.Studs);
                    DrawTimeBonusMark(body, MarkLayout.Anchor(Horizontal2), config.IntroBonusSeconds);
                    return BlockBounds(Vector2.zero, Horizontal2);
                }
            }
        }

        /// <summary>
        /// A 1×1 block at the left of a 2×1 board, an arrow in the free cell,
        /// and the block's open gate on the board's right edge.
        /// </summary>
        private Rect DrawHowToPlay(RectTransform root)
        {
            var boardWidth = Single.Count + 1;
            var gate = new GateDefinition(0, BoardEdge.Right, 0, 1, config.IntroBlockColor, null);
            var ctx = GateBoard(boardWidth, 1, gate);

            DrawBlock(root, "Block", Vector2.zero, Single, config.BlockFill(config.IntroBlockColor), BlockFace.Studs);

            var arrow = config.IntroHowToPlayArrowCells;
            AddSprite(
                root, "Arrow", config.GateArrowSprite, CellCenter(new Coord(boardWidth - 1, 0)), new Vector2(arrow, arrow),
                config.GateArrowColor, Vector2.SignedAngle(Vector2.up, GateExit.Outward(gate.Edge)));

            DrawGate(root, ctx, isOpen: true);

            var lip = config.LipOffsetCells;
            return Rect.MinMaxRect(0f, -lip, boardWidth + config.FrameThicknessCells, 1f);
        }

        /// <summary>A closed gate (M2) on the top edge of a board as wide as the gate.</summary>
        private Rect DrawIceDoor(RectTransform root)
        {
            var gate = new GateDefinition(0, BoardEdge.Top, 0, EdgeFeatureCells, config.IntroBlockColor, config.IntroCount);
            var ctx = GateBoard(EdgeFeatureCells, 1, gate);

            DrawGate(root, ctx, isOpen: false);

            var span = GateSpan(ctx);
            return Rect.MinMaxRect(span.xMin, span.yMin - config.LipOffsetCells, span.xMax, span.yMax);
        }

        /// <summary>A locked 1×2 block beside a 1×1 block carrying its key, whose gem is the lock's colour (D47).</summary>
        private Rect DrawLockAndKey(RectTransform root)
        {
            var locked = DrawBlock(
                root, "Locked block", Vector2.zero, Horizontal2, config.BlockFill(config.IntroBlockColor), BlockFace.Studs);
            DrawLock(locked, Horizontal2, MarkLayout.Anchor(Horizontal2), KeysOnTheLockCard);

            var carrierOrigin = new Vector2(Horizontal2.Count, 0f);
            var carrier = DrawBlock(
                root, "Key block", carrierOrigin, Single, config.BlockFill(config.IntroSecondColor), BlockFace.Studs);
            DrawKey(carrier, MarkLayout.Anchor(Single), config.BlockFill(config.IntroBlockColor));

            var first = BlockBounds(Vector2.zero, Horizontal2);
            var second = BlockBounds(carrierOrigin, Single);
            return Rect.MinMaxRect(first.xMin, first.yMin, second.xMax, first.yMax);
        }

        /// <summary>A closed shutter's panel (M5): slats, a border, and its count.</summary>
        private Rect DrawShutter(RectTransform root)
        {
            var region = new Rect(0f, 0f, RegionCells, RegionCells);
            AddTiled(
                root, "Slats", config.ShutterSlatsSprite, region.center, region.size,
                config.ShutterSlatsSprite.rect.width, 1f, config.ShutterSlatColor);
            AddSliced(root, "Border", config.RingSprite, region.center, region.size, config.ShutterBorderCells, config.ShutterBorderColor);
            DrawBadge(root, region.center, config.IntroCount, config.BadgeRimColor);
            return region;
        }

        /// <summary>An elevator's lift doors (M9): panels, a divider down the middle, and a border.</summary>
        private Rect DrawElevator(RectTransform root)
        {
            var region = new Rect(0f, 0f, RegionCells, RegionCells);
            AddTiled(
                root, "Doors", config.DoorPanelSprite, region.center, region.size,
                config.DoorPanelSprite.rect.width, 1f, config.ElevatorDoorColor);

            var divider = config.ElevatorDividerCells;
            AddSliced(
                root, "Divider", config.RoundedRectSprite, region.center, new Vector2(divider, region.height), divider * 0.5f,
                config.ElevatorDividerColor);
            AddSliced(root, "Border", config.RingSprite, region.center, region.size, config.ElevatorBorderCells, config.ElevatorBorderColor);
            return region;
        }

        /// <summary>
        /// A generator's machine (M6, D48) above a board as wide as the
        /// machine: body, screen, the next block in miniature, and the queued
        /// count. Placed by <see cref="GeneratorMachine.Place"/>, the board's
        /// rule.
        /// </summary>
        private Rect DrawGenerator(RectTransform root)
        {
            var queue = new SpawnedBlock[config.IntroCount];
            for (var q = 0; q < queue.Length; q++)
            {
                queue[q] = new SpawnedBlock(
                    Horizontal2, new[] { config.IntroBlockColor }, MovementAxis.Free, null, null, 0, null, 0);
            }

            var generator = new GeneratorDefinition(0, BoardEdge.Top, 0, EdgeFeatureCells, queue);
            var placement = config.CreateGeneratorMachine().Place(EdgeFeatureCells, 1, generator);
            var body = placement.Body;
            var screen = placement.Screen;
            var corner = config.MachineCornerCells;
            var lipOffset = new Vector2(0f, -config.LipOffsetCells);

            AddSliced(
                root, "Body lip", config.RoundedRectSprite, body.center + lipOffset, body.size, corner,
                config.LipFill(config.MachineColor));
            AddSliced(root, "Body", config.RoundedRectSprite, body.center, body.size, corner, config.MachineColor);
            AddSliced(root, "Screen", config.RoundedRectSprite, screen.center, screen.size, corner, config.MachineScreenColor);

            DrawMiniature(root, Horizontal2, screen, config.BlockFill(config.IntroBlockColor));
            DrawBadge(root, placement.BadgeCenter, queue.Length, config.BadgeRimColor);

            var badgeTop = placement.BadgeCenter.y + config.BadgeHeightCells * 0.5f;
            return Rect.MinMaxRect(body.xMin, body.yMin - config.LipOffsetCells, body.xMax, Mathf.Max(body.yMax, badgeTop));
        }

        /// <summary>
        /// A block at <paramref name="origin"/>, as <c>BoardView.DrawBlocks</c>
        /// and <c>DrawFace</c> draw it: lip, face quarters, then frost on ice,
        /// studs with gloss on a plain block, or the axis arrow.
        /// </summary>
        /// <returns>The block's body, whose frame is the footprint's: marks go under it.</returns>
        private RectTransform DrawBlock(
            RectTransform parent, string name, Vector2 origin, IReadOnlyList<Coord> cells, Color fill, BlockFace face)
        {
            var body = AddGroup(parent, name, origin);
            var tiles = BlockTiling.Compute(cells);

            var lip = AddGroup(body, "Lip", new Vector2(0f, -config.LipOffsetCells));
            AddQuarters(lip, tiles, BlockTiling.QuarterRect, config.LipFill(fill));
            AddQuarters(body, tiles, BlockTiling.QuarterRect, fill);

            for (var c = 0; c < cells.Count; c++)
            {
                var center = CellCenter(cells[c]);
                if (face == BlockFace.Ice)
                {
                    AddSprite(body, $"Frost {cells[c]}", config.FrostSprite, center, Vector2.one, config.FrostColor);
                }
                else if (face == BlockFace.Studs)
                {
                    AddSprite(body, $"Studs {cells[c]}", config.StudsSprite, center, Vector2.one, fill);
                    AddSprite(body, $"Gloss {cells[c]}", config.StudGlossSprite, center, Vector2.one, config.GlossColor);
                }
            }

            if (face == BlockFace.AxisArrow)
            {
                DrawAxisArrow(body, cells);
            }

            return body;
        }

        /// <summary>
        /// The M7 arrow along a horizontal block, as <c>BoardView.DrawAxisArrow</c>
        /// draws it: sliced, so only its shaft stretches, its thickness the
        /// sprite's whole height.
        /// </summary>
        private void DrawAxisArrow(RectTransform body, IReadOnlyList<Coord> cells)
        {
            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            var center = new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);
            var thickness = config.AxisArrowThicknessCells;
            var length = maxX + 1 - minX - 2f * config.AxisArrowEndInsetCells;

            var sprite = config.AxisArrowSprite;
            var image = AddImage(body, "Axis arrow", sprite, center, new Vector2(length, thickness), config.AxisArrowColor, 0f);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = UiBuilder.TiledPixelsPerUnitMultiplier(
                sprite.rect.height, sprite.pixelsPerUnit, referencePixelsPerUnit, thickness * unitsPerCell);
        }

        /// <summary>The count badge, as <c>BoardView.DrawBadge</c> draws it.</summary>
        private void DrawBadge(RectTransform parent, Vector2 center, int value, Color rim)
        {
            var height = config.BadgeHeightCells;
            var width = config.BadgeWidthCells(value);
            var inner = new Vector2(width - 2f * config.BadgeRimCells, height - 2f * config.BadgeRimCells);

            var badge = AddGroup(parent, "Badge", center);
            AddSliced(badge, "Rim", config.RoundedRectSprite, Vector2.zero, new Vector2(width, height), height * 0.5f, rim);
            AddSliced(badge, "Fill", config.RoundedRectSprite, Vector2.zero, inner, inner.y * 0.5f, config.BadgeColor);
            AddLabel(
                badge, "Count", Vector2.zero, value.ToString(CultureInfo.InvariantCulture),
                config.BadgeLabelFontSize, config.BadgeTextColor);
        }

        /// <summary>A lock (M8), as <c>BoardView.DrawLock</c> draws it: a chain along each row, then the padlock and its count.</summary>
        private void DrawLock(RectTransform body, IReadOnlyList<Coord> cells, Vector2 icon, int keysStillRequired)
        {
            var chains = AddGroup(body, "Chains", Vector2.zero);
            var thickness = config.ChainThicknessCells;
            var strips = MarkLayout.ChainStrips(cells, thickness, config.ChainEndInsetCells);
            var sprite = config.ChainSprite;
            for (var s = 0; s < strips.Count; s++)
            {
                AddTiled(chains, $"Chain {s}", sprite, strips[s].center, strips[s].size, sprite.rect.height, thickness, config.ChainColor);
            }

            var mark = AddGroup(body, "Padlock", icon);
            var padlock = config.PadlockSizeCells;
            AddSprite(mark, "Body", config.PadlockSprite, Vector2.zero, new Vector2(padlock, padlock), config.PadlockColor);
            DrawBadge(mark, new Vector2(0f, -config.PadlockBadgeDropCells), keysStillRequired, config.BadgeRimColor);
        }

        /// <summary>A key, as <c>BoardView.DrawBlockMarks</c> draws it: the gold body, then its gem in the lock's colour.</summary>
        private void DrawKey(RectTransform body, Vector2 icon, Color gem)
        {
            var bounds = config.KeyBodySprite.bounds.size;
            var length = config.KeySizeCells;
            var size = new Vector2(length, length * bounds.y / bounds.x);
            var key = AddGroup(body, "Key", icon);
            AddSprite(key, "Body", config.KeyBodySprite, Vector2.zero, size, config.KeyColor, config.KeyRotationDegrees);
            AddSprite(key, "Gem", config.KeyGemSprite, Vector2.zero, size, gem, config.KeyRotationDegrees);
        }

        /// <summary>The time-bonus mark (M10), as <c>BoardView.DrawTimeBonusMark</c> draws it.</summary>
        private void DrawTimeBonusMark(RectTransform body, Vector2 center, int seconds)
        {
            var text = config.TimeBonusMarkText(seconds);
            var height = config.BadgeHeightCells;
            var width = config.TimeBonusMarkWidthCells(text);
            var rim = config.BadgeRimCells;
            var inner = new Vector2(width - 2f * rim, height - 2f * rim);
            var icon = config.TimeBonusMarkIconCells;
            var padding = config.BadgePaddingCells;

            var iconCenter = -width * 0.5f + padding + icon * 0.5f;
            var textLeft = iconCenter + icon * 0.5f + config.TimeBonusMarkIconGapCells;
            var textRight = width * 0.5f - padding;

            var mark = AddGroup(body, "Time bonus", center);
            AddSliced(mark, "Rim", config.RoundedRectSprite, Vector2.zero, new Vector2(width, height), height * 0.5f, config.TimeBonusMarkRimColor);
            AddSliced(mark, "Fill", config.RoundedRectSprite, Vector2.zero, inner, inner.y * 0.5f, config.TimeBonusMarkColor);
            AddSprite(mark, "Clock", config.ClockSprite, new Vector2(iconCenter, 0f), new Vector2(icon, icon), config.TimeBonusMarkIconColor);
            AddLabel(mark, "Seconds", new Vector2((textLeft + textRight) * 0.5f, 0f), text, config.BadgeLabelFontSize, config.BadgeTextColor);
        }

        /// <summary>
        /// The one gate of <paramref name="ctx"/>, as <c>BoardView.DrawGates</c>
        /// draws it: its own segment of the frame, with a lip; open, in its
        /// colour with the white arrow pointing out; closed, in ice with frost
        /// on each cell and the clears still needed on a badge.
        /// </summary>
        private void DrawGate(RectTransform root, LevelContext ctx, bool isOpen)
        {
            var gate = ctx.Gates[0];
            var thickness = config.FrameThicknessCells;
            var tiles = BlockTiling.Compute(FrameTiling.GateCells(ctx, 0));
            Rect RectOf(QuarterTile tile) => FrameTiling.QuarterRect(tile, ctx.Width, ctx.Height, thickness);

            var fill = isOpen ? config.BlockFill(gate.Color) : config.IceColor;
            var group = AddGroup(root, "Gate", Vector2.zero);
            var lip = AddGroup(group, "Lip", new Vector2(0f, -config.LipOffsetCells));
            AddQuarters(lip, tiles, RectOf, config.LipFill(fill));
            AddQuarters(group, tiles, RectOf, fill);

            var span = GateSpan(ctx);
            if (isOpen)
            {
                var arrow = config.GateArrowSizeCells;
                AddSprite(
                    group, "Arrow", config.GateArrowSprite, span.center, new Vector2(arrow, arrow), config.GateArrowColor,
                    Vector2.SignedAngle(Vector2.up, GateExit.Outward(gate.Edge)));
                return;
            }

            for (var i = 0; i < gate.Width; i++)
            {
                var cell = FrameTiling.EdgeSpanRect(ctx.Width, ctx.Height, gate.Edge, gate.Offset + i, 1, thickness);
                AddSprite(group, $"Frost {i}", config.FrostSprite, cell.center, cell.size, config.FrostColor);
            }

            // .Value is safe: only the Ice Door card draws a closed gate, and
            // it gives the gate its threshold.
            DrawBadge(group, span.center, gate.OpenAtClearCount.Value, config.BadgeRimColor);
        }

        /// <summary>
        /// The next block on a machine's screen, as <c>BoardView.DrawMiniature</c>
        /// draws it, fitted by the same <see cref="BoardView.MiniatureFit"/>.
        /// </summary>
        private void DrawMiniature(RectTransform parent, IReadOnlyList<Coord> cells, Rect screen, Color fill)
        {
            var perCell = BoardView.MiniatureFit(cells, screen, out var shapeCenter);
            var tiles = BlockTiling.Compute(cells);

            Rect RectOf(QuarterTile tile)
            {
                var cellRect = BlockTiling.QuarterRect(tile);
                return new Rect(screen.center + (cellRect.min - shapeCenter) * perCell, cellRect.size * perCell);
            }

            var miniature = AddGroup(parent, "Next block", Vector2.zero);
            var lip = AddGroup(miniature, "Lip", new Vector2(0f, -config.LipOffsetCells * perCell));
            AddQuarters(lip, tiles, RectOf, config.LipFill(fill));
            AddQuarters(miniature, tiles, RectOf, fill);
        }

        /// <summary>A board holding nothing but <paramref name="gate"/>: what <see cref="FrameTiling"/> needs to place it.</summary>
        private static LevelContext GateBoard(int width, int height, GateDefinition gate) =>
            new LevelContext(0, width, height, null, null, new[] { gate }, null, null, null, 0, 0);

        /// <summary>The whole span on the frame of the one gate of <paramref name="ctx"/>.</summary>
        private Rect GateSpan(LevelContext ctx)
        {
            var gate = ctx.Gates[0];
            return FrameTiling.EdgeSpanRect(ctx.Width, ctx.Height, gate.Edge, gate.Offset, gate.Width, config.FrameThicknessCells);
        }

        /// <summary>What a block at <paramref name="origin"/> covers: its footprint's bounding box and the lip below it.</summary>
        private Rect BlockBounds(Vector2 origin, IReadOnlyList<Coord> cells)
        {
            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            return Rect.MinMaxRect(
                origin.x + minX, origin.y + minY - config.LipOffsetCells, origin.x + maxX + 1, origin.y + maxY + 1);
        }

        private static Vector2 CellCenter(Coord cell) => new Vector2(cell.X + 0.5f, cell.Y + 0.5f);

        /// <summary>
        /// One image per quarter, posed as its tile says, as
        /// <c>BoardView.AddQuarters</c> and <c>PoseQuarter</c> do. An
        /// <see cref="Image"/> has no flip, so a mirrored quarter gets a
        /// negative scale, which — like a <c>SpriteRenderer</c>'s flip — acts
        /// in the sprite's own frame, before the rotation.
        /// </summary>
        private void AddQuarters(RectTransform parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, Color color)
        {
            var overlap = config.SeamOverlapCells * 2f;
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var rect = rectOf(tile);
                var size = rect.size + new Vector2(overlap, overlap);
                var frameSize = tile.IsRotated ? new Vector2(size.y, size.x) : size;
                var image = AddSprite(
                    parent, tile.ToString(), config.QuarterSprite(tile.SpriteKind), rect.center, frameSize, color,
                    tile.IsRotated ? QuarterTile.EdgeAlongYRotationDegrees : 0f);
                image.rectTransform.localScale = new Vector3(tile.FlipX ? -1f : 1f, tile.FlipY ? -1f : 1f, 1f);
            }
        }

        /// <summary>A group whose children are placed relative to <paramref name="center"/>.</summary>
        private RectTransform AddGroup(RectTransform parent, string name, Vector2 center) =>
            AddRect(parent, name, center, Vector2.zero, 0f);

        /// <summary>A tinted sprite stretched over <paramref name="size"/>, turned by <paramref name="rotationDegrees"/>.</summary>
        private Image AddSprite(
            RectTransform parent, string name, Sprite sprite, Vector2 center, Vector2 size, Color color, float rotationDegrees = 0f)
        {
            var image = AddImage(parent, name, sprite, center, size, color, rotationDegrees);
            image.type = Image.Type.Simple;
            return image;
        }

        /// <summary>
        /// A 9-sliced sprite over <paramref name="size"/> whose slice border
        /// draws <paramref name="border"/> wide, capped at half the smaller
        /// side; nothing, and null, when the border is not positive — the
        /// rule of <c>BoardView.AddSliced</c>.
        /// </summary>
        private Image AddSliced(RectTransform parent, string name, Sprite sprite, Vector2 center, Vector2 size, float border, Color color)
        {
            var drawn = Mathf.Min(border, Mathf.Min(size.x, size.y) * 0.5f);
            if (!(drawn > 0f))
            {
                return null;
            }

            var image = AddImage(parent, name, sprite, center, size, color, 0f);
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = UiBuilder.SlicedPixelsPerUnitMultiplier(
                sprite.border.x, sprite.pixelsPerUnit, referencePixelsPerUnit, drawn * unitsPerCell, size * unitsPerCell);
            return image;
        }

        /// <summary>
        /// A sprite repeated over <paramref name="size"/>, each tile drawn so
        /// that <paramref name="spritePixels"/> of it cover
        /// <paramref name="tileCells"/>: one cell per tile of slats or doors,
        /// the chain's thickness per tile of chain.
        /// </summary>
        private Image AddTiled(
            RectTransform parent, string name, Sprite sprite, Vector2 center, Vector2 size, float spritePixels, float tileCells, Color color)
        {
            var image = AddImage(parent, name, sprite, center, size, color, 0f);
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = UiBuilder.TiledPixelsPerUnitMultiplier(
                spritePixels, sprite.pixelsPerUnit, referencePixelsPerUnit, tileCells * unitsPerCell);
            return image;
        }

        private Image AddImage(
            RectTransform parent, string name, Sprite sprite, Vector2 center, Vector2 size, Color color, float rotationDegrees)
        {
            var rect = AddRect(parent, name, center, size, rotationDegrees);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A label in the label font, sized as the board's world-space label of
        /// <paramref name="boardFontSize"/> reads on a cell.
        /// </summary>
        private void AddLabel(RectTransform parent, string name, Vector2 center, string text, float boardFontSize, Color color)
        {
            var rect = AddRect(parent, name, center, Vector2.one, 0f);
            var fontSize = boardFontSize * WorldFontUnitsPerCanvasFontUnit / config.CellSize * unitsPerCell;
            UiBuilder.AddLabel(rect, config.LabelFont, fontSize, color, false).text = text;
        }

        /// <summary>
        /// The one place cells become canvas units: a rect centred
        /// <paramref name="center"/> from its parent's centre,
        /// <paramref name="size"/> large.
        /// </summary>
        private RectTransform AddRect(RectTransform parent, string name, Vector2 center, Vector2 size, float rotationDegrees)
        {
            var rect = UiBuilder.CreateRect(name, parent);
            UiBuilder.Place(rect, Middle, center * unitsPerCell, size * unitsPerCell);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            return rect;
        }
    }
}
