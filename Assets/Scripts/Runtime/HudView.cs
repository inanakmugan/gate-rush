using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// The HUD in the top band: a restart button on the left, the timer pill
    /// with a clock icon in the centre, and a "Level N" pill on the right.
    /// Builds its hierarchy from code, in the game's style, and holds no rules
    /// — the bootstrap decides what to show and handles the restart it asks
    /// for.
    /// </summary>
    /// <remarks>
    /// <para>Put this component on the screen-space canvas, or under it; it
    /// builds one <c>HUD</c> child under its own transform, first among the
    /// canvas's children so everything built later — the result panel — draws
    /// over it. Call <see cref="Initialize"/> before anything else.</para>
    /// <para><b>Placement.</b> The <c>HUD</c> rect is anchored to the band
    /// the camera keeps free above the board, starting at the safe area's top
    /// (<see cref="ScreenBands.HudBandAnchors"/>), and re-anchored whenever the
    /// screen's size or safe area changes. Its row of elements has a fixed
    /// size in canvas units; when the band is too small for it, the row is
    /// scaled down uniformly (<see cref="ScreenBands.ContentScale"/>), so no
    /// tuning can make the HUD reach the board.</para>
    /// <para><b>Timer.</b> The text and its colour change only when the
    /// displayed second does, so a frame allocates nothing; the colour follows
    /// the displayed second (<see cref="TimeFormat.IsWarning"/>).</para>
    /// </remarks>
    public sealed class HudView : MonoBehaviour
    {
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
        private static readonly Vector2 RightMiddle = new Vector2(1f, 0.5f);

        private RuntimeConfig config;
        private RectTransform band;
        private RectTransform row;
        private Vector2 rowSize;
        private Button restartButton;
        private GameObject timerPill;
        private TMP_Text timerDigits;
        private GameObject levelPill;
        private TMP_Text levelLabel;
        private bool isSubscribed;
        private int shownSeconds = -1;

        private int placedScreenWidth;
        private int placedScreenHeight;
        private Rect placedSafeArea;
        private Vector2 fittedBandSize;

        /// <summary>Raised when the player presses the restart button.</summary>
        public event Action RestartRequested;

        /// <summary>
        /// A message saying this view is not on or under a canvas; empty when
        /// it is.
        /// </summary>
        public string MissingCanvas() =>
            GetComponentInParent<Canvas>() != null
                ? string.Empty
                : $"{name}: HudView must sit on a Canvas or under one.";

        /// <summary>
        /// Builds the HUD from <paramref name="config"/>, replacing any HUD
        /// built before. The timer and level pills start shown with no text;
        /// call <see cref="ShowTime"/> or <see cref="HideTime"/>, and
        /// <see cref="ShowLevel"/> or <see cref="HideLevel"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public void Initialize(RuntimeConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            Unsubscribe();
            if (band != null)
            {
                Destroy(band.gameObject);
            }

            var referencePixelsPerUnit = GetComponentInParent<Canvas>().referencePixelsPerUnit;

            band = UiBuilder.CreateRect("HUD", transform);
            band.SetAsFirstSibling();
            row = UiBuilder.CreateRect("Row", band);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);

            BuildRestart(referencePixelsPerUnit);
            BuildTimer(referencePixelsPerUnit);
            BuildLevel(referencePixelsPerUnit);

            // The timer is centred, so each side must hold the wider of the
            // two outer elements with padding at the edge and next to it.
            var side = Mathf.Max(config.HudRestartSizeUnits, config.LevelPillWidthUnits) + 2f * config.HudSidePaddingUnits;
            rowSize = new Vector2(
                config.TimerPillWidthUnits + 2f * side,
                Mathf.Max(config.HudRestartSizeUnits, config.HudPillHeightUnits));

            shownSeconds = -1;
            placedScreenWidth = 0;
            fittedBandSize = Vector2.negativeInfinity;
            Place();

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>Shows the level pill reading <paramref name="levelNumber"/> through the configured format.</summary>
        public void ShowLevel(int levelNumber)
        {
            levelLabel.text = string.Format(CultureInfo.InvariantCulture, config.LevelLabelFormat, levelNumber);
            levelPill.SetActive(true);
        }

        /// <summary>Hides the level pill, for a level that has no number.</summary>
        public void HideLevel()
        {
            levelPill.SetActive(false);
        }

        /// <summary>
        /// Shows the timer pill with <paramref name="remainingSeconds"/> as
        /// <c>mm:ss</c>, rounded up, in the warning colour once the displayed
        /// second reaches the threshold. Cheap to call every frame.
        /// </summary>
        public void ShowTime(float remainingSeconds)
        {
            if (!timerPill.activeSelf)
            {
                timerPill.SetActive(true);
            }

            var whole = TimeFormat.WholeSeconds(remainingSeconds);
            if (whole == shownSeconds)
            {
                return;
            }

            shownSeconds = whole;
            timerDigits.text = TimeFormat.MinutesSeconds(remainingSeconds);
            timerDigits.color = TimeFormat.IsWarning(remainingSeconds, config.TimerWarningSeconds)
                ? config.TimerWarningColor
                : config.HudTextColor;
        }

        /// <summary>Hides the timer pill, for a level without a countdown.</summary>
        public void HideTime()
        {
            timerPill.SetActive(false);
            shownSeconds = -1;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            Place();
        }

        /// <summary>
        /// Anchors the HUD to the band when the screen or its safe area has
        /// changed, then scales the row to the band when the band's size has.
        /// The band's size in canvas units also follows the canvas scaler, so
        /// it is compared on its own.
        /// </summary>
        private void Place()
        {
            if (band == null)
            {
                return;
            }

            var width = Screen.width;
            var height = Screen.height;
            var safeArea = Screen.safeArea;
            if ((width != placedScreenWidth || height != placedScreenHeight || safeArea != placedSafeArea)
                && width > 0 && height > 0)
            {
                placedScreenWidth = width;
                placedScreenHeight = height;
                placedSafeArea = safeArea;
                var anchors = ScreenBands.HudBandAnchors(safeArea, width, height, config.TopBandScreenFraction);
                UiBuilder.SetAnchors(band, anchors.min, anchors.max);
            }

            var bandSize = band.rect.size;
            if (bandSize == fittedBandSize)
            {
                return;
            }

            fittedBandSize = bandSize;
            var scale = ScreenBands.ContentScale(bandSize, rowSize);
            row.sizeDelta = scale > 0f ? bandSize / scale : rowSize;
            row.localScale = new Vector3(scale, scale, 1f);
        }

        private void BuildRestart(float referencePixelsPerUnit)
        {
            var rect = UiBuilder.CreateRect("Restart", row);
            var size = Vector2.one * config.HudRestartSizeUnits;
            UiBuilder.Place(rect, LeftMiddle, new Vector2(config.HudSidePaddingUnits, 0f), size);
            var face = UiBuilder.AddRoundedBox(
                rect, size, config.RoundedRectSprite, config.FrameColor, config.HudRestartCornerUnits, referencePixelsPerUnit);
            restartButton = UiBuilder.AddButton(rect, face);

            var icon = UiBuilder.CreateRect("Icon", rect);
            UiBuilder.PlaceCentered(icon, Vector2.one * config.HudRestartIconSizeUnits);
            UiBuilder.AddIcon(icon, config.RestartSprite, config.HudIconColor);
        }

        private void BuildTimer(float referencePixelsPerUnit)
        {
            var rect = UiBuilder.CreateRect("Timer", row);
            var height = config.HudPillHeightUnits;
            var size = new Vector2(config.TimerPillWidthUnits, height);
            UiBuilder.PlaceCentered(rect, size);
            UiBuilder.AddRoundedBox(rect, size, config.RoundedRectSprite, config.HudPillColor, height * 0.5f, referencePixelsPerUnit);
            timerPill = rect.gameObject;

            // The icon sits as far in from the pill's left end as from its top
            // and bottom; the digits are centred in the rest.
            var iconSize = config.HudClockIconSizeUnits;
            var inset = (height - iconSize) * 0.5f;
            var icon = UiBuilder.CreateRect("Clock", rect);
            UiBuilder.Place(icon, LeftMiddle, new Vector2(inset, 0f), Vector2.one * iconSize);
            UiBuilder.AddIcon(icon, config.ClockSprite, config.HudIconColor);

            var digits = UiBuilder.CreateRect("Digits", rect);
            UiBuilder.Stretch(digits);
            digits.offsetMin = new Vector2(inset + iconSize + config.HudIconGapUnits, 0f);
            digits.offsetMax = new Vector2(-inset, 0f);
            timerDigits = UiBuilder.AddLabel(digits, config.LabelFont, config.HudFontSize, config.HudTextColor, false);
        }

        private void BuildLevel(float referencePixelsPerUnit)
        {
            var rect = UiBuilder.CreateRect("Level", row);
            var height = config.HudPillHeightUnits;
            var size = new Vector2(config.LevelPillWidthUnits, height);
            UiBuilder.Place(rect, RightMiddle, new Vector2(-config.HudSidePaddingUnits, 0f), size);
            UiBuilder.AddRoundedBox(rect, size, config.RoundedRectSprite, config.HudPillColor, height * 0.5f, referencePixelsPerUnit);
            levelPill = rect.gameObject;

            var label = UiBuilder.CreateRect("Label", rect);
            UiBuilder.Stretch(label);
            levelLabel = UiBuilder.AddLabel(label, config.LabelFont, config.HudFontSize, config.HudTextColor, false);
        }

        /// <summary>
        /// Listens to the restart button once. Called from
        /// <see cref="Initialize"/> as well as <see cref="OnEnable"/>: the
        /// button does not exist until Initialize, which the bootstrap may call
        /// after this component was enabled.
        /// </summary>
        private void Subscribe()
        {
            if (isSubscribed || restartButton == null)
            {
                return;
            }

            restartButton.onClick.AddListener(OnRestartClicked);
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

            isSubscribed = false;
        }

        private void OnRestartClicked()
        {
            RestartRequested?.Invoke();
        }
    }
}
