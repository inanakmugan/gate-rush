using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// Every parameter of the generated art (D46): the board and blocks of
    /// Module 15, the state visuals of Module 16, the HUD icons of Module 17,
    /// the exit cube, ice shard and gate glow of Module 18 and the sparkle of
    /// Module 19, restyled by Module 46 — sizes, radii, the lit body's height
    /// field and light, the floor shadow, studs, tones, arrow shapes. The look
    /// is tuned here, never in code; <see cref="ArtGenerator"/> reads nothing
    /// else. All sizes are in pixels of the generated textures, and tones are
    /// shares of the colour the runtime tints with, in linear light.
    /// </summary>
    /// <remarks>
    /// <para>The type lives in an editor-only assembly and nothing at runtime
    /// references the asset, so it never enters a build; only the PNGs and
    /// materials it produces do.</para>
    /// <para><b>Seams.</b> Four quarter pieces meet at every seam of a block,
    /// the frame and a gate, each drawn without knowing its neighbours. They
    /// meet cleanly only while the rounding stays inside the piece that owns
    /// it, which is what <see cref="Problems"/> checks: the corner radius and
    /// the bevel must fit inside the body, and an inside corner's radius
    /// inside half the gap between blocks — so inside corners stay nearly
    /// sharp.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "ArtRecipe", menuName = "Gate Rush/Art Recipe")]
    public sealed class ArtRecipe : ScriptableObject
    {
        /// <summary>Pixel centres sit half a pixel inside a sprite's border.</summary>
        private const float HalfPixel = 0.5f;

        [Header("Cell")]
        [Tooltip("Pixels per cell of a cell-sized sprite; a quarter piece is half of it. Also the sprites' pixels per unit. Even, at least 16.")]
        [SerializeField] private int cellPixels = 128;

        [Tooltip("Width of the anti-aliased edge, in pixels.")]
        [SerializeField] private float antiAliasPixels = 1f;

        [Header("Pieces (blocks, frame, gates)")]
        [Tooltip("Gap between two neighbouring blocks, in pixels: half of it is inset on each block's edge.")]
        [SerializeField] private float blockGapPixels = 6f;

        [Tooltip("Radius of an outer corner of a block's edge, in pixels. At least Bevel Pixels, and at most half a cell less half the gap, less half a pixel.")]
        [SerializeField] private float cornerRadiusPixels = 24f;

        [Tooltip("Radius of an inside (concave) corner, in pixels. At most half the gap less half a pixel, or the pieces stop meeting at their seams.")]
        [SerializeField] private float concaveRadiusPixels = 1f;

        [Header("Lit body (blocks, gates and the frame; Module 46)")]
        [Tooltip("How far in from its edge a body rises, in pixels: its edge is a quarter circle over this to Body Height Pixels. Above 0 and at most Corner Radius Pixels, so the way an edge faces turns smoothly round a corner.")]
        [SerializeField] private float bevelPixels = 16f;

        [Tooltip("How high a body stands, in pixels: with Bevel Pixels it sets how steeply the bevel faces out. Above 0.")]
        [SerializeField] private float bodyHeightPixels = 11f;

        [Tooltip("The direction toward the light, x right, y up, z out of the board; normalised when used. From the top left and in front.")]
        [SerializeField] private Vector3 lightDirection = new Vector3(-0.45f, 0.55f, 0.7f);

        [Tooltip("The share of a body's colour every surface gets, lit or not.")]
        [SerializeField, Range(0f, 1f)] private float ambient = 0.6f;

        [Tooltip("The share added in full where a surface faces the light squarely (Lambert). Ambient plus this may exceed 1; Shade Headroom stores it.")]
        [SerializeField, Range(0f, 1f)] private float diffuse = 0.55f;

        [Tooltip("The highlight's sharpness: the Blinn-Phong exponent. At least 1.")]
        [SerializeField] private float shininess = 30f;

        [Tooltip("The highlight's strength: white added where a surface mirrors the light toward the viewer.")]
        [SerializeField, Range(0f, 1f)] private float specularStrength = 0.3f;

        [Tooltip("How much darker a body is at the very foot of its bevel, by ambient occlusion: a share taken off the shade.")]
        [SerializeField, Range(0f, 1f)] private float occlusionFoot = 0.16f;

        [Tooltip("How far in from the edge the occlusion fades out, in pixels. Above 0 and at most Bevel Pixels.")]
        [SerializeField] private float occlusionFootPixels = 2f;

        [Tooltip("The occlusion never darkens below this share.")]
        [SerializeField, Range(0f, 1f)] private float occlusionMinimum = 0.4f;

        [Tooltip("The faces and studs are stored divided by this and the board's lit material multiplies them back, so a shade above 1 (lit bevels, stud tops) survives an 8-bit texture. The generator writes it into the lit materials. At least 1, and at least the highest shade.")]
        [SerializeField] private float shadeHeadroom = 2f;

        [Header("Floor shadow (under blocks, gates and the frame)")]
        [Tooltip("Black's alpha of a body's shadow on the floor, well under the body.")]
        [SerializeField, Range(0f, 1f)] private float floorShadowAlpha = 0.5f;

        [Tooltip("Half the width of the shadow's soft edge, in pixels: it eases from full to nothing across twice this. Above 0.")]
        [SerializeField] private float floorShadowSpreadPixels = 11.3f;

        [Tooltip("How far the shadow falls from the body, in pixels, x right and y up: down and to the right, away from the light.")]
        [SerializeField] private Vector2 floorShadowOffsetPixels = new Vector2(3f, -5f);

        [Header("Bevel (rounded box, padlock, key)")]
        [Tooltip("Width of the darker line along the outline, in pixels.")]
        [SerializeField] private float outlineWidthPixels = 2f;

        [SerializeField, Range(0f, 1f)] private float outlineTone = 0.55f;

        [Tooltip("Width of the highlight band just inside the outline, in pixels. It fades from Rim Tone to Face Tone.")]
        [SerializeField] private float rimWidthPixels = 10f;

        [SerializeField, Range(0f, 1f)] private float rimTone = 1f;

        [Tooltip("Tone of the face. Below 1 so the rim can read lighter: a runtime tint can only darken.")]
        [SerializeField, Range(0f, 1f)] private float faceTone = 0.84f;

        [Header("Studs (a grid per cell, centred)")]
        [Tooltip("How many studs a cell carries along each side: 2 gives a 2×2 grid, centred on the cell. At least 1.")]
        [SerializeField] private int studsPerCellSide = 2;

        [Tooltip("Distance between neighbouring stud centres within a cell, in pixels. The grid is centred on its cell, so studs in neighbouring cells sit Cell Pixels less (Studs Per Cell Side − 1) × this apart: below Cell Pixels ÷ Studs Per Cell Side, the outer studs sit further in from a block's edge than an even grid would put them.")]
        [SerializeField] private float studSpacingPixels = 59f;

        [Tooltip("Diameter of a stud, in pixels: its top disc and its wall are discs of this size. StudFitTests checks the grid fits a single cell.")]
        [SerializeField] private float studDiameterPixels = 35f;

        [Tooltip("Half the width of a stud disc's soft edge, in pixels: each disc eases in across twice this. Above 0.")]
        [SerializeField] private float studEdgePixels = 0.6f;

        [Tooltip("Where a stud's soft contact shadow on the face is centred, from the stud's grid point, in pixels, x right and y up.")]
        [SerializeField] private Vector2 studContactOffsetPixels = new Vector2(2f, -3f);

        [Tooltip("Black's alpha of the contact shadow at its strongest: the face is darkened by this share.")]
        [SerializeField, Range(0f, 1f)] private float studContactAlpha = 0.3f;

        [Tooltip("Where the contact shadow starts to fade, in pixels past the stud's radius from its centre; negative is inside.")]
        [SerializeField] private float studContactInnerPixels = -1f;

        [Tooltip("Where the contact shadow has faded out, in pixels past the stud's radius from its centre. Above Stud Contact Inner Pixels.")]
        [SerializeField] private float studContactOuterPixels = 6f;

        [Tooltip("Where a stud's wall — its cylinder side — is centred, from the grid point, in pixels, x right and y up.")]
        [SerializeField] private Vector2 studWallOffsetPixels = new Vector2(1.2f, -2.2f);

        [Tooltip("The wall's colour as a share of the block's.")]
        [SerializeField, Range(0f, 2f)] private float studWallTone = 0.55f;

        [Tooltip("Where a stud's top disc is centred, from the grid point, in pixels, x right and y up.")]
        [SerializeField] private Vector2 studTopOffsetPixels = new Vector2(-0.6f, 1f);

        [Tooltip("The top's colour as a share of the block's; above 1 reads lighter than the face. At most Shade Headroom.")]
        [SerializeField, Range(0f, 2f)] private float studTopTone = 1.08f;

        [Tooltip("The rim of the top: where it starts to rise, in pixels in from the top's edge.")]
        [SerializeField] private float studRimInnerPixels = 3.5f;

        [Tooltip("Where the rim is at its full strength, in pixels in from the top's edge. Below Stud Rim Inner Pixels.")]
        [SerializeField] private float studRimFullPixels = 1.2f;

        [Tooltip("Where the rim starts to fade toward the top's edge, in pixels in from it. At most Stud Rim Full Pixels.")]
        [SerializeField] private float studRimFadePixels = 1f;

        [Tooltip("The direction toward the light across a stud's top, x right and y up; normalised when used.")]
        [SerializeField] private Vector2 studRimLightDirection = new Vector2(-0.6f, 0.8f);

        [Tooltip("The rim's white toward the light at its strongest (untinted, the stud's gloss sprite).")]
        [SerializeField, Range(0f, 1f)] private float studRimLightAlpha = 0.35f;

        [Tooltip("How quickly the rim's light falls away round the stud: the exponent on how squarely the rim faces the light. Above 0.")]
        [SerializeField] private float studRimLightExponent = 1.5f;

        [Tooltip("How much the rim on the side away from the light darkens the top, as a share.")]
        [SerializeField, Range(0f, 1f)] private float studRimDarken = 0.12f;

        [Header("Floor")]
        [SerializeField, Range(0f, 1f)] private float floorTone = 0.85f;
        [SerializeField, Range(0f, 1f)] private float gridLineTone = 1f;

        [Tooltip("Width of the grid line between two floor tiles, in pixels; each tile draws half of it along its edges.")]
        [SerializeField] private float gridLinePixels = 2f;

        [Header("Gate arrow (points up; white)")]
        [SerializeField] private int gateArrowPixels = 64;

        [Tooltip("Length of the arrow, tip to tail, as a fraction of the sprite.")]
        [SerializeField, Range(0f, 1f)] private float gateArrowLength = 0.8f;

        [Tooltip("Width of the arrow head's base, as a fraction of the sprite.")]
        [SerializeField, Range(0f, 1f)] private float gateArrowHeadWidth = 0.8f;

        [Tooltip("Length of the arrow head, as a fraction of the sprite.")]
        [SerializeField, Range(0f, 1f)] private float gateArrowHeadLength = 0.45f;

        [Tooltip("Width of the shaft, as a fraction of the sprite.")]
        [SerializeField, Range(0f, 1f)] private float gateArrowShaftWidth = 0.32f;

        [Tooltip("Width of the dark outline round the gate arrow, in pixels: the outline sprite is the arrow grown by this. Arrow and outline must fit inside the sprite.")]
        [SerializeField] private float gateArrowOutlinePixels = 5f;

        [Header("Axis arrow (double-headed, horizontal; 9-sliced)")]
        [SerializeField] private int axisArrowLengthPixels = 192;
        [SerializeField] private int axisArrowThicknessPixels = 64;
        [SerializeField] private float axisArrowHeadLengthPixels = 30f;
        [SerializeField] private float axisArrowShaftPixels = 16f;

        [Header("Background")]
        [Tooltip("Height of the vertical gradient ramp, in pixels.")]
        [SerializeField] private int rampPixels = 256;

        [SerializeField] private int vignettePixels = 128;

        [Tooltip("Where the vignette starts, as a fraction of the distance from the centre to a corner.")]
        [SerializeField, Range(0f, 1f)] private float vignetteInner = 0.45f;

        [Tooltip("Where the vignette reaches full strength, as a fraction of the distance from the centre to a corner.")]
        [SerializeField, Range(0f, 2f)] private float vignetteOuter = 1f;

        [Header("Frost (one cell; drawn untinted over ice)")]
        [SerializeField] private int frostStreakCount = 3;
        [SerializeField] private float frostStreakWidthPixels = 5f;

        [Tooltip("Distance between neighbouring streaks, across them, in pixels.")]
        [SerializeField] private float frostStreakSpacingPixels = 26f;

        [Tooltip("Length of the middle streak, in pixels; the outer ones are shorter.")]
        [SerializeField] private float frostStreakLengthPixels = 70f;

        [Tooltip("Margin from every side of the cell that no streak crosses, in pixels. It must clear half the gap and an outer corner's rounding, so frost never leaves a block's face.")]
        [SerializeField] private float frostInsetPixels = 22f;

        [SerializeField, Range(0f, 1f)] private float frostAlpha = 0.75f;

        [Header("Rounded box (9-sliced; badges, machines, dividers)")]
        [SerializeField] private int panelPixels = 64;

        [Tooltip("Corner radius, in pixels. Shaded with the face's outline and rim.")]
        [SerializeField] private float panelCornerRadiusPixels = 16f;

        [Header("Ring (9-sliced outline; shutter and elevator borders)")]
        [SerializeField] private int ringPixels = 32;

        [Tooltip("Width of the outline, in pixels; also its outer corner radius and its slice border.")]
        [SerializeField] private int ringWidthPixels = 6;

        [Header("Chain (one period, tiled along x)")]
        [SerializeField] private int chainPeriodPixels = 64;
        [SerializeField] private int chainThicknessPixels = 32;

        [Tooltip("Length of the link seen face-on, in pixels.")]
        [SerializeField] private float chainLinkLengthPixels = 44f;

        [Tooltip("Length of the link seen edge-on, which straddles the period's ends, in pixels.")]
        [SerializeField] private float chainEdgeLinkLengthPixels = 30f;

        [Tooltip("Thickness of a link's metal, in pixels.")]
        [SerializeField] private float chainLinkWallPixels = 7f;

        [SerializeField, Range(0f, 1f)] private float chainTone = 1f;
        [SerializeField, Range(0f, 1f)] private float chainShadeTone = 0.6f;

        [Header("Padlock (sizes as fractions of the sprite)")]
        [SerializeField] private int padlockPixels = 128;
        [SerializeField, Range(0f, 1f)] private float padlockBodyWidth = 0.78f;
        [SerializeField, Range(0f, 1f)] private float padlockBodyHeight = 0.56f;
        [SerializeField] private float padlockBodyCornerPixels = 14f;

        [Tooltip("Radius of the shackle's arc, to the middle of its bar.")]
        [SerializeField, Range(0f, 1f)] private float padlockShackleRadius = 0.24f;

        [SerializeField, Range(0f, 1f)] private float padlockShackleThickness = 0.1f;
        [SerializeField, Range(0f, 1f)] private float padlockShackleTone = 0.8f;

        [Header("Key (horizontal: bow on the left)")]
        [SerializeField] private int keyWidthPixels = 128;
        [SerializeField] private int keyHeightPixels = 64;
        [SerializeField] private float keyBowDiameterPixels = 56f;

        [Tooltip("Diameter of the hole in the bow the gem sits in, in pixels.")]
        [SerializeField] private float keyHoleDiameterPixels = 32f;

        [SerializeField] private float keyGemDiameterPixels = 26f;
        [SerializeField] private float keyShaftThicknessPixels = 12f;
        [SerializeField] private float keyToothWidthPixels = 10f;
        [SerializeField] private float keyToothLengthPixels = 14f;
        [SerializeField, Range(0f, 1f)] private float keyGemTone = 0.95f;
        [SerializeField, Range(0f, 1f)] private float keyGemEdgeTone = 0.55f;

        [Header("Shutter slats (one cell, tiled)")]
        [SerializeField] private int slatsPerCell = 4;

        [Tooltip("Width of the groove between two slats, in pixels.")]
        [SerializeField] private float slatGapPixels = 5f;

        [SerializeField, Range(0f, 1f)] private float slatTone = 1f;
        [SerializeField, Range(0f, 1f)] private float slatShadeTone = 0.7f;
        [SerializeField, Range(0f, 1f)] private float slatGapTone = 0.35f;

        [Header("Lift doors (one cell, tiled)")]
        [SerializeField] private int doorLinesPerCell = 2;
        [SerializeField] private float doorLineWidthPixels = 2f;
        [SerializeField, Range(0f, 1f)] private float doorTone = 0.85f;
        [SerializeField, Range(0f, 1f)] private float doorLineTone = 1f;

        [Header("Clock icon (white; sizes as fractions of the sprite)")]
        [SerializeField] private int clockPixels = 128;

        [Tooltip("Thickness of the round rim.")]
        [SerializeField, Range(0f, 0.5f)] private float clockRimThickness = 0.09f;

        [SerializeField, Range(0f, 0.5f)] private float clockHandThickness = 0.09f;

        [Tooltip("From the centre to the minute hand's tip; it points to 12.")]
        [SerializeField, Range(0f, 0.5f)] private float clockMinuteHandLength = 0.28f;

        [Tooltip("From the centre to the hour hand's tip; it points to 3.")]
        [SerializeField, Range(0f, 0.5f)] private float clockHourHandLength = 0.2f;

        [Header("Restart icon (white, clockwise; sizes as fractions of the sprite)")]
        [SerializeField] private int restartPixels = 128;

        [Tooltip("Radius of the arc, to the middle of its stroke. The arc runs three quarters of the way round, from 3 o'clock through 6 and 9 to 12.")]
        [SerializeField, Range(0f, 0.5f)] private float restartRadius = 0.3f;

        [SerializeField, Range(0f, 0.5f)] private float restartThickness = 0.11f;

        [Tooltip("Width of the arrow head's base, across the arc at 12 o'clock.")]
        [SerializeField, Range(0f, 1f)] private float restartHeadWidth = 0.32f;

        [Tooltip("Length of the arrow head, from the arc's end at 12 o'clock to its tip, pointing right.")]
        [SerializeField, Range(0f, 0.5f)] private float restartHeadLength = 0.2f;

        [Header("Exit cube (tinted the block's colour)")]
        [Tooltip("Side of the cube sprite. At least 8.")]
        [SerializeField] private int cubePixels = 48;

        [Tooltip("Corner radius of the cube.")]
        [SerializeField] private float cubeCornerPixels = 10f;

        [Tooltip("Width of the light bevel just inside the cube's edge.")]
        [SerializeField] private float cubeRimPixels = 5f;

        [SerializeField, Range(0f, 1f)] private float cubeRimTone = 1f;
        [SerializeField, Range(0f, 1f)] private float cubeFaceTone = 0.85f;

        [Tooltip("Diameter of the soft highlight on the cube, like a stud's gloss.")]
        [SerializeField] private float cubeHighlightDiameterPixels = 12f;

        [Tooltip("Where the highlight sits, from the cube's centre.")]
        [SerializeField] private Vector2 cubeHighlightOffsetPixels = new Vector2(-8f, 8f);

        [Tooltip("How far the highlight's edge fades, in pixels.")]
        [SerializeField] private float cubeHighlightSoftnessPixels = 3f;

        [SerializeField, Range(0f, 1f)] private float cubeHighlightTone = 1f;

        [Header("Ice shard (tinted the ice colour)")]
        [Tooltip("Side of the shard sprite. At least 8.")]
        [SerializeField] private int shardPixels = 48;

        [Tooltip("The shard's four corners as fractions of the sprite, counter-clockwise, forming a convex shape. The frost streak runs from the first to the third.")]
        [SerializeField] private Vector2[] shardPoints =
        {
            new Vector2(0.5f, 0.95f), new Vector2(0.1f, 0.45f), new Vector2(0.45f, 0.05f), new Vector2(0.9f, 0.55f)
        };

        [SerializeField, Range(0f, 1f)] private float shardFaceTone = 0.8f;

        [Tooltip("Width of the frost streak across the shard.")]
        [SerializeField] private float shardStreakWidthPixels = 4f;

        [SerializeField, Range(0f, 1f)] private float shardStreakTone = 1f;

        [Header("Gate glow (white, strongest along its bottom edge; tinted the gate's colour)")]
        [Tooltip("Height of the glow's gradient, from the gate into the board. At least 8.")]
        [SerializeField] private int gateGlowPixels = 64;

        [Tooltip("The fraction of the height, from the gate, held at full strength before the glow fades: at least 0, below 1.")]
        [SerializeField] private float gateGlowHold = 0.1f;

        [Header("Sparkle (white four-point star; tinted where placed)")]
        [Tooltip("Side of the sparkle sprite. At least 8.")]
        [SerializeField] private int sparklePixels = 64;

        [Tooltip("How far the star's four inner corners sit from its centre along each axis, as a fraction of the sprite: above 0 and below a quarter, so its sides bend inward.")]
        [SerializeField] private float sparkleWaist = 0.09f;

        /// <summary>Pixels per cell, and the sprites' pixels per unit.</summary>
        public int CellPixels => cellPixels;

        /// <summary>Side of a quarter piece, in pixels.</summary>
        public int QuarterPixels => cellPixels / 2;

        /// <summary>Width of the anti-aliased edge, in pixels.</summary>
        public float AntiAliasPixels => antiAliasPixels;

        /// <summary>Gap between neighbouring blocks, in pixels.</summary>
        public float BlockGapPixels => blockGapPixels;

        /// <summary>Radius of an outer corner, in pixels.</summary>
        public float CornerRadiusPixels => cornerRadiusPixels;

        /// <summary>Radius of an inside corner, in pixels.</summary>
        public float ConcaveRadiusPixels => concaveRadiusPixels;

        /// <summary>How far in from its edge a body rises, in pixels.</summary>
        public float BevelPixels => bevelPixels;

        /// <summary>How high a body stands, in pixels.</summary>
        public float BodyHeightPixels => bodyHeightPixels;

        /// <summary>The direction toward the light, x right, y up, z out of the board; not normalised.</summary>
        public Vector3 LightDirection => lightDirection;

        /// <summary>The share of a body's colour every surface gets.</summary>
        public float Ambient => ambient;

        /// <summary>The share added where a surface faces the light squarely.</summary>
        public float Diffuse => diffuse;

        /// <summary>The Blinn-Phong exponent.</summary>
        public float Shininess => shininess;

        /// <summary>The white the highlight adds at its strongest.</summary>
        public float SpecularStrength => specularStrength;

        /// <summary>The share the occlusion takes off at the very foot of a bevel.</summary>
        public float OcclusionFoot => occlusionFoot;

        /// <summary>How far in from the edge the occlusion fades out, in pixels.</summary>
        public float OcclusionFootPixels => occlusionFootPixels;

        /// <summary>The lowest share the occlusion leaves.</summary>
        public float OcclusionMinimum => occlusionMinimum;

        /// <summary>The factor faces and studs are stored divided by, and the lit materials multiply back.</summary>
        public float ShadeHeadroom => shadeHeadroom;

        /// <summary>Black's alpha of a body's floor shadow at its strongest.</summary>
        public float FloorShadowAlpha => floorShadowAlpha;

        /// <summary>Half the width of the floor shadow's soft edge, in pixels.</summary>
        public float FloorShadowSpreadPixels => floorShadowSpreadPixels;

        /// <summary>How far the floor shadow falls from the body, in pixels, x right and y up.</summary>
        public Vector2 FloorShadowOffsetPixels => floorShadowOffsetPixels;

        /// <summary>
        /// How far a body's floor shadow reaches past its cell, in pixels,
        /// rounded up: past its edge by its offset and its soft edge, less the
        /// half gap between the edge and the cell's. A shadow piece's canvas is
        /// a quarter widened by this on the sides that face out of the body.
        /// </summary>
        public int FloorShadowMarginPixels =>
            Math.Max(0, (int)Math.Ceiling(
                Math.Max(Math.Abs(floorShadowOffsetPixels.x), Math.Abs(floorShadowOffsetPixels.y))
                + floorShadowSpreadPixels - blockGapPixels / 2f));

        /// <summary>Width of the rounded box's, padlock's and key's darker outline line, in pixels.</summary>
        public float OutlineWidthPixels => outlineWidthPixels;

        /// <summary>Tone of the outline.</summary>
        public float OutlineTone => outlineTone;

        /// <summary>Width of the rim highlight, in pixels.</summary>
        public float RimWidthPixels => rimWidthPixels;

        /// <summary>Tone at the outer edge of the rim highlight.</summary>
        public float RimTone => rimTone;

        /// <summary>Tone of the face inside the rim.</summary>
        public float FaceTone => faceTone;

        /// <summary>Diameter of a stud's top and wall, in pixels.</summary>
        public float StudDiameterPixels => studDiameterPixels;

        /// <summary>How many studs a cell carries along each side.</summary>
        public int StudsPerCellSide => studsPerCellSide;

        /// <summary>Distance between neighbouring stud centres within a cell, in pixels; the grid is centred on its cell.</summary>
        public float StudSpacingPixels => studSpacingPixels;

        /// <summary>Half the width of a stud disc's soft edge, in pixels.</summary>
        public float StudEdgePixels => studEdgePixels;

        /// <summary>The contact shadow's centre from the grid point, in pixels, x right and y up.</summary>
        public Vector2 StudContactOffsetPixels => studContactOffsetPixels;

        /// <summary>Black's alpha of the contact shadow at its strongest.</summary>
        public float StudContactAlpha => studContactAlpha;

        /// <summary>Where the contact shadow starts to fade, in pixels past the stud's radius.</summary>
        public float StudContactInnerPixels => studContactInnerPixels;

        /// <summary>Where the contact shadow has faded out, in pixels past the stud's radius.</summary>
        public float StudContactOuterPixels => studContactOuterPixels;

        /// <summary>The wall's centre from the grid point, in pixels, x right and y up.</summary>
        public Vector2 StudWallOffsetPixels => studWallOffsetPixels;

        /// <summary>The wall's colour as a share of the block's.</summary>
        public float StudWallTone => studWallTone;

        /// <summary>The top's centre from the grid point, in pixels, x right and y up.</summary>
        public Vector2 StudTopOffsetPixels => studTopOffsetPixels;

        /// <summary>The top's colour as a share of the block's.</summary>
        public float StudTopTone => studTopTone;

        /// <summary>Where the top's rim starts to rise, in pixels in from its edge.</summary>
        public float StudRimInnerPixels => studRimInnerPixels;

        /// <summary>Where the rim is at full strength, in pixels in from the top's edge.</summary>
        public float StudRimFullPixels => studRimFullPixels;

        /// <summary>Where the rim starts to fade toward the top's edge, in pixels in from it.</summary>
        public float StudRimFadePixels => studRimFadePixels;

        /// <summary>The direction toward the light across a stud's top, x right and y up; not normalised.</summary>
        public Vector2 StudRimLightDirection => studRimLightDirection;

        /// <summary>The rim's white toward the light at its strongest.</summary>
        public float StudRimLightAlpha => studRimLightAlpha;

        /// <summary>The exponent on how squarely the rim faces the light.</summary>
        public float StudRimLightExponent => studRimLightExponent;

        /// <summary>How much the rim away from the light darkens the top.</summary>
        public float StudRimDarken => studRimDarken;

        /// <summary>Tone of a floor tile.</summary>
        public float FloorTone => floorTone;

        /// <summary>Tone of the grid line.</summary>
        public float GridLineTone => gridLineTone;

        /// <summary>Full width of the grid line between two tiles, in pixels.</summary>
        public float GridLinePixels => gridLinePixels;

        /// <summary>Side of the gate arrow sprite, in pixels.</summary>
        public int GateArrowPixels => gateArrowPixels;

        /// <summary>Arrow length, as a fraction of the sprite.</summary>
        public float GateArrowLength => gateArrowLength;

        /// <summary>Head base width, as a fraction of the sprite.</summary>
        public float GateArrowHeadWidth => gateArrowHeadWidth;

        /// <summary>Head length, as a fraction of the sprite.</summary>
        public float GateArrowHeadLength => gateArrowHeadLength;

        /// <summary>Shaft width, as a fraction of the sprite.</summary>
        public float GateArrowShaftWidth => gateArrowShaftWidth;

        /// <summary>Width of the gate arrow's outline, in pixels.</summary>
        public float GateArrowOutlinePixels => gateArrowOutlinePixels;

        /// <summary>Width of the axis arrow sprite, in pixels.</summary>
        public int AxisArrowLengthPixels => axisArrowLengthPixels;

        /// <summary>Height of the axis arrow sprite, in pixels.</summary>
        public int AxisArrowThicknessPixels => axisArrowThicknessPixels;

        /// <summary>Length of each head of the axis arrow, in pixels.</summary>
        public float AxisArrowHeadLengthPixels => axisArrowHeadLengthPixels;

        /// <summary>Thickness of the axis arrow's shaft, in pixels.</summary>
        public float AxisArrowShaftPixels => axisArrowShaftPixels;

        /// <summary>Height of the gradient ramp, in pixels.</summary>
        public int RampPixels => rampPixels;

        /// <summary>Side of the vignette sprite, in pixels.</summary>
        public int VignettePixels => vignettePixels;

        /// <summary>Where the vignette starts, as a fraction of the centre-to-corner distance.</summary>
        public float VignetteInner => vignetteInner;

        /// <summary>Where the vignette is full, as a fraction of the centre-to-corner distance.</summary>
        public float VignetteOuter => vignetteOuter;

        /// <summary>Number of frost streaks per cell.</summary>
        public int FrostStreakCount => frostStreakCount;

        /// <summary>Width of a frost streak, in pixels.</summary>
        public float FrostStreakWidthPixels => frostStreakWidthPixels;

        /// <summary>Distance between neighbouring streaks, in pixels.</summary>
        public float FrostStreakSpacingPixels => frostStreakSpacingPixels;

        /// <summary>Length of the middle streak, in pixels.</summary>
        public float FrostStreakLengthPixels => frostStreakLengthPixels;

        /// <summary>Margin from the cell's sides no streak crosses, in pixels.</summary>
        public float FrostInsetPixels => frostInsetPixels;

        /// <summary>Opacity of the frost.</summary>
        public float FrostAlpha => frostAlpha;

        /// <summary>Side of the rounded box sprite, in pixels.</summary>
        public int PanelPixels => panelPixels;

        /// <summary>Corner radius of the rounded box, in pixels.</summary>
        public float PanelCornerRadiusPixels => panelCornerRadiusPixels;

        /// <summary>Side of the ring sprite, in pixels.</summary>
        public int RingPixels => ringPixels;

        /// <summary>Width of the ring's outline, in pixels.</summary>
        public int RingWidthPixels => ringWidthPixels;

        /// <summary>Length of one chain period, in pixels.</summary>
        public int ChainPeriodPixels => chainPeriodPixels;

        /// <summary>Thickness of the chain sprite, in pixels.</summary>
        public int ChainThicknessPixels => chainThicknessPixels;

        /// <summary>Length of the face-on link, in pixels.</summary>
        public float ChainLinkLengthPixels => chainLinkLengthPixels;

        /// <summary>Length of the edge-on link, in pixels.</summary>
        public float ChainEdgeLinkLengthPixels => chainEdgeLinkLengthPixels;

        /// <summary>Thickness of a link's metal, in pixels.</summary>
        public float ChainLinkWallPixels => chainLinkWallPixels;

        /// <summary>Tone inside a link's metal.</summary>
        public float ChainTone => chainTone;

        /// <summary>Tone at a link's edges.</summary>
        public float ChainShadeTone => chainShadeTone;

        /// <summary>Side of the padlock sprite, in pixels.</summary>
        public int PadlockPixels => padlockPixels;

        /// <summary>Width of the padlock's body, as a fraction of the sprite.</summary>
        public float PadlockBodyWidth => padlockBodyWidth;

        /// <summary>Height of the padlock's body, as a fraction of the sprite.</summary>
        public float PadlockBodyHeight => padlockBodyHeight;

        /// <summary>Corner radius of the padlock's body, in pixels.</summary>
        public float PadlockBodyCornerPixels => padlockBodyCornerPixels;

        /// <summary>Radius of the shackle's arc, as a fraction of the sprite.</summary>
        public float PadlockShackleRadius => padlockShackleRadius;

        /// <summary>Thickness of the shackle, as a fraction of the sprite.</summary>
        public float PadlockShackleThickness => padlockShackleThickness;

        /// <summary>Tone of the shackle.</summary>
        public float PadlockShackleTone => padlockShackleTone;

        /// <summary>Width of the key sprites, in pixels.</summary>
        public int KeyWidthPixels => keyWidthPixels;

        /// <summary>Height of the key sprites, in pixels.</summary>
        public int KeyHeightPixels => keyHeightPixels;

        /// <summary>Diameter of the key's bow, in pixels.</summary>
        public float KeyBowDiameterPixels => keyBowDiameterPixels;

        /// <summary>Diameter of the hole in the bow, in pixels.</summary>
        public float KeyHoleDiameterPixels => keyHoleDiameterPixels;

        /// <summary>Diameter of the gem, in pixels.</summary>
        public float KeyGemDiameterPixels => keyGemDiameterPixels;

        /// <summary>Thickness of the key's shaft, in pixels.</summary>
        public float KeyShaftThicknessPixels => keyShaftThicknessPixels;

        /// <summary>Width of a key tooth, in pixels.</summary>
        public float KeyToothWidthPixels => keyToothWidthPixels;

        /// <summary>How far a key tooth hangs below the shaft, in pixels.</summary>
        public float KeyToothLengthPixels => keyToothLengthPixels;

        /// <summary>Tone at the gem's centre.</summary>
        public float KeyGemTone => keyGemTone;

        /// <summary>Tone at the gem's edge.</summary>
        public float KeyGemEdgeTone => keyGemEdgeTone;

        /// <summary>Number of shutter slats per cell.</summary>
        public int SlatsPerCell => slatsPerCell;

        /// <summary>Width of the groove between slats, in pixels.</summary>
        public float SlatGapPixels => slatGapPixels;

        /// <summary>Tone at the top of a slat.</summary>
        public float SlatTone => slatTone;

        /// <summary>Tone at the bottom of a slat.</summary>
        public float SlatShadeTone => slatShadeTone;

        /// <summary>Tone of the groove between slats.</summary>
        public float SlatGapTone => slatGapTone;

        /// <summary>Number of vertical lines per cell of lift door.</summary>
        public int DoorLinesPerCell => doorLinesPerCell;

        /// <summary>Width of a door line, in pixels.</summary>
        public float DoorLineWidthPixels => doorLineWidthPixels;

        /// <summary>Tone of a door panel.</summary>
        public float DoorTone => doorTone;

        /// <summary>Tone of a door line.</summary>
        public float DoorLineTone => doorLineTone;

        /// <summary>Side of the clock icon, in pixels.</summary>
        public int ClockPixels => clockPixels;

        /// <summary>Thickness of the clock's rim, as a fraction of the sprite.</summary>
        public float ClockRimThickness => clockRimThickness;

        /// <summary>Thickness of the clock's hands, as a fraction of the sprite.</summary>
        public float ClockHandThickness => clockHandThickness;

        /// <summary>Length of the minute hand, as a fraction of the sprite.</summary>
        public float ClockMinuteHandLength => clockMinuteHandLength;

        /// <summary>Length of the hour hand, as a fraction of the sprite.</summary>
        public float ClockHourHandLength => clockHourHandLength;

        /// <summary>Side of the restart icon, in pixels.</summary>
        public int RestartPixels => restartPixels;

        /// <summary>Radius of the restart arc to the middle of its stroke, as a fraction of the sprite.</summary>
        public float RestartRadius => restartRadius;

        /// <summary>Thickness of the restart arc, as a fraction of the sprite.</summary>
        public float RestartThickness => restartThickness;

        /// <summary>Width of the restart arrow's head, as a fraction of the sprite.</summary>
        public float RestartHeadWidth => restartHeadWidth;

        /// <summary>Length of the restart arrow's head, as a fraction of the sprite.</summary>
        public float RestartHeadLength => restartHeadLength;

        /// <summary>Side of the cube sprite, in pixels.</summary>
        public int CubePixels => cubePixels;

        /// <summary>Corner radius of the cube, in pixels.</summary>
        public float CubeCornerPixels => cubeCornerPixels;

        /// <summary>Width of the cube's light bevel, in pixels.</summary>
        public float CubeRimPixels => cubeRimPixels;

        /// <summary>Tone of the cube's bevel.</summary>
        public float CubeRimTone => cubeRimTone;

        /// <summary>Tone of the cube's face.</summary>
        public float CubeFaceTone => cubeFaceTone;

        /// <summary>Diameter of the cube's highlight, in pixels.</summary>
        public float CubeHighlightDiameterPixels => cubeHighlightDiameterPixels;

        /// <summary>Offset of the cube's highlight from its centre, in pixels.</summary>
        public Vector2 CubeHighlightOffsetPixels => cubeHighlightOffsetPixels;

        /// <summary>How far the cube's highlight fades at its edge, in pixels.</summary>
        public float CubeHighlightSoftnessPixels => cubeHighlightSoftnessPixels;

        /// <summary>Tone of the cube's highlight.</summary>
        public float CubeHighlightTone => cubeHighlightTone;

        /// <summary>Side of the shard sprite, in pixels.</summary>
        public int ShardPixels => shardPixels;

        /// <summary>The shard's corners as fractions of the sprite, counter-clockwise.</summary>
        public IReadOnlyList<Vector2> ShardPoints => shardPoints;

        /// <summary>Tone of the shard's face.</summary>
        public float ShardFaceTone => shardFaceTone;

        /// <summary>Width of the shard's frost streak, in pixels.</summary>
        public float ShardStreakWidthPixels => shardStreakWidthPixels;

        /// <summary>Tone of the shard's frost streak.</summary>
        public float ShardStreakTone => shardStreakTone;

        /// <summary>Height of the gate glow's gradient, in pixels.</summary>
        public int GateGlowPixels => gateGlowPixels;

        /// <summary>The fraction of the gate glow held at full strength before it fades.</summary>
        public float GateGlowHold => gateGlowHold;

        /// <summary>Side of the sparkle sprite, in pixels.</summary>
        public int SparklePixels => sparklePixels;

        /// <summary>How far the sparkle's inner corners sit from its centre along each axis, as a fraction of the sprite.</summary>
        public float SparkleWaist => sparkleWaist;

        /// <summary>
        /// The rounded box's 9-slice border, in pixels, on every side: its
        /// corner and its outline-and-rim bevel both fit inside it, so the
        /// stretched middle holds only straight profile. The runtime scales the
        /// box so this border draws as the corner it asks for.
        /// </summary>
        public int PanelBorderPixels =>
            (int)Math.Ceiling(Math.Max(panelCornerRadiusPixels, outlineWidthPixels + rimWidthPixels) + antiAliasPixels);

        /// <summary>
        /// Where frost may not reach from a cell's side, in pixels, for it to
        /// stay on a block's face: half the gap, plus how far the edge's
        /// corner cuts in along the cell's diagonal, plus the anti-aliased edge.
        /// </summary>
        private float MinFrostInsetPixels =>
            blockGapPixels / 2f + cornerRadiusPixels
            - cornerRadiusPixels * (float)(1.0 / Math.Sqrt(2.0)) + antiAliasPixels;

        /// <summary>
        /// Every reason this recipe cannot generate the art, as messages naming
        /// the field as the inspector shows it; empty when it can.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            var quarter = cellPixels / 2f;
            var halfGap = blockGapPixels / 2f;
            var faceExtent = quarter - halfGap;

            if (cellPixels < 16 || cellPixels % 2 != 0)
            {
                problems.Add($"{name}: Cell Pixels must be even and at least 16.");
            }

            if (!(antiAliasPixels > 0f))
            {
                problems.Add($"{name}: Anti Alias Pixels must be positive.");
            }

            if (!(blockGapPixels >= 0f && halfGap < quarter))
            {
                problems.Add($"{name}: Block Gap Pixels must be at least 0 and below Cell Pixels.");
            }

            // A rounding must end by the last pixel centre before a seam, half a
            // pixel inside it, so the pixels on both sides of the seam already
            // show the straight outline the neighbouring piece draws.
            if (!(cornerRadiusPixels >= 0f && cornerRadiusPixels <= faceExtent - HalfPixel))
            {
                problems.Add($"{name}: Corner Radius Pixels must be at least 0 and at most half a cell less half the gap, less half a pixel ({faceExtent - HalfPixel}).");
            }

            if (!(concaveRadiusPixels >= 0f && concaveRadiusPixels <= halfGap - HalfPixel))
            {
                problems.Add($"{name}: Concave Radius Pixels must be at least 0 and at most half the Block Gap Pixels less half a pixel ({halfGap - HalfPixel}).");
            }

            AddBodyProblems(problems);

            if (!(outlineWidthPixels >= 0f && rimWidthPixels >= 0f && outlineWidthPixels + rimWidthPixels < panelPixels / 2f))
            {
                problems.Add($"{name}: Outline Width Pixels and Rim Width Pixels may not be negative, and together must fit inside half of Panel Pixels.");
            }

            AddStudProblems(problems, faceExtent);

            if (!(gridLinePixels >= 0f && gridLinePixels <= quarter))
            {
                problems.Add($"{name}: Grid Line Pixels must be at least 0 and at most half a cell.");
            }

            if (gateArrowPixels < 8)
            {
                problems.Add($"{name}: Gate Arrow Pixels must be at least 8.");
            }

            if (!(gateArrowHeadLength > 0f && gateArrowHeadLength < gateArrowLength
                  && gateArrowShaftWidth > 0f && gateArrowShaftWidth <= gateArrowHeadWidth))
            {
                problems.Add($"{name}: the gate arrow's head must be shorter than the arrow, and its shaft positive and no wider than its head.");
            }

            // The arrow is centred, so its longest extent, along or across,
            // leaves this margin at each side of the sprite.
            var arrowMargin = (1f - Math.Max(gateArrowLength, gateArrowHeadWidth)) * gateArrowPixels / 2f;
            if (!(gateArrowOutlinePixels > 0f && gateArrowOutlinePixels + antiAliasPixels <= arrowMargin))
            {
                problems.Add($"{name}: Gate Arrow Outline Pixels must be above 0, and with the anti-aliased edge fit in the arrow's margin inside Gate Arrow Pixels ({arrowMargin}).");
            }

            if (axisArrowLengthPixels < 8 || axisArrowThicknessPixels < 8)
            {
                problems.Add($"{name}: Axis Arrow Length Pixels and Axis Arrow Thickness Pixels must be at least 8.");
            }

            if (!(axisArrowHeadLengthPixels > 0f && axisArrowHeadLengthPixels * 2f < axisArrowLengthPixels))
            {
                problems.Add($"{name}: Axis Arrow Head Length Pixels must be positive and leave room for a shaft between the two heads.");
            }

            if (!(axisArrowShaftPixels > 0f && axisArrowShaftPixels <= axisArrowThicknessPixels))
            {
                problems.Add($"{name}: Axis Arrow Shaft Pixels must be positive and at most Axis Arrow Thickness Pixels.");
            }

            if (rampPixels < 2 || vignettePixels < 2)
            {
                problems.Add($"{name}: Ramp Pixels and Vignette Pixels must be at least 2.");
            }

            if (!(vignetteInner >= 0f && vignetteInner < vignetteOuter))
            {
                problems.Add($"{name}: Vignette Inner must be at least 0 and below Vignette Outer.");
            }

            AddStateProblems(problems);
            AddIconProblems(problems);
            AddFeedbackProblems(problems);
            AddIntroductionProblems(problems);
            return problems;
        }

        /// <summary>
        /// The constraints on Module 46's lit body and its floor shadow, each
        /// message naming its field. The bevel must end within the corner's
        /// radius, so the way the edge faces turns smoothly round a corner
        /// instead of jumping across its diagonal, and every pixel of a piece
        /// stays a function of its own geometry; the highest shade must fit
        /// the headroom the lit materials multiply back.
        /// </summary>
        private void AddBodyProblems(List<string> problems)
        {
            if (!(bevelPixels > 0f && bevelPixels <= cornerRadiusPixels))
            {
                problems.Add($"{name}: Bevel Pixels must be above 0 and at most Corner Radius Pixels ({cornerRadiusPixels}).");
            }

            if (!(bodyHeightPixels > 0f))
            {
                problems.Add($"{name}: Body Height Pixels must be above 0.");
            }

            if (!(lightDirection.z > 0f))
            {
                problems.Add($"{name}: Light Direction must point out of the board (z above 0), or a flat top would be unlit.");
            }

            if (!(shininess >= 1f))
            {
                problems.Add($"{name}: Shininess must be at least 1.");
            }

            if (!(occlusionFootPixels > 0f && occlusionFootPixels <= bevelPixels))
            {
                problems.Add($"{name}: Occlusion Foot Pixels must be above 0 and at most Bevel Pixels ({bevelPixels}).");
            }

            var highest = Math.Max(Math.Max(ambient + diffuse, studTopTone), studWallTone);
            if (!(shadeHeadroom >= 1f && shadeHeadroom >= highest))
            {
                problems.Add($"{name}: Shade Headroom must be at least 1 and at least the highest shade, Ambient plus Diffuse or a stud's tone ({highest}), or it clips.");
            }

            if (!(floorShadowSpreadPixels > 0f))
            {
                problems.Add($"{name}: Floor Shadow Spread Pixels must be above 0.");
            }

            if (!(FloorShadowMarginPixels < QuarterPixels))
            {
                problems.Add($"{name}: Floor Shadow Offset Pixels and Floor Shadow Spread Pixels reach past a quarter beyond the cell ({FloorShadowMarginPixels} of {QuarterPixels}).");
            }
        }

        /// <summary>
        /// The constraints on the studs, each message naming its field. The
        /// grid sits on the face, the cell less half the gap on every side,
        /// and each stud reaches its radius past the farthest of its contact
        /// shadow, wall and top; a soft shadow counts to where it fades out.
        /// </summary>
        /// <param name="faceExtent">From the cell's centre to the body's edge, in pixels.</param>
        private void AddStudProblems(List<string> problems, float faceExtent)
        {
            if (studsPerCellSide < 1)
            {
                problems.Add($"{name}: Studs Per Cell Side must be at least 1.");
                return;
            }

            if (!(studDiameterPixels > 0f && studEdgePixels > 0f))
            {
                problems.Add($"{name}: Stud Diameter Pixels and Stud Edge Pixels must be above 0.");
            }

            if (!(studContactInnerPixels < studContactOuterPixels))
            {
                problems.Add($"{name}: Stud Contact Inner Pixels must be below Stud Contact Outer Pixels.");
            }

            var radius = studDiameterPixels / 2f;
            if (!(studRimFadePixels >= 0f && studRimFadePixels <= studRimFullPixels && studRimFullPixels < studRimInnerPixels
                  && studRimInnerPixels <= radius))
            {
                problems.Add($"{name}: Stud Rim Fade, Full and Inner Pixels must rise in that order from 0 to at most the stud's radius.");
            }

            if (!(studRimLightExponent > 0f && studRimLightDirection.sqrMagnitude > 0f))
            {
                problems.Add($"{name}: Stud Rim Light Exponent must be above 0 and Stud Rim Light Direction not zero.");
            }

            // Face shows between neighbours: each stud's wall and top stay
            // clear of the next stud's.
            var spacing = studSpacingPixels;
            var discReach = radius + studEdgePixels + Math.Max(studWallOffsetPixels.magnitude, studTopOffsetPixels.magnitude);
            if (studsPerCellSide > 1 && !(spacing > 2f * discReach))
            {
                problems.Add($"{name}: Stud Spacing Pixels ({spacing}) must keep two neighbouring studs' walls and tops apart ({2f * discReach}).");
            }

            var reach = (studsPerCellSide - 1) * spacing / 2f + StudReachPixels;
            if (!(reach <= faceExtent))
            {
                problems.Add($"{name}: Studs Per Cell Side, Stud Spacing Pixels, Stud Diameter Pixels and the studs' offsets put the studs outside the body ({reach} of {faceExtent} from the cell's centre).");
            }
        }

        /// <summary>
        /// How far a stud reaches from its grid point along either axis, in
        /// pixels: the farthest of its contact shadow, out to where it fades,
        /// its wall and its top, each with its offset.
        /// </summary>
        public float StudReachPixels
        {
            get
            {
                var radius = studDiameterPixels / 2f;
                var disc = radius + studEdgePixels;
                var contact = radius + studContactOuterPixels;
                return Math.Max(
                    contact + Math.Max(Math.Abs(studContactOffsetPixels.x), Math.Abs(studContactOffsetPixels.y)),
                    disc + Math.Max(
                        Math.Max(Math.Abs(studWallOffsetPixels.x), Math.Abs(studWallOffsetPixels.y)),
                        Math.Max(Math.Abs(studTopOffsetPixels.x), Math.Abs(studTopOffsetPixels.y))));
            }
        }

        /// <summary>The constraints on Module 19's sparkle, each message naming its field.</summary>
        private void AddIntroductionProblems(List<string> problems)
        {
            if (sparklePixels < 8)
            {
                problems.Add($"{name}: Sparkle Pixels must be at least 8.");
            }

            // The star's tips sit the anti-aliased edge inside the sprite; an
            // inner corner at half a tip's reach or beyond would make a
            // diamond or a square, not a star.
            var tip = sparklePixels / 2f - antiAliasPixels;
            if (!(sparkleWaist > 0f && sparkleWaist * sparklePixels < tip / 2f))
            {
                problems.Add($"{name}: Sparkle Waist must be above 0 and below a quarter, so the star's sides bend inward.");
            }
        }

        /// <summary>The constraints on Module 18's cube, shard and gate glow, each message naming its field.</summary>
        private void AddFeedbackProblems(List<string> problems)
        {
            var cubeHalf = cubePixels / 2f - antiAliasPixels;

            if (cubePixels < 8)
            {
                problems.Add($"{name}: Cube Pixels must be at least 8.");
            }

            if (!(cubeCornerPixels >= 0f && cubeCornerPixels < cubeHalf && cubeRimPixels >= 0f && cubeRimPixels < cubeHalf))
            {
                problems.Add($"{name}: Cube Corner Pixels and Cube Rim Pixels must be at least 0 and below half the cube.");
            }

            var highlightReach = Math.Max(Math.Abs(cubeHighlightOffsetPixels.x), Math.Abs(cubeHighlightOffsetPixels.y))
                                 + cubeHighlightDiameterPixels / 2f;
            if (!(cubeHighlightDiameterPixels > 0f && cubeHighlightSoftnessPixels >= 0f && highlightReach <= cubeHalf))
            {
                problems.Add($"{name}: Cube Highlight Diameter Pixels must be positive, Cube Highlight Softness Pixels at least 0, and the highlight at Cube Highlight Offset Pixels must stay inside the cube.");
            }

            if (shardPixels < 8)
            {
                problems.Add($"{name}: Shard Pixels must be at least 8.");
            }

            if (!IsConvexCounterClockwiseInUnitSquare(shardPoints))
            {
                problems.Add($"{name}: Shard Points must be four points from 0 to 1, counter-clockwise, forming a convex shape.");
            }

            if (!(shardStreakWidthPixels > 0f))
            {
                problems.Add($"{name}: Shard Streak Width Pixels must be positive.");
            }

            if (gateGlowPixels < 8)
            {
                problems.Add($"{name}: Gate Glow Pixels must be at least 8.");
            }

            if (!(gateGlowHold >= 0f && gateGlowHold < 1f))
            {
                problems.Add($"{name}: Gate Glow Hold must be at least 0 and below 1, so the glow fades.");
            }
        }

        /// <summary>
        /// True for exactly four points inside the unit square whose every turn
        /// is to the left: a convex shape listed counter-clockwise, as the
        /// shard's signed distance needs.
        /// </summary>
        private static bool IsConvexCounterClockwiseInUnitSquare(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count != 4)
            {
                return false;
            }

            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var c = points[(i + 2) % points.Count];
                if (!(a.x >= 0f && a.x <= 1f && a.y >= 0f && a.y <= 1f))
                {
                    return false;
                }

                var cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                if (!(cross > 0f))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The constraints on Module 17's icons, each message naming its field.</summary>
        private void AddIconProblems(List<string> problems)
        {
            if (clockPixels < 16 || restartPixels < 16)
            {
                problems.Add($"{name}: Clock Pixels and Restart Pixels must be at least 16.");
            }

            if (!(clockRimThickness > 0f && clockRimThickness < 0.5f))
            {
                problems.Add($"{name}: Clock Rim Thickness must be above 0 and below 0.5, so the rim leaves a face.");
            }

            // A hand's round end reaches half its thickness past its length.
            var handReach = Math.Max(clockMinuteHandLength, clockHourHandLength) + clockHandThickness / 2f;
            if (!(clockHandThickness > 0f && clockMinuteHandLength > 0f && clockHourHandLength > 0f
                  && handReach <= 0.5f - clockRimThickness))
            {
                problems.Add($"{name}: Clock Hand Thickness, Clock Minute Hand Length and Clock Hour Hand Length must be positive, and each hand with its round end must stay inside the rim.");
            }

            if (!(restartThickness > 0f && restartThickness < 2f * restartRadius && restartRadius + restartThickness / 2f < 0.5f))
            {
                problems.Add($"{name}: Restart Thickness must be positive and below twice Restart Radius, and Restart Radius plus half the thickness below 0.5, so the arc has a hole and stays in the sprite.");
            }

            if (!(restartHeadWidth > restartThickness && restartHeadWidth / 2f < restartRadius
                  && restartRadius + restartHeadWidth / 2f < 0.5f
                  && restartHeadLength > 0f && restartHeadLength <= restartRadius))
            {
                problems.Add($"{name}: Restart Head Width must be wider than Restart Thickness, below twice Restart Radius, and with Restart Radius plus half of it below 0.5, so the head stays in the sprite; Restart Head Length must be positive and at most Restart Radius.");
            }
        }

        /// <summary>The constraints on Module 16's sprites, each message naming its field.</summary>
        private void AddStateProblems(List<string> problems)
        {
            var quarter = cellPixels / 2f;

            if (frostStreakCount < 1 || !(frostStreakWidthPixels > 0f && frostStreakSpacingPixels > 0f && frostStreakLengthPixels > 0f))
            {
                problems.Add($"{name}: Frost Streak Count must be at least 1, and Frost Streak Width, Spacing and Length Pixels positive.");
            }

            if (!(frostInsetPixels >= MinFrostInsetPixels && frostInsetPixels < quarter))
            {
                problems.Add($"{name}: Frost Inset Pixels must be at least {MinFrostInsetPixels:0.##} (half the gap plus the corner's rounding) and below half a cell, or frost leaves the block's face.");
            }

            if (!(panelCornerRadiusPixels >= 0f && PanelBorderPixels * 2 < panelPixels))
            {
                problems.Add($"{name}: Panel Corner Radius Pixels must be at least 0, and the corner and bevel must leave a middle to stretch in Panel Pixels.");
            }

            if (!(ringWidthPixels >= 1 && ringWidthPixels * 2 < ringPixels))
            {
                problems.Add($"{name}: Ring Width Pixels must be at least 1 and leave a middle to stretch in Ring Pixels.");
            }

            if (!(chainPeriodPixels >= 8 && chainThicknessPixels >= 8
                  && chainLinkLengthPixels > 0f && chainLinkLengthPixels < chainPeriodPixels
                  && chainEdgeLinkLengthPixels > 0f && chainEdgeLinkLengthPixels < chainPeriodPixels
                  && chainLinkWallPixels > 0f && chainLinkWallPixels * 2f < chainThicknessPixels))
            {
                problems.Add($"{name}: Chain Link Length Pixels and Chain Edge Link Length Pixels must be positive and below Chain Period Pixels, and Chain Link Wall Pixels positive and below half of Chain Thickness Pixels.");
            }

            var shackleHalf = padlockShackleThickness / 2f;
            if (!(padlockPixels >= 16 && padlockBodyWidth > 0f && padlockBodyHeight > 0f && padlockShackleThickness > 0f
                  && padlockShackleRadius + shackleHalf <= padlockBodyWidth / 2f
                  && padlockBodyHeight + padlockShackleRadius + shackleHalf <= 1f
                  && padlockBodyCornerPixels >= 0f))
            {
                problems.Add($"{name}: Padlock Shackle Radius and Thickness must fit over the body's width, and the body and shackle together inside the sprite's height.");
            }

            if (!(keyGemDiameterPixels > 0f && keyGemDiameterPixels < keyHoleDiameterPixels
                  && keyHoleDiameterPixels < keyBowDiameterPixels && keyBowDiameterPixels <= keyHeightPixels
                  && keyShaftThicknessPixels > 0f && keyShaftThicknessPixels < keyBowDiameterPixels
                  && keyToothWidthPixels > 0f && keyToothLengthPixels > 0f
                  && keyShaftThicknessPixels / 2f + keyToothLengthPixels < keyHeightPixels / 2f
                  && keyBowDiameterPixels + 3f * keyToothWidthPixels < keyWidthPixels))
            {
                problems.Add($"{name}: the key must nest gem inside hole inside bow, fit the bow in Key Height Pixels, and fit the shaft and teeth in Key Width Pixels.");
            }

            if (!(slatsPerCell >= 1 && cellPixels % slatsPerCell == 0 && slatGapPixels >= 0f && slatGapPixels < (float)cellPixels / slatsPerCell))
            {
                problems.Add($"{name}: Slats Per Cell must be at least 1 and divide Cell Pixels, so tiles meet at a groove, and Slat Gap Pixels at least 0 and below a slat's height.");
            }

            if (!(doorLinesPerCell >= 1 && cellPixels % doorLinesPerCell == 0 && doorLineWidthPixels >= 0f && doorLineWidthPixels < (float)cellPixels / doorLinesPerCell))
            {
                problems.Add($"{name}: Door Lines Per Cell must be at least 1 and divide Cell Pixels, so tiles meet at a line, and Door Line Width Pixels at least 0 and below the space between lines.");
            }
        }
    }
}
