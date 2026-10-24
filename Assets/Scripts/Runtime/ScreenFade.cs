using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// The fade between the front end and a level (Module 47): one full-screen
    /// image in the level's background colour, on a small canvas of its own
    /// that sorts above every other, so fading it rebuilds only that canvas.
    /// The screen fades to the colour, the switch happens at the midpoint, and
    /// the fade lifts. Built in code by <see cref="LevelBootstrap"/> under the
    /// screen's canvas, so no scene or prefab holds it.
    /// </summary>
    /// <remarks>
    /// <para><b>Input.</b> While busy the image is a raycast target over
    /// everything, so no button, overlay or card below takes a press; the
    /// bootstrap holds the board and the keys through <see cref="IsBusy"/>.
    /// While idle the image is disabled: nothing is drawn, so the fade costs no
    /// full-screen overdraw on a phone.</para>
    /// <para><b>Order.</b> The order of the two actions is
    /// <see cref="ScreenFadeSequence"/>'s. The fade lifts one frame after the
    /// midpoint, so the frame that builds the new screen is not counted into
    /// the lift and it does not jump; it lifts even when the midpoint
    /// throws.</para>
    /// <para>Its tweens run on unscaled time and carry this component as their
    /// id. Disabling or destroying it abandons a fade in progress without
    /// running what is left: only the scene's teardown does either.</para>
    /// </remarks>
    public sealed class ScreenFade : MonoBehaviour
    {
        // The fade's own alphas: clear and covering. Not tunables.
        private const float ClearAlpha = 0f;
        private const float CoverAlpha = 1f;

        private readonly ScreenFadeSequence sequence = new ScreenFadeSequence();
        private Image image;
        private ScreenFadeSettings settings;
        private Color color;

        /// <summary>True from <see cref="Play"/> until the fade has lifted: input to everything below is refused.</summary>
        public bool IsBusy => sequence.IsBusy;

        /// <summary>The fade's canvas, sorting above <paramref name="screenCanvas"/>'s.</summary>
        public Canvas Canvas { get; private set; }

        /// <summary>
        /// Builds the fade as the last child of <paramref name="screenCanvas"/>:
        /// its own canvas, sorting <see cref="ScreenFadeSettings.SortingOffset"/>
        /// above the screen's root canvas, a raycaster, and the full-screen
        /// image in <paramref name="fadeColor"/>, opaque, disabled until a fade
        /// plays.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="screenCanvas"/> is null.</exception>
        public static ScreenFade Create(Canvas screenCanvas, Color fadeColor, ScreenFadeSettings fadeSettings)
        {
            if (screenCanvas == null)
            {
                throw new ArgumentNullException(nameof(screenCanvas));
            }

            var rect = UiBuilder.CreateRect(nameof(ScreenFade), screenCanvas.transform);
            UiBuilder.Stretch(rect);
            rect.SetAsLastSibling();

            // Set once it is under the screen's canvas, where a nested canvas's
            // override takes effect; nothing of it is saved.
            var canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = screenCanvas.rootCanvas.sortingOrder + fadeSettings.SortingOffset;
            rect.gameObject.AddComponent<GraphicRaycaster>();

            var fadeImage = rect.gameObject.AddComponent<Image>();
            fadeColor.a = CoverAlpha;
            fadeImage.color = fadeColor;
            fadeImage.raycastTarget = false;
            fadeImage.enabled = false;

            var fade = rect.gameObject.AddComponent<ScreenFade>();
            fade.image = fadeImage;
            fade.color = fadeColor;
            fade.settings = fadeSettings;
            fade.Canvas = canvas;
            return fade;
        }

        /// <summary>
        /// Fades the screen to the colour, runs <paramref name="atMidpoint"/>
        /// once while it is covered, lifts the fade, then runs
        /// <paramref name="onDone"/>, which may be null. False, with nothing
        /// done, while a fade is already playing.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="atMidpoint"/> is null.</exception>
        public bool Play(Action atMidpoint, Action onDone)
        {
            if (!sequence.TryStart(atMidpoint, onDone))
            {
                return false;
            }

            DOTween.Kill(this);
            image.raycastTarget = true;
            image.enabled = true;
            SetAlpha(ClearAlpha);
            DOVirtual.Float(ClearAlpha, CoverAlpha, settings.InSeconds, SetAlpha)
                .SetEase(settings.Ease)
                .SetUpdate(true)
                .SetId(this)
                .OnComplete(OnCovered);
            return true;
        }

        private void OnCovered()
        {
            SetAlpha(CoverAlpha);
            try
            {
                sequence.Covered();
            }
            finally
            {
                if (isActiveAndEnabled)
                {
                    StartCoroutine(LiftNextFrame());
                }
            }
        }

        private IEnumerator LiftNextFrame()
        {
            yield return null;
            DOVirtual.Float(CoverAlpha, ClearAlpha, settings.OutSeconds, SetAlpha)
                .SetEase(settings.Ease)
                .SetUpdate(true)
                .SetId(this)
                .OnComplete(OnLifted);
        }

        private void OnLifted()
        {
            Clear();
            sequence.Revealed();
        }

        private void SetAlpha(float alpha)
        {
            var shown = color;
            shown.a = alpha;
            image.color = shown;
        }

        /// <summary>No fade: the image disabled and no raycast target.</summary>
        private void Clear()
        {
            if (image == null)
            {
                return;
            }

            image.raycastTarget = false;
            image.enabled = false;
        }

        // Nothing but the scene's teardown disables or destroys the fade, so
        // both abandon a fade in progress rather than run it into views that
        // are going away.
        private void OnDisable()
        {
            Abandon();
        }

        private void OnDestroy()
        {
            Abandon();
        }

        private void Abandon()
        {
            DOTween.Kill(this);
            StopAllCoroutines();
            sequence.Abandon();
            Clear();
        }
    }
}
