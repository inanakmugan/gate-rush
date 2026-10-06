using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// settings, label settings, the settle, the lift and every feedback
    /// animation (Module 18), the HUD, the result panel and the introduction
    /// cards with their texts (Module 19). Nothing in <c>GateRush.Runtime</c> hardcodes one of
    /// these at a call site. Board sizes are in cells unless named otherwise,
    /// so the board keeps its proportions whatever <see cref="CellSize"/> is;
    /// HUD and panel sizes are in canvas units of a 1080 × 1920 portrait
    /// screen, the canvas's reference resolution.
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
        private const int DefaultLayerEdgeOrder = 14;
        private const int DefaultLayerOrder = 15;
        private const int DefaultStudOrder = 16;
        private const int DefaultGlossOrder = 17;
        private const int DefaultChainOrder = 18;
        private const int DefaultIconOrder = 19;
        private const int DefaultKeyGemOrder = 20;
        private const int DefaultShutterOrder = 21;
        private const int DefaultShutterBorderOrder = 22;
        private const int DefaultBadgeRimOrder = 23;
        private const int DefaultBadgeOrder = 24;
        private const int DefaultLabelOrder = 25;

        // Compared only inside a lifted block's sorting group, where it has to
        // sort below the block's lip and nothing else. Sharing a value with
        // another board layer (the gate marks) is harmless: nothing outside
        // the group is compared with it.
        private const int DefaultOutlineOrder = DefaultBlockLipOrder - 1;

        /// <summary>
        /// The cube burst's maximum delay. Not a tunable: a stream spreads its
        /// cubes' starts over the block's pass and never reads it.
        /// </summary>
        private const float CubeStreamSetsItsOwnDelays = 0f;

        /// <summary>
        /// The longest time bonus <see cref="Problems"/> sizes a time-bonus
        /// mark for when it checks that two marks sharing a block cannot
        /// overlap: two digits. Not a tunable; a longer bonus widens its mark
        /// past what was checked.
        /// </summary>
        private const int WidestBonusSecondsChecked = 99;

        /// <summary>
        /// The deepest layer count <see cref="Problems"/> sizes a layer badge
        /// for when it checks that marks sharing a block cannot overlap: two
        /// digits. Not a tunable; a deeper stack widens its badge past what
        /// was checked.
        /// </summary>
        private const int DeepestLayerCountChecked = 99;

        private const int DefaultLiftedBlockOrder = 26;
        private const int DefaultEffectOrder = 27;

        [Header("Assets")]
        [Tooltip("A plain white square sprite, tinted and scaled for the floor's backing, and the shape of the mask that clips a block passing through its gate.")]
        [SerializeField] private Sprite cellSprite;

        [Tooltip("Sprite-Unlit-Default. Under the 2D Renderer a lit sprite renders black with no Light 2D.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip("TextMeshPro font asset for every label: the board's counts, the HUD and the result panel. Lilita One (D46), created from Assets/Art/Fonts.")]
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
        [SerializeField] private Sprite clockSprite;
        [SerializeField] private Sprite restartSprite;
        [SerializeField] private Sprite cubeSprite;
        [SerializeField] private Sprite shardSprite;
        [SerializeField] private Sprite gateGlowSprite;
        [SerializeField] private Sprite sparkleSprite;

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

        [Header("Layered blocks (M4)")]
        [Tooltip("How far the inner shape, in the colour beneath, sits inside a layered block's footprint, in cells: above 0, below 0.5. The outer colour shows as a rim of about this width.")]
        [SerializeField] private float layerInsetCells = 0.1f;

        [Tooltip("Width of the darker edge round the inner shape, in cells: at least 0, below Layer Inset Cells. Its colour is the colour beneath, darkened as a lip is.")]
        [SerializeField] private float layerEdgeCells = 0.02f;

        [Tooltip("Scale of a layered block's studs about each cell's centre, so they stay on the inner shape: above 0, at most 1. A peel grows them back to 1.")]
        [SerializeField] private float layerStudScale = 0.9f;

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

        [Header("Time-bonus mark (M10; the count badge's height, padding, digit width, rim thickness and text)")]
        [Tooltip("Text on a time-bonus block's mark; {0} is the seconds it adds.")]
        [SerializeField] private string timeBonusMarkFormat = "+{0}";

        [Tooltip("Size of the clock icon on the mark, in cells.")]
        [SerializeField] private float timeBonusMarkIconCells = 0.24f;

        [Tooltip("Space between the clock icon and the text, in cells.")]
        [SerializeField] private float timeBonusMarkIconGapCells = 0.03f;

        [SerializeField] private Color timeBonusMarkColor = new Color(0.20f, 0.62f, 0.30f);
        [SerializeField] private Color timeBonusMarkRimColor = new Color(0.62f, 0.95f, 0.62f);
        [SerializeField] private Color timeBonusMarkIconColor = Color.white;

        [Tooltip("The scale of two marks — a padlock or key, a layer count, a time-bonus mark — that share a block with no room for both at full size (a 1×1, an L, a T): above 0, at most 1.")]
        [SerializeField] private float crowdedMarkScale = 0.6f;

        [Tooltip("The scale of three marks — a padlock or key, a layer count and a time-bonus mark — that share a block with no room for a cell each: above 0, at most Crowded Mark Scale.")]
        [SerializeField] private float crowdedTripleMarkScale = 0.45f;

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

        [Header("Grab lift")]
        [Tooltip("How much a grabbed block grows about its centre while it is held: at least 1.")]
        [SerializeField] private float liftScale = 1.06f;

        [Tooltip("Seconds a grabbed block takes to lift, and to drop back when released.")]
        [SerializeField] private float liftSeconds = 0.08f;

        [SerializeField] private Ease liftEase = Ease.OutQuad;

        [Tooltip("How far a grabbed block's outline reaches past its face, in cells: above 0, below 0.5. On release it retracts to nothing.")]
        [SerializeField] private float outlineWidthCells = 0.07f;

        [SerializeField] private Color outlineColor = Color.white;

        [Header("Exit and peel")]
        [Tooltip("Seconds a destroyed block takes to pass one cell of its depth through its gate. Input never waits for it.")]
        [SerializeField] private float exitSecondsPerCell = 0.12f;

        [Tooltip("Linear passes the block through at a steady speed.")]
        [SerializeField] private Ease exitEase = Ease.Linear;

        [Tooltip("Seconds a surviving layered block takes to peel its removed outer colour: the rim fades and shrinks while the inner shape grows to the whole block.")]
        [SerializeField] private float peelSeconds = 0.2f;

        [SerializeField] private Ease peelEase = Ease.InQuad;

        [Tooltip("The part of a destroyed block's cube stream a peel gives, in the removed colour, from 0 (none) to 1 (as many as an exit). The Exit cubes values below set the rest.")]
        [SerializeField] private float peelCubeFraction = 0.5f;

        [Tooltip("How far a peeling block pushes into its gate and back within Peel Seconds, in cells: at least 0, below 0.5. 0 turns the bump off.")]
        [SerializeField] private float peelBumpCells = 0.15f;

        [Tooltip("Easing of the bump's way in, and mirrored, of its way back.")]
        [SerializeField] private Ease peelBumpEase = Ease.OutQuad;

        [Header("Exit cubes (stream out beneath the gate while a destroyed block passes through it)")]
        [Tooltip("Cubes per cell of the block, before the cap.")]
        [SerializeField] private int cubeCountPerCell = 8;

        [Tooltip("The most cubes one block streams out.")]
        [SerializeField] private int cubeCap = 40;

        [Tooltip("Side of a cube in cells: x the smallest, y the largest.")]
        [SerializeField] private Vector2 cubeSizeCells = new Vector2(0.12f, 0.22f);

        [Tooltip("How far a cube flies, in cells: x the shortest, y the longest.")]
        [SerializeField] private Vector2 cubeTravelCells = new Vector2(0.6f, 2f);

        [Tooltip("How far a cube's direction may turn from straight out through the gate, in degrees either way: at least 0, below 90.")]
        [SerializeField] private float cubeSpreadDegrees = 35f;

        [Tooltip("The most a cube turns over its flight, in degrees either way.")]
        [SerializeField] private float cubeSpinDegrees = 270f;

        [Tooltip("A cube's scale at the end of its flight, from 0 to 1.")]
        [SerializeField] private float cubeEndScale = 0.3f;

        [Tooltip("Seconds one cube's flight takes. The stream spreads the cubes' starts over the block's pass.")]
        [SerializeField] private float cubeSeconds = 0.45f;

        [SerializeField] private Ease cubeEase = Ease.OutQuad;

        [Header("Gate glow (inside a gate while a destroyed block passes through it)")]
        [Tooltip("How far the glow reaches into the board from the gate, in cells.")]
        [SerializeField] private float gateGlowDepthCells = 0.6f;

        [Tooltip("How far the glow's colour moves from the gate's colour toward white, from 0 to 1.")]
        [SerializeField] private float gateGlowWhiten = 0.5f;

        [Tooltip("The glow's opacity at the gate while it holds, from 0 to 1.")]
        [SerializeField] private float gateGlowAlpha = 0.8f;

        [Tooltip("Seconds the glow takes to fade in once the block starts passing.")]
        [SerializeField] private float gateGlowFadeInSeconds = 0.06f;

        [Tooltip("Seconds the glow takes to fade out once the block is through.")]
        [SerializeField] private float gateGlowFadeOutSeconds = 0.15f;

        [SerializeField] private Ease gateGlowEase = Ease.OutQuad;

        [Header("Ice shards (a thawing block's or an opening gate's ice breaks into these)")]
        [Tooltip("Shards per cell of the block or of the gate, before the cap.")]
        [SerializeField] private int shardCountPerCell = 2;

        [Tooltip("The most shards one block or gate breaks into.")]
        [SerializeField] private int shardCap = 8;

        [Tooltip("Side of a shard in cells: x the smallest, y the largest.")]
        [SerializeField] private Vector2 shardSizeCells = new Vector2(0.12f, 0.22f);

        [Tooltip("How far a shard flies out from the centre, in cells: x the shortest, y the longest.")]
        [SerializeField] private Vector2 shardTravelCells = new Vector2(0.3f, 0.6f);

        [Tooltip("How far a shard's direction may turn from straight out from the centre, in degrees either way: at least 0, below 90.")]
        [SerializeField] private float shardSpreadDegrees = 30f;

        [Tooltip("The most a shard turns over its flight, in degrees either way.")]
        [SerializeField] private float shardSpinDegrees = 180f;

        [Tooltip("How far a shard falls over its flight on top of flying out, in cells.")]
        [SerializeField] private float shardFallCells = 0.3f;

        [Tooltip("A shard's scale at the end of its flight, from 0 to 1.")]
        [SerializeField] private float shardEndScale = 0.6f;

        [Tooltip("Seconds one shard's flight takes.")]
        [SerializeField] private float shardSeconds = 0.35f;

        [Tooltip("The latest a shard starts after the burst does, in seconds.")]
        [SerializeField] private float shardMaxDelaySeconds = 0.03f;

        [SerializeField] private Ease shardEase = Ease.OutQuad;

        [Header("Shutters, locks and badges")]
        [Tooltip("Seconds an opening shutter's panel takes to lift off: it shrinks toward its top edge while fading.")]
        [SerializeField] private float shutterLiftSeconds = 0.3f;

        [SerializeField] private Ease shutterLiftEase = Ease.InQuad;

        [Tooltip("Seconds an opening lock's padlock and chains take to fade out.")]
        [SerializeField] private float lockOpenSeconds = 0.25f;

        [Tooltip("The scale an opening lock's padlock and chains reach as they fade: at least 1.")]
        [SerializeField] private float lockOpenScale = 1.25f;

        [SerializeField] private Ease lockOpenEase = Ease.OutQuad;

        [Tooltip("The scale a badge pops to when its count goes down: above 1.")]
        [SerializeField] private float badgePopScale = 1.3f;

        [Tooltip("Seconds a badge's pop takes, up and back.")]
        [SerializeField] private float badgePopSeconds = 0.2f;

        [Header("Spawns")]
        [Tooltip("Seconds a generated block takes to grow from the machine's screen into its cells.")]
        [SerializeField] private float generatorSpawnSeconds = 0.25f;

        [SerializeField] private Ease generatorSpawnEase = Ease.OutQuad;

        [Tooltip("Seconds an elevator's doors take to slide apart before a wave rises.")]
        [SerializeField] private float doorsOpenSeconds = 0.12f;

        [Tooltip("Seconds an elevator's doors take to close again beneath the risen wave.")]
        [SerializeField] private float doorsCloseSeconds = 0.1f;

        [SerializeField] private Ease doorsEase = Ease.InOutQuad;

        [Tooltip("Seconds a wave takes to rise.")]
        [SerializeField] private float riseSeconds = 0.25f;

        [Tooltip("A rising block's scale when it starts: above 0, at most 1.")]
        [SerializeField] private float riseStartScale = 0.85f;

        [Tooltip("How far below its cells a rising block starts, in cells.")]
        [SerializeField] private float riseDropCells = 0.2f;

        [Tooltip("An easing with an overshoot, such as OutBack, settles the wave with a small bounce.")]
        [SerializeField] private Ease riseEase = Ease.OutBack;

        [Header("HUD (canvas units of a 1080 × 1920 portrait screen)")]
        [Tooltip("Space between the safe area's left and right edges and the restart button and level pill, and the least space kept between them and the timer.")]
        [SerializeField] private float hudSidePaddingUnits = 40f;

        [Tooltip("Side of the square restart button. It is tinted the frame's colour.")]
        [SerializeField] private float hudRestartSizeUnits = 120f;

        [Tooltip("Corner radius of the restart button.")]
        [SerializeField] private float hudRestartCornerUnits = 30f;

        [Tooltip("Size of the restart icon on the button. At most the button's size.")]
        [SerializeField] private float hudRestartIconSizeUnits = 72f;

        [Tooltip("Height of the timer and level pills; their corners are half of it.")]
        [SerializeField] private float hudPillHeightUnits = 96f;

        [SerializeField] private float timerPillWidthUnits = 280f;
        [SerializeField] private float levelPillWidthUnits = 260f;

        [Tooltip("Size of the clock icon in the timer pill. At most the pill's height.")]
        [SerializeField] private float hudClockIconSizeUnits = 60f;

        [Tooltip("Space between the clock icon and the digits.")]
        [SerializeField] private float hudIconGapUnits = 12f;

        [Tooltip("Font size of the timer's digits and the level pill's text.")]
        [SerializeField] private float hudFontSize = 56f;

        [Tooltip("The timer's digits turn Timer Warning Color once the displayed second is at most this. 0 turns them red only at 00:00.")]
        [SerializeField] private int timerWarningSeconds = 10;

        [SerializeField] private Color hudPillColor = new Color(0.08f, 0.08f, 0.16f, 0.9f);
        [SerializeField] private Color hudTextColor = Color.white;

        [Tooltip("Tint of the clock and restart icons.")]
        [SerializeField] private Color hudIconColor = Color.white;

        [SerializeField] private Color timerWarningColor = new Color(1f, 0.32f, 0.32f);

        [Tooltip("Text of the level pill; {0} is the level's 1-based position in level id order.")]
        [SerializeField] private string levelLabelFormat = "Level {0}";

        [Tooltip("Text shown next to the timer when a move earns a time bonus (M10); {0} is the seconds earned.")]
        [SerializeField] private string timeBonusFormat = "+{0} s";

        [SerializeField] private Color timeBonusColor = new Color(0.45f, 1f, 0.45f);
        [SerializeField] private float timeBonusFontSize = 56f;

        [Tooltip("Space between the timer pill's right end and the time bonus text.")]
        [SerializeField] private float timeBonusGapUnits = 16f;

        [Tooltip("How far the time bonus text rises while it fades.")]
        [SerializeField] private float timeBonusRiseUnits = 60f;

        [Tooltip("Seconds the time bonus text takes to rise and fade. Runs on unscaled time.")]
        [SerializeField] private float timeBonusSeconds = 0.9f;

        [SerializeField] private Ease timeBonusEase = Ease.OutQuad;

        [Tooltip("The scale the timer pill pulses to when a bonus is earned: at least 1.")]
        [SerializeField] private float timerPulseScale = 1.15f;

        [Tooltip("Seconds the timer pill's pulse takes, up and back.")]
        [SerializeField] private float timerPulseSeconds = 0.25f;

        [Header("Result panel (canvas units; the panel is tinted the frame's colour)")]
        [SerializeField] private string winTitle = "Level Complete";

        [Tooltip("The win title after the last level in the level order, in place of Win Title.")]
        [SerializeField] private string allDoneTitle = "All Levels Complete";
        [SerializeField] private string lossTitle = "Time's Up";

        [Tooltip("The full-screen backdrop behind the panel; it covers the board and the HUD and swallows presses on them.")]
        [SerializeField] private Color resultBackdropColor = new Color(0f, 0f, 0f, 0.6f);

        [SerializeField] private Vector2 resultPanelSizeUnits = new Vector2(860f, 620f);
        [SerializeField] private float resultPanelCornerUnits = 60f;

        [Tooltip("Space inside the panel's edges, and between the title and the buttons.")]
        [SerializeField] private float resultPaddingUnits = 60f;

        [SerializeField] private float resultTitleFontSize = 110f;
        [SerializeField] private Color resultTitleColor = Color.white;
        [SerializeField] private Vector2 resultButtonSizeUnits = new Vector2(340f, 130f);
        [SerializeField] private float resultButtonCornerUnits = 40f;

        [Tooltip("Space between Next and Restart.")]
        [SerializeField] private float resultButtonGapUnits = 40f;

        [SerializeField] private float resultButtonFontSize = 64f;
        [SerializeField] private Color resultButtonTextColor = Color.white;
        [SerializeField] private Color nextButtonColor = new Color(0.30f, 0.78f, 0.32f);
        [SerializeField] private Color restartButtonColor = new Color(1.0f, 0.58f, 0.15f);
        [SerializeField] private string nextLabel = "Next";
        [SerializeField] private string restartLabel = "Restart";

        [Tooltip("Seconds the panel's opening pop takes: it fades in and grows from Result Pop Start Scale. Runs on unscaled time.")]
        [SerializeField] private float resultPopSeconds = 0.25f;

        [SerializeField] private Ease resultPopEase = Ease.OutBack;

        [Tooltip("The panel's scale when the pop starts; it ends at 1.")]
        [SerializeField] private float resultPopStartScale = 0.8f;

        [Header("Introduction cards (canvas units; backdrop and opening pop are the result panel's)")]
        [Tooltip("One entry per mechanic: the card's title and its one or two lines of text.")]
        [SerializeField] private MechanicIntroduction[] mechanicIntroductions =
        {
            new MechanicIntroduction(LevelMechanic.HowToPlay, "How to Play", "Drag each block out through a gate of its colour!"),
            new MechanicIntroduction(LevelMechanic.IceBlock, "Ice Block!", "Clear blocks to melt the ice. The number shows how many."),
            new MechanicIntroduction(LevelMechanic.IceDoor, "Ice Door!", "Clear blocks to crack open the Ice Door!"),
            new MechanicIntroduction(LevelMechanic.LayeredBlock, "Layered Block!", "Each exit peels one layer. The colour beneath goes next."),
            new MechanicIntroduction(LevelMechanic.OneWayBlock, "One-Way Block!", "This block only slides along its arrow."),
            new MechanicIntroduction(LevelMechanic.LockAndKey, "Lock & Key!", "Clear the block with the key to open the lock of its colour."),
            new MechanicIntroduction(LevelMechanic.Shutter, "Shutter!", "Clear blocks to lift the shutter and see what is beneath."),
            new MechanicIntroduction(LevelMechanic.Generator, "Generator!", "The machine sends its next block when there is room. Its screen shows what comes next."),
            new MechanicIntroduction(LevelMechanic.Elevator, "Elevator!", "Clear every block on the lift to bring up the next wave."),
            new MechanicIntroduction(LevelMechanic.TimeBonus, "Time Bonus!", "Clear this block to win extra seconds.")
        };

        [Tooltip("The line under every card's title but How to Play's.")]
        [SerializeField] private string introSubtitle = "New Item Unlocked!";

        [Tooltip("The line under the How to Play card's title.")]
        [SerializeField] private string howToPlaySubtitle = "Welcome!";

        [SerializeField] private float introTitleFontSize = 120f;

        [Tooltip("Height of the title's row.")]
        [SerializeField] private float introTitleHeightUnits = 160f;

        [Tooltip("Fill of the title. Its outline is the frame's lip colour: Frame Color darkened by Lip Darken.")]
        [SerializeField] private Color introTitleColor = Color.white;

        [Tooltip("Thickness of the title's outline, as TextMeshPro measures it: above 0, at most 1. It is set on the title's own material, so no other label changes.")]
        [SerializeField] private float introTitleOutlineWidth = 0.2f;

        [SerializeField] private float introSubtitleFontSize = 56f;

        [Tooltip("Height of the subtitle's row.")]
        [SerializeField] private float introSubtitleHeightUnits = 80f;

        [SerializeField] private Color introSubtitleColor = Color.white;

        [Tooltip("The area the illustration is centred in, and scaled down to when it is larger.")]
        [SerializeField] private Vector2 introIllustrationSizeUnits = new Vector2(640f, 480f);

        [Tooltip("Canvas units one board cell is drawn at in an illustration. Every other size in it is the board's own, in cells.")]
        [SerializeField] private float introCellUnits = 170f;

        [Tooltip("Size of the arrow between the block and its gate on the How to Play card, in cells.")]
        [SerializeField] private float introHowToPlayArrowCells = 0.6f;

        [Tooltip("The block's colour in every illustration, and the lock's.")]
        [SerializeField] private BlockColor introBlockColor = BlockColor.Blue;

        [Tooltip("The colour beneath on the Layered Block card and the key carrier's on the Lock & Key card. Not Intro Block Color.")]
        [SerializeField] private BlockColor introSecondColor = BlockColor.Yellow;

        [Tooltip("The number on every count badge in an illustration, and the blocks the Generator card's machine has queued: at least 1.")]
        [SerializeField] private int introCount = 3;

        [Tooltip("The seconds on the Time Bonus card's mark: at least 1.")]
        [SerializeField] private int introBonusSeconds = 5;

        [SerializeField] private Vector2 introTextBoxSizeUnits = new Vector2(900f, 260f);
        [SerializeField] private float introTextBoxCornerUnits = 48f;

        [Tooltip("Thickness of the text box's border, which is the frame's colour. Below the corner and below half the box's height.")]
        [SerializeField] private float introTextBoxBorderUnits = 10f;

        [SerializeField] private Color introTextBoxColor = new Color(1.00f, 0.95f, 0.82f);

        [Tooltip("Space between the text box's edges and its text.")]
        [SerializeField] private float introTextPaddingUnits = 40f;

        [SerializeField] private float introTextFontSize = 52f;
        [SerializeField] private Color introTextColor = new Color(0.20f, 0.14f, 0.30f);

        [Tooltip("Space between the card's rows: close button, title, subtitle, illustration, text box.")]
        [SerializeField] private float introSpacingUnits = 36f;

        [Tooltip("Diameter of the round close button at the card's top right.")]
        [SerializeField] private float introCloseSizeUnits = 110f;

        [SerializeField] private Color introCloseColor = new Color(0.90f, 0.22f, 0.25f);

        [Tooltip("Length of each stroke of the close button's cross. At most the button's diameter.")]
        [SerializeField] private float introCloseCrossSizeUnits = 56f;

        [Tooltip("Thickness of each stroke of the cross. At most its length.")]
        [SerializeField] private float introCloseCrossThicknessUnits = 14f;

        [SerializeField] private Color introCloseCrossColor = Color.white;

        [Tooltip("Seconds a closing card takes to fade out. Runs on unscaled time.")]
        [SerializeField] private float introCloseSeconds = 0.15f;

        [SerializeField] private Ease introCloseEase = Ease.OutQuad;

        [Header("Sparkles (around a card's illustration)")]
        [Tooltip("One entry per sparkle: where it sits, from the illustration area's centre, as a fraction of the area's size; its size in canvas units; and where in the twinkle it starts, from 0 to below 1.")]
        [SerializeField] private SparklePlacement[] introSparkles =
        {
            new SparklePlacement(new Vector2(-0.52f, 0.40f), 64f, 0f),
            new SparklePlacement(new Vector2(0.50f, 0.46f), 44f, 0.35f),
            new SparklePlacement(new Vector2(0.56f, -0.20f), 56f, 0.6f),
            new SparklePlacement(new Vector2(-0.46f, -0.42f), 40f, 0.8f),
            new SparklePlacement(new Vector2(0.05f, 0.58f), 36f, 0.2f)
        };

        [SerializeField] private Color sparkleColor = new Color(1f, 1f, 0.85f);

        [Tooltip("Seconds one twinkle takes, from small and faint to full and back. Runs on unscaled time.")]
        [SerializeField] private float sparkleTwinkleSeconds = 1.2f;

        [Tooltip("A sparkle's scale at the faint end of its twinkle, from 0 to 1.")]
        [SerializeField] private float sparkleMinScale = 0.45f;

        [Tooltip("A sparkle's opacity at the faint end of its twinkle, from 0 to 1.")]
        [SerializeField] private float sparkleMinAlpha = 0.2f;

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
        [Tooltip("Font size of the number on a badge.")]
        [SerializeField] private float badgeLabelFontSize = 2.5f;

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

        [Tooltip("The darker edge round a layered block's inner shape: above Block Order, below Layer Order.")]
        [SerializeField] private int layerEdgeOrder = DefaultLayerEdgeOrder;

        [Tooltip("A layered block's inner shape, in the colour beneath: above Layer Edge Order, below Stud Order.")]
        [SerializeField] private int layerOrder = DefaultLayerOrder;

        [Tooltip("Studs, and a frozen block's frost, which has no studs.")]
        [SerializeField] private int studOrder = DefaultStudOrder;

        [Tooltip("Stud gloss, and an axis-restricted block's arrow, which has no studs.")]
        [SerializeField] private int glossOrder = DefaultGlossOrder;

        [SerializeField] private int chainOrder = DefaultChainOrder;

        [Tooltip("A padlock or a key.")]
        [SerializeField] private int iconOrder = DefaultIconOrder;

        [SerializeField] private int keyGemOrder = DefaultKeyGemOrder;
        [SerializeField] private int shutterOrder = DefaultShutterOrder;
        [SerializeField] private int shutterBorderOrder = DefaultShutterBorderOrder;

        [Tooltip("A badge's rim: a full-size rounded box behind its fill.")]
        [SerializeField] private int badgeRimOrder = DefaultBadgeRimOrder;

        [SerializeField] private int badgeOrder = DefaultBadgeOrder;

        [Tooltip("Every label: the numbers on badges, in front of every other board layer. A time-bonus mark's clock icon shares it.")]
        [SerializeField] private int labelOrder = DefaultLabelOrder;

        [Tooltip("A grabbed block's outline. Compared only inside the lifted block's sorting group, where it must sort below Block Lip Order; equal to another board layer's order is fine.")]
        [SerializeField] private int outlineOrder = DefaultOutlineOrder;

        [Tooltip("Sorting group order of a grabbed block, lifted above every other board layer.")]
        [SerializeField] private int liftedBlockOrder = DefaultLiftedBlockOrder;

        [Tooltip("Cubes and ice shards, above everything else on the board.")]
        [SerializeField] private int effectOrder = DefaultEffectOrder;

        /// <summary>The white square the floor's backing is drawn with, and the shape of a passing block's clip mask.</summary>
        public Sprite CellSprite => cellSprite;

        /// <summary>The material every sprite uses: <c>Sprite-Unlit-Default</c>.</summary>
        public Material SpriteMaterial => spriteMaterial;

        /// <summary>The font of every label, on the board and in the HUD and result panel: Lilita One.</summary>
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

        /// <summary>The HUD's white clock icon, tinted <see cref="HudIconColor"/>.</summary>
        public Sprite ClockSprite => clockSprite;

        /// <summary>The HUD's white restart arrow, tinted <see cref="HudIconColor"/>.</summary>
        public Sprite RestartSprite => restartSprite;

        /// <summary>One cube of a destroyed block's burst, tinted the block's colour.</summary>
        public Sprite CubeSprite => cubeSprite;

        /// <summary>One ice shard, tinted <see cref="IceColor"/>.</summary>
        public Sprite ShardSprite => shardSprite;

        /// <summary>A soft white gradient, strongest along its bottom edge: the glow inside a gate a block is passing.</summary>
        public Sprite GateGlowSprite => gateGlowSprite;

        /// <summary>A white four-point star: the sparkles around an introduction card's illustration, tinted <see cref="SparkleColor"/>.</summary>
        public Sprite SparkleSprite => sparkleSprite;

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

        /// <summary>How far a layered block's inner shape sits inside its footprint, in cells (<see cref="LayerInset"/>).</summary>
        public float LayerInsetCells => layerInsetCells;

        /// <summary>Width of the darker edge round a layered block's inner shape, in cells.</summary>
        public float LayerEdgeCells => layerEdgeCells;

        /// <summary>Scale of a layered block's studs about each cell's centre.</summary>
        public float LayerStudScale => layerStudScale;

        /// <summary>Height of a count badge, in cells.</summary>
        public float BadgeHeightCells => badgeHeightCells;

        /// <summary>Space either side of a badge's digits, in cells.</summary>
        public float BadgePaddingCells => badgePaddingCells;

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

        /// <summary>Size of the clock icon on a time-bonus mark, in cells.</summary>
        public float TimeBonusMarkIconCells => timeBonusMarkIconCells;

        /// <summary>Space between a time-bonus mark's clock icon and its text, in cells.</summary>
        public float TimeBonusMarkIconGapCells => timeBonusMarkIconGapCells;

        /// <summary>Fill of a time-bonus mark.</summary>
        public Color TimeBonusMarkColor => timeBonusMarkColor;

        /// <summary>Rim of a time-bonus mark.</summary>
        public Color TimeBonusMarkRimColor => timeBonusMarkRimColor;

        /// <summary>Tint of a time-bonus mark's clock icon.</summary>
        public Color TimeBonusMarkIconColor => timeBonusMarkIconColor;

        /// <summary>The scale of two marks sharing a block without room for both at full size (<see cref="MarkLayout.Row"/>).</summary>
        public float CrowdedMarkScale => crowdedMarkScale;

        /// <summary>The scale of three marks sharing a block without room for a cell each (<see cref="MarkLayout.Row"/>).</summary>
        public float CrowdedTripleMarkScale => crowdedTripleMarkScale;

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

        /// <summary>How much a grabbed block grows while held.</summary>
        public float LiftScale => liftScale;

        /// <summary>Seconds a grabbed block takes to lift or drop.</summary>
        public float LiftSeconds => liftSeconds;

        /// <summary>Easing of the lift and the drop.</summary>
        public Ease LiftEase => liftEase;

        /// <summary>How far a grabbed block's outline reaches past its face, in cells.</summary>
        public float OutlineWidthCells => outlineWidthCells;

        /// <summary>Colour of a grabbed block's outline.</summary>
        public Color OutlineColor => outlineColor;

        /// <summary>Seconds a destroyed block takes to pass one cell of its depth through its gate.</summary>
        public float ExitSecondsPerCell => exitSecondsPerCell;

        /// <summary>Easing of a destroyed block's pass through its gate.</summary>
        public Ease ExitEase => exitEase;

        /// <summary>How far the gate glow reaches into the board, in cells.</summary>
        public float GateGlowDepthCells => gateGlowDepthCells;

        /// <summary>How far the gate glow's colour moves toward white.</summary>
        public float GateGlowWhiten => gateGlowWhiten;

        /// <summary>The gate glow's opacity while it holds.</summary>
        public float GateGlowAlpha => gateGlowAlpha;

        /// <summary>Seconds the gate glow takes to fade in.</summary>
        public float GateGlowFadeInSeconds => gateGlowFadeInSeconds;

        /// <summary>Seconds the gate glow takes to fade out.</summary>
        public float GateGlowFadeOutSeconds => gateGlowFadeOutSeconds;

        /// <summary>Easing of the gate glow's fades.</summary>
        public Ease GateGlowEase => gateGlowEase;

        /// <summary>A cube's scale at the end of its flight.</summary>
        public float CubeEndScale => cubeEndScale;

        /// <summary>Seconds one cube's flight takes.</summary>
        public float CubeSeconds => cubeSeconds;

        /// <summary>Easing of a cube's flight.</summary>
        public Ease CubeEase => cubeEase;

        /// <summary>How far a shard falls over its flight, in cells.</summary>
        public float ShardFallCells => shardFallCells;

        /// <summary>A shard's scale at the end of its flight.</summary>
        public float ShardEndScale => shardEndScale;

        /// <summary>Seconds one shard's flight takes.</summary>
        public float ShardSeconds => shardSeconds;

        /// <summary>Easing of a shard's flight.</summary>
        public Ease ShardEase => shardEase;

        /// <summary>Seconds an opening shutter's lift takes.</summary>
        public float ShutterLiftSeconds => shutterLiftSeconds;

        /// <summary>Easing of an opening shutter's lift.</summary>
        public Ease ShutterLiftEase => shutterLiftEase;

        /// <summary>Seconds an opening lock takes to fade out.</summary>
        public float LockOpenSeconds => lockOpenSeconds;

        /// <summary>The scale an opening lock reaches as it fades.</summary>
        public float LockOpenScale => lockOpenScale;

        /// <summary>Easing of an opening lock.</summary>
        public Ease LockOpenEase => lockOpenEase;

        /// <summary>The scale a badge pops to.</summary>
        public float BadgePopScale => badgePopScale;

        /// <summary>Seconds a badge's pop takes, up and back.</summary>
        public float BadgePopSeconds => badgePopSeconds;

        /// <summary>Seconds a generated block takes to arrive from its machine.</summary>
        public float GeneratorSpawnSeconds => generatorSpawnSeconds;

        /// <summary>Easing of a generated block's arrival.</summary>
        public Ease GeneratorSpawnEase => generatorSpawnEase;

        /// <summary>Seconds an elevator's doors take to open.</summary>
        public float DoorsOpenSeconds => doorsOpenSeconds;

        /// <summary>Seconds an elevator's doors take to close.</summary>
        public float DoorsCloseSeconds => doorsCloseSeconds;

        /// <summary>Easing of an elevator's doors.</summary>
        public Ease DoorsEase => doorsEase;

        /// <summary>Seconds a wave takes to rise.</summary>
        public float RiseSeconds => riseSeconds;

        /// <summary>A rising block's starting scale.</summary>
        public float RiseStartScale => riseStartScale;

        /// <summary>How far below its cells a rising block starts, in cells.</summary>
        public float RiseDropCells => riseDropCells;

        /// <summary>Easing of a wave's rise.</summary>
        public Ease RiseEase => riseEase;

        /// <summary>Seconds a surviving layered block's peel takes.</summary>
        public float PeelSeconds => peelSeconds;

        /// <summary>Easing of the peel.</summary>
        public Ease PeelEase => peelEase;

        /// <summary>The part of a destroyed block's cube stream a peel gives, from 0 to 1 (<see cref="BurstLayout.StreamCount"/>).</summary>
        public float PeelCubeFraction => peelCubeFraction;

        /// <summary>How far a peeling block pushes into its gate and back, in cells; 0 for no bump.</summary>
        public float PeelBumpCells => peelBumpCells;

        /// <summary>Easing of each half of a peeling block's bump.</summary>
        public Ease PeelBumpEase => peelBumpEase;

        /// <summary>Result panel title after a win.</summary>
        public string WinTitle => winTitle;

        /// <summary>Result panel title after winning the last level (<see cref="ResultTitle"/>).</summary>
        public string AllDoneTitle => allDoneTitle;

        /// <summary>Result panel title after the countdown runs out.</summary>
        public string LossTitle => lossTitle;

        /// <summary>Space from the safe area's sides to the HUD's outer elements, and the least space beside the timer, in canvas units.</summary>
        public float HudSidePaddingUnits => hudSidePaddingUnits;

        /// <summary>Side of the square restart button, in canvas units.</summary>
        public float HudRestartSizeUnits => hudRestartSizeUnits;

        /// <summary>Corner radius of the restart button, in canvas units.</summary>
        public float HudRestartCornerUnits => hudRestartCornerUnits;

        /// <summary>Size of the restart icon, in canvas units.</summary>
        public float HudRestartIconSizeUnits => hudRestartIconSizeUnits;

        /// <summary>Height of the timer and level pills, in canvas units.</summary>
        public float HudPillHeightUnits => hudPillHeightUnits;

        /// <summary>Width of the timer pill, in canvas units.</summary>
        public float TimerPillWidthUnits => timerPillWidthUnits;

        /// <summary>Width of the level pill, in canvas units.</summary>
        public float LevelPillWidthUnits => levelPillWidthUnits;

        /// <summary>Size of the clock icon, in canvas units.</summary>
        public float HudClockIconSizeUnits => hudClockIconSizeUnits;

        /// <summary>Space between the clock icon and the digits, in canvas units.</summary>
        public float HudIconGapUnits => hudIconGapUnits;

        /// <summary>Font size of the HUD's text.</summary>
        public float HudFontSize => hudFontSize;

        /// <summary>The displayed second at or below which the timer turns <see cref="TimerWarningColor"/> (<see cref="TimeFormat.IsWarning"/>).</summary>
        public int TimerWarningSeconds => timerWarningSeconds;

        /// <summary>Tint of the timer and level pills.</summary>
        public Color HudPillColor => hudPillColor;

        /// <summary>Colour of the HUD's text.</summary>
        public Color HudTextColor => hudTextColor;

        /// <summary>Tint of the HUD's icons.</summary>
        public Color HudIconColor => hudIconColor;

        /// <summary>Colour of the timer's digits near the end.</summary>
        public Color TimerWarningColor => timerWarningColor;

        /// <summary>Composite format of the level pill; <c>{0}</c> is the level number.</summary>
        public string LevelLabelFormat => levelLabelFormat;

        /// <summary>Composite format of the time bonus text; <c>{0}</c> is the seconds earned.</summary>
        public string TimeBonusFormat => timeBonusFormat;

        /// <summary>Colour of the time bonus text.</summary>
        public Color TimeBonusColor => timeBonusColor;

        /// <summary>Font size of the time bonus text.</summary>
        public float TimeBonusFontSize => timeBonusFontSize;

        /// <summary>Space between the timer pill and the time bonus text, in canvas units.</summary>
        public float TimeBonusGapUnits => timeBonusGapUnits;

        /// <summary>How far the time bonus text rises, in canvas units.</summary>
        public float TimeBonusRiseUnits => timeBonusRiseUnits;

        /// <summary>Seconds the time bonus text takes to rise and fade.</summary>
        public float TimeBonusSeconds => timeBonusSeconds;

        /// <summary>Easing of the time bonus text.</summary>
        public Ease TimeBonusEase => timeBonusEase;

        /// <summary>The scale the timer pill pulses to on a bonus.</summary>
        public float TimerPulseScale => timerPulseScale;

        /// <summary>Seconds the timer pill's pulse takes, up and back.</summary>
        public float TimerPulseSeconds => timerPulseSeconds;

        /// <summary>Colour of the full-screen backdrop behind the result panel.</summary>
        public Color ResultBackdropColor => resultBackdropColor;

        /// <summary>Size of the result panel, in canvas units.</summary>
        public Vector2 ResultPanelSizeUnits => resultPanelSizeUnits;

        /// <summary>Corner radius of the result panel, in canvas units.</summary>
        public float ResultPanelCornerUnits => resultPanelCornerUnits;

        /// <summary>Space inside the panel's edges and between title and buttons, in canvas units.</summary>
        public float ResultPaddingUnits => resultPaddingUnits;

        /// <summary>Font size of the result panel's title.</summary>
        public float ResultTitleFontSize => resultTitleFontSize;

        /// <summary>Colour of the result panel's title.</summary>
        public Color ResultTitleColor => resultTitleColor;

        /// <summary>Size of each result panel button, in canvas units.</summary>
        public Vector2 ResultButtonSizeUnits => resultButtonSizeUnits;

        /// <summary>Corner radius of the result panel's buttons, in canvas units.</summary>
        public float ResultButtonCornerUnits => resultButtonCornerUnits;

        /// <summary>Space between the result panel's buttons, in canvas units.</summary>
        public float ResultButtonGapUnits => resultButtonGapUnits;

        /// <summary>Font size of the result panel's button labels.</summary>
        public float ResultButtonFontSize => resultButtonFontSize;

        /// <summary>Colour of the result panel's button labels.</summary>
        public Color ResultButtonTextColor => resultButtonTextColor;

        /// <summary>Tint of the Next button.</summary>
        public Color NextButtonColor => nextButtonColor;

        /// <summary>Tint of the result panel's Restart button.</summary>
        public Color RestartButtonColor => restartButtonColor;

        /// <summary>Label of the Next button.</summary>
        public string NextLabel => nextLabel;

        /// <summary>Label of the result panel's Restart button.</summary>
        public string RestartLabel => restartLabel;

        /// <summary>Seconds the result panel's opening pop takes.</summary>
        public float ResultPopSeconds => resultPopSeconds;

        /// <summary>Easing of the result panel's opening pop.</summary>
        public Ease ResultPopEase => resultPopEase;

        /// <summary>The result panel's scale when its pop starts.</summary>
        public float ResultPopStartScale => resultPopStartScale;

        /// <summary>Font size of an introduction card's title.</summary>
        public float IntroTitleFontSize => introTitleFontSize;

        /// <summary>Height of an introduction card's title row, in canvas units.</summary>
        public float IntroTitleHeightUnits => introTitleHeightUnits;

        /// <summary>Fill of an introduction card's title.</summary>
        public Color IntroTitleColor => introTitleColor;

        /// <summary>
        /// Outline of an introduction card's title: the frame's lip colour —
        /// <see cref="FrameColor"/> darkened as every lip is — so the title is
        /// edged in the frame's own purple, deep enough to hold white.
        /// </summary>
        public Color IntroTitleOutlineColor => LipFill(frameColor);

        /// <summary>Thickness of the title's outline, as TextMeshPro measures it.</summary>
        public float IntroTitleOutlineWidth => introTitleOutlineWidth;

        /// <summary>Font size of an introduction card's subtitle.</summary>
        public float IntroSubtitleFontSize => introSubtitleFontSize;

        /// <summary>Height of an introduction card's subtitle row, in canvas units.</summary>
        public float IntroSubtitleHeightUnits => introSubtitleHeightUnits;

        /// <summary>Colour of an introduction card's subtitle.</summary>
        public Color IntroSubtitleColor => introSubtitleColor;

        /// <summary>The area an illustration is centred in and fitted to, in canvas units.</summary>
        public Vector2 IntroIllustrationSizeUnits => introIllustrationSizeUnits;

        /// <summary>Canvas units one board cell is drawn at in an illustration.</summary>
        public float IntroCellUnits => introCellUnits;

        /// <summary>Size of the arrow between block and gate on the How to Play card, in cells.</summary>
        public float IntroHowToPlayArrowCells => introHowToPlayArrowCells;

        /// <summary>The block's colour in every illustration.</summary>
        public BlockColor IntroBlockColor => introBlockColor;

        /// <summary>The second colour in an illustration: the colour beneath, and the key carrier's.</summary>
        public BlockColor IntroSecondColor => introSecondColor;

        /// <summary>The number on an illustration's count badges.</summary>
        public int IntroCount => introCount;

        /// <summary>The seconds on the Time Bonus card's mark.</summary>
        public int IntroBonusSeconds => introBonusSeconds;

        /// <summary>Size of an introduction card's text box, in canvas units.</summary>
        public Vector2 IntroTextBoxSizeUnits => introTextBoxSizeUnits;

        /// <summary>Corner radius of the text box, in canvas units.</summary>
        public float IntroTextBoxCornerUnits => introTextBoxCornerUnits;

        /// <summary>Thickness of the text box's border, in canvas units.</summary>
        public float IntroTextBoxBorderUnits => introTextBoxBorderUnits;

        /// <summary>Fill of the text box.</summary>
        public Color IntroTextBoxColor => introTextBoxColor;

        /// <summary>Space between the text box's edges and its text, in canvas units.</summary>
        public float IntroTextPaddingUnits => introTextPaddingUnits;

        /// <summary>Font size of the text in the text box.</summary>
        public float IntroTextFontSize => introTextFontSize;

        /// <summary>Colour of the text in the text box.</summary>
        public Color IntroTextColor => introTextColor;

        /// <summary>Space between an introduction card's rows, in canvas units.</summary>
        public float IntroSpacingUnits => introSpacingUnits;

        /// <summary>Diameter of the close button, in canvas units.</summary>
        public float IntroCloseSizeUnits => introCloseSizeUnits;

        /// <summary>Tint of the close button.</summary>
        public Color IntroCloseColor => introCloseColor;

        /// <summary>Length of each stroke of the close button's cross, in canvas units.</summary>
        public float IntroCloseCrossSizeUnits => introCloseCrossSizeUnits;

        /// <summary>Thickness of each stroke of the cross, in canvas units.</summary>
        public float IntroCloseCrossThicknessUnits => introCloseCrossThicknessUnits;

        /// <summary>Colour of the close button's cross.</summary>
        public Color IntroCloseCrossColor => introCloseCrossColor;

        /// <summary>Seconds a closing card takes to fade out.</summary>
        public float IntroCloseSeconds => introCloseSeconds;

        /// <summary>Easing of a closing card's fade.</summary>
        public Ease IntroCloseEase => introCloseEase;

        /// <summary>The sparkles around a card's illustration.</summary>
        public IReadOnlyList<SparklePlacement> IntroSparkles => introSparkles;

        /// <summary>Tint of the sparkles.</summary>
        public Color SparkleColor => sparkleColor;

        /// <summary>Seconds one twinkle takes.</summary>
        public float SparkleTwinkleSeconds => sparkleTwinkleSeconds;

        /// <summary>A sparkle's scale at the faint end of its twinkle.</summary>
        public float SparkleMinScale => sparkleMinScale;

        /// <summary>A sparkle's opacity at the faint end of its twinkle.</summary>
        public float SparkleMinAlpha => sparkleMinAlpha;

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

        /// <summary>Font size of the number on a badge.</summary>
        public float BadgeLabelFontSize => badgeLabelFontSize;

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

        /// <summary>Sorting order of the darker edge round a layered block's inner shape.</summary>
        public int LayerEdgeOrder => layerEdgeOrder;

        /// <summary>Sorting order of a layered block's inner shape.</summary>
        public int LayerOrder => layerOrder;

        /// <summary>Sorting order of a block's studs, and of a frozen block's frost.</summary>
        public int StudOrder => studOrder;

        /// <summary>Sorting order of the stud highlights and of an axis-restricted block's arrow.</summary>
        public int GlossOrder => glossOrder;

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

        /// <summary>Sorting order of every label, in front of every other board layer.</summary>
        public int LabelOrder => labelOrder;

        /// <summary>Sorting order of a grabbed block's outline, compared only inside its lifted sorting group.</summary>
        public int OutlineOrder => outlineOrder;

        /// <summary>Sorting group order of a grabbed block.</summary>
        public int LiftedBlockOrder => liftedBlockOrder;

        /// <summary>Sorting order of cubes and ice shards.</summary>
        public int EffectOrder => effectOrder;

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

        /// <summary>The text on the time-bonus mark of a block that adds <paramref name="seconds"/> (M10).</summary>
        public string TimeBonusMarkText(int seconds) =>
            string.Format(CultureInfo.InvariantCulture, timeBonusMarkFormat, seconds);

        /// <summary>The width, in cells, of the time-bonus mark reading <paramref name="text"/>.</summary>
        public float TimeBonusMarkWidthCells(string text) =>
            MarkLayout.BonusBadgeWidth(
                text.Length, badgeHeightCells, badgeDigitWidthCells, badgePaddingCells,
                timeBonusMarkIconCells, timeBonusMarkIconGapCells);

        /// <summary>The title of <paramref name="mechanic"/>'s introduction card.</summary>
        /// <exception cref="InvalidOperationException">No entry introduces it; see <see cref="Problems"/>.</exception>
        public string IntroductionTitle(LevelMechanic mechanic) => IntroductionOf(mechanic).Title;

        /// <summary>The text in the text box of <paramref name="mechanic"/>'s introduction card.</summary>
        /// <exception cref="InvalidOperationException">No entry introduces it; see <see cref="Problems"/>.</exception>
        public string IntroductionText(LevelMechanic mechanic) => IntroductionOf(mechanic).Text;

        /// <summary>The line under the title of <paramref name="mechanic"/>'s card: How to Play has its own.</summary>
        public string IntroductionSubtitle(LevelMechanic mechanic) =>
            mechanic == LevelMechanic.HowToPlay ? howToPlaySubtitle : introSubtitle;

        /// <summary>The first entry for <paramref name="mechanic"/>.</summary>
        private MechanicIntroduction IntroductionOf(LevelMechanic mechanic)
        {
            if (mechanicIntroductions != null)
            {
                for (var i = 0; i < mechanicIntroductions.Length; i++)
                {
                    if (mechanicIntroductions[i] != null && mechanicIntroductions[i].Mechanic == mechanic)
                    {
                        return mechanicIntroductions[i];
                    }
                }
            }

            throw new InvalidOperationException($"{name}: Mechanic Introductions has no entry for {mechanic}.");
        }

        /// <summary>
        /// The layout of the cubes a destroyed block streams out. Its maximum
        /// delay is <see cref="CubeStreamSetsItsOwnDelays"/>: a stream spreads
        /// its cubes' starts over the block's pass (<see cref="BurstLayout.Stream"/>),
        /// so no delay is configured.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A cube value is out of range; see <see cref="Problems"/>.</exception>
        public BurstSettings CreateCubeBurst() =>
            new BurstSettings(
                cubeCountPerCell, cubeCap, cubeSizeCells.x, cubeSizeCells.y, cubeTravelCells.x, cubeTravelCells.y,
                cubeSpreadDegrees, cubeSpinDegrees, CubeStreamSetsItsOwnDelays);

        /// <summary>The layout of the ice shards of a thaw or a gate opening.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A shard value is out of range; see <see cref="Problems"/>.</exception>
        public BurstSettings CreateShardBurst() =>
            new BurstSettings(
                shardCountPerCell, shardCap, shardSizeCells.x, shardSizeCells.y, shardTravelCells.x, shardTravelCells.y,
                shardSpreadDegrees, shardSpinDegrees, shardMaxDelaySeconds);

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

            if (labelFont == null)
            {
                problems.Add($"{name}: Label Font is not assigned; create the Lilita One font asset from Assets/Art/Fonts and assign it.");
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
            AddIfUnassigned(problems, clockSprite, "Clock Sprite");
            AddIfUnassigned(problems, restartSprite, "Restart Sprite");
            AddIfUnassigned(problems, cubeSprite, "Cube Sprite");
            AddIfUnassigned(problems, shardSprite, "Shard Sprite");
            AddIfUnassigned(problems, gateGlowSprite, "Gate Glow Sprite");
            AddIfUnassigned(problems, sparkleSprite, "Sparkle Sprite");

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

            if (!(peelSeconds > 0f))
            {
                problems.Add($"{name}: Peel Seconds must be positive.");
            }

            AddLayerProblems(problems);
            AddFeedbackProblems(problems);

            if (blockPalette == null || blockPalette.Length < colourCount)
            {
                problems.Add($"{name}: Block Palette needs {colourCount} entries, one per BlockColor.");
            }

            AddHudProblems(problems);
            AddResultPanelProblems(problems);
            AddMarkProblems(problems);
            AddIntroductionProblems(problems);
            return problems;
        }

        /// <summary>
        /// The constraints on a layered block's inner shape (Module 20), each
        /// message naming its field. The inset is bounded by half a cell: an
        /// outer corner's quarter is pulled in on both sides and scales as a
        /// whole, its rounding with it, so the inner shape stays visible for
        /// every inset below that.
        /// </summary>
        private void AddLayerProblems(List<string> problems)
        {
            if (!(layerInsetCells > 0f && layerInsetCells < 0.5f))
            {
                problems.Add($"{name}: Layer Inset Cells must be above 0 and below 0.5, so a layered block keeps a visible inner shape.");
            }

            if (!(layerEdgeCells >= 0f && layerEdgeCells < layerInsetCells))
            {
                problems.Add($"{name}: Layer Edge Cells must be at least 0 and below Layer Inset Cells.");
            }

            if (!(layerStudScale > 0f && layerStudScale <= 1f))
            {
                problems.Add($"{name}: Layer Stud Scale must be above 0 and at most 1.");
            }

            if (!(blockOrder < layerEdgeOrder && layerEdgeOrder < layerOrder && layerOrder < studOrder))
            {
                problems.Add($"{name}: Layer Edge Order must be above Block Order, Layer Order above Layer Edge Order and Stud Order above Layer Order, so the inner shape sits on the face and under the studs.");
            }
        }

        /// <summary>
        /// The constraints on the time-bonus mark (M10) and on marks sharing a
        /// block, each message naming its field. The overlap bounds measure a
        /// key by its length, a time-bonus mark at
        /// <see cref="WidestBonusSecondsChecked"/> seconds and a layer badge at
        /// <see cref="DeepestLayerCountChecked"/> colours.
        /// </summary>
        private void AddMarkProblems(List<string> problems)
        {
            var isFormatUsable = IsUsableFormat(timeBonusMarkFormat);
            if (!isFormatUsable)
            {
                problems.Add($"{name}: Time Bonus Mark Format must be a format with at most one placeholder, {{0}}, for the seconds.");
            }

            if (!(timeBonusMarkIconCells > 0f && timeBonusMarkIconCells <= badgeHeightCells && timeBonusMarkIconGapCells >= 0f))
            {
                problems.Add($"{name}: Time Bonus Mark Icon Cells must be positive and at most Badge Height Cells, and Time Bonus Mark Icon Gap Cells at least 0.");
            }

            if (!(crowdedMarkScale > 0f && crowdedMarkScale <= 1f))
            {
                problems.Add($"{name}: Crowded Mark Scale must be above 0 and at most 1.");
                return;
            }

            if (!(crowdedTripleMarkScale > 0f && crowdedTripleMarkScale <= crowdedMarkScale))
            {
                problems.Add($"{name}: Crowded Triple Mark Scale must be above 0 and at most Crowded Mark Scale.");
                return;
            }

            if (!isFormatUsable)
            {
                return;
            }

            // Two marks with room sit a whole cell apart, so each may be a
            // cell wide; crowded ones sit half a cell apart at the crowded
            // scale, so together they may be a cell wide before scaling.
            var icon = Math.Max(padlockSizeCells, keySizeCells);
            var bonus = TimeBonusMarkWidthCells(TimeBonusMarkText(WidestBonusSecondsChecked));
            var roomyWidth = 2f * MarkLayout.RoomyOffsetCells;
            var crowdedWidth = 4f * MarkLayout.CrowdedOffsetCells;
            if (!(icon <= roomyWidth && bonus <= roomyWidth && crowdedMarkScale * (icon + bonus) <= crowdedWidth))
            {
                problems.Add(
                    $"{name}: a padlock or key and a time-bonus mark sharing a block would overlap. Padlock Size Cells, Key Size Cells and the " +
                    $"mark's width for a two-digit bonus ({bonus:0.##} cells, from Time Bonus Mark Icon Cells, the badge's digit width and padding) " +
                    $"must each be at most {roomyWidth:0.##}, and Crowded Mark Scale times the icon plus the mark at most {crowdedWidth:0.##}.");
            }

            // The layer badge pairs with either of them by the same two
            // bounds.
            var layer = BadgeWidthCells(DeepestLayerCountChecked);
            if (!(layer <= roomyWidth && crowdedMarkScale * (icon + layer) <= crowdedWidth
                  && crowdedMarkScale * (layer + bonus) <= crowdedWidth))
            {
                problems.Add(
                    $"{name}: a layer count badge sharing a block with a padlock, key or time-bonus mark would overlap it. The badge's width for a " +
                    $"two-digit count ({layer:0.##} cells, from the badge's height, digit width and padding) must be at most {roomyWidth:0.##}, and " +
                    $"Crowded Mark Scale times the badge plus the other mark at most {crowdedWidth:0.##}.");
            }

            // Three crowded marks sit a third of a cell apart with the layer
            // badge in the middle, so each neighbouring pair may be, at the
            // triple scale, two thirds of a cell wide together.
            var apart = MarkLayout.CrowdedTripleOffsetCells;
            if (!(crowdedTripleMarkScale * (icon + layer) * 0.5f <= apart && crowdedTripleMarkScale * (layer + bonus) * 0.5f <= apart))
            {
                problems.Add(
                    $"{name}: a padlock or key, a layer count badge and a time-bonus mark sharing a block would overlap. Crowded Triple Mark Scale " +
                    $"times half the badge plus half its neighbour — the icon, or the time-bonus mark — must be at most {apart:0.##}.");
            }
        }

        /// <summary>The constraints on the introduction cards' values (Module 19), each message naming its field or mechanic.</summary>
        private void AddIntroductionProblems(List<string> problems)
        {
            foreach (var mechanic in LevelMechanics.All)
            {
                var entries = 0;
                MechanicIntroduction first = null;
                for (var i = 0; mechanicIntroductions != null && i < mechanicIntroductions.Length; i++)
                {
                    if (mechanicIntroductions[i] != null && mechanicIntroductions[i].Mechanic == mechanic)
                    {
                        first = first ?? mechanicIntroductions[i];
                        entries++;
                    }
                }

                if (entries == 0)
                {
                    problems.Add($"{name}: Mechanic Introductions has no entry for {mechanic}.");
                    continue;
                }

                if (entries > 1)
                {
                    problems.Add($"{name}: Mechanic Introductions has {entries} entries for {mechanic}; it needs exactly one.");
                }

                if (string.IsNullOrWhiteSpace(first.Title))
                {
                    problems.Add($"{name}: Mechanic Introductions' entry for {mechanic} has no title.");
                }

                if (string.IsNullOrWhiteSpace(first.Text))
                {
                    problems.Add($"{name}: Mechanic Introductions' entry for {mechanic} has no text.");
                }
            }

            if (string.IsNullOrWhiteSpace(introSubtitle) || string.IsNullOrWhiteSpace(howToPlaySubtitle))
            {
                problems.Add($"{name}: Intro Subtitle and How To Play Subtitle may not be empty.");
            }

            if (!(introTitleFontSize > 0f && introTitleHeightUnits > 0f && introSubtitleFontSize > 0f
                  && introSubtitleHeightUnits > 0f && introTextFontSize > 0f))
            {
                problems.Add($"{name}: Intro Title Font Size, Intro Title Height Units, Intro Subtitle Font Size, Intro Subtitle Height Units and Intro Text Font Size must be positive.");
            }

            if (!(introTitleOutlineWidth > 0f && introTitleOutlineWidth <= 1f))
            {
                problems.Add($"{name}: Intro Title Outline Width must be above 0 and at most 1.");
            }

            if (!(introIllustrationSizeUnits.x > 0f && introIllustrationSizeUnits.y > 0f))
            {
                problems.Add($"{name}: Intro Illustration Size Units must be positive.");
            }

            AddIfNotPositive(problems, introCellUnits, "Intro Cell Units");
            AddIfNotPositive(problems, introHowToPlayArrowCells, "Intro How To Play Arrow Cells");

            if (introBlockColor == introSecondColor)
            {
                problems.Add($"{name}: Intro Second Color must differ from Intro Block Color: adjacent layers differ (M4), and a key's gem must stand out from its carrier.");
            }

            if (introCount < 1 || introBonusSeconds < 1)
            {
                problems.Add($"{name}: Intro Count and Intro Bonus Seconds must be at least 1.");
            }

            if (!(introTextBoxSizeUnits.x > 0f && introTextBoxSizeUnits.y > 0f && introTextBoxCornerUnits > 0f))
            {
                problems.Add($"{name}: Intro Text Box Size Units and Intro Text Box Corner Units must be positive.");
            }

            if (!(introTextBoxBorderUnits > 0f && introTextBoxBorderUnits < introTextBoxCornerUnits
                  && introTextBoxBorderUnits < introTextBoxSizeUnits.y * 0.5f))
            {
                problems.Add($"{name}: Intro Text Box Border Units must be positive, below Intro Text Box Corner Units and below half the box's height.");
            }

            if (!(introTextPaddingUnits >= 0f && 2f * introTextPaddingUnits < introTextBoxSizeUnits.x
                  && 2f * introTextPaddingUnits < introTextBoxSizeUnits.y))
            {
                problems.Add($"{name}: Intro Text Padding Units must be at least 0 and leave room for the text in Intro Text Box Size Units.");
            }

            if (!(introSpacingUnits >= 0f) || float.IsInfinity(introSpacingUnits))
            {
                problems.Add($"{name}: Intro Spacing Units must be at least 0 and finite.");
            }

            if (!(introCloseSizeUnits > 0f && introCloseCrossSizeUnits > 0f && introCloseCrossSizeUnits <= introCloseSizeUnits
                  && introCloseCrossThicknessUnits > 0f && introCloseCrossThicknessUnits <= introCloseCrossSizeUnits))
            {
                problems.Add($"{name}: Intro Close Size Units must be positive, Intro Close Cross Size Units positive and at most it, and Intro Close Cross Thickness Units positive and at most the cross's size.");
            }

            AddIfNotPositive(problems, introCloseSeconds, "Intro Close Seconds");
            AddIfNotPositive(problems, sparkleTwinkleSeconds, "Sparkle Twinkle Seconds");

            if (!(sparkleMinScale >= 0f && sparkleMinScale <= 1f && sparkleMinAlpha >= 0f && sparkleMinAlpha <= 1f))
            {
                problems.Add($"{name}: Sparkle Min Scale and Sparkle Min Alpha must be from 0 to 1.");
            }

            for (var i = 0; introSparkles != null && i < introSparkles.Length; i++)
            {
                var sparkle = introSparkles[i];
                if (sparkle == null || !(sparkle.SizeUnits > 0f && sparkle.Phase >= 0f && sparkle.Phase < 1f))
                {
                    problems.Add($"{name}: Intro Sparkles' entry {i} must have a positive size and a phase from 0 to below 1.");
                }
            }
        }

        /// <summary>The constraints on the feedback animations' values (Module 18), each message naming its field.</summary>
        private void AddFeedbackProblems(List<string> problems)
        {
            if (!(liftScale >= 1f) || float.IsInfinity(liftScale))
            {
                problems.Add($"{name}: Lift Scale must be at least 1 and finite.");
            }

            if (!(outlineWidthCells > 0f && outlineWidthCells < 0.5f))
            {
                problems.Add($"{name}: Outline Width Cells must be above 0 and below 0.5.");
            }

            if (!(gateGlowWhiten >= 0f && gateGlowWhiten <= 1f && gateGlowAlpha >= 0f && gateGlowAlpha <= 1f))
            {
                problems.Add($"{name}: Gate Glow Whiten and Gate Glow Alpha must be from 0 to 1.");
            }

            foreach (var problem in BurstSettings.Problems(
                         "Cube", cubeCountPerCell, cubeCap, cubeSizeCells.x, cubeSizeCells.y, cubeTravelCells.x,
                         cubeTravelCells.y, cubeSpreadDegrees, cubeSpinDegrees, CubeStreamSetsItsOwnDelays))
            {
                problems.Add($"{name}: {problem}");
            }

            foreach (var problem in BurstSettings.Problems(
                         "Shard", shardCountPerCell, shardCap, shardSizeCells.x, shardSizeCells.y, shardTravelCells.x,
                         shardTravelCells.y, shardSpreadDegrees, shardSpinDegrees, shardMaxDelaySeconds))
            {
                problems.Add($"{name}: {problem}");
            }

            if (!(cubeEndScale >= 0f && cubeEndScale <= 1f && shardEndScale >= 0f && shardEndScale <= 1f))
            {
                problems.Add($"{name}: Cube End Scale and Shard End Scale must be from 0 to 1.");
            }

            if (!(shardFallCells >= 0f) || float.IsInfinity(shardFallCells))
            {
                problems.Add($"{name}: Shard Fall Cells must be at least 0 and finite.");
            }

            if (!(peelCubeFraction >= 0f && peelCubeFraction <= 1f))
            {
                problems.Add($"{name}: Peel Cube Fraction must be from 0 to 1.");
            }

            if (!(peelBumpCells >= 0f && peelBumpCells < 0.5f))
            {
                problems.Add($"{name}: Peel Bump Cells must be at least 0 and below 0.5.");
            }

            if (!(lockOpenScale >= 1f) || float.IsInfinity(lockOpenScale))
            {
                problems.Add($"{name}: Lock Open Scale must be at least 1 and finite.");
            }

            if (!(badgePopScale > 1f) || float.IsInfinity(badgePopScale))
            {
                problems.Add($"{name}: Badge Pop Scale must be above 1 and finite.");
            }

            if (!(riseStartScale > 0f && riseStartScale <= 1f))
            {
                problems.Add($"{name}: Rise Start Scale must be above 0 and at most 1.");
            }

            if (!(riseDropCells >= 0f) || float.IsInfinity(riseDropCells))
            {
                problems.Add($"{name}: Rise Drop Cells must be at least 0 and finite.");
            }

            AddIfNotPositive(problems, liftSeconds, "Lift Seconds");
            AddIfNotPositive(problems, exitSecondsPerCell, "Exit Seconds Per Cell");
            AddIfNotPositive(problems, gateGlowDepthCells, "Gate Glow Depth Cells");
            AddIfNotPositive(problems, gateGlowFadeInSeconds, "Gate Glow Fade In Seconds");
            AddIfNotPositive(problems, gateGlowFadeOutSeconds, "Gate Glow Fade Out Seconds");
            AddIfNotPositive(problems, cubeSeconds, "Cube Seconds");
            AddIfNotPositive(problems, shardSeconds, "Shard Seconds");
            AddIfNotPositive(problems, shutterLiftSeconds, "Shutter Lift Seconds");
            AddIfNotPositive(problems, lockOpenSeconds, "Lock Open Seconds");
            AddIfNotPositive(problems, badgePopSeconds, "Badge Pop Seconds");
            AddIfNotPositive(problems, generatorSpawnSeconds, "Generator Spawn Seconds");
            AddIfNotPositive(problems, doorsOpenSeconds, "Doors Open Seconds");
            AddIfNotPositive(problems, doorsCloseSeconds, "Doors Close Seconds");
            AddIfNotPositive(problems, riseSeconds, "Rise Seconds");

            if (!(outlineOrder < blockLipOrder))
            {
                problems.Add($"{name}: Outline Order must be below Block Lip Order, so the outline sits under the lip.");
            }
        }

        /// <summary>The constraints on the HUD's values, each message naming its field.</summary>
        private void AddHudProblems(List<string> problems)
        {
            if (!(hudRestartSizeUnits > 0f && hudRestartCornerUnits > 0f && hudRestartIconSizeUnits > 0f
                  && hudRestartIconSizeUnits <= hudRestartSizeUnits))
            {
                problems.Add($"{name}: Hud Restart Size Units, Hud Restart Corner Units and Hud Restart Icon Size Units must be positive, and the icon at most the button's size.");
            }

            if (!(hudPillHeightUnits > 0f && timerPillWidthUnits > 0f && levelPillWidthUnits > 0f))
            {
                problems.Add($"{name}: Hud Pill Height Units, Timer Pill Width Units and Level Pill Width Units must be positive.");
            }

            if (!(hudClockIconSizeUnits > 0f && hudClockIconSizeUnits <= hudPillHeightUnits && hudIconGapUnits >= 0f
                  && hudClockIconSizeUnits + hudIconGapUnits + (hudPillHeightUnits - hudClockIconSizeUnits) < timerPillWidthUnits))
            {
                problems.Add($"{name}: Hud Clock Icon Size Units must be positive and at most Hud Pill Height Units, Hud Icon Gap Units at least 0, and together they must leave room for the digits in Timer Pill Width Units.");
            }

            if (!(hudSidePaddingUnits >= 0f && hudFontSize > 0f))
            {
                problems.Add($"{name}: Hud Side Padding Units must be at least 0 and Hud Font Size positive.");
            }

            if (timerWarningSeconds < 0)
            {
                problems.Add($"{name}: Timer Warning Seconds may not be negative.");
            }

            if (!IsUsableFormat(levelLabelFormat))
            {
                problems.Add($"{name}: Level Label Format must be a format with at most one placeholder, {{0}}, for the level number.");
            }

            if (!IsUsableFormat(timeBonusFormat))
            {
                problems.Add($"{name}: Time Bonus Format must be a format with at most one placeholder, {{0}}, for the seconds earned.");
            }

            if (!(timeBonusFontSize > 0f && timeBonusGapUnits >= 0f && timeBonusRiseUnits >= 0f))
            {
                problems.Add($"{name}: Time Bonus Font Size must be positive, and Time Bonus Gap Units and Time Bonus Rise Units at least 0.");
            }

            if (!(timerPulseScale >= 1f) || float.IsInfinity(timerPulseScale))
            {
                problems.Add($"{name}: Timer Pulse Scale must be at least 1 and finite.");
            }

            AddIfNotPositive(problems, timeBonusSeconds, "Time Bonus Seconds");
            AddIfNotPositive(problems, timerPulseSeconds, "Timer Pulse Seconds");
        }

        /// <summary>The constraints on the result panel's values, each message naming its field.</summary>
        private void AddResultPanelProblems(List<string> problems)
        {
            if (!(resultPanelSizeUnits.x > 0f && resultPanelSizeUnits.y > 0f && resultPanelCornerUnits > 0f
                  && resultButtonSizeUnits.x > 0f && resultButtonSizeUnits.y > 0f && resultButtonCornerUnits > 0f))
            {
                problems.Add($"{name}: Result Panel Size Units, Result Panel Corner Units, Result Button Size Units and Result Button Corner Units must be positive.");
            }

            if (!(resultPaddingUnits >= 0f && resultButtonGapUnits >= 0f
                  && 2f * resultButtonSizeUnits.x + resultButtonGapUnits <= resultPanelSizeUnits.x - 2f * resultPaddingUnits
                  && resultButtonSizeUnits.y + 3f * resultPaddingUnits < resultPanelSizeUnits.y))
            {
                problems.Add($"{name}: Result Panel Size Units must hold two Result Button Size Units side by side with Result Button Gap Units between them, and leave room for the title, inside Result Padding Units.");
            }

            if (!(resultTitleFontSize > 0f && resultButtonFontSize > 0f))
            {
                problems.Add($"{name}: Result Title Font Size and Result Button Font Size must be positive.");
            }

            if (!(resultPopSeconds > 0f && resultPopStartScale > 0f))
            {
                problems.Add($"{name}: Result Pop Seconds and Result Pop Start Scale must be positive.");
            }
        }

        private static bool IsUsableFormat(string format)
        {
            if (format == null)
            {
                return false;
            }

            try
            {
                string.Format(CultureInfo.InvariantCulture, format, 1);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
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
            layerEdgeOrder = DefaultLayerEdgeOrder;
            layerOrder = DefaultLayerOrder;
            studOrder = DefaultStudOrder;
            glossOrder = DefaultGlossOrder;
            chainOrder = DefaultChainOrder;
            iconOrder = DefaultIconOrder;
            keyGemOrder = DefaultKeyGemOrder;
            shutterOrder = DefaultShutterOrder;
            shutterBorderOrder = DefaultShutterBorderOrder;
            badgeOrder = DefaultBadgeOrder;
            badgeRimOrder = DefaultBadgeRimOrder;
            labelOrder = DefaultLabelOrder;
            outlineOrder = DefaultOutlineOrder;
            liftedBlockOrder = DefaultLiftedBlockOrder;
            effectOrder = DefaultEffectOrder;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void AddIfNotPositive(List<string> problems, float value, string field)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                problems.Add($"{name}: {field} must be positive and finite.");
            }
        }

        private void AddIfUnassigned(List<string> problems, Sprite sprite, string field)
        {
            if (sprite == null)
            {
                problems.Add($"{name}: {field} is not assigned; run Gate Rush → Generate Art and assign it from Assets/Art/Generated.");
            }
        }

        /// <summary>One sparkle around an introduction card's illustration.</summary>
        [Serializable]
        public sealed class SparklePlacement
        {
            [Tooltip("Where the sparkle sits, from the illustration area's centre, as a fraction of the area's size: (0.5, 0.5) is its top right corner.")]
            [SerializeField] private Vector2 position;

            [Tooltip("The sparkle's size at the full end of its twinkle, in canvas units.")]
            [SerializeField] private float sizeUnits;

            [Tooltip("Where in the twinkle this sparkle starts, from 0 to below 1, so the sparkles do not pulse together.")]
            [SerializeField] private float phase;

            /// <summary>A sparkle.</summary>
            public SparklePlacement(Vector2 position, float sizeUnits, float phase)
            {
                this.position = position;
                this.sizeUnits = sizeUnits;
                this.phase = phase;
            }

            /// <summary>Where it sits, from the illustration area's centre, as a fraction of the area's size.</summary>
            public Vector2 Position => position;

            /// <summary>Its size at the full end of its twinkle, in canvas units.</summary>
            public float SizeUnits => sizeUnits;

            /// <summary>Where in the twinkle it starts, from 0 to below 1.</summary>
            public float Phase => phase;
        }

        /// <summary>One mechanic's introduction card: its title and its text.</summary>
        [Serializable]
        private sealed class MechanicIntroduction
        {
            [SerializeField] private LevelMechanic mechanic;
            [SerializeField] private string title;
            [SerializeField, TextArea] private string text;

            public MechanicIntroduction(LevelMechanic mechanic, string title, string text)
            {
                this.mechanic = mechanic;
                this.title = title;
                this.text = text;
            }

            public LevelMechanic Mechanic => mechanic;

            public string Title => title;

            public string Text => text;
        }
    }
}
