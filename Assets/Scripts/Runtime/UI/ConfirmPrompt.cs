using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// The confirmation before an action that costs a life (Module 32, D57):
    /// a full-screen backdrop and a rounded panel with a title — "Leave
    /// level?" or "Restart level?" —, the line saying a life will be lost, and
    /// two buttons, "Lose a life" and "Keep playing". Builds its hierarchy
    /// from code, in the result panel's style, and holds no rules: the
    /// bootstrap opens it, stops and resumes the countdown, and reports the
    /// loss.
    /// </summary>
    /// <remarks>
    /// <para>Put this component on the screen-space canvas, which stays
    /// active; it builds one <c>Confirm</c> child under its own transform,
    /// last among the canvas's children when <see cref="Initialize"/> runs,
    /// and shows and hides that child. The bootstrap initialises it last, so
    /// it draws over the HUD; the card, the result panel and the menu are
    /// never open together with it.</para>
    /// <para>The backdrop covers the whole screen, the HUD included, and is a
    /// raycast target: while the prompt shows, a press anywhere but its
    /// buttons does nothing. The panel sits in the safe area and is scaled
    /// down to fit it.</para>
    /// <para><b>Tweens.</b> The pop runs on unscaled time, carries this
    /// component as its id, and is killed by <see cref="Hide"/>, by the next
    /// <see cref="Show"/>, and when this component is disabled or destroyed.
    /// A prompt left showing is snapped to its final look.</para>
    /// </remarks>
    public sealed class ConfirmPrompt : MonoBehaviour
    {
        private static readonly Vector2 BottomMiddle = new Vector2(0.5f, 0f);

        private RuntimeConfig config;
        private RectTransform root;
        private CanvasGroup group;
        private RectTransform safe;
        private RectTransform fit;
        private RectTransform panel;
        private Vector2 fittedSafeSize;
        private TMP_Text title;
        private Button confirmButton;
        private Button cancelButton;
        private bool isSubscribed;

        /// <summary>Raised when the player presses "Lose a life". The prompt is still open; the listener hides it.</summary>
        public event Action Confirmed;

        /// <summary>Raised when the player presses "Keep playing". The prompt is still open; the listener hides it.</summary>
        public event Action Cancelled;

        /// <summary>
        /// Raised when the player presses either button, after the button's
        /// own event — so a level left or restarted has stopped its sounds
        /// before the press itself sounds (Module 25).
        /// </summary>
        public event Action ButtonPressed;

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/>.</summary>
        public bool IsOpen => root != null && root.gameObject.activeSelf;

        /// <summary>
        /// A message saying this prompt is not on or under a canvas; empty
        /// when it is.
        /// </summary>
        public string MissingCanvas() =>
            GetComponentInParent<Canvas>() != null
                ? string.Empty
                : $"{name}: ConfirmPrompt must sit on a Canvas or under one.";

        /// <summary>
        /// Builds the prompt from <paramref name="config"/>, hidden, replacing
        /// any prompt built before, and puts it last among its siblings.
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

            root = UiBuilder.CreateRect("Confirm", transform);
            UiBuilder.Stretch(root);
            root.SetAsLastSibling();
            group = root.gameObject.AddComponent<CanvasGroup>();

            var backdrop = UiBuilder.CreateRect("Backdrop", root);
            UiBuilder.Stretch(backdrop);
            backdrop.gameObject.AddComponent<Image>().color = config.ResultBackdropColor;

            safe = UiBuilder.CreateRect("Safe", root);
            UiBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // Fit scales the panel down to the safe area; the pop scales Panel.
            var panelSize = config.ConfirmPanelSizeUnits;
            fit = UiBuilder.CreateRect("Fit", safe);
            UiBuilder.PlaceCentered(fit, panelSize);
            panel = UiBuilder.CreateRect("Panel", fit);
            UiBuilder.PlaceCentered(panel, panelSize);
            UiBuilder.AddRoundedBox(
                panel, panelSize, config.RoundedRectSprite, config.FrameColor, config.ResultPanelCornerUnits, referencePixelsPerUnit);

            var padding = config.ResultPaddingUnits;
            var buttonSize = config.MenuButtonSizeUnits;

            // From the panel's foot: Keep playing, then Lose a life above it,
            // then the line, then the title filling the rest.
            cancelButton = BuildButton("Keep playing", config.NextButtonColor, config.KeepPlayingLabel, referencePixelsPerUnit);
            UiBuilder.Place((RectTransform)cancelButton.transform, BottomMiddle, new Vector2(0f, padding), buttonSize);

            var confirmBottom = padding + buttonSize.y + config.MenuButtonGapUnits;
            confirmButton = BuildButton("Lose a life", config.RestartButtonColor, config.LoseLifeLabel, referencePixelsPerUnit);
            UiBuilder.Place((RectTransform)confirmButton.transform, BottomMiddle, new Vector2(0f, confirmBottom), buttonSize);

            var lineBottom = confirmBottom + buttonSize.y + padding;
            var line = UiBuilder.CreateRect("Message", panel);
            line.anchorMin = Vector2.zero;
            line.anchorMax = Vector2.right;
            line.pivot = BottomMiddle;
            line.anchoredPosition = new Vector2(0f, lineBottom);
            line.sizeDelta = new Vector2(-2f * padding, config.ResultMessageHeightUnits);
            UiBuilder.AddLabel(line, config.LabelFont, config.ResultMessageFontSize, config.ResultMessageColor, false).text =
                config.LoseLifeMessage;

            var titleRect = UiBuilder.CreateRect("Title", panel);
            UiBuilder.Stretch(titleRect);
            titleRect.offsetMin = new Vector2(padding, lineBottom + config.ResultMessageHeightUnits);
            titleRect.offsetMax = new Vector2(-padding, -padding);
            title = UiBuilder.AddLabel(titleRect, config.LabelFont, config.ResultTitleFontSize, config.ResultTitleColor, true);

            fittedSafeSize = Vector2.negativeInfinity;
            root.gameObject.SetActive(false);

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>Opens the prompt titled <paramref name="titleText"/>, with the pop.</summary>
        /// <exception cref="InvalidOperationException"><see cref="Initialize"/> has not been called.</exception>
        public void Show(string titleText)
        {
            if (root == null)
            {
                throw new InvalidOperationException($"{name}: ConfirmPrompt.Show was called before Initialize.");
            }

            title.text = titleText;
            fittedSafeSize = Vector2.negativeInfinity;
            root.gameObject.SetActive(true);
            Fit();
            PlayPop();
        }

        /// <summary>Hides the prompt, stopping its pop. Does nothing before <see cref="Initialize"/>.</summary>
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

        private void LateUpdate()
        {
            if (IsOpen)
            {
                Fit();
            }
        }

        /// <summary>
        /// Scales the panel down to the safe area when the safe area's size
        /// has changed. Its size in canvas units follows the canvas scaler as
        /// well as the screen, so it is compared on its own.
        /// </summary>
        private void Fit()
        {
            var safeSize = safe.rect.size;
            if (safeSize == fittedSafeSize)
            {
                return;
            }

            fittedSafeSize = safeSize;
            var scale = ScreenBands.ContentScale(safeSize, config.ConfirmPanelSizeUnits);
            fit.localScale = new Vector3(scale, scale, 1f);
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

        /// <summary>The prompt's look once its pop has finished.</summary>
        private void ShowAtRest()
        {
            group.alpha = 1f;
            panel.localScale = Vector3.one;
        }

        private Button BuildButton(string buttonName, Color color, string label, float referencePixelsPerUnit)
        {
            var rect = UiBuilder.CreateRect(buttonName, panel);
            var size = config.MenuButtonSizeUnits;
            rect.sizeDelta = size;
            var face = UiBuilder.AddRoundedBox(
                rect, size, config.RoundedRectSprite, color, config.MenuButtonCornerUnits, referencePixelsPerUnit);

            var text = UiBuilder.CreateRect("Label", rect);
            UiBuilder.Stretch(text);
            UiBuilder.AddLabel(text, config.LabelFont, config.MenuButtonFontSize, config.ResultButtonTextColor, false).text = label;

            return UiBuilder.AddButton(rect, face);
        }

        /// <summary>
        /// Listens to both buttons once. Called from <see cref="Initialize"/>
        /// as well as <see cref="OnEnable"/>: the buttons do not exist until
        /// Initialize, which the bootstrap may call after this component was
        /// enabled.
        /// </summary>
        private void Subscribe()
        {
            if (isSubscribed || confirmButton == null || cancelButton == null)
            {
                return;
            }

            confirmButton.onClick.AddListener(OnConfirmClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
            }

            isSubscribed = false;
        }

        private void OnConfirmClicked()
        {
            Confirmed?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnCancelClicked()
        {
            Cancelled?.Invoke();
            ButtonPressed?.Invoke();
        }
    }
}
