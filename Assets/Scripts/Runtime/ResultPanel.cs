using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// The end-of-level panel: a full-screen backdrop and a rounded panel with
    /// a title, Restart and, after a win, Next. Builds its hierarchy from code
    /// and opens with a short fade-and-scale pop. Holds no rules — the
    /// bootstrap decides what to show and handles what the buttons ask for.
    /// </summary>
    /// <remarks>
    /// <para>Put this component on the screen-space canvas, which stays
    /// active; it builds one <c>Result</c> child under its own transform, last
    /// among the canvas's children so it draws over the HUD, and shows and
    /// hides that child. Call <see cref="Initialize"/> before anything
    /// else.</para>
    /// <para>The backdrop covers the whole screen, the HUD included, and is a
    /// raycast target: while the panel shows, a press anywhere but its
    /// buttons does nothing. The panel itself sits in the safe area.</para>
    /// <para><b>Tweens.</b> The pop runs on unscaled time, carries this
    /// component as its id, and is killed by <see cref="Hide"/>, by a new
    /// result, and when this component is disabled or destroyed. A panel left
    /// showing is snapped to its final look, so re-enabling shows it
    /// whole.</para>
    /// </remarks>
    public sealed class ResultPanel : MonoBehaviour
    {
        private RuntimeConfig config;
        private RectTransform root;
        private CanvasGroup group;
        private RectTransform panel;
        private TMP_Text title;
        private Button nextButton;
        private Button restartButton;
        private bool isSubscribed;

        /// <summary>Raised when the player presses Restart.</summary>
        public event Action RestartRequested;

        /// <summary>Raised when the player presses Next.</summary>
        public event Action NextRequested;

        /// <summary>
        /// A message saying this panel is not on or under a canvas; empty when
        /// it is.
        /// </summary>
        public string MissingCanvas() =>
            GetComponentInParent<Canvas>() != null
                ? string.Empty
                : $"{name}: ResultPanel must sit on a Canvas or under one.";

        /// <summary>
        /// Builds the panel from <paramref name="config"/>, hidden, replacing
        /// any panel built before.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public void Initialize(RuntimeConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            DOTween.Kill(this);
            Unsubscribe();
            if (root != null)
            {
                Destroy(root.gameObject);
            }

            var referencePixelsPerUnit = GetComponentInParent<Canvas>().referencePixelsPerUnit;

            root = UiBuilder.CreateRect("Result", transform);
            UiBuilder.Stretch(root);
            root.SetAsLastSibling();
            group = root.gameObject.AddComponent<CanvasGroup>();

            var backdrop = UiBuilder.CreateRect("Backdrop", root);
            UiBuilder.Stretch(backdrop);
            backdrop.gameObject.AddComponent<Image>().color = config.ResultBackdropColor;

            var safe = UiBuilder.CreateRect("Safe", root);
            UiBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            panel = UiBuilder.CreateRect("Panel", safe);
            var panelSize = config.ResultPanelSizeUnits;
            UiBuilder.PlaceCentered(panel, panelSize);
            UiBuilder.AddRoundedBox(
                panel, panelSize, config.RoundedRectSprite, config.FrameColor, config.ResultPanelCornerUnits, referencePixelsPerUnit);

            var padding = config.ResultPaddingUnits;
            var buttonSize = config.ResultButtonSizeUnits;

            // The title fills the panel above the buttons, inside the padding,
            // and wraps there if it is too long for one line.
            var titleRect = UiBuilder.CreateRect("Title", panel);
            UiBuilder.Stretch(titleRect);
            titleRect.offsetMin = new Vector2(padding, 2f * padding + buttonSize.y);
            titleRect.offsetMax = new Vector2(-padding, -padding);
            title = UiBuilder.AddLabel(titleRect, config.LabelFont, config.ResultTitleFontSize, config.ResultTitleColor, true);

            // The buttons are laid out centred in a row, so Restart alone sits
            // in the middle when Next is hidden.
            var buttons = UiBuilder.CreateRect("Buttons", panel);
            buttons.anchorMin = Vector2.zero;
            buttons.anchorMax = Vector2.right;
            buttons.pivot = new Vector2(0.5f, 0f);
            buttons.anchoredPosition = new Vector2(0f, padding);
            buttons.sizeDelta = new Vector2(-2f * padding, buttonSize.y);
            var layout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = config.ResultButtonGapUnits;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            nextButton = BuildButton(buttons, "Next", config.NextButtonColor, config.NextLabel, referencePixelsPerUnit);
            restartButton = BuildButton(buttons, "Restart", config.RestartButtonColor, config.RestartLabel, referencePixelsPerUnit);

            root.gameObject.SetActive(false);

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>Shows the win result; Next only when there is a next level.</summary>
        public void ShowWin(string titleText, bool hasNext)
        {
            Show(titleText, hasNext);
        }

        /// <summary>Shows the loss result, with Restart only.</summary>
        public void ShowLoss(string titleText)
        {
            Show(titleText, false);
        }

        /// <summary>Hides the panel, stopping its pop. Does nothing before <see cref="Initialize"/>.</summary>
        public void Hide()
        {
            if (root == null)
            {
                return;
            }

            DOTween.Kill(this);
            ShowAtRest();
            root.gameObject.SetActive(false);
        }

        private void Show(string titleText, bool showsNext)
        {
            title.text = titleText;
            nextButton.gameObject.SetActive(showsNext);
            root.gameObject.SetActive(true);
            PlayPop();
        }

        private void PlayPop()
        {
            DOTween.Kill(this);
            group.alpha = 0f;
            panel.localScale = Vector3.one * config.ResultPopStartScale;

            DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 1f, config.ResultPopSeconds)
                .SetEase(config.ResultPopEase)
                .SetUpdate(true)
                .SetId(this);
            panel.DOScale(1f, config.ResultPopSeconds)
                .SetEase(config.ResultPopEase)
                .SetUpdate(true)
                .SetId(this);
        }

        /// <summary>The panel's look once its pop has finished.</summary>
        private void ShowAtRest()
        {
            group.alpha = 1f;
            panel.localScale = Vector3.one;
        }

        private Button BuildButton(RectTransform parent, string buttonName, Color color, string label, float referencePixelsPerUnit)
        {
            var rect = UiBuilder.CreateRect(buttonName, parent);
            var size = config.ResultButtonSizeUnits;
            rect.sizeDelta = size;
            var face = UiBuilder.AddRoundedBox(
                rect, size, config.RoundedRectSprite, color, config.ResultButtonCornerUnits, referencePixelsPerUnit);

            var text = UiBuilder.CreateRect("Label", rect);
            UiBuilder.Stretch(text);
            UiBuilder.AddLabel(text, config.LabelFont, config.ResultButtonFontSize, config.ResultButtonTextColor, false).text = label;

            return UiBuilder.AddButton(rect, face);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            if (root != null)
            {
                ShowAtRest();
            }

            Unsubscribe();
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        /// <summary>
        /// Listens to both buttons once. Called from <see cref="Initialize"/>
        /// as well as <see cref="OnEnable"/>: the buttons do not exist until
        /// Initialize, which the bootstrap may call after this component was
        /// enabled.
        /// </summary>
        private void Subscribe()
        {
            if (isSubscribed || restartButton == null || nextButton == null)
            {
                return;
            }

            restartButton.onClick.AddListener(OnRestartClicked);
            nextButton.onClick.AddListener(OnNextClicked);
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(OnNextClicked);
            }

            isSubscribed = false;
        }

        private void OnRestartClicked()
        {
            RestartRequested?.Invoke();
        }

        private void OnNextClicked()
        {
            NextRequested?.Invoke();
        }
    }
}
