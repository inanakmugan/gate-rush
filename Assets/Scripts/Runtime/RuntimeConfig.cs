using System;
using System.Collections.Generic;
using GateRush.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Every tunable value the board's presentation and input read: the
    /// generated art, palette, tints, sizes, margins, sorting orders, the drag
    /// settings, label settings, settle and clear-effect timings, and the
    /// result panel's titles. Nothing in <c>GateRush.Runtime</c> hardcodes one
    /// of these at a call site. Sizes are in cells unless named otherwise, so
    /// the board keeps its proportions whatever <see cref="CellSize"/> is.
    /// </summary>
    /// <remarks>
    /// The art sprites are generated in greyscale from the <c>ArtRecipe</c>
    /// asset (<i>Gate Rush → Generate Art</i>) and tinted here: one set serves
    /// every colour (D46). What must not take a block's colour — the stud
    /// gloss, the arrows — has its own sprite and its own colour below.
    /// </remarks>
    [CreateAssetMenu(fileName = "RuntimeConfig", menuName = "Gate Rush/Runtime Config")]
    public sealed class RuntimeConfig : ScriptableObject
    {
        [Header("Assets")]
        [Tooltip("A plain white square sprite. The shapes still drawn as placeholders (2.4b) — shutters, elevators, badges, beneath squares, generators, closed gates, the floor underlay — are this sprite, tinted and scaled.")]
        [SerializeField] private Sprite cellSprite;

        [Tooltip("Sprite-Unlit-Default. Under the 2D Renderer a lit sprite renders black with no Light 2D.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip("TextMeshPro font for every label. Leave empty to use the TMP Settings default font.")]
        [SerializeField] private TMP_FontAsset labelFont;

        [Header("Generated art (Assets/Art/Generated)")]
        [SerializeField] private Sprite quarterOuterSprite;
        [SerializeField] private Sprite quarterEdgeSprite;
        [SerializeField] private Sprite quarterFillSprite;
        [SerializeField] private Sprite quarterConcaveSprite;
        [SerializeField] private Sprite studsSprite;
        [SerializeField] private Sprite studGlossSprite;
        [SerializeField] private Sprite floorSprite;
        [SerializeField] private Sprite gateArrowSprite;
        [SerializeField] private Sprite axisArrowSprite;
        [SerializeField] private Sprite backgroundRampSprite;
        [SerializeField] private Sprite vignetteSprite;

        [Header("Board")]
        [Tooltip("World units per cell.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Empty space kept on each side of the framed board when fitting the camera, in cells.")]
        [SerializeField] private float sideMarginCells = 0.3f;

        [Tooltip("Part of the screen's height kept free above the board for the HUD, from 0 to 1. Top and bottom together must stay below 1.")]
        [SerializeField] private float topBandScreenFraction = 0.14f;

        [Tooltip("Part of the screen's height kept free below the board for the HUD, from 0 to 1. Top and bottom together must stay below 1.")]
        [SerializeField] private float bottomBandScreenFraction = 0.10f;

        [Tooltip("Thickness of the frame around the grid, in cells: above 0, at most 1. Generators and closed gates are drawn at this thickness too.")]
        [SerializeField] private float frameThicknessCells = 0.45f;

        [Tooltip("How far the floor's backing reaches past the grid under the frame, in cells, so no background shows between floor and frame. At most the frame thickness.")]
        [SerializeField] private float floorUnderlayCells = 0.2f;

        [Tooltip("How far below a block or the frame its darker copy (the lip) shows, in cells. It reads as thickness.")]
        [SerializeField] private float lipOffsetCells = 0.09f;

        [Tooltip("The lip's colour is the face colour with RGB multiplied by this, from 0 (black) to 1 (no darkening).")]
        [SerializeField] private float lipDarken = 0.6f;

        [Tooltip("How much each quarter tile is enlarged on every side, in cells, to hide sub-pixel cracks between tiles. 0 turns it off.")]
        [SerializeField] private float seamOverlapCells = 0.002f;

        [Tooltip("Size of the white arrow on an open gate, in cells.")]
        [SerializeField] private float gateArrowSizeCells = 0.3f;

        [Tooltip("Thickness of the double-headed arrow on an axis-restricted block (M7), in cells.")]
        [SerializeField] private float axisArrowThicknessCells = 0.42f;

        [Tooltip("How far the axis arrow stops short of each end of the block's bounding box, in cells. Below 0.5.")]
        [SerializeField] private float axisArrowEndInsetCells = 0.14f;

        [Tooltip("Size of the beneath-colour square drawn inside each cell of a layered block, as a fraction of a cell.")]
        [SerializeField, Range(0f, 1f)] private float beneathColorSize = 0.45f;

        [Tooltip("Thickness of an elevator's region outline, in cells.")]
        [SerializeField] private float elevatorOutlineThickness = 0.08f;

        [Tooltip("Side of a lock or key badge, in cells.")]
        [SerializeField] private float badgeSize = 0.36f;

        [Tooltip("Space between a badge and the edge of its cell, in cells.")]
        [SerializeField] private float badgeInsetCells = 0.06f;

        [Header("Input")]
        [Tooltip("How far the pointer must travel, in cells, before a release at the start reads as a push.")]
        [SerializeField] private float pushThresholdCells = 0.3f;

        [Tooltip("How fast, per second, a dragged block catches up with the finger. Higher follows more tightly; the lag is the same at any frame rate.")]
        [SerializeField] private float followRate = 20f;

        [Tooltip("How far, in cells, a block stopped against a corner may be off a corridor's line and still slide into it. 0 turns the assist off; must stay below 0.5.")]
        [SerializeField] private float cornerAssistCells = 0.3f;

        [Header("Settle")]
        [Tooltip("Seconds a released block takes to settle from where it was dropped into its cell.")]
        [SerializeField] private float settleSeconds = 0.08f;

        [SerializeField] private Ease settleEase = Ease.OutQuad;

        [Header("Clear effect")]
        [Tooltip("Seconds a destroyed block takes to shrink and fade toward its gate.")]
        [SerializeField] private float clearSeconds = 0.25f;

        [SerializeField] private Ease clearEase = Ease.InQuad;

        [Tooltip("How far a destroyed block travels toward and through its gate while it shrinks, in cells.")]
        [SerializeField] private float clearTravelCells = 0.6f;

        [Tooltip("Seconds a surviving layered block takes to peel its removed outer colour.")]
        [SerializeField] private float peelSeconds = 0.2f;

        [SerializeField] private Ease peelEase = Ease.InQuad;

        [Header("Result panel")]
        [SerializeField] private string winTitle = "Level Complete";
        [SerializeField] private string lossTitle = "Time's Up";

        [Header("Colours")]
        [Tooltip("One colour per BlockColor, in enum order: Red, Blue, Green, Yellow, Purple, Orange, Pink, Cyan.")]
        [SerializeField] private Color[] blockPalette =
        {
            new Color(0.93f, 0.20f, 0.25f),
            new Color(0.16f, 0.47f, 0.98f),
            new Color(0.22f, 0.80f, 0.30f),
            new Color(1.00f, 0.83f, 0.10f),
            new Color(0.62f, 0.30f, 0.95f),
            new Color(1.00f, 0.52f, 0.10f),
            new Color(1.00f, 0.45f, 0.75f),
            new Color(0.15f, 0.85f, 0.95f)
        };

        [Tooltip("Lock and key badge colours, indexed by lock id (M8: the identifier doubles as the badge colour). Keep them light: the count on a lock badge is drawn in Label Color.")]
        [SerializeField] private Color[] lockBadgePalette =
        {
            new Color(1.00f, 1.00f, 1.00f),
            new Color(0.72f, 0.72f, 0.72f),
            new Color(0.86f, 0.72f, 0.52f),
            new Color(0.72f, 0.95f, 0.72f),
            new Color(0.82f, 0.76f, 1.00f),
            new Color(1.00f, 0.80f, 0.80f)
        };

        [Tooltip("Badge colour used for a lock id outside the badge palette. The load logs an error naming the lock.")]
        [SerializeField] private Color unknownBadgeColor = new Color(1f, 0f, 1f);

        [Tooltip("Top of the background gradient.")]
        [SerializeField] private Color backgroundTop = new Color(0.23f, 0.20f, 0.42f);

        [Tooltip("Bottom of the background gradient; also the camera's clear colour.")]
        [SerializeField] private Color backgroundBottom = new Color(0.07f, 0.07f, 0.15f);

        [Tooltip("Colour of the vignette at the screen's corners; its alpha is the vignette's strength.")]
        [SerializeField] private Color vignetteColor = new Color(0f, 0f, 0f, 0.5f);

        [SerializeField] private Color floorColor = new Color(0.13f, 0.14f, 0.22f);
        [SerializeField] private Color frameColor = new Color(0.42f, 0.46f, 0.62f);

        [Tooltip("The highlight on each stud. Not tinted by the block's colour.")]
        [SerializeField] private Color glossColor = new Color(1f, 1f, 1f, 0.85f);

        [SerializeField] private Color gateArrowColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color axisArrowColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color frozenTint = new Color(0.72f, 0.86f, 0.95f);
        [SerializeField] private Color closedGateColor = new Color(0.60f, 0.72f, 0.80f);
        [SerializeField] private Color shutterColor = new Color(0.36f, 0.38f, 0.44f);
        [SerializeField] private Color generatorColor = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color elevatorOutlineColor = new Color(1f, 1f, 1f, 0.8f);

        [Header("Labels")]
        [SerializeField] private float labelFontSize = 4f;
        [SerializeField] private float badgeLabelFontSize = 2.5f;
        [SerializeField] private Color labelColor = new Color(0.08f, 0.08f, 0.10f);
        [SerializeField] private Color lightLabelColor = new Color(0.95f, 0.95f, 0.95f);

        [Header("Sorting orders (back to front)")]
        [SerializeField] private int backgroundOrder = -20;
        [SerializeField] private int vignetteOrder = -19;
        [SerializeField] private int floorUnderlayOrder = -1;
        [SerializeField] private int floorOrder;

        [Tooltip("The frame's lip, and an open gate's.")]
        [SerializeField] private int frameLipOrder = 1;

        [Tooltip("The frame's face, and an open gate's.")]
        [SerializeField] private int frameOrder = 2;

        [Tooltip("Generator bars and closed-gate bars, drawn over the frame.")]
        [SerializeField] private int edgeFeatureOrder = 3;

        [SerializeField] private int gateArrowOrder = 3;
        [SerializeField] private int blockLipOrder = 4;
        [SerializeField] private int blockOrder = 5;
        [SerializeField] private int studOrder = 6;
        [SerializeField] private int glossOrder = 7;
        [SerializeField] private int axisArrowOrder = 7;

        [Tooltip("Sorting group order of a peeling outer colour: above every part of a block's face, so the exposed colour shows beneath it.")]
        [SerializeField] private int peelOrder = 8;

        [SerializeField] private int beneathColorOrder = 9;
        [SerializeField] private int badgeOrder = 10;
        [SerializeField] private int shutterOrder = 11;
        [SerializeField] private int elevatorOrder = 12;
        [SerializeField] private int labelOrder = 13;

        /// <summary>The white square every placeholder shape is drawn with.</summary>
        public Sprite CellSprite => cellSprite;

        /// <summary>The material every sprite uses: <c>Sprite-Unlit-Default</c>.</summary>
        public Material SpriteMaterial => spriteMaterial;

        /// <summary>Label font; null means the TMP Settings default.</summary>
        public TMP_FontAsset LabelFont => labelFont;

        /// <summary>A block's 2×2 studs for one cell, tinted with its colour.</summary>
        public Sprite StudsSprite => studsSprite;

        /// <summary>The highlights on one cell's studs, drawn in <see cref="GlossColor"/>.</summary>
        public Sprite StudGlossSprite => studGlossSprite;

        /// <summary>One floor tile with its faint grid line, tinted <see cref="FloorColor"/>.</summary>
        public Sprite FloorSprite => floorSprite;

        /// <summary>The arrow on an open gate, pointing up before it is turned outward.</summary>
        public Sprite GateArrowSprite => gateArrowSprite;

        /// <summary>The 9-sliced double-headed arrow of an axis-restricted block, horizontal before it is turned.</summary>
        public Sprite AxisArrowSprite => axisArrowSprite;

        /// <summary>White with alpha rising to the top: the background gradient's top colour over the clear colour.</summary>
        public Sprite BackgroundRampSprite => backgroundRampSprite;

        /// <summary>Radial alpha, tinted <see cref="VignetteColor"/>.</summary>
        public Sprite VignetteSprite => vignetteSprite;

        /// <summary>World units per cell.</summary>
        public float CellSize => cellSize;

        /// <summary>Space kept on each side of the framed board, in cells.</summary>
        public float SideMarginCells => sideMarginCells;

        /// <summary>Part of the screen's height kept free above the board for the HUD.</summary>
        public float TopBandScreenFraction => topBandScreenFraction;

        /// <summary>Part of the screen's height kept free below the board for the HUD.</summary>
        public float BottomBandScreenFraction => bottomBandScreenFraction;

        /// <summary>Thickness of the frame, in cells.</summary>
        public float FrameThicknessCells => frameThicknessCells;

        /// <summary>How far the floor's backing reaches under the frame, in cells.</summary>
        public float FloorUnderlayCells => floorUnderlayCells;

        /// <summary>How far below its face a lip shows, in cells.</summary>
        public float LipOffsetCells => lipOffsetCells;

        /// <summary>How much each quarter tile is enlarged on every side, in cells.</summary>
        public float SeamOverlapCells => seamOverlapCells;

        /// <summary>Size of an open gate's arrow, in cells.</summary>
        public float GateArrowSizeCells => gateArrowSizeCells;

        /// <summary>Thickness of an axis-restricted block's arrow, in cells.</summary>
        public float AxisArrowThicknessCells => axisArrowThicknessCells;

        /// <summary>How far the axis arrow stops short of each end of the block, in cells.</summary>
        public float AxisArrowEndInsetCells => axisArrowEndInsetCells;

        /// <summary>Side of the beneath-colour square, as a fraction of a cell.</summary>
        public float BeneathColorSize => beneathColorSize;

        /// <summary>Thickness of an elevator outline, in cells.</summary>
        public float ElevatorOutlineThickness => elevatorOutlineThickness;

        /// <summary>Side of a lock or key badge, in cells.</summary>
        public float BadgeSize => badgeSize;

        /// <summary>Space between a badge and its cell's edge, in cells.</summary>
        public float BadgeInsetCells => badgeInsetCells;

        /// <summary>Pointer travel, in cells, that makes a release at the start a push.</summary>
        public float PushThresholdCells => pushThresholdCells;

        /// <summary>How fast the dragged block's smoothed pointer closes on the real one, per second (<see cref="DragSettings.FollowRate"/>).</summary>
        public float FollowRate => followRate;

        /// <summary>How far off a corridor's line a block may be and still slide in, in cells (<see cref="DragSettings.CornerAssistCells"/>).</summary>
        public float CornerAssistCells => cornerAssistCells;

        /// <summary>Seconds a released block takes to settle into its cell.</summary>
        public float SettleSeconds => settleSeconds;

        /// <summary>Easing of the settle.</summary>
        public Ease SettleEase => settleEase;

        /// <summary>Seconds a destroyed block's exit takes.</summary>
        public float ClearSeconds => clearSeconds;

        /// <summary>Easing of a destroyed block's exit.</summary>
        public Ease ClearEase => clearEase;

        /// <summary>How far a destroyed block travels toward its gate, in cells.</summary>
        public float ClearTravelCells => clearTravelCells;

        /// <summary>Seconds a surviving layered block's peel takes.</summary>
        public float PeelSeconds => peelSeconds;

        /// <summary>Easing of the peel.</summary>
        public Ease PeelEase => peelEase;

        /// <summary>Result panel title after a win.</summary>
        public string WinTitle => winTitle;

        /// <summary>Result panel title after the countdown runs out.</summary>
        public string LossTitle => lossTitle;

        /// <summary>Top of the background gradient.</summary>
        public Color BackgroundTop => backgroundTop;

        /// <summary>Bottom of the background gradient and the camera's clear colour.</summary>
        public Color BackgroundBottom => backgroundBottom;

        /// <summary>Vignette colour; its alpha is the strength.</summary>
        public Color VignetteColor => vignetteColor;

        /// <summary>Tint of the floor tiles and their backing.</summary>
        public Color FloorColor => floorColor;

        /// <summary>Tint of the frame and of walls, which are drawn as frame.</summary>
        public Color FrameColor => frameColor;

        /// <summary>Colour of the stud highlights.</summary>
        public Color GlossColor => glossColor;

        /// <summary>Colour of an open gate's arrow.</summary>
        public Color GateArrowColor => gateArrowColor;

        /// <summary>Colour of an axis-restricted block's arrow.</summary>
        public Color AxisArrowColor => axisArrowColor;

        /// <summary>Fill of a frozen block, whose colour is hidden (M3).</summary>
        public Color FrozenTint => frozenTint;

        /// <summary>Fill of a closed, colourless gate (M2).</summary>
        public Color ClosedGateColor => closedGateColor;

        /// <summary>Fill of a closed shutter's cover (M5).</summary>
        public Color ShutterColor => shutterColor;

        /// <summary>Fill of a generator's edge marker (M6).</summary>
        public Color GeneratorColor => generatorColor;

        /// <summary>Colour of an elevator's region outline (M9).</summary>
        public Color ElevatorOutlineColor => elevatorOutlineColor;

        /// <summary>Font size of count labels on cells.</summary>
        public float LabelFontSize => labelFontSize;

        /// <summary>Font size of the count on a badge.</summary>
        public float BadgeLabelFontSize => badgeLabelFontSize;

        /// <summary>Label colour on light fills.</summary>
        public Color LabelColor => labelColor;

        /// <summary>Label colour on dark fills: shutters, the frame, the background.</summary>
        public Color LightLabelColor => lightLabelColor;

        /// <summary>Sorting order of the background gradient.</summary>
        public int BackgroundOrder => backgroundOrder;

        /// <summary>Sorting order of the vignette.</summary>
        public int VignetteOrder => vignetteOrder;

        /// <summary>Sorting order of the floor's backing, beneath the tiles.</summary>
        public int FloorUnderlayOrder => floorUnderlayOrder;

        /// <summary>Sorting order of the floor tiles.</summary>
        public int FloorOrder => floorOrder;

        /// <summary>Sorting order of the frame's and open gates' lip.</summary>
        public int FrameLipOrder => frameLipOrder;

        /// <summary>Sorting order of the frame's and open gates' face.</summary>
        public int FrameOrder => frameOrder;

        /// <summary>Sorting order of generator and closed-gate bars.</summary>
        public int EdgeFeatureOrder => edgeFeatureOrder;

        /// <summary>Sorting order of an open gate's arrow.</summary>
        public int GateArrowOrder => gateArrowOrder;

        /// <summary>Sorting order of a block's lip.</summary>
        public int BlockLipOrder => blockLipOrder;

        /// <summary>Sorting order of a block's face quarters.</summary>
        public int BlockOrder => blockOrder;

        /// <summary>Sorting order of a block's studs.</summary>
        public int StudOrder => studOrder;

        /// <summary>Sorting order of the stud highlights.</summary>
        public int GlossOrder => glossOrder;

        /// <summary>Sorting order of an axis-restricted block's arrow.</summary>
        public int AxisArrowOrder => axisArrowOrder;

        /// <summary>Sorting group order of a peeling outer colour.</summary>
        public int PeelOrder => peelOrder;

        /// <summary>Sorting order of the beneath-colour squares.</summary>
        public int BeneathColorOrder => beneathColorOrder;

        /// <summary>Sorting order of lock and key badges.</summary>
        public int BadgeOrder => badgeOrder;

        /// <summary>Sorting order of shutter covers.</summary>
        public int ShutterOrder => shutterOrder;

        /// <summary>Sorting order of elevator outlines.</summary>
        public int ElevatorOrder => elevatorOrder;

        /// <summary>Sorting order of every label, in front of everything else.</summary>
        public int LabelOrder => labelOrder;

        /// <summary>The generated sprite for one of the four quarter pieces.</summary>
        public Sprite QuarterSprite(QuarterSpriteKind kind)
        {
            switch (kind)
            {
                case QuarterSpriteKind.Outer:
                    return quarterOuterSprite;
                case QuarterSpriteKind.Edge:
                    return quarterEdgeSprite;
                case QuarterSpriteKind.Fill:
                    return quarterFillSprite;
                default:
                    return quarterConcaveSprite;
            }
        }

        /// <summary>The fill for block colour <paramref name="color"/>.</summary>
        /// <exception cref="InvalidOperationException">The palette has no entry for it; see <see cref="Problems"/>.</exception>
        public Color BlockFill(BlockColor color)
        {
            var index = (int)color;
            if (blockPalette == null || index >= blockPalette.Length)
            {
                throw new InvalidOperationException(
                    $"{name}: the block palette has no entry for {color}.");
            }

            return blockPalette[index];
        }

        /// <summary>The lip colour under a face of <paramref name="face"/>: its RGB darkened, its alpha kept.</summary>
        public Color LipFill(Color face) =>
            new Color(face.r * lipDarken, face.g * lipDarken, face.b * lipDarken, face.a);

        /// <summary>
        /// The badge colour for lock <paramref name="lockId"/>. False — with
        /// <see cref="unknownBadgeColor"/> in <paramref name="color"/> — when the
        /// id lies outside the badge palette.
        /// </summary>
        public bool TryGetBadgeColor(int lockId, out Color color)
        {
            if (lockBadgePalette != null && lockId >= 0 && lockId < lockBadgePalette.Length)
            {
                color = lockBadgePalette[lockId];
                return true;
            }

            color = unknownBadgeColor;
            return false;
        }

        /// <summary>
        /// Every reason this config cannot draw a board, as messages naming the
        /// field; empty when it is usable.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            var colourCount = Enum.GetValues(typeof(BlockColor)).Length;

            if (cellSprite == null)
            {
                problems.Add($"{name}: Cell Sprite is not assigned.");
            }

            if (spriteMaterial == null)
            {
                problems.Add($"{name}: Sprite Material is not assigned (use Sprite-Unlit-Default).");
            }

            AddIfUnassigned(problems, quarterOuterSprite, "Quarter Outer Sprite");
            AddIfUnassigned(problems, quarterEdgeSprite, "Quarter Edge Sprite");
            AddIfUnassigned(problems, quarterFillSprite, "Quarter Fill Sprite");
            AddIfUnassigned(problems, quarterConcaveSprite, "Quarter Concave Sprite");
            AddIfUnassigned(problems, studsSprite, "Studs Sprite");
            AddIfUnassigned(problems, studGlossSprite, "Stud Gloss Sprite");
            AddIfUnassigned(problems, floorSprite, "Floor Sprite");
            AddIfUnassigned(problems, gateArrowSprite, "Gate Arrow Sprite");
            AddIfUnassigned(problems, axisArrowSprite, "Axis Arrow Sprite");
            AddIfUnassigned(problems, backgroundRampSprite, "Background Ramp Sprite");
            AddIfUnassigned(problems, vignetteSprite, "Vignette Sprite");

            if (!(cellSize > 0f))
            {
                problems.Add($"{name}: Cell Size must be positive.");
            }

            foreach (var problem in BoardLayout.Problems(
                         frameThicknessCells, sideMarginCells, topBandScreenFraction, bottomBandScreenFraction))
            {
                problems.Add($"{name}: {problem}");
            }

            if (!(floorUnderlayCells >= 0f && floorUnderlayCells <= frameThicknessCells))
            {
                problems.Add($"{name}: Floor Underlay Cells must be at least 0 and at most Frame Thickness Cells.");
            }

            if (!(lipOffsetCells >= 0f))
            {
                problems.Add($"{name}: Lip Offset Cells may not be negative.");
            }

            if (!(lipDarken >= 0f && lipDarken <= 1f))
            {
                problems.Add($"{name}: Lip Darken must be from 0 to 1.");
            }

            if (!(seamOverlapCells >= 0f))
            {
                problems.Add($"{name}: Seam Overlap Cells may not be negative.");
            }

            if (!(gateArrowSizeCells > 0f))
            {
                problems.Add($"{name}: Gate Arrow Size Cells must be positive.");
            }

            if (!(axisArrowThicknessCells > 0f && axisArrowThicknessCells <= 1f))
            {
                problems.Add($"{name}: Axis Arrow Thickness Cells must be above 0 and at most 1.");
            }

            if (!(axisArrowEndInsetCells >= 0f && axisArrowEndInsetCells < 0.5f))
            {
                problems.Add($"{name}: Axis Arrow End Inset Cells must be at least 0 and below 0.5.");
            }

            if (!(badgeInsetCells >= 0f))
            {
                problems.Add($"{name}: Badge Inset Cells may not be negative.");
            }

            foreach (var problem in DragSettings.Problems(pushThresholdCells, followRate, cornerAssistCells))
            {
                problems.Add($"{name}: {problem}");
            }

            if (!(settleSeconds > 0f))
            {
                problems.Add($"{name}: Settle Seconds must be positive.");
            }

            if (!(clearSeconds > 0f))
            {
                problems.Add($"{name}: Clear Seconds must be positive.");
            }

            if (clearTravelCells < 0f)
            {
                problems.Add($"{name}: Clear Travel Cells may not be negative.");
            }

            if (!(peelSeconds > 0f))
            {
                problems.Add($"{name}: Peel Seconds must be positive.");
            }

            if (blockPalette == null || blockPalette.Length < colourCount)
            {
                problems.Add($"{name}: Block Palette needs {colourCount} entries, one per BlockColor.");
            }

            return problems;
        }

        private void AddIfUnassigned(List<string> problems, Sprite sprite, string field)
        {
            if (sprite == null)
            {
                problems.Add($"{name}: {field} is not assigned; run Gate Rush → Generate Art and assign it from Assets/Art/Generated.");
            }
        }
    }
}
