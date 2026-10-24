using System;
using System.Collections.Generic;
using System.Globalization;
using GateRush.Core;
using GateRush.Meta;
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
    /// animation (Module 18), the introduction cards with their texts and the
    /// theme's look (Modules 19 and 41), and Home's campaign-done notice
    /// (Module 32); the front end's, the level HUD's and the level panels'
    /// are <c>UiConfig</c>'s (Modules 35, 40 and 41). Nothing in
    /// <c>GateRush.Runtime</c> hardcodes one of these at a call site. Board
    /// sizes are in cells unless named otherwise, so the board keeps its
    /// proportions whatever <see cref="CellSize"/> is; card sizes are in
    /// canvas units of a 1080 × 1920 portrait screen, the canvas's reference
    /// resolution.
    /// </summary>
    /// <remarks>
    /// <para>The art sprites are generated in greyscale from the
    /// <c>ArtRecipe</c> asset (<i>Gate Rush → Generate Art</i>) and tinted here:
    /// one set serves every colour (D46). What must not take a block's colour —
    /// the outline and the shine (D79), the stud gloss, the arrows, the frost,
    /// the key's gold — has its own sprite and its own colour below.</para>
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
        private const int DefaultElevatorRimOrder = 1;
        private const int DefaultElevatorHazardOrder = 2;
        private const int DefaultElevatorDoorPanelOrder = 3;
        private const int DefaultElevatorHandleOrder = 4;
        private const int DefaultMachineOrder = 5;
        private const int DefaultMachineScreenOrder = 6;
        private const int DefaultMachineFittingsOrder = 7;
        private const int DefaultMiniatureOrder = 8;
        private const int DefaultFrameContourOrder = 9;
        private const int DefaultFrameLipOrder = 10;
        private const int DefaultFrameOrder = 11;
        private const int DefaultFrameShineOrder = 12;
        private const int DefaultGateArrowOutlineOrder = 13;
        private const int DefaultGateMarkOrder = 14;
        private const int DefaultBlockContourOrder = 15;
        private const int DefaultBlockLipOrder = 16;
        private const int DefaultBlockOrder = 17;
        private const int DefaultBlockShineOrder = 18;
        private const int DefaultStudOrder = 19;
        private const int DefaultGlossOrder = 20;
        private const int DefaultLayerEdgeOrder = 21;
        private const int DefaultLayerOrder = 22;
        private const int DefaultLayerShineOrder = 23;
        private const int DefaultLayerStudOrder = 24;
        private const int DefaultLayerGlossOrder = 25;
        private const int DefaultChainOrder = 26;
        private const int DefaultIconOrder = 27;
        private const int DefaultKeyGemOrder = 28;
        private const int DefaultKeyGemHighlightOrder = 29;
        private const int DefaultShutterOrder = 30;
        private const int DefaultShutterBorderOrder = 31;
        private const int DefaultBadgeRimOrder = 32;
        private const int DefaultBadgeOrder = 33;
        private const int DefaultBadgeHighlightOrder = 34;
        private const int DefaultLabelOrder = 35;

        // Module 46, pass 2. A frozen block shows no colour beneath, so it has
        // no layered window: its ice and frost take the window's two lowest
        // slots. A closed gate has no arrow: its ice takes the arrow
        // outline's. The next block's studs on a machine's screen take the
        // frame's shadow and lip slots, which never lie over a screen.
        private const int DefaultIceOrder = DefaultLayerEdgeOrder;
        private const int DefaultIceStreakOrder = DefaultLayerOrder;
        private const int DefaultGateIceOrder = DefaultGateArrowOutlineOrder;
        private const int DefaultMiniatureStudOrder = DefaultFrameContourOrder;
        private const int DefaultMiniatureGlossOrder = DefaultFrameLipOrder;

        // Compared only inside a lifted block's sorting group, where it has to
        // sort below the block's contour and nothing else. Sharing a value
        // with another board layer (the gate marks) is harmless: nothing
        // outside the group is compared with it.
        private const int DefaultOutlineOrder = DefaultBlockContourOrder - 1;

        /// <summary>
        /// The cube burst's maximum delay. Not a tunable: a stream spreads its
        /// cubes' starts over the block's pass and never reads it.
        /// </summary>
        private const float CubeStreamSetsItsOwnDelays = 0f;

        /// <summary>
        /// The longest time bonus <see cref="Problems"/> sizes a time-bonus
        /// mark for when it checks that two marks sharing a block cannot
        /// overlap: two digits. Not a tunable; a longer bonus widens its mark
        /// past what was checked. Public so that nothing goes past it
        /// unnoticed: the Level Editor's validator warns about a block whose
        /// bonus is above it, and a test holds every shipped level to it
        /// (Module 28).
        /// </summary>
        public const int WidestBonusSecondsChecked = 99;

        /// <summary>
        /// World units to the em of a world-space TextMeshPro label of font
        /// size 1: a tenth. TextMeshPro's, not a tunable; the time-bonus
        /// seconds' advance and Generate Art's outline are measured with it.
        /// </summary>
        public const float WorldUnitsPerFontSize = 0.1f;

        /// <summary>
        /// The deepest layer count <see cref="Problems"/> sizes a layer badge
        /// for when it checks that marks sharing a block cannot overlap: two
        /// digits. Not a tunable; a deeper stack widens its badge past what
        /// was checked.
        /// </summary>
        private const int DeepestLayerCountChecked = 99;

        private const int DefaultLiftedBlockOrder = 36;
        private const int DefaultEffectOrder = 37;
        private const int DefaultPullGlowOrder = DefaultBlockContourOrder - 1;

        // The generator preview lies inside the grid, over the floor and an
        // elevator's doors and handles and under every block. Sharing values
        // with the machine's body and screen is harmless: machines lie outside
        // the frame and never over a board cell.
        private const int DefaultGeneratorPreviewRimOrder = DefaultElevatorHandleOrder + 1;
        private const int DefaultGeneratorPreviewOrder = DefaultElevatorHandleOrder + 2;

        [Header("Assets")]
        [Tooltip("A plain white square sprite, tinted and scaled for the floor's backing, and the shape of the mask that clips a block passing through its gate.")]
        [SerializeField] private Sprite cellSprite;

        [Tooltip("Sprite-Unlit-Default. Under the 2D Renderer a lit sprite renders black with no Light 2D.")]
        [SerializeField] private Material spriteMaterial;

        [Header("Lit body materials (Module 46; Gate Rush → Generate Art makes and assigns these)")]
        [Tooltip("Gate Rush/Board Sprite multiplying the colour back by the art recipe's Shade Headroom: the lit faces and the studs.")]
        [SerializeField] private Material litMaterial;

        [Tooltip("Gate Rush/Board Sprite adding its colour: the white highlight over the lit faces.")]
        [SerializeField] private Material specularMaterial;

        [Tooltip("The lit material's twin on Gate Rush/Board UI Sprite, for the introduction cards' UI Images.")]
        [SerializeField] private Material uiLitMaterial;

        [Tooltip("The highlight material's twin on Gate Rush/Board UI Sprite, for the introduction cards' UI Images.")]
        [SerializeField] private Material uiSpecularMaterial;

        [Tooltip("TextMeshPro font asset for the board's labels: its counts, and the counts and marks in the introduction cards' illustrations. Lilita One (D46), created from Assets/ThirdParty/Fonts/LilitaOne. The cards' own texts use Intro Font.")]
        [SerializeField] private TMP_FontAsset labelFont;

        [Tooltip("The label font's material with a soft drop shadow (its underlay): the white digits on badges, padlocks and time-bonus marks (Module 46, pass 2). Gate Rush → Generate Art makes it from Label Font and the Label Shadow values, and assigns it.")]
        [SerializeField] private Material labelShadowMaterial;

        [Tooltip("Lilita One again, with room in its atlas for a wide outline, holding only the time-bonus mark's characters: a second font asset, so the shared one and Label Shadow Material stay as they are. Gate Rush → Generate Art makes it and assigns it.")]
        [SerializeField] private TMP_FontAsset labelOutlineFont;

        [Tooltip("Label Outline Font's material with the dark outline and the soft drop shadow: a time-bonus mark's seconds. Gate Rush → Generate Art makes it and assigns it.")]
        [SerializeField] private Material labelOutlineMaterial;

        [Header("Generated art (Assets/Art/Generated; Gate Rush → Generate Art assigns these)")]
        [SerializeField] private Sprite quarterOuterSprite;
        [SerializeField] private Sprite quarterEdgeSprite;
        [SerializeField] private Sprite quarterFillSprite;
        [SerializeField] private Sprite quarterConcaveSprite;
        [SerializeField] private Sprite faceOuterTopLeftSprite;
        [SerializeField] private Sprite faceOuterTopRightSprite;
        [SerializeField] private Sprite faceOuterBottomLeftSprite;
        [SerializeField] private Sprite faceOuterBottomRightSprite;
        [SerializeField] private Sprite faceConcaveTopLeftSprite;
        [SerializeField] private Sprite faceConcaveTopRightSprite;
        [SerializeField] private Sprite faceConcaveBottomLeftSprite;
        [SerializeField] private Sprite faceConcaveBottomRightSprite;
        [SerializeField] private Sprite faceEdgeTopSprite;
        [SerializeField] private Sprite faceEdgeBottomSprite;
        [SerializeField] private Sprite faceEdgeLeftSprite;
        [SerializeField] private Sprite faceEdgeRightSprite;
        [SerializeField] private Sprite faceFillSprite;
        [SerializeField] private Sprite bodyOuterTopLeftSprite;
        [SerializeField] private Sprite bodyOuterTopRightSprite;
        [SerializeField] private Sprite bodyOuterBottomLeftSprite;
        [SerializeField] private Sprite bodyOuterBottomRightSprite;
        [SerializeField] private Sprite bodyConcaveTopLeftSprite;
        [SerializeField] private Sprite bodyConcaveTopRightSprite;
        [SerializeField] private Sprite bodyConcaveBottomLeftSprite;
        [SerializeField] private Sprite bodyConcaveBottomRightSprite;
        [SerializeField] private Sprite bodyEdgeTopSprite;
        [SerializeField] private Sprite bodyEdgeBottomSprite;
        [SerializeField] private Sprite bodyEdgeLeftSprite;
        [SerializeField] private Sprite bodyEdgeRightSprite;
        [SerializeField] private Sprite bodyFillSprite;
        [SerializeField] private Sprite shadowOuterTopLeftSprite;
        [SerializeField] private Sprite shadowOuterTopRightSprite;
        [SerializeField] private Sprite shadowOuterBottomLeftSprite;
        [SerializeField] private Sprite shadowOuterBottomRightSprite;
        [SerializeField] private Sprite shadowConcaveTopLeftSprite;
        [SerializeField] private Sprite shadowConcaveTopRightSprite;
        [SerializeField] private Sprite shadowConcaveBottomLeftSprite;
        [SerializeField] private Sprite shadowConcaveBottomRightSprite;
        [SerializeField] private Sprite shadowEdgeTopSprite;
        [SerializeField] private Sprite shadowEdgeBottomSprite;
        [SerializeField] private Sprite shadowEdgeLeftSprite;
        [SerializeField] private Sprite shadowEdgeRightSprite;
        [SerializeField] private Sprite studsSprite;
        [SerializeField] private Sprite studGlossSprite;
        [SerializeField] private Sprite singleStudSprite;
        [SerializeField] private Sprite singleStudGlossSprite;
        [SerializeField] private Sprite floorSprite;
        [SerializeField] private Sprite gateArrowSprite;
        [SerializeField] private Sprite gateArrowOutlineSprite;
        [SerializeField] private Sprite axisArrowSprite;
        [SerializeField] private Sprite backgroundRampSprite;
        [SerializeField] private Sprite vignetteSprite;
        [SerializeField] private Sprite roundedRectSprite;
        [SerializeField] private Sprite chainSprite;
        [SerializeField] private Sprite padlockSprite;
        [SerializeField] private Sprite keyBodySprite;
        [SerializeField] private Sprite keyGemSprite;
        [SerializeField] private Sprite shutterSlatsSprite;
        [SerializeField] private Sprite clockSprite;
        [SerializeField] private Sprite cubeSprite;
        [SerializeField] private Sprite cubeHighlightSprite;
        [SerializeField] private Sprite shardSprite;
        [SerializeField] private Sprite shard2Sprite;
        [SerializeField] private Sprite shard3Sprite;
        [SerializeField] private Sprite gateGlowSprite;
        [SerializeField] private Sprite sparkleSprite;

        [Header("Generated mechanics (Module 46, pass 2; Gate Rush → Generate Art assigns these)")]
        [SerializeField] private Sprite iceOuterTopLeftSprite;
        [SerializeField] private Sprite iceOuterTopRightSprite;
        [SerializeField] private Sprite iceOuterBottomLeftSprite;
        [SerializeField] private Sprite iceOuterBottomRightSprite;
        [SerializeField] private Sprite iceConcaveTopLeftSprite;
        [SerializeField] private Sprite iceConcaveTopRightSprite;
        [SerializeField] private Sprite iceConcaveBottomLeftSprite;
        [SerializeField] private Sprite iceConcaveBottomRightSprite;
        [SerializeField] private Sprite iceEdgeTopSprite;
        [SerializeField] private Sprite iceEdgeBottomSprite;
        [SerializeField] private Sprite iceEdgeLeftSprite;
        [SerializeField] private Sprite iceEdgeRightSprite;
        [SerializeField] private Sprite iceFillSprite;
        [SerializeField] private Sprite iceStreaksSprite;
        [SerializeField] private Sprite iceStreaksWideSprite;
        [SerializeField] private Sprite badgeRimSprite;
        [SerializeField] private Sprite badgeFaceSprite;
        [SerializeField] private Sprite badgeHighlightSprite;
        [SerializeField] private Sprite lockSprite;
        [SerializeField] private Sprite goldKeySprite;
        [SerializeField] private Sprite gemSprite;
        [SerializeField] private Sprite gemHighlightSprite;
        [SerializeField] private Sprite bonusCoinSprite;
        [SerializeField] private Sprite oneWayArrowSprite;
        [SerializeField] private Sprite oneWayArrowHighlightSprite;
        [SerializeField] private Sprite oneWayArrowVerticalSprite;
        [SerializeField] private Sprite oneWayArrowVerticalHighlightSprite;
        [SerializeField] private Sprite shutterRimSprite;
        [SerializeField] private Sprite shutterCylindersSprite;
        [SerializeField] private Sprite machineBodySprite;
        [SerializeField] private Sprite machineScreenSprite;
        [SerializeField] private Sprite machineVentSprite;
        [SerializeField] private Sprite machineButtonSprite;
        [SerializeField] private Sprite elevatorRimSprite;
        [SerializeField] private Sprite elevatorHazardLeftSprite;
        [SerializeField] private Sprite elevatorHazardRightSprite;
        [SerializeField] private Sprite elevatorDoorSprite;
        [SerializeField] private Sprite elevatorHandleSprite;

        [Header("Generated measures (Module 46, pass 2; in cells, Gate Rush → Generate Art writes these from the Art Recipe)")]
        [Tooltip("Height of the badge every count sits on: the painted badge's.")]
        [SerializeField] private float badgeHeightCells = 54f / 128f;

        [Tooltip("Width each further digit adds to a badge: the painted badge's.")]
        [SerializeField] private float badgeDigitWidthCells = 10f / 128f;

        [Tooltip("Space either side of a badge's one digit: the painted badge's.")]
        [SerializeField] private float badgePaddingCells = 23f / 128f;

        [Tooltip("How far a badge's sprite reaches past the badge, for its shadow.")]
        [SerializeField] private float badgeShadowMarginCells = 13f / 128f;

        [Tooltip("How far a shutter's rim, an elevator's rim and a machine's body reach past their rectangle, for their floor shadow.")]
        [SerializeField] private float raisedShadowMarginCells = 23f / 128f;

        [Tooltip("How far inside a closed shutter's region its slats start: the rim's inner edge.")]
        [SerializeField] private float shutterInnerInsetCells = 22f / 128f;

        [Tooltip("How far inside an elevator's region its recess starts at the sides.")]
        [SerializeField] private float elevatorInnerInsetAcrossCells = 22f / 128f;

        [Tooltip("How far inside an elevator's region its recess starts at the top and the bottom.")]
        [SerializeField] private float elevatorInnerInsetUpCells = 16f / 128f;

        [Tooltip("The gap around each of an elevator's doors.")]
        [SerializeField] private float elevatorDoorGapCells = 3f / 128f;

        [Tooltip("How far a door's sprite reaches past the door, for its shadow.")]
        [SerializeField] private float elevatorDoorShadowMarginCells = 12f / 128f;

        [Tooltip("How far either side of the doors' seam each handle's centre sits.")]
        [SerializeField] private float elevatorHandleOffsetCells = 13f / 128f;

        [Tooltip("How far in from an elevator's side each hazard strip's middle runs.")]
        [SerializeField] private float elevatorHazardCentreCells = 13f / 128f;

        [Tooltip("How far short of an elevator's top and bottom its hazard strips end.")]
        [SerializeField] private float elevatorHazardEndCells = 24f / 128f;

        [Tooltip("How far a one-way arrow's sprite stops short of each end of its block's bounding box.")]
        [SerializeField] private float oneWayArrowEndInsetCells = 21f / 128f;

        [Tooltip("How far below a padlock's centre the keys still needed are written.")]
        [SerializeField] private float padlockCountDropCells = 20f / 128f;

        [Tooltip("The diameter of a time-bonus mark's coin: the reference's 68 pixels of 128.")]
        [SerializeField] private float bonusCoinCells = 68f / 128f;

        [Header("Board")]
        [Tooltip("World units per cell.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Empty space kept on each side of the framed board, and of any generator machine, when fitting the camera, in cells.")]
        [SerializeField] private float sideMarginCells = 0.3f;

        [Tooltip("Part of the screen's height kept free above the board for the HUD, from 0 to 1. Top and bottom together must stay below 1. 0.16 holds the 306-unit HUD of the Module 45 follow-up — ribbon, timer, pills — at the 1920-unit reference height.")]
        [SerializeField] private float topBandScreenFraction = 0.16f;

        [Tooltip("Part of the screen's height kept free below the board for the joker bar, from 0 to 1. Top and bottom together must stay below 1. 0.145 is the most the board-fit floor allows with the top band at 0.16; the 399-unit joker bar (Module 46) is scaled to it, its foot on the band's.")]
        [SerializeField] private float bottomBandScreenFraction = 0.145f;

        [Tooltip("Thickness of the frame around the grid, in cells: above 0, at most 1. Closed gates are drawn at this thickness too.")]
        [SerializeField] private float frameThicknessCells = 0.45f;

        [Tooltip("How far the floor's backing reaches past the grid under the frame, in cells, so no background shows between floor and frame. At most the frame thickness.")]
        [SerializeField] private float floorUnderlayCells = 0.2f;

        [Tooltip("A previewed cell's rim is the colour with RGB multiplied by this, from 0 (black) to 1 (no darkening).")]
        [SerializeField] private float lipDarken = 0.5f;

        [Tooltip("How much each quarter tile is enlarged on every side, in cells, to hide sub-pixel cracks between tiles. 0 turns it off.")]
        [SerializeField] private float seamOverlapCells = 0.002f;

        [Tooltip("Size of the white arrow on an open gate, in cells.")]
        [SerializeField] private float gateArrowSizeCells = 0.3f;

        [Tooltip("Thickness of the double-headed arrow on an axis-restricted block (M7), in cells.")]
        [SerializeField] private float axisArrowThicknessCells = 0.42f;

        [Tooltip("How far the axis arrow stops short of each end of the block's bounding box, in cells. Below 0.5.")]
        [SerializeField] private float axisArrowEndInsetCells = 0.14f;

        [Header("Layered blocks (M4)")]
        [Tooltip("How far a layered block's window sits inside its footprint, in cells, on every side (Module 46): the outer block, with its studs, shows around it in the same colour as a plain block's. Above 0; with Layer Edge Cells below 0.5.")]
        [SerializeField] private float layerInsetCells = 0.31f;

        [Tooltip("Width of the window's dark rim, in cells, in the outer colour darkened by Layer Edge Darken. At least 0.")]
        [SerializeField] private float layerEdgeCells = 0.02f;

        [Tooltip("The window's rim is the outer colour with RGB multiplied by this, from 0 to 1: a very dark shade of it, never the outline colour.")]
        [SerializeField] private float layerEdgeDarken = 0.35f;

        [Tooltip("Distance between neighbouring studs of the inner block in a layered block's window, in cells: its own grid, centred, as many studs as fit. Above 0.")]
        [SerializeField] private float layerStudSpacingCells = 0.42f;

        [Tooltip("How far an inner stud's centre stays from the inner block's edge, in cells, on the inner block's rectangles; it covers the stud, its foot and a margin of face. Above 0. StudFitTests checks it against the art recipe.")]
        [SerializeField] private float layerStudClearanceCells = 0.18f;

        [Header("Count badge")]
        [Tooltip("How far a block's count badge — a frozen block's clears, a deep layered block's colours — sits in from the outer bottom-right corner of its bottom-right cell, on both axes, in cells (Module 46): the reference's 34 pixels of 128. At least 0, below 0.5.")]
        [SerializeField] private float blockBadgeInsetCells = 34f / 128f;

        [Header("Locks and keys")]
        [Tooltip("How far a chain stops short of each end of its row of cells, in cells. Below 0.5.")]
        [SerializeField] private float chainEndInsetCells = 0.1f;

        [Tooltip("The padlock's width, in cells, as the marks sharing a block are laid out, and as the Level Editor draws it: at least the board's padlock's body (ArtRecipeTests checks it against the art recipe).")]
        [SerializeField] private float padlockSizeCells = 0.62f;

        [Tooltip("The key's length, in cells, as the marks sharing a block are laid out, and as the Level Editor draws it: at least the board's key's, bow to tip, at Key Scale (ArtRecipeTests checks it against the art recipe).")]
        [SerializeField] private float keySizeCells = 0.6f;

        [Tooltip("The board's key's size relative to its painted size: its gem and the gem's highlight with it, about the key's centre. The key is painted at 1.2 times the reference script's bow and shaft; 1 / 1.2 draws it at the script's size (Module 46). Above 0.")]
        [SerializeField] private float keyScale = 1f / 1.2f;

        [Tooltip("How far the Level Editor turns its key from horizontal, in degrees, counter-clockwise. The board's key lies flat: a lit sprite turned would turn its light (Module 46).")]
        [SerializeField] private float keyRotationDegrees = 35f;

        [Tooltip("How far a frozen block's padlock or key is raised above its frozen count, in cells, so the two do not overlap.")]
        [SerializeField] private float frozenMarkRaiseCells = 0.3f;

        [Header("Time-bonus mark (M10: the green clock coin and the outlined seconds beside it)")]
        [Tooltip("Text on a time-bonus block's mark; {0} is the seconds it adds.")]
        [SerializeField] private string timeBonusMarkFormat = "+{0}";

        [Tooltip("Space between the coin's right edge and the start of the seconds' advance, in cells, for every value: the reference's 4 pixels of 128 (time_bonus_reference_b.py). The coin and the seconds are centred on the anchor together. At least 0.")]
        [SerializeField] private float timeBonusLabelGapCells = 4f / 128f;

        [Tooltip("How far below the mark's anchor the seconds' middle sits, in cells: the reference's 2 pixels of 128.")]
        [SerializeField] private float timeBonusLabelDropCells = 2f / 128f;

        [Tooltip("Width each character of the seconds takes, in cells, at Count Font Size, used only when neither Label Outline Font nor Label Font is assigned: Lilita One's \"+10\" and \"+99\" average about 19 pixels of 128 at 34 pixels. With a font, the glyphs' own advances are used. Above 0.")]
        [SerializeField] private float timeBonusMarkCharacterWidthCells = 19f / 128f;

        [Tooltip("The seconds' dark outline, standing this far outside the glyphs, in cells: the reference's 3-pixel stroke of 128. Generate Art writes it into Label Outline Material. Above 0.")]
        [SerializeField] private float labelOutlineStrokeCells = 3f / 128f;

        [Tooltip("The seconds' dark outline's colour. Written into Label Outline Material by Generate Art.")]
        [SerializeField] private Color labelOutlineColor = new Color32(0x2A, 0x16, 0x50, 0xFF);

        [Tooltip("The atlas padding, in pixels, of the font asset Generate Art makes for the seconds (Label Outline Font). An outline reaches at most about half of it outside a glyph, so a wider stroke needs more; Generate Art warns when it falls short. At least 1.")]
        [SerializeField] private int labelOutlineAtlasPadding = 17;

        [Tooltip("The characters Generate Art puts in Label Outline Font: every character a time-bonus mark can show.")]
        [SerializeField] private string labelOutlineCharacters = "+0123456789";

        [Tooltip("The room a mark keeps from each side of the width it has on its block, in cells, when a time-bonus mark is scaled down to fit: the key's, (1 - 0.72) / 2. At least 0, below half a cell.")]
        [SerializeField] private float markFitMarginCells = 0.14f;

        [Tooltip("The Level Editor's time-bonus plate's fill; the board's mark has no plate (Module 46).")]
        [SerializeField] private Color timeBonusMarkColor = new Color(0.20f, 0.62f, 0.30f);
        [SerializeField] private Color timeBonusMarkRimColor = new Color(0.62f, 0.95f, 0.62f);
        [SerializeField] private Color timeBonusMarkIconColor = Color.white;

        [Tooltip("The scale of two marks — a padlock or key, a layer count, a time-bonus mark — that share a block with no room for both at full size (a 1×1, an L, a T): above 0, at most 1.")]
        [SerializeField] private float crowdedMarkScale = 0.6f;

        [Tooltip("The scale of three marks — a padlock or key, a layer count and a time-bonus mark — that share a block with no room for a cell each: above 0, at most Crowded Mark Scale.")]
        [SerializeField] private float crowdedTripleMarkScale = 0.45f;

        [Header("Generator machines")]
        [Tooltip("How deep a generator's machine is, out from the frame, in cells.")]
        [SerializeField] private float machineDepthCells = 1f;

        [Tooltip("How far a machine runs past its generator's span at each end, in cells.")]
        [SerializeField] private float machineSideOverhangCells = 0.12f;

        [Tooltip("How far a machine's inner side reaches back under the frame, in cells. Below the depth.")]
        [SerializeField] private float machineFrameOverlapCells = 0.15f;

        [Tooltip("Margin between a machine's rim and its screen, in cells.")]
        [SerializeField] private float machineScreenInsetCells = 0.12f;

        [Tooltip("The room a machine keeps beyond its screen on its outer side, half of it past its rim, as tall as this on the top and bottom edges: the count badge's height before Module 46's second pass, kept so the board fit does not move. The count is on the screen now; the room holds the vent and the button.")]
        [SerializeField] private float machineCountRoomHeightCells = 0.34f;

        [Tooltip("With Machine Count Room Padding Cells, the room's width on a side for each digit the queue's length has, as the badge had it.")]
        [SerializeField] private float machineCountRoomDigitWidthCells = 0.15f;

        [SerializeField] private float machineCountRoomPaddingCells = 0.08f;

        [Tooltip("The share of a machine's screen, along its longer side and at its end, that holds the queued count; the next block has the rest. Above 0, below 1.")]
        [SerializeField] private float machineScreenCountShare = 0.35f;

        [Tooltip("How much of its part of the screen the next block fills, on each side: the reference's 32 of 44 pixels. Above 0, at most 1.")]
        [SerializeField] private float machineMiniatureFillShare = 0.72f;

        [Tooltip("Where the vent and the button sit along the machine's outer room, from its middle, in cells: the reference's 14 pixels before and 24 after, of 128.")]
        [SerializeField] private Vector2 machineFittingOffsetsCells = new Vector2(-14f / 128f, 24f / 128f);

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

        [Header("Gate pull (an open gate drawing in a block dragged toward it)")]
        [Tooltip("How far, in cells, from where the block would arrive at its gate the pull starts.")]
        [SerializeField] private float pullRangeCells = 0.8f;

        [Tooltip("How far, in cells, from where the block would arrive at its gate it latches onto the gate, and a release arrives there. At most Pull Range Cells.")]
        [SerializeField] private float captureRangeCells = 0.6f;

        [Tooltip("The most of the way to the gate the block is drawn before it latches, as a fraction of its distance: above 0 and at most 1. Within Capture Range Cells it is drawn all the way.")]
        [SerializeField] private float pullAmount = 1f;

        [Tooltip("How fast, per second, the pull takes hold and lets go. Higher is snappier; the same at any frame rate.")]
        [SerializeField] private float pullFollowRate = 25f;

        [Tooltip("The furthest, in cells, a block on its gate's cell is drawn into the gate's mouth while pushed on toward it. 0 turns the nudge off; must stay below 0.5.")]
        [SerializeField] private float nudgeMaxCells = 0.15f;

        [Tooltip("How the pull's strength grows from the edge of its range (0) to the gate (1).")]
        [SerializeField] private Ease pullEase = Ease.InOutSine;

        [Tooltip("The gate's glow at full pull strength, from 0 to 1.")]
        [SerializeField] private float pullGlowAlpha = 0.5f;

        [Tooltip("Seconds the pull's glow takes to fade out fully, and to come up fully.")]
        [SerializeField] private float pullGlowFadeSeconds = 0.12f;

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

        [Tooltip("Seconds a block a Rocket or Broom destroyed takes to fade where it stands, after its flash, while its cubes fly — or, frozen, its ice shards (Modules 34, 47). Input never waits for it; a win's panel does.")]
        [SerializeField] private float breakFadeSeconds = 0.2f;

        [SerializeField] private Ease breakFadeEase = Ease.OutQuad;

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

        [Tooltip("The shards' glassy ice, baked into the three shard sprites by Gate Rush > Generate Art (Module 47): run it again after a change. Opaque.")]
        [SerializeField] private Color shardColor = new Color(0.65882355f, 0.8862745f, 1f);

        [Tooltip("Draws each exit, peel and joker cube's white highlight as a second sprite, as a block's is drawn. Off by default: the cube is small, and the highlight doubles its renderers.")]
        [SerializeField] private bool drawCubeHighlights;

        [Header("Joker clears in place (Module 47: a destroyed block breaks apart where it stands)")]
        [Tooltip("How far the block's colour moves toward white at the height of its flash, from 0 (no flash) to 1.")]
        [SerializeField] private float breakFlashStrength = 0.6f;

        [Tooltip("Seconds the flash takes to reach its height; the fade over Break Fade Seconds then takes the block away. Above 0, at most 1.")]
        [SerializeField] private float breakFlashSeconds = 0.08f;

        [Tooltip("Cubes per cell of the block, before the caps: at least 1, at most Cube Count Per Cell.")]
        [SerializeField] private int breakCubeCountPerCell = 4;

        [Tooltip("The most cubes one block breaks into: at least 1, at most Cube Cap.")]
        [SerializeField] private int breakCubeCap = 16;

        [Tooltip("The most cubes one joker breaks into over every block it clears, shared between them (a Broom): at least Break Cube Cap, at most 128.")]
        [SerializeField] private int breakCubeMoveCap = 48;

        [Tooltip("How far a cube flies out from the block's centre, in cells: x the shortest, y the longest. The Exit cubes' size, spin, seconds, ease and end scale apply.")]
        [SerializeField] private Vector2 breakCubeTravelCells = new Vector2(0.4f, 1.2f);

        [Tooltip("How far a cube's direction may turn from straight out from the block's centre, in degrees either way: at least 0, below 90.")]
        [SerializeField] private float breakCubeSpreadDegrees = 25f;

        [Tooltip("The latest a cube starts after the break does, in seconds.")]
        [SerializeField] private float breakCubeMaxDelaySeconds = 0.03f;

        [Tooltip("Sparkles over one block that breaks, before the cap: from 0 (none) to 8.")]
        [SerializeField] private int breakSparkleCount = 2;

        [Tooltip("The most sparkles one joker shows over every block it clears, shared between them: from 0 to 32.")]
        [SerializeField] private int breakSparkleMoveCap = 6;

        [Tooltip("Side of a sparkle in cells: x the smallest, y the largest.")]
        [SerializeField] private Vector2 breakSparkleSizeCells = new Vector2(0.35f, 0.5f);

        [Tooltip("How far a sparkle drifts out from the block's centre, in cells: x the shortest, y the longest.")]
        [SerializeField] private Vector2 breakSparkleTravelCells = new Vector2(0.1f, 0.3f);

        [Tooltip("Seconds one sparkle shows, shrinking and fading. Above 0, at most 2.")]
        [SerializeField] private float breakSparkleSeconds = 0.35f;

        [Tooltip("A sparkle's scale at the end, from 0 to 1.")]
        [SerializeField] private float breakSparkleEndScale = 0f;

        [Header("Debris pool (Module 47)")]
        [Tooltip("Sprite renderers made once for the debris (cubes, shards, sparkles, gate glows) and reused: none is made or destroyed in play. A piece is skipped while all are flying. At least what one exit, one joker clear and one gate's or block's shards need at once; at most 512.")]
        [SerializeField] private int debrisPoolSize = 112;

        [Header("Finished elevator (Module 47)")]
        [Tooltip("Seconds an elevator whose last wave cleared takes to sink into the floor and fade. Above 0, at most 2.")]
        [SerializeField] private float elevatorSinkSeconds = 0.35f;

        [Tooltip("The lift's scale about its region's centre when it has sunk, from 0 to below 1.")]
        [SerializeField] private float elevatorSinkEndScale = 0.2f;

        [SerializeField] private Ease elevatorSinkEase = Ease.InQuad;

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

        [Header("Generator preview (D53; a generator's next block on its target cells while a block standing on them is held)")]
        [Tooltip("Opacity of a previewed cell's fill, in the next block's colour, or the ice colour when it would spawn frozen: above 0, at most 1.")]
        [SerializeField] private float generatorPreviewFillAlpha = 0.35f;

        [Tooltip("Opacity of a previewed cell's rim, in the lip colour of its fill: above 0, at most 1. The fill is translucent, so the rim's piece shows faintly through it.")]
        [SerializeField] private float generatorPreviewRimAlpha = 0.6f;

        [Tooltip("How far a previewed cell is inset from its cell's edges, in cells: at least 0. With Generator Preview Rim Cells it must stay below 0.5.")]
        [SerializeField] private float generatorPreviewInsetCells = 0.06f;

        [Tooltip("Thickness of a previewed cell's rim, in cells: above 0.")]
        [SerializeField] private float generatorPreviewRimCells = 0.05f;

        [Tooltip("Corner radius of a previewed cell, in cells: above Generator Preview Rim Cells.")]
        [SerializeField] private float generatorPreviewCornerCells = 0.18f;

        [Tooltip("Seconds the preview takes to fade in fully when the block is grabbed.")]
        [SerializeField] private float generatorPreviewFadeInSeconds = 0.1f;

        [Tooltip("Seconds the preview takes to fade out fully when the block is released.")]
        [SerializeField] private float generatorPreviewFadeOutSeconds = 0.12f;

        [Header("Meta game (Module 32; the result panel's and the confirmation's texts are the UI Config's, Module 41)")]
        [Tooltip("Shown above Home's play button when Play is pressed after the last campaign level (Module 35).")]
        [SerializeField] private string campaignDoneNotice = "All levels played. Play any level from Settings.";

        [Header("Introduction cards (canvas units; the theme's panel, font and close button, Module 41)")]
        [Tooltip("The font of the card's title, subtitle and sentence: Assets/ThirdParty/Fonts/Fredoka/Fredoka-Bold SDF.asset, whose fallback list holds Nunito for what Fredoka lacks. The illustrations' counts and marks keep Label Font.")]
        [SerializeField] private TMP_FontAsset introFont;

        [Tooltip("The card's panel, drawn 9-sliced behind its rows, so it needs a border: the theme's panel, Assets/Art/Sprites/UI/panel.png.")]
        [SerializeField] private Sprite introPanelSprite;

        [Tooltip("Space between the panel's edges and the card's rows.")]
        [SerializeField] private float introPanelPaddingUnits = 40f;

        [Tooltip("The face of the round close button, drawn in its own colours: Assets/Art/Sprites/UI/button_round_red.png, as Button Close's.")]
        [SerializeField] private Sprite introCloseSprite;

        [Tooltip("The cross on the close button, drawn in its own colours: Assets/ThirdParty/Kenney/GameIcons/cross.png, as Button Close's.")]
        [SerializeField] private Sprite introCloseCrossSprite;

        [Tooltip("The full-screen dim behind the card; it covers the board and the HUD and swallows presses on them. The UI Config's Scrim Color, as every overlay's: the project blends in linear light, so 0.85 dims as 0.6 would in gamma.")]
        [FormerlySerializedAs("resultBackdropColor")]
        [SerializeField] private Color introBackdropColor = new Color(0f, 0f, 0f, 0.85f);

        [Tooltip("Seconds the card's opening pop takes: it fades in and grows from Intro Pop Start Scale. Runs on unscaled time.")]
        [FormerlySerializedAs("resultPopSeconds")]
        [SerializeField] private float introPopSeconds = 0.25f;

        [FormerlySerializedAs("resultPopEase")]
        [SerializeField] private Ease introPopEase = Ease.OutBack;

        [Tooltip("The card's scale when the pop starts; it ends at 1.")]
        [FormerlySerializedAs("resultPopStartScale")]
        [SerializeField] private float introPopStartScale = 0.8f;

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

        [Tooltip("Colour of the title: the theme's purple, #3B2466, as the overlays' titles.")]
        [SerializeField] private Color introTitleColor = new Color(0.231f, 0.141f, 0.400f);

        [SerializeField] private float introSubtitleFontSize = 56f;

        [Tooltip("Height of the subtitle's row.")]
        [SerializeField] private float introSubtitleHeightUnits = 80f;

        [Tooltip("Colour of the subtitle: the theme's purple, #3B2466, readable on the grey panel.")]
        [SerializeField] private Color introSubtitleColor = new Color(0.231f, 0.141f, 0.400f);

        [Tooltip("The area the illustration is centred in, and scaled down to when it is larger.")]
        [SerializeField] private Vector2 introIllustrationSizeUnits = new Vector2(960f, 720f);

        [Tooltip("Canvas units one board cell is drawn at in an illustration. Every other size in it is the board's own, in cells.")]
        [SerializeField] private float introCellUnits = 255f;

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

        [Tooltip("The text box behind the card's sentence, drawn 9-sliced at its own scale in its own colours, so it needs a border: the theme's dark pill, Assets/Art/Sprites/UI/pill.png.")]
        [SerializeField] private Sprite introTextBoxSprite;

        [Tooltip("Space between the text box's edges and its text.")]
        [SerializeField] private float introTextPaddingUnits = 40f;

        [SerializeField] private float introTextFontSize = 52f;
        [Tooltip("The sentence's colour: white, on the dark pill.")]
        [SerializeField] private Color introTextColor = Color.white;

        [Tooltip("Space between the card's rows: close button, title, subtitle, illustration, text box.")]
        [SerializeField] private float introSpacingUnits = 36f;

        [Tooltip("Diameter of the round close button at the card's top right, inside the panel's padding.")]
        [SerializeField] private float introCloseSizeUnits = 110f;

        [Tooltip("Size of the cross centred on the close button. At most the button's diameter.")]
        [SerializeField] private float introCloseCrossSizeUnits = 66f;

        [Tooltip("Seconds a closing card takes to fade out. Runs on unscaled time.")]
        [SerializeField] private float introCloseSeconds = 0.15f;

        [SerializeField] private Ease introCloseEase = Ease.OutQuad;

        [Tooltip("The scale a closing card shrinks to while it fades, as every overlay's close does (Module 47): from 0.8 to 1.")]
        [SerializeField] private float introCloseScale = 0.95f;

        [Header("New Joker card (Module 43; the introduction card's theme, after any mechanic cards)")]
        [Tooltip("The joker icons the New Joker card draws in place of an illustration: Assets/Config/JokerIcons.asset, the one the UI prefabs are built from.")]
        [SerializeField] private JokerIcons jokerIcons;

        [Tooltip("One entry per joker: the card's title and its sentence. {0} in the sentence is the seconds a Clock adds, from the Meta Config (D65).")]
        [SerializeField] private JokerIntroduction[] jokerIntroductions =
        {
            new JokerIntroduction(JokerKind.Clock, "Clock!", "Tap it to add {0} seconds to the clock."),
            new JokerIntroduction(JokerKind.Rocket, "Rocket!", "Tap the Rocket, then a block, to clear its colour."),
            new JokerIntroduction(JokerKind.Broom, "Broom!", "Tap the Broom, then a block, to clear that colour from every block.")
        };

        [Tooltip("The line under every New Joker card's title.")]
        [SerializeField] private string newJokerSubtitle = "New Joker Unlocked!";

        [Tooltip("The joker icon's size, centred in the illustration area with the sparkles around it.")]
        [SerializeField] private float newJokerIconSizeUnits = 540f;

        [Tooltip("The joker icon's colour: white, since the Joker Icons are drawn in their own colours (Module 45).")]
        [SerializeField] private Color newJokerIconColor = Color.white;

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
        [Tooltip("One colour per BlockColor, in enum order: Red, Blue, Green, Yellow, Purple, Orange, Pink, Cyan. A face is white in its middle, so each entry is the face colour the player sees there. Opaque.")]
        [SerializeField] private Color[] blockPalette =
        {
            new Color32(0xF0, 0x2A, 0x1E, 0xFF),
            new Color32(0x00, 0x54, 0xF6, 0xFF),
            new Color32(0x07, 0xA5, 0x15, 0xFF),
            new Color32(0xF6, 0xBA, 0x00, 0xFF),
            new Color32(0xA2, 0x36, 0xF6, 0xFF),
            new Color32(0xF6, 0x66, 0x0C, 0xFF),
            new Color32(0xF6, 0x3C, 0xA2, 0xFF),
            new Color32(0x06, 0xA8, 0xF6, 0xFF)
        };

        [Tooltip("Top of the background gradient: a dark indigo under the lighter frame, as the reference game (Module 46, pass 2). Opaque.")]
        [SerializeField] private Color backgroundTop = new Color32(0x2A, 0x24, 0x60, 0xFF);

        [Tooltip("Bottom of the background gradient, darker than its top; also the camera's clear colour. Opaque.")]
        [SerializeField] private Color backgroundBottom = new Color32(0x16, 0x12, 0x36, 0xFF);

        [Tooltip("Colour of the vignette at the screen's corners; its alpha is the vignette's strength.")]
        [SerializeField] private Color vignetteColor = new Color(0f, 0f, 0f, 0.5f);

        [Tooltip("The floor tiles' tint. The tile is greyscale, darker than its grid line, so the floor shows darker than this and the grid shows this. Opaque.")]
        [SerializeField] private Color floorColor = new Color32(0x31, 0x18, 0x5F, 0xFF);

        [Tooltip("The frame's face, white in its middle, so this is the colour shown there: a lighter bluish purple than the background, as the reference game, so the board's edge reads (Module 46, pass 2). Opaque.")]
        [SerializeField] private Color frameColor = new Color32(0x48, 0x3C, 0xA8, 0xFF);

        [Tooltip("The dark outline round the gate arrow (D79). Blocks, gates and the frame are lit bodies without one (Module 46). Opaque.")]
        [SerializeField] private Color contourColor = new Color32(0x1C, 0x0B, 0x3A, 0xFF);

        [Tooltip("The tint of a block's and a gate's highlight pieces (Module 46): white, so the highlight stays white; its alpha scales it, as the art recipe paints it.")]
        [SerializeField] private Color blockLightColor = Color.white;

        [Tooltip("The tint of the frame's highlight pieces: white, its alpha scaling the frame's highlight against the blocks'.")]
        [SerializeField] private Color frameLightColor = new Color(1f, 1f, 1f, 0.7f);

        [Tooltip("The tint of each stud's rim light. Not tinted by the block's colour: white and opaque draws it as the art recipe paints it (Module 46); a lower alpha weakens it.")]
        [SerializeField] private Color glossColor = Color.white;

        [SerializeField] private Color gateArrowColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("The Level Editor's one-way arrow. The board's is raised, in the block's own colour (Module 46).")]
        [SerializeField] private Color axisArrowColor = new Color(1f, 1f, 1f, 0.9f);

        [Tooltip("The body of a frozen block (M3) and a closed gate (M2), under the ice: its real colour stays hidden until it thaws.")]
        [FormerlySerializedAs("frozenTint")]
        [SerializeField] private Color iceColor = new Color(0.70f, 0.88f, 1.00f);

        [Tooltip("The Level Editor's number on a badge. The board's is Count Text Color.")]
        [SerializeField] private Color badgeTextColor = new Color(1.00f, 0.96f, 0.85f);

        [Tooltip("The Level Editor's padlock tint. The board's padlock is painted in Lock Gold Color.")]
        [SerializeField] private Color padlockColor = new Color(1.00f, 0.80f, 0.25f);

        [Tooltip("The Level Editor's key tint. The board's key is painted in Lock Gold Color; its gem takes the colour of the lock it opens (D47).")]
        [SerializeField] private Color keyColor = new Color(1.00f, 0.80f, 0.25f);

        [Tooltip("The Level Editor's shutter slats. The board's are painted in Shutter Cylinder Color.")]
        [SerializeField] private Color shutterSlatColor = new Color(0.30f, 0.18f, 0.45f);

        [Tooltip("The Level Editor's shutter border. The board's rim is painted in Shutter Rim Color.")]
        [SerializeField] private Color shutterBorderColor = new Color(1.00f, 0.66f, 0.20f);

        [Header("Mechanics (Module 46, pass 2; the painted ones need Gate Rush → Generate Art after a change)")]
        [Tooltip("Painted: the ice's thin tint over a frozen block or a closed gate.")]
        [SerializeField] private Color iceTintColor = new Color32(0xD8, 0xF3, 0xFF, 0xFF);

        [Tooltip("Painted: the bright rim just inside the ice's edge.")]
        [SerializeField] private Color iceRimColor = new Color32(0xE6, 0xF7, 0xFF, 0xFF);

        [Tooltip("The frost streaks' tint: white draws them as painted.")]
        [SerializeField] private Color iceStreakColor = Color.white;

        [Tooltip("The count badge's face.")]
        [SerializeField] private Color countBadgeColor = new Color32(0xE5, 0x39, 0x35, 0xFF);

        [Tooltip("The count badge's raised rim. A colour-bound shutter's badge takes the colour it counts instead.")]
        [SerializeField] private Color countBadgeRimColor = new Color32(0xFF, 0xC5, 0x31, 0xFF);

        [Tooltip("Every count's digits and a time-bonus mark's seconds: white, with the label shadow.")]
        [SerializeField] private Color countTextColor = Color.white;

        [Tooltip("Painted: the chain's links and the padlock's shackle.")]
        [SerializeField] private Color chainSilverColor = new Color32(0xBF, 0xC3, 0xDA, 0xFF);

        [Tooltip("Painted: the padlock's body and the key.")]
        [SerializeField] private Color lockGoldColor = new Color32(0xFF, 0xC5, 0x31, 0xFF);

        [Tooltip("Painted: the padlock's keyhole.")]
        [SerializeField] private Color keyholeColor = new Color32(0x6B, 0x43, 0x00, 0xFF);

        [Tooltip("Painted: the time-bonus coin's raised rim, green (time_bonus_reference.py).")]
        [SerializeField] private Color coinColor = new Color32(0x2B, 0xC2, 0x3A, 0xFF);

        [Tooltip("Painted: the coin's face.")]
        [SerializeField] private Color coinFaceColor = new Color32(0xFF, 0xF4, 0xC4, 0xFF);

        [Tooltip("Painted: the coin's hands, dark green.")]
        [SerializeField] private Color coinHandColor = new Color32(0x1E, 0x5A, 0x12, 0xFF);

        [Tooltip("Painted: a closed shutter's raised rim.")]
        [SerializeField] private Color shutterRimColor = new Color32(0xF4, 0xA3, 0x00, 0xFF);

        [Tooltip("Painted: a closed shutter's slats.")]
        [SerializeField] private Color shutterCylinderColor = new Color32(0x4B, 0x3B, 0xC0, 0xFF);

        [Tooltip("Painted: a generator machine's metal.")]
        [SerializeField] private Color machineMetalColor = new Color32(0x8E, 0x9A, 0xC0, 0xFF);

        [Tooltip("Painted: the machine's screen glass.")]
        [SerializeField] private Color machineGlassColor = new Color32(0x0B, 0x1E, 0x5E, 0xFF);

        [Tooltip("Painted: the machine's corner rivets.")]
        [SerializeField] private Color machineRivetColor = new Color32(0xC7, 0xCE, 0xE6, 0xFF);

        [Tooltip("Painted: the machine's button.")]
        [SerializeField] private Color machineButtonColor = new Color32(0xFF, 0x7A, 0x00, 0xFF);

        [Tooltip("The queued count on the machine's screen.")]
        [SerializeField] private Color machineCountColor = new Color32(0x7F, 0xE3, 0xFF, 0xFF);

        [Tooltip("Painted: an elevator's raised rim.")]
        [SerializeField] private Color elevatorRimColor = new Color32(0xFB, 0x6F, 0x00, 0xFF);

        [Tooltip("Painted: the light stripes of an elevator's hazard strips.")]
        [SerializeField] private Color hazardColor = new Color32(0xFF, 0xC5, 0x31, 0xFF);

        [Tooltip("Painted: their dark stripes.")]
        [SerializeField] private Color hazardDarkColor = new Color32(0x3A, 0x23, 0x70, 0xFF);

        [Tooltip("Painted: the dark recess the doors stand in.")]
        [SerializeField] private Color elevatorRecessColor = new Color32(0x12, 0x0A, 0x2E, 0xFF);

        [Tooltip("Painted: the elevator's doors.")]
        [SerializeField] private Color elevatorPanelColor = new Color32(0x5B, 0x45, 0xB0, 0xFF);

        [Tooltip("Painted: the doors' handles.")]
        [SerializeField] private Color elevatorHandleColor = new Color32(0xD7, 0xDA, 0xEC, 0xFF);

        [Header("Labels")]
        [Tooltip("Font size of a count badge's digits and a time-bonus mark's seconds: the reference's 34 pixels of 128.")]
        [SerializeField] private float countFontSize = 2.65625f;

        [Tooltip("Font size of the keys still needed on a padlock: the reference's 26 pixels of 128.")]
        [SerializeField] private float padlockCountFontSize = 2.03125f;

        [Tooltip("Font size of the queued count on a machine's screen: the reference's 26 pixels of 128.")]
        [SerializeField] private float machineCountFontSize = 2.03125f;

        [Tooltip("The colour of the digits' soft drop shadow; its alpha is its strength. Gate Rush → Generate Art writes it into Label Shadow Material.")]
        [SerializeField] private Color labelShadowColor = new Color(0f, 0f, 0f, 0.5f);

        [Tooltip("How far the shadow falls, x right and y up, in the label shader's underlay units: down and to the right. Written into Label Shadow Material by Generate Art.")]
        [SerializeField] private Vector2 labelShadowOffset = new Vector2(0.15f, -0.3f);

        [Tooltip("How soft the shadow's edge is, from 0 to 1, in the label shader's underlay units. Written into Label Shadow Material by Generate Art.")]
        [SerializeField] private float labelShadowSoftness = 0.3f;

        [Header("Sorting orders (back to front; context menu → Reset Sorting Orders)")]
        [SerializeField] private int backgroundOrder = DefaultBackgroundOrder;
        [SerializeField] private int vignetteOrder = DefaultVignetteOrder;
        [SerializeField] private int floorUnderlayOrder = DefaultFloorUnderlayOrder;
        [SerializeField] private int floorOrder = DefaultFloorOrder;

        [Tooltip("An elevator's rim and recess, under its doors and every block.")]
        [SerializeField] private int elevatorRimOrder = DefaultElevatorRimOrder;

        [SerializeField] private int elevatorHazardOrder = DefaultElevatorHazardOrder;
        [SerializeField] private int elevatorDoorPanelOrder = DefaultElevatorDoorPanelOrder;
        [SerializeField] private int elevatorHandleOrder = DefaultElevatorHandleOrder;

        [Tooltip("A generator machine's body. Machines sit behind the frame, which runs on over them.")]
        [SerializeField] private int machineOrder = DefaultMachineOrder;

        [SerializeField] private int machineScreenOrder = DefaultMachineScreenOrder;

        [Tooltip("A machine's vent and button.")]
        [FormerlySerializedAs("miniatureLipOrder")]
        [SerializeField] private int machineFittingsOrder = DefaultMachineFittingsOrder;

        [SerializeField] private int miniatureOrder = DefaultMiniatureOrder;

        [Tooltip("The next block's studs on a machine's screen: Frame Contour Order by default, which never lies over a screen.")]
        [SerializeField] private int miniatureStudOrder = DefaultMiniatureStudOrder;

        [Tooltip("Their gloss: Frame Lip Order by default, kept free otherwise.")]
        [SerializeField] private int miniatureGlossOrder = DefaultMiniatureGlossOrder;

        [Tooltip("The frame's floor shadow, and a gate's (Module 46; this slot held their outline): below Frame Lip Order.")]
        [SerializeField] private int frameContourOrder = DefaultFrameContourOrder;

        [Tooltip("Kept free between the frame's shadow and its body: the frame and gates no longer carry a lip (Module 46).")]
        [SerializeField] private int frameLipOrder = DefaultFrameLipOrder;

        [Tooltip("The frame's lit body, and a gate's.")]
        [SerializeField] private int frameOrder = DefaultFrameOrder;

        [Tooltip("The frame's and a gate's white highlight: above Frame Order, below Gate Arrow Outline Order.")]
        [SerializeField] private int frameShineOrder = DefaultFrameShineOrder;

        [Tooltip("The dark outline under an open gate's arrow: below Gate Mark Order.")]
        [SerializeField] private int gateArrowOutlineOrder = DefaultGateArrowOutlineOrder;

        [Tooltip("An open gate's arrow.")]
        [SerializeField] private int gateMarkOrder = DefaultGateMarkOrder;

        [Tooltip("A closed gate's ice, over its lit body: Gate Arrow Outline Order by default, since a closed gate has no arrow.")]
        [SerializeField] private int gateIceOrder = DefaultGateIceOrder;

        [Tooltip("A block's floor shadow (Module 46; this slot held its outline): above Gate Mark Order, below Block Lip Order, so it falls on the floor and the frame and under every block.")]
        [SerializeField] private int blockContourOrder = DefaultBlockContourOrder;

        [Tooltip("Kept free between a block's shadow and its body: blocks no longer carry a lip (Module 46). A previewed cell stays below it.")]
        [SerializeField] private int blockLipOrder = DefaultBlockLipOrder;

        [Tooltip("A block's lit body.")]
        [SerializeField] private int blockOrder = DefaultBlockOrder;

        [Tooltip("A block's white highlight: above Block Order, below Stud Order.")]
        [SerializeField] private int blockShineOrder = DefaultBlockShineOrder;

        [Tooltip("Studs, and an axis-restricted block's arrow, which has no studs.")]
        [SerializeField] private int studOrder = DefaultStudOrder;

        [Tooltip("Stud gloss, and the highlight of an axis-restricted block's arrow.")]
        [SerializeField] private int glossOrder = DefaultGlossOrder;

        [Tooltip("A frozen block's ice, over its studs: Layer Edge Order by default, since a frozen block shows no colour beneath and has no window.")]
        [SerializeField] private int iceOrder = DefaultIceOrder;

        [Tooltip("A frozen block's frost streaks, over its ice: Layer Order by default, for the same reason.")]
        [SerializeField] private int iceStreakOrder = DefaultIceStreakOrder;

        [Tooltip("A layered block's window rim: above Gloss Order, so the window covers the outer studs.")]
        [SerializeField] private int layerEdgeOrder = DefaultLayerEdgeOrder;

        [Tooltip("A layered block's inner block, in the colour beneath: above Layer Edge Order, below Layer Shine Order.")]
        [SerializeField] private int layerOrder = DefaultLayerOrder;

        [Tooltip("The inner block's top highlight: above Layer Order, below Layer Stud Order.")]
        [SerializeField] private int layerShineOrder = DefaultLayerShineOrder;

        [Tooltip("The inner block's studs, in the colour beneath: above Layer Shine Order, below Layer Gloss Order.")]
        [SerializeField] private int layerStudOrder = DefaultLayerStudOrder;

        [Tooltip("The inner studs' gloss, and the highlight of a layered axis-restricted block's arrow, which must show over the window: below Chain Order. The arrow itself takes Layer Stud Order: such a block has no studs.")]
        [SerializeField] private int layerGlossOrder = DefaultLayerGlossOrder;

        [SerializeField] private int chainOrder = DefaultChainOrder;

        [Tooltip("A padlock, a key or a time-bonus coin.")]
        [SerializeField] private int iconOrder = DefaultIconOrder;

        [SerializeField] private int keyGemOrder = DefaultKeyGemOrder;

        [Tooltip("The gem's highlight, added over it.")]
        [SerializeField] private int keyGemHighlightOrder = DefaultKeyGemHighlightOrder;

        [Tooltip("A closed shutter's slats.")]
        [SerializeField] private int shutterOrder = DefaultShutterOrder;

        [Tooltip("A closed shutter's rim, over its slats.")]
        [SerializeField] private int shutterBorderOrder = DefaultShutterBorderOrder;

        [Tooltip("A badge's raised rim and its shadow.")]
        [SerializeField] private int badgeRimOrder = DefaultBadgeRimOrder;

        [Tooltip("A badge's face, inside its rim.")]
        [SerializeField] private int badgeOrder = DefaultBadgeOrder;

        [Tooltip("The highlight on a badge's rim and face, added over them.")]
        [SerializeField] private int badgeHighlightOrder = DefaultBadgeHighlightOrder;

        [Tooltip("Every label: the numbers on badges, padlocks, time-bonus marks and machines' screens, in front of every other board layer.")]
        [SerializeField] private int labelOrder = DefaultLabelOrder;

        [Tooltip("A grabbed block's outline. Compared only inside the lifted block's sorting group, where it must sort below Block Contour Order; equal to another board layer's order is fine.")]
        [SerializeField] private int outlineOrder = DefaultOutlineOrder;

        [Tooltip("Sorting group order of a grabbed block, lifted above every other board layer.")]
        [SerializeField] private int liftedBlockOrder = DefaultLiftedBlockOrder;

        [Tooltip("Cubes and ice shards, above everything else on the board.")]
        [SerializeField] private int effectOrder = DefaultEffectOrder;

        [Tooltip("The glow of a gate pulling a dragged block: below Block Contour Order, so it lights the gate's mouth and never a block, and at least Gate Mark Order. Equal to Gate Mark Order is fine: the glow lies inside the grid, the marks outside it.")]
        [SerializeField] private int pullGlowOrder = DefaultPullGlowOrder;

        [Tooltip("A previewed cell's rim (D53): above Floor Order and Elevator Handle Order, below Generator Preview Order. Equal to a machine's order is fine: the preview lies inside the grid, machines outside the frame.")]
        [SerializeField] private int generatorPreviewRimOrder = DefaultGeneratorPreviewRimOrder;

        [Tooltip("A previewed cell's fill (D53): above Generator Preview Rim Order, and below Block Lip Order, Shutter Order and Lifted Block Order, so every block, a grabbed block's outline and a closed shutter cover it.")]
        [SerializeField] private int generatorPreviewOrder = DefaultGeneratorPreviewOrder;

        /// <summary>The white square the floor's backing is drawn with, and the shape of a passing block's clip mask.</summary>
        public Sprite CellSprite => cellSprite;

        /// <summary>The material every sprite uses but the lit body's: <c>Sprite-Unlit-Default</c>.</summary>
        public Material SpriteMaterial => spriteMaterial;

        /// <summary>The material of the lit faces and the studs, which multiplies them back by the art recipe's headroom.</summary>
        public Material LitMaterial => litMaterial;

        /// <summary>
        /// The material a SpriteRenderer drawing <paramref name="layer"/>
        /// uses: the lit one for faces, the highlight one for the body's
        /// white, <see cref="SpriteMaterial"/> for the rest.
        /// </summary>
        public Material QuarterMaterial(QuarterLayer layer) => MaterialFor(layer, litMaterial, specularMaterial, spriteMaterial);

        /// <summary>
        /// The material a UI Image drawing <paramref name="layer"/> uses, for
        /// the introduction cards: the UI twins of <see cref="QuarterMaterial"/>,
        /// or null — Unity's UI default — for the rest.
        /// </summary>
        public Material QuarterUiMaterial(QuarterLayer layer) => MaterialFor(layer, uiLitMaterial, uiSpecularMaterial, null);

        /// <summary>The material a UI Image drawing a stud uses: the lit UI twin.</summary>
        public Material UiLitMaterial => uiLitMaterial;

        private static Material MaterialFor(QuarterLayer layer, Material lit, Material specular, Material plain)
        {
            switch (layer)
            {
                case QuarterLayer.Lit:
                case QuarterLayer.ReversedLit:
                case QuarterLayer.Ice:
                    return lit;
                case QuarterLayer.Body:
                    return specular;
                default:
                    return plain;
            }
        }

        /// <summary>
        /// How far a shadow piece's canvas reaches past its cell, in cells:
        /// its sprite's width less a plain quarter's, as a share of a cell,
        /// which a quarter is half of (<see cref="FaceShape.ShadowQuarterRect"/>).
        /// Read from the sprites, so it always matches what the generator
        /// painted; 0 while they are unassigned.
        /// </summary>
        public float ShadowMarginCells =>
            shadowOuterTopLeftSprite != null && quarterFillSprite != null
                ? (shadowOuterTopLeftSprite.rect.width - quarterFillSprite.rect.width) / (2f * quarterFillSprite.rect.width)
                : 0f;

        /// <summary>The font of the board's labels and of the introduction cards' illustrations: Lilita One.</summary>
        public TMP_FontAsset LabelFont => labelFont;

        /// <summary>A block's grid of studs for one cell, each with its darker foot, tinted with its colour.</summary>
        public Sprite StudsSprite => studsSprite;

        /// <summary>The highlights on one cell's studs, drawn in <see cref="GlossColor"/>.</summary>
        public Sprite StudGlossSprite => studGlossSprite;

        /// <summary>One stud with its foot, centred on a cell's canvas, tinted: a layered block's inner studs, each placed on the inner block's own grid.</summary>
        public Sprite SingleStudSprite => singleStudSprite;

        /// <summary>The highlight on <see cref="SingleStudSprite"/>, drawn in <see cref="GlossColor"/>.</summary>
        public Sprite SingleStudGlossSprite => singleStudGlossSprite;

        /// <summary>The dark outline under an open gate's arrow: the arrow grown by the outline's width, drawn in <see cref="ContourColor"/>.</summary>
        public Sprite GateArrowOutlineSprite => gateArrowOutlineSprite;

        /// <summary>One floor tile with its faint grid line, tinted <see cref="FloorColor"/>.</summary>
        public Sprite FloorSprite => floorSprite;

        /// <summary>The arrow on an open gate, pointing up before it is turned outward.</summary>
        public Sprite GateArrowSprite => gateArrowSprite;

        /// <summary>The Level Editor's 9-sliced double-headed arrow of an axis-restricted block, horizontal before it is turned.</summary>
        public Sprite AxisArrowSprite => axisArrowSprite;

        /// <summary>White with alpha rising to the top: the background gradient's top colour over the clear colour.</summary>
        public Sprite BackgroundRampSprite => backgroundRampSprite;

        /// <summary>Radial alpha, tinted <see cref="VignetteColor"/>.</summary>
        public Sprite VignetteSprite => vignetteSprite;

        /// <summary>A 9-sliced bevelled rounded box whose slice border is its corner: a previewed cell's rim and fill.</summary>
        public Sprite RoundedRectSprite => roundedRectSprite;

        /// <summary>One period of a locked block's chain, silver tube links, tiled along its row.</summary>
        public Sprite ChainSprite => chainSprite;

        /// <summary>The Level Editor's padlock, tinted <see cref="PadlockColor"/>; the board's is <see cref="LockSprite"/>.</summary>
        public Sprite PadlockSprite => padlockSprite;

        /// <summary>The Level Editor's key, horizontal, tinted <see cref="KeyColor"/>; the board's is <see cref="GoldKeySprite"/>.</summary>
        public Sprite KeyBodySprite => keyBodySprite;

        /// <summary>The gem in the Level Editor's key, on the same canvas as <see cref="KeyBodySprite"/>, tinted the lock's colour.</summary>
        public Sprite KeyGemSprite => keyGemSprite;

        /// <summary>The Level Editor's shutter slats, one cell, tiled over the region.</summary>
        public Sprite ShutterSlatsSprite => shutterSlatsSprite;

        /// <summary>The Level Editor's white clock icon of a time-bonus block's mark, tinted <see cref="TimeBonusMarkIconColor"/>.</summary>
        public Sprite ClockSprite => clockSprite;

        /// <summary>
        /// The ice over a frozen block or a closed gate for <paramref name="tile"/>:
        /// a placed quarter set, as the lit body's (<see cref="QuarterSprite"/>).
        /// </summary>
        private Sprite IceQuarterSprite(QuarterTile tile) =>
            PlaceSprite(
                tile.Piece, tile.SignX, tile.SignY,
                iceOuterTopLeftSprite, iceOuterTopRightSprite, iceOuterBottomLeftSprite, iceOuterBottomRightSprite,
                iceConcaveTopLeftSprite, iceConcaveTopRightSprite, iceConcaveBottomLeftSprite, iceConcaveBottomRightSprite,
                iceEdgeTopSprite, iceEdgeBottomSprite, iceEdgeLeftSprite, iceEdgeRightSprite, iceFillSprite);

        /// <summary>A frozen block's frost streaks, white, at its top row's leftmost cell: one cell wide.</summary>
        public Sprite IceStreaksSprite => iceStreaksSprite;

        /// <summary>The frost streaks two cells wide, for an anchor cell with the block's cell to its right.</summary>
        public Sprite IceStreaksWideSprite => iceStreaksWideSprite;

        /// <summary>The count badge's raised rim and its shadow, grey, 9-sliced along its width, tinted <see cref="CountBadgeRimColor"/>.</summary>
        public Sprite BadgeRimSprite => badgeRimSprite;

        /// <summary>The count badge's face, grey, on the rim's canvas, tinted <see cref="CountBadgeColor"/>.</summary>
        public Sprite BadgeFaceSprite => badgeFaceSprite;

        /// <summary>The highlight on the badge's rim and face, white, for the specular material.</summary>
        public Sprite BadgeHighlightSprite => badgeHighlightSprite;

        /// <summary>The board's padlock, painted in its own colours, with its shadow.</summary>
        public Sprite LockSprite => lockSprite;

        /// <summary>The board's gold key, painted, with its shadow and its gem's seat.</summary>
        public Sprite GoldKeySprite => goldKeySprite;

        /// <summary>The gem in the key's bow, grey, on the key's canvas, tinted its lock's colour (D47).</summary>
        public Sprite GemSprite => gemSprite;

        /// <summary>The gem's highlight, white, for the specular material.</summary>
        public Sprite GemHighlightSprite => gemHighlightSprite;

        /// <summary>A time-bonus mark's green-rimmed clock coin, painted, with its shadow.</summary>
        public Sprite BonusCoinSprite => bonusCoinSprite;

        /// <summary>A horizontal one-way block's raised arrow, grey, 9-sliced along its length, tinted the block's colour.</summary>
        public Sprite OneWayArrowSprite => oneWayArrowSprite;

        /// <summary>Its highlight, white, for the specular material.</summary>
        public Sprite OneWayArrowHighlightSprite => oneWayArrowHighlightSprite;

        /// <summary>A vertical one-way block's arrow, painted upright, so its light still falls from the top left.</summary>
        public Sprite OneWayArrowVerticalSprite => oneWayArrowVerticalSprite;

        /// <summary>Its highlight.</summary>
        public Sprite OneWayArrowVerticalHighlightSprite => oneWayArrowVerticalHighlightSprite;

        /// <summary>A closed shutter's raised rim with its shadow and its recess, painted, 9-sliced both ways.</summary>
        public Sprite ShutterRimSprite => shutterRimSprite;

        /// <summary>One slat of a closed shutter, painted, tiled over its region.</summary>
        public Sprite ShutterCylindersSprite => shutterCylindersSprite;

        /// <summary>A generator machine's metal body with its rivets and shadow, painted, 9-sliced both ways.</summary>
        public Sprite MachineBodySprite => machineBodySprite;

        /// <summary>The machine's recessed glass screen, painted, 9-sliced both ways.</summary>
        public Sprite MachineScreenSprite => machineScreenSprite;

        /// <summary>The machine's vent.</summary>
        public Sprite MachineVentSprite => machineVentSprite;

        /// <summary>The machine's button.</summary>
        public Sprite MachineButtonSprite => machineButtonSprite;

        /// <summary>An elevator's raised rim with its shadow and its dark recess, painted, 9-sliced both ways.</summary>
        public Sprite ElevatorRimSprite => elevatorRimSprite;

        /// <summary>One tile of the hazard strip down an elevator's left side, painted.</summary>
        public Sprite ElevatorHazardLeftSprite => elevatorHazardLeftSprite;

        /// <summary>One tile of the hazard strip down its right side.</summary>
        public Sprite ElevatorHazardRightSprite => elevatorHazardRightSprite;

        /// <summary>An elevator's door with its inset panel and shadow, painted, 9-sliced both ways: both doors.</summary>
        public Sprite ElevatorDoorSprite => elevatorDoorSprite;

        /// <summary>A door's handle, painted.</summary>
        public Sprite ElevatorHandleSprite => elevatorHandleSprite;

        /// <summary>One cube of a destroyed block's burst, tinted the block's colour.</summary>
        public Sprite CubeSprite => cubeSprite;

        /// <summary>A cube's white highlight, added over it by the specular material while <see cref="DrawCubeHighlights"/> is on.</summary>
        public Sprite CubeHighlightSprite => cubeHighlightSprite;

        /// <summary>
        /// The ice shard sprites, baked in <see cref="ShardColor"/>: Shard,
        /// Shard2 and Shard3, one per outline. A burst's piece <c>i</c> draws
        /// shard <c>i</c> modulo their count, so a burst mixes all of them.
        /// </summary>
        public Sprite ShardSpriteAt(int index)
        {
            switch (index % ShardSpriteCount)
            {
                case 0:
                    return shardSprite;
                case 1:
                    return shard2Sprite;
                default:
                    return shard3Sprite;
            }
        }

        /// <summary>How many shard sprites there are.</summary>
        public const int ShardSpriteCount = 3;

        /// <summary>The shards' glassy ice, baked into their sprites by Generate Art.</summary>
        public Color ShardColor => shardColor;

        /// <summary>True when a cube's highlight is drawn as a second sprite; off by default, for performance.</summary>
        public bool DrawCubeHighlights => drawCubeHighlights;

        /// <summary>How far a breaking block's colour moves toward white at its flash's height.</summary>
        public float BreakFlashStrength => breakFlashStrength;

        /// <summary>Seconds a breaking block's flash takes to reach its height.</summary>
        public float BreakFlashSeconds => breakFlashSeconds;

        /// <summary>The most cubes one joker breaks into over every block it clears.</summary>
        public int BreakCubeMoveCap => breakCubeMoveCap;

        /// <summary>Sparkles over one block that breaks, before the caps.</summary>
        public int BreakSparkleCount => breakSparkleCount;

        /// <summary>The most sparkles one joker shows over every block it clears.</summary>
        public int BreakSparkleMoveCap => breakSparkleMoveCap;

        /// <summary>Seconds one sparkle shows.</summary>
        public float BreakSparkleSeconds => breakSparkleSeconds;

        /// <summary>A sparkle's scale at the end.</summary>
        public float BreakSparkleEndScale => breakSparkleEndScale;

        /// <summary>Sprite renderers made once for the debris and reused.</summary>
        public int DebrisPoolSize => debrisPoolSize;

        /// <summary>Seconds a finished elevator takes to sink and fade.</summary>
        public float ElevatorSinkSeconds => elevatorSinkSeconds;

        /// <summary>A finished elevator's scale when it has sunk.</summary>
        public float ElevatorSinkEndScale => elevatorSinkEndScale;

        /// <summary>How a finished elevator sinks.</summary>
        public Ease ElevatorSinkEase => elevatorSinkEase;

        /// <summary>The first ice shard, baked in <see cref="ShardColor"/> (<see cref="ShardSpriteAt"/> gives all three).</summary>
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

        /// <summary>How much each quarter tile is enlarged on every side, in cells.</summary>
        public float SeamOverlapCells => seamOverlapCells;

        /// <summary>Size of an open gate's arrow, in cells.</summary>
        public float GateArrowSizeCells => gateArrowSizeCells;

        /// <summary>Thickness of the Level Editor's axis arrow, in cells.</summary>
        public float AxisArrowThicknessCells => axisArrowThicknessCells;

        /// <summary>How far the Level Editor's axis arrow stops short of each end of the block, in cells.</summary>
        public float AxisArrowEndInsetCells => axisArrowEndInsetCells;

        /// <summary>How far a layered block's window sits inside its footprint, in cells (<see cref="FaceShape"/>).</summary>
        public float LayerInsetCells => layerInsetCells;

        /// <summary>Width of the window's dark rim, in cells.</summary>
        public float LayerEdgeCells => layerEdgeCells;

        /// <summary>
        /// How far a layered block's inner block, in the colour beneath, sits
        /// inside its footprint, in cells: the window, less its rim.
        /// </summary>
        public float LayerInnerInsetCells => layerInsetCells + layerEdgeCells;

        /// <summary>Distance between the inner block's neighbouring studs, in cells (<see cref="LayerStuds"/>).</summary>
        public float LayerStudSpacingCells => layerStudSpacingCells;

        /// <summary>How far an inner stud's centre stays from the inner block's edge, in cells (<see cref="LayerStuds"/>).</summary>
        public float LayerStudClearanceCells => layerStudClearanceCells;

        /// <summary>Height of a count badge, in cells: the painted badge's.</summary>
        public float BadgeHeightCells => badgeHeightCells;

        /// <summary>Space either side of a badge's one digit, in cells: the painted badge's.</summary>
        public float BadgePaddingCells => badgePaddingCells;

        /// <summary>How far a badge's sprite reaches past the badge, for its shadow, in cells.</summary>
        public float BadgeShadowMarginCells => badgeShadowMarginCells;

        /// <summary>How far a shutter's rim, an elevator's rim and a machine's body reach past their rectangle, in cells.</summary>
        public float RaisedShadowMarginCells => raisedShadowMarginCells;

        /// <summary>How far inside a closed shutter's region its slats start, in cells.</summary>
        public float ShutterInnerInsetCells => shutterInnerInsetCells;

        /// <summary>How far inside an elevator's region its recess starts, in cells, across and up.</summary>
        public Vector2 ElevatorInnerInsetCells => new Vector2(elevatorInnerInsetAcrossCells, elevatorInnerInsetUpCells);

        /// <summary>The gap around each of an elevator's doors, in cells.</summary>
        public float ElevatorDoorGapCells => elevatorDoorGapCells;

        /// <summary>How far a door's sprite reaches past the door, in cells.</summary>
        public float ElevatorDoorShadowMarginCells => elevatorDoorShadowMarginCells;

        /// <summary>How far either side of the doors' seam each handle sits, in cells.</summary>
        public float ElevatorHandleOffsetCells => elevatorHandleOffsetCells;

        /// <summary>How far in from an elevator's side each hazard strip's middle runs, in cells.</summary>
        public float ElevatorHazardCentreCells => elevatorHazardCentreCells;

        /// <summary>How far short of an elevator's top and bottom its hazard strips end, in cells.</summary>
        public float ElevatorHazardEndCells => elevatorHazardEndCells;

        /// <summary>How far a one-way arrow's sprite stops short of each end of its block, in cells.</summary>
        public float OneWayArrowEndInsetCells => oneWayArrowEndInsetCells;

        /// <summary>How far below a padlock's centre the keys still needed are written, in cells.</summary>
        public float PadlockCountDropCells => padlockCountDropCells;

        /// <summary>The diameter of a time-bonus mark's coin, in cells.</summary>
        public float BonusCoinCells => bonusCoinCells;

        /// <summary>How far a block's count badge sits in from its bottom-right corner, in cells (<see cref="MarkLayout.CornerBadge"/>).</summary>
        public float BlockBadgeInsetCells => blockBadgeInsetCells;

        /// <summary>How far a chain stops short of each end of its row, in cells.</summary>
        public float ChainEndInsetCells => chainEndInsetCells;

        /// <summary>A padlock's width as the marks are laid out and the Level Editor draws it, in cells.</summary>
        public float PadlockSizeCells => padlockSizeCells;

        /// <summary>A key's length as the marks are laid out and the Level Editor draws it, in cells.</summary>
        public float KeySizeCells => keySizeCells;

        /// <summary>The board's key's size relative to its painted size, gem included.</summary>
        public float KeyScale => keyScale;

        /// <summary>How far the Level Editor turns its key from horizontal, in degrees.</summary>
        public float KeyRotationDegrees => keyRotationDegrees;

        /// <summary>How far a frozen block's padlock or key is raised above its frozen count, in cells.</summary>
        public float FrozenMarkRaiseCells => frozenMarkRaiseCells;

        /// <summary>Space between a time-bonus mark's coin and the start of its seconds' advance, in cells.</summary>
        public float TimeBonusLabelGapCells => timeBonusLabelGapCells;

        /// <summary>How far below a time-bonus mark's anchor its seconds' middle sits, in cells.</summary>
        public float TimeBonusLabelDropCells => timeBonusLabelDropCells;

        /// <summary>How far a time-bonus mark's seconds' outline stands outside the glyphs, in cells.</summary>
        public float LabelOutlineStrokeCells => labelOutlineStrokeCells;

        /// <summary>The colour of a time-bonus mark's seconds' outline.</summary>
        public Color LabelOutlineColor => labelOutlineColor;

        /// <summary>The atlas padding of the font asset Generate Art makes for a time-bonus mark's seconds, in pixels.</summary>
        public int LabelOutlineAtlasPadding => labelOutlineAtlasPadding;

        /// <summary>The characters Generate Art puts in the time-bonus mark's font asset.</summary>
        public string LabelOutlineCharacters => labelOutlineCharacters;

        /// <summary>The room a time-bonus mark keeps from each side of its width on the block when it is fitted, in cells.</summary>
        public float MarkFitMarginCells => markFitMarginCells;

        /// <summary>The Level Editor's time-bonus plate's fill.</summary>
        public Color TimeBonusMarkColor => timeBonusMarkColor;

        /// <summary>The Level Editor's time-bonus plate's rim.</summary>
        public Color TimeBonusMarkRimColor => timeBonusMarkRimColor;

        /// <summary>The Level Editor's time-bonus clock tint.</summary>
        public Color TimeBonusMarkIconColor => timeBonusMarkIconColor;

        /// <summary>The scale of two marks sharing a block without room for both at full size (<see cref="MarkLayout.Row"/>).</summary>
        public float CrowdedMarkScale => crowdedMarkScale;

        /// <summary>The scale of three marks sharing a block without room for a cell each (<see cref="MarkLayout.Row"/>).</summary>
        public float CrowdedTripleMarkScale => crowdedTripleMarkScale;

        /// <summary>The share of a machine's screen, at its end along its longer side, that holds the queued count.</summary>
        public float MachineScreenCountShare => machineScreenCountShare;

        /// <summary>How much of its part of the screen the next block fills, on each side.</summary>
        public float MachineMiniatureFillShare => machineMiniatureFillShare;

        /// <summary>Where the vent (x) and the button (y) sit along a machine's outer room, from its middle, in cells.</summary>
        public Vector2 MachineFittingOffsetsCells => machineFittingOffsetsCells;

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

        /// <summary>The opacity of a gate's glow at full pull strength (Module 22).</summary>
        public float PullGlowAlpha => pullGlowAlpha;

        /// <summary>Seconds the pull's glow takes to fade out fully, and to come up fully.</summary>
        public float PullGlowFadeSeconds => pullGlowFadeSeconds;

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

        /// <summary>Opacity of a previewed cell's fill (Module 26, D53).</summary>
        public float GeneratorPreviewFillAlpha => generatorPreviewFillAlpha;

        /// <summary>Opacity of a previewed cell's rim.</summary>
        public float GeneratorPreviewRimAlpha => generatorPreviewRimAlpha;

        /// <summary>How far a previewed cell is inset from its cell's edges, in cells.</summary>
        public float GeneratorPreviewInsetCells => generatorPreviewInsetCells;

        /// <summary>Thickness of a previewed cell's rim, in cells.</summary>
        public float GeneratorPreviewRimCells => generatorPreviewRimCells;

        /// <summary>Corner radius of a previewed cell, in cells.</summary>
        public float GeneratorPreviewCornerCells => generatorPreviewCornerCells;

        /// <summary>Seconds the generator preview takes to fade in fully.</summary>
        public float GeneratorPreviewFadeInSeconds => generatorPreviewFadeInSeconds;

        /// <summary>Seconds the generator preview takes to fade out fully.</summary>
        public float GeneratorPreviewFadeOutSeconds => generatorPreviewFadeOutSeconds;

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

        /// <summary>Seconds a block a joker destroyed takes to fade where it stands (Module 34).</summary>
        public float BreakFadeSeconds => breakFadeSeconds;

        /// <summary>Easing of that fade.</summary>
        public Ease BreakFadeEase => breakFadeEase;

        /// <summary>The notice above Home's play button when Play is pressed after the last campaign level.</summary>
        public string CampaignDoneNotice => campaignDoneNotice;

        /// <summary>
        /// The font of an introduction card's title, subtitle and sentence:
        /// the theme's (Module 41), whose fallback list covers what it lacks.
        /// </summary>
        public TMP_FontAsset IntroFont => introFont;

        /// <summary>The card's panel, drawn 9-sliced behind its rows.</summary>
        public Sprite IntroPanelSprite => introPanelSprite;

        /// <summary>Space between the card's panel edges and its rows, in canvas units.</summary>
        public float IntroPanelPaddingUnits => introPanelPaddingUnits;

        /// <summary>The face of the card's round close button, drawn in its own colours.</summary>
        public Sprite IntroCloseSprite => introCloseSprite;

        /// <summary>The cross on the card's close button, drawn in its own colours.</summary>
        public Sprite IntroCloseCrossSprite => introCloseCrossSprite;

        /// <summary>Colour of the full-screen dim behind an introduction card.</summary>
        public Color IntroBackdropColor => introBackdropColor;

        /// <summary>Seconds an introduction card's opening pop takes.</summary>
        public float IntroPopSeconds => introPopSeconds;

        /// <summary>Easing of an introduction card's opening pop.</summary>
        public Ease IntroPopEase => introPopEase;

        /// <summary>An introduction card's scale when its pop starts.</summary>
        public float IntroPopStartScale => introPopStartScale;

        /// <summary>Font size of an introduction card's title.</summary>
        public float IntroTitleFontSize => introTitleFontSize;

        /// <summary>Height of an introduction card's title row, in canvas units.</summary>
        public float IntroTitleHeightUnits => introTitleHeightUnits;

        /// <summary>Colour of an introduction card's title.</summary>
        public Color IntroTitleColor => introTitleColor;

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

        /// <summary>The text box, drawn 9-sliced in its own colours.</summary>
        public Sprite IntroTextBoxSprite => introTextBoxSprite;

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

        /// <summary>Size of the cross on the close button, in canvas units.</summary>
        public float IntroCloseCrossSizeUnits => introCloseCrossSizeUnits;

        /// <summary>Seconds a closing card takes to fade out.</summary>
        public float IntroCloseSeconds => introCloseSeconds;

        /// <summary>Easing of a closing card's fade.</summary>
        public Ease IntroCloseEase => introCloseEase;

        /// <summary>The scale a closing card shrinks to while it fades.</summary>
        public float IntroCloseScale => introCloseScale;

        /// <summary>The sparkles around a card's illustration.</summary>
        public IReadOnlyList<SparklePlacement> IntroSparkles => introSparkles;

        /// <summary>The joker icons the New Joker card draws (Module 43); null when unassigned, which <see cref="Problems"/> reports.</summary>
        public JokerIcons JokerIcons => jokerIcons;

        /// <summary>The line under every New Joker card's title.</summary>
        public string NewJokerSubtitle => newJokerSubtitle;

        /// <summary>The size of the joker icon on a New Joker card, in canvas units.</summary>
        public float NewJokerIconSizeUnits => newJokerIconSizeUnits;

        /// <summary>The colour of the joker icon on a New Joker card.</summary>
        public Color NewJokerIconColor => newJokerIconColor;

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

        /// <summary>The outline round the gate arrow; drawn untinted (D79).</summary>
        public Color ContourColor => contourColor;

        /// <summary>The tint of a block's and a gate's highlight pieces: white, its alpha scaling the highlight.</summary>
        public Color BlockLightColor => blockLightColor;

        /// <summary>The tint of the frame's highlight pieces.</summary>
        public Color FrameLightColor => frameLightColor;

        /// <summary>Colour of the stud highlights.</summary>
        public Color GlossColor => glossColor;

        /// <summary>Colour of an open gate's arrow.</summary>
        public Color GateArrowColor => gateArrowColor;

        /// <summary>Colour of the Level Editor's axis arrow.</summary>
        public Color AxisArrowColor => axisArrowColor;

        /// <summary>The body of a frozen block (M3) and a closed gate (M2), under the ice.</summary>
        public Color IceColor => iceColor;

        /// <summary>The Level Editor's number on a badge.</summary>
        public Color BadgeTextColor => badgeTextColor;

        /// <summary>The Level Editor's padlock tint.</summary>
        public Color PadlockColor => padlockColor;

        /// <summary>The Level Editor's key tint.</summary>
        public Color KeyColor => keyColor;

        /// <summary>The Level Editor's shutter slats.</summary>
        public Color ShutterSlatColor => shutterSlatColor;

        /// <summary>The Level Editor's shutter border.</summary>
        public Color ShutterBorderColor => shutterBorderColor;

        /// <summary>Painted: the ice's tint.</summary>
        public Color IceTintColor => iceTintColor;

        /// <summary>Painted: the ice's bright rim.</summary>
        public Color IceRimColor => iceRimColor;

        /// <summary>The frost streaks' tint.</summary>
        public Color IceStreakColor => iceStreakColor;

        /// <summary>The count badge's face.</summary>
        public Color CountBadgeColor => countBadgeColor;

        /// <summary>The count badge's rim, when it counts no particular colour.</summary>
        public Color CountBadgeRimColor => countBadgeRimColor;

        /// <summary>Every count's digits on the board.</summary>
        public Color CountTextColor => countTextColor;

        /// <summary>Painted: the chain and the shackle.</summary>
        public Color ChainSilverColor => chainSilverColor;

        /// <summary>Painted: the padlock's body and the key.</summary>
        public Color LockGoldColor => lockGoldColor;

        /// <summary>Painted: the keyhole.</summary>
        public Color KeyholeColor => keyholeColor;

        /// <summary>Painted: the time-bonus coin's rim.</summary>
        public Color CoinColor => coinColor;

        /// <summary>Painted: the coin's face.</summary>
        public Color CoinFaceColor => coinFaceColor;

        /// <summary>Painted: the coin's hands.</summary>
        public Color CoinHandColor => coinHandColor;

        /// <summary>Painted: a shutter's rim.</summary>
        public Color ShutterRimColor => shutterRimColor;

        /// <summary>Painted: a shutter's slats.</summary>
        public Color ShutterCylinderColor => shutterCylinderColor;

        /// <summary>Painted: a machine's metal.</summary>
        public Color MachineMetalColor => machineMetalColor;

        /// <summary>Painted: a machine's glass.</summary>
        public Color MachineGlassColor => machineGlassColor;

        /// <summary>Painted: a machine's rivets.</summary>
        public Color MachineRivetColor => machineRivetColor;

        /// <summary>Painted: a machine's button.</summary>
        public Color MachineButtonColor => machineButtonColor;

        /// <summary>The queued count on a machine's screen.</summary>
        public Color MachineCountColor => machineCountColor;

        /// <summary>Painted: an elevator's rim.</summary>
        public Color ElevatorRimColor => elevatorRimColor;

        /// <summary>Painted: the hazard strips' light stripes.</summary>
        public Color HazardColor => hazardColor;

        /// <summary>Painted: their dark stripes.</summary>
        public Color HazardDarkColor => hazardDarkColor;

        /// <summary>Painted: an elevator's recess.</summary>
        public Color ElevatorRecessColor => elevatorRecessColor;

        /// <summary>Painted: an elevator's doors.</summary>
        public Color ElevatorPanelColor => elevatorPanelColor;

        /// <summary>Painted: the doors' handles.</summary>
        public Color ElevatorHandleColor => elevatorHandleColor;

        /// <summary>Font size of a count badge's digits and a time-bonus mark's seconds.</summary>
        public float CountFontSize => countFontSize;

        /// <summary>Font size of the keys still needed on a padlock.</summary>
        public float PadlockCountFontSize => padlockCountFontSize;

        /// <summary>Font size of the queued count on a machine's screen.</summary>
        public float MachineCountFontSize => machineCountFontSize;

        /// <summary>The digits' drop shadow's colour; its alpha is its strength.</summary>
        public Color LabelShadowColor => labelShadowColor;

        /// <summary>How far the digits' shadow falls, in the label shader's underlay units.</summary>
        public Vector2 LabelShadowOffset => labelShadowOffset;

        /// <summary>How soft the digits' shadow is, in the label shader's underlay units.</summary>
        public float LabelShadowSoftness => labelShadowSoftness;

        /// <summary>The label font's material with the digits' drop shadow; null until Generate Art has made it.</summary>
        public Material LabelShadowMaterial => labelShadowMaterial;

        /// <summary>The label font's own material, which <see cref="LabelShadowMaterial"/> is made from; null without a label font.</summary>
        public Material LabelFontMaterial => labelFont != null ? labelFont.material : null;

        /// <summary>The time-bonus mark's font asset, with room for its outline; null until Generate Art has made it.</summary>
        public TMP_FontAsset LabelOutlineFont => labelOutlineFont;

        /// <summary><see cref="LabelOutlineFont"/>'s material with the outline and the drop shadow; null until Generate Art has made it.</summary>
        public Material LabelOutlineMaterial => labelOutlineMaterial;

        /// <summary>
        /// True when the outlined label can be drawn: both
        /// <see cref="LabelOutlineFont"/> and <see cref="LabelOutlineMaterial"/>
        /// are assigned, since the material only reads its own font's atlas.
        /// Otherwise an outlined label falls back to the label font with
        /// <see cref="LabelShadowMaterial"/>. The one test for the board and
        /// the introduction cards.
        /// </summary>
        public bool HasLabelOutline => labelOutlineFont != null && labelOutlineMaterial != null;

        /// <summary>Sorting order of the background gradient.</summary>
        public int BackgroundOrder => backgroundOrder;

        /// <summary>Sorting order of the vignette.</summary>
        public int VignetteOrder => vignetteOrder;

        /// <summary>Sorting order of the floor's backing, beneath the tiles.</summary>
        public int FloorUnderlayOrder => floorUnderlayOrder;

        /// <summary>Sorting order of the floor tiles.</summary>
        public int FloorOrder => floorOrder;

        /// <summary>Sorting order of an elevator's rim and recess.</summary>
        public int ElevatorRimOrder => elevatorRimOrder;

        /// <summary>Sorting order of an elevator's hazard strips.</summary>
        public int ElevatorHazardOrder => elevatorHazardOrder;

        /// <summary>Sorting order of an elevator's doors.</summary>
        public int ElevatorDoorPanelOrder => elevatorDoorPanelOrder;

        /// <summary>Sorting order of the doors' handles.</summary>
        public int ElevatorHandleOrder => elevatorHandleOrder;

        /// <summary>Sorting order of a generator machine's body.</summary>
        public int MachineOrder => machineOrder;

        /// <summary>Sorting order of a generator machine's screen.</summary>
        public int MachineScreenOrder => machineScreenOrder;

        /// <summary>Sorting order of a machine's vent and button.</summary>
        public int MachineFittingsOrder => machineFittingsOrder;

        /// <summary>Sorting order of the next block on a machine's screen.</summary>
        public int MiniatureOrder => miniatureOrder;

        /// <summary>Sorting order of the next block's studs on a machine's screen.</summary>
        public int MiniatureStudOrder => miniatureStudOrder;

        /// <summary>Sorting order of their gloss.</summary>
        public int MiniatureGlossOrder => miniatureGlossOrder;

        /// <summary>Sorting order of the frame's and gates' outline.</summary>
        public int FrameContourOrder => frameContourOrder;

        /// <summary>Sorting order of the frame's and gates' lip.</summary>
        public int FrameLipOrder => frameLipOrder;

        /// <summary>Sorting order of the frame's and gates' face.</summary>
        public int FrameOrder => frameOrder;

        /// <summary>Sorting order of the frame's rim and a gate's gloss.</summary>
        public int FrameShineOrder => frameShineOrder;

        /// <summary>Sorting order of the outline under an open gate's arrow.</summary>
        public int GateArrowOutlineOrder => gateArrowOutlineOrder;

        /// <summary>Sorting order of an open gate's arrow.</summary>
        public int GateMarkOrder => gateMarkOrder;

        /// <summary>Sorting order of a closed gate's ice.</summary>
        public int GateIceOrder => gateIceOrder;

        /// <summary>Sorting order of a frozen block's ice.</summary>
        public int IceOrder => iceOrder;

        /// <summary>Sorting order of a frozen block's frost streaks.</summary>
        public int IceStreakOrder => iceStreakOrder;

        /// <summary>Sorting order of a block's outline.</summary>
        public int BlockContourOrder => blockContourOrder;

        /// <summary>Sorting order of a block's lip.</summary>
        public int BlockLipOrder => blockLipOrder;

        /// <summary>Sorting order of a block's face quarters.</summary>
        public int BlockOrder => blockOrder;

        /// <summary>Sorting order of a layered block's window rim, above the outer studs.</summary>
        public int LayerEdgeOrder => layerEdgeOrder;

        /// <summary>Sorting order of a layered block's inner block.</summary>
        public int LayerOrder => layerOrder;

        /// <summary>Sorting order of the inner block's top highlight.</summary>
        public int LayerShineOrder => layerShineOrder;

        /// <summary>Sorting order of the inner block's studs.</summary>
        public int LayerStudOrder => layerStudOrder;

        /// <summary>Sorting order of the inner studs' gloss and of a layered axis-restricted block's arrow.</summary>
        public int LayerGlossOrder => layerGlossOrder;

        /// <summary>Sorting order of a block's top highlight.</summary>
        public int BlockShineOrder => blockShineOrder;

        /// <summary>Sorting order of a block's studs, and of an axis-restricted block's arrow.</summary>
        public int StudOrder => studOrder;

        /// <summary>Sorting order of the stud highlights and of an axis-restricted block's arrow's highlight.</summary>
        public int GlossOrder => glossOrder;

        /// <summary>Sorting order of a locked block's chains.</summary>
        public int ChainOrder => chainOrder;

        /// <summary>Sorting order of a padlock or a key's body.</summary>
        public int IconOrder => iconOrder;

        /// <summary>Sorting order of a key's gem.</summary>
        public int KeyGemOrder => keyGemOrder;

        /// <summary>Sorting order of the gem's highlight.</summary>
        public int KeyGemHighlightOrder => keyGemHighlightOrder;

        /// <summary>Sorting order of a closed shutter's slats.</summary>
        public int ShutterOrder => shutterOrder;

        /// <summary>Sorting order of a closed shutter's border.</summary>
        public int ShutterBorderOrder => shutterBorderOrder;

        /// <summary>Sorting order of a badge's raised rim and its shadow.</summary>
        public int BadgeRimOrder => badgeRimOrder;

        /// <summary>Sorting order of a badge's face.</summary>
        public int BadgeOrder => badgeOrder;

        /// <summary>Sorting order of a badge's highlight.</summary>
        public int BadgeHighlightOrder => badgeHighlightOrder;

        /// <summary>Sorting order of every label, in front of every other board layer.</summary>
        public int LabelOrder => labelOrder;

        /// <summary>Sorting order of a grabbed block's outline, compared only inside its lifted sorting group.</summary>
        public int OutlineOrder => outlineOrder;

        /// <summary>Sorting group order of a grabbed block.</summary>
        public int LiftedBlockOrder => liftedBlockOrder;

        /// <summary>Sorting order of cubes and ice shards.</summary>
        public int EffectOrder => effectOrder;

        /// <summary>Sorting order of a pulling gate's glow, below every block (Module 22).</summary>
        public int PullGlowOrder => pullGlowOrder;

        /// <summary>Sorting order of a previewed cell's rim, above the floor and an elevator's doors (Module 26).</summary>
        public int GeneratorPreviewRimOrder => generatorPreviewRimOrder;

        /// <summary>Sorting order of a previewed cell's fill, below every block, a grabbed block's outline and a closed shutter.</summary>
        public int GeneratorPreviewOrder => generatorPreviewOrder;

        /// <summary>
        /// The generated sprite <paramref name="tile"/> is drawn with in
        /// <paramref name="layer"/>'s set. A plain piece depends on the piece
        /// alone. A lit, highlight or shadow piece depends on the quarter's
        /// place too — its corner of the cell, or the side an edge faces —
        /// and <see cref="QuarterLayer.ReversedLit"/> takes the lit piece of
        /// the opposite place. A fill has a lit and a highlight piece of its
        /// own, the flat top's; it casts no shadow past the shape
        /// (<see cref="QuarterLayers.Edged"/>), so null is returned for it.
        /// </summary>
        public Sprite QuarterSprite(QuarterLayer layer, QuarterTile tile)
        {
            switch (layer)
            {
                case QuarterLayer.Lit:
                case QuarterLayer.ReversedLit:
                {
                    var flip = layer == QuarterLayer.ReversedLit ? -1 : 1;
                    return PlaceSprite(
                        tile.Piece, tile.SignX * flip, tile.SignY * flip,
                        faceOuterTopLeftSprite, faceOuterTopRightSprite, faceOuterBottomLeftSprite, faceOuterBottomRightSprite,
                        faceConcaveTopLeftSprite, faceConcaveTopRightSprite, faceConcaveBottomLeftSprite, faceConcaveBottomRightSprite,
                        faceEdgeTopSprite, faceEdgeBottomSprite, faceEdgeLeftSprite, faceEdgeRightSprite, faceFillSprite);
                }

                case QuarterLayer.Body:
                    return PlaceSprite(
                        tile.Piece, tile.SignX, tile.SignY,
                        bodyOuterTopLeftSprite, bodyOuterTopRightSprite, bodyOuterBottomLeftSprite, bodyOuterBottomRightSprite,
                        bodyConcaveTopLeftSprite, bodyConcaveTopRightSprite, bodyConcaveBottomLeftSprite, bodyConcaveBottomRightSprite,
                        bodyEdgeTopSprite, bodyEdgeBottomSprite, bodyEdgeLeftSprite, bodyEdgeRightSprite, bodyFillSprite);
                case QuarterLayer.Shadow:
                    return PlaceSprite(
                        tile.Piece, tile.SignX, tile.SignY,
                        shadowOuterTopLeftSprite, shadowOuterTopRightSprite, shadowOuterBottomLeftSprite, shadowOuterBottomRightSprite,
                        shadowConcaveTopLeftSprite, shadowConcaveTopRightSprite, shadowConcaveBottomLeftSprite, shadowConcaveBottomRightSprite,
                        shadowEdgeTopSprite, shadowEdgeBottomSprite, shadowEdgeLeftSprite, shadowEdgeRightSprite, null);
                case QuarterLayer.Ice:
                    return IceQuarterSprite(tile);
            }

            switch (tile.SpriteKind)
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

        /// <summary>
        /// Of one set of place-dependent pieces, the one for
        /// <paramref name="piece"/> as if at corner <c>(signX, signY)</c> of
        /// its cell: a corner piece's by that corner, an edge along x's by the
        /// side it faces up or down, an edge along y's by the side it faces
        /// left or right, and a fill's <paramref name="fill"/>.
        /// </summary>
        private static Sprite PlaceSprite(
            QuarterPiece piece, int signX, int signY,
            Sprite outerTopLeft, Sprite outerTopRight, Sprite outerBottomLeft, Sprite outerBottomRight,
            Sprite concaveTopLeft, Sprite concaveTopRight, Sprite concaveBottomLeft, Sprite concaveBottomRight,
            Sprite edgeTop, Sprite edgeBottom, Sprite edgeLeft, Sprite edgeRight, Sprite fill)
        {
            var isRight = signX > 0;
            var isTop = signY > 0;
            switch (piece)
            {
                case QuarterPiece.OuterCorner:
                    return isTop
                        ? isRight ? outerTopRight : outerTopLeft
                        : isRight ? outerBottomRight : outerBottomLeft;
                case QuarterPiece.ConcaveCorner:
                    return isTop
                        ? isRight ? concaveTopRight : concaveTopLeft
                        : isRight ? concaveBottomRight : concaveBottomLeft;
                case QuarterPiece.EdgeAlongX:
                    return isTop ? edgeTop : edgeBottom;
                case QuarterPiece.EdgeAlongY:
                    return isRight ? edgeRight : edgeLeft;
                default:
                    return fill;
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

        /// <summary>The window's dark rim, for an outer colour of <paramref name="outer"/>: its RGB darkened, its alpha kept.</summary>
        public Color LayerEdgeFill(Color outer) => Darken(outer, layerEdgeDarken);

        private static Color Darken(Color color, float by) => new Color(color.r * by, color.g * by, color.b * by, color.a);

        /// <summary>The lip colour under a face of <paramref name="face"/>: its RGB darkened, its alpha kept.</summary>
        public Color LipFill(Color face) =>
            new Color(face.r * lipDarken, face.g * lipDarken, face.b * lipDarken, face.a);

        /// <summary>
        /// The scale a block's marks are drawn at for <paramref name="crowding"/>:
        /// 1 with room, <see cref="CrowdedMarkScale"/> for a crowded pair,
        /// <see cref="CrowdedTripleMarkScale"/> for a crowded triple. The one
        /// mapping for the board and the Level Editor (Module 24).
        /// </summary>
        public float MarkScale(MarkCrowding crowding)
        {
            switch (crowding)
            {
                case MarkCrowding.Roomy:
                    return 1f;
                case MarkCrowding.CrowdedPair:
                    return crowdedMarkScale;
                case MarkCrowding.CrowdedTriple:
                    return crowdedTripleMarkScale;
                default:
                    throw new ArgumentOutOfRangeException(nameof(crowding), crowding, "Unknown mark crowding.");
            }
        }

        /// <summary>The width, in cells, of the badge showing <paramref name="value"/>.</summary>
        public float BadgeWidthCells(int value) =>
            MarkLayout.BadgeWidth(value, badgeHeightCells, badgeDigitWidthCells, badgePaddingCells);

        /// <summary>The text on the time-bonus mark of a block that adds <paramref name="seconds"/> (M10).</summary>
        public string TimeBonusMarkText(int seconds) =>
            string.Format(CultureInfo.InvariantCulture, timeBonusMarkFormat, seconds);

        /// <summary>
        /// The width, in cells, of the time-bonus mark reading
        /// <paramref name="text"/> at full size (Module 46, pass 2, revision
        /// B): the coin, the gap and the seconds' advance, the outline not
        /// counted, as <c>time_bonus_reference_b.py</c> measures the group it
        /// centres on the anchor. Nothing is drawn behind them.
        /// </summary>
        public float TimeBonusMarkWidthCells(string text) =>
            bonusCoinCells + timeBonusLabelGapCells + TimeBonusLabelAdvanceCells(text);

        /// <summary>
        /// The advance, in cells, of the seconds reading <paramref name="text"/>
        /// at <see cref="CountFontSize"/>: the sum of their glyphs' advances in
        /// <see cref="LabelOutlineFont"/> — or <see cref="LabelFont"/>, the
        /// same face, before Generate Art has made it — as a world-space
        /// TextMeshPro label lays them, without kerning, like every label on
        /// the board. A character the font lacks, or every character when
        /// neither font is assigned (a fresh config; <see cref="Problems"/>
        /// reports it), takes Time Bonus Mark Character Width Cells.
        /// </summary>
        public float TimeBonusLabelAdvanceCells(string text)
        {
            var font = labelOutlineFont != null ? labelOutlineFont : labelFont;
            if (font == null)
            {
                return text.Length * timeBonusMarkCharacterWidthCells;
            }

            var face = font.faceInfo;
            var cellsPerFontUnit = countFontSize * WorldUnitsPerFontSize * face.scale / face.pointSize / cellSize;
            var lookup = font.characterLookupTable;
            var advance = 0f;
            foreach (var character in text)
            {
                advance += lookup != null && lookup.TryGetValue(character, out var found)
                    ? found.glyph.metrics.horizontalAdvance * found.glyph.scale * cellsPerFontUnit
                    : timeBonusMarkCharacterWidthCells;
            }

            return advance;
        }

        /// <summary>
        /// Where the coin's centre sits, right of the anchor, in cells, on the
        /// mark reading <paramref name="text"/>: the group of
        /// <see cref="TimeBonusMarkWidthCells"/> is centred on the anchor, so
        /// the coin moves with the text.
        /// </summary>
        public float TimeBonusCoinCenterCells(string text) => (bonusCoinCells - TimeBonusMarkWidthCells(text)) * 0.5f;

        /// <summary>Where the seconds reading <paramref name="text"/> start, right of the anchor, in cells: past the coin and the gap.</summary>
        public float TimeBonusLabelStartCells(string text) =>
            -TimeBonusMarkWidthCells(text) * 0.5f + bonusCoinCells + timeBonusLabelGapCells;

        /// <summary>
        /// How far the full-size time-bonus mark reading <paramref name="text"/>
        /// reaches from its anchor on either side, in cells: half its width
        /// with the outline counted at both ends. The group is centred, so this
        /// is what must fit.
        /// </summary>
        public float TimeBonusMarkReachCells(string text) => TimeBonusMarkWidthCells(text) * 0.5f + labelOutlineStrokeCells;

        /// <summary>
        /// The scale of the time-bonus mark reading <paramref name="text"/> on
        /// a block whose marks are <paramref name="crowding"/>, where the mark
        /// has <paramref name="roomCells"/> of width centred on its slot
        /// (<see cref="MarkLayout.BonusRoomCells"/>): the marks' scale
        /// (<see cref="MarkScale"/>), times what fits the coin and the seconds
        /// together, uniformly, inside that width less
        /// <see cref="MarkFitMarginCells"/> on each side — never above 1. On a
        /// 2×1 the mark keeps its full size; on a 1×1, a vertical 1×2 or an L
        /// it shrinks.
        /// </summary>
        public float TimeBonusMarkScale(float roomCells, MarkCrowding crowding, string text)
        {
            var fit = (roomCells * 0.5f - markFitMarginCells) / TimeBonusMarkReachCells(text);
            return MarkScale(crowding) * Math.Min(1f, fit);
        }

        /// <summary>The title of <paramref name="mechanic"/>'s introduction card.</summary>
        /// <exception cref="InvalidOperationException">No entry introduces it; see <see cref="Problems"/>.</exception>
        public string IntroductionTitle(LevelMechanic mechanic) => IntroductionOf(mechanic).Title;

        /// <summary>The text in the text box of <paramref name="mechanic"/>'s introduction card.</summary>
        /// <exception cref="InvalidOperationException">No entry introduces it; see <see cref="Problems"/>.</exception>
        public string IntroductionText(LevelMechanic mechanic) => IntroductionOf(mechanic).Text;

        /// <summary>The line under the title of <paramref name="mechanic"/>'s card: How to Play has its own.</summary>
        public string IntroductionSubtitle(LevelMechanic mechanic) =>
            mechanic == LevelMechanic.HowToPlay ? howToPlaySubtitle : introSubtitle;

        /// <summary>The title of <paramref name="kind"/>'s New Joker card: "Rocket!" by default.</summary>
        /// <exception cref="InvalidOperationException">No entry introduces it; see <see cref="Problems"/>.</exception>
        public string NewJokerTitle(JokerKind kind) => IntroductionOf(kind).Title;

        /// <summary>
        /// The sentence of <paramref name="kind"/>'s New Joker card, with
        /// <paramref name="clockBonusSeconds"/> — the rules' Clock seconds — in
        /// place of {0}, read with the invariant culture.
        /// </summary>
        /// <exception cref="InvalidOperationException">No entry introduces it; see <see cref="Problems"/>.</exception>
        /// <exception cref="FormatException">The sentence is not a usable format; see <see cref="Problems"/>.</exception>
        public string NewJokerText(JokerKind kind, int clockBonusSeconds) =>
            string.Format(CultureInfo.InvariantCulture, IntroductionOf(kind).Text, clockBonusSeconds);

        /// <summary>The first entry for <paramref name="kind"/>.</summary>
        private JokerIntroduction IntroductionOf(JokerKind kind)
        {
            if (jokerIntroductions != null)
            {
                for (var i = 0; i < jokerIntroductions.Length; i++)
                {
                    if (jokerIntroductions[i] != null && jokerIntroductions[i].Joker == kind)
                    {
                        return jokerIntroductions[i];
                    }
                }
            }

            throw new InvalidOperationException($"{name}: Joker Introductions has no entry for {kind}.");
        }

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

        /// <summary>
        /// The layout of the cubes a block a joker destroyed breaks into, flying
        /// out from its centre (Module 47): its own count, cap, travel, spread
        /// and delay, and the exit cubes' size and spin.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A value is out of range; see <see cref="Problems"/>.</exception>
        public BurstSettings CreateBreakCubeBurst() =>
            new BurstSettings(
                breakCubeCountPerCell, breakCubeCap, cubeSizeCells.x, cubeSizeCells.y, breakCubeTravelCells.x, breakCubeTravelCells.y,
                breakCubeSpreadDegrees, cubeSpinDegrees, breakCubeMaxDelaySeconds);

        /// <summary>
        /// The layout of the sparkles over a block a joker destroyed: one per
        /// cell before the caps, which <see cref="BreakSparkleCount"/> and
        /// <see cref="BreakSparkleMoveCap"/> set, unspun.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A value is out of range; see <see cref="Problems"/>.</exception>
        public BurstSettings CreateBreakSparkleBurst() =>
            new BurstSettings(
                SparklesPerCell, Math.Max(SparklesPerCell, breakSparkleCount), breakSparkleSizeCells.x, breakSparkleSizeCells.y,
                breakSparkleTravelCells.x, breakSparkleTravelCells.y, breakCubeSpreadDegrees, SparkleSpinDegrees, breakCubeMaxDelaySeconds);

        // A sparkle's layout spreads it over the block's cells, one a cell
        // before its count caps it, and never turns it. Not tunables: the
        // count and the caps are.
        private const int SparklesPerCell = 1;
        private const float SparkleSpinDegrees = 0f;

        // The least a closing introduction card may shrink to: the bound of
        // Intro Close Scale, not a tunable.
        private const float MinIntroCloseScale = 0.8f;

        /// <summary>The layout of the ice shards of a thaw or a gate opening.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A shard value is out of range; see <see cref="Problems"/>.</exception>
        public BurstSettings CreateShardBurst() =>
            new BurstSettings(
                shardCountPerCell, shardCap, shardSizeCells.x, shardSizeCells.y, shardTravelCells.x, shardTravelCells.y,
                shardSpreadDegrees, shardSpinDegrees, shardMaxDelaySeconds);

        /// <summary>
        /// The settings of one drag: the input values and the gate pull's
        /// (Module 22), with the pull's ease as a plain function so
        /// <see cref="DragController"/> needs no tweening library.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A drag value is out of range; see <see cref="Problems"/>.</exception>
        public DragSettings CreateDragSettings()
        {
            var ease = pullEase;
            return new DragSettings(
                pushThresholdCells, followRate, cornerAssistCells, pullRangeCells, pullAmount, captureRangeCells,
                pullFollowRate, nudgeMaxCells, linear => DOVirtual.EasedValue(0f, 1f, linear, ease));
        }

        /// <summary>The generator machine placement rule these values describe.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A machine size is out of range; see <see cref="Problems"/>.</exception>
        public GeneratorMachine CreateGeneratorMachine() =>
            new GeneratorMachine(
                frameThicknessCells, machineDepthCells, machineSideOverhangCells, machineFrameOverlapCells,
                machineScreenInsetCells, machineCountRoomHeightCells, machineCountRoomDigitWidthCells, machineCountRoomPaddingCells);

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

            foreach (var (material, shownName) in new[]
                     {
                         (litMaterial, "Lit Material"), (specularMaterial, "Specular Material"),
                         (uiLitMaterial, "Ui Lit Material"), (uiSpecularMaterial, "Ui Specular Material")
                     })
            {
                if (material == null)
                {
                    problems.Add($"{name}: {shownName} is not assigned; run Gate Rush → Generate Art.");
                }
            }

            if (labelFont == null)
            {
                problems.Add($"{name}: Label Font is not assigned; create the Lilita One font asset from Assets/ThirdParty/Fonts/LilitaOne and assign it.");
            }

            AddIfUnassigned(problems, quarterOuterSprite, "Quarter Outer Sprite");
            AddIfUnassigned(problems, quarterEdgeSprite, "Quarter Edge Sprite");
            AddIfUnassigned(problems, quarterFillSprite, "Quarter Fill Sprite");
            AddIfUnassigned(problems, quarterConcaveSprite, "Quarter Concave Sprite");
            AddIfUnassigned(problems, faceOuterTopLeftSprite, "Face Outer Top Left Sprite");
            AddIfUnassigned(problems, faceOuterTopRightSprite, "Face Outer Top Right Sprite");
            AddIfUnassigned(problems, faceOuterBottomLeftSprite, "Face Outer Bottom Left Sprite");
            AddIfUnassigned(problems, faceOuterBottomRightSprite, "Face Outer Bottom Right Sprite");
            AddIfUnassigned(problems, faceConcaveTopLeftSprite, "Face Concave Top Left Sprite");
            AddIfUnassigned(problems, faceConcaveTopRightSprite, "Face Concave Top Right Sprite");
            AddIfUnassigned(problems, faceConcaveBottomLeftSprite, "Face Concave Bottom Left Sprite");
            AddIfUnassigned(problems, faceConcaveBottomRightSprite, "Face Concave Bottom Right Sprite");
            AddIfUnassigned(problems, faceEdgeTopSprite, "Face Edge Top Sprite");
            AddIfUnassigned(problems, faceEdgeBottomSprite, "Face Edge Bottom Sprite");
            AddIfUnassigned(problems, faceEdgeLeftSprite, "Face Edge Left Sprite");
            AddIfUnassigned(problems, faceEdgeRightSprite, "Face Edge Right Sprite");
            AddIfUnassigned(problems, faceFillSprite, "Face Fill Sprite");
            AddIfUnassigned(problems, bodyOuterTopLeftSprite, "Body Outer Top Left Sprite");
            AddIfUnassigned(problems, bodyOuterTopRightSprite, "Body Outer Top Right Sprite");
            AddIfUnassigned(problems, bodyOuterBottomLeftSprite, "Body Outer Bottom Left Sprite");
            AddIfUnassigned(problems, bodyOuterBottomRightSprite, "Body Outer Bottom Right Sprite");
            AddIfUnassigned(problems, bodyConcaveTopLeftSprite, "Body Concave Top Left Sprite");
            AddIfUnassigned(problems, bodyConcaveTopRightSprite, "Body Concave Top Right Sprite");
            AddIfUnassigned(problems, bodyConcaveBottomLeftSprite, "Body Concave Bottom Left Sprite");
            AddIfUnassigned(problems, bodyConcaveBottomRightSprite, "Body Concave Bottom Right Sprite");
            AddIfUnassigned(problems, bodyEdgeTopSprite, "Body Edge Top Sprite");
            AddIfUnassigned(problems, bodyEdgeBottomSprite, "Body Edge Bottom Sprite");
            AddIfUnassigned(problems, bodyEdgeLeftSprite, "Body Edge Left Sprite");
            AddIfUnassigned(problems, bodyEdgeRightSprite, "Body Edge Right Sprite");
            AddIfUnassigned(problems, bodyFillSprite, "Body Fill Sprite");
            AddIfUnassigned(problems, shadowOuterTopLeftSprite, "Shadow Outer Top Left Sprite");
            AddIfUnassigned(problems, shadowOuterTopRightSprite, "Shadow Outer Top Right Sprite");
            AddIfUnassigned(problems, shadowOuterBottomLeftSprite, "Shadow Outer Bottom Left Sprite");
            AddIfUnassigned(problems, shadowOuterBottomRightSprite, "Shadow Outer Bottom Right Sprite");
            AddIfUnassigned(problems, shadowConcaveTopLeftSprite, "Shadow Concave Top Left Sprite");
            AddIfUnassigned(problems, shadowConcaveTopRightSprite, "Shadow Concave Top Right Sprite");
            AddIfUnassigned(problems, shadowConcaveBottomLeftSprite, "Shadow Concave Bottom Left Sprite");
            AddIfUnassigned(problems, shadowConcaveBottomRightSprite, "Shadow Concave Bottom Right Sprite");
            AddIfUnassigned(problems, shadowEdgeTopSprite, "Shadow Edge Top Sprite");
            AddIfUnassigned(problems, shadowEdgeBottomSprite, "Shadow Edge Bottom Sprite");
            AddIfUnassigned(problems, shadowEdgeLeftSprite, "Shadow Edge Left Sprite");
            AddIfUnassigned(problems, shadowEdgeRightSprite, "Shadow Edge Right Sprite");
            AddIfUnassigned(problems, studsSprite, "Studs Sprite");
            AddIfUnassigned(problems, studGlossSprite, "Stud Gloss Sprite");
            AddIfUnassigned(problems, singleStudSprite, "Single Stud Sprite");
            AddIfUnassigned(problems, singleStudGlossSprite, "Single Stud Gloss Sprite");
            AddIfUnassigned(problems, floorSprite, "Floor Sprite");
            AddIfUnassigned(problems, gateArrowSprite, "Gate Arrow Sprite");
            AddIfUnassigned(problems, gateArrowOutlineSprite, "Gate Arrow Outline Sprite");
            AddIfUnassigned(problems, axisArrowSprite, "Axis Arrow Sprite");
            AddIfUnassigned(problems, backgroundRampSprite, "Background Ramp Sprite");
            AddIfUnassigned(problems, vignetteSprite, "Vignette Sprite");
            AddIfUnassigned(problems, roundedRectSprite, "Rounded Rect Sprite");
            AddIfUnassigned(problems, chainSprite, "Chain Sprite");
            AddIfUnassigned(problems, padlockSprite, "Padlock Sprite");
            AddIfUnassigned(problems, keyBodySprite, "Key Body Sprite");
            AddIfUnassigned(problems, keyGemSprite, "Key Gem Sprite");
            AddIfUnassigned(problems, shutterSlatsSprite, "Shutter Slats Sprite");
            AddIfUnassigned(problems, clockSprite, "Clock Sprite");
            AddIfUnassigned(problems, cubeSprite, "Cube Sprite");
            AddIfUnassigned(problems, cubeHighlightSprite, "Cube Highlight Sprite");
            AddIfUnassigned(problems, shardSprite, "Shard Sprite");
            AddIfUnassigned(problems, shard2Sprite, "Shard 2 Sprite");
            AddIfUnassigned(problems, shard3Sprite, "Shard 3 Sprite");
            AddIfUnassigned(problems, gateGlowSprite, "Gate Glow Sprite");
            AddIfUnassigned(problems, sparkleSprite, "Sparkle Sprite");
            AddMechanicSpriteProblems(problems);

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
                problems.Add($"{name}: Badge Height Cells and Badge Digit Width Cells must be positive, and Badge Padding Cells at least 0; run Gate Rush → Generate Art.");
            }

            if (!(blockBadgeInsetCells >= 0f && blockBadgeInsetCells < 0.5f))
            {
                problems.Add($"{name}: Block Badge Inset Cells must be at least 0 and below 0.5, so a count badge stays on its corner cell.");
            }

            if (!(chainEndInsetCells >= 0f && chainEndInsetCells < 0.5f))
            {
                problems.Add($"{name}: Chain End Inset Cells must be at least 0 and below 0.5.");
            }

            if (!(padlockSizeCells > 0f && keySizeCells > 0f))
            {
                problems.Add($"{name}: Padlock Size Cells and Key Size Cells must be positive.");
            }

            AddIfNotPositive(problems, keyScale, "Key Scale");

            AddMechanicMeasureProblems(problems);

            foreach (var problem in GeneratorMachine.Problems(
                         machineDepthCells, machineSideOverhangCells, machineFrameOverlapCells, machineScreenInsetCells))
            {
                problems.Add($"{name}: {problem}");
            }

            foreach (var problem in DragSettings.Problems(
                         pushThresholdCells, followRate, cornerAssistCells, pullRangeCells, pullAmount, captureRangeCells,
                         pullFollowRate, nudgeMaxCells))
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

            AddBoardLookProblems(problems);

            AddMetaProblems(problems);
            AddJokerBreakProblems(problems);
            AddMarkProblems(problems);
            AddIntroductionProblems(problems);
            AddNewJokerProblems(problems);
            return problems;
        }

        /// <summary>
        /// The constraints on Module 46's board look, each message naming its
        /// field: the colours a face or a backdrop shows must be opaque, or the
        /// floor and the background would show through them; the shine's
        /// alpha is its strength and must lie in [0, 1]; and the outline sits
        /// under what it rings, the shine over the face and under the studs.
        /// </summary>
        private void AddBoardLookProblems(List<string> problems)
        {
            if (blockPalette != null)
            {
                for (var i = 0; i < blockPalette.Length; i++)
                {
                    if (!(blockPalette[i].a >= 1f))
                    {
                        problems.Add($"{name}: Block Palette entry {i} ({(BlockColor)i}) must be opaque.");
                    }
                }
            }

            AddIfNotOpaque(problems, contourColor, "Contour Color");
            AddIfNotOpaque(problems, floorColor, "Floor Color");
            AddIfNotOpaque(problems, frameColor, "Frame Color");
            AddIfNotOpaque(problems, backgroundTop, "Background Top");
            AddIfNotOpaque(problems, backgroundBottom, "Background Bottom");

            AddIfAlphaOutOfRange(problems, blockLightColor, "Block Light Color");
            AddIfAlphaOutOfRange(problems, frameLightColor, "Frame Light Color");

            if (!(frameContourOrder < frameLipOrder))
            {
                problems.Add($"{name}: Frame Contour Order must be below Frame Lip Order, so the shadow sits under the frame and its gates.");
            }

            if (!(frameOrder < frameShineOrder && frameShineOrder < gateArrowOutlineOrder && gateArrowOutlineOrder < gateMarkOrder))
            {
                problems.Add($"{name}: Frame Shine Order must be above Frame Order, Gate Arrow Outline Order above Frame Shine Order and Gate Mark Order above Gate Arrow Outline Order, so a gate's arrow sits on its outline over the gloss.");
            }

            if (!(gateMarkOrder < blockContourOrder && blockContourOrder < blockLipOrder))
            {
                problems.Add($"{name}: Block Contour Order must be above Gate Mark Order and below Block Lip Order, so a block's shadow sits under every block and over the frame.");
            }
        }

        /// <summary>The sprites of Module 46's second pass, each reported by its field's name when it is not assigned.</summary>
        private void AddMechanicSpriteProblems(List<string> problems)
        {
            foreach (var (sprite, shownName) in new[]
                     {
                         (iceOuterTopLeftSprite, "Ice Outer Top Left Sprite"), (iceOuterTopRightSprite, "Ice Outer Top Right Sprite"),
                         (iceOuterBottomLeftSprite, "Ice Outer Bottom Left Sprite"), (iceOuterBottomRightSprite, "Ice Outer Bottom Right Sprite"),
                         (iceConcaveTopLeftSprite, "Ice Concave Top Left Sprite"), (iceConcaveTopRightSprite, "Ice Concave Top Right Sprite"),
                         (iceConcaveBottomLeftSprite, "Ice Concave Bottom Left Sprite"),
                         (iceConcaveBottomRightSprite, "Ice Concave Bottom Right Sprite"),
                         (iceEdgeTopSprite, "Ice Edge Top Sprite"), (iceEdgeBottomSprite, "Ice Edge Bottom Sprite"),
                         (iceEdgeLeftSprite, "Ice Edge Left Sprite"), (iceEdgeRightSprite, "Ice Edge Right Sprite"),
                         (iceFillSprite, "Ice Fill Sprite"), (iceStreaksSprite, "Ice Streaks Sprite"),
                         (iceStreaksWideSprite, "Ice Streaks Wide Sprite"), (badgeRimSprite, "Badge Rim Sprite"),
                         (badgeFaceSprite, "Badge Face Sprite"), (badgeHighlightSprite, "Badge Highlight Sprite"),
                         (lockSprite, "Lock Sprite"), (goldKeySprite, "Gold Key Sprite"), (gemSprite, "Gem Sprite"),
                         (gemHighlightSprite, "Gem Highlight Sprite"), (bonusCoinSprite, "Bonus Coin Sprite"),
                         (oneWayArrowSprite, "One Way Arrow Sprite"), (oneWayArrowHighlightSprite, "One Way Arrow Highlight Sprite"),
                         (oneWayArrowVerticalSprite, "One Way Arrow Vertical Sprite"),
                         (oneWayArrowVerticalHighlightSprite, "One Way Arrow Vertical Highlight Sprite"),
                         (shutterRimSprite, "Shutter Rim Sprite"), (shutterCylindersSprite, "Shutter Cylinders Sprite"),
                         (machineBodySprite, "Machine Body Sprite"), (machineScreenSprite, "Machine Screen Sprite"),
                         (machineVentSprite, "Machine Vent Sprite"), (machineButtonSprite, "Machine Button Sprite"),
                         (elevatorRimSprite, "Elevator Rim Sprite"), (elevatorHazardLeftSprite, "Elevator Hazard Left Sprite"),
                         (elevatorHazardRightSprite, "Elevator Hazard Right Sprite"), (elevatorDoorSprite, "Elevator Door Sprite"),
                         (elevatorHandleSprite, "Elevator Handle Sprite")
                     })
            {
                AddIfUnassigned(problems, sprite, shownName);
            }

            if (labelShadowMaterial == null)
            {
                problems.Add($"{name}: Label Shadow Material is not assigned; run Gate Rush → Generate Art.");
            }

            if (labelOutlineFont == null)
            {
                problems.Add($"{name}: Label Outline Font is not assigned; run Gate Rush → Generate Art.");
            }

            if (labelOutlineMaterial == null)
            {
                problems.Add($"{name}: Label Outline Material is not assigned; run Gate Rush → Generate Art.");
            }
        }

        /// <summary>
        /// The constraints on Module 46's second pass, each message naming its
        /// field: the measures Generate Art writes lie in range, the painted
        /// colours and the tinted ones a face shows are opaque, the fonts have
        /// a size, the machine's screen keeps room for its block and its count,
        /// and each mechanic's pieces sort in the order they are laid.
        /// </summary>
        private void AddMechanicMeasureProblems(List<string> problems)
        {
            foreach (var (value, shownName) in new[]
                     {
                         (badgeShadowMarginCells, "Badge Shadow Margin Cells"), (raisedShadowMarginCells, "Raised Shadow Margin Cells"),
                         (shutterInnerInsetCells, "Shutter Inner Inset Cells"),
                         (elevatorInnerInsetAcrossCells, "Elevator Inner Inset Across Cells"),
                         (elevatorInnerInsetUpCells, "Elevator Inner Inset Up Cells"), (elevatorDoorGapCells, "Elevator Door Gap Cells"),
                         (elevatorDoorShadowMarginCells, "Elevator Door Shadow Margin Cells"),
                         (elevatorHandleOffsetCells, "Elevator Handle Offset Cells"), (elevatorHazardCentreCells, "Elevator Hazard Centre Cells"),
                         (elevatorHazardEndCells, "Elevator Hazard End Cells"), (oneWayArrowEndInsetCells, "One Way Arrow End Inset Cells"),
                         (padlockCountDropCells, "Padlock Count Drop Cells")
                     })
            {
                if (!(value >= 0f && value < 0.5f))
                {
                    problems.Add($"{name}: {shownName} must be at least 0 and below 0.5; run Gate Rush → Generate Art.");
                }
            }

            foreach (var (color, shownName) in new[]
                     {
                         (iceColor, "Ice Color"), (iceTintColor, "Ice Tint Color"), (iceRimColor, "Ice Rim Color"),
                         (countBadgeColor, "Count Badge Color"), (countBadgeRimColor, "Count Badge Rim Color"),
                         (chainSilverColor, "Chain Silver Color"), (lockGoldColor, "Lock Gold Color"), (keyholeColor, "Keyhole Color"),
                         (coinColor, "Coin Color"), (coinFaceColor, "Coin Face Color"), (coinHandColor, "Coin Hand Color"),
                         (shutterRimColor, "Shutter Rim Color"), (shutterCylinderColor, "Shutter Cylinder Color"),
                         (machineMetalColor, "Machine Metal Color"), (machineGlassColor, "Machine Glass Color"),
                         (machineRivetColor, "Machine Rivet Color"), (machineButtonColor, "Machine Button Color"),
                         (elevatorRimColor, "Elevator Rim Color"), (hazardColor, "Hazard Color"), (hazardDarkColor, "Hazard Dark Color"),
                         (elevatorRecessColor, "Elevator Recess Color"), (elevatorPanelColor, "Elevator Panel Color"),
                         (elevatorHandleColor, "Elevator Handle Color")
                     })
            {
                AddIfNotOpaque(problems, color, shownName);
            }

            AddIfAlphaOutOfRange(problems, iceStreakColor, "Ice Streak Color");
            AddIfAlphaOutOfRange(problems, countTextColor, "Count Text Color");
            AddIfAlphaOutOfRange(problems, machineCountColor, "Machine Count Color");
            AddIfAlphaOutOfRange(problems, labelShadowColor, "Label Shadow Color");
            AddIfNotPositive(problems, countFontSize, "Count Font Size");
            AddIfNotPositive(problems, padlockCountFontSize, "Padlock Count Font Size");
            AddIfNotPositive(problems, machineCountFontSize, "Machine Count Font Size");

            if (!(labelShadowSoftness >= 0f && labelShadowSoftness <= 1f))
            {
                problems.Add($"{name}: Label Shadow Softness must be from 0 to 1.");
            }

            if (!(machineScreenCountShare > 0f && machineScreenCountShare < 1f))
            {
                problems.Add($"{name}: Machine Screen Count Share must be above 0 and below 1, so the screen shows both the next block and the count.");
            }

            if (!(machineMiniatureFillShare > 0f && machineMiniatureFillShare <= 1f))
            {
                problems.Add($"{name}: Machine Miniature Fill Share must be above 0 and at most 1.");
            }

            if (!(machineCountRoomHeightCells > 0f && machineCountRoomDigitWidthCells > 0f && machineCountRoomPaddingCells >= 0f))
            {
                problems.Add($"{name}: Machine Count Room Height Cells and Machine Count Room Digit Width Cells must be positive, and Machine Count Room Padding Cells at least 0.");
            }

            if (!(elevatorRimOrder < elevatorHazardOrder && elevatorHazardOrder < elevatorDoorPanelOrder
                  && elevatorDoorPanelOrder < elevatorHandleOrder))
            {
                problems.Add($"{name}: Elevator Rim Order, Elevator Hazard Order, Elevator Door Panel Order and Elevator Handle Order must rise in that order, so the doors stand in the recess and carry their handles.");
            }

            if (!(machineOrder < machineScreenOrder && machineScreenOrder < machineFittingsOrder && machineFittingsOrder < miniatureOrder
                  && miniatureOrder < miniatureStudOrder && miniatureStudOrder < miniatureGlossOrder))
            {
                problems.Add($"{name}: Machine Order, Machine Screen Order, Machine Fittings Order, Miniature Order, Miniature Stud Order and Miniature Gloss Order must rise in that order, so the screen and the next block show on the body.");
            }

            if (!(glossOrder < iceOrder && iceOrder < iceStreakOrder && iceStreakOrder < chainOrder && frameShineOrder < gateIceOrder))
            {
                problems.Add($"{name}: Ice Order must be above Gloss Order, Ice Streak Order above it and below Chain Order, and Gate Ice Order above Frame Shine Order, so the ice lies over the studs and the gate.");
            }

            if (!(keyGemOrder < keyGemHighlightOrder && badgeRimOrder < badgeOrder && badgeOrder < badgeHighlightOrder
                  && badgeHighlightOrder < labelOrder && shutterOrder < shutterBorderOrder))
            {
                problems.Add($"{name}: Key Gem Highlight Order must be above Key Gem Order, Badge Order above Badge Rim Order, Badge Highlight Order above Badge Order and below Label Order, and Shutter Border Order above Shutter Order.");
            }
        }

        private void AddIfNotOpaque(List<string> problems, Color color, string shownName)
        {
            if (!(color.a >= 1f))
            {
                problems.Add($"{name}: {shownName} must be opaque.");
            }
        }

        private void AddIfAlphaOutOfRange(List<string> problems, Color color, string shownName)
        {
            if (!(color.a >= 0f && color.a <= 1f))
            {
                problems.Add($"{name}: {shownName}'s alpha must be from 0 to 1.");
            }
        }

        /// <summary>
        /// The constraints on a layered block's window and inner block (Modules
        /// 20 and 46), each message naming its field. The insets are bounded
        /// by half a cell: an outer corner's quarter is pulled in on both sides
        /// and scales as a whole, its rounding with it, so the inner block
        /// stays visible while the window, its rim and the lip
        /// leave it a height.
        /// </summary>
        private void AddLayerProblems(List<string> problems)
        {
            if (!(layerInsetCells > 0f && layerInsetCells < 0.5f))
            {
                problems.Add($"{name}: Layer Inset Cells must be above 0 and below 0.5, so a layered block keeps a visible inner shape.");
            }

            if (!(layerEdgeCells >= 0f))
            {
                problems.Add($"{name}: Layer Edge Cells may not be negative.");
            }

            if (!(layerInsetCells + layerEdgeCells < 0.5f))
            {
                problems.Add($"{name}: Layer Inset Cells and Layer Edge Cells together must be below 0.5, so a layered block's inner block keeps a height.");
            }

            if (!(layerEdgeDarken >= 0f && layerEdgeDarken <= 1f))
            {
                problems.Add($"{name}: Layer Edge Darken must be from 0 to 1.");
            }

            if (!(layerStudSpacingCells > 0f && layerStudClearanceCells > 0f))
            {
                problems.Add($"{name}: Layer Stud Spacing Cells and Layer Stud Clearance Cells must be above 0.");
            }

            // The window covers the outer studs, and the inner block's studs
            // sit on the window.
            if (!(blockOrder < blockShineOrder && blockShineOrder < studOrder && studOrder < glossOrder
                  && glossOrder < layerEdgeOrder && layerEdgeOrder < layerOrder
                  && layerOrder < layerShineOrder && layerShineOrder < layerStudOrder && layerStudOrder < layerGlossOrder
                  && layerGlossOrder < chainOrder))
            {
                problems.Add($"{name}: Block Shine Order, Stud Order, Gloss Order, Layer Edge Order, Layer Order, Layer Shine Order, Layer Stud Order and Layer Gloss Order must rise in that order between Block Order and Chain Order, so a layered block's window covers its outer studs and carries its own.");
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

            AddIfNotPositive(problems, bonusCoinCells, "Bonus Coin Cells");
            AddIfNotPositive(problems, timeBonusMarkCharacterWidthCells, "Time Bonus Mark Character Width Cells");
            AddIfNotPositive(problems, labelOutlineStrokeCells, "Label Outline Stroke Cells");
            AddIfNotOpaque(problems, labelOutlineColor, "Label Outline Color");
            if (!(timeBonusLabelGapCells >= 0f))
            {
                problems.Add($"{name}: Time Bonus Label Gap Cells must be at least 0.");
            }

            if (labelOutlineAtlasPadding < 1)
            {
                problems.Add($"{name}: Label Outline Atlas Padding must be at least 1.");
            }

            if (isFormatUsable)
            {
                AddMissingOutlineCharacters(problems);
            }

            if (!(markFitMarginCells >= 0f && markFitMarginCells < 0.5f * MarkLayout.SharedMarkRoomCells))
            {
                problems.Add($"{name}: Mark Fit Margin Cells must be at least 0 and below half a cell, so a fitted time-bonus mark keeps a size.");
                return;
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

            // A time-bonus mark sharing a block is fitted to a cell less the
            // margin (TimeBonusMarkScale), so it is measured at that size.
            var widestBonus = TimeBonusMarkText(WidestBonusSecondsChecked);

            // Two marks with room sit a whole cell apart, so each may be a
            // cell wide; crowded ones sit half a cell apart at the crowded
            // scale, so together they may be a cell wide before scaling. The
            // time-bonus group is centred, so its span, outline included, is
            // twice its reach.
            var icon = Math.Max(padlockSizeCells, keySizeCells);
            var bonus = 2f * TimeBonusMarkReachCells(widestBonus)
                        * TimeBonusMarkScale(MarkLayout.SharedMarkRoomCells, MarkCrowding.Roomy, widestBonus);
            var roomyWidth = 2f * MarkLayout.RoomyOffsetCells;
            var crowdedWidth = 4f * MarkLayout.CrowdedOffsetCells;
            if (!(icon <= roomyWidth && bonus <= roomyWidth && crowdedMarkScale * (icon + bonus) <= crowdedWidth))
            {
                problems.Add(
                    $"{name}: a padlock or key and a time-bonus mark sharing a block would overlap. Padlock Size Cells, Key Size Cells and the " +
                    $"mark's width for a two-digit bonus fitted to a cell ({bonus:0.##} cells, from Bonus Coin Cells, Time Bonus Label Gap Cells, " +
                    $"the seconds' advance, Label Outline Stroke Cells and Mark Fit Margin Cells) must each be at most {roomyWidth:0.##}, and Crowded Mark " +
                    $"Scale times the icon plus the mark at most {crowdedWidth:0.##}.");
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

        /// <summary>
        /// Every character a time-bonus mark shows, for any bonus up to
        /// <see cref="WidestBonusSecondsChecked"/> seconds, must be in
        /// <see cref="LabelOutlineCharacters"/>: Label Outline Font holds only
        /// those, so a missing one would not be drawn.
        /// </summary>
        private void AddMissingOutlineCharacters(List<string> problems)
        {
            var missing = new SortedSet<char>();
            for (var seconds = 0; seconds <= WidestBonusSecondsChecked; seconds++)
            {
                foreach (var character in TimeBonusMarkText(seconds))
                {
                    if (labelOutlineCharacters == null || labelOutlineCharacters.IndexOf(character) < 0)
                    {
                        missing.Add(character);
                    }
                }
            }

            if (missing.Count > 0)
            {
                problems.Add(
                    $"{name}: Label Outline Characters must hold every character a time-bonus mark shows; it lacks \"{string.Concat(missing)}\".");
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

            AddIntroLookProblems(problems);

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

            if (!(introTextBoxSizeUnits.x > 0f && introTextBoxSizeUnits.y > 0f))
            {
                problems.Add($"{name}: Intro Text Box Size Units must be positive.");
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

            if (!(introCloseSizeUnits > 0f && introCloseCrossSizeUnits > 0f && introCloseCrossSizeUnits <= introCloseSizeUnits)
                || float.IsInfinity(introCloseSizeUnits))
            {
                problems.Add($"{name}: Intro Close Size Units must be positive and finite, and Intro Close Cross Size Units positive and at most it.");
            }

            AddIfNotPositive(problems, introCloseSeconds, "Intro Close Seconds");
            if (!(introCloseScale >= MinIntroCloseScale && introCloseScale <= 1f))
            {
                problems.Add($"{name}: Intro Close Scale must be from {MinIntroCloseScale} to 1.");
            }

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

        /// <summary>
        /// The constraints on the New Joker card (Module 43), each message
        /// naming its field or joker: the Joker Icons are assigned and report
        /// no problem; every joker has exactly one entry, with a title and a
        /// sentence that formats with the Clock's seconds; the subtitle is not
        /// empty; the icon has a positive size and a colour that shows.
        /// </summary>
        private void AddNewJokerProblems(List<string> problems)
        {
            if (jokerIcons == null)
            {
                problems.Add($"{name}: Joker Icons is not assigned; assign Assets/Config/JokerIcons.asset.");
            }
            else
            {
                problems.AddRange(jokerIcons.Problems());
            }

            foreach (JokerKind kind in Enum.GetValues(typeof(JokerKind)))
            {
                var entries = 0;
                JokerIntroduction first = null;
                for (var i = 0; jokerIntroductions != null && i < jokerIntroductions.Length; i++)
                {
                    if (jokerIntroductions[i] != null && jokerIntroductions[i].Joker == kind)
                    {
                        first = first ?? jokerIntroductions[i];
                        entries++;
                    }
                }

                if (entries == 0)
                {
                    problems.Add($"{name}: Joker Introductions has no entry for {kind}.");
                    continue;
                }

                if (entries > 1)
                {
                    problems.Add($"{name}: Joker Introductions has {entries} entries for {kind}; it needs exactly one.");
                }

                if (string.IsNullOrWhiteSpace(first.Title))
                {
                    problems.Add($"{name}: Joker Introductions' entry for {kind} has no title.");
                }

                if (string.IsNullOrWhiteSpace(first.Text))
                {
                    problems.Add($"{name}: Joker Introductions' entry for {kind} has no text.");
                }
                else if (!IsUsableFormat(first.Text))
                {
                    problems.Add($"{name}: Joker Introductions' entry for {kind} must have at most the placeholder {{0}}, for the Clock's seconds, and balanced braces.");
                }
            }

            if (string.IsNullOrWhiteSpace(newJokerSubtitle))
            {
                problems.Add($"{name}: New Joker Subtitle may not be empty.");
            }

            AddIfNotPositive(problems, newJokerIconSizeUnits, "New Joker Icon Size Units");

            if (!(newJokerIconColor.a > 0f))
            {
                problems.Add($"{name}: New Joker Icon Color must not be fully transparent.");
            }
        }

        /// <summary>
        /// The constraints on the introduction card's theme (Module 41), each
        /// message naming its field: the font and the four sprites are
        /// assigned, the panel and text box sprites have a border to be drawn
        /// 9-sliced, the
        /// padding is not negative, and the pop takes a positive time from a
        /// positive scale.
        /// </summary>
        private void AddIntroLookProblems(List<string> problems)
        {
            if (introFont == null)
            {
                problems.Add($"{name}: Intro Font is not assigned; assign Assets/ThirdParty/Fonts/Fredoka/Fredoka-Bold SDF.asset.");
            }

            AddIfUnslicedIntroSprite(problems, introPanelSprite, "Intro Panel Sprite", "Assets/Art/Sprites/UI/panel.png");
            AddIfUnslicedIntroSprite(problems, introTextBoxSprite, "Intro Text Box Sprite", "Assets/Art/Sprites/UI/pill.png");

            if (introCloseSprite == null)
            {
                problems.Add($"{name}: Intro Close Sprite is not assigned; assign Assets/Art/Sprites/UI/button_round_red.png.");
            }

            if (introCloseCrossSprite == null)
            {
                problems.Add($"{name}: Intro Close Cross Sprite is not assigned; assign Assets/ThirdParty/Kenney/GameIcons/cross.png.");
            }

            if (!(introPanelPaddingUnits >= 0f) || float.IsInfinity(introPanelPaddingUnits))
            {
                problems.Add($"{name}: Intro Panel Padding Units must be at least 0 and finite.");
            }

            AddIfNotPositive(problems, introPopSeconds, "Intro Pop Seconds");
            AddIfNotPositive(problems, introPopStartScale, "Intro Pop Start Scale");
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

            if (!(pullGlowAlpha >= 0f && pullGlowAlpha <= 1f))
            {
                problems.Add($"{name}: Pull Glow Alpha must be from 0 to 1.");
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
            AddIfNotPositive(problems, pullGlowFadeSeconds, "Pull Glow Fade Seconds");
            AddIfNotPositive(problems, cubeSeconds, "Cube Seconds");
            AddIfNotPositive(problems, shardSeconds, "Shard Seconds");
            AddIfNotPositive(problems, shutterLiftSeconds, "Shutter Lift Seconds");
            AddIfNotPositive(problems, lockOpenSeconds, "Lock Open Seconds");
            AddIfNotPositive(problems, badgePopSeconds, "Badge Pop Seconds");
            AddIfNotPositive(problems, generatorSpawnSeconds, "Generator Spawn Seconds");
            AddIfNotPositive(problems, doorsOpenSeconds, "Doors Open Seconds");
            AddIfNotPositive(problems, doorsCloseSeconds, "Doors Close Seconds");
            AddIfNotPositive(problems, riseSeconds, "Rise Seconds");

            if (!(outlineOrder < blockContourOrder))
            {
                problems.Add($"{name}: Outline Order must be below Block Contour Order, so the outline sits under the block's contour.");
            }

            if (!(pullGlowOrder < blockContourOrder && pullGlowOrder >= gateMarkOrder))
            {
                problems.Add(
                    $"{name}: Pull Glow Order must be below Block Contour Order and at least Gate Mark Order, so the glow lights the gate's mouth and not a block.");
            }

            if (!(generatorPreviewFillAlpha > 0f && generatorPreviewFillAlpha <= 1f
                  && generatorPreviewRimAlpha > 0f && generatorPreviewRimAlpha <= 1f))
            {
                problems.Add($"{name}: Generator Preview Fill Alpha and Generator Preview Rim Alpha must be above 0 and at most 1.");
            }

            if (!(generatorPreviewInsetCells >= 0f && generatorPreviewRimCells > 0f
                  && generatorPreviewInsetCells + generatorPreviewRimCells < 0.5f))
            {
                problems.Add($"{name}: Generator Preview Inset Cells must be at least 0, Generator Preview Rim Cells above 0, and the two together below 0.5, so a previewed cell keeps a fill inside its rim.");
            }

            if (!(generatorPreviewCornerCells > generatorPreviewRimCells) || float.IsInfinity(generatorPreviewCornerCells))
            {
                problems.Add($"{name}: Generator Preview Corner Cells must be above Generator Preview Rim Cells and finite, so the fill inside the rim keeps a rounded corner.");
            }

            AddIfNotPositive(problems, generatorPreviewFadeInSeconds, "Generator Preview Fade In Seconds");
            AddIfNotPositive(problems, generatorPreviewFadeOutSeconds, "Generator Preview Fade Out Seconds");

            if (!(generatorPreviewRimOrder > floorOrder && generatorPreviewRimOrder > elevatorHandleOrder
                  && generatorPreviewOrder > generatorPreviewRimOrder))
            {
                problems.Add($"{name}: Generator Preview Rim Order must be above Floor Order and Elevator Handle Order, and Generator Preview Order above Generator Preview Rim Order, so the preview shows on the floor and on an elevator's doors.");
            }

            if (!(generatorPreviewOrder < blockLipOrder && generatorPreviewOrder < shutterOrder
                  && generatorPreviewOrder < liftedBlockOrder))
            {
                problems.Add($"{name}: Generator Preview Order must be below Block Lip Order, Shutter Order and Lifted Block Order, so every block, a grabbed block's outline and a closed shutter cover the preview.");
            }
        }

        /// <summary>
        /// The constraint on the meta game's one value left here (Module 32):
        /// Home's campaign-done notice is there to read. The result panel's
        /// and the confirmation's texts are the UI Config's (Module 41).
        /// </summary>
        private void AddMetaProblems(List<string> problems)
        {
            AddIfBlank(problems, campaignDoneNotice, "Campaign Done Notice");
        }

        /// <summary>
        /// The constraint on the joker break (Module 34): a destroyed frozen
        /// block's fade takes a positive time.
        /// </summary>
        private void AddJokerBreakProblems(List<string> problems)
        {
            AddIfNotPositive(problems, breakFadeSeconds, "Break Fade Seconds");
            AddEffectProblems(problems);
        }

        /// <summary>
        /// The constraints on Module 47's debris, joker break, pool and
        /// finished elevator, each message naming its field: the joker's caps
        /// stay within the exit's, and the pool holds one exit's cubes, one
        /// joker clear's cubes and sparkles and one burst of shards at once.
        /// </summary>
        private void AddEffectProblems(List<string> problems)
        {
            if (!(shardColor.a >= 1f))
            {
                problems.Add($"{name}: Shard Color must be opaque.");
            }

            if (!(breakFlashStrength >= 0f && breakFlashStrength <= 1f))
            {
                problems.Add($"{name}: Break Flash Strength must be from 0 to 1.");
            }

            if (!(breakFlashSeconds > 0f && breakFlashSeconds <= MaxEffectSeconds))
            {
                problems.Add($"{name}: Break Flash Seconds must be above 0 and at most {MaxEffectSeconds}.");
            }

            if (!(breakCubeCountPerCell >= 1 && breakCubeCountPerCell <= cubeCountPerCell))
            {
                problems.Add($"{name}: Break Cube Count Per Cell must be at least 1 and at most Cube Count Per Cell ({cubeCountPerCell}).");
            }

            if (!(breakCubeCap >= 1 && breakCubeCap <= cubeCap))
            {
                problems.Add($"{name}: Break Cube Cap must be at least 1 and at most Cube Cap ({cubeCap}).");
            }

            if (!(breakCubeMoveCap >= breakCubeCap && breakCubeMoveCap <= MaxBreakCubeMoveCap))
            {
                problems.Add($"{name}: Break Cube Move Cap must be at least Break Cube Cap ({breakCubeCap}) and at most {MaxBreakCubeMoveCap}.");
            }

            foreach (var problem in BurstSettings.Problems(
                         "Break Cube", breakCubeCountPerCell, breakCubeCap, cubeSizeCells.x, cubeSizeCells.y, breakCubeTravelCells.x,
                         breakCubeTravelCells.y, breakCubeSpreadDegrees, cubeSpinDegrees, breakCubeMaxDelaySeconds))
            {
                problems.Add($"{name}: {problem}");
            }

            if (!(breakSparkleCount >= 0 && breakSparkleCount <= MaxBreakSparkleCount))
            {
                problems.Add($"{name}: Break Sparkle Count must be from 0 to {MaxBreakSparkleCount}.");
            }

            if (!(breakSparkleMoveCap >= 0 && breakSparkleMoveCap <= MaxBreakSparkleMoveCap))
            {
                problems.Add($"{name}: Break Sparkle Move Cap must be from 0 to {MaxBreakSparkleMoveCap}.");
            }

            if (!(breakSparkleSizeCells.x > 0f && breakSparkleSizeCells.y >= breakSparkleSizeCells.x) || float.IsInfinity(breakSparkleSizeCells.y))
            {
                problems.Add($"{name}: Break Sparkle Size Cells must have a positive smallest size (x) no larger than its largest (y).");
            }

            if (!(breakSparkleTravelCells.x > 0f && breakSparkleTravelCells.y >= breakSparkleTravelCells.x) || float.IsInfinity(breakSparkleTravelCells.y))
            {
                problems.Add($"{name}: Break Sparkle Travel Cells must have a positive shortest travel (x) no longer than its longest (y).");
            }

            if (!(breakSparkleSeconds > 0f && breakSparkleSeconds <= MaxEffectSeconds))
            {
                problems.Add($"{name}: Break Sparkle Seconds must be above 0 and at most {MaxEffectSeconds}.");
            }

            if (!(breakSparkleEndScale >= 0f && breakSparkleEndScale <= 1f))
            {
                problems.Add($"{name}: Break Sparkle End Scale must be from 0 to 1.");
            }

            var needed = DebrisPoolNeed;
            if (!(debrisPoolSize >= needed && debrisPoolSize <= MaxDebrisPoolSize))
            {
                problems.Add($"{name}: Debris Pool Size must be at least {needed} — one exit's cubes, one joker clear's cubes and sparkles, one burst of shards and a gate glow at once — and at most {MaxDebrisPoolSize}.");
            }

            if (!(elevatorSinkSeconds > 0f && elevatorSinkSeconds <= MaxEffectSeconds))
            {
                problems.Add($"{name}: Elevator Sink Seconds must be above 0 and at most {MaxEffectSeconds}.");
            }

            if (!(elevatorSinkEndScale >= 0f && elevatorSinkEndScale < 1f))
            {
                problems.Add($"{name}: Elevator Sink End Scale must be at least 0 and below 1.");
            }
        }

        /// <summary>
        /// The fewest pooled renderers the debris needs: one exit's cubes, one
        /// joker clear's cubes and sparkles, one burst of shards and one gate
        /// glow at once, a cube taking two while its highlight is drawn.
        /// </summary>
        public int DebrisPoolNeed =>
            (cubeCap + breakCubeMoveCap) * (drawCubeHighlights ? 2 : 1) + breakSparkleMoveCap + shardCap + 1;

        // The bounds of Module 47's counts and durations: past them an
        // effect would drag on or crowd the board. Not tunables.
        private const float MaxEffectSeconds = 2f;
        private const int MaxBreakCubeMoveCap = 128;
        private const int MaxBreakSparkleCount = 8;
        private const int MaxBreakSparkleMoveCap = 32;
        private const int MaxDebrisPoolSize = 512;

        private void AddIfBlank(List<string> problems, string text, string field)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                problems.Add($"{name}: {field} is empty.");
            }
        }

        private static bool IsUsableFormat(string format) => IsUsableFormat(format, 1);

        /// <summary>The shared format check (<see cref="FormatStrings.IsUsable"/>).</summary>
        private static bool IsUsableFormat(string format, int argumentCount) => FormatStrings.IsUsable(format, argumentCount);

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
            elevatorRimOrder = DefaultElevatorRimOrder;
            elevatorHazardOrder = DefaultElevatorHazardOrder;
            elevatorDoorPanelOrder = DefaultElevatorDoorPanelOrder;
            elevatorHandleOrder = DefaultElevatorHandleOrder;
            machineOrder = DefaultMachineOrder;
            machineScreenOrder = DefaultMachineScreenOrder;
            machineFittingsOrder = DefaultMachineFittingsOrder;
            miniatureOrder = DefaultMiniatureOrder;
            miniatureStudOrder = DefaultMiniatureStudOrder;
            miniatureGlossOrder = DefaultMiniatureGlossOrder;
            frameContourOrder = DefaultFrameContourOrder;
            frameLipOrder = DefaultFrameLipOrder;
            frameOrder = DefaultFrameOrder;
            frameShineOrder = DefaultFrameShineOrder;
            gateArrowOutlineOrder = DefaultGateArrowOutlineOrder;
            gateMarkOrder = DefaultGateMarkOrder;
            gateIceOrder = DefaultGateIceOrder;
            blockContourOrder = DefaultBlockContourOrder;
            blockLipOrder = DefaultBlockLipOrder;
            blockOrder = DefaultBlockOrder;
            layerEdgeOrder = DefaultLayerEdgeOrder;
            layerOrder = DefaultLayerOrder;
            layerShineOrder = DefaultLayerShineOrder;
            layerStudOrder = DefaultLayerStudOrder;
            layerGlossOrder = DefaultLayerGlossOrder;
            blockShineOrder = DefaultBlockShineOrder;
            studOrder = DefaultStudOrder;
            glossOrder = DefaultGlossOrder;
            iceOrder = DefaultIceOrder;
            iceStreakOrder = DefaultIceStreakOrder;
            chainOrder = DefaultChainOrder;
            iconOrder = DefaultIconOrder;
            keyGemOrder = DefaultKeyGemOrder;
            keyGemHighlightOrder = DefaultKeyGemHighlightOrder;
            shutterOrder = DefaultShutterOrder;
            shutterBorderOrder = DefaultShutterBorderOrder;
            badgeOrder = DefaultBadgeOrder;
            badgeRimOrder = DefaultBadgeRimOrder;
            badgeHighlightOrder = DefaultBadgeHighlightOrder;
            labelOrder = DefaultLabelOrder;
            outlineOrder = DefaultOutlineOrder;
            liftedBlockOrder = DefaultLiftedBlockOrder;
            effectOrder = DefaultEffectOrder;
            pullGlowOrder = DefaultPullGlowOrder;
            generatorPreviewRimOrder = DefaultGeneratorPreviewRimOrder;
            generatorPreviewOrder = DefaultGeneratorPreviewOrder;
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

        /// <summary>
        /// An introduction card sprite drawn 9-sliced: reported when it is
        /// not assigned, naming <paramref name="expectedPath"/>, or when it
        /// has no border to slice by.
        /// </summary>
        private void AddIfUnslicedIntroSprite(List<string> problems, Sprite sprite, string field, string expectedPath)
        {
            if (sprite == null)
            {
                problems.Add($"{name}: {field} is not assigned; assign {expectedPath}.");
            }
            else if (sprite.border == Vector4.zero)
            {
                problems.Add($"{name}: {field} is drawn 9-sliced, so it needs a border (Sprite Editor: Border); it has none.");
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

        /// <summary>One joker's New Joker card: its title and its sentence.</summary>
        [Serializable]
        private sealed class JokerIntroduction
        {
            [SerializeField] private JokerKind joker;
            [SerializeField] private string title;
            [SerializeField, TextArea] private string text;

            public JokerIntroduction(JokerKind joker, string title, string text)
            {
                this.joker = joker;
                this.title = title;
                this.text = text;
            }

            public JokerKind Joker => joker;

            public string Title => title;

            public string Text => text;
        }
    }
}
