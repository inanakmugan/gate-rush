using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Fits its RectTransform to <see cref="Screen.safeArea"/>, so what it holds
    /// stays clear of notches, rounded corners and system bars. Refits when the
    /// screen's size or safe area changes.
    /// </summary>
    /// <remarks>
    /// Its parent must fill the screen — the rect of a screen-space canvas, or
    /// a child stretched over it — since the safe area is applied as anchors
    /// normalized to the whole screen (<see cref="ScreenBands.SafeAnchors"/>).
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private int fittedWidth;
        private int fittedHeight;
        private Rect fittedSafeArea;

        private void OnEnable()
        {
            // Whatever changed while disabled is picked up now.
            fittedWidth = 0;
            Fit();
        }

        private void Update()
        {
            Fit();
        }

        private void Fit()
        {
            var width = Screen.width;
            var height = Screen.height;
            var safeArea = Screen.safeArea;
            if (width == fittedWidth && height == fittedHeight && safeArea == fittedSafeArea)
            {
                return;
            }

            fittedWidth = width;
            fittedHeight = height;
            fittedSafeArea = safeArea;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var anchors = ScreenBands.SafeAnchors(safeArea, width, height);
            UiBuilder.SetAnchors((RectTransform)transform, anchors.min, anchors.max);
        }
    }
}
