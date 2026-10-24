using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// The finish of a lit mechanic part (Module 46, pass 2): how sharp and how
    /// strong the white highlight the light leaves on it is. Its shade is the
    /// lit body's (<see cref="ArtRecipe.Ambient"/>, <see cref="ArtRecipe.Diffuse"/>).
    /// </summary>
    [Serializable]
    public struct LitFinish
    {
        [Tooltip("The highlight's sharpness: the Blinn-Phong exponent. At least 1.")]
        [SerializeField] private float shininess;

        [Tooltip("The highlight's strength: white added where the surface mirrors the light toward the viewer, from 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float specular;

        /// <summary>A finish.</summary>
        public LitFinish(float shininess, float specular)
        {
            this.shininess = shininess;
            this.specular = specular;
        }

        /// <summary>The Blinn-Phong exponent.</summary>
        public float Shininess => shininess;

        /// <summary>The white the highlight adds at its strongest.</summary>
        public float Specular => specular;
    }

    /// <summary>
    /// A soft shadow a raised part casts on what lies under it (Module 46,
    /// pass 2): its own mask blurred and moved down and to the right, darkening
    /// by its strength.
    /// </summary>
    [Serializable]
    public struct SoftShadow
    {
        [Tooltip("How far the shadow falls, in whole pixels, x right and y up: down and to the right, away from the light.")]
        [SerializeField] private Vector2 offsetPixels;

        [Tooltip("The blur's standard deviation, in pixels. Above 0.")]
        [SerializeField] private float blurPixels;

        [Tooltip("How much the shadow darkens what lies under it, at its strongest, from 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float strength;

        /// <summary>A shadow.</summary>
        public SoftShadow(Vector2 offsetPixels, float blurPixels, float strength)
        {
            this.offsetPixels = offsetPixels;
            this.blurPixels = blurPixels;
            this.strength = strength;
        }

        /// <summary>How far the shadow falls, in pixels, x right and y up.</summary>
        public Vector2 OffsetPixels => offsetPixels;

        /// <summary>The blur's standard deviation, in pixels.</summary>
        public float BlurPixels => blurPixels;

        /// <summary>How much it darkens at its strongest.</summary>
        public float Strength => strength;

        /// <summary>
        /// The blur's reach, in whole pixels: <see cref="ArtRecipe.BlurReachSigmas"/>
        /// standard deviations, rounded up. Nothing of the blur lies beyond it.
        /// </summary>
        public int BlurReachPixels => (int)Math.Ceiling(ArtRecipe.BlurReachSigmas * blurPixels);

        /// <summary>
        /// How far past its part the shadow can reach on any side, in whole
        /// pixels: its blur's reach plus its larger offset. A canvas widened by
        /// this on every side holds the whole shadow.
        /// </summary>
        public int ReachPixels =>
            BlurReachPixels + (int)Math.Ceiling(Math.Max(Math.Abs(offsetPixels.x), Math.Abs(offsetPixels.y)));
    }

    /// <summary>
    /// A raised, rounded part's profile (Module 46, pass 2): its corner, and
    /// the quarter circle its edge rises along to its height.
    /// </summary>
    [Serializable]
    public struct RaisedShape
    {
        [Tooltip("Corner radius, in pixels. At least 0.")]
        [SerializeField] private float cornerPixels;

        [Tooltip("How far in from the edge the part rises, in pixels. Above 0.")]
        [SerializeField] private float bevelPixels;

        [Tooltip("How high it stands, in pixels. Above 0.")]
        [SerializeField] private float heightPixels;

        /// <summary>A profile.</summary>
        public RaisedShape(float cornerPixels, float bevelPixels, float heightPixels)
        {
            this.cornerPixels = cornerPixels;
            this.bevelPixels = bevelPixels;
            this.heightPixels = heightPixels;
        }

        /// <summary>Corner radius, in pixels.</summary>
        public float CornerPixels => cornerPixels;

        /// <summary>How far in from the edge it rises, in pixels.</summary>
        public float BevelPixels => bevelPixels;

        /// <summary>How high it stands, in pixels.</summary>
        public float HeightPixels => heightPixels;
    }

    /// <summary>
    /// The state visuals of Module 46's second pass, as the reference script
    /// <c>mechanics_reference.py</c> draws them: every number below is the
    /// script's, named, so the look is tuned here (D46). Positions are in
    /// pixels of a 128-pixel cell, x right and y up, and every mechanic is
    /// lit like the blocks, from the top left, without outlines.
    /// </summary>
    public sealed partial class ArtRecipe
    {
        /// <summary>
        /// How many standard deviations a blur reaches before it is cut off.
        /// Not a look parameter: past three a Gaussian adds less than a byte's
        /// worth, and a fixed reach keeps a stretched sprite's middle uniform.
        /// </summary>
        public const float BlurReachSigmas = 3f;

        [Header("Mechanics: shared (Module 46, pass 2)")]
        [Tooltip("The shadow a shutter's rim, an elevator's rim and a generator's machine cast on the floor.")]
        [SerializeField] private SoftShadow raisedShadow = new SoftShadow(new Vector2(3f, -5f), 6f, 0.5f);

        [Header("Count badge (9-sliced along its width)")]
        [Tooltip("The badge's width and height for one digit, in pixels.")]
        [SerializeField] private Vector2 badgeSizePixels = new Vector2(56f, 54f);

        [Tooltip("How much wider each further digit makes the badge, in pixels.")]
        [SerializeField] private float badgeDigitWidthPixels = 10f;

        [Tooltip("The raised gold rim: its corner, bevel and height.")]
        [SerializeField] private RaisedShape badgeRim = new RaisedShape(15f, 8f, 9f);

        [SerializeField] private LitFinish badgeRimFinish = new LitFinish(40f, 0.45f);

        [Tooltip("How far the red face sits inside the rim's edge, in pixels.")]
        [SerializeField] private float badgeFaceInsetPixels = 7f;

        [SerializeField] private RaisedShape badgeFace = new RaisedShape(9f, 6f, 6f);
        [SerializeField] private LitFinish badgeFaceFinish = new LitFinish(30f, 0.3f);
        [SerializeField] private SoftShadow badgeShadow = new SoftShadow(new Vector2(2f, -4f), 3f, 0.45f);

        [Header("Ice (frozen blocks and closed gates; a placed quarter set)")]
        [Tooltip("How far the ice's edge sits inside the cell's edge, in pixels: beyond half the gap, so it lies on the block, and below Corner Radius Pixels.")]
        [SerializeField] private float iceInsetPixels = 5f;

        [Tooltip("How far in from its edge the ice rises, in pixels: above 0 and at most its corner, Corner Radius Pixels less how much further in it sits than the block.")]
        [SerializeField] private float iceBevelPixels = 14f;

        [SerializeField] private float iceHeightPixels = 10f;
        [SerializeField] private LitFinish iceFinish = new LitFinish(60f, 0.7f);

        [Tooltip("How much the ice's tint covers the block under it: thin, so the block reads through.")]
        [SerializeField, Range(0f, 1f)] private float iceTintAlpha = 0.32f;

        [Tooltip("How far inside the ice's edge its bright rim runs, in pixels.")]
        [SerializeField] private float iceRimDepthPixels = 2f;

        [Tooltip("Half the width of the bright rim, in pixels: it fades out this far either side of its line. Above 0.")]
        [SerializeField] private float iceRimWidthPixels = 2.5f;

        [SerializeField, Range(0f, 1f)] private float iceRimAlpha = 0.5f;

        [Header("Frost streaks (one per frozen block, at its top row's left cell)")]
        [Tooltip("Where each diagonal streak crosses, as x + y in pixels from the anchor cell's top-left corner, y down. At least one.")]
        [SerializeField] private float[] iceStreakOffsetsPixels = { 95f, 140f, 175f };

        [Tooltip("The first streak's width, in pixels: the width of its Gaussian. Above 0.")]
        [SerializeField] private float iceStreakWidthPixels = 3f;

        [Tooltip("How much wider each further streak is, in pixels.")]
        [SerializeField] private float iceStreakWidthStepPixels = 1f;

        [SerializeField, Range(0f, 1f)] private float iceStreakAlpha = 0.45f;

        [Tooltip("The streaks fade in from the cell's left edge between these, in pixels.")]
        [SerializeField] private Vector2 iceStreakLeftFadePixels = new Vector2(25f, 60f);

        [Tooltip("Without a cell to its right, the streaks fade out between these, in pixels in from the block's right edge: from the second to the first.")]
        [SerializeField] private Vector2 iceStreakRightFadePixels = new Vector2(21f, 61f);

        [Tooltip("The streaks fade in from the cell's top edge between these, in pixels.")]
        [SerializeField] private Vector2 iceStreakTopFadePixels = new Vector2(20f, 50f);

        [Tooltip("The streaks fade out over this many pixels above the anchor cell's bottom edge, so a block two cells tall shows no cut there (not the script's: it draws the streaks over the whole block). Above 0 and below a cell.")]
        [SerializeField] private float iceStreakBottomFadePixels = 24f;

        [Header("Chain (tube links; one period of a ring and a side-on link, tiled)")]
        [Tooltip("From one link to the next, in whole pixels: a period holds two.")]
        [SerializeField] private float chainSpacingPixels = 31f;

        [Tooltip("The flat ring's radii, across and up, in pixels, to its tube's middle.")]
        [SerializeField] private Vector2 chainRingRadiiPixels = new Vector2(17f, 11f);

        [SerializeField] private float chainRingTubePixels = 5f;
        [SerializeField] private float chainRingHeightPixels = 6f;

        [Tooltip("The side-on link's radii, across and up, in pixels.")]
        [SerializeField] private Vector2 chainLinkRadiiPixels = new Vector2(15f, 3.5f);

        [SerializeField] private float chainLinkTubePixels = 3.5f;
        [SerializeField] private float chainLinkHeightPixels = 5f;
        [SerializeField] private LitFinish chainFinish = new LitFinish(45f, 0.6f);
        [SerializeField] private SoftShadow chainShadow = new SoftShadow(new Vector2(2f, -4f), 2.5f, 0.5f);

        [Header("Padlock on a locked block (silver shackle, gold body)")]
        [Tooltip("Half the body's width and height, in pixels.")]
        [SerializeField] private Vector2 lockBodyHalfSizePixels = new Vector2(30f, 26f);

        [SerializeField] private RaisedShape lockBody = new RaisedShape(11f, 11f, 13f);
        [SerializeField] private LitFinish lockFinish = new LitFinish(40f, 0.5f);
        [SerializeField] private SoftShadow lockShadow = new SoftShadow(new Vector2(3f, -5f), 4f, 0.5f);

        [Tooltip("How far below the mark's centre the body's centre sits, in whole pixels.")]
        [SerializeField] private float lockDropPixels = 8f;

        [Tooltip("The shackle's radii, across and up, in pixels, to its tube's middle.")]
        [SerializeField] private Vector2 lockShackleRadiiPixels = new Vector2(16f, 18f);

        [SerializeField] private float lockShackleTubePixels = 5f;
        [SerializeField] private float lockShackleHeightPixels = 7f;

        [Tooltip("How far above the body's centre the shackle's centre sits, in pixels.")]
        [SerializeField] private float lockShackleRisePixels = 22f;

        [Tooltip("The shackle shows only this far and more above the body's centre, in pixels: its legs run behind the body.")]
        [SerializeField] private float lockShackleCutPixels = 6f;

        [SerializeField] private LitFinish lockShackleFinish = new LitFinish(45f, 0.6f);
        [SerializeField] private SoftShadow lockShackleShadow = new SoftShadow(new Vector2(2f, -4f), 2.5f, 0.45f);
        [SerializeField] private float keyholeRadiusPixels = 5f;

        [Tooltip("How far above the body's centre the keyhole's round top sits, in pixels.")]
        [SerializeField] private float keyholeRisePixels = 8f;

        [SerializeField] private float keyholeSlotHalfWidthPixels = 2.5f;

        [Tooltip("How far below the body's centre the keyhole's slot ends, in pixels.")]
        [SerializeField] private float keyholeSlotEndPixels = 2f;

        [Tooltip("How deep the keyhole is recessed, in pixels.")]
        [SerializeField] private float keyholeDepthPixels = 6f;

        [Tooltip("How soft the recess's edge is: a blur, in pixels. Above 0.")]
        [SerializeField] private float keyholeBlurPixels = 1.2f;

        [Tooltip("How much of the keyhole's dark colour covers its recess.")]
        [SerializeField, Range(0f, 1f)] private float keyholeAlpha = 0.9f;

        [Tooltip("How far below the body's centre the keys still needed are written, in pixels.")]
        [SerializeField] private float lockCountDropPixels = 12f;

        [Header("Key on a block (gold, with its lock's gem; 1.2 times the script's shaft and bow)")]
        [SerializeField] private float goldKeyBowRadiusPixels = 24f;

        [Tooltip("How far left of the mark's centre the bow's centre sits, in pixels.")]
        [SerializeField] private float goldKeyBowOffsetPixels = 22f;

        [SerializeField] private float goldKeyShaftHalfPixels = 7.2f;

        [Tooltip("How far right of the mark's centre the shaft ends, in pixels.")]
        [SerializeField] private float goldKeyShaftEndPixels = 34f;

        [Tooltip("The first tooth: from x, to x, and how far below the key's middle it reaches, in pixels from the mark's centre.")]
        [SerializeField] private Vector3 goldKeyFirstToothPixels = new Vector3(18f, 26f, 16f);

        [Tooltip("The second tooth, as the first.")]
        [SerializeField] private Vector3 goldKeySecondToothPixels = new Vector3(28f, 34f, 12f);

        [SerializeField] private float goldKeyHeightPixels = 8f;

        [Tooltip("How round the key's edges rise: a blur of its shape, in pixels. Above 0.")]
        [SerializeField] private float goldKeyRoundingPixels = 3f;

        [Tooltip("How soft its outer edge is: a blur, in pixels. Above 0.")]
        [SerializeField] private float goldKeyEdgePixels = 0.8f;

        [SerializeField] private LitFinish goldKeyFinish = new LitFinish(40f, 0.6f);
        [SerializeField] private SoftShadow goldKeyShadow = new SoftShadow(new Vector2(2f, -4f), 3f, 0.5f);

        [Tooltip("The radius of the dark ring the gem sits in, in pixels.")]
        [SerializeField] private float gemSeatRadiusPixels = 12f;

        [Tooltip("Half the ring's width, in pixels. Above 0.")]
        [SerializeField] private float gemSeatWidthPixels = 2f;

        [SerializeField, Range(0f, 1f)] private float gemSeatDarken = 0.35f;
        [SerializeField] private float gemRadiusPixels = 11f;
        [SerializeField] private float gemHeightPixels = 7f;
        [SerializeField] private LitFinish gemFinish = new LitFinish(60f, 0.8f);

        [Header("Time-bonus coin (green rim, cream face, dark green hands: time_bonus_reference.py, option D)")]
        [SerializeField] private float coinRadiusPixels = 34f;

        [Tooltip("How far in from its edge the coin rises, in pixels. Above 0.")]
        [SerializeField] private float coinRimPixels = 7f;

        [SerializeField] private float coinHeightPixels = 8f;

        [Tooltip("The face's radius, in pixels: the coin's less 8.")]
        [SerializeField] private float coinFaceRadiusPixels = 26f;

        [Tooltip("How deep the face is set into the coin, in pixels.")]
        [SerializeField] private float coinFaceDepthPixels = 2f;

        [Tooltip("How far past the face's recess its cream reaches, in pixels.")]
        [SerializeField] private float coinFaceEdgePixels = 0.5f;

        [SerializeField] private LitFinish coinFinish = new LitFinish(40f, 0.5f);
        [SerializeField] private SoftShadow coinShadow = new SoftShadow(new Vector2(2f, -4f), 3f, 0.5f);
        [Tooltip("The coin radius the hands' widths and lengths are given for, in pixels: the hands scale with Coin Radius Pixels over it, as the script's do with R / 24. Above 0.")]
        [SerializeField] private float coinHandDesignRadiusPixels = 24f;

        [Tooltip("Half a hand's width on a coin of Coin Hand Design Radius Pixels, in pixels.")]
        [SerializeField] private float coinHandHalfWidthPixels = 2.2f;

        [Tooltip("The minute hand's length, up from the centre, on a coin of Coin Hand Design Radius Pixels, in pixels.")]
        [SerializeField] private float coinMinuteHandPixels = 12f;

        [Tooltip("The hour hand's length, right from the centre, on a coin of Coin Hand Design Radius Pixels, in pixels.")]
        [SerializeField] private float coinHourHandPixels = 9f;

        [Tooltip("How far the hour hand starts left of the centre, in pixels; not scaled, as in the script.")]
        [SerializeField] private float coinHourHandTailPixels = 1f;

        [Header("One-way arrow (raised, in the block's colour; 9-sliced along its length)")]
        [Tooltip("The block length the arrow is painted for, in cells; it stretches to any other.")]
        [SerializeField] private int oneWayPaintedCells = 2;

        [Tooltip("Half the shaft's length, in pixels from the block's centre.")]
        [SerializeField] private float oneWayShaftHalfLengthPixels = 70f;

        [SerializeField] private float oneWayShaftHalfThicknessPixels = 10f;

        [Tooltip("Where each head starts, in pixels from the block's centre.")]
        [SerializeField] private float oneWayHeadStartPixels = 48f;

        [Tooltip("Where each tip is, in pixels from the block's centre.")]
        [SerializeField] private float oneWayTipPixels = 94f;

        [Tooltip("A head's half thickness per pixel back from its tip.")]
        [SerializeField] private float oneWayHeadSlope = 0.95f;

        [Tooltip("How high the arrow stands on the block, in pixels.")]
        [SerializeField] private float oneWayRaisePixels = 14f;

        [Tooltip("How round its edges rise: a blur of its shape, in pixels. Above 0.")]
        [SerializeField] private float oneWayRoundingPixels = 2f;

        [Tooltip("How much lighter than the block it is: a share of the way to white.")]
        [SerializeField, Range(0f, 1f)] private float oneWayLighten = 0.06f;

        [Tooltip("Its thin edge's colour as a share of the block's.")]
        [SerializeField, Range(0f, 1f)] private float oneWayEdgeTone = 0.45f;

        [Tooltip("How far past the arrow the edge reaches: the size of a max filter, in whole odd pixels.")]
        [SerializeField] private int oneWayEdgeGrowPixels = 3;

        [SerializeField, Range(0f, 1f)] private float oneWayEdgeAlpha = 0.85f;

        [Tooltip("How quickly the arrow's soft edge becomes opaque.")]
        [SerializeField] private float oneWayAlphaGain = 1.5f;

        [SerializeField] private LitFinish oneWayFinish = new LitFinish(30f, 0.3f);
        [SerializeField] private SoftShadow oneWayShadow = new SoftShadow(new Vector2(2f, -4f), 3f, 0.5f);

        [Header("Shutter (raised gold rim, cylinder slats; 9-sliced and tiled)")]
        [Tooltip("The region the rim is painted for, in cells on each side; it stretches to any other.")]
        [SerializeField] private int shutterPaintedCells = 2;

        [Tooltip("How far the rim's edge sits inside the region, in pixels.")]
        [SerializeField] private float shutterRimInsetPixels = 4f;

        [SerializeField] private RaisedShape shutterRim = new RaisedShape(20f, 14f, 13f);
        [SerializeField] private LitFinish shutterRimFinish = new LitFinish(40f, 0.45f);

        [Tooltip("How far inside the region the slats start, in pixels: the rim's inner edge.")]
        [SerializeField] private float shutterInnerInsetPixels = 22f;

        [SerializeField] private float shutterInnerCornerPixels = 8f;

        [Tooltip("The height of one slat, in whole pixels: the tile's.")]
        [SerializeField] private int shutterCylinderPitchPixels = 24;

        [SerializeField] private float shutterCylinderHeightPixels = 6f;
        [SerializeField] private LitFinish shutterCylinderFinish = new LitFinish(30f, 0.25f);

        [Tooltip("How much the gap between two slats darkens.")]
        [SerializeField, Range(0f, 1f)] private float shutterGapDarken = 0.35f;

        [Tooltip("How narrow the gap is: the higher, the narrower. Above 0.")]
        [SerializeField] private float shutterGapSharpness = 3f;

        [Tooltip("How much the slats darken right under the rim, as a recess.")]
        [SerializeField, Range(0f, 1f)] private float shutterRecessDarken = 0.4f;

        [Tooltip("How far under the rim the recess reaches, in pixels. Above 0.")]
        [SerializeField] private float shutterRecessPixels = 10f;

        [Header("Generator machine (metal body and glass screen 9-sliced; vent and button whole)")]
        [Tooltip("How far the body's edge sits inside the machine's rectangle, in pixels.")]
        [SerializeField] private float machineInsetPixels = 2f;

        [SerializeField] private RaisedShape machineBody = new RaisedShape(22f, 16f, 14f);
        [SerializeField] private LitFinish machineFinish = new LitFinish(40f, 0.4f);

        [Tooltip("How far in from the body's edge each corner rivet's centre sits, in pixels.")]
        [SerializeField] private float machineRivetInsetPixels = 16f;

        [SerializeField] private float machineRivetRadiusPixels = 5f;
        [SerializeField] private float machineRivetHeightPixels = 4f;
        [SerializeField] private LitFinish machineRivetFinish = new LitFinish(50f, 0.6f);

        [Tooltip("The screen the sprite is painted for, in whole pixels; it stretches to any other.")]
        [SerializeField] private Vector2 machineScreenSizePixels = new Vector2(68f, 56f);

        [SerializeField] private float machineScreenCornerPixels = 10f;

        [Tooltip("How deep the screen is set into the body, in pixels.")]
        [SerializeField] private float machineScreenDepthPixels = 7f;

        [Tooltip("How wide the slope down to the screen is, in pixels. Above 0.")]
        [SerializeField] private float machineScreenSlopePixels = 4f;

        [Tooltip("The glass starts this far inside the screen's edge and is whole this far in, in pixels.")]
        [SerializeField] private Vector2 machineGlassFadePixels = new Vector2(1f, 4f);

        [Tooltip("The glass's shade at its darkest, as a share of its colour.")]
        [SerializeField] private float machineGlassBaseShade = 0.7f;

        [Tooltip("How much lighter the glass grows downward, as a share of its colour.")]
        [SerializeField] private float machineGlassShadeGain = 0.5f;

        [Tooltip("The glass lightens from the first to the second, in pixels below the screen's centre (negative is above).")]
        [SerializeField] private Vector2 machineGlassGradientPixels = new Vector2(-30f, 10f);

        [Tooltip("How much the glass darkens at its edge, under the slope.")]
        [SerializeField, Range(0f, 1f)] private float machineScreenShadowDarken = 0.5f;

        [Tooltip("How far into the glass that shadow reaches, in pixels. Above 0.")]
        [SerializeField] private float machineScreenShadowPixels = 6f;

        [Tooltip("The width of the glare's Gaussian, in pixels. Above 0.")]
        [SerializeField] private float machineGlareWidthPixels = 7f;

        [Tooltip("How far below the screen's top the glare meets its left edge, in pixels: it runs down to the right at 45 degrees.")]
        [SerializeField] private float machineGlareDropPixels = 14f;

        [SerializeField, Range(0f, 1f)] private float machineGlareAlpha = 0.18f;

        [Tooltip("The glare starts this far inside the screen's edge and is whole this far in, in pixels.")]
        [SerializeField] private Vector2 machineGlareInsetPixels = new Vector2(2f, 8f);

        [Tooltip("The glare fades out between these, in pixels from the screen's left edge, so it stays in the screen's 9-slice corner, which must fit half the screen's height (not the script's: its glare crosses the screen).")]
        [SerializeField] private Vector2 machineGlareFadePixels = new Vector2(16f, 27f);

        [Tooltip("Half the button's width and height, in pixels.")]
        [SerializeField] private Vector2 machineButtonHalfSizePixels = new Vector2(9f, 7f);

        [SerializeField] private RaisedShape machineButton = new RaisedShape(4f, 4f, 5f);
        [SerializeField] private LitFinish machineButtonFinish = new LitFinish(50f, 0.6f);
        [SerializeField] private SoftShadow machineButtonShadow = new SoftShadow(new Vector2(1f, -2f), 1.5f, 0.4f);

        [Tooltip("Half a vent slot's width and height, in pixels.")]
        [SerializeField] private Vector2 machineVentSlotHalfSizePixels = new Vector2(15f, 2f);

        [SerializeField] private float machineVentSlotCornerPixels = 2f;

        [Tooltip("How many slots the vent has. At least 1.")]
        [SerializeField] private int machineVentSlots = 3;

        [Tooltip("From one slot to the next, in pixels.")]
        [SerializeField] private float machineVentPitchPixels = 8f;

        [SerializeField, Range(0f, 1f)] private float machineVentDarken = 0.45f;

        [Header("Elevator (orange rim with hazard strips, plastic doors)")]
        [Tooltip("The region the rim and doors are painted for, in cells on each side; they stretch to any other.")]
        [SerializeField] private int elevatorPaintedCells = 2;

        [SerializeField] private float elevatorRimInsetPixels = 4f;
        [SerializeField] private RaisedShape elevatorRim = new RaisedShape(20f, 14f, 13f);
        [SerializeField] private LitFinish elevatorRimFinish = new LitFinish(40f, 0.45f);

        [Tooltip("How far inside the region the dark recess starts, in pixels: across at the sides, and up at the top and bottom.")]
        [SerializeField] private Vector2 elevatorInnerInsetPixels = new Vector2(22f, 16f);

        [SerializeField] private float elevatorInnerCornerPixels = 8f;

        [Tooltip("How far in from the region's side each hazard strip's middle runs, in pixels.")]
        [SerializeField] private float elevatorHazardCentrePixels = 13f;

        [Tooltip("Half a hazard strip's width, in whole pixels.")]
        [SerializeField] private int elevatorHazardHalfWidthPixels = 6;

        [Tooltip("How far short of the region's top and bottom a hazard strip ends, in pixels.")]
        [SerializeField] private float elevatorHazardEndPixels = 24f;

        [Tooltip("One stripe's height, in whole pixels; a tile holds two.")]
        [SerializeField] private int elevatorHazardStripePixels = 12;

        [Tooltip("The gap around each door, to the recess's edge and to the other door, in pixels.")]
        [SerializeField] private float elevatorDoorGapPixels = 3f;

        [SerializeField] private RaisedShape elevatorDoor = new RaisedShape(6f, 10f, 10f);
        [SerializeField] private LitFinish elevatorDoorFinish = new LitFinish(35f, 0.35f);

        [Tooltip("How far inside a door its inset panel's edge sits, in pixels, across and up.")]
        [SerializeField] private Vector2 elevatorDoorPanelInsetPixels = new Vector2(15f, 21f);

        [SerializeField] private float elevatorDoorPanelCornerPixels = 5f;

        [Tooltip("How deep the panel is set into the door, in pixels.")]
        [SerializeField] private float elevatorDoorPanelDepthPixels = 4f;

        [Tooltip("How wide the slope down to the panel is, in pixels. Above 0.")]
        [SerializeField] private float elevatorDoorPanelSlopePixels = 4f;

        [SerializeField] private SoftShadow elevatorDoorShadow = new SoftShadow(new Vector2(2f, -3f), 3f, 0.5f);

        [Tooltip("Half a handle's width and height, in pixels.")]
        [SerializeField] private Vector2 elevatorHandleHalfSizePixels = new Vector2(3.5f, 16f);

        [SerializeField] private RaisedShape elevatorHandle = new RaisedShape(3.5f, 3f, 4f);
        [SerializeField] private LitFinish elevatorHandleFinish = new LitFinish(50f, 0.7f);
        [SerializeField] private SoftShadow elevatorHandleShadow = new SoftShadow(new Vector2(1f, -2f), 1.5f, 0.4f);

        [Tooltip("How far either side of the doors' seam each handle's centre sits, in pixels.")]
        [SerializeField] private float elevatorHandleOffsetPixels = 13f;

        /// <summary>The shadow a shutter's rim, an elevator's rim and a machine cast on the floor.</summary>
        public SoftShadow RaisedShadow => raisedShadow;

        /// <summary>The badge's size for one digit, in pixels.</summary>
        public Vector2 BadgeSizePixels => badgeSizePixels;

        /// <summary>How much wider each further digit makes the badge, in pixels.</summary>
        public float BadgeDigitWidthPixels => badgeDigitWidthPixels;

        /// <summary>The badge's rim.</summary>
        public RaisedShape BadgeRim => badgeRim;

        /// <summary>The rim's finish.</summary>
        public LitFinish BadgeRimFinish => badgeRimFinish;

        /// <summary>How far the face sits inside the rim's edge, in pixels.</summary>
        public float BadgeFaceInsetPixels => badgeFaceInsetPixels;

        /// <summary>The badge's face.</summary>
        public RaisedShape BadgeFace => badgeFace;

        /// <summary>The face's finish.</summary>
        public LitFinish BadgeFaceFinish => badgeFaceFinish;

        /// <summary>The badge's shadow.</summary>
        public SoftShadow BadgeShadow => badgeShadow;

        /// <summary>How far the ice's edge sits inside the cell's edge, in pixels.</summary>
        public float IceInsetPixels => iceInsetPixels;

        /// <summary>How far in from its edge the ice rises, in pixels.</summary>
        public float IceBevelPixels => iceBevelPixels;

        /// <summary>How high the ice stands, in pixels.</summary>
        public float IceHeightPixels => iceHeightPixels;

        /// <summary>The ice's finish.</summary>
        public LitFinish IceFinish => iceFinish;

        /// <summary>How much the ice's tint covers the block.</summary>
        public float IceTintAlpha => iceTintAlpha;

        /// <summary>How far inside the ice's edge its rim runs, in pixels.</summary>
        public float IceRimDepthPixels => iceRimDepthPixels;

        /// <summary>Half the rim's width, in pixels.</summary>
        public float IceRimWidthPixels => iceRimWidthPixels;

        /// <summary>The rim's opacity at its line.</summary>
        public float IceRimAlpha => iceRimAlpha;

        /// <summary>Where each streak crosses, as x + y in pixels from the anchor cell's top-left corner, y down.</summary>
        public IReadOnlyList<float> IceStreakOffsetsPixels => iceStreakOffsetsPixels;

        /// <summary>The first streak's width, in pixels.</summary>
        public float IceStreakWidthPixels => iceStreakWidthPixels;

        /// <summary>How much wider each further streak is, in pixels.</summary>
        public float IceStreakWidthStepPixels => iceStreakWidthStepPixels;

        /// <summary>A streak's opacity at its line.</summary>
        public float IceStreakAlpha => iceStreakAlpha;

        /// <summary>The fade in from the left edge, in pixels.</summary>
        public Vector2 IceStreakLeftFadePixels => iceStreakLeftFadePixels;

        /// <summary>The fade out toward a lone cell's right edge, in pixels in from it.</summary>
        public Vector2 IceStreakRightFadePixels => iceStreakRightFadePixels;

        /// <summary>The fade in from the top edge, in pixels.</summary>
        public Vector2 IceStreakTopFadePixels => iceStreakTopFadePixels;

        /// <summary>The fade out above the anchor cell's bottom edge, in pixels.</summary>
        public float IceStreakBottomFadePixels => iceStreakBottomFadePixels;

        /// <summary>From one link to the next, in pixels.</summary>
        public float ChainSpacingPixels => chainSpacingPixels;

        /// <summary>The flat ring's radii, in pixels.</summary>
        public Vector2 ChainRingRadiiPixels => chainRingRadiiPixels;

        /// <summary>The flat ring's tube half width, in pixels.</summary>
        public float ChainRingTubePixels => chainRingTubePixels;

        /// <summary>The flat ring's height, in pixels.</summary>
        public float ChainRingHeightPixels => chainRingHeightPixels;

        /// <summary>The side-on link's radii, in pixels.</summary>
        public Vector2 ChainLinkRadiiPixels => chainLinkRadiiPixels;

        /// <summary>The side-on link's tube half width, in pixels.</summary>
        public float ChainLinkTubePixels => chainLinkTubePixels;

        /// <summary>The side-on link's height, in pixels.</summary>
        public float ChainLinkHeightPixels => chainLinkHeightPixels;

        /// <summary>The chain's finish.</summary>
        public LitFinish ChainFinish => chainFinish;

        /// <summary>The chain's shadow.</summary>
        public SoftShadow ChainShadow => chainShadow;

        /// <summary>Half the padlock body's width and height, in pixels.</summary>
        public Vector2 LockBodyHalfSizePixels => lockBodyHalfSizePixels;

        /// <summary>The padlock body's profile.</summary>
        public RaisedShape LockBody => lockBody;

        /// <summary>The padlock body's finish.</summary>
        public LitFinish LockFinish => lockFinish;

        /// <summary>The padlock body's shadow.</summary>
        public SoftShadow LockShadow => lockShadow;

        /// <summary>How far below the mark's centre the body's centre sits, in pixels.</summary>
        public float LockDropPixels => lockDropPixels;

        /// <summary>The shackle's radii, in pixels.</summary>
        public Vector2 LockShackleRadiiPixels => lockShackleRadiiPixels;

        /// <summary>The shackle's tube half width, in pixels.</summary>
        public float LockShackleTubePixels => lockShackleTubePixels;

        /// <summary>The shackle's height, in pixels.</summary>
        public float LockShackleHeightPixels => lockShackleHeightPixels;

        /// <summary>How far above the body's centre the shackle's centre sits, in pixels.</summary>
        public float LockShackleRisePixels => lockShackleRisePixels;

        /// <summary>The shackle shows only this far and more above the body's centre, in pixels.</summary>
        public float LockShackleCutPixels => lockShackleCutPixels;

        /// <summary>The shackle's finish.</summary>
        public LitFinish LockShackleFinish => lockShackleFinish;

        /// <summary>The shackle's shadow.</summary>
        public SoftShadow LockShackleShadow => lockShackleShadow;

        /// <summary>The keyhole's round top's radius, in pixels.</summary>
        public float KeyholeRadiusPixels => keyholeRadiusPixels;

        /// <summary>How far above the body's centre the keyhole's round top sits, in pixels.</summary>
        public float KeyholeRisePixels => keyholeRisePixels;

        /// <summary>Half the keyhole slot's width, in pixels.</summary>
        public float KeyholeSlotHalfWidthPixels => keyholeSlotHalfWidthPixels;

        /// <summary>How far below the body's centre the slot ends, in pixels.</summary>
        public float KeyholeSlotEndPixels => keyholeSlotEndPixels;

        /// <summary>How deep the keyhole is, in pixels.</summary>
        public float KeyholeDepthPixels => keyholeDepthPixels;

        /// <summary>The blur of the keyhole's recess, in pixels.</summary>
        public float KeyholeBlurPixels => keyholeBlurPixels;

        /// <summary>How much of the keyhole's dark colour covers it.</summary>
        public float KeyholeAlpha => keyholeAlpha;

        /// <summary>How far below the body's centre the keys still needed are written, in pixels.</summary>
        public float LockCountDropPixels => lockCountDropPixels;

        /// <summary>The key's bow radius, in pixels.</summary>
        public float GoldKeyBowRadiusPixels => goldKeyBowRadiusPixels;

        /// <summary>How far left of the mark's centre the bow's centre sits, in pixels.</summary>
        public float GoldKeyBowOffsetPixels => goldKeyBowOffsetPixels;

        /// <summary>Half the shaft's thickness, in pixels.</summary>
        public float GoldKeyShaftHalfPixels => goldKeyShaftHalfPixels;

        /// <summary>How far right of the mark's centre the shaft ends, in pixels.</summary>
        public float GoldKeyShaftEndPixels => goldKeyShaftEndPixels;

        /// <summary>The first tooth: from x, to x, depth below the middle, in pixels.</summary>
        public Vector3 GoldKeyFirstToothPixels => goldKeyFirstToothPixels;

        /// <summary>The second tooth, as the first.</summary>
        public Vector3 GoldKeySecondToothPixels => goldKeySecondToothPixels;

        /// <summary>How high the key stands, in pixels.</summary>
        public float GoldKeyHeightPixels => goldKeyHeightPixels;

        /// <summary>The blur its height rises along, in pixels.</summary>
        public float GoldKeyRoundingPixels => goldKeyRoundingPixels;

        /// <summary>The blur of its outer edge, in pixels.</summary>
        public float GoldKeyEdgePixels => goldKeyEdgePixels;

        /// <summary>The key's finish.</summary>
        public LitFinish GoldKeyFinish => goldKeyFinish;

        /// <summary>The key's shadow.</summary>
        public SoftShadow GoldKeyShadow => goldKeyShadow;

        /// <summary>The radius of the gem's seat ring, in pixels.</summary>
        public float GemSeatRadiusPixels => gemSeatRadiusPixels;

        /// <summary>Half the seat ring's width, in pixels.</summary>
        public float GemSeatWidthPixels => gemSeatWidthPixels;

        /// <summary>How much the seat ring darkens.</summary>
        public float GemSeatDarken => gemSeatDarken;

        /// <summary>The gem's radius, in pixels.</summary>
        public float GemRadiusPixels => gemRadiusPixels;

        /// <summary>The gem's height, in pixels.</summary>
        public float GemHeightPixels => gemHeightPixels;

        /// <summary>The gem's finish.</summary>
        public LitFinish GemFinish => gemFinish;

        /// <summary>The coin's radius, in pixels.</summary>
        public float CoinRadiusPixels => coinRadiusPixels;

        /// <summary>How far in from its edge the coin rises, in pixels.</summary>
        public float CoinRimPixels => coinRimPixels;

        /// <summary>How high the coin stands, in pixels.</summary>
        public float CoinHeightPixels => coinHeightPixels;

        /// <summary>The face's radius, in pixels.</summary>
        public float CoinFaceRadiusPixels => coinFaceRadiusPixels;

        /// <summary>How deep the face is set in, in pixels.</summary>
        public float CoinFaceDepthPixels => coinFaceDepthPixels;

        /// <summary>How far past the recess the cream reaches, in pixels.</summary>
        public float CoinFaceEdgePixels => coinFaceEdgePixels;

        /// <summary>The coin's finish.</summary>
        public LitFinish CoinFinish => coinFinish;

        /// <summary>The coin's shadow.</summary>
        public SoftShadow CoinShadow => coinShadow;

        /// <summary>Half a hand's width on this coin, in pixels: scaled from the design radius.</summary>
        public float CoinHandHalfWidthPixels => coinHandHalfWidthPixels * CoinHandScale;

        /// <summary>The minute hand's length on this coin, in pixels: scaled from the design radius.</summary>
        public float CoinMinuteHandPixels => coinMinuteHandPixels * CoinHandScale;

        /// <summary>The hour hand's length on this coin, in pixels: scaled from the design radius.</summary>
        public float CoinHourHandPixels => coinHourHandPixels * CoinHandScale;

        /// <summary>How much larger the hands are than at the design radius: the script's R / 24.</summary>
        private float CoinHandScale => coinRadiusPixels / coinHandDesignRadiusPixels;

        /// <summary>How far the hour hand starts left of the centre, in pixels.</summary>
        public float CoinHourHandTailPixels => coinHourHandTailPixels;

        /// <summary>The block length the arrow is painted for, in cells.</summary>
        public int OneWayPaintedCells => oneWayPaintedCells;

        /// <summary>Half the shaft's length, in pixels.</summary>
        public float OneWayShaftHalfLengthPixels => oneWayShaftHalfLengthPixels;

        /// <summary>Half the shaft's thickness, in pixels.</summary>
        public float OneWayShaftHalfThicknessPixels => oneWayShaftHalfThicknessPixels;

        /// <summary>Where each head starts, in pixels from the centre.</summary>
        public float OneWayHeadStartPixels => oneWayHeadStartPixels;

        /// <summary>Where each tip is, in pixels from the centre.</summary>
        public float OneWayTipPixels => oneWayTipPixels;

        /// <summary>A head's half thickness per pixel back from its tip.</summary>
        public float OneWayHeadSlope => oneWayHeadSlope;

        /// <summary>How high the arrow stands, in pixels.</summary>
        public float OneWayRaisePixels => oneWayRaisePixels;

        /// <summary>The blur its height rises along, in pixels.</summary>
        public float OneWayRoundingPixels => oneWayRoundingPixels;

        /// <summary>How much lighter than the block it is.</summary>
        public float OneWayLighten => oneWayLighten;

        /// <summary>Its edge's colour as a share of the block's.</summary>
        public float OneWayEdgeTone => oneWayEdgeTone;

        /// <summary>The size of the max filter its edge grows by, in pixels.</summary>
        public int OneWayEdgeGrowPixels => oneWayEdgeGrowPixels;

        /// <summary>Its edge's opacity.</summary>
        public float OneWayEdgeAlpha => oneWayEdgeAlpha;

        /// <summary>How quickly its soft edge becomes opaque.</summary>
        public float OneWayAlphaGain => oneWayAlphaGain;

        /// <summary>The arrow's finish.</summary>
        public LitFinish OneWayFinish => oneWayFinish;

        /// <summary>The arrow's shadow.</summary>
        public SoftShadow OneWayShadow => oneWayShadow;

        /// <summary>The region the shutter is painted for, in cells.</summary>
        public int ShutterPaintedCells => shutterPaintedCells;

        /// <summary>How far the rim's edge sits inside the region, in pixels.</summary>
        public float ShutterRimInsetPixels => shutterRimInsetPixels;

        /// <summary>The shutter rim's profile.</summary>
        public RaisedShape ShutterRim => shutterRim;

        /// <summary>The rim's finish.</summary>
        public LitFinish ShutterRimFinish => shutterRimFinish;

        /// <summary>How far inside the region the slats start, in pixels.</summary>
        public float ShutterInnerInsetPixels => shutterInnerInsetPixels;

        /// <summary>The corner of the slats' area, in pixels.</summary>
        public float ShutterInnerCornerPixels => shutterInnerCornerPixels;

        /// <summary>One slat's height, in pixels.</summary>
        public int ShutterCylinderPitchPixels => shutterCylinderPitchPixels;

        /// <summary>How high a slat's cylinder stands, in pixels.</summary>
        public float ShutterCylinderHeightPixels => shutterCylinderHeightPixels;

        /// <summary>The slats' finish.</summary>
        public LitFinish ShutterCylinderFinish => shutterCylinderFinish;

        /// <summary>How much a gap between slats darkens.</summary>
        public float ShutterGapDarken => shutterGapDarken;

        /// <summary>How narrow that gap is.</summary>
        public float ShutterGapSharpness => shutterGapSharpness;

        /// <summary>How much the slats darken under the rim.</summary>
        public float ShutterRecessDarken => shutterRecessDarken;

        /// <summary>How far under the rim that reaches, in pixels.</summary>
        public float ShutterRecessPixels => shutterRecessPixels;

        /// <summary>How far the body's edge sits inside the machine's rectangle, in pixels.</summary>
        public float MachineInsetPixels => machineInsetPixels;

        /// <summary>The machine body's profile.</summary>
        public RaisedShape MachineBody => machineBody;

        /// <summary>The machine's finish.</summary>
        public LitFinish MachineFinish => machineFinish;

        /// <summary>How far in from the body's edge each rivet sits, in pixels.</summary>
        public float MachineRivetInsetPixels => machineRivetInsetPixels;

        /// <summary>A rivet's radius, in pixels.</summary>
        public float MachineRivetRadiusPixels => machineRivetRadiusPixels;

        /// <summary>A rivet's height, in pixels.</summary>
        public float MachineRivetHeightPixels => machineRivetHeightPixels;

        /// <summary>The rivets' finish.</summary>
        public LitFinish MachineRivetFinish => machineRivetFinish;

        /// <summary>The screen painted, in pixels.</summary>
        public Vector2 MachineScreenSizePixels => machineScreenSizePixels;

        /// <summary>The screen's corner radius, in pixels.</summary>
        public float MachineScreenCornerPixels => machineScreenCornerPixels;

        /// <summary>How deep the screen is set in, in pixels.</summary>
        public float MachineScreenDepthPixels => machineScreenDepthPixels;

        /// <summary>How wide the slope to the screen is, in pixels.</summary>
        public float MachineScreenSlopePixels => machineScreenSlopePixels;

        /// <summary>Where the glass starts and where it is whole, in pixels inside the screen's edge.</summary>
        public Vector2 MachineGlassFadePixels => machineGlassFadePixels;

        /// <summary>The glass's darkest shade.</summary>
        public float MachineGlassBaseShade => machineGlassBaseShade;

        /// <summary>How much lighter it grows downward.</summary>
        public float MachineGlassShadeGain => machineGlassShadeGain;

        /// <summary>Where it lightens, in pixels below the screen's centre.</summary>
        public Vector2 MachineGlassGradientPixels => machineGlassGradientPixels;

        /// <summary>How much the glass darkens at its edge.</summary>
        public float MachineScreenShadowDarken => machineScreenShadowDarken;

        /// <summary>How far in that reaches, in pixels.</summary>
        public float MachineScreenShadowPixels => machineScreenShadowPixels;

        /// <summary>The glare's width, in pixels.</summary>
        public float MachineGlareWidthPixels => machineGlareWidthPixels;

        /// <summary>Where the glare meets the screen's left edge, in pixels below its top.</summary>
        public float MachineGlareDropPixels => machineGlareDropPixels;

        /// <summary>The glare's opacity.</summary>
        public float MachineGlareAlpha => machineGlareAlpha;

        /// <summary>Where the glare starts and where it is whole, in pixels inside the screen's edge.</summary>
        public Vector2 MachineGlareInsetPixels => machineGlareInsetPixels;

        /// <summary>Where the glare fades out, in pixels from the screen's left edge.</summary>
        public Vector2 MachineGlareFadePixels => machineGlareFadePixels;

        /// <summary>Half the button's size, in pixels.</summary>
        public Vector2 MachineButtonHalfSizePixels => machineButtonHalfSizePixels;

        /// <summary>The button's profile.</summary>
        public RaisedShape MachineButton => machineButton;

        /// <summary>The button's finish.</summary>
        public LitFinish MachineButtonFinish => machineButtonFinish;

        /// <summary>The button's shadow.</summary>
        public SoftShadow MachineButtonShadow => machineButtonShadow;

        /// <summary>Half a vent slot's size, in pixels.</summary>
        public Vector2 MachineVentSlotHalfSizePixels => machineVentSlotHalfSizePixels;

        /// <summary>A vent slot's corner, in pixels.</summary>
        public float MachineVentSlotCornerPixels => machineVentSlotCornerPixels;

        /// <summary>How many slots the vent has.</summary>
        public int MachineVentSlots => machineVentSlots;

        /// <summary>From one slot to the next, in pixels.</summary>
        public float MachineVentPitchPixels => machineVentPitchPixels;

        /// <summary>How much a slot darkens.</summary>
        public float MachineVentDarken => machineVentDarken;

        /// <summary>The region the elevator is painted for, in cells.</summary>
        public int ElevatorPaintedCells => elevatorPaintedCells;

        /// <summary>How far the rim's edge sits inside the region, in pixels.</summary>
        public float ElevatorRimInsetPixels => elevatorRimInsetPixels;

        /// <summary>The elevator rim's profile.</summary>
        public RaisedShape ElevatorRim => elevatorRim;

        /// <summary>The rim's finish.</summary>
        public LitFinish ElevatorRimFinish => elevatorRimFinish;

        /// <summary>How far inside the region the recess starts, in pixels, across and up.</summary>
        public Vector2 ElevatorInnerInsetPixels => elevatorInnerInsetPixels;

        /// <summary>The recess's corner, in pixels.</summary>
        public float ElevatorInnerCornerPixels => elevatorInnerCornerPixels;

        /// <summary>How far in from the side a hazard strip's middle runs, in pixels.</summary>
        public float ElevatorHazardCentrePixels => elevatorHazardCentrePixels;

        /// <summary>Half a hazard strip's width, in pixels.</summary>
        public int ElevatorHazardHalfWidthPixels => elevatorHazardHalfWidthPixels;

        /// <summary>How far short of the top and bottom a strip ends, in pixels.</summary>
        public float ElevatorHazardEndPixels => elevatorHazardEndPixels;

        /// <summary>One stripe's height, in pixels.</summary>
        public int ElevatorHazardStripePixels => elevatorHazardStripePixels;

        /// <summary>The gap around each door, in pixels.</summary>
        public float ElevatorDoorGapPixels => elevatorDoorGapPixels;

        /// <summary>A door's profile.</summary>
        public RaisedShape ElevatorDoor => elevatorDoor;

        /// <summary>The doors' finish.</summary>
        public LitFinish ElevatorDoorFinish => elevatorDoorFinish;

        /// <summary>How far inside a door its panel sits, in pixels, across and up.</summary>
        public Vector2 ElevatorDoorPanelInsetPixels => elevatorDoorPanelInsetPixels;

        /// <summary>The panel's corner, in pixels.</summary>
        public float ElevatorDoorPanelCornerPixels => elevatorDoorPanelCornerPixels;

        /// <summary>How deep the panel is set in, in pixels.</summary>
        public float ElevatorDoorPanelDepthPixels => elevatorDoorPanelDepthPixels;

        /// <summary>How wide the slope to the panel is, in pixels.</summary>
        public float ElevatorDoorPanelSlopePixels => elevatorDoorPanelSlopePixels;

        /// <summary>A door's shadow.</summary>
        public SoftShadow ElevatorDoorShadow => elevatorDoorShadow;

        /// <summary>Half a handle's size, in pixels.</summary>
        public Vector2 ElevatorHandleHalfSizePixels => elevatorHandleHalfSizePixels;

        /// <summary>A handle's profile.</summary>
        public RaisedShape ElevatorHandle => elevatorHandle;

        /// <summary>The handles' finish.</summary>
        public LitFinish ElevatorHandleFinish => elevatorHandleFinish;

        /// <summary>A handle's shadow.</summary>
        public SoftShadow ElevatorHandleShadow => elevatorHandleShadow;

        /// <summary>How far either side of the seam each handle sits, in pixels.</summary>
        public float ElevatorHandleOffsetPixels => elevatorHandleOffsetPixels;

        /// <summary>
        /// The constraints on the mechanics of Module 46's second pass, each
        /// message naming its field. Sizes must be positive, finishes and
        /// shadows usable, tiles whole pixels, and every 9-sliced or tiled
        /// part must leave something to stretch or repeat; the lit colours
        /// must fit the headroom the lit material multiplies back.
        /// </summary>
        private void AddMechanicProblems(List<string> problems)
        {
            foreach (var (finish, shownName) in new[]
                     {
                         (badgeRimFinish, "Badge Rim Finish"), (badgeFaceFinish, "Badge Face Finish"), (iceFinish, "Ice Finish"),
                         (chainFinish, "Chain Finish"), (lockFinish, "Lock Finish"), (lockShackleFinish, "Lock Shackle Finish"),
                         (goldKeyFinish, "Gold Key Finish"), (gemFinish, "Gem Finish"), (coinFinish, "Coin Finish"),
                         (oneWayFinish, "One Way Finish"), (shutterRimFinish, "Shutter Rim Finish"),
                         (shutterCylinderFinish, "Shutter Cylinder Finish"), (machineFinish, "Machine Finish"),
                         (machineRivetFinish, "Machine Rivet Finish"), (machineButtonFinish, "Machine Button Finish"),
                         (elevatorRimFinish, "Elevator Rim Finish"), (elevatorDoorFinish, "Elevator Door Finish"),
                         (elevatorHandleFinish, "Elevator Handle Finish")
                     })
            {
                if (!(finish.Shininess >= 1f && finish.Specular >= 0f && finish.Specular <= 1f))
                {
                    problems.Add($"{name}: {shownName}'s Shininess must be at least 1 and its Specular from 0 to 1.");
                }
                else if (!(ambient + diffuse + finish.Specular <= shadeHeadroom))
                {
                    problems.Add($"{name}: {shownName}'s Specular with Ambient and Diffuse ({ambient + diffuse + finish.Specular}) must fit Shade Headroom, or it clips.");
                }
            }

            foreach (var (shadow, shownName) in new[]
                     {
                         (raisedShadow, "Raised Shadow"), (badgeShadow, "Badge Shadow"), (chainShadow, "Chain Shadow"),
                         (lockShadow, "Lock Shadow"), (lockShackleShadow, "Lock Shackle Shadow"), (goldKeyShadow, "Gold Key Shadow"),
                         (coinShadow, "Coin Shadow"), (oneWayShadow, "One Way Shadow"), (machineButtonShadow, "Machine Button Shadow"),
                         (elevatorDoorShadow, "Elevator Door Shadow"), (elevatorHandleShadow, "Elevator Handle Shadow")
                     })
            {
                var offset = shadow.OffsetPixels;
                if (!(shadow.BlurPixels > 0f && offset.x == Mathf.Round(offset.x) && offset.y == Mathf.Round(offset.y)))
                {
                    problems.Add($"{name}: {shownName}'s Blur Pixels must be above 0 and its Offset Pixels whole pixels.");
                }
            }

            foreach (var (shape, shownName) in new[]
                     {
                         (badgeRim, "Badge Rim"), (badgeFace, "Badge Face"), (lockBody, "Lock Body"), (shutterRim, "Shutter Rim"),
                         (machineBody, "Machine Body"), (machineButton, "Machine Button"), (elevatorRim, "Elevator Rim"),
                         (elevatorDoor, "Elevator Door"), (elevatorHandle, "Elevator Handle")
                     })
            {
                if (!(shape.CornerPixels >= 0f && shape.BevelPixels > 0f && shape.HeightPixels > 0f))
                {
                    problems.Add($"{name}: {shownName}'s Corner Pixels must be at least 0, and its Bevel Pixels and Height Pixels above 0.");
                }
            }

            AddBadgeAndIceProblems(problems);
            AddMarkArtProblems(problems);
            AddPanelArtProblems(problems);
        }

        /// <summary>The constraints on the count badge and the ice, each message naming its field.</summary>
        private void AddBadgeAndIceProblems(List<string> problems)
        {
            var faceHalf = new Vector2(badgeSizePixels.x, badgeSizePixels.y) / 2f - Vector2.one * badgeFaceInsetPixels;
            if (!(badgeSizePixels.x > 0f && badgeSizePixels.y > 0f && badgeDigitWidthPixels > 0f
                  && badgeDigitWidthPixels < badgeSizePixels.x && badgeFaceInsetPixels >= 0f
                  && faceHalf.x > badgeFace.CornerPixels && faceHalf.y > badgeFace.CornerPixels))
            {
                problems.Add($"{name}: Badge Size Pixels and Badge Digit Width Pixels must be positive, the digit narrower than the badge, and Badge Face Inset Pixels leave the face wider and taller than its corner.");
            }

            var halfGap = blockGapPixels / 2f;
            var iceCorner = cornerRadiusPixels - (iceInsetPixels - halfGap);
            if (!(iceInsetPixels > halfGap && iceCorner > 0f))
            {
                problems.Add($"{name}: Ice Inset Pixels must be above half the Block Gap Pixels ({halfGap}), so the ice lies on the block, and below that plus Corner Radius Pixels, so its corner stays round.");
            }

            if (!(iceBevelPixels > 0f && iceBevelPixels <= iceCorner && iceHeightPixels > 0f))
            {
                problems.Add($"{name}: Ice Bevel Pixels must be above 0 and at most the ice's corner ({iceCorner}), and Ice Height Pixels above 0, so the ice's light turns smoothly round its corners.");
            }

            if (!(iceRimDepthPixels >= 0f && iceRimWidthPixels > 0f))
            {
                problems.Add($"{name}: Ice Rim Depth Pixels must be at least 0 and Ice Rim Width Pixels above 0.");
            }

            if (iceStreakOffsetsPixels == null || iceStreakOffsetsPixels.Length == 0
                || !(iceStreakWidthPixels > 0f && iceStreakWidthStepPixels >= 0f))
            {
                problems.Add($"{name}: Ice Streak Offsets Pixels needs at least one streak, Ice Streak Width Pixels must be above 0 and Ice Streak Width Step Pixels at least 0.");
            }

            if (!(iceStreakLeftFadePixels.x < iceStreakLeftFadePixels.y && iceStreakTopFadePixels.x < iceStreakTopFadePixels.y
                  && iceStreakRightFadePixels.x < iceStreakRightFadePixels.y
                  && iceStreakBottomFadePixels > 0f && iceStreakBottomFadePixels < cellPixels))
            {
                problems.Add($"{name}: Ice Streak Left, Top and Right Fade Pixels must each rise from their first to their second, and Ice Streak Bottom Fade Pixels lie above 0 and below a cell.");
            }
        }

        /// <summary>The constraints on the chain, the padlock, the key, the coin and the one-way arrow, each message naming its field.</summary>
        private void AddMarkArtProblems(List<string> problems)
        {
            if (!(chainSpacingPixels >= 1f && chainSpacingPixels == Mathf.Round(chainSpacingPixels)
                  && chainRingRadiiPixels.x > 0f && chainRingRadiiPixels.y > 0f && chainRingTubePixels > 0f
                  && chainLinkRadiiPixels.x > 0f && chainLinkRadiiPixels.y > 0f && chainLinkTubePixels > 0f
                  && chainRingHeightPixels > 0f && chainLinkHeightPixels > 0f))
            {
                problems.Add($"{name}: Chain Spacing Pixels must be a whole number of pixels, at least 1, and the chain's radii, tubes and heights above 0.");
            }

            if (!(lockBodyHalfSizePixels.x > lockBody.CornerPixels && lockBodyHalfSizePixels.y > lockBody.CornerPixels
                  && lockShackleRadiiPixels.x > 0f && lockShackleRadiiPixels.y > 0f && lockShackleTubePixels > 0f
                  && lockShackleRadiiPixels.x + lockShackleTubePixels <= lockBodyHalfSizePixels.x
                  && lockShackleHeightPixels > 0f && lockDropPixels == Mathf.Round(lockDropPixels)))
            {
                problems.Add($"{name}: Lock Body Half Size Pixels must be larger than its corner, Lock Shackle Radii Pixels and Tube Pixels positive and the shackle no wider than the body, and Lock Drop Pixels whole pixels.");
            }

            if (!(keyholeRadiusPixels > 0f && keyholeSlotHalfWidthPixels > 0f && keyholeDepthPixels >= 0f && keyholeBlurPixels > 0f
                  && keyholeRisePixels + keyholeRadiusPixels < lockBodyHalfSizePixels.y
                  && keyholeSlotEndPixels < lockBodyHalfSizePixels.y))
            {
                problems.Add($"{name}: the keyhole's radius, slot and blur must be positive and the keyhole (Keyhole Rise Pixels, Keyhole Radius Pixels, Keyhole Slot End Pixels) inside the padlock's body.");
            }

            if (!(goldKeyBowRadiusPixels > 0f && goldKeyShaftHalfPixels > 0f && goldKeyShaftHalfPixels < goldKeyBowRadiusPixels
                  && goldKeyShaftEndPixels > -goldKeyBowOffsetPixels && goldKeyHeightPixels > 0f
                  && goldKeyRoundingPixels > 0f && goldKeyEdgePixels > 0f
                  && gemRadiusPixels > 0f && gemRadiusPixels < gemSeatRadiusPixels && gemSeatRadiusPixels < goldKeyBowRadiusPixels
                  && gemSeatWidthPixels > 0f && gemHeightPixels > 0f))
            {
                problems.Add($"{name}: the key needs Gold Key Bow Radius Pixels above its shaft's half, a shaft ending right of the bow, positive heights and blurs, and the gem inside its seat inside the bow (Gem Radius Pixels below Gem Seat Radius Pixels below Gold Key Bow Radius Pixels).");
            }

            if (!(coinHandDesignRadiusPixels > 0f))
            {
                problems.Add($"{name}: Coin Hand Design Radius Pixels must be above 0.");
            }
            else if (!(coinRadiusPixels > coinFaceRadiusPixels && coinFaceRadiusPixels > 0f && coinRimPixels > 0f && coinHeightPixels > 0f
                       && coinFaceDepthPixels >= 0f && CoinHandHalfWidthPixels > 0f
                       && CoinMinuteHandPixels < coinFaceRadiusPixels && CoinHourHandPixels < coinFaceRadiusPixels))
            {
                problems.Add($"{name}: Coin Radius Pixels must exceed Coin Face Radius Pixels, the rim, height and hands be positive, and both hands (Coin Minute Hand Pixels, Coin Hour Hand Pixels, scaled to the coin) stay on the face.");
            }

            var oneWayHalfBlock = oneWayPaintedCells * cellPixels / 2f;
            var oneWayReach = oneWayTipPixels + OneWayMarginPixels;
            if (!(oneWayPaintedCells >= 1 && oneWayHeadStartPixels > 0f && oneWayHeadStartPixels < oneWayTipPixels
                  && oneWayShaftHalfLengthPixels > oneWayHeadStartPixels && oneWayShaftHalfLengthPixels < oneWayTipPixels
                  && oneWayShaftHalfThicknessPixels > 0f && oneWayHeadSlope > 0f
                  && oneWayTipPixels <= oneWayHalfBlock - blockGapPixels / 2f - bevelPixels))
            {
                problems.Add($"{name}: the one-way arrow needs its heads (One Way Head Start Pixels) inside its tips, its shaft (One Way Shaft Half Length Pixels) reaching into its heads, and its tips (One Way Tip Pixels) on the flat of a block One Way Painted Cells long.");
            }

            if (!(oneWayRaisePixels > 0f && oneWayRoundingPixels > 0f && oneWayEdgeGrowPixels >= 1 && oneWayEdgeGrowPixels % 2 == 1
                  && oneWayAlphaGain > 0f && oneWayReach <= oneWayHalfBlock))
            {
                problems.Add($"{name}: One Way Raise Pixels, One Way Rounding Pixels and One Way Alpha Gain must be above 0, One Way Edge Grow Pixels odd and at least 1, and the arrow with its shadow ({oneWayReach} px) fit a block One Way Painted Cells long.");
            }
        }

        /// <summary>The constraints on the shutter, the machine and the elevator, each message naming its field.</summary>
        private void AddPanelArtProblems(List<string> problems)
        {
            var shutterSide = shutterPaintedCells * cellPixels;
            if (!(shutterPaintedCells >= 1 && shutterRimInsetPixels >= 0f && shutterInnerInsetPixels > shutterRimInsetPixels
                  && shutterInnerCornerPixels >= 0f && shutterInnerInsetPixels + shutterInnerCornerPixels < shutterSide / 2f
                  && shutterRim.CornerPixels < shutterSide / 2f - shutterRimInsetPixels))
            {
                problems.Add($"{name}: Shutter Inner Inset Pixels must lie beyond Shutter Rim Inset Pixels, and the rim and its inner corner fit a region Shutter Painted Cells wide.");
            }

            if (!(shutterCylinderPitchPixels >= 2 && shutterCylinderHeightPixels > 0f && shutterGapSharpness > 0f
                  && shutterRecessPixels > 0f))
            {
                problems.Add($"{name}: Shutter Cylinder Pitch Pixels must be at least 2, and Shutter Cylinder Height Pixels, Shutter Gap Sharpness and Shutter Recess Pixels above 0.");
            }

            var screen = machineScreenSizePixels;
            if (!(machineInsetPixels >= 0f && machineRivetRadiusPixels > 0f && machineRivetHeightPixels > 0f
                  && machineRivetInsetPixels >= machineRivetRadiusPixels
                  && screen.x == Mathf.Round(screen.x) && screen.y == Mathf.Round(screen.y)
                  && screen.x > machineGlareFadePixels.y + machineScreenCornerPixels + machineScreenShadowPixels
                  && screen.y > 2f * (machineScreenCornerPixels + machineScreenShadowPixels)
                  && machineGlareFadePixels.y < screen.y / 2f
                  && machineScreenSlopePixels > 0f && machineScreenShadowPixels > 0f && machineGlareWidthPixels > 0f
                  && machineGlassFadePixels.x < machineGlassFadePixels.y && machineGlareInsetPixels.x < machineGlareInsetPixels.y
                  && machineGlassGradientPixels.x < machineGlassGradientPixels.y
                  && machineGlareFadePixels.x < machineGlareFadePixels.y))
            {
                problems.Add($"{name}: the machine needs positive rivets inside the body (Machine Rivet Inset Pixels at least the radius), a whole-pixel screen (Machine Screen Size Pixels) wider than Machine Glare Fade Pixels' end with a corner and taller than two corners with their shadow, the glare's fade ending within half the screen's height, and every fade and gradient rising from its first value to its second.");
            }

            if (!(machineButtonHalfSizePixels.x > machineButton.CornerPixels && machineButtonHalfSizePixels.y > machineButton.CornerPixels
                  && machineVentSlots >= 1 && machineVentPitchPixels > 2f * machineVentSlotHalfSizePixels.y
                  && machineVentSlotHalfSizePixels.x > 0f && machineVentSlotHalfSizePixels.y > 0f))
            {
                problems.Add($"{name}: Machine Button Half Size Pixels must exceed its corner, and the vent have at least one slot (Machine Vent Slots), slots apart (Machine Vent Pitch Pixels above their height).");
            }

            var elevatorSide = elevatorPaintedCells * cellPixels;
            var inner = elevatorInnerInsetPixels;
            var hazardOuter = elevatorHazardCentrePixels - elevatorHazardHalfWidthPixels;
            var hazardInner = elevatorHazardCentrePixels + elevatorHazardHalfWidthPixels;
            if (!(elevatorPaintedCells >= 1 && inner.x > elevatorRimInsetPixels && inner.y > elevatorRimInsetPixels
                  && inner.x + elevatorInnerCornerPixels < elevatorSide / 2f && inner.y + elevatorInnerCornerPixels < elevatorSide / 2f
                  && elevatorHazardHalfWidthPixels >= 1 && hazardOuter >= elevatorRimInsetPixels && hazardInner <= inner.x
                  && elevatorHazardStripePixels >= 1 && elevatorHazardEndPixels >= 0f
                  && elevatorHazardEndPixels < elevatorSide / 2f))
            {
                problems.Add($"{name}: the elevator's recess (Elevator Inner Inset Pixels) must lie inside its rim and fit a region Elevator Painted Cells wide, and each hazard strip (Elevator Hazard Centre Pixels, Elevator Hazard Half Width Pixels) lie on the rim.");
            }

            var door = ElevatorDoorHalfSizePixels;
            if (!(elevatorDoorGapPixels >= 0f && door.x > elevatorDoorPanelInsetPixels.x + elevatorDoorPanelCornerPixels
                  && door.y > elevatorDoorPanelInsetPixels.y + elevatorDoorPanelCornerPixels
                  && elevatorDoorPanelDepthPixels >= 0f && elevatorDoorPanelSlopePixels > 0f
                  && elevatorHandleHalfSizePixels.x >= elevatorHandle.CornerPixels && elevatorHandleHalfSizePixels.y >= elevatorHandle.CornerPixels
                  && elevatorHandleOffsetPixels - elevatorHandleHalfSizePixels.x >= elevatorDoorGapPixels / 2f))
            {
                problems.Add($"{name}: each door, with Elevator Door Gap Pixels, must hold its inset panel (Elevator Door Panel Inset Pixels and its corner), and each handle sit on its door (Elevator Handle Offset Pixels past the gap).");
            }
        }

        /// <summary>
        /// Half a door's width and height as painted, in pixels: half of the
        /// recess of an elevator <see cref="ElevatorPaintedCells"/> wide, less
        /// the gap around it.
        /// </summary>
        public Vector2 ElevatorDoorHalfSizePixels
        {
            get
            {
                var half = elevatorPaintedCells * cellPixels / 2f;
                var innerHalf = new Vector2(half - elevatorInnerInsetPixels.x, half - elevatorInnerInsetPixels.y);
                return new Vector2(innerHalf.x / 2f - elevatorDoorGapPixels, innerHalf.y - elevatorDoorGapPixels);
            }
        }

        /// <summary>
        /// The sizes the board draws the mechanics' sprites at, in cells, keyed
        /// by the <c>RuntimeConfig</c> field that holds each: what Generate Art
        /// writes into the config with the sprites, so the board places a
        /// sliced part's shadow margin, a recess or a label where the painter
        /// put it, and lays out the badges as wide as they are painted.
        /// </summary>
        public IReadOnlyList<(string field, float value)> BoardMeasures()
        {
            float cell = cellPixels;
            var alongArrow = 2 * ((int)Math.Ceiling(oneWayTipPixels) + OneWayMarginPixels);
            return new[]
            {
                ("badgeHeightCells", badgeSizePixels.y / cell),
                ("badgeDigitWidthCells", badgeDigitWidthPixels / cell),
                ("badgePaddingCells", (badgeSizePixels.x - badgeDigitWidthPixels) / 2f / cell),
                ("badgeShadowMarginCells", badgeShadow.ReachPixels / cell),
                ("raisedShadowMarginCells", raisedShadow.ReachPixels / cell),
                ("shutterInnerInsetCells", shutterInnerInsetPixels / cell),
                ("elevatorInnerInsetAcrossCells", elevatorInnerInsetPixels.x / cell),
                ("elevatorInnerInsetUpCells", elevatorInnerInsetPixels.y / cell),
                ("elevatorDoorGapCells", elevatorDoorGapPixels / cell),
                ("elevatorDoorShadowMarginCells", elevatorDoorShadow.ReachPixels / cell),
                ("elevatorHandleOffsetCells", elevatorHandleOffsetPixels / cell),
                ("elevatorHazardCentreCells", elevatorHazardCentrePixels / cell),
                ("elevatorHazardEndCells", elevatorHazardEndPixels / cell),
                ("oneWayArrowEndInsetCells", (oneWayPaintedCells * cell - alongArrow) / 2f / cell),
                ("padlockCountDropCells", (lockDropPixels + lockCountDropPixels) / cell),
                ("bonusCoinCells", 2f * coinRadiusPixels / cell),
            };
        }

        /// <summary>
        /// How far past its tips and sides the one-way arrow's canvas reaches,
        /// in whole pixels: the farthest of its rounding's blur, its grown
        /// edge and its shadow, the same on every side, so a horizontal and a
        /// vertical arrow stop equally short of a block's ends.
        /// </summary>
        public int OneWayMarginPixels =>
            Math.Max(
                Math.Max((int)Math.Ceiling(BlurReachSigmas * oneWayRoundingPixels), oneWayEdgeGrowPixels / 2),
                oneWayShadow.ReachPixels);
    }
}
