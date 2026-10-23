using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// Draws a mechanic on the canvas for its introduction card (Module 19),
    /// as it looks on the board: the same generated sprites, composed by the
    /// board's own recipes (<see cref="BoardRecipes{TGroup, TPiece}"/>), placed
    /// by the same layout rules — <see cref="FrameTiling"/>,
    /// <see cref="GeneratorMachine"/>, <see cref="MarkLayout"/> — and sized by
    /// the board's own cell-relative values in <see cref="RuntimeConfig"/>,
    /// times <see cref="RuntimeConfig.IntroCellUnits"/>. Retuning the board
    /// retunes the cards.
    /// </summary>
    /// <remarks>
    /// <para><b>One recipe, two surfaces (Module 28).</b> The canvas is a
    /// screen-space overlay, where a <c>SpriteRenderer</c> cannot draw, so
    /// every piece here is a uGUI <see cref="Image"/>. What a block, a mark, a
    /// gate, a shutter's panel, a generator's machine or an elevator's doors
    /// is made of is not written here: this class is the recipes' surface
    /// (<see cref="IRecipeSurface{TGroup, TPiece}"/>) and only knows how to
    /// put one piece on the canvas. What it does decide is each card's scene:
    /// which things it shows, where, and what the scene covers.</para>
    /// <para><b>Order.</b> uGUI draws in hierarchy order and has no sorting
    /// order, so the order a recipe gives a piece is ignored here; the
    /// recipes add their pieces from the bottom up, which is what this
    /// surface draws.</para>
    /// <para><b>Units.</b> Every position and size in this class is in cells,
    /// in the scene's own frame; <see cref="AddRect"/> alone converts to canvas
    /// units. Nothing here is a raycast target: a tap on an illustration falls
    /// through to the card's backdrop.</para>
    /// </remarks>
    public sealed class MechanicIllustration : IRecipeSurface<RectTransform, Image>
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
        private readonly BoardRecipes<RectTransform, Image> recipes;

        /// <summary>An illustrator for one canvas.</summary>
        /// <param name="config">The board's and the card's values.</param>
        /// <param name="referencePixelsPerUnit">The canvas's reference pixels per unit, which sliced and tiled images are sized against.</param>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public MechanicIllustration(RuntimeConfig config, float referencePixelsPerUnit)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.referencePixelsPerUnit = referencePixelsPerUnit;
            unitsPerCell = config.IntroCellUnits;
            recipes = new BoardRecipes<RectTransform, Image>(config, this);
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
                    var body = DrawBlock(root, "Ice block", Vector2.zero, Horizontal2, config.IceColor, isIce: true);
                    var corner = MarkLayout.CornerBadge(Horizontal2, config.BlockBadgeInsetCells);
                    recipes.Badge(body, corner, config.IntroCount, config.CountBadgeRimColor);

                    // The badge overlaps the block's edge at its corner, so
                    // the card frames it too.
                    var bounds = BlockBounds(Vector2.zero, Horizontal2);
                    var halfBadge = new Vector2(config.BadgeWidthCells(config.IntroCount), config.BadgeHeightCells) * 0.5f;
                    return Rect.MinMaxRect(
                        bounds.xMin, Mathf.Min(bounds.yMin, corner.y - halfBadge.y),
                        Mathf.Max(bounds.xMax, corner.x + halfBadge.x), bounds.yMax);
                }

                case LevelMechanic.IceDoor:
                    return DrawIceDoor(root);

                case LevelMechanic.LayeredBlock:
                {
                    DrawBlock(
                        root, "Layered block", Vector2.zero, Horizontal2, block,
                        beneath: config.BlockFill(config.IntroSecondColor));
                    return BlockBounds(Vector2.zero, Horizontal2);
                }

                case LevelMechanic.OneWayBlock:
                    DrawBlock(root, "One-way block", Vector2.zero, Horizontal2, block, axis: MovementAxis.HorizontalOnly);
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
                    var body = DrawBlock(root, "Time-bonus block", Vector2.zero, Horizontal2, block);
                    recipes.TimeBonusMark(body, MarkLayout.Anchor(Horizontal2), 1f, config.IntroBonusSeconds);
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

            DrawBlock(root, "Block", Vector2.zero, Single, config.BlockFill(config.IntroBlockColor));

            var arrow = config.IntroHowToPlayArrowCells;
            AddSprite(
                root, "Arrow", config.GateArrowSprite, CellCenter(new Coord(boardWidth - 1, 0)), new Vector2(arrow, arrow),
                config.GateArrowColor, Vector2.SignedAngle(Vector2.up, GateExit.Outward(gate.Edge)));

            DrawGate(root, ctx, isOpen: true);

            return Rect.MinMaxRect(0f, 0f, boardWidth + config.FrameThicknessCells, 1f);
        }

        /// <summary>A closed gate (M2) on the top edge of a board as wide as the gate.</summary>
        private Rect DrawIceDoor(RectTransform root)
        {
            var gate = new GateDefinition(0, BoardEdge.Top, 0, EdgeFeatureCells, config.IntroBlockColor, config.IntroCount);
            var ctx = GateBoard(EdgeFeatureCells, 1, gate);

            DrawGate(root, ctx, isOpen: false);

            return GateSpan(ctx);
        }

        /// <summary>A locked 1×2 block beside a 1×1 block carrying its key, whose gem is the lock's colour (D47).</summary>
        private Rect DrawLockAndKey(RectTransform root)
        {
            var locked = DrawBlock(
                root, "Locked block", Vector2.zero, Horizontal2, config.BlockFill(config.IntroBlockColor));
            recipes.Lock(locked, Horizontal2, MarkLayout.Anchor(Horizontal2), 1f, KeysOnTheLockCard);

            var carrierOrigin = new Vector2(Horizontal2.Count, 0f);
            var carrier = DrawBlock(
                root, "Key block", carrierOrigin, Single, config.BlockFill(config.IntroSecondColor));
            recipes.Key(carrier, MarkLayout.Anchor(Single), 1f, config.BlockFill(config.IntroBlockColor));

            var first = BlockBounds(Vector2.zero, Horizontal2);
            var second = BlockBounds(carrierOrigin, Single);
            return Rect.MinMaxRect(first.xMin, first.yMin, second.xMax, first.yMax);
        }

        /// <summary>A closed shutter's panel (M5): slats, a rim, and its count.</summary>
        private Rect DrawShutter(RectTransform root)
        {
            var region = new Rect(0f, 0f, RegionCells, RegionCells);
            recipes.ShutterPanel(root, region, config.IntroCount, config.CountBadgeRimColor);
            return region;
        }

        /// <summary>An elevator (M9): its rim with hazard strips, and its two doors.</summary>
        private Rect DrawElevator(RectTransform root)
        {
            var region = new Rect(0f, 0f, RegionCells, RegionCells);
            recipes.Elevator(root, region);
            return region;
        }

        /// <summary>
        /// A generator's machine (M6, D48) above a board as wide as the
        /// machine: body, screen, the next block in miniature, and the queued
        /// count on the screen. Placed by <see cref="GeneratorMachine.Place"/>,
        /// the board's rule; the card frames its body.
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

            recipes.Machine(root, body, placement.Screen, Horizontal2, config.BlockFill(config.IntroBlockColor), queue.Length);
            return body;
        }

        /// <summary>
        /// A block at <paramref name="origin"/>, by the board's recipe
        /// (<see cref="BoardRecipes{TGroup, TPiece}.Block"/>), as the board
        /// shows it.
        /// </summary>
        /// <returns>The block's body, whose frame is the footprint's: marks go under it.</returns>
        private RectTransform DrawBlock(
            RectTransform parent, string name, Vector2 origin, IReadOnlyList<Coord> cells, Color fill,
            Color? beneath = null, bool isIce = false, MovementAxis axis = MovementAxis.Free)
        {
            var body = AddGroup(parent, name, origin);
            recipes.Block(body, cells, BlockTiling.Compute(cells), fill, beneath, isIce, axis);
            return body;
        }

        /// <summary>
        /// The one gate of <paramref name="ctx"/>, by the board's recipe
        /// (<see cref="BoardRecipes{TGroup, TPiece}.Gate"/>): open, in its
        /// colour; closed, in ice with the clears it needs.
        /// </summary>
        private void DrawGate(RectTransform root, LevelContext ctx, bool isOpen)
        {
            var gate = ctx.Gates[0];
            var thickness = config.FrameThicknessCells;
            var tiles = BlockTiling.Compute(FrameTiling.GateCells(ctx, 0));
            var group = AddGroup(root, "Gate", Vector2.zero);

            // .Value is safe: only the Ice Door card draws a closed gate, and
            // it gives the gate its threshold. An open gate's count is not
            // read.
            recipes.Gate(
                group, gate, tiles,
                tile => FrameTiling.QuarterRect(tile, ctx.Width, ctx.Height, thickness),
                GateSpan(ctx),
                isOpen ? gate.Color : (BlockColor?)null,
                isOpen ? 0 : gate.OpenAtClearCount.Value);
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

        /// <summary>What a block at <paramref name="origin"/> covers: its footprint's bounding box, which holds its lip too.</summary>
        private static Rect BlockBounds(Vector2 origin, IReadOnlyList<Coord> cells)
        {
            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            return Rect.MinMaxRect(origin.x + minX, origin.y + minY, origin.x + maxX + 1, origin.y + maxY + 1);
        }

        private static Vector2 CellCenter(Coord cell) => new Vector2(cell.X + 0.5f, cell.Y + 0.5f);

        // ----- The recipes' surface (IRecipeSurface) -----
        //
        // A sorting order means nothing on a canvas: each ignores the order
        // it is given and draws in the sequence it is called.

        RectTransform IRecipeSurface<RectTransform, Image>.Group(RectTransform parent, string name, Vector2 center, float scale)
        {
            var group = AddGroup(parent, name, center);
            group.localScale = new Vector3(scale, scale, 1f);
            return group;
        }

        List<Image> IRecipeSurface<RectTransform, Image>.Quarters(
            RectTransform parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, QuarterLayer layer, Color color,
            int order) =>
            AddQuarters(parent, tiles, rectOf, layer, color);

        Image IRecipeSurface<RectTransform, Image>.Sprite(
            RectTransform parent, string name, Sprite sprite, Vector2 center, Vector2 size, Color color, int order,
            float rotationDegrees, SpriteLook look)
        {
            var image = AddSprite(parent, name, sprite, center, size, color, rotationDegrees);
            image.material = MaterialOf(look);
            return image;
        }

        Image IRecipeSurface<RectTransform, Image>.Sliced(
            RectTransform parent, string name, Sprite sprite, Vector2 center, Vector2 size, float border, Color color, int order,
            SpriteLook look)
        {
            var image = AddSliced(parent, name, sprite, center, size, border, color);
            if (image != null)
            {
                image.material = MaterialOf(look);
            }

            return image;
        }

        Image IRecipeSurface<RectTransform, Image>.Tiled(
            RectTransform parent, string name, Sprite sprite, Vector2 center, Vector2 size, float tileCells,
            bool tileIsMeasuredByHeight, Color color, int order, SpriteLook look)
        {
            var image = AddTiled(
                parent, name, sprite, center, size, tileIsMeasuredByHeight ? sprite.rect.height : sprite.rect.width,
                tileCells, color);
            image.material = MaterialOf(look);
            return image;
        }

        /// <summary>
        /// Sliced, so only the strip's middle stretches along its length,
        /// across x or along y; its thickness is the sprite's whole extent
        /// across it.
        /// </summary>
        Image IRecipeSurface<RectTransform, Image>.Strip(
            RectTransform parent, string name, Sprite sprite, Vector2 center, float length, float thickness, bool isAlongY,
            Color color, int order, SpriteLook look)
        {
            var size = isAlongY ? new Vector2(thickness, length) : new Vector2(length, thickness);
            var image = AddImage(parent, name, sprite, center, size, color, 0f);
            image.type = Image.Type.Sliced;
            image.material = MaterialOf(look);
            image.pixelsPerUnitMultiplier = UiBuilder.TiledPixelsPerUnitMultiplier(
                isAlongY ? sprite.rect.width : sprite.rect.height, sprite.pixelsPerUnit, referencePixelsPerUnit, thickness * unitsPerCell);
            return image;
        }

        void IRecipeSurface<RectTransform, Image>.Label(
            RectTransform parent, string name, Vector2 center, string text, float boardFontSize, Color color, bool isShadowed) =>
            AddLabel(parent, name, center, text, boardFontSize, color, isShadowed);

        /// <summary>The material a UI Image drawing a sprite as <paramref name="look"/> uses: the board's UI twins, or the UI default.</summary>
        private Material MaterialOf(SpriteLook look)
        {
            switch (look)
            {
                case SpriteLook.Lit:
                    return config.QuarterUiMaterial(QuarterLayer.Lit);
                case SpriteLook.Highlight:
                    return config.QuarterUiMaterial(QuarterLayer.Body);
                default:
                    return null;
            }
        }

        /// <summary>
        /// One image per quarter, in the tiles' order, each with its layer's
        /// UI material (<see cref="RuntimeConfig.QuarterUiMaterial"/>) and
        /// posed as its tile says over its rectangle enlarged by the seam
        /// overlap; a rotated sprite's own x runs along the scene's y. An
        /// <see cref="Image"/> has no flip, so a mirrored quarter gets a
        /// negative scale, which — like a <c>SpriteRenderer</c>'s flip on the
        /// board — acts in the sprite's own frame, before the rotation.
        /// </summary>
        private List<Image> AddQuarters(
            RectTransform parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, QuarterLayer layer, Color color)
        {
            var images = new List<Image>(tiles.Count);
            var overlap = QuarterLayers.SeamOverlap(layer, color, config.SeamOverlapCells) * 2f;
            var material = config.QuarterUiMaterial(layer);
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var rect = rectOf(tile);
                var size = rect.size + new Vector2(overlap, overlap);
                var frameSize = tile.IsRotated ? new Vector2(size.y, size.x) : size;
                var image = AddSprite(
                    parent, tile.ToString(), config.QuarterSprite(layer, tile), rect.center, frameSize, color,
                    tile.IsRotated ? QuarterTile.EdgeAlongYRotationDegrees : 0f);
                image.rectTransform.localScale = new Vector3(tile.FlipX ? -1f : 1f, tile.FlipY ? -1f : 1f, 1f);
                image.material = material;
                images.Add(image);
            }

            return images;
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
        /// side; nothing, and null, when the border is not positive.
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
        /// <paramref name="boardFontSize"/> reads on a cell, with the board's
        /// label shadow when <paramref name="isShadowed"/>.
        /// </summary>
        private void AddLabel(RectTransform parent, string name, Vector2 center, string text, float boardFontSize, Color color, bool isShadowed)
        {
            var rect = AddRect(parent, name, center, Vector2.one, 0f);
            var fontSize = boardFontSize * WorldFontUnitsPerCanvasFontUnit / config.CellSize * unitsPerCell;
            var label = UiBuilder.AddLabel(rect, config.LabelFont, fontSize, color, false);
            if (isShadowed && config.LabelShadowMaterial != null)
            {
                label.fontSharedMaterial = config.LabelShadowMaterial;
            }

            label.text = text;
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
