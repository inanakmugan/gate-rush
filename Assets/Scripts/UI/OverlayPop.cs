using DG.Tweening;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// The level panels' opening pop (Module 41): the whole overlay fades in
    /// while its panel grows to its size, on unscaled time, from
    /// <see cref="UiConfig"/>'s timing. The one path the result panel and the
    /// confirmation share; it keeps no state of its own.
    /// </summary>
    internal static class OverlayPop
    {
        /// <summary>
        /// Starts the pop of <paramref name="group"/> and
        /// <paramref name="panel"/>, its tweens carrying <paramref name="id"/>;
        /// a pop already running with that id is stopped first.
        /// </summary>
        public static void Play(object id, CanvasGroup group, RectTransform panel, UiConfig config)
        {
            DOTween.Kill(id);
            group.alpha = 0f;
            panel.localScale = Vector3.one * config.OverlayPopStartScale;

            DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 1f, config.OverlayPopSeconds)
                .SetEase(config.OverlayPopEase)
                .SetUpdate(true)
                .SetId(id);
            panel.DOScale(1f, config.OverlayPopSeconds)
                .SetEase(config.OverlayPopEase)
                .SetUpdate(true)
                .SetId(id);
        }

        /// <summary>
        /// Stops the pop carrying <paramref name="id"/> and leaves the overlay
        /// at its finished look. Either part may be missing during teardown.
        /// </summary>
        public static void Rest(object id, CanvasGroup group, RectTransform panel)
        {
            DOTween.Kill(id);
            if (group != null)
            {
                group.alpha = 1f;
            }

            if (panel != null)
            {
                panel.localScale = Vector3.one;
            }
        }
    }
}
