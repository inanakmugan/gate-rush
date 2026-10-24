using System;
using System.Collections.Generic;
using System.Globalization;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// How the things on a board are composed from sprites, written once
    /// (Module 28): a block's shadow, lit body, inner shape, studs, ice and axis
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
    /// <para><b>The mechanics' art (Module 46, pass 2).</b> Each mechanic's
    /// sprites are drawn at their painted size — a sprite's pixels per unit
    /// are a cell's pixels, so its rectangle over its pixels per unit is its
    /// size in cells — and the parts that fit any size are 9-sliced at their
    /// painted border or tiled at their painted tile. A part painted in its own
    /// colours is drawn white; a grey one is tinted, its white highlight added
    /// over it.</para>
    /// </remarks>
    /// <typeparam name="TGroup">The surface's group.</typeparam>
    /// <typeparam name="TPiece">The surface's drawn sprite.</typeparam>
    internal sealed class BoardRecipes<TGroup, TPiece>
        where TGroup : class
        where TPiece : class
    {
        /// <summary>A one-digit count: the badge as it is painted. Not a tunable.</summary>
        private const int OneDigitCount = 1;

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
        /// highlight; then the cell's studs with their gloss, or — for an
        /// axis-restricted block (M7) — its raised arrow along its axis
        /// instead; and, when it is ice, the ice over all of it and its frost.
        /// </summary>
        /// <remarks>
        /// <para><b>Ice (M3, Module 46, pass 2).</b> A frozen block is drawn in
        /// the ice colour, studs and all — its real colour stays hidden — and
        /// over it lies the ice, a placed quarter set on the block's own tiles
        /// (<see cref="QuarterLayer.Ice"/>), so it follows any shape without a
        /// seam, and one sprite of frost streaks at its top row's leftmost
        /// cell (<see cref="Frost"/>).</para>
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
            var hasStuds = axis == MovementAxis.Free;
            if (hasStuds)
            {
                for (var c = 0; c < cells.Count; c++)
                {
                    var cell = cells[c];
                    var center = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
                    studs.Add(surface.Sprite(
                        face, $"Studs {cell}", config.StudsSprite, center, Vector2.one, outerFill, config.StudOrder, 0f, SpriteLook.Lit));
                    surface.Sprite(
                        face, $"Gloss {cell}", config.StudGlossSprite, center, Vector2.one, config.GlossColor, config.GlossOrder, 0f,
                        SpriteLook.Plain);
                }
            }

            LayerParts<TPiece> layer = null;
            if (beneath.HasValue)
            {
                layer = Window(face, cells, tiles, fill, beneath.Value, hasStuds);
            }

            if (axis != MovementAxis.Free)
            {
                var isLayered = beneath.HasValue;
                AxisArrow(
                    face, cells, axis, fill, isLayered ? config.LayerStudOrder : config.StudOrder,
                    isLayered ? config.LayerGlossOrder : config.GlossOrder);
            }

            if (isIce)
            {
                surface.Quarters(face, tiles, BlockTiling.QuarterRect, QuarterLayer.Ice, Color.white, config.IceOrder);
                Frost(face, cells);
            }

            return new BlockParts<TPiece>(faceQuarters, shineQuarters, studs, layer);
        }

        /// <summary>
        /// A frozen block's frost streaks: one sprite with its top-left corner
        /// on the top-left corner of the block's top row's leftmost cell, two
        /// cells wide when the cell to that one's right is the block's.
        /// </summary>
        private void Frost(TGroup face, IReadOnlyList<Coord> cells)
        {
            var anchor = cells[0];
            for (var c = 1; c < cells.Count; c++)
            {
                var cell = cells[c];
                if (cell.Y > anchor.Y || (cell.Y == anchor.Y && cell.X < anchor.X))
                {
                    anchor = cell;
                }
            }

            var isWide = false;
            for (var c = 0; c < cells.Count; c++)
            {
                isWide |= cells[c].X == anchor.X + 1 && cells[c].Y == anchor.Y;
            }

            var sprite = isWide ? config.IceStreaksWideSprite : config.IceStreaksSprite;
            var size = NaturalSize(sprite);
            var center = new Vector2(anchor.X + size.x * 0.5f, anchor.Y + 1f - size.y * 0.5f);
            surface.Sprite(face, "Frost", sprite, center, size, config.IceStreakColor, config.IceStreakOrder, 0f, SpriteLook.Plain);
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
                    innerStuds.Add(surface.Sprite(
                        inner, $"Inner stud {i}", config.SingleStudSprite, centers[i], Vector2.one, beneath,
                        config.LayerStudOrder, 0f, SpriteLook.Lit));
                    innerStuds.Add(surface.Sprite(
                        inner, $"Inner gloss {i}", config.SingleStudGlossSprite, centers[i], Vector2.one, config.GlossColor,
                        config.LayerGlossOrder, 0f, SpriteLook.Plain));
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
        /// The M7 arrow (Module 46, pass 2): a raised double-headed arrow in
        /// <paramref name="fill"/>, the block's colour, with its highlight,
        /// through the centre of the footprint's bounding box along the
        /// block's axis, stopping <see cref="RuntimeConfig.OneWayArrowEndInsetCells"/>
        /// short of each end (<see cref="MarkLayout.AxisArrowRect"/>). A
        /// vertical arrow is its own sprite, never turned, so its light falls
        /// from the top left. Only its sliced middle stretches; on a block too
        /// short for both heads at their painted size, the whole arrow shrinks.
        /// </summary>
        private void AxisArrow(TGroup face, IReadOnlyList<Coord> cells, MovementAxis axis, Color fill, int order, int highlightOrder)
        {
            var isVertical = axis == MovementAxis.VerticalOnly;
            var sprite = isVertical ? config.OneWayArrowVerticalSprite : config.OneWayArrowSprite;
            var highlight = isVertical ? config.OneWayArrowVerticalHighlightSprite : config.OneWayArrowHighlightSprite;
            var natural = NaturalSize(sprite);
            var thickness = isVertical ? natural.x : natural.y;
            var arrow = MarkLayout.AxisArrowRect(cells, axis, thickness, config.OneWayArrowEndInsetCells);
            var length = isVertical ? arrow.height : arrow.width;

            // The heads at their painted size need the borders along the
            // length; a shorter arrow keeps its shape, smaller.
            var border = sprite.border;
            var heads = (isVertical ? border.y + border.w : border.x + border.z) / sprite.pixelsPerUnit;
            if (length < heads)
            {
                thickness *= length / heads;
            }

            surface.Strip(face, "Axis arrow", sprite, arrow.center, length, thickness, isVertical, fill, order, SpriteLook.Lit);
            surface.Strip(
                face, "Axis arrow highlight", highlight, arrow.center, length, thickness, isVertical, Color.white, highlightOrder,
                SpriteLook.Highlight);
        }

        /// <summary>
        /// The one count badge (D48, Module 46, pass 2): a raised rim in
        /// <paramref name="rim"/> with its shadow, a raised face in the badge
        /// colour inside it, their highlight, and <paramref name="value"/> in
        /// white with its soft drop shadow on top. Sliced along its width, it
        /// widens by a digit's width per digit past the first
        /// (<see cref="MarkLayout.BadgeWidth"/>).
        /// </summary>
        /// <returns>The badge's group, centred on <paramref name="center"/>: scaling it pops the badge.</returns>
        public TGroup Badge(TGroup parent, Vector2 center, int value, Color rim)
        {
            var natural = NaturalSize(config.BadgeRimSprite);
            var size = new Vector2(natural.x + config.BadgeWidthCells(value) - config.BadgeWidthCells(OneDigitCount), natural.y);
            var border = SliceBorder(config.BadgeRimSprite);

            var badge = surface.Group(parent, "Badge", center, 1f);
            surface.Sliced(badge, "Rim", config.BadgeRimSprite, Vector2.zero, size, border, rim, config.BadgeRimOrder, SpriteLook.Lit);
            surface.Sliced(
                badge, "Fill", config.BadgeFaceSprite, Vector2.zero, size, border, config.CountBadgeColor, config.BadgeOrder,
                SpriteLook.Lit);
            surface.Sliced(
                badge, "Highlight", config.BadgeHighlightSprite, Vector2.zero, size, border, Color.white, config.BadgeHighlightOrder,
                SpriteLook.Highlight);
            surface.Label(
                badge, "Count", Vector2.zero, value.ToString(CultureInfo.InvariantCulture), config.CountFontSize,
                config.CountTextColor, true);
            return badge;
        }

        /// <summary>
        /// A lock (M8) under <paramref name="parent"/>, whose frame is a block
        /// body's (Module 46, pass 2): silver tube links along each row of
        /// <paramref name="cells"/> (<see cref="MarkLayout.ChainStrips"/>), as
        /// many whole periods as fit each row, stretched a little to fit it;
        /// and the gold padlock centred on <paramref name="icon"/> at
        /// <paramref name="scale"/>, with <paramref name="keysStillRequired"/>
        /// in white on its body, or nothing written when that is null. The one
        /// drawing of a lock, for a locked block, for the lock that fades away
        /// when it opens, and for the Lock &amp; Key card.
        /// </summary>
        /// <returns>The count's group, or null when none was written.</returns>
        public TGroup Lock(TGroup parent, IReadOnlyList<Coord> cells, Vector2 icon, float scale, int? keysStillRequired)
        {
            var chains = surface.Group(parent, "Chains", Vector2.zero, 1f);
            var period = NaturalSize(config.ChainSprite);
            var strips = MarkLayout.ChainStrips(cells, period.y, config.ChainEndInsetCells);
            for (var s = 0; s < strips.Count; s++)
            {
                var strip = strips[s];
                var count = Mathf.Max(1, Mathf.RoundToInt(strip.width / period.x));
                var length = strip.width / count;
                var size = new Vector2(length, period.y * length / period.x);
                for (var k = 0; k < count; k++)
                {
                    surface.Sprite(
                        chains, $"Chain {s}.{k}", config.ChainSprite, new Vector2(strip.xMin + (k + 0.5f) * length, strip.center.y), size,
                        Color.white, config.ChainOrder, 0f, SpriteLook.Lit);
                }
            }

            var mark = surface.Group(parent, "Padlock", icon, scale);
            surface.Sprite(
                mark, "Body", config.LockSprite, Vector2.zero, NaturalSize(config.LockSprite), Color.white, config.IconOrder, 0f,
                SpriteLook.Lit);
            if (!keysStillRequired.HasValue)
            {
                return null;
            }

            var keys = surface.Group(mark, "Count", new Vector2(0f, -config.PadlockCountDropCells), 1f);
            surface.Label(
                keys, "Keys", Vector2.zero, keysStillRequired.Value.ToString(CultureInfo.InvariantCulture), config.PadlockCountFontSize,
                config.CountTextColor, true);
            return keys;
        }

        /// <summary>
        /// A key (M8) under a block's body, centred on <paramref name="icon"/>
        /// at <paramref name="scale"/>: the gold key, lying flat so its light
        /// falls from the top left, then its gem in <paramref name="gem"/>, the
        /// colour of the lock it opens (D47), and the gem's highlight.
        /// </summary>
        public void Key(TGroup parent, Vector2 icon, float scale, Color gem)
        {
            var key = surface.Group(parent, "Key", icon, scale);
            var size = NaturalSize(config.GoldKeySprite);
            surface.Sprite(key, "Body", config.GoldKeySprite, Vector2.zero, size, Color.white, config.IconOrder, 0f, SpriteLook.Lit);
            surface.Sprite(key, "Gem", config.GemSprite, Vector2.zero, size, gem, config.KeyGemOrder, 0f, SpriteLook.Lit);
            surface.Sprite(
                key, "Gem highlight", config.GemHighlightSprite, Vector2.zero, size, Color.white, config.KeyGemHighlightOrder, 0f,
                SpriteLook.Highlight);
        }

        /// <summary>
        /// A time-bonus block's mark (M10) under a block's body, on
        /// <paramref name="center"/> at <paramref name="scale"/> (Module 46,
        /// pass 2, option D, revision B): the green-rimmed clock coin and,
        /// past a small gap, the seconds through
        /// <see cref="RuntimeConfig.TimeBonusMarkText"/> at the count digits'
        /// size, white with their dark outline and soft shadow — together
        /// centred on the anchor, so the coin moves with the text
        /// (<see cref="RuntimeConfig.TimeBonusCoinCenterCells"/>). Nothing is
        /// drawn behind either; the caller fits the whole group with
        /// <paramref name="scale"/> (<see cref="RuntimeConfig.TimeBonusMarkScale"/>).
        /// </summary>
        public void TimeBonusMark(TGroup parent, Vector2 center, float scale, int seconds)
        {
            var text = config.TimeBonusMarkText(seconds);
            var mark = surface.Group(parent, "Time bonus", center, scale);
            surface.Sprite(
                mark, "Coin", config.BonusCoinSprite, new Vector2(config.TimeBonusCoinCenterCells(text), 0f),
                NaturalSize(config.BonusCoinSprite), Color.white, config.IconOrder, 0f, SpriteLook.Lit);
            surface.OutlinedLabel(
                mark, "Seconds", new Vector2(config.TimeBonusLabelStartCells(text), -config.TimeBonusLabelDropCells),
                text, config.CountFontSize, config.CountTextColor);
        }

        /// <summary>
        /// A gate under <paramref name="root"/>: its own segment of the frame,
        /// a lit body like a block's with its highlight and its shadow on the
        /// floor (Module 46). Open, it is in <paramref name="openColor"/> with a
        /// white arrow outlined in the outline colour, pointing out of the
        /// board. Closed (M2) — <paramref name="openColor"/> null — it is in
        /// the ice colour under the ice (Module 46, pass 2), with
        /// <paramref name="opensInClears"/> on a badge at its middle, and no
        /// arrow.
        /// </summary>
        /// <param name="tiles">The quarters of the gate's span on the frame (<see cref="FrameTiling.GateCells"/>).</param>
        /// <param name="rectOf">Where a quarter goes, in <paramref name="root"/>'s frame.</param>
        /// <param name="span">The gate's whole span on the frame, in <paramref name="root"/>'s frame.</param>
        /// <param name="opensInClears">The clears still needed; read only for a closed gate.</param>
        /// <returns>The badge of a closed gate; null for an open one.</returns>
        public TGroup Gate(
            TGroup root, GateDefinition gate, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, Rect span,
            BlockColor? openColor, int opensInClears)
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
                    config.GateArrowOutlineOrder, turn, SpriteLook.Plain);
                surface.Sprite(
                    root, "Arrow", config.GateArrowSprite, span.center, arrow, config.GateArrowColor, config.GateMarkOrder, turn,
                    SpriteLook.Plain);
                return null;
            }

            surface.Quarters(root, tiles, rectOf, QuarterLayer.Ice, Color.white, config.GateIceOrder);
            return Badge(root, span.center, opensInClears, config.CountBadgeRimColor);
        }

        /// <summary>
        /// A closed shutter's panel (M5) under <paramref name="parent"/>
        /// (Module 46, pass 2): cylinder slats tiled over the region inside its
        /// rim, the raised gold rim round the region, 9-sliced to its size with
        /// its shadow and the recess under it, and — when
        /// <paramref name="opensInClears"/> is given — the clears still needed
        /// on a badge at its centre, rimmed in <paramref name="rim"/>: the
        /// colour a colour-bound shutter counts, as before. The one drawing of
        /// a shutter, for a closed one, for the panel that lifts off when it
        /// opens, and for the Shutter card.
        /// </summary>
        /// <returns>The badge, or null when none was drawn.</returns>
        public TGroup ShutterPanel(TGroup parent, Rect region, int? opensInClears, Color rim)
        {
            var inset = config.ShutterInnerInsetCells;
            var inner = new Rect(region.x + inset, region.y + inset, region.width - 2f * inset, region.height - 2f * inset);
            surface.Tiled(
                parent, "Slats", config.ShutterCylindersSprite, inner.center, inner.size, NaturalSize(config.ShutterCylindersSprite).y,
                tileIsMeasuredByHeight: true, Color.white, config.ShutterOrder, SpriteLook.Lit);
            surface.Sliced(
                parent, "Border", config.ShutterRimSprite, region.center, Grown(region.size, config.RaisedShadowMarginCells),
                SliceBorder(config.ShutterRimSprite), Color.white, config.ShutterBorderOrder, SpriteLook.Lit);

            return opensInClears.HasValue ? Badge(parent, region.center, opensInClears.Value, rim) : null;
        }

        /// <summary>
        /// An elevator (M9) under <paramref name="root"/> (Module 46, pass 2):
        /// the raised orange rim round <paramref name="region"/> with its shadow
        /// and the dark recess inside it, a hazard strip tiled down each side
        /// of the rim, and in a <c>Doors</c> group the two doors standing in
        /// the recess, each with its handle by the seam
        /// (<see cref="BoardView.ElevatorDoorRects"/>).
        /// </summary>
        public ElevatorParts<TGroup> Elevator(TGroup root, Rect region)
        {
            surface.Sliced(
                root, "Border", config.ElevatorRimSprite, region.center, Grown(region.size, config.RaisedShadowMarginCells),
                SliceBorder(config.ElevatorRimSprite), Color.white, config.ElevatorRimOrder, SpriteLook.Lit);

            var strip = NaturalSize(config.ElevatorHazardLeftSprite);
            var length = region.height - 2f * config.ElevatorHazardEndCells;
            if (length > 0f)
            {
                var size = new Vector2(strip.x, length);
                var reach = config.ElevatorHazardCentreCells;
                surface.Tiled(
                    root, "Hazard left", config.ElevatorHazardLeftSprite, new Vector2(region.xMin + reach, region.center.y), size, strip.y,
                    tileIsMeasuredByHeight: true, Color.white, config.ElevatorHazardOrder, SpriteLook.Lit);
                surface.Tiled(
                    root, "Hazard right", config.ElevatorHazardRightSprite, new Vector2(region.xMax - reach, region.center.y), size,
                    strip.y, tileIsMeasuredByHeight: true, Color.white, config.ElevatorHazardOrder, SpriteLook.Lit);
            }

            var doors = surface.Group(root, "Doors", Vector2.zero, 1f);
            var (left, right) = BoardView.ElevatorDoorRects(config, region);
            Door(doors, "Left door", left);
            Door(doors, "Right door", right);
            Handle(doors, "Left handle", new Vector2(region.center.x - config.ElevatorHandleOffsetCells, region.center.y));
            Handle(doors, "Right handle", new Vector2(region.center.x + config.ElevatorHandleOffsetCells, region.center.y));
            return new ElevatorParts<TGroup>(doors);
        }

        /// <summary>One elevator door over <paramref name="door"/>, 9-sliced, its sprite reaching past it for its shadow.</summary>
        public TPiece Door(TGroup parent, string name, Rect door) =>
            surface.Sliced(
                parent, name, config.ElevatorDoorSprite, door.center, Grown(door.size, config.ElevatorDoorShadowMarginCells),
                SliceBorder(config.ElevatorDoorSprite), Color.white, config.ElevatorDoorPanelOrder, SpriteLook.Lit);

        /// <summary>One door handle centred on <paramref name="center"/>.</summary>
        public TPiece Handle(TGroup parent, string name, Vector2 center) =>
            surface.Sprite(
                parent, name, config.ElevatorHandleSprite, center, NaturalSize(config.ElevatorHandleSprite), Color.white,
                config.ElevatorHandleOrder, 0f, SpriteLook.Lit);

        /// <summary>
        /// A generator's machine (M6, D48) under <paramref name="root"/>
        /// (Module 46, pass 2): the raised metal body with its rivets, 9-sliced
        /// to <paramref name="body"/>; the recessed glass screen over
        /// <paramref name="screen"/>, showing the next block lit with its studs
        /// and, at its end, the queued count; and the vent and the button in
        /// the room left on the body's outer side.
        /// </summary>
        /// <param name="body">The machine's body, in <paramref name="root"/>'s frame (<see cref="MachinePlacement.Body"/>).</param>
        /// <param name="screen">Its screen (<see cref="MachinePlacement.Screen"/>).</param>
        /// <param name="nextFill">The colour the next block is shown in: its own, or ice when it would spawn frozen (M3).</param>
        /// <returns>The count's group: scaling it pops the count.</returns>
        public TGroup Machine(TGroup root, Rect body, Rect screen, IReadOnlyList<Coord> nextCells, Color nextFill, int queued)
        {
            surface.Sliced(
                root, "Body", config.MachineBodySprite, body.center, Grown(body.size, config.RaisedShadowMarginCells),
                SliceBorder(config.MachineBodySprite), Color.white, config.MachineOrder, SpriteLook.Lit);
            surface.Sliced(
                root, "Screen", config.MachineScreenSprite, screen.center, screen.size, SliceBorder(config.MachineScreenSprite),
                Color.white, config.MachineScreenOrder, SpriteLook.Lit);

            var room = OuterRoom(body, screen);
            var along = room.width >= room.height ? Vector2.right : Vector2.up;
            var offsets = config.MachineFittingOffsetsCells;
            surface.Sprite(
                root, "Vent", config.MachineVentSprite, room.center + along * offsets.x, NaturalSize(config.MachineVentSprite),
                Color.white, config.MachineFittingsOrder, 0f, SpriteLook.Lit);
            surface.Sprite(
                root, "Button", config.MachineButtonSprite, room.center + along * offsets.y, NaturalSize(config.MachineButtonSprite),
                Color.white, config.MachineFittingsOrder, 0f, SpriteLook.Lit);

            var (blockArea, countArea) = BoardView.SplitScreen(screen, config.MachineScreenCountShare, config.MachineMiniatureFillShare);
            Miniature(root, nextCells, blockArea, nextFill);

            var count = surface.Group(root, "Badge", countArea.center, 1f);
            surface.Label(
                count, "Count", Vector2.zero, queued.ToString(CultureInfo.InvariantCulture), config.MachineCountFontSize,
                config.MachineCountColor, false);
            return count;
        }

        /// <summary>
        /// The room a machine's <paramref name="body"/> leaves beyond its
        /// <paramref name="screen"/> on its widest side, the outer one
        /// (<see cref="GeneratorMachine.Place"/> keeps it there).
        /// </summary>
        private static Rect OuterRoom(Rect body, Rect screen)
        {
            var below = screen.yMin - body.yMin;
            var above = body.yMax - screen.yMax;
            var left = screen.xMin - body.xMin;
            var right = body.xMax - screen.xMax;
            var most = Mathf.Max(Mathf.Max(below, above), Mathf.Max(left, right));
            if (most == below)
            {
                return Rect.MinMaxRect(body.xMin, body.yMin, body.xMax, screen.yMin);
            }

            if (most == above)
            {
                return Rect.MinMaxRect(body.xMin, screen.yMax, body.xMax, body.yMax);
            }

            return most == left
                ? Rect.MinMaxRect(body.xMin, body.yMin, screen.xMin, body.yMax)
                : Rect.MinMaxRect(screen.xMax, body.yMin, body.xMax, body.yMax);
        }

        /// <summary>
        /// The next block on a machine's screen: its lit body as a block on
        /// the board has it, with its studs and their gloss, scaled uniformly
        /// to fit <paramref name="area"/> (<see cref="BoardView.MiniatureFit"/>)
        /// and centred on it. Blocks never rotate, so it shows the block as it
        /// will arrive. No highlight and no marks: it is a preview of shape
        /// and colour.
        /// </summary>
        private void Miniature(TGroup parent, IReadOnlyList<Coord> cells, Rect area, Color fill)
        {
            var perCell = BoardView.MiniatureFit(cells, area, out var shapeCenter);
            var tiles = BlockTiling.Compute(cells);

            // A rectangle in the block's cell units, on the screen.
            Rect OnScreen(Rect cellRect) =>
                new Rect(area.center + (cellRect.min - shapeCenter) * perCell, cellRect.size * perCell);

            var miniature = surface.Group(parent, "Next block", Vector2.zero, 1f);
            surface.Quarters(
                miniature, tiles, tile => OnScreen(BlockTiling.QuarterRect(tile)), QuarterLayer.Lit, fill,
                config.MiniatureOrder);

            var stud = new Vector2(perCell, perCell);
            for (var c = 0; c < cells.Count; c++)
            {
                var center = OnScreen(new Rect(cells[c].X, cells[c].Y, 1f, 1f)).center;
                surface.Sprite(
                    miniature, $"Studs {cells[c]}", config.StudsSprite, center, stud, fill, config.MiniatureStudOrder, 0f, SpriteLook.Lit);
                surface.Sprite(
                    miniature, $"Gloss {cells[c]}", config.StudGlossSprite, center, stud, config.GlossColor, config.MiniatureGlossOrder,
                    0f, SpriteLook.Plain);
            }
        }

        /// <summary>A generated sprite's painted size, in cells: its pixels per unit are a cell's pixels.</summary>
        private static Vector2 NaturalSize(Sprite sprite) => new Vector2(sprite.rect.width, sprite.rect.height) / sprite.pixelsPerUnit;

        /// <summary>A 9-sliced generated sprite's left border at its painted size, in cells.</summary>
        private static float SliceBorder(Sprite sprite) => sprite.border.x / sprite.pixelsPerUnit;

        /// <summary><paramref name="size"/> with <paramref name="margin"/> added on every side: a sprite reaching past its part for its shadow.</summary>
        private static Vector2 Grown(Vector2 size, float margin) => size + Vector2.one * (2f * margin);
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

        /// <summary>The tinted per-cell stud sprites, without their gloss; empty for an axis-restricted block.</summary>
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

    /// <summary>The part of an elevator that its owner hides while a wave rises (<see cref="BoardRecipes{TGroup, TPiece}.Elevator"/>).</summary>
    /// <typeparam name="TGroup">The surface's group.</typeparam>
    internal readonly struct ElevatorParts<TGroup>
        where TGroup : class
    {
        public ElevatorParts(TGroup doors)
        {
            Doors = doors;
        }

        /// <summary>The doors and their handles, in one group.</summary>
        public TGroup Doors { get; }
    }
}
