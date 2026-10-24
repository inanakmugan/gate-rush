using System;
using System.Collections.Generic;
using System.IO;
using GateRush.Core;
using GateRush.Runtime;
using UnityEditor;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// <i>Gate Rush → Render Mechanics Comparison</i> and <i>Render Time Bonus
    /// Comparison</i> (Module 46, pass 2): compose the scene of a reference
    /// script from the generated sprites and write it next to the reference
    /// image, so the two can be compared pixel by pixel.
    /// </summary>
    /// <remarks>
    /// <para><b>The layout is the script's.</b> <c>mechanics_reference.py</c>
    /// lays its mechanics on a sheet of four by two slots, each two cells and
    /// a gap wide, at 128 pixels a cell — the sprites' own size, so most are
    /// drawn one to one — on its own floor, in its own colours: a blue frozen
    /// block (the board draws it in the ice colour, M3), a red locked block, a
    /// key on a yellow block and a time bonus on an orange one, a green
    /// one-way block, a red block, a shutter, a generator, an elevator and a
    /// blue block with a two-digit badge. Each part sits where the script puts
    /// it; the chain follows the board's rule, whole periods fitted to the
    /// row.</para>
    /// <para><b>The blending is the board's.</b> In linear light, as
    /// <see cref="LightingComparison"/>: a lit sprite's stored colour times
    /// the headroom and its tint over what is under, a highlight added, a
    /// plain sprite over; a 9-sliced or tiled sprite is stretched or repeated
    /// as the board stretches it, each pixel the average of the texels it
    /// covers.</para>
    /// <para><b>Left out.</b> The digits and the time bonus's text: the board
    /// draws them as TextMeshPro labels, not as generated sprites.</para>
    /// <para><b>The time bonus has its own sheet.</b> Option D changed its
    /// coin (<c>time_bonus_reference.py</c>), so the mechanics sheet's slot
    /// draws the new coin where the old script put the old one and no longer
    /// matches <c>mechanics_reference.png</c> there.
    /// <i>Gate Rush → Render Time Bonus Comparison</i>
    /// (<see cref="RenderTimeBonus"/>) renders the new reference's scene.</para>
    /// </remarks>
    public static class MechanicsComparison
    {
        /// <summary>Where the comparison image is written, from the project's folder: beside the reference.</summary>
        public const string OutputPath = "Claude outputs/board-lighting/generated_mechanics.png";

        // The reference script's sheet and colours (mechanics_reference.py):
        // the comparison reproduces its scene, so these are its data, not
        // look parameters of the game.
        private const int SlotGapPixels = 40;
        private const int MarginPixels = 20;
        private const int SlotColumns = 4;
        private const int SlotRows = 2;
        private const int SlotCells = 2;
        private const string FloorHex = "#281350";
        private const string BadgeHex = "#E53935";
        private const string BadgeRimHex = "#FFC531";
        private const string BlueHex = "#0054F6";
        private const string RedHex = "#F02A1E";
        private const string YellowHex = "#F6BA00";
        private const string OrangeHex = "#F6660C";
        private const string GreenHex = "#07A515";
        private const string PurpleHex = "#A236F6";
        private const string CyanHex = "#06A8F6";
        private const int BadgeCornerPixels = 34;
        private const int BlockGapPixels = 10;
        private const int CoinLeftPixels = 14;
        private const int ScreenRisePixels = 8;
        private const int FittingsDropPixels = 38;
        private const int VentLeftPixels = 14;
        private const int ButtonRightPixels = 24;
        private const int TwoDigitCount = 12;

        /// <summary>Where the time bonus's comparison image is written, from the project's folder: beside its reference.</summary>
        public const string TimeBonusOutputPath = "Claude outputs/board-lighting/generated_time_bonus.png";

        // time_bonus_reference_b.py's scene: eight 2×1 blocks in its palette
        // order, four to a row, on its own floor. Its data, not look
        // parameters of the game; the gap between columns and the left margin
        // are the mechanics sheet's.
        private const int TimeBonusColumns = 4;
        private const int TimeBonusRows = 2;
        private const int TimeBonusTopPixels = 30;
        private const int TimeBonusRowGapPixels = 30;
        private const string TimeBonusFloorHex = "#2A2460";
        private const string PinkHex = "#F63CA2";
        private const int TimeBonusSeconds = 10;
        private static readonly string[] TimeBonusBlockHexes =
            { RedHex, OrangeHex, YellowHex, GreenHex, CyanHex, BlueHex, PurpleHex, PinkHex };

        /// <summary>Renders the comparison from the selected or only recipe and the project's config, and writes it to <see cref="OutputPath"/>.</summary>
        [MenuItem("Gate Rush/Render Mechanics Comparison")]
        public static void RenderFromMenu() => RenderToFile("Render Mechanics Comparison", OutputPath, Render);

        /// <summary>Renders the time bonus's comparison, as <see cref="RenderFromMenu"/>, and writes it to <see cref="TimeBonusOutputPath"/>.</summary>
        [MenuItem("Gate Rush/Render Time Bonus Comparison")]
        public static void RenderTimeBonusFromMenu() =>
            RenderToFile("Render Time Bonus Comparison", TimeBonusOutputPath, RenderTimeBonus);

        /// <summary>
        /// Renders with <paramref name="render"/> from the selected or only
        /// recipe and the project's config, and writes the image to
        /// <paramref name="outputPath"/>; logs under <paramref name="command"/>
        /// what it wrote, or why it wrote nothing.
        /// </summary>
        private static void RenderToFile(string command, string outputPath, Func<ArtRecipe, RuntimeConfig, ArtImage> render)
        {
            if (!ArtGenerator.TryFindRecipe(out var recipe, out var error))
            {
                Debug.LogError($"{command}: {error} Nothing was rendered.");
                return;
            }

            if (!ArtGenerator.TryFindRuntimeConfig(out var config, out var configError))
            {
                Debug.LogError($"{command}: {configError} Nothing was rendered.");
                return;
            }

            var problems = recipe.Problems();
            if (problems.Count > 0)
            {
                Debug.LogError($"{command}: {recipe.name} has {problems.Count} problem(s); see Generate Art. Nothing was rendered.", recipe);
                return;
            }

            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, ArtPainter.EncodePng(render(recipe, config)));
            Debug.Log($"{command}: wrote {path} from {recipe.name} and {config.name}.", recipe);
        }

        /// <summary>
        /// <c>time_bonus_reference_b.py</c>'s scene (Module 46, pass 2, option
        /// D, revision B), drawn from <paramref name="recipe"/>'s sprites and
        /// <paramref name="config"/>'s layout: eight 2×1 blocks in its palette
        /// order, each with the time-bonus coin where the "+10" group centred
        /// on the block's middle puts it
        /// (<see cref="RuntimeConfig.TimeBonusCoinCenterCells"/>), on the
        /// script's canvas and floor. The "+10" itself is left out: the board
        /// draws it as a TextMeshPro label, checked in play.
        /// </summary>
        public static ArtImage RenderTimeBonus(ArtRecipe recipe, RuntimeConfig config)
        {
            var cell = recipe.CellPixels;
            var coinCenter = config.TimeBonusCoinCenterCells(config.TimeBonusMarkText(TimeBonusSeconds)) * cell;
            var columnPitch = SlotCells * cell + SlotGapPixels;
            var rowPitch = cell + TimeBonusRowGapPixels;
            var width = TimeBonusColumns * columnPitch + MarginPixels;
            var height = TimeBonusRows * rowPitch + TimeBonusTopPixels;
            var sheet = new Sheet(recipe, config, width, height, TimeBonusFloorHex);

            for (var i = 0; i < TimeBonusBlockHexes.Length; i++)
            {
                var left = MarginPixels + i % TimeBonusColumns * columnPitch;
                var top = TimeBonusTopPixels + i / TimeBonusColumns * rowPitch;
                sheet.Block(left, top, SlotCells, 1, TimeBonusBlockHexes[i], hasStuds: true, isIce: false);
                sheet.Natural(
                    ArtSprite.BonusCoin, left + SlotCells * cell / 2.0 + coinCenter, top + cell / 2.0,
                    sheet.Lit(Vector3.one));
            }

            return sheet.ToImage();
        }

        /// <summary>The script's sheet, drawn from <paramref name="recipe"/>'s sprites in <paramref name="config"/>'s colours and layout, bottom row first.</summary>
        public static ArtImage Render(ArtRecipe recipe, RuntimeConfig config)
        {
            var sheet = new Sheet(recipe, config);
            var cell = recipe.CellPixels;
            var slot = SlotCells * cell + SlotGapPixels;

            int SlotX(int i) => MarginPixels + i * slot;
            int SlotY(int j) => MarginPixels + j * slot;

            // The frozen block, its ice, its frost and its count.
            var x = SlotX(0);
            var y = SlotY(0);
            sheet.Block(x, y, 2, 2, BlueHex, hasStuds: true, isIce: true);
            var streaks = sheet.SpriteOf(ArtSprite.IceStreaksWide);
            sheet.Blit(streaks, x + streaks.Width / 2.0, y + streaks.Height / 2.0, streaks.Width, streaks.Height, sheet.Plain(Vector3.one));
            sheet.Badge(x + 2 * cell - BadgeCornerPixels, y + 2 * cell - BadgeCornerPixels, 1);

            // The locked block, its chain and its padlock.
            x = SlotX(1);
            y = SlotY(0) + cell / 2;
            var locked = sheet.Block(x, y, 2, 1, RedHex, hasStuds: true, isIce: false);
            sheet.Chain(x, y, locked);
            sheet.Natural(ArtSprite.Lock, x + cell, y + cell / 2.0, sheet.Lit(Vector3.one));

            // The key and the time bonus.
            x = SlotX(2);
            y = SlotY(0);
            sheet.Block(x, y, 1, 1, YellowHex, hasStuds: true, isIce: false);
            sheet.Natural(ArtSprite.GoldKey, x + cell / 2.0, y + cell / 2.0, sheet.Lit(Vector3.one));
            sheet.Natural(ArtSprite.Gem, x + cell / 2.0, y + cell / 2.0, sheet.Lit(LightingComparison.Linear(RedHex)));
            sheet.Natural(ArtSprite.GemHighlight, x + cell / 2.0, y + cell / 2.0, sheet.Highlight());
            var bonusX = x + cell + BlockGapPixels;
            sheet.Block(bonusX, y, 1, 1, OrangeHex, hasStuds: true, isIce: false);
            sheet.Natural(ArtSprite.BonusCoin, bonusX + cell / 2.0 - CoinLeftPixels, y + cell / 2.0, sheet.Lit(Vector3.one));

            // The one-way block.
            var oneWayY = y + cell + BlockGapPixels;
            sheet.Block(x, oneWayY, 2, 1, GreenHex, hasStuds: false, isIce: false);
            sheet.Natural(ArtSprite.OneWayArrow, x + cell, oneWayY + cell / 2.0, sheet.Lit(LightingComparison.Linear(GreenHex)));
            sheet.Natural(ArtSprite.OneWayArrowHighlight, x + cell, oneWayY + cell / 2.0, sheet.Highlight());

            sheet.Block(SlotX(3), SlotY(0), 2, 2, RedHex, hasStuds: true, isIce: false);

            // The shutter: slats tiled inside its rim, the rim, its count.
            x = SlotX(0);
            y = SlotY(1);
            var side = SlotCells * cell;
            var inset = (int)Math.Round(config.ShutterInnerInsetCells * cell);
            sheet.Tiled(ArtSprite.ShutterCylinders, x + inset, y + inset, side - 2 * inset, side - 2 * inset);
            sheet.Natural(ArtSprite.ShutterRim, x + side / 2.0, y + side / 2.0, sheet.Lit(Vector3.one));
            sheet.Badge(x + side / 2.0, y + side / 2.0, 1);

            // The generator: body, screen, the next block, rivets, vent, button.
            var machineX = SlotX(1) + cell;
            var machineY = SlotY(1) + cell;
            sheet.Natural(ArtSprite.MachineBody, machineX, machineY, sheet.Lit(Vector3.one));
            var screen = sheet.SpriteOf(ArtSprite.MachineScreen);
            var screenY = machineY - ScreenRisePixels;
            sheet.Blit(screen, machineX, screenY, screen.Width, screen.Height, sheet.Lit(Vector3.one));
            sheet.Miniature(machineX, screenY, screen.Width, screen.Height, PurpleHex);
            sheet.Natural(ArtSprite.MachineVent, machineX - VentLeftPixels, machineY + FittingsDropPixels, sheet.Lit(Vector3.one));
            sheet.Natural(ArtSprite.MachineButton, machineX + ButtonRightPixels, machineY + FittingsDropPixels, sheet.Lit(Vector3.one));

            // The elevator.
            sheet.Elevator(SlotX(2), SlotY(1), side);

            // A plain block with a two-digit count.
            x = SlotX(3);
            y = SlotY(1);
            sheet.Block(x, y, 2, 2, CyanHex, hasStuds: true, isIce: false);
            sheet.Badge(x + 2 * cell - BadgeCornerPixels, y + 2 * cell - BadgeCornerPixels, TwoDigitCount);

            return sheet.ToImage();
        }

        /// <summary>The sheet being composed: its pixels in linear light, rows from the bottom, and the sprites painted for it.</summary>
        private sealed class Sheet
        {
            private readonly ArtRecipe recipe;
            private readonly RuntimeConfig config;
            private readonly ArtPalette palette;
            private readonly Dictionary<ArtSprite, ArtImage> sprites = new Dictionary<ArtSprite, ArtImage>();
            private readonly Vector3[] image;
            private readonly int width;
            private readonly int height;

            /// <summary>The mechanics sheet: four by two slots on the script's floor.</summary>
            public Sheet(ArtRecipe recipe, RuntimeConfig config)
                : this(
                    recipe, config, SlotColumns * (SlotCells * recipe.CellPixels + SlotGapPixels) + MarginPixels,
                    SlotRows * (SlotCells * recipe.CellPixels + SlotGapPixels) + MarginPixels, FloorHex)
            {
            }

            /// <summary>A sheet <paramref name="width"/> by <paramref name="height"/> pixels on a floor of <paramref name="floorHex"/>.</summary>
            public Sheet(ArtRecipe recipe, RuntimeConfig config, int width, int height, string floorHex)
            {
                this.recipe = recipe;
                this.config = config;
                palette = ArtPalette.From(config);
                this.width = width;
                this.height = height;
                image = new Vector3[width * height];
                var floor = LightingComparison.Linear(floorHex);
                for (var i = 0; i < image.Length; i++)
                {
                    image[i] = floor;
                }
            }

            public ArtImage SpriteOf(ArtSprite sprite)
            {
                if (!sprites.TryGetValue(sprite, out var painted))
                {
                    painted = ArtPainter.Paint(recipe, palette, sprite);
                    sprites[sprite] = painted;
                }

                return painted;
            }

            /// <summary>A lit sprite in <paramref name="tint"/>, as the lit material draws it.</summary>
            public Func<Vector3, Color32, Vector3> Lit(Vector3 tint)
            {
                float headroom = recipe.ShadeHeadroom;
                return (under, texel) =>
                    Vector3.Lerp(under, Vector3.Scale(LightingComparison.Linear(texel), tint) * headroom, texel.a / 255f);
            }

            /// <summary>A white highlight, as the specular material adds it.</summary>
            public Func<Vector3, Color32, Vector3> Highlight() =>
                (under, texel) => under + Vector3.one * (float)(LightingComparison.Decode(texel.r) * texel.a / 255.0);

            /// <summary>A plain sprite in <paramref name="tint"/>.</summary>
            public Func<Vector3, Color32, Vector3> Plain(Vector3 tint) =>
                (under, texel) => Vector3.Lerp(under, Vector3.Scale(LightingComparison.Linear(texel), tint), texel.a / 255f);

            /// <summary>
            /// A block <paramref name="columns"/> by <paramref name="rows"/>
            /// cells with its top-left corner at <c>(left, top)</c>, y down, in
            /// <paramref name="hex"/>: its floor shadow, lit body, highlight,
            /// and its studs, or its ice, as the board draws them.
            /// </summary>
            /// <returns>The block's cells.</returns>
            public Coord[] Block(int left, int top, int columns, int rows, string hex, bool hasStuds, bool isIce)
            {
                var cell = recipe.CellPixels;
                var cells = new List<Coord>();
                for (var j = 0; j < rows; j++)
                {
                    for (var i = 0; i < columns; i++)
                    {
                        cells.Add(new Coord(i, j));
                    }
                }

                var footprint = cells.ToArray();
                var tiles = BlockTiling.Compute(footprint);
                var origin = new Vector2(left, height - top - rows * cell);
                var albedo = LightingComparison.Linear(hex);
                double headroom = recipe.ShadeHeadroom;
                var margin = recipe.FloorShadowMarginPixels / (float)cell;

                foreach (var tile in QuarterLayers.Edged(tiles))
                {
                    LightingComparison.Draw(
                        image, width, height, SpriteOf(LightingComparison.Placed(LightingComparison.PlacedLayer.Shadow, tile)), tile, origin,
                        FaceShape.ShadowQuarterRect(BlockTiling.QuarterRect(tile), tile, margin), cell,
                        (under, tone, alpha) => under * (1f - alpha));
                }

                foreach (var tile in tiles)
                {
                    var rect = BlockTiling.QuarterRect(tile);
                    LightingComparison.Draw(
                        image, width, height, SpriteOf(LightingComparison.Placed(LightingComparison.PlacedLayer.Face, tile)), tile, origin,
                        rect, cell, (under, tone, alpha) => Vector3.Lerp(under, albedo * (float)(tone * headroom), alpha));
                    LightingComparison.Draw(
                        image, width, height, SpriteOf(LightingComparison.Placed(LightingComparison.PlacedLayer.Body, tile)), tile, origin,
                        rect, cell, (under, tone, alpha) => under + Vector3.one * (float)(tone * alpha));
                }

                if (hasStuds)
                {
                    foreach (var c in footprint)
                    {
                        var rect = new Rect(c.X, c.Y, 1f, 1f);
                        LightingComparison.Draw(
                            image, width, height, SpriteOf(ArtSprite.Studs), null, origin, rect, cell,
                            (under, tone, alpha) => Vector3.Lerp(under, albedo * (float)(tone * headroom), alpha));
                        LightingComparison.Draw(
                            image, width, height, SpriteOf(ArtSprite.StudGloss), null, origin, rect, cell,
                            (under, tone, alpha) => Vector3.Lerp(under, Vector3.one * (float)tone, alpha));
                    }
                }

                if (isIce)
                {
                    foreach (var tile in tiles)
                    {
                        LightingComparison.DrawTexels(
                            image, width, height, SpriteOf(LightingComparison.Placed(LightingComparison.PlacedLayer.Ice, tile)), tile, origin,
                            BlockTiling.QuarterRect(tile), cell, Lit(Vector3.one));
                    }
                }

                return footprint;
            }

            /// <summary>
            /// A locked block's chain on each of its rows, by the board's rule:
            /// the row less its end inset, filled with as many whole periods as
            /// fit, stretched to fill it.
            /// </summary>
            public void Chain(int left, int top, IReadOnlyList<Coord> cells)
            {
                var cell = recipe.CellPixels;
                var period = SpriteOf(ArtSprite.Chain);
                var thickness = period.Height / (float)cell;
                var strips = MarkLayout.ChainStrips(cells, thickness, config.ChainEndInsetCells);
                var maxY = 0;
                foreach (var c in cells)
                {
                    maxY = Math.Max(maxY, c.Y);
                }

                foreach (var strip in strips)
                {
                    var count = Mathf.Max(1, Mathf.RoundToInt(strip.width * cell / period.Width));
                    var length = strip.width * cell / count;
                    var tileHeight = period.Height * length / period.Width;
                    var centreY = top + (maxY + 1 - strip.center.y) * cell;
                    for (var k = 0; k < count; k++)
                    {
                        var centreX = left + strip.xMin * cell + (k + 0.5) * length;
                        Blit(period, centreX, centreY, length, tileHeight, Lit(Vector3.one));
                    }
                }
            }

            /// <summary>The count badge for <paramref name="value"/> centred on <c>(x, y)</c>, y down, in the script's colours, widened as the board widens it.</summary>
            public void Badge(double x, double y, int value)
            {
                var rim = SpriteOf(ArtSprite.BadgeRim);
                var grow = (config.BadgeWidthCells(value) - config.BadgeWidthCells(1)) * recipe.CellPixels;
                var drawn = rim.Width + grow;
                Blit(rim, x, y, drawn, rim.Height, Lit(LightingComparison.Linear(BadgeRimHex)));
                Blit(SpriteOf(ArtSprite.BadgeFace), x, y, drawn, rim.Height, Lit(LightingComparison.Linear(BadgeHex)));
                Blit(SpriteOf(ArtSprite.BadgeHighlight), x, y, drawn, rim.Height, Highlight());
            }

            /// <summary>
            /// The next block, a single cell, on a machine's screen centred on
            /// <c>(x, y)</c>, y down: the board's split of the screen, its lit
            /// quarters and its studs scaled to it.
            /// </summary>
            public void Miniature(double x, double y, int screenWidth, int screenHeight, string hex)
            {
                var screen = new Rect((float)(x - screenWidth / 2.0), (float)(y - screenHeight / 2.0), screenWidth, screenHeight);
                var (area, _) = BoardView.SplitScreen(screen, config.MachineScreenCountShare, config.MachineMiniatureFillShare);
                var side = Mathf.Min(area.width, area.height);
                var albedo = LightingComparison.Linear(hex);
                var cx = area.center.x;
                var cy = area.center.y;

                // A lone cell's four quarters, each a corner of the face, then
                // its studs and their gloss over the whole cell.
                foreach (var (sprite, dx, dy) in new[]
                         {
                             (ArtSprite.FaceOuterTopLeft, -1, -1), (ArtSprite.FaceOuterTopRight, 1, -1),
                             (ArtSprite.FaceOuterBottomLeft, -1, 1), (ArtSprite.FaceOuterBottomRight, 1, 1)
                         })
                {
                    var quarter = SpriteOf(sprite);
                    var flipX = dx < 0;
                    var flipY = dy > 0;
                    Blit(quarter, cx + dx * side / 4.0, cy + dy * side / 4.0, side / 2.0, side / 2.0, Lit(albedo), flipX, flipY);
                }

                Blit(SpriteOf(ArtSprite.Studs), cx, cy, side, side, Lit(albedo));
                Blit(SpriteOf(ArtSprite.StudGloss), cx, cy, side, side, Plain(Vector3.one));
            }

            /// <summary>An elevator in a region <paramref name="side"/> pixels square at <c>(left, top)</c>, y down: rim, hazard strips, doors, handles.</summary>
            public void Elevator(int left, int top, int side)
            {
                var cell = recipe.CellPixels;
                var lit = Lit(Vector3.one);
                Natural(ArtSprite.ElevatorRim, left + side / 2.0, top + side / 2.0, lit);

                var end = (int)Math.Round(config.ElevatorHazardEndCells * cell);
                var centre = config.ElevatorHazardCentreCells * cell;
                var strip = SpriteOf(ArtSprite.ElevatorHazardLeft);
                Tiled(ArtSprite.ElevatorHazardLeft, (int)Math.Round(left + centre - strip.Width / 2.0), top + end, strip.Width, side - 2 * end);
                Tiled(ArtSprite.ElevatorHazardRight, (int)Math.Round(left + side - centre - strip.Width / 2.0), top + end, strip.Width, side - 2 * end);

                var region = new Rect(0f, 0f, side / (float)cell, side / (float)cell);
                var (leftDoor, rightDoor) = BoardView.ElevatorDoorRects(config, region);
                var door = SpriteOf(ArtSprite.ElevatorDoor);
                var reach = 2.0 * config.ElevatorDoorShadowMarginCells * cell;
                foreach (var rect in new[] { leftDoor, rightDoor })
                {
                    Blit(door, left + rect.center.x * cell, top + side - rect.center.y * cell, rect.width * cell + reach, rect.height * cell + reach, lit);
                }

                var offset = config.ElevatorHandleOffsetCells * cell;
                Natural(ArtSprite.ElevatorHandle, left + side / 2.0 - offset, top + side / 2.0, lit);
                Natural(ArtSprite.ElevatorHandle, left + side / 2.0 + offset, top + side / 2.0, lit);
            }

            /// <summary><paramref name="sprite"/> at its painted size centred on <c>(x, y)</c>, y down.</summary>
            public void Natural(ArtSprite sprite, double x, double y, Func<Vector3, Color32, Vector3> blend)
            {
                var painted = SpriteOf(sprite);
                Blit(painted, x, y, painted.Width, painted.Height, blend);
            }

            /// <summary>
            /// <paramref name="sprite"/> repeated, from its top-left corner,
            /// over a rectangle <paramref name="w"/> by <paramref name="h"/> at
            /// <c>(left, top)</c>, y down, lit and white.
            /// </summary>
            public void Tiled(ArtSprite sprite, int left, int top, int w, int h)
            {
                var tile = SpriteOf(sprite);
                var blend = Lit(Vector3.one);
                for (var dj = 0; dj < h; dj++)
                {
                    for (var di = 0; di < w; di++)
                    {
                        var u = di % tile.Width;
                        var v = dj % tile.Height;
                        Put(left + di, top + dj, tile.Pixels[(tile.Height - 1 - v) * tile.Width + u], blend);
                    }
                }
            }

            /// <summary>
            /// <paramref name="sprite"/> drawn over a rectangle
            /// <paramref name="w"/> by <paramref name="h"/> centred on
            /// <c>(x, y)</c>, y down, 9-sliced by its border — the border at its
            /// own size, the middle stretched — and mirrored when asked, each
            /// pixel the average of the texels its footprint covers.
            /// </summary>
            public void Blit(
                ArtImage sprite, double x, double y, double w, double h, Func<Vector3, Color32, Vector3> blend,
                bool flipX = false, bool flipY = false)
            {
                var left = (int)Math.Round(x - w / 2.0);
                var top = (int)Math.Round(y - h / 2.0);
                var destW = (int)Math.Round(w);
                var destH = (int)Math.Round(h);
                var border = sprite.Border;
                for (var dj = 0; dj < destH; dj++)
                {
                    var (v0, v1) = Footprint(dj, destH, sprite.Height, (int)border.w, (int)border.y);
                    for (var di = 0; di < destW; di++)
                    {
                        var (u0, u1) = Footprint(di, destW, sprite.Width, (int)border.x, (int)border.z);
                        Put(left + di, top + dj, Average(sprite, u0, u1, v0, v1, flipX, flipY), blend);
                    }
                }
            }

            /// <summary>
            /// The source range of destination pixel <paramref name="d"/> of
            /// <paramref name="destLength"/> along a sprite <paramref name="sourceLength"/>
            /// long, sliced <paramref name="before"/> and <paramref name="after"/>.
            /// </summary>
            private static (double from, double to) Footprint(int d, int destLength, int sourceLength, int before, int after)
            {
                if (destLength == sourceLength)
                {
                    return (d, d + 1);
                }

                var middle = destLength - before - after;
                if (middle <= 0 || before + after >= sourceLength)
                {
                    var scale = sourceLength / (double)destLength;
                    return (d * scale, (d + 1) * scale);
                }

                if (d < before)
                {
                    return (d, d + 1);
                }

                if (d >= destLength - after)
                {
                    var fromEnd = destLength - d;
                    return (sourceLength - fromEnd, sourceLength - fromEnd + 1);
                }

                var stretch = (sourceLength - before - after) / (double)middle;
                return (before + (d - before) * stretch, before + (d - before + 1) * stretch);
            }

            /// <summary>The average of the texels of <paramref name="sprite"/> whose centres lie in the footprint, rows from the top; the nearest when none does.</summary>
            private static Color32 Average(ArtImage sprite, double u0, double u1, double v0, double v1, bool flipX, bool flipY)
            {
                var r = 0.0;
                var g = 0.0;
                var b = 0.0;
                var a = 0.0;
                var n = 0;
                var iFrom = (int)Math.Ceiling(u0 - 0.5);
                var iTo = Math.Max(iFrom, (int)Math.Ceiling(u1 - 0.5) - 1);
                var jFrom = (int)Math.Ceiling(v0 - 0.5);
                var jTo = Math.Max(jFrom, (int)Math.Ceiling(v1 - 0.5) - 1);
                for (var j = jFrom; j <= jTo; j++)
                {
                    for (var i = iFrom; i <= iTo; i++)
                    {
                        var u = Mathf.Clamp(flipX ? sprite.Width - 1 - i : i, 0, sprite.Width - 1);
                        var v = Mathf.Clamp(flipY ? sprite.Height - 1 - j : j, 0, sprite.Height - 1);
                        var texel = sprite.Pixels[(sprite.Height - 1 - v) * sprite.Width + u];
                        r += texel.r;
                        g += texel.g;
                        b += texel.b;
                        a += texel.a;
                        n++;
                    }
                }

                return new Color32(
                    (byte)Math.Round(r / n), (byte)Math.Round(g / n), (byte)Math.Round(b / n), (byte)Math.Round(a / n));
            }

            /// <summary>Blends <paramref name="texel"/> into the sheet at <c>(x, y)</c>, y down, when it lies on it.</summary>
            private void Put(int x, int y, Color32 texel, Func<Vector3, Color32, Vector3> blend)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                {
                    return;
                }

                var index = (height - 1 - y) * width + x;
                image[index] = blend(image[index], texel);
            }

            public ArtImage ToImage()
            {
                var pixels = new Color32[image.Length];
                for (var i = 0; i < image.Length; i++)
                {
                    var c = image[i];
                    pixels[i] = new Color32(LightingComparison.Byte(c.x), LightingComparison.Byte(c.y), LightingComparison.Byte(c.z), 255);
                }

                return new ArtImage(width, height, pixels, Vector4.zero);
            }
        }
    }
}
