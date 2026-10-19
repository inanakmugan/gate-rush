using System;
using GateRush.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// The joker bar in the bottom band (Module 34, D67): three buttons in one
    /// row — Clock, Rocket, Broom — each reading its count, or its price in
    /// the campaign when none is held. Builds its hierarchy from code in the
    /// menu buttons' style and holds no rules: the bootstrap decides each
    /// button's text and whether it can be pressed (<see cref="JokerButtons"/>),
    /// which joker is selected (<see cref="JokerSelection"/>), and what a
    /// press does. Prototype front end (D64); Phase 6 replaces its look.
    /// </summary>
    /// <remarks>
    /// <para>Put this component on the screen-space canvas, which stays
    /// active; it builds one <c>Jokers</c> child under its own transform, last
    /// among the canvas's children when <see cref="Initialize"/> runs. The
    /// bootstrap initialises it right after the HUD, so the introduction
    /// card, the result panel and the confirmation, built later,
    /// all draw over it. The front end (Module 35), a scene object under the
    /// canvas, ends up beneath it, and the bar is hidden while it shows.</para>
    /// <para><b>Placement.</b> The <c>Jokers</c> rect is anchored to the band
    /// the camera keeps free below the board, starting at the safe area's
    /// bottom (<see cref="ScreenBands.BottomBandAnchors"/>), and re-anchored
    /// whenever the screen's size or safe area changes. The row has a fixed
    /// size in canvas units and is scaled down uniformly when the band is too
    /// small for it (<see cref="ScreenBands.ContentScale"/>), as the HUD's
    /// is.</para>
    /// <para>It plays no animation. Its buttons are not pressable until the
    /// bootstrap first shows them.</para>
    /// </remarks>
    public sealed class JokerBar : MonoBehaviour
    {
        // The kinds the bar shows, one button each, in this order left to
        // right; an instance field, so nothing static can be changed.
        private readonly JokerKind[] kinds = { JokerKind.Clock, JokerKind.Rocket, JokerKind.Broom };

        private RuntimeConfig config;
        private RectTransform band;
        private RectTransform row;
        private Vector2 rowSize;
        private Button[] buttons;
        private Image[] faces;
        private TMP_Text[] labels;
        private UnityAction[] clickHandlers;
        private bool isSubscribed;

        private int placedScreenWidth;
        private int placedScreenHeight;
        private Rect placedSafeArea;
        private Vector2 fittedBandSize;

        /// <summary>
        /// Raised when the player presses a joker's button, with its kind.
        /// Pressing spends nothing; the listener decides what follows.
        /// </summary>
        public event Action<JokerKind> JokerPressed;

        /// <summary>
        /// Raised when the player presses any of the buttons, after
        /// <see cref="JokerPressed"/>, for the tap sound (Module 25), as the
        /// HUD's buttons do.
        /// </summary>
        public event Action ButtonPressed;

        /// <summary>
        /// A message saying this bar is not on or under a canvas; empty when
        /// it is.
        /// </summary>
        public string MissingCanvas() =>
            GetComponentInParent<Canvas>() != null
                ? string.Empty
                : $"{name}: JokerBar must sit on a Canvas or under one.";

        /// <summary>
        /// Builds the bar from <paramref name="config"/>, hidden, with every
        /// button not pressable and nothing selected, replacing any bar built
        /// before, and puts it last among its siblings.
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

            band = UiBuilder.CreateRect("Jokers", transform);
            band.SetAsLastSibling();
            row = UiBuilder.CreateRect("Row", band);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);

            var buttonSize = config.JokerButtonSizeUnits;
            var gap = config.JokerButtonGapUnits;
            rowSize = new Vector2(kinds.Length * buttonSize.x + (kinds.Length - 1) * gap, buttonSize.y);

            buttons = new Button[kinds.Length];
            faces = new Image[kinds.Length];
            labels = new TMP_Text[kinds.Length];
            clickHandlers = new UnityAction[kinds.Length];
            var firstCenterX = (buttonSize.x - rowSize.x) * 0.5f;
            for (var i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                var rect = UiBuilder.CreateRect(kind.ToString(), row);
                UiBuilder.PlaceCentered(rect, buttonSize);
                rect.anchoredPosition = new Vector2(firstCenterX + i * (buttonSize.x + gap), 0f);
                faces[i] = UiBuilder.AddRoundedBox(
                    rect, buttonSize, config.RoundedRectSprite, config.FrameColor, config.MenuButtonCornerUnits,
                    referencePixelsPerUnit);

                var text = UiBuilder.CreateRect("Label", rect);
                UiBuilder.Stretch(text);
                labels[i] = UiBuilder.AddLabel(text, config.LabelFont, config.JokerButtonFontSize, config.ResultButtonTextColor, false);

                buttons[i] = UiBuilder.AddButton(rect, faces[i]);
                buttons[i].interactable = false;
                clickHandlers[i] = () => OnClicked(kind);
            }

            placedScreenWidth = 0;
            fittedBandSize = Vector2.negativeInfinity;
            Place();
            band.gameObject.SetActive(false);

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>Shows the bar, for a level being played. What its buttons read is unchanged. Does nothing before <see cref="Initialize"/>.</summary>
        public void Show()
        {
            if (band != null)
            {
                band.gameObject.SetActive(true);
                Place();
            }
        }

        /// <summary>Hides the bar, for the menu, where no level is loaded. Does nothing before <see cref="Initialize"/>.</summary>
        public void Hide()
        {
            if (band != null)
            {
                band.gameObject.SetActive(false);
            }
        }

        /// <summary>Shows <paramref name="button"/>'s text on <paramref name="kind"/>'s button and makes it pressable or not.</summary>
        /// <exception cref="InvalidOperationException"><see cref="Initialize"/> has not been called.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public void ShowButton(JokerKind kind, JokerButton button)
        {
            var i = SlotOf(kind);
            labels[i].text = button.Label;
            buttons[i].interactable = button.IsInteractable;
        }

        /// <summary>
        /// Highlights the button of <paramref name="kind"/> in
        /// <see cref="RuntimeConfig.JokerSelectedColor"/> and shows every other
        /// in the frame's colour; null highlights none.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="Initialize"/> has not been called.</exception>
        public void ShowSelected(JokerKind? kind)
        {
            RequireInitialized();
            for (var i = 0; i < kinds.Length; i++)
            {
                faces[i].color = kinds[i] == kind ? config.JokerSelectedColor : config.FrameColor;
            }
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
        /// Anchors the bar to the bottom band when the screen or its safe area
        /// has changed, then scales the row to the band when the band's size
        /// has. The band's size in canvas units also follows the canvas
        /// scaler, so it is compared on its own.
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
                var anchors = ScreenBands.BottomBandAnchors(safeArea, width, height, config.BottomBandScreenFraction);
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

        private int SlotOf(JokerKind kind)
        {
            RequireInitialized();
            for (var i = 0; i < kinds.Length; i++)
            {
                if (kinds[i] == kind)
                {
                    return i;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(kind), kind, $"{(int)kind} is not a joker kind.");
        }

        private void RequireInitialized()
        {
            if (band == null)
            {
                throw new InvalidOperationException($"{name}: JokerBar was used before Initialize.");
            }
        }

        /// <summary>
        /// Listens to every button once. Called from <see cref="Initialize"/>
        /// as well as <see cref="OnEnable"/>: the buttons do not exist until
        /// Initialize, which the bootstrap may call after this component was
        /// enabled.
        /// </summary>
        private void Subscribe()
        {
            if (isSubscribed || buttons == null)
            {
                return;
            }

            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i].onClick.AddListener(clickHandlers[i]);
            }

            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].onClick.RemoveListener(clickHandlers[i]);
                }
            }

            isSubscribed = false;
        }

        private void OnClicked(JokerKind kind)
        {
            JokerPressed?.Invoke(kind);
            ButtonPressed?.Invoke();
        }
    }
}
