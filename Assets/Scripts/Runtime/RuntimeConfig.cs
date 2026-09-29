using System;
using System.Collections.Generic;
using GateRush.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

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
    /// <para>The art sprites are generated in greyscale from the
    /// <c>ArtRecipe</c> asset (<i>Gate Rush → Generate Art</i>) and tinted here:
    /// one set serves every colour (D46). What must not take a block's colour —
    /// the stud gloss, the arrows, the frost, the key's gold — has its own
    /// sprite and its own colour below.</para>
    /// <para><b>Sorting orders</b> have their defaults as constants, used both by
    /// the fields and by <i>Reset Sorting Orders</i> in the component's context
    /// menu, which resets those fields alone.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "RuntimeConfig", menuName = "Gate Rush/Runtime Config")]
    public sealed class RuntimeConfig : ScriptableObject
    {
        private const int DefaultBackgroundOrder = -20;
        private const int DefaultVignetteOrder = -19;
        private const int DefaultFloorUnderlayOrder = -1;
        private const int DefaultFloorOrder = 0;
        private const int DefaultElevatorDoorOrder = 1;
        private const int DefaultElevatorDividerOrder = 2;
        private const int DefaultElevatorBorderOrder = 3;
        private const int DefaultMachineLipOrder = 4;
        private const int DefaultMachineOrder = 5;
        private const int DefaultMachineScreenOrder = 6;
        private const int DefaultMiniatureLipOrder = 7;
        private const int DefaultMiniatureOrder = 8;
        private const int DefaultFrameLipOrder = 9;
        private const int DefaultFrameOrder = 10;
        private const int DefaultGateMarkOrder = 11;
        private const int DefaultBlockLipOrder = 12;
        private const int DefaultBlockOrder = 13;
        private const int DefaultStudOrder = 14;
        private const int DefaultGlossOrder = 15;
        private const int DefaultPeelOrder = 16;
        private const int DefaultBeneathColorOrder = 17;
        private const int DefaultChainOrder = 18;
        private const int DefaultIconOrder = 19;
        private const int DefaultKeyGemOrder = 20;
        private const int DefaultShutterOrder = 21;
        private const int DefaultShutterBorderOrder = 22;
        private const int DefaultBadgeRimOrder = 23;
        private const int DefaultBadgeOrder = 24;
        private const int DefaultLabelOrder = 25;

        [Header("Assets")]
        [Tooltip("A plain white square sprite, tinted and scaled for the floor's backing and a layered block's beneath-colour squares.")]
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
        [SerializeField] private Sprite frostSprite;
        [SerializeField] private Sprite roundedRectSprite;
        [SerializeField] private Sprite ringSprite;
        [SerializeField] private Sprite chainSprite;
        [SerializeField] private Sprite padlockSprite;
        [SerializeField] private Sprite keyBodySprite;
        [SerializeField] private Sprite keyGemSprite;
        [SerializeField] private Sprite shutterSlatsSprite;
        [SerializeField] private Sprite doorPanelSprite;

        [Header("Board")]
        [Tooltip("World units per cell.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Empty space kept on each side of the framed board, and of any generator machine, when fitting the camera, in cells.")]
        [SerializeField] private float sideMarginCells = 0.3f;

        [Tooltip("Part of the screen's height kept free above the board for the HUD, from 0 to 1. Top and bottom together must stay below 1.")]
        [SerializeField] private float topBandScreenFraction = 0.14f;

        [Tooltip("Part of the screen's height kept free below the board for the HUD, from 0 to 1. Top and bottom together must stay below 1.")]
        [SerializeField] private float bottomBandScreenFraction = 0.10f;

        [Tooltip("Thickness of the frame around the grid, in cells: above 0, at most 1. Closed gates are drawn at this thickness too.")]
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

        [Header("Count badge")]
        [Tooltip("Height of the badge every count sits on, in cells.")]
        [SerializeField] private float badgeHeightCells = 0.34f;

        [Tooltip("Width each digit adds to a badge, in cells. A one-digit badge is never narrower than it is tall.")]
        [SerializeField] private float badgeDigitWidthCells = 0.15f;

        [Tooltip("Space either side of a badge's digits, in cells.")]
        [SerializeField] private float badgePaddingCells = 0.08f;

        [Tooltip("Thickness of a badge's lighter rim, in cells. Below half the badge's height.")]
        [SerializeField] private float badgeRimCells = 0.035f;

        [Header("Locks and keys")]
        [Tooltip("Thickness of a locked block's chains, in cells: above 0, at most 1.")]
        [SerializeField] private float chainThicknessCells = 0.2f;

        [Tooltip("How far a chain stops short of each end of its row of cells, in cells. Below 0.5.")]
        [SerializeField] private float chainEndInsetCells = 0.1f;

        [Tooltip("Size of the padlock on a locked block, in cells.")]
        [SerializeField] private float padlockSizeCells = 0.62f;

        [Tooltip("How far below the padlock's centre its count badge sits, in cells, so the badge lands on the padlock's body.")]
        [SerializeField] private float padlockBadgeDropCells = 0.1f;

        [Tooltip("Length of the key on a key-carrying block, in cells.")]
        [SerializeField] private float keySizeCells = 0.72f;

        [Tooltip("How far the key is turned from horizontal, in degrees, counter-clockwise.")]
        [SerializeField] private float keyRotationDegrees = 35f;

        [Tooltip("How far a frozen block's padlock or key is raised above its frozen count, in cells, so the two do not overlap.")]
        [SerializeField] private float frozenMarkRaiseCells = 0.3f;

        [Header("Shutters and elevators")]
        [Tooltip("Thickness of a closed shutter's border, in cells.")]
        [SerializeField] private float shutterBorderCells = 0.07f;

        [Tooltip("Thickness of an elevator's border, in cells.")]
        [SerializeField] private float elevatorBorderCells = 0.05f;

        [Tooltip("Width of the divider down the middle of an elevator's doors, in cells.")]
        [SerializeField] private float elevatorDividerCells = 0.07f;

        [Header("Generator machines")]
        [Tooltip("How deep a generator's machine is, out from the frame, in cells.")]
        [SerializeField] private float machineDepthCells = 1f;

        [Tooltip("How far a machine runs past its generator's span at each end, in cells.")]
        [SerializeField] private float machineSideOverhangCells = 0.12f;

        [Tooltip("How far a machine's inner side reaches back under the frame, in cells. Below the depth.")]
        [SerializeField] private float machineFrameOverlapCells = 0.15f;

        [Tooltip("Margin between a machine's rim and its screen, in cells.")]
        [SerializeField] private float machineScreenInsetCells = 0.12f;

        [Tooltip("Corner radius of a machine's body and screen, in cells.")]
        [SerializeField] private float machineCornerCells = 0.18f;

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

        [Tooltip("Fill of a frozen block (M3) and a closed gate (M2); their lip is this colour darkened.")]
        [FormerlySerializedAs("frozenTint")]
        [SerializeField] private Color iceColor = new Color(0.70f, 0.88f, 1.00f);

        [Tooltip("The frost streaks over ice. Not tinted by anything else.")]
        [SerializeField] private Color frostColor = new Color(1f, 1f, 1f, 0.85f);

        [Tooltip("Fill of the badge every count sits on.")]
        [SerializeField] private Color badgeColor = new Color(0.80f, 0.45f, 0.18f);

        [Tooltip("Rim of a badge. A colour-bound shutter's badge takes the colour it counts instead.")]
        [SerializeField] private Color badgeRimColor = new Color(1.00f, 0.78f, 0.45f);

        [Tooltip("The number on a badge.")]
        [SerializeField] private Color badgeTextColor = new Color(1.00f, 0.96f, 0.85f);

        [SerializeField] private Color chainColor = new Color(0.62f, 0.64f, 0.70f);
        [SerializeField] private Color padlockColor = new Color(1.00f, 0.80f, 0.25f);

        [Tooltip("The key's gold. Its gem takes the colour of the lock it opens (D47).")]
        [SerializeField] private Color keyColor = new Color(1.00f, 0.80f, 0.25f);

        [SerializeField] private Color shutterSlatColor = new Color(0.30f, 0.18f, 0.45f);
        [SerializeField] private Color shutterBorderColor = new Color(1.00f, 0.66f, 0.20f);
        [SerializeField] private Color machineColor = new Color(0.55f, 0.58f, 0.70f);
        [SerializeField] private Color machineScreenColor = new Color(0.06f, 0.07f, 0.12f);
        [SerializeField] private Color elevatorDoorColor = new Color(0.22f, 0.14f, 0.34f);
        [SerializeField] private Color elevatorDividerColor = new Color(1.00f, 0.78f, 0.30f);
        [SerializeField] private Color elevatorBorderColor = new Color(1.00f, 0.66f, 0.20f);

        [Header("Labels")]
        [Tooltip("Font size of a layered block's remaining-colour numeral (M4).")]
        [SerializeField] private float labelFontSize = 4f;

        [Tooltip("Font size of the number on a badge.")]
        [SerializeField] private float badgeLabelFontSize = 2.5f;

        [Tooltip("Colour of a layered block's numeral.")]
        [SerializeField] private Color labelColor = new Color(0.08f, 0.08f, 0.10f);

        [Header("Sorting orders (back to front; context menu → Reset Sorting Orders)")]
        [SerializeField] private int backgroundOrder = DefaultBackgroundOrder;
        [SerializeField] private int vignetteOrder = DefaultVignetteOrder;
        [SerializeField] private int floorUnderlayOrder = DefaultFloorUnderlayOrder;
        [SerializeField] private int floorOrder = DefaultFloorOrder;
        [SerializeField] private int elevatorDoorOrder = DefaultElevatorDoorOrder;
        [SerializeField] private int elevatorDividerOrder = DefaultElevatorDividerOrder;
        [SerializeField] private int elevatorBorderOrder = DefaultElevatorBorderOrder;

        [Tooltip("A generator machine's lip. Machines sit behind the frame, which runs on over them.")]
        [SerializeField] private int machineLipOrder = DefaultMachineLipOrder;

        [SerializeField] private int machineOrder = DefaultMachineOrder;
        [SerializeField] private int machineScreenOrder = DefaultMachineScreenOrder;
        [SerializeField] private int miniatureLipOrder = DefaultMiniatureLipOrder;
        [SerializeField] private int miniatureOrder = DefaultMiniatureOrder;

        [Tooltip("The frame's lip, and a gate's.")]
        [SerializeField] private int frameLipOrder = DefaultFrameLipOrder;

        [Tooltip("The frame's face, and a gate's.")]
        [SerializeField] private int frameOrder = DefaultFrameOrder;

        [Tooltip("An open gate's arrow and a closed gate's frost.")]
        [SerializeField] private int gateMarkOrder = DefaultGateMarkOrder;

        [SerializeField] private int blockLipOrder = DefaultBlockLipOrder;
        [SerializeField] private int blockOrder = DefaultBlockOrder;

        [Tooltip("Studs, and a frozen block's frost, which has no studs.")]
        [SerializeField] private int studOrder = DefaultStudOrder;

        [Tooltip("Stud gloss, and an axis-restricted block's arrow, which has no studs.")]
        [SerializeField] private int glossOrder = DefaultGlossOrder;

        [Tooltip("Sorting group order of a peeling outer colour: above every part of a block's face, below its marks.")]
        [SerializeField] private int peelOrder = DefaultPeelOrder;

        [SerializeField] private int beneathColorOrder = DefaultBeneathColorOrder;
        [SerializeField] private int chainOrder = DefaultChainOrder;

        [Tooltip("A padlock or a key.")]
        [SerializeField] private int iconOrder = DefaultIconOrder;

        [SerializeField] private int keyGemOrder = DefaultKeyGemOrder;
        [SerializeField] private int shutterOrder = DefaultShutterOrder;
        [SerializeField] private int shutterBorderOrder = DefaultShutterBorderOrder;

        [Tooltip("A badge's rim: a full-size rounded box behind its fill.")]
        [SerializeField] private int badgeRimOrder = DefaultBadgeRimOrder;

        [SerializeField] private int badgeOrder = DefaultBadgeOrder;

        [Tooltip("Every label: badge numbers and layer numerals, in front of everything else.")]
        [SerializeField] private int labelOrder = DefaultLabelOrder;

        /// <summary>The white square the floor's backing and beneath squares are drawn with.</summary>
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

        /// <summary>One cell of frost streaks over ice, drawn in <see cref="FrostColor"/>.</summary>
        public Sprite FrostSprite => frostSprite;

        /// <summary>A 9-sliced bevelled rounded box whose slice border is its corner: badge rim and fill, machine body and screen, elevator divider.</summary>
        public Sprite RoundedRectSprite => roundedRectSprite;

        /// <summary>A 9-sliced rounded outline whose slice border is its width: shutter and elevator borders.</summary>
        public Sprite RingSprite => ringSprite;

        /// <summary>One period of chain, tiled along its length.</summary>
        public Sprite ChainSprite => chainSprite;

        /// <summary>The padlock on a locked block, tinted <see cref="PadlockColor"/>.</summary>
        public Sprite PadlockSprite => padlockSprite;

        /// <summary>The key on a key-carrying block, horizontal, tinted <see cref="KeyColor"/>.</summary>
        public Sprite KeyBodySprite => keyBodySprite;

        /// <summary>The gem in the key's bow, on the same canvas as <see cref="KeyBodySprite"/>, tinted the lock's colour.</summary>
        public Sprite KeyGemSprite => keyGemSprite;

        /// <summary>One cell of shutter slats, tiled over the region.</summary>
        public Sprite ShutterSlatsSprite => shutterSlatsSprite;

        /// <summary>One cell of lift door, tiled over an elevator's region.</summary>
        public Sprite DoorPanelSprite => doorPanelSprite;

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

        /// <summary>Height of a count badge, in cells.</summary>
        public float BadgeHeightCells => badgeHeightCells;

        /// <summary>Thickness of a badge's rim, in cells.</summary>
        public float BadgeRimCells => badgeRimCells;

        /// <summary>Thickness of a locked block's chains, in cells.</summary>
        public float ChainThicknessCells => chainThicknessCells;

        /// <summary>How far a chain stops short of each end of its row, in cells.</summary>
        public float ChainEndInsetCells => chainEndInsetCells;

        /// <summary>Size of a padlock, in cells.</summary>
        public float PadlockSizeCells => padlockSizeCells;

        /// <summary>How far below a padlock's centre its badge sits, in cells.</summary>
        public float PadlockBadgeDropCells => padlockBadgeDropCells;

        /// <summary>Length of a key, in cells.</summary>
        public float KeySizeCells => keySizeCells;

        /// <summary>How far a key is turned from horizontal, in degrees.</summary>
        public float KeyRotationDegrees => keyRotationDegrees;

        /// <summary>How far a frozen block's padlock or key is raised above its frozen count, in cells.</summary>
        public float FrozenMarkRaiseCells => frozenMarkRaiseCells;

        /// <summary>Thickness of a shutter's border, in cells.</summary>
        public float ShutterBorderCells => shutterBorderCells;

        /// <summary>Thickness of an elevator's border, in cells.</summary>
        public float ElevatorBorderCells => elevatorBorderCells;

        /// <summary>Width of an elevator's door divider, in cells.</summary>
        public float ElevatorDividerCells => elevatorDividerCells;

        /// <summary>Corner radius of a machine's body and screen, in cells.</summary>
        public float MachineCornerCells => machineCornerCells;

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

        /// <summary>Fill of a frozen block (M3) and a closed gate (M2).</summary>
        public Color IceColor => iceColor;

        /// <summary>Colour of the frost over ice.</summary>
        public Color FrostColor => frostColor;

        /// <summary>Fill of a count badge.</summary>
        public Color BadgeColor => badgeColor;

        /// <summary>Rim of a count badge that counts no particular colour.</summary>
        public Color BadgeRimColor => badgeRimColor;

        /// <summary>The number on a badge.</summary>
        public Color BadgeTextColor => badgeTextColor;

        /// <summary>Tint of a locked block's chains.</summary>
        public Color ChainColor => chainColor;

        /// <summary>Tint of a padlock.</summary>
        public Color PadlockColor => padlockColor;

        /// <summary>Tint of a key's body.</summary>
        public Color KeyColor => keyColor;

        /// <summary>Tint of a closed shutter's slats.</summary>
        public Color ShutterSlatColor => shutterSlatColor;

        /// <summary>Tint of a closed shutter's border.</summary>
        public Color ShutterBorderColor => shutterBorderColor;

        /// <summary>Tint of a generator machine's body.</summary>
        public Color MachineColor => machineColor;

        /// <summary>Tint of a generator machine's screen.</summary>
        public Color MachineScreenColor => machineScreenColor;

        /// <summary>Tint of an elevator's doors.</summary>
        public Color ElevatorDoorColor => elevatorDoorColor;

        /// <summary>Tint of the divider between an elevator's doors.</summary>
        public Color ElevatorDividerColor => elevatorDividerColor;

        /// <summary>Tint of an elevator's border.</summary>
        public Color ElevatorBorderColor => elevatorBorderColor;

        /// <summary>Font size of a layered block's numeral.</summary>
        public float LabelFontSize => labelFontSize;

        /// <summary>Font size of the number on a badge.</summary>
        public float BadgeLabelFontSize => badgeLabelFontSize;

        /// <summary>Colour of a layered block's numeral.</summary>
        public Color LabelColor => labelColor;

        /// <summary>Sorting order of the background gradient.</summary>
        public int BackgroundOrder => backgroundOrder;

        /// <summary>Sorting order of the vignette.</summary>
        public int VignetteOrder => vignetteOrder;

        /// <summary>Sorting order of the floor's backing, beneath the tiles.</summary>
        public int FloorUnderlayOrder => floorUnderlayOrder;

        /// <summary>Sorting order of the floor tiles.</summary>
        public int FloorOrder => floorOrder;

        /// <summary>Sorting order of an elevator's doors.</summary>
        public int ElevatorDoorOrder => elevatorDoorOrder;

        /// <summary>Sorting order of an elevator's divider.</summary>
        public int ElevatorDividerOrder => elevatorDividerOrder;

        /// <summary>Sorting order of an elevator's border.</summary>
        public int ElevatorBorderOrder => elevatorBorderOrder;

        /// <summary>Sorting order of a generator machine's lip.</summary>
        public int MachineLipOrder => machineLipOrder;

        /// <summary>Sorting order of a generator machine's body.</summary>
        public int MachineOrder => machineOrder;

        /// <summary>Sorting order of a generator machine's screen.</summary>
        public int MachineScreenOrder => machineScreenOrder;

        /// <summary>Sorting order of the lip of the next block on a machine's screen.</summary>
        public int MiniatureLipOrder => miniatureLipOrder;

        /// <summary>Sorting order of the next block on a machine's screen.</summary>
        public int MiniatureOrder => miniatureOrder;

        /// <summary>Sorting order of the frame's and gates' lip.</summary>
        public int FrameLipOrder => frameLipOrder;

        /// <summary>Sorting order of the frame's and gates' face.</summary>
        public int FrameOrder => frameOrder;

        /// <summary>Sorting order of an open gate's arrow and a closed gate's frost.</summary>
        public int GateMarkOrder => gateMarkOrder;

        /// <summary>Sorting order of a block's lip.</summary>
        public int BlockLipOrder => blockLipOrder;

        /// <summary>Sorting order of a block's face quarters.</summary>
        public int BlockOrder => blockOrder;

        /// <summary>Sorting order of a block's studs, and of a frozen block's frost.</summary>
        public int StudOrder => studOrder;

        /// <summary>Sorting order of the stud highlights and of an axis-restricted block's arrow.</summary>
        public int GlossOrder => glossOrder;

        /// <summary>Sorting group order of a peeling outer colour.</summary>
        public int PeelOrder => peelOrder;

        /// <summary>Sorting order of the beneath-colour squares.</summary>
        public int BeneathColorOrder => beneathColorOrder;

        /// <summary>Sorting order of a locked block's chains.</summary>
        public int ChainOrder => chainOrder;

        /// <summary>Sorting order of a padlock or a key's body.</summary>
        public int IconOrder => iconOrder;

        /// <summary>Sorting order of a key's gem.</summary>
        public int KeyGemOrder => keyGemOrder;

        /// <summary>Sorting order of a closed shutter's slats.</summary>
        public int ShutterOrder => shutterOrder;

        /// <summary>Sorting order of a closed shutter's border.</summary>
        public int ShutterBorderOrder => shutterBorderOrder;

        /// <summary>Sorting order of a badge's rim, a full-size rounded box behind its fill.</summary>
        public int BadgeRimOrder => badgeRimOrder;

        /// <summary>Sorting order of a badge's fill, inset by the rim's thickness.</summary>
        public int BadgeOrder => badgeOrder;

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

        /// <summary>The width, in cells, of the badge showing <paramref name="value"/>.</summary>
        public float BadgeWidthCells(int value) =>
            MarkLayout.BadgeWidth(value, badgeHeightCells, badgeDigitWidthCells, badgePaddingCells);

        /// <summary>The generator machine placement rule these values describe.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A machine size is out of range; see <see cref="Problems"/>.</exception>
        public GeneratorMachine CreateGeneratorMachine() =>
            new GeneratorMachine(
                frameThicknessCells, machineDepthCells, machineSideOverhangCells, machineFrameOverlapCells,
                machineScreenInsetCells, badgeHeightCells, badgeDigitWidthCells, badgePaddingCells);

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
            AddIfUnassigned(problems, frostSprite, "Frost Sprite");
            AddIfUnassigned(problems, roundedRectSprite, "Rounded Rect Sprite");
            AddIfUnassigned(problems, ringSprite, "Ring Sprite");
            AddIfUnassigned(problems, chainSprite, "Chain Sprite");
            AddIfUnassigned(problems, padlockSprite, "Padlock Sprite");
            AddIfUnassigned(problems, keyBodySprite, "Key Body Sprite");
            AddIfUnassigned(problems, keyGemSprite, "Key Gem Sprite");
            AddIfUnassigned(problems, shutterSlatsSprite, "Shutter Slats Sprite");
            AddIfUnassigned(problems, doorPanelSprite, "Door Panel Sprite");

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

            if (!(badgeHeightCells > 0f && badgeDigitWidthCells > 0f && badgePaddingCells >= 0f))
            {
                problems.Add($"{name}: Badge Height Cells and Badge Digit Width Cells must be positive, and Badge Padding Cells at least 0.");
            }

            if (!(badgeRimCells >= 0f && badgeRimCells < badgeHeightCells * 0.5f))
            {
                problems.Add($"{name}: Badge Rim Cells must be at least 0 and below half of Badge Height Cells.");
            }

            if (!(chainThicknessCells > 0f && chainThicknessCells <= 1f))
            {
                problems.Add($"{name}: Chain Thickness Cells must be above 0 and at most 1.");
            }

            if (!(chainEndInsetCells >= 0f && chainEndInsetCells < 0.5f))
            {
                problems.Add($"{name}: Chain End Inset Cells must be at least 0 and below 0.5.");
            }

            if (!(padlockSizeCells > 0f && keySizeCells > 0f))
            {
                problems.Add($"{name}: Padlock Size Cells and Key Size Cells must be positive.");
            }

            if (!(shutterBorderCells >= 0f && elevatorBorderCells >= 0f && elevatorDividerCells >= 0f && machineCornerCells >= 0f))
            {
                problems.Add($"{name}: Shutter Border Cells, Elevator Border Cells, Elevator Divider Cells and Machine Corner Cells may not be negative.");
            }

            foreach (var problem in GeneratorMachine.Problems(
                         machineDepthCells, machineSideOverhangCells, machineFrameOverlapCells, machineScreenInsetCells))
            {
                problems.Add($"{name}: {problem}");
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

        /// <summary>
        /// Sets every sorting order back to its default and leaves every other
        /// field as it is: the way to take up a new drawing order without
        /// losing tuned colours, sizes and sprite assignments.
        /// </summary>
        [ContextMenu("Reset Sorting Orders")]
        private void ResetSortingOrders()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Reset Sorting Orders");
#endif
            backgroundOrder = DefaultBackgroundOrder;
            vignetteOrder = DefaultVignetteOrder;
            floorUnderlayOrder = DefaultFloorUnderlayOrder;
            floorOrder = DefaultFloorOrder;
            elevatorDoorOrder = DefaultElevatorDoorOrder;
            elevatorDividerOrder = DefaultElevatorDividerOrder;
            elevatorBorderOrder = DefaultElevatorBorderOrder;
            machineLipOrder = DefaultMachineLipOrder;
            machineOrder = DefaultMachineOrder;
            machineScreenOrder = DefaultMachineScreenOrder;
            miniatureLipOrder = DefaultMiniatureLipOrder;
            miniatureOrder = DefaultMiniatureOrder;
            frameLipOrder = DefaultFrameLipOrder;
            frameOrder = DefaultFrameOrder;
            gateMarkOrder = DefaultGateMarkOrder;
            blockLipOrder = DefaultBlockLipOrder;
            blockOrder = DefaultBlockOrder;
            studOrder = DefaultStudOrder;
            glossOrder = DefaultGlossOrder;
            peelOrder = DefaultPeelOrder;
            beneathColorOrder = DefaultBeneathColorOrder;
            chainOrder = DefaultChainOrder;
            iconOrder = DefaultIconOrder;
            keyGemOrder = DefaultKeyGemOrder;
            shutterOrder = DefaultShutterOrder;
            shutterBorderOrder = DefaultShutterBorderOrder;
            badgeOrder = DefaultBadgeOrder;
            badgeRimOrder = DefaultBadgeRimOrder;
            labelOrder = DefaultLabelOrder;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
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
