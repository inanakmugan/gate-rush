using System;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where the HUD and the board go on a screen with a safe area: the one
    /// rule shared by the camera fit, the HUD band and <see cref="SafeArea"/>.
    /// Anchor rects are normalized to the whole screen — (0, 0) bottom left,
    /// (1, 1) top right — so they apply directly to a rect that fills a
    /// screen-space canvas.
    /// </summary>
    /// <remarks>
    /// The bands of <c>RuntimeConfig</c> are fractions of the screen's height
    /// measured from the safe area's edges, not the screen's: the camera keeps
    /// free the unsafe strip plus the band at the top and at the bottom, and
    /// the HUD band starts at the safe area's top. Where the safe area is the
    /// whole screen, that is the band alone.
    /// </remarks>
    public static class ScreenBands
    {
        /// <summary>
        /// The safe area as anchors, clamped to the screen.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A screen dimension is not positive.</exception>
        public static Rect SafeAnchors(Rect safeArea, int screenWidth, int screenHeight)
        {
            RequirePositive(screenWidth, nameof(screenWidth));
            RequirePositive(screenHeight, nameof(screenHeight));

            return Rect.MinMaxRect(
                Clamp01(safeArea.xMin / screenWidth),
                Clamp01(safeArea.yMin / screenHeight),
                Clamp01(safeArea.xMax / screenWidth),
                Clamp01(safeArea.yMax / screenHeight));
        }

        /// <summary>
        /// The HUD's band as anchors: as wide as the safe area, from its top
        /// down by <paramref name="topBandScreenFraction"/> of the screen's
        /// height — the strip the camera keeps free above the board when
        /// <see cref="TryAddInsets"/> succeeds. Never reaches below the screen.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A screen dimension is not positive.</exception>
        public static Rect HudBandAnchors(Rect safeArea, int screenWidth, int screenHeight, float topBandScreenFraction)
        {
            var safe = SafeAnchors(safeArea, screenWidth, screenHeight);
            return Rect.MinMaxRect(safe.xMin, Math.Max(safe.yMax - topBandScreenFraction, 0f), safe.xMax, safe.yMax);
        }

        /// <summary>
        /// The top and bottom bands the camera must keep free: each band plus
        /// the unsafe strip on its side, as fractions of the screen's height.
        /// False when together they would leave no room for the board (1 or
        /// more); <paramref name="top"/> and <paramref name="bottom"/> are then
        /// the bands without the insets.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="screenHeight"/> is not positive.</exception>
        public static bool TryAddInsets(
            float topBandScreenFraction, float bottomBandScreenFraction, Rect safeArea, int screenHeight,
            out float top, out float bottom)
        {
            RequirePositive(screenHeight, nameof(screenHeight));

            var withTop = topBandScreenFraction + (1f - Clamp01(safeArea.yMax / screenHeight));
            var withBottom = bottomBandScreenFraction + Clamp01(safeArea.yMin / screenHeight);
            if (withTop + withBottom < 1f)
            {
                top = withTop;
                bottom = withBottom;
                return true;
            }

            top = topBandScreenFraction;
            bottom = bottomBandScreenFraction;
            return false;
        }

        /// <summary>
        /// The uniform scale at which content of <paramref name="needed"/> size
        /// fits in <paramref name="available"/>: 1 when it already fits, never
        /// above 1, and 0 when there is no room at all.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="needed"/> is not positive on both axes.</exception>
        public static float ContentScale(Vector2 available, Vector2 needed)
        {
            if (!(needed.x > 0f && needed.y > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(needed), needed, "The content's size must be positive.");
            }

            if (!(available.x > 0f && available.y > 0f))
            {
                return 0f;
            }

            return Math.Min(1f, Math.Min(available.x / needed.x, available.y / needed.y));
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static void RequirePositive(int value, string parameter)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(parameter, value, "The screen dimension must be positive.");
            }
        }
    }
}
