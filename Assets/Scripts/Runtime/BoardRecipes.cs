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
        /// corner of the footprint's origin cell: a <c>Lip</c> group filling
        /// the footprint's quarters in the darker lip colour, and a
        /// <c>Face</c> group with the quarters in <paramref name="fill"/>,
        /// raised on their downward sides so the lip shows beneath
        /// (<see cref="FaceShape"/>); for a layered block (M4) its inner shape
        /// in <paramref name="beneath"/> — the same tiles pulled inward by
        /// <see cref="RuntimeConfig.LayerInsetCells"/>, over a copy in the lip
        /// colour reaching <see cref="RuntimeConfig.LayerEdgeCells"/> further
        /// out, its edge; then frost on every cell when it is ice, studs with
        /// their gloss on every cell when it is not, and — for an
        /// axis-restricted block (M7), ice or not — one double-headed arrow
        /// along its axis instead of studs. Studs and the arrow are raised by
        /// <see cref="FaceShape.ContentRise"/>, which keeps them on the
        /// shortened face; frost stays on its cell, clear of the lip by its
        /// own inset. A layered block's studs take the colour beneath.
        /// </summary>
        /// <remarks>
        /// Studs and gloss are drawn at full size, whatever the block. A
        /// layered block's are to be scaled about their own centres by
        /// <see cref="BlockParts{TPiece}.StudScale"/>, which the caller does
        /// in its own medium: the board records each one's full size first, so
        /// a peel can grow them back.
        /// </remarks>
        /// <param name="tiles"><see cref="BlockTiling.Compute"/> of <paramref name="cells"/>.</param>
        public BlockParts<TPiece> Block(
            TGroup body, IReadOnlyList<Coord> cells, IReadOnlyList<QuarterTile> tiles, Color fill, Color? beneath,
            bool isIce, MovementAxis axis)
        {
            var lipCells = config.LipOffsetCells;

            var lip = surface.Group(body, "Lip", Vector2.zero, 1f);
            var lipQuarters = surface.Quarters(lip, tiles, BlockTiling.QuarterRect, config.LipFill(fill), config.BlockLipOrder);

            var face = surface.Group(body, "Face", Vector2.zero, 1f);
            var faceQuarters = surface.Quarters(
                face, tiles, tile => FaceShape.QuarterRect(tile, 0f, lipCells), fill, config.BlockOrder);

            List<TPiece> layerEdgeQuarters = null;
            List<TPiece> layerQuarters = null;
            var studFill = fill;
            var studScale = 1f;
            if (beneath.HasValue)
            {
                var inset = config.LayerInsetCells;
                var edgeInset = inset - config.LayerEdgeCells;
                var inner = surface.Group(face, "Inner", Vector2.zero, 1f);
                layerEdgeQuarters = surface.Quarters(
                    inner, tiles, tile => FaceShape.QuarterRect(tile, edgeInset, lipCells), config.LipFill(beneath.Value),
                    config.LayerEdgeOrder);
                layerQuarters = surface.Quarters(
                    inner, tiles, tile => FaceShape.QuarterRect(tile, inset, lipCells), beneath.Value, config.LayerOrder);
                studFill = beneath.Value;
                studScale = config.LayerStudScale;
            }

            var studs = new List<TPiece>();
            var rise = new Vector2(0f, FaceShape.ContentRise(lipCells));
            for (var c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                var center = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
                if (isIce)
                {
                    surface.Sprite(face, $"Frost {cell}", config.FrostSprite, center, Vector2.one, config.FrostColor, config.StudOrder, 0f);
                }
                else if (axis == MovementAxis.Free)
                {
                    studs.Add(surface.Sprite(
                        face, $"Studs {cell}", config.StudsSprite, center + rise, Vector2.one, studFill, config.StudOrder, 0f));
                    studs.Add(surface.Sprite(
                        face, $"Gloss {cell}", config.StudGlossSprite, center + rise, Vector2.one, config.GlossColor,
                        config.GlossOrder, 0f));
                }
            }

            if (axis != MovementAxis.Free)
            {
                AxisArrow(face, cells, axis, rise);
            }

            return new BlockParts<TPiece>(lipQuarters, faceQuarters, layerEdgeQuarters, layerQuarters, studs, studScale);
        }

        /// <summary>
        /// The M7 arrow: a 9-sliced sprite through the centre of the
        /// footprint's bounding box, raised with the studs, along the block's
        /// axis, stopping <see cref="RuntimeConfig.AxisArrowEndInsetCells"/>
        /// short of each end (<see cref="MarkLayout.AxisArrowRect"/>, the rule
        /// the Level Editor shares). Its thickness is the sprite's whole
        /// height and only its sliced middle stretches, so the heads keep
        /// their shape at any length.
        /// </summary>
        private void AxisArrow(TGroup face, IReadOnlyList<Coord> cells, MovementAxis axis, Vector2 rise)
        {
            var isHorizontal = axis == MovementAxis.HorizontalOnly;
            var arrow = MarkLayout.AxisArrowRect(cells, axis, config.AxisArrowThicknessCells, config.AxisArrowEndInsetCells);

            surface.Strip(
                face, "Axis arrow", config.AxisArrowSprite, arrow.center + rise,
                isHorizontal ? arrow.width : arrow.height, isHorizontal ? arrow.height : arrow.width,
                config.AxisArrowColor, config.GlossOrder,
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
        /// with a lip hanging below its face. Open, it is in
        /// <paramref name="openColor"/> with a white arrow pointing out of the
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
            var lip = surface.Group(root, "Lip", new Vector2(0f, -config.LipOffsetCells), 1f);
            surface.Quarters(lip, tiles, rectOf, config.LipFill(fill), config.FrameLipOrder);
            surface.Quarters(root, tiles, rectOf, fill, config.FrameOrder);

            if (openColor.HasValue)
            {
                var arrow = config.GateArrowSizeCells;
                surface.Sprite(
                    root, "Arrow", config.GateArrowSprite, span.center, new Vector2(arrow, arrow),
                    config.GateArrowColor, config.GateMarkOrder,
                    Vector2.SignedAngle(Vector2.up, GateExit.Outward(gate.Edge)));
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
        /// The next block on a machine's screen: its lip and face as a block
        /// on the board has them (<see cref="FaceShape"/>), scaled uniformly to
        /// fit <paramref name="screen"/> (<see cref="BoardView.MiniatureFit"/>)
        /// and centred on it. Blocks never rotate, so it shows the block as it
        /// will arrive. No studs and no marks: it is a preview of shape and
        /// colour.
        /// </summary>
        private void Miniature(TGroup parent, IReadOnlyList<Coord> cells, Rect screen, Color fill)
        {
            var perCell = BoardView.MiniatureFit(cells, screen, out var shapeCenter);
            var tiles = BlockTiling.Compute(cells);
            var lipCells = config.LipOffsetCells;

            // A rectangle in the block's cell units, on the screen.
            Rect OnScreen(Rect cellRect) =>
                new Rect(screen.center + (cellRect.min - shapeCenter) * perCell, cellRect.size * perCell);

            var miniature = surface.Group(parent, "Next block", Vector2.zero, 1f);
            var lip = surface.Group(miniature, "Lip", Vector2.zero, 1f);
            surface.Quarters(
                lip, tiles, tile => OnScreen(BlockTiling.QuarterRect(tile)), config.LipFill(fill), config.MiniatureLipOrder);
            surface.Quarters(
                miniature, tiles, tile => OnScreen(FaceShape.QuarterRect(tile, 0f, lipCells)), fill, config.MiniatureOrder);
        }
    }

    /// <summary>The pieces of a block that its owner goes on to animate (<see cref="BoardRecipes{TGroup, TPiece}.Block"/>).</summary>
    /// <typeparam name="TPiece">The surface's drawn sprite.</typeparam>
    internal readonly struct BlockParts<TPiece>
        where TPiece : class
    {
        public BlockParts(
            List<TPiece> lip, List<TPiece> face, List<TPiece> layerEdge, List<TPiece> layer, List<TPiece> studs, float studScale)
        {
            Lip = lip;
            Face = face;
            LayerEdge = layerEdge;
            Layer = layer;
            Studs = studs;
            StudScale = studScale;
        }

        /// <summary>The lip's quarters, filling the footprint under the face, in the tiles' order.</summary>
        public List<TPiece> Lip { get; }

        /// <summary>The face's quarters, in the block's colour, in the tiles' order.</summary>
        public List<TPiece> Face { get; }

        /// <summary>The quarters of a layered block's inner shape's darker edge, in the tiles' order; null when it shows no colour beneath.</summary>
        public List<TPiece> LayerEdge { get; }

        /// <summary>The quarters of a layered block's inner shape (M4), in the tiles' order; null when it shows no colour beneath.</summary>
        public List<TPiece> Layer { get; }

        /// <summary>Every stud sprite and its gloss, cell by cell, drawn at full size; empty for an ice or axis-restricted block.</summary>
        public List<TPiece> Studs { get; }

        /// <summary>
        /// The scale <see cref="Studs"/> are to be shown at, about their own
        /// centres: <see cref="RuntimeConfig.LayerStudScale"/> on a layered
        /// block, whose studs sit on its smaller inner shape, and 1 otherwise.
        /// </summary>
        public float StudScale { get; }
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
