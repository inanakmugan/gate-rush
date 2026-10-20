using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// The card that introduces a mechanic the first time the player meets it
    /// (Module 19): a dim over the board, and on the theme's grey panel a
    /// title, a subtitle, the mechanic drawn in the game's own art among
    /// twinkling sparkles, one sentence in a text box, and the red round close
    /// button (Module 41). Several mechanics show one card after another.
    /// Builds its hierarchy from code and holds no rules — the bootstrap
    /// decides which mechanics to show and what follows the last card.
    /// </summary>
    /// <remarks>
    /// <para>Put this component on the screen-space canvas, which stays
    /// active; it builds one <c>Introduction</c> child under its own transform
    /// and shows and hides that child. <see cref="Initialize"/> puts the child
    /// last among the canvas's children, after the level's interface, so the
    /// card draws over the HUD, the joker bar and the level's panels — which
    /// are never open together with it.</para>
    /// <para><b>The theme.</b> It stays in <c>Runtime</c> because its
    /// drawings are the game's own art (D46). The panel, the font and the
    /// close button's sprites are <see cref="RuntimeConfig"/>'s, drawn in
    /// their own colours; nothing of them is drawn by hand.</para>
    /// <para><b>Closing.</b> The backdrop covers the whole screen, swallows
    /// every press, and is itself a button, and nothing else on the card but
    /// the close button takes a press: a tap anywhere closes the card.
    /// <see cref="IsOpen"/> holds from <see cref="Show"/> until the last
    /// card's fade has finished.</para>
    /// <para><b>Tweens.</b> The pop, the fade and the twinkle run on unscaled
    /// time, carry this component as their id, and are killed by
    /// <see cref="Hide"/>, by the next card, and when this component is
    /// disabled or destroyed. A card left open through a disable is snapped to
    /// its resting look, a close in progress included: the queue never
    /// advances, and the callback never runs, while disabled.</para>
    /// </remarks>
    public sealed class IntroductionCard : MonoBehaviour
    {
        private static readonly Vector2 TopMiddle = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        // The panel and the close button are the theme's sprites in their own
        // colours, as the front end's prefabs draw them: not a tunable.
        private static readonly Color Untinted = Color.white;

        private readonly List<LevelMechanic> queue = new List<LevelMechanic>();
        private readonly List<RectTransform> sparkles = new List<RectTransform>();
        private readonly List<Image> sparkleImages = new List<Image>();

        // Each built sparkle's phase, kept as built: the config's list may be
        // edited in the Inspector while a card is open.
        private readonly List<float> sparklePhases = new List<float>();

        private RuntimeConfig config;
        private MechanicIllustration illustrator;
        private RectTransform root;
        private CanvasGroup group;
        private RectTransform safe;
        private RectTransform fit;
        private RectTransform card;
        private Vector2 cardSize;
        private float rowWidth;
        private Vector2 fittedSafeSize;
        private TMP_Text title;
        private TMP_Text subtitle;
        private TMP_Text text;
        private RectTransform illustration;
        private RectTransform art;
        private Button backdropButton;
        private Button closeButton;
        private bool isSubscribed;

        private int shownIndex;
        private Action onAllClosed;
        private bool isClosing;

        /// <summary>
        /// True from <see cref="Show"/> with at least one mechanic until the
        /// last card has closed and faded, or <see cref="Hide"/>. While it
        /// holds, the level's countdown and input wait.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Raised when a press closes a card — on the close button or anywhere
        /// on the backdrop — as its fade starts (Module 25). A press while a
        /// card is already fading closes nothing and raises nothing.
        /// </summary>
        public event Action ButtonPressed;

        /// <summary>
        /// A message saying this card is not on or under a canvas; empty when
        /// it is.
        /// </summary>
        public string MissingCanvas() =>
            GetComponentInParent<Canvas>() != null
                ? string.Empty
                : $"{name}: IntroductionCard must sit on a Canvas or under one.";

        /// <summary>
        /// Builds the card from <paramref name="config"/>, hidden, replacing
        /// any card built before, and puts it last among its siblings.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public void Initialize(RuntimeConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            DOTween.Kill(this);
            Unsubscribe();
            Forget();
            if (root != null)
            {
                Destroy(root.gameObject);
            }

            sparkles.Clear();
            sparkleImages.Clear();
            sparklePhases.Clear();
            art = null;

            var referencePixelsPerUnit = GetComponentInParent<Canvas>().referencePixelsPerUnit;
            illustrator = new MechanicIllustration(config, referencePixelsPerUnit);

            root = UiBuilder.CreateRect("Introduction", transform);
            UiBuilder.Stretch(root);
            root.SetAsLastSibling();
            group = root.gameObject.AddComponent<CanvasGroup>();

            var backdrop = UiBuilder.CreateRect("Backdrop", root);
            UiBuilder.Stretch(backdrop);
            var dim = backdrop.gameObject.AddComponent<Image>();
            dim.color = config.IntroBackdropColor;
            backdropButton = UiBuilder.AddButton(backdrop, dim);

            // The dim must not flash as it is pressed.
            backdropButton.transition = Selectable.Transition.None;

            safe = UiBuilder.CreateRect("Safe", root);
            UiBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // Fit scales the card down to the safe area; the pop scales Card.
            fit = UiBuilder.CreateRect("Fit", safe);
            card = UiBuilder.CreateRect("Card", fit);

            var spacing = config.IntroSpacingUnits;
            var padding = config.IntroPanelPaddingUnits;
            var closeSize = config.IntroCloseSizeUnits;
            var illustrationSize = config.IntroIllustrationSizeUnits;
            var boxSize = config.IntroTextBoxSizeUnits;
            rowWidth = Mathf.Max(illustrationSize.x, boxSize.x);
            cardSize = new Vector2(
                rowWidth + 2f * padding,
                closeSize + config.IntroTitleHeightUnits + config.IntroSubtitleHeightUnits + illustrationSize.y + boxSize.y
                + 4f * spacing + 2f * padding);
            UiBuilder.PlaceCentered(fit, cardSize);
            UiBuilder.PlaceCentered(card, cardSize);
            BuildPanel();

            // Rows from the top, inside the padding: close button, title,
            // subtitle, illustration, text box.
            var top = padding;
            BuildClose(padding);
            top += closeSize + spacing;

            title = BuildRow("Title", top, config.IntroTitleHeightUnits, config.IntroTitleFontSize, config.IntroTitleColor);
            top += config.IntroTitleHeightUnits + spacing;

            subtitle = BuildRow("Subtitle", top, config.IntroSubtitleHeightUnits, config.IntroSubtitleFontSize, config.IntroSubtitleColor);
            top += config.IntroSubtitleHeightUnits + spacing;

            illustration = UiBuilder.CreateRect("Illustration", card);
            UiBuilder.Place(illustration, TopMiddle, new Vector2(0f, -top), illustrationSize);
            BuildSparkles();
            top += illustrationSize.y + spacing;

            BuildTextBox(top, referencePixelsPerUnit);

            fittedSafeSize = Vector2.negativeInfinity;
            root.gameObject.SetActive(false);

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>
        /// Shows one card per mechanic of <paramref name="mechanics"/>, in
        /// order, each opening when the one before it has closed, and calls
        /// <paramref name="onAllClosed"/> once the last has closed and faded.
        /// Cards still showing from an earlier call are dropped without their
        /// callback. With no mechanics, nothing shows and
        /// <paramref name="onAllClosed"/> is called at once.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="mechanics"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Initialize"/> has not been called.</exception>
        public void Show(IReadOnlyList<LevelMechanic> mechanics, Action onAllClosed)
        {
            if (mechanics == null)
            {
                throw new ArgumentNullException(nameof(mechanics));
            }

            if (root == null)
            {
                throw new InvalidOperationException($"{name}: IntroductionCard.Show was called before Initialize.");
            }

            Hide();
            if (mechanics.Count == 0)
            {
                onAllClosed?.Invoke();
                return;
            }

            queue.AddRange(mechanics);
            shownIndex = 0;
            this.onAllClosed = onAllClosed;
            IsOpen = true;
            root.gameObject.SetActive(true);
            ShowCurrent();
        }

        /// <summary>
        /// Hides the card and drops every card still to come, without calling
        /// the callback <see cref="Show"/> was given. Does nothing before
        /// <see cref="Initialize"/>.
        /// </summary>
        public void Hide()
        {
            if (root == null)
            {
                return;
            }

            DOTween.Kill(this);
            Forget();
            ShowAtRest();
            root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Subscribe();

            // Disabling killed the twinkle of a card left open.
            if (IsOpen)
            {
                PlayTwinkle();
            }
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            if (root != null)
            {
                // A close that was fading is undone: the card is open again,
                // whole, and the player closes it once more.
                isClosing = false;
                ShowAtRest();
            }

            Unsubscribe();
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        /// <summary>
        /// Scales the card down to the safe area when the safe area's size has
        /// changed. Its size in canvas units follows the canvas scaler as well
        /// as the screen, so it is compared on its own.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsOpen)
            {
                return;
            }

            var safeSize = safe.rect.size;
            if (safeSize == fittedSafeSize)
            {
                return;
            }

            fittedSafeSize = safeSize;
            var scale = ScreenBands.ContentScale(safeSize, cardSize);
            fit.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Fills the card for the mechanic at <see cref="shownIndex"/> and opens it with the pop.</summary>
        private void ShowCurrent()
        {
            var mechanic = queue[shownIndex];
            title.text = config.IntroductionTitle(mechanic);
            subtitle.text = config.IntroductionSubtitle(mechanic);
            text.text = config.IntroductionText(mechanic);

            if (art != null)
            {
                Destroy(art.gameObject);
            }

            art = illustrator.Build(illustration, mechanic);

            // Sparkles draw over the illustration.
            art.SetAsFirstSibling();

            PlayPop();
            PlayTwinkle();
        }

        /// <summary>The opening pop: the whole card fades in while it grows to its size.</summary>
        private void PlayPop()
        {
            DOTween.Kill(this);
            group.alpha = 0f;
            card.localScale = Vector3.one * config.IntroPopStartScale;

            DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 1f, config.IntroPopSeconds)
                .SetEase(config.IntroPopEase)
                .SetUpdate(true)
                .SetId(this);
            card.DOScale(1f, config.IntroPopSeconds)
                .SetEase(config.IntroPopEase)
                .SetUpdate(true)
                .SetId(this);
        }

        /// <summary>
        /// Every sparkle pulses between its faint and its full look along one
        /// cosine, each from its own phase. One looping tween drives them all.
        /// </summary>
        private void PlayTwinkle()
        {
            DOVirtual.Float(0f, 1f, config.SparkleTwinkleSeconds, ApplyTwinkle)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true)
                .SetId(this);
        }

        private void ApplyTwinkle(float time)
        {
            var color = config.SparkleColor;
            for (var i = 0; i < sparkles.Count; i++)
            {
                var strength = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * (time + sparklePhases[i]));
                var scale = Mathf.Lerp(config.SparkleMinScale, 1f, strength);
                sparkles[i].localScale = new Vector3(scale, scale, 1f);

                var tint = color;
                tint.a *= Mathf.Lerp(config.SparkleMinAlpha, 1f, strength);
                sparkleImages[i].color = tint;
            }
        }

        /// <summary>
        /// A press on the backdrop or the close button: the card fades out,
        /// then the next one opens or the callback runs. A press while a card
        /// is already fading does nothing.
        /// </summary>
        private void OnCloseClicked()
        {
            if (!IsOpen || isClosing)
            {
                return;
            }

            isClosing = true;
            DOTween.Kill(this);
            card.localScale = Vector3.one;

            DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 0f, config.IntroCloseSeconds)
                .SetEase(config.IntroCloseEase)
                .SetUpdate(true)
                .SetId(this)
                .OnComplete(OnClosed);

            ButtonPressed?.Invoke();
        }

        private void OnClosed()
        {
            isClosing = false;
            shownIndex++;
            if (shownIndex < queue.Count)
            {
                ShowCurrent();
                return;
            }

            // Forgotten before the callback runs, so a callback that shows
            // cards again starts from a closed card.
            var done = onAllClosed;
            Forget();
            ShowAtRest();
            root.gameObject.SetActive(false);
            done?.Invoke();
        }

        /// <summary>Drops the queue and the callback: the card is no longer open.</summary>
        private void Forget()
        {
            queue.Clear();
            shownIndex = 0;
            onAllClosed = null;
            isClosing = false;
            IsOpen = false;
        }

        /// <summary>The card's look once its pop has finished, with every sparkle at its full look.</summary>
        private void ShowAtRest()
        {
            group.alpha = 1f;
            card.localScale = Vector3.one;
            for (var i = 0; i < sparkles.Count; i++)
            {
                sparkles[i].localScale = Vector3.one;
                sparkleImages[i].color = config.SparkleColor;
            }
        }

        /// <summary>
        /// The theme's grey panel behind the card's rows, 9-sliced. It takes
        /// no press, so a tap on the panel closes the card too.
        /// </summary>
        private void BuildPanel()
        {
            var panel = card.gameObject.AddComponent<Image>();
            panel.sprite = config.IntroPanelSprite;
            panel.type = Image.Type.Sliced;
            panel.color = Untinted;
            panel.raycastTarget = false;
        }

        /// <summary>A one-line label in the card's font, filling a row of the card <paramref name="top"/> below its top edge.</summary>
        private TMP_Text BuildRow(string rowName, float top, float height, float fontSize, Color color)
        {
            var rect = UiBuilder.CreateRect(rowName, card);
            UiBuilder.Place(rect, TopMiddle, new Vector2(0f, -top), new Vector2(rowWidth, height));
            return UiBuilder.AddLabel(rect, config.IntroFont, fontSize, color, false);
        }

        /// <summary>
        /// The round close button in the card's top right corner, inside the
        /// padding: the theme's red face with the cross centred on it.
        /// </summary>
        private void BuildClose(float padding)
        {
            var rect = UiBuilder.CreateRect("Close", card);
            UiBuilder.Place(rect, TopRight, new Vector2(-padding, -padding), Vector2.one * config.IntroCloseSizeUnits);
            var face = UiBuilder.AddIcon(rect, config.IntroCloseSprite, Untinted);

            // AddIcon leaves an icon out of raycasts; the face is the button's.
            face.raycastTarget = true;
            closeButton = UiBuilder.AddButton(rect, face);

            var cross = UiBuilder.CreateRect("Cross", rect);
            UiBuilder.PlaceCentered(cross, Vector2.one * config.IntroCloseCrossSizeUnits);
            UiBuilder.AddIcon(cross, config.IntroCloseCrossSprite, Untinted);
        }

        /// <summary>The sparkles: each sits where its placement says, over the illustration.</summary>
        private void BuildSparkles()
        {
            var area = config.IntroIllustrationSizeUnits;
            var placements = config.IntroSparkles;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                var rect = UiBuilder.CreateRect($"Sparkle {i}", illustration);
                UiBuilder.Place(rect, Middle, Vector2.Scale(placement.Position, area), Vector2.one * placement.SizeUnits);
                sparkles.Add(rect);
                sparkleImages.Add(UiBuilder.AddIcon(rect, config.SparkleSprite, config.SparkleColor));
                sparklePhases.Add(placement.Phase);
            }
        }

        /// <summary>
        /// The text box at the card's foot: a rounded box in the frame's colour
        /// with a smaller cream one on it, which leaves the frame's colour as a
        /// border, and dark text in the card's font that wraps inside the
        /// padding. Neither box takes a press, so a tap on the text closes the
        /// card too.
        /// </summary>
        private void BuildTextBox(float top, float referencePixelsPerUnit)
        {
            var size = config.IntroTextBoxSizeUnits;
            var border = config.IntroTextBoxBorderUnits;
            var corner = config.IntroTextBoxCornerUnits;

            var box = UiBuilder.CreateRect("Text box", card);
            UiBuilder.Place(box, TopMiddle, new Vector2(0f, -top), size);
            UiBuilder.AddRoundedBox(box, size, config.RoundedRectSprite, config.FrameColor, corner, referencePixelsPerUnit)
                .raycastTarget = false;

            var fill = UiBuilder.CreateRect("Fill", box);
            var fillSize = size - 2f * border * Vector2.one;
            UiBuilder.PlaceCentered(fill, fillSize);
            UiBuilder.AddRoundedBox(
                    fill, fillSize, config.RoundedRectSprite, config.IntroTextBoxColor, corner - border, referencePixelsPerUnit)
                .raycastTarget = false;

            var label = UiBuilder.CreateRect("Text", box);
            UiBuilder.Stretch(label);
            var padding = config.IntroTextPaddingUnits;
            label.offsetMin = new Vector2(padding, padding);
            label.offsetMax = new Vector2(-padding, -padding);
            text = UiBuilder.AddLabel(label, config.IntroFont, config.IntroTextFontSize, config.IntroTextColor, true);
        }

        /// <summary>
        /// Listens to both buttons once. Called from <see cref="Initialize"/>
        /// as well as <see cref="OnEnable"/>: the buttons do not exist until
        /// Initialize, which the bootstrap may call after this component was
        /// enabled.
        /// </summary>
        private void Subscribe()
        {
            if (isSubscribed || backdropButton == null || closeButton == null)
            {
                return;
            }

            backdropButton.onClick.AddListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            if (backdropButton != null)
            {
                backdropButton.onClick.RemoveListener(OnCloseClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
            }

            isSubscribed = false;
        }
    }
}
