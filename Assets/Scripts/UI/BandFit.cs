using GateRush.Runtime;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// Fits a row laid out at a fixed size into the band its parent fills
    /// (Module 40): when the band is too small for <see cref="designSize"/>,
    /// the row is scaled down uniformly (<see cref="ScreenBands.ContentScale"/>)
    /// and stretched to the band at that scale, so its left and right
    /// elements still meet the band's edges and nothing reaches the board.
    /// Put it on the row, centred under the band.
    /// </summary>
    /// <remarks>
    /// The band's size in canvas units follows the screen and the canvas
    /// scaler, so it is compared every frame and the row refitted only when
    /// it changed.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BandFit : MonoBehaviour
    {
        [Tooltip("The size the row is laid out at, in canvas units; written by the UI builder.")]
        [SerializeField] private Vector2 designSize;

        private Vector2 fittedBandSize = Vector2.negativeInfinity;

        private void OnEnable()
        {
            fittedBandSize = Vector2.negativeInfinity;
            Fit();
        }

        private void LateUpdate()
        {
            Fit();
        }

        private void Fit()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null || !(designSize.x > 0f && designSize.y > 0f))
            {
                return;
            }

            var bandSize = parent.rect.size;
            if (bandSize == fittedBandSize)
            {
                return;
            }

            fittedBandSize = bandSize;
            var row = (RectTransform)transform;
            var scale = ScreenBands.ContentScale(bandSize, designSize);
            row.sizeDelta = scale > 0f ? bandSize / scale : designSize;
            row.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
