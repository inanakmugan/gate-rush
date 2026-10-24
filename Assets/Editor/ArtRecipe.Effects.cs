using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// One ice shard's outline (Module 47): a convex polygon's corners as
    /// fractions of the shard's canvas, x right and y up, listed in either
    /// direction. The ridge runs from the first corner to the third, and the
    /// frost streak crosses the second.
    /// </summary>
    [Serializable]
    public struct ShardOutline
    {
        [SerializeField] private Vector2[] points;

        /// <summary>An outline.</summary>
        public ShardOutline(params Vector2[] points)
        {
            this.points = points;
        }

        /// <summary>The corners, as fractions of the canvas, x right and y up.</summary>
        public IReadOnlyList<Vector2> Points => points ?? Array.Empty<Vector2>();
    }

    /// <summary>
    /// The board's debris of Module 47, pass 1, as the approved reference
    /// script <c>effects_reference.py</c> draws it: every number below is the
    /// script's, named, so the look is tuned here (D46, D80). The cube and the
    /// shard are painted at the script's own canvas size, in pixels of a
    /// 128-pixel cell, and scaled down to their sprite's size as the script
    /// scales them; the gate glow and the sparkle are painted at their size.
    /// </summary>
    public sealed partial class ArtRecipe
    {
        // ----- Cube: a broken-off quarter of a block, with one stud -----

        [Header("Cube (Module 47: a broken-off quarter of a block, tinted the block's colour)")]
        [Tooltip("Side of the cube sprite, in pixels. At least 8 and at most Cube Paint Pixels.")]
        [SerializeField] private int cubePixels = 48;

        [Tooltip("Side of the canvas the cube is painted on before it is scaled down, in pixels of a 128-pixel cell: a quarter of a block. At least 8.")]
        [SerializeField] private int cubePaintPixels = 64;

        [Tooltip("How far the cube's edge sits in from its canvas, in paint pixels. At least 0.")]
        [SerializeField] private float cubeInsetPixels = 2f;

        [Tooltip("The cube's corner radius, in paint pixels. At least 0 and at most its half side.")]
        [SerializeField] private float cubeCornerRadiusPixels = 16f;

        [Tooltip("How far in from the edge the cube rises along a quarter circle, in paint pixels. Above 0.")]
        [SerializeField] private float cubeBevelPixels = 16f;

        [Tooltip("How high the cube stands, in paint pixels. Above 0.")]
        [SerializeField] private float cubeHeightPixels = 11f;

        // ----- Shards: faceted, glassy ice, colour baked in -----

        [Header("Ice shards (Module 47: glassy ice, painted in Runtime Config's Shard Color)")]
        [Tooltip("Side of a shard sprite, in pixels. At least 8 and at most Shard Paint Pixels.")]
        [SerializeField] private int shardPixels = 48;

        [Tooltip("Side of the canvas a shard is painted on before it is scaled down, in pixels. At least 8.")]
        [SerializeField] private int shardPaintPixels = 96;

        [Tooltip("The three shards' outlines, one per sprite (Shard, Shard2, Shard3): convex, five corners each, as fractions of the canvas, x right and y up. The ridge runs from the first corner to the third; the frost streak crosses the second.")]
        [SerializeField] private ShardOutline[] shardOutlines =
        {
            new ShardOutline(new Vector2(0.15f, 0.60f), new Vector2(0.45f, 0.92f), new Vector2(0.88f, 0.70f), new Vector2(0.80f, 0.22f), new Vector2(0.35f, 0.10f)),
            new ShardOutline(new Vector2(0.10f, 0.80f), new Vector2(0.70f, 0.90f), new Vector2(0.92f, 0.45f), new Vector2(0.55f, 0.08f), new Vector2(0.18f, 0.30f)),
            new ShardOutline(new Vector2(0.30f, 0.94f), new Vector2(0.86f, 0.78f), new Vector2(0.78f, 0.14f), new Vector2(0.22f, 0.06f), new Vector2(0.08f, 0.50f))
        };

        [Tooltip("How far in from the edge a shard rises along a quarter circle, in paint pixels. Above 0.")]
        [SerializeField] private float shardBevelPixels = 10f;

        [Tooltip("How high a shard's bevel stands, in paint pixels. Above 0.")]
        [SerializeField] private float shardHeightPixels = 12f;

        [Tooltip("Half the width of the ridge along a shard's long diagonal, in paint pixels: the ridge falls to nothing this far from the diagonal. Above 0.")]
        [SerializeField] private float shardRidgeHalfWidthPixels = 30f;

        [Tooltip("How high the ridge stands on the diagonal, in paint pixels. At least 0.")]
        [SerializeField] private float shardRidgeHeightPixels = 14f;

        [Tooltip("How far in from the edge the ridge reaches its full height, in paint pixels. Above 0.")]
        [SerializeField] private float shardRidgeRisePixels = 10f;

        [Tooltip("A shard's glassy highlight.")]
        [SerializeField] private LitFinish shardFinish = new LitFinish(50f, 0.9f);

        [Tooltip("A shard's shade where it faces away from the light: brighter than a block's, as glass is. At least 0.")]
        [SerializeField] private float shardAmbient = 0.75f;

        [Tooltip("What a shard's shade gains facing the light. At least 0.")]
        [SerializeField] private float shardDiffuse = 0.45f;

        [Tooltip("How far inside the edge the bright rim's middle runs, in paint pixels. At least 0.")]
        [SerializeField] private float shardRimOffsetPixels = 1.5f;

        [Tooltip("Half the bright rim's width, in paint pixels: it fades to nothing this far from its middle. Above 0.")]
        [SerializeField] private float shardRimHalfWidthPixels = 2f;

        [Tooltip("How far the rim lifts the shard's colour toward white, from 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float shardRimStrength = 0.7f;

        [Tooltip("The frost streak's distance is measured along this direction from the outline's second corner, x right and y up: the streak runs across it. Not zero.")]
        [SerializeField] private Vector2 shardStreakAcross = new Vector2(0.7f, 0.7f);

        [Tooltip("The streak's width, in paint pixels, as a Gaussian's scale. Above 0.")]
        [SerializeField] private float shardStreakWidthPixels = 4f;

        [Tooltip("How far in from the edge the streak reaches full strength, in paint pixels. Above 0.")]
        [SerializeField] private float shardStreakRisePixels = 8f;

        [Tooltip("How far the streak lifts the shard's colour toward white, from 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float shardStreakStrength = 0.55f;

        [Tooltip("A shard's opacity where neither the rim, the streak nor the highlight lies, from 0 to 1; those lift it to 1.")]
        [SerializeField, Range(0f, 1f)] private float shardBaseAlpha = 0.85f;

        [Tooltip("How much the highlight counts toward a shard's opacity, beside the rim and the streak. At least 0.")]
        [SerializeField] private float shardHighlightOpacity = 2f;

        // ----- Gate glow: 2D, white, drawn additive -----

        [Header("Gate glow (Module 47: white, bright at the gate's middle, tinted and added)")]
        [Tooltip("Side of the gate glow sprite, in pixels: across the gate and into the board. At least 8.")]
        [SerializeField] private int gateGlowPixels = 64;

        [Tooltip("The depth into the board at which the glow has fallen to half, as a share of the sprite. Above 0.")]
        [SerializeField] private float gateGlowHalfDepth = 0.28f;

        [Tooltip("The depth, as a share of the sprite, from which the glow eases to nothing at the far edge: above 0 and below 1.")]
        [SerializeField] private float gateGlowFadeFrom = 0.85f;

        // ----- Sparkle: thin tapering four-point star, halo, core -----

        [Header("Sparkle (Module 47: white, untinted)")]
        [Tooltip("Side of the sparkle sprite, in pixels. At least 8.")]
        [SerializeField] private int sparklePixels = 64;

        [Tooltip("An arm's width at the centre, as a share of the sprite's half side: it tapers to nothing at the tip. Above 0, at most 0.5.")]
        [SerializeField] private float sparkleArmWidth = 0.06f;

        [Tooltip("How the arm's width falls toward its tip: the power of the share left. Above 0.")]
        [SerializeField] private float sparkleArmTaper = 1.2f;

        [Tooltip("The soft halo's spread, as a share of the sprite's half side: a Gaussian's standard deviation. Above 0.")]
        [SerializeField] private float sparkleHaloSigma = 0.32f;

        [Tooltip("The halo's opacity at the centre, from 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float sparkleHaloStrength = 0.45f;

        [Tooltip("The bright core's radius, as a share of the sprite's half side. Above 0.")]
        [SerializeField] private float sparkleCoreRadius = 0.07f;

        /// <summary>Side of the cube sprite, in pixels.</summary>
        public int CubePixels => cubePixels;

        /// <summary>Side of the canvas the cube is painted on, in pixels of a 128-pixel cell.</summary>
        public int CubePaintPixels => cubePaintPixels;

        /// <summary>How far the cube's edge sits in from its canvas, in paint pixels.</summary>
        public float CubeInsetPixels => cubeInsetPixels;

        /// <summary>The cube's corner radius, in paint pixels.</summary>
        public float CubeCornerRadiusPixels => cubeCornerRadiusPixels;

        /// <summary>How far in from the edge the cube rises, in paint pixels.</summary>
        public float CubeBevelPixels => cubeBevelPixels;

        /// <summary>How high the cube stands, in paint pixels.</summary>
        public float CubeHeightPixels => cubeHeightPixels;

        /// <summary>Side of a shard sprite, in pixels.</summary>
        public int ShardPixels => shardPixels;

        /// <summary>Side of the canvas a shard is painted on, in pixels.</summary>
        public int ShardPaintPixels => shardPaintPixels;

        /// <summary>The three shards' outlines, one per sprite.</summary>
        public IReadOnlyList<ShardOutline> ShardOutlines => shardOutlines ?? Array.Empty<ShardOutline>();

        /// <summary>How far in from the edge a shard rises, in paint pixels.</summary>
        public float ShardBevelPixels => shardBevelPixels;

        /// <summary>How high a shard's bevel stands, in paint pixels.</summary>
        public float ShardHeightPixels => shardHeightPixels;

        /// <summary>Half the ridge's width, in paint pixels.</summary>
        public float ShardRidgeHalfWidthPixels => shardRidgeHalfWidthPixels;

        /// <summary>How high the ridge stands, in paint pixels.</summary>
        public float ShardRidgeHeightPixels => shardRidgeHeightPixels;

        /// <summary>How far in from the edge the ridge reaches its full height, in paint pixels.</summary>
        public float ShardRidgeRisePixels => shardRidgeRisePixels;

        /// <summary>A shard's glassy highlight.</summary>
        public LitFinish ShardFinish => shardFinish;

        /// <summary>A shard's shade facing away from the light.</summary>
        public float ShardAmbient => shardAmbient;

        /// <summary>What a shard's shade gains facing the light.</summary>
        public float ShardDiffuse => shardDiffuse;

        /// <summary>How far inside the edge the rim's middle runs, in paint pixels.</summary>
        public float ShardRimOffsetPixels => shardRimOffsetPixels;

        /// <summary>Half the rim's width, in paint pixels.</summary>
        public float ShardRimHalfWidthPixels => shardRimHalfWidthPixels;

        /// <summary>How far the rim lifts the colour toward white.</summary>
        public float ShardRimStrength => shardRimStrength;

        /// <summary>The direction the streak's distance is measured along, x right and y up.</summary>
        public Vector2 ShardStreakAcross => shardStreakAcross;

        /// <summary>The streak's width, in paint pixels.</summary>
        public float ShardStreakWidthPixels => shardStreakWidthPixels;

        /// <summary>How far in from the edge the streak reaches full strength, in paint pixels.</summary>
        public float ShardStreakRisePixels => shardStreakRisePixels;

        /// <summary>How far the streak lifts the colour toward white.</summary>
        public float ShardStreakStrength => shardStreakStrength;

        /// <summary>A shard's opacity away from its rim, streak and highlight.</summary>
        public float ShardBaseAlpha => shardBaseAlpha;

        /// <summary>How much the highlight counts toward a shard's opacity.</summary>
        public float ShardHighlightOpacity => shardHighlightOpacity;

        /// <summary>Side of the gate glow sprite, in pixels.</summary>
        public int GateGlowPixels => gateGlowPixels;

        /// <summary>The depth at which the glow has fallen to half, as a share of the sprite.</summary>
        public float GateGlowHalfDepth => gateGlowHalfDepth;

        /// <summary>The depth from which the glow eases to nothing, as a share of the sprite.</summary>
        public float GateGlowFadeFrom => gateGlowFadeFrom;

        /// <summary>Side of the sparkle sprite, in pixels.</summary>
        public int SparklePixels => sparklePixels;

        /// <summary>An arm's width at the centre, as a share of the half side.</summary>
        public float SparkleArmWidth => sparkleArmWidth;

        /// <summary>How the arm's width falls toward its tip.</summary>
        public float SparkleArmTaper => sparkleArmTaper;

        /// <summary>The halo's spread, as a share of the half side.</summary>
        public float SparkleHaloSigma => sparkleHaloSigma;

        /// <summary>The halo's opacity at the centre.</summary>
        public float SparkleHaloStrength => sparkleHaloStrength;

        /// <summary>The core's radius, as a share of the half side.</summary>
        public float SparkleCoreRadius => sparkleCoreRadius;

        /// <summary>The constraints on Module 47's debris, each message naming its field.</summary>
        private void AddEffectProblems(List<string> problems)
        {
            if (!(cubePaintPixels >= 8 && cubePixels >= 8 && cubePixels <= cubePaintPixels))
            {
                problems.Add($"{name}: Cube Paint Pixels and Cube Pixels must be at least 8, and Cube Pixels at most Cube Paint Pixels.");
            }

            var cubeHalf = cubePaintPixels / 2f - cubeInsetPixels;
            if (!(cubeInsetPixels >= 0f && cubeHalf > 0f && cubeCornerRadiusPixels >= 0f && cubeCornerRadiusPixels <= cubeHalf))
            {
                problems.Add($"{name}: Cube Inset Pixels must be at least 0 and leave a cube, and Cube Corner Radius Pixels from 0 to its half side ({cubeHalf}).");
            }

            if (!(cubeBevelPixels > 0f && cubeHeightPixels > 0f))
            {
                problems.Add($"{name}: Cube Bevel Pixels and Cube Height Pixels must be above 0.");
            }

            // The cube carries one of the block's studs at its centre, painted
            // at the block's own scale, so the whole stud must fit on it.
            if (!(StudReachPixels <= cubeHalf))
            {
                problems.Add($"{name}: the stud reaches {StudReachPixels} pixels from its centre, past the cube's half side ({cubeHalf}); raise Cube Paint Pixels or lower Cube Inset Pixels.");
            }

            AddShardProblems(problems);

            if (!(gateGlowPixels >= 8 && gateGlowHalfDepth > 0f && gateGlowFadeFrom > 0f && gateGlowFadeFrom < 1f))
            {
                problems.Add($"{name}: Gate Glow Pixels must be at least 8, Gate Glow Half Depth above 0, and Gate Glow Fade From above 0 and below 1.");
            }

            if (!(sparklePixels >= 8 && sparkleArmWidth > 0f && sparkleArmWidth <= 0.5f && sparkleArmTaper > 0f
                  && sparkleHaloSigma > 0f && sparkleCoreRadius > 0f))
            {
                problems.Add($"{name}: Sparkle Pixels must be at least 8, Sparkle Arm Width above 0 and at most 0.5, and Sparkle Arm Taper, Sparkle Halo Sigma and Sparkle Core Radius above 0.");
            }
        }

        private void AddShardProblems(List<string> problems)
        {
            if (!(shardPaintPixels >= 8 && shardPixels >= 8 && shardPixels <= shardPaintPixels))
            {
                problems.Add($"{name}: Shard Paint Pixels and Shard Pixels must be at least 8, and Shard Pixels at most Shard Paint Pixels.");
            }

            var outlines = ShardOutlines;
            if (outlines.Count != ShardSpriteCount)
            {
                problems.Add($"{name}: Shard Outlines must hold {ShardSpriteCount} outlines, one per shard sprite; it holds {outlines.Count}.");
            }

            for (var i = 0; i < outlines.Count; i++)
            {
                if (!IsConvexInUnitSquare(outlines[i].Points))
                {
                    problems.Add($"{name}: Shard Outlines element {i} must be {ShardCorners} corners from 0 to 1 forming a convex shape.");
                }
            }

            if (!(shardBevelPixels > 0f && shardHeightPixels > 0f && shardRidgeHalfWidthPixels > 0f && shardRidgeHeightPixels >= 0f
                  && shardRidgeRisePixels > 0f))
            {
                problems.Add($"{name}: Shard Bevel Pixels, Shard Height Pixels, Shard Ridge Half Width Pixels and Shard Ridge Rise Pixels must be above 0, and Shard Ridge Height Pixels at least 0.");
            }

            if (!(shardFinish.Shininess >= 1f && shardAmbient >= 0f && shardDiffuse >= 0f))
            {
                problems.Add($"{name}: Shard Finish's shininess must be at least 1, and Shard Ambient and Shard Diffuse at least 0.");
            }

            if (!(shardRimOffsetPixels >= 0f && shardRimHalfWidthPixels > 0f))
            {
                problems.Add($"{name}: Shard Rim Offset Pixels must be at least 0 and Shard Rim Half Width Pixels above 0.");
            }

            if (!(shardStreakAcross.sqrMagnitude > 0f && shardStreakWidthPixels > 0f && shardStreakRisePixels > 0f))
            {
                problems.Add($"{name}: Shard Streak Across must not be zero, and Shard Streak Width Pixels and Shard Streak Rise Pixels must be above 0.");
            }

            if (!(shardHighlightOpacity >= 0f))
            {
                problems.Add($"{name}: Shard Highlight Opacity must be at least 0.");
            }
        }

        /// <summary>How many shard sprites there are, one per outline: Shard, Shard2, Shard3.</summary>
        public const int ShardSpriteCount = 3;

        /// <summary>How many corners a shard's outline has.</summary>
        public const int ShardCorners = 5;

        /// <summary>
        /// True for exactly <see cref="ShardCorners"/> points inside the unit
        /// square whose every turn goes the same way: a convex shape, listed in
        /// either direction.
        /// </summary>
        private static bool IsConvexInUnitSquare(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count != ShardCorners)
            {
                return false;
            }

            var sign = 0;
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
                var turn = cross > 0f ? 1 : cross < 0f ? -1 : 0;
                if (turn == 0 || (sign != 0 && turn != sign))
                {
                    return false;
                }

                sign = turn;
            }

            return true;
        }
    }
}
