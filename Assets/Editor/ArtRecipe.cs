using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// Every parameter of the generated board and block art (D46, Module 15):
    /// sizes, radii, stud size and spacing, tones, arrow shapes. The look is
    /// tuned here, never in code; <see cref="ArtGenerator"/> reads nothing
    /// else. All sizes are in pixels of the generated textures, and tones are
    /// greyscale values from 0 (black) to 1 (white) that the runtime tints.
    /// </summary>
    /// <remarks>
    /// <para>The type lives in an editor-only assembly and nothing at runtime
    /// references the asset, so it never enters a build; only the PNGs it
    /// produces do.</para>
    /// <para><b>Seams.</b> Four quarter pieces meet at every seam of a block,
    /// the frame and a gate, each drawn without knowing its neighbours. They
    /// meet cleanly only while the rounding stays inside the piece that owns
    /// it, which is what <see cref="Problems"/> checks: the corner radius and
    /// the bevel must fit inside the face, and an inside corner's radius
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

        [Header("Face (blocks, frame, gates)")]
        [Tooltip("Gap between two neighbouring blocks, in pixels: half of it is inset on each block's outline.")]
        [SerializeField] private float blockGapPixels = 10f;

        [Tooltip("Radius of an outer corner, in pixels. At most half a cell less half the gap, less half a pixel.")]
        [SerializeField] private float cornerRadiusPixels = 24f;

        [Tooltip("Radius of an inside (concave) corner, in pixels. At most half the gap less half a pixel, or the pieces stop meeting at their seams.")]
        [SerializeField] private float concaveRadiusPixels = 4f;

        [Tooltip("Width of the darker line along the outline, in pixels.")]
        [SerializeField] private float outlineWidthPixels = 2f;

        [SerializeField, Range(0f, 1f)] private float outlineTone = 0.55f;

        [Tooltip("Width of the highlight band just inside the outline, in pixels. It fades from Rim Tone to Face Tone.")]
        [SerializeField] private float rimWidthPixels = 10f;

        [SerializeField, Range(0f, 1f)] private float rimTone = 1f;

        [Tooltip("Tone of the face. Below 1 so the rim can read lighter: a runtime tint can only darken.")]
        [SerializeField, Range(0f, 1f)] private float faceTone = 0.84f;

        [Header("Studs")]
        [SerializeField] private float studDiameterPixels = 36f;

        [Tooltip("Distance between the centres of neighbouring studs, in pixels; a cell carries a 2×2 grid of them.")]
        [SerializeField] private float studSpacingPixels = 64f;

        [SerializeField, Range(0f, 1f)] private float studTone = 0.92f;

        [Tooltip("Width of the darker ring at each stud's edge, in pixels.")]
        [SerializeField] private float studRingWidthPixels = 4f;

        [SerializeField, Range(0f, 1f)] private float studRingTone = 0.66f;

        [Header("Stud gloss (never tinted by the block)")]
        [SerializeField] private float glossDiameterPixels = 12f;

        [Tooltip("Where the highlight sits relative to its stud's centre, in pixels.")]
        [SerializeField] private Vector2 glossOffsetPixels = new Vector2(-7f, 7f);

        [Tooltip("How far the highlight's edge fades out, in pixels.")]
        [SerializeField] private float glossSoftnessPixels = 3f;

        [SerializeField, Range(0f, 1f)] private float glossAlpha = 0.85f;

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

        /// <summary>Width of the outline, in pixels.</summary>
        public float OutlineWidthPixels => outlineWidthPixels;

        /// <summary>Tone of the outline.</summary>
        public float OutlineTone => outlineTone;

        /// <summary>Width of the rim highlight, in pixels.</summary>
        public float RimWidthPixels => rimWidthPixels;

        /// <summary>Tone at the outer edge of the rim highlight.</summary>
        public float RimTone => rimTone;

        /// <summary>Tone of the face inside the rim.</summary>
        public float FaceTone => faceTone;

        /// <summary>Stud diameter, in pixels.</summary>
        public float StudDiameterPixels => studDiameterPixels;

        /// <summary>Distance between neighbouring stud centres, in pixels.</summary>
        public float StudSpacingPixels => studSpacingPixels;

        /// <summary>Tone of a stud.</summary>
        public float StudTone => studTone;

        /// <summary>Width of a stud's edge ring, in pixels.</summary>
        public float StudRingWidthPixels => studRingWidthPixels;

        /// <summary>Tone of a stud's edge ring.</summary>
        public float StudRingTone => studRingTone;

        /// <summary>Diameter of a stud's highlight, in pixels.</summary>
        public float GlossDiameterPixels => glossDiameterPixels;

        /// <summary>Offset of a highlight from its stud's centre, in pixels.</summary>
        public Vector2 GlossOffsetPixels => glossOffsetPixels;

        /// <summary>How far a highlight's edge fades, in pixels.</summary>
        public float GlossSoftnessPixels => glossSoftnessPixels;

        /// <summary>Opacity of a highlight.</summary>
        public float GlossAlpha => glossAlpha;

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

            if (!(outlineWidthPixels >= 0f && rimWidthPixels >= 0f && outlineWidthPixels + rimWidthPixels <= faceExtent))
            {
                problems.Add($"{name}: Outline Width Pixels and Rim Width Pixels may not be negative, and together must fit inside the face ({faceExtent}).");
            }

            if (!(studDiameterPixels > 0f && studDiameterPixels < studSpacingPixels))
            {
                problems.Add($"{name}: Stud Diameter Pixels must be positive and below Stud Spacing Pixels.");
            }

            if (!((studSpacingPixels + studDiameterPixels) / 2f <= faceExtent))
            {
                problems.Add($"{name}: Stud Spacing Pixels and Stud Diameter Pixels put the studs outside the face.");
            }

            if (!(studRingWidthPixels >= 0f && studRingWidthPixels * 2f <= studDiameterPixels))
            {
                problems.Add($"{name}: Stud Ring Width Pixels must be at least 0 and at most half the stud's diameter.");
            }

            if (!(glossDiameterPixels > 0f && glossSoftnessPixels >= 0f))
            {
                problems.Add($"{name}: Gloss Diameter Pixels must be positive and Gloss Softness Pixels at least 0.");
            }

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

            return problems;
        }
    }
}
