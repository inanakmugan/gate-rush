using System;
using System.Collections.Generic;
using System.Globalization;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// How the things on a board are composed from sprites, written once
    /// (Module 28): a block's lip, face, inner shape, studs, frost and axis
    /// arrow; the count badge; a lock's chains and padlock; a key; the
    /// time-bonus mark; a gate; a shutter's panel; an elevator's doors; a
    /// generator's machine and the miniature on its screen.
    /// <see cref="BoardView"/> draws the board with them and
    /// <see cref="MechanicIllustration"/> the introduction cards, each through
    /// its own <see cref="IRecipeSurface{TGroup, TPiece}"/>, so a card cannot
    /// drift from the board: a recipe changed here changes on both.
    /// </summary>
    /// <remarks>
    /// <para><b>What is not here.</b> Which marks a block carries and where
    /// they sit together (<see cref="MarkLayout"/>, decided by each caller),
    /// the floor and the frame, and everything a move animates stay with
    /// <see cref="BoardView"/>: only the board draws them.</para>
    /// <para><b>Units and order.</b> Every position and size is in cells in
    /// the frame of the group given, and every recipe adds its pieces from the
    /// bottom up — see <see cref="IRecipeSurface{TGroup, TPiece}"/>. The group
    /// and piece names are the board's; its effects find parts by the groups
    /// these recipes return, and its hierarchy must not change under them.</para>
    /// </remarks>
    /// <typeparam name="TGroup">The surface's group.</typeparam>
    /// <typeparam name="TPiece">The surface's drawn sprite.</typeparam>
    internal sealed class BoardRecipes<TGroup, TPiece>
        where TGroup : class
        where TPiece : class
    {
        /// <summary>A shutter's slats and an elevator's doors repeat once per cell. The sprites' own shape, not a tunable.</summary>
        private const float PanelTileCells = 1f;

        private readonly RuntimeConfig config;
        private readonly IRecipeSurface<TGroup, TPiece> surface;

        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public BoardRecipes(RuntimeConfig config, IRecipeSurface<TGroup, TPiece> surface)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
        }

        /// <summary>
        /// A block under <paramref name="body"/>, whose frame starts at the
        /// corner of the footprint's origin cell, lit as a raised body
        /// (Module 46): a <c>Contour</c> group with its soft shadow on the
        /// floor, untinted (<see cref="QuarterLayer.Shadow"/>; the group keeps
        /// the outline's name), and a <c>Face</c> group with the footprint's
        /// lit quarters in <paramref name="fill"/> and over them their white
        /// highlight; then frost on every cell when it is ice, the cell's studs
        /// with their gloss when it is not, and — for an axis-restricted block
        /// (M7), ice or not — one double-headed arrow along its axis instead of
        /// studs.
        /// </summary>
        /// <remarks>
        /// <para><b>Layered (M4, Module 46).</b> With a colour
        /// <paramref name="beneath"/>, the block is drawn as above in its outer
        /// colour darkened (<see cref="RuntimeConfig.LayeredOuterFill"/>), and
        /// an <c>Inner</c> group sets a recessed window into it, above the
        /// outer studs, so it covers them where they meet: the same tiles
        /// pulled in by <see cref="RuntimeConfig.LayerInsetCells"/> as a sunken
        /// rim in <see cref="RuntimeConfig.LayerEdgeFill"/>, lit the other way
        /// round (<see cref="QuarterLayer.ReversedLit"/>), and inside it the
        /// inner block, a smaller lit body in the colour beneath with its own
        /// highlight and its own studs on its own grid (<see cref="LayerStuds"/>)
        /// — Module 20's studs that take the colour beneath. A layered
        /// axis-restricted block's arrow sits over the window.</para>
        /// <para>Pieces are added from the bottom up, so the introduction
        /// cards, which draw in hierarchy order, layer them as the board's
        /// sorting orders do.</para>
        /// </remarks>
        /// <param name="tiles"><see cref="BlockTiling.Compute"/> of <paramref name="cells"/>.</param>
        public BlockParts<TPiece> Block(
            TGroup body, IReadOnlyList<Coord> cells, IReadOnlyList<QuarterTile> tiles, Color fill, Color? beneath,
            bool isIce, MovementAxis axis)
        {
            var outerFill = beneath.HasValue ? config.LayeredOuterFill(fill) : fill;

            var contour = surface.Group(body, "Contour", Vector2.zero, 1f);
            var margin = config.ShadowMarginCells;
            surface.Quarters(
                contour, QuarterLayers.Edged(tiles), tile => FaceShape.ShadowQuarterRect(BlockTiling.QuarterRect(tile), tile, margin),
                QuarterLayer.Shadow, Color.white, config.BlockContourOrder);

            var face = surface.Group(body, "Face", Vector2.zero, 1f);
            var faceQuarters = surface.Quarters(face, tiles, BlockTiling.QuarterRect, QuarterLayer.Lit, outerFill, config.BlockOrder);
            var shineQuarters = Highlight(
                face, tiles, tile => FaceShape.LightQuarterRect(tile, tiles, 0f), config.BlockLightColor, config.BlockShineOrder);

            var studs = new List<TPiece>();
            var hasStuds = !isIce && axis == MovementAxis.Free;
            for (var c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                var center = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
                if (isIce)
                {
                    surface.Sprite(face, $"Frost {cell}", config.FrostSprite, center, Vector2.one, config.FrostColor, config.StudOrder, 0f);
                }
                else if (hasStuds)
                {
                    studs.Add(surface.LitSprite(
                        face, $"Studs {cell}", config.StudsSprite, center, Vector2.one, outerFill, config.StudOrder));
                    surface.Sprite(
                        face, $"Gloss {cell}", config.StudGlossSprite, center, Vector2.one, config.GlossColor,
                        config.GlossOrder, 0f);
                }
            }

            LayerParts<TPiece> layer = null;
            if (beneath.HasValue)
            {
                layer = Window(face, cells, tiles, fill, beneath.Value, hasStuds);
            }

            if (axis != MovementAxis.Free)
            {
                AxisArrow(face, cells, axis, beneath.HasValue ? config.LayerGlossOrder : config.GlossOrder);
            }

            return new BlockParts<TPiece>(faceQuarters, shineQuarters, studs, layer);
        }

        /// <summary>
        /// A layered block's window, in an <c>Inner</c> group under
        /// <paramref name="face"/>: the sunken rim at the window's inset, lit
        /// the other way round as a recess is; the inner block inside it, a
        /// smaller lit body in <paramref name="beneath"/>, with its highlight;
        /// and — when the block carries studs — the inner block's studs with
        /// their gloss (<see cref="LayerStuds"/>). The highlight's quarters
        /// keep to their own span along the edge
        /// (<see cref="FaceShape.LightQuarterRect"/>), so it is added once
        /// along each side.
        /// </summary>
        private LayerParts<TPiece> Window(
            TGroup face, IReadOnlyList<Coord> cells, IReadOnlyList<QuarterTile> tiles, Color outer, Color beneath, bool hasStuds)
        {
            var edgeInset = config.LayerInsetCells;
            var innerInset = config.LayerInnerInsetCells;

            var inner = surface.Group(face, "Inner", Vector2.zero, 1f);
            var edge = surface.Quarters(
                inner, tiles, tile => FaceShape.QuarterRect(tile, edgeInset), QuarterLayer.ReversedLit,
                config.LayerEdgeFill(outer), config.LayerEdgeOrder);
            var innerFace = surface.Quarters(
                inner, tiles, tile => FaceShape.QuarterRect(tile, innerInset), QuarterLayer.Lit, beneath, config.LayerOrder);
            var innerShine = Highlight(
                inner, tiles, tile => FaceShape.LightQuarterRect(tile, tiles, innerInset), config.BlockLightColor,
                config.LayerShineOrder);

            var innerStuds = new List<TPiece>();
            if (hasStuds)
            {
                var centers = LayerStuds.Centers(
                    cells, innerInset, config.LayerStudSpacingCells, config.LayerStudClearanceCells);
                for (var i = 0; i < centers.Count; i++)
                {
                    innerStuds.Add(surface.LitSprite(
                        inner, $"Inner stud {i}", config.SingleStudSprite, centers[i], Vector2.one, beneath,
                        config.LayerStudOrder));
                    innerStuds.Add(surface.Sprite(
                        inner, $"Inner gloss {i}", config.SingleStudGlossSprite, centers[i], Vector2.one, config.GlossColor,
                        config.LayerGlossOrder, 0f));
                }
            }

            return new LayerParts<TPiece>(edge, innerFace, innerShine, innerStuds);
        }

        /// <summary>
        /// A <c>Shine</c> group under <paramref name="parent"/>: the white
        /// highlight over the lit quarters at <paramref name="rectOf"/>, in
        /// <paramref name="color"/>, on every quarter of
        /// <paramref name="tiles"/> — the flat top carries a little too. The
        /// group keeps its name: effects find the drawing by it.
        /// </summary>
        /// <returns>The pieces, in the order of <paramref name="tiles"/>.</returns>
        private List<TPiece> Highlight(
            TGroup parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, Color color, int order)
        {
            var shine = surface.Group(parent, "Shine", Vector2.zero, 1f);
            return surface.Quarters(shine, tiles, rectOf, QuarterLayer.Body, color, order);
        }

        /// <summary>
        /// The M7 arrow: a 9-sliced sprite through the centre of the
        /// footprint's bounding box, along the block's axis, stopping
        /// <see cref="RuntimeConfig.AxisArrowEndInsetCells"/> short of each end
        /// (<see cref="MarkLayout.AxisArrowRect"/>, the rule the Level Editor
        /// shares). Its thickness is the sprite's whole height and only its
        /// sliced middle stretches, so the heads keep their shape at any
        /// length.
        /// </summary>
        private void AxisArrow(TGroup face, IReadOnlyList<Coord> cells, MovementAxis axis, int order)
        {
            var isHorizontal = axis == MovementAxis.HorizontalOnly;
            var arrow = MarkLayout.AxisArrowRect(cells, axis, config.AxisArrowThicknessCells, config.AxisArrowEndInsetCells);

            surface.Strip(
                face, "Axis arrow", config.AxisArrowSprite, arrow.center,
                isHorizontal ? arrow.width : arrow.height, isHorizontal ? arrow.height : arrow.width,
                config.AxisArrowColor, order,
                isHorizontal ? 0f : Vector2.SignedAngle(Vector2.right, Vector2.up));
        }

        /// <summary>
        /// The one count badge (D48): a rounded box in <paramref name="rim"/>
        /// with a slightly smaller one in the badge colour on it, and
        /// <paramref name="value"/> in cream on top. It widens with the number
        /// of digits (<see cref="MarkLayout.BadgeWidth"/>).
        /// </summary>
        /// <returns>The badge's group, centred on <paramref name="center"/>: scaling it pops the badge.</returns>
        public TGroup Badge(TGroup parent, Vector2 center, int value, Color rim)
        {
            var height = config.BadgeHeightCells;
            var width = config.BadgeWidthCells(value);
            var rimThickness = config.BadgeRimCells;
            var inner = new Vector2(width - 2f * rimThickness, height - 2f * rimThickness);

            var badge = surface.Group(parent, "Badge", center, 1f);
            surface.Sliced(badge, "Rim", config.RoundedRectSprite, Vector2.zero, new Vector2(width, height), height * 0.5f, rim, config.BadgeRimOrder);
            surface.Sliced(badge, "Fill", config.RoundedRectSprite, Vector2.zero, inner, inner.y * 0.5f, config.BadgeColor, config.BadgeOrder);
            surface.Label(
                badge, "Count", Vector2.zero, value.ToString(CultureInfo.InvariantCulture),
                config.BadgeLabelFontSize, config.BadgeTextColor);
            return badge;
        }

        /// <summary>
        /// A lock (M8) under <paramref name="parent"/>, whose frame is a block
        /// body's: a chain along each row of <paramref name="cells"/>
        /// (<see cref="MarkLayout.ChainStrips"/>), and a gold padlock centred
        /// on <paramref name="icon"/> at <paramref name="scale"/>, with
        /// <paramref name="keysStillRequired"/> on a badge on its body, or no
        /// badge when that is null. The one drawing of a lock, for a locked
        /// block, for the lock that fades away when it opens, and for the
        /// Lock &amp; Key card.
        /// </summary>
        /// <returns>The badge, or null when none was drawn.</returns>
        public TGroup Lock(TGroup parent, IReadOnlyList<Coord> cells, Vector2 icon, float scale, int? keysStillRequired)
        {
            var chains = surface.Group(parent, "Chains", Vector2.zero, 1f);
            var thickness = config.ChainThicknessCells;
            var strips = MarkLayout.ChainStrips(cells, thickness, config.ChainEndInsetCells);
            for (var s = 0; s < strips.Count; s++)
            {
                surface.Tiled(
                    chains, $"Chain {s}", config.ChainSprite, strips[s].center, strips[s].size, thickness,
                    tileIsMeasuredByHeight: true, config.ChainColor, config.ChainOrder);
            }

            var mark = surface.Group(parent, "Padlock", icon, scale);
            var padlock = config.PadlockSizeCells;
            surface.Sprite(
                mark, "Body", config.PadlockSprite, Vector2.zero, new Vector2(padlock, padlock), config.PadlockColor,
                config.IconOrder, 0f);
            if (!keysStillRequired.HasValue)
            {
                return null;
            }

            return Badge(mark, new Vector2(0f, -config.PadlockBadgeDropCells), keysStillRequired.Value, config.BadgeRimColor);
        }

        /// <summary>
        /// A key (M8) under a block's body, centred on <paramref name="icon"/>
        /// at <paramref name="scale"/>: the gold body, then its gem in
        /// <paramref name="gem"/>, the colour of the lock it opens (D47).
        /// </summary>
        public void Key(TGroup parent, Vector2 icon, float scale, Color gem)
        {
            var key = surface.Group(parent, "Key", icon, scale);
            var bounds = config.KeyBodySprite.bounds.size;
            var length = config.KeySizeCells;
            var size = new Vector2(length, length * bounds.y / bounds.x);
            surface.Sprite(key, "Body", config.KeyBodySprite, Vector2.zero, size, config.KeyColor, config.IconOrder, config.KeyRotationDegrees);
            surface.Sprite(key, "Gem", config.KeyGemSprite, Vector2.zero, size, gem, config.KeyGemOrder, config.KeyRotationDegrees);
        }

        /// <summary>
        /// A time-bonus block's mark (M10) under a block's body, centred on
        /// <paramref name="center"/> at <paramref name="scale"/>: the count
        /// badge's shape in the bonus colours, the clock icon at its left and
        /// the seconds through <see cref="RuntimeConfig.TimeBonusMarkText"/>
        /// beside it. It widens with the text
        /// (<see cref="MarkLayout.BonusBadgeWidth"/>).
        /// </summary>
        public void TimeBonusMark(TGroup parent, Vector2 center, float scale, int seconds)
        {
            var text = config.TimeBonusMarkText(seconds);
            var height = config.BadgeHeightCells;
            var width = config.TimeBonusMarkWidthCells(text);
            var rim = config.BadgeRimCells;
            var inner = new Vector2(width - 2f * rim, height - 2f * rim);
            var icon = config.TimeBonusMarkIconCells;
            var padding = config.BadgePaddingCells;

            // The icon sits inside the left padding; the text is centred in
            // what is left between the icon's gap and the right padding.
            var iconCenter = -width * 0.5f + padding + icon * 0.5f;
            var textLeft = iconCenter + icon * 0.5f + config.TimeBonusMarkIconGapCells;
            var textRight = width * 0.5f - padding;

            var mark = surface.Group(parent, "Time bonus", center, scale);
            surface.Sliced(mark, "Rim", config.RoundedRectSprite, Vector2.zero, new Vector2(width, height), height * 0.5f, config.TimeBonusMarkRimColor, config.BadgeRimOrder);
            surface.Sliced(mark, "Fill", config.RoundedRectSprite, Vector2.zero, inner, inner.y * 0.5f, config.TimeBonusMarkColor, config.BadgeOrder);
            surface.Sprite(
                mark, "Clock", config.ClockSprite, new Vector2(iconCenter, 0f), new Vector2(icon, icon),
                config.TimeBonusMarkIconColor, config.LabelOrder, 0f);
            surface.Label(mark, "Seconds", new Vector2((textLeft + textRight) * 0.5f, 0f), text, config.BadgeLabelFontSize, config.BadgeTextColor);
        }

        /// <summary>
        /// A gate under <paramref name="root"/>: its own segment of the frame,
        /// a lit body like a block's with its highlight and its shadow on the
        /// floor (Module 46). Open, it is in <paramref name="openColor"/> with a
        /// white arrow outlined in the outline colour, pointing out of the
        /// board. Closed (M2) — <paramref name="openColor"/> null — it is ice
        /// with frost on each of its cells and
        /// <paramref name="opensInClears"/> on a badge at its middle, and no
        /// arrow.
        /// </summary>
        /// <param name="tiles">The quarters of the gate's span on the frame (<see cref="FrameTiling.GateCells"/>).</param>
        /// <param name="rectOf">Where a quarter goes, in <paramref name="root"/>'s frame.</param>
        /// <param name="span">The gate's whole span on the frame, in <paramref name="root"/>'s frame.</param>
        /// <param name="cellOfSpan">Cell <c>i</c> of that span, in <paramref name="root"/>'s frame; read only for a closed gate.</param>
        /// <param name="opensInClears">The clears still needed; read only for a closed gate.</param>
        /// <returns>The badge of a closed gate; null for an open one.</returns>
        public TGroup Gate(
            TGroup root, GateDefinition gate, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, Rect span,
            Func<int, Rect> cellOfSpan, BlockColor? openColor, int opensInClears)
        {
            var fill = openColor.HasValue ? config.BlockFill(openColor.Value) : config.IceColor;

            var contour = surface.Group(root, "Contour", Vector2.zero, 1f);
            var margin = config.ShadowMarginCells;
            surface.Quarters(
                contour, QuarterLayers.Edged(tiles), tile => FaceShape.ShadowQuarterRect(rectOf(tile), tile, margin),
                QuarterLayer.Shadow, Color.white, config.FrameContourOrder);
            surface.Quarters(root, tiles, rectOf, QuarterLayer.Lit, fill, config.FrameOrder);
            Highlight(root, tiles, rectOf, config.BlockLightColor, config.FrameShineOrder);

            if (openColor.HasValue)
            {
                var arrow = new Vector2(config.GateArrowSizeCells, config.GateArrowSizeCells);
                var turn = Vector2.SignedAngle(Vector2.up, GateExit.Outward(gate.Edge));
                surface.Sprite(
                    root, "Arrow outline", config.GateArrowOutlineSprite, span.center, arrow, config.ContourColor,
                    config.GateArrowOutlineOrder, turn);
                surface.Sprite(
                    root, "Arrow", config.GateArrowSprite, span.center, arrow, config.GateArrowColor, config.GateMarkOrder, turn);
                return null;
            }

            for (var i = 0; i < gate.Width; i++)
            {
                var cell = cellOfSpan(i);
                surface.Sprite(root, $"Frost {i}", config.FrostSprite, cell.center, cell.size, config.FrostColor, config.GateMarkOrder, 0f);
            }

            return Badge(root, span.center, opensInClears, config.BadgeRimColor);
        }

        /// <summary>
        /// A closed shutter's panel (M5) under <paramref name="parent"/>:
        /// slats over the whole of <paramref name="region"/> and a border,
        /// and — when <paramref name="opensInClears"/> is given — the clears
        /// still needed on a badge at its centre, rimmed in
        /// <paramref name="rim"/>. The one drawing of a shutter, for a closed
        /// one, for the panel that lifts off when it opens, and for the
        /// Shutter card.
        /// </summary>
        /// <returns>The badge, or null when none was drawn.</returns>
        public TGroup ShutterPanel(TGroup parent, Rect region, int? opensInClears, Color rim)
        {
            surface.Tiled(
                parent, "Slats", config.ShutterSlatsSprite, region.center, region.size, PanelTileCells,
                tileIsMeasuredByHeight: false, config.ShutterSlatColor, config.ShutterOrder);
            surface.Sliced(
                parent, "Border", config.RingSprite, region.center, region.size, config.ShutterBorderCells,
                config.ShutterBorderColor, config.ShutterBorderOrder);

            return opensInClears.HasValue ? Badge(parent, region.center, opensInClears.Value, rim) : null;
        }

        /// <summary>
        /// An elevator's lift doors (M9) under <paramref name="root"/>: door
        /// panels over <paramref name="region"/>, a divider down the middle,
        /// and a border.
        /// </summary>
        public ElevatorParts<TPiece> Elevator(TGroup root, Rect region)
        {
            var doors = surface.Tiled(
                root, "Doors", config.DoorPanelSprite, region.center, region.size, PanelTileCells,
                tileIsMeasuredByHeight: false, config.ElevatorDoorColor, config.ElevatorDoorOrder);

            var dividerWidth = config.ElevatorDividerCells;
            var divider = surface.Sliced(
                root, "Divider", config.RoundedRectSprite, region.center, new Vector2(dividerWidth, region.height),
                dividerWidth * 0.5f, config.ElevatorDividerColor, config.ElevatorDividerOrder);
            surface.Sliced(
                root, "Border", config.RingSprite, region.center, region.size, config.ElevatorBorderCells,
                config.ElevatorBorderColor, config.ElevatorBorderOrder);

            return new ElevatorParts<TPiece>(doors, divider);
        }

        /// <summary>
        /// A generator's machine (M6, D48) under <paramref name="root"/>: a
        /// body with a lip hanging below it, a dark screen showing the next
        /// block in miniature (<see cref="Miniature"/>), and the queued count
        /// on a badge.
        /// </summary>
        /// <param name="body">The machine's body, in <paramref name="root"/>'s frame (<see cref="MachinePlacement.Body"/>).</param>
        /// <param name="screen">Its screen (<see cref="MachinePlacement.Screen"/>).</param>
        /// <param name="badgeCenter">Where its badge goes (<see cref="MachinePlacement.BadgeCenter"/>).</param>
        /// <param name="nextFill">The colour the next block is shown in: its own, or ice when it would spawn frozen (M3).</param>
        /// <returns>The badge.</returns>
        public TGroup Machine(
            TGroup root, Rect body, Rect screen, Vector2 badgeCenter, IReadOnlyList<Coord> nextCells, Color nextFill, int queued)
        {
            var corner = config.MachineCornerCells;

            var lip = surface.Group(root, "Lip", new Vector2(0f, -config.LipOffsetCells), 1f);
            surface.Sliced(lip, "Body", config.RoundedRectSprite, body.center, body.size, corner, config.LipFill(config.MachineColor), config.MachineLipOrder);
            surface.Sliced(root, "Body", config.RoundedRectSprite, body.center, body.size, corner, config.MachineColor, config.MachineOrder);
            surface.Sliced(root, "Screen", config.RoundedRectSprite, screen.center, screen.size, corner, config.MachineScreenColor, config.MachineScreenOrder);

            Miniature(root, nextCells, screen, nextFill);
            return Badge(root, badgeCenter, queued, config.BadgeRimColor);
        }

        /// <summary>
        /// The next block on a machine's screen: its lit body as a block on
        /// the board has it, scaled uniformly to fit <paramref name="screen"/>
        /// (<see cref="BoardView.MiniatureFit"/>) and centred on it. Blocks
        /// never rotate, so it shows the block as it will arrive. No studs, no
        /// highlight and no marks: it is a preview of shape and colour.
        /// </summary>
        private void Miniature(TGroup parent, IReadOnlyList<Coord> cells, Rect screen, Color fill)
        {
            var perCell = BoardView.MiniatureFit(cells, screen, out var shapeCenter);
            var tiles = BlockTiling.Compute(cells);

            // A rectangle in the block's cell units, on the screen.
            Rect OnScreen(Rect cellRect) =>
                new Rect(screen.center + (cellRect.min - shapeCenter) * perCell, cellRect.size * perCell);

            var miniature = surface.Group(parent, "Next block", Vector2.zero, 1f);
            surface.Quarters(
                miniature, tiles, tile => OnScreen(BlockTiling.QuarterRect(tile)), QuarterLayer.Lit, fill,
                config.MiniatureOrder);
        }
    }

    /// <summary>The pieces of a block that its owner goes on to animate (<see cref="BoardRecipes{TGroup, TPiece}.Block"/>).</summary>
    /// <typeparam name="TPiece">The surface's drawn sprite.</typeparam>
    internal readonly struct BlockParts<TPiece>
        where TPiece : class
    {
        public BlockParts(List<TPiece> face, List<TPiece> shine, List<TPiece> studs, LayerParts<TPiece> layer)
        {
            Face = face;
            Shine = shine;
            Studs = studs;
            Layer = layer;
        }

        /// <summary>The lit body's quarters, in the block's colour (darkened when layered), in the tiles' order.</summary>
        public List<TPiece> Face { get; }

        /// <summary>The body's highlight pieces, in the tiles' order.</summary>
        public List<TPiece> Shine { get; }

        /// <summary>The tinted per-cell stud sprites, without their gloss; empty for an ice or axis-restricted block.</summary>
        public List<TPiece> Studs { get; }

        /// <summary>A layered block's window (M4, Module 46); null when it shows no colour beneath.</summary>
        public LayerParts<TPiece> Layer { get; }
    }

    /// <summary>The pieces of a layered block's window that a peel animates (<see cref="BoardRecipes{TGroup, TPiece}.Block"/>).</summary>
    /// <typeparam name="TPiece">The surface's drawn sprite.</typeparam>
    internal sealed class LayerParts<TPiece>
        where TPiece : class
    {
        public LayerParts(List<TPiece> edge, List<TPiece> face, List<TPiece> shine, List<TPiece> studs)
        {
            Edge = edge;
            Face = face;
            Shine = shine;
            Studs = studs;
        }

        /// <summary>The window's sunken rim, lit the other way round, in the tiles' order.</summary>
        public List<TPiece> Edge { get; }

        /// <summary>The inner block's lit body, in the colour beneath, in the tiles' order.</summary>
        public List<TPiece> Face { get; }

        /// <summary>The inner block's highlight pieces, in the tiles' order.</summary>
        public List<TPiece> Shine { get; }

        /// <summary>The inner block's studs and their gloss, alternately; empty when none fit or the block carries none.</summary>
        public List<TPiece> Studs { get; }
    }

    /// <summary>The pieces of an elevator that its owner hides while a wave rises (<see cref="BoardRecipes{TGroup, TPiece}.Elevator"/>).</summary>
    /// <typeparam name="TPiece">The surface's drawn sprite.</typeparam>
    internal readonly struct ElevatorParts<TPiece>
        where TPiece : class
    {
        public ElevatorParts(TPiece doors, TPiece divider)
        {
            Doors = doors;
            Divider = divider;
        }

        /// <summary>The door panels over the region.</summary>
        public TPiece Doors { get; }

        /// <summary>The divider down the middle; null when its configured width draws nothing.</summary>
        public TPiece Divider { get; }
    }
}
