using DG.Tweening;
using TMPro;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// A gold pill a coin flight can land on (Module 47): its label, shown
    /// through a <see cref="GoldCounter"/> so it is written only when the
    /// number changes and can be held during a flight; its pop as a coin
    /// lands; and the pill's own canvas, which the flight lifts over a scrim.
    /// The one path the level HUD's pill and the Top Bar's gold share.
    /// </summary>
    /// <remarks>
    /// The pop runs on unscaled time with an id of its own, so its owner's
    /// other tweens never stop it and it stops none of theirs; its owner calls
    /// <see cref="Rest"/> on disable and <see cref="Kill"/> on destroy.
    /// </remarks>
    internal sealed class GoldPill
    {
        private readonly TMP_Text label;
        private readonly object popId = new object();
        private readonly TweenCallback<float> popStep;
        private readonly TweenCallback popRest;
        private UiConfig config;

        /// <summary>The pill of <paramref name="label"/>: the label's own canvas, or the label when it has none.</summary>
        public GoldPill(TMP_Text label)
        {
            this.label = label;
            Counter = new GoldCounter(Write, Pop);
            popStep = PopStep;
            popRest = Rest;
        }

        /// <summary>What a flight holds and adds to; what the view's refresh feeds.</summary>
        public GoldCounter Counter { get; }

        /// <summary>The pill's own canvas, which a flight lifts; the screen's canvas when the pill has none.</summary>
        public Canvas Canvas => label.canvas;

        /// <summary>Where coins land and what pops: the pill's canvas, or the label.</summary>
        public RectTransform Rect => Canvas != null && !Canvas.isRootCanvas ? (RectTransform)Canvas.transform : label.rectTransform;

        /// <summary>Shows the wallet's <paramref name="gold"/>, formatted with <paramref name="uiConfig"/>, unless a flight holds the pill.</summary>
        public void Show(int gold, UiConfig uiConfig)
        {
            config = uiConfig;
            Counter.Show(gold);
        }

        /// <summary>Stops a pop and leaves the pill at rest.</summary>
        public void Rest()
        {
            DOTween.Kill(popId);
            if (label != null)
            {
                Rect.localScale = Vector3.one;
            }
        }

        /// <summary>Stops a pop, for an owner being destroyed.</summary>
        public void Kill()
        {
            DOTween.Kill(popId);
        }

        private void Write(int gold)
        {
            if (config != null)
            {
                label.text = HomeTexts.Gold(gold, config);
            }
        }

        /// <summary>A coin landed: the pill grows and shrinks back once; a pop still playing starts over.</summary>
        private void Pop()
        {
            if (config == null)
            {
                return;
            }

            Rest();
            DOVirtual.Float(0f, 1f, config.GoldPopSeconds, popStep)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetId(popId)
                .OnComplete(popRest);
        }

        // Linear time: the sine is the pop's whole shape.
        private void PopStep(float t)
        {
            var scale = 1f + (config.GoldPopScale - 1f) * Mathf.Sin(Mathf.PI * t);
            Rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
