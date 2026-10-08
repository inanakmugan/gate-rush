using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// The prototype menu (Module 23, D50): a title page with the game's name,
    /// Play and Levels, and a level-select page with one button per level and
    /// Back. An overlay in the Level scene, drawn over the background while no
    /// level is loaded. Builds its hierarchy from code and holds no rules — the
    /// bootstrap says which levels to list (<see cref="MenuLevels.Entries"/>)
    /// and starts the level a button asks for.
    /// </summary>
    /// <remarks>
    /// <para>Put this component on the screen-space canvas, which stays
    /// active; it builds one <c>Menu</c> child under its own transform, last
    /// among the canvas's children, and shows and hides that child. Call
    /// <see cref="Initialize"/> before anything else.</para>
    /// <para><b>No level is locked.</b> Every entry gets a button that asks
    /// for its level, completed or not; a completed one only looks different:
    /// its own tint and a tick.</para>
    /// <para><b>Fit.</b> Each page has a fixed size in canvas units and is
    /// scaled down uniformly to the safe area less the menu's padding
    /// (<see cref="ScreenBands.ContentScale"/>), so it fits a portrait phone
    /// and a short embed alike. There is no scrolling: a longer level list
    /// makes a taller page, which is scaled down further.</para>
    /// <para><b>Tweens.</b> A page opens with the result panel's pop, on
    /// unscaled time, carrying this component as its id; it is killed by
    /// <see cref="Hide"/>, by the next page, and when this component is
    /// disabled or destroyed. A menu left showing is snapped to its resting
    /// look.</para>
    /// </remarks>
    public sealed class MenuScreen : MonoBehaviour
    {
        private static readonly Vector2 TopMiddle = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomMiddle = new Vector2(0.5f, 0f);

        // The tick's shape, as fractions of the square it is drawn in: two
        // strokes meeting at a vertex left of and below the square's centre,
        // the short one rising to the left and the long one to the right. Not
        // tunables; the square's size, the strokes' thickness and the colour
        // are (RuntimeConfig).
        private static readonly Vector2 TickVertex = new Vector2(-0.12f, -0.28f);
        private const float TickShortStrokeLength = 0.42f;
        private const float TickLongStrokeLength = 0.82f;
        private const float TickShortStrokeDegrees = 135f;
        private const float TickLongStrokeDegrees = 45f;

        private readonly List<LevelButton> levelButtons = new List<LevelButton>();

        private RuntimeConfig config;
        private float referencePixelsPerUnit;
        private RectTransform root;
        private CanvasGroup group;
        private RectTransform safe;

        private RectTransform titleFit;
        private RectTransform titlePage;
        private Vector2 titlePageSize;
        private Button playButton;
        private Button levelsButton;

        // Null while there is no level to list.
        private RectTransform levelsFit;
        private RectTransform levelsPage;
        private Vector2 levelsPageSize;
        private Button backButton;

        private Vector2 fittedSafeSize;
        private bool isSubscribed;

        /// <summary>Raised when the player presses Play.</summary>
        public event Action PlayRequested;

        /// <summary>Raised when the player presses a level's button, with that level's file name.</summary>
        public event Action<string> LevelRequested;

        /// <summary>True from <see cref="ShowTitle"/> until <see cref="Hide"/>.</summary>
        public bool IsOpen => root != null && root.gameObject.activeSelf;

        /// <summary>
        /// A message saying this screen is not on or under a canvas; empty
        /// when it is.
        /// </summary>
        public string MissingCanvas() =>
            GetComponentInParent<Canvas>() != null
                ? string.Empty
                : $"{name}: MenuScreen must sit on a Canvas or under one.";

        /// <summary>
        /// Builds the menu from <paramref name="config"/>, hidden, replacing
        /// any menu built before, and puts it last among its siblings.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public void Initialize(RuntimeConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            DOTween.Kill(this);
            Unsubscribe();
            levelButtons.Clear();
            backButton = null;
            levelsFit = null;
            levelsPage = null;
            if (root != null)
            {
                Destroy(root.gameObject);
            }

            referencePixelsPerUnit = GetComponentInParent<Canvas>().referencePixelsPerUnit;

            root = UiBuilder.CreateRect("Menu", transform);
            UiBuilder.Stretch(root);
            root.SetAsLastSibling();
            group = root.gameObject.AddComponent<CanvasGroup>();

            safe = UiBuilder.CreateRect("Safe", root);
            UiBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            BuildTitlePage();

            fittedSafeSize = Vector2.negativeInfinity;
            root.gameObject.SetActive(false);

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>
        /// Opens the menu on its title page, with the level select rebuilt
        /// from <paramref name="entries"/> — one button per entry, in order.
        /// With no entries the Levels button is hidden: there is nothing to
        /// select.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="entries"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Initialize"/> has not been called.</exception>
        public void ShowTitle(IReadOnlyList<MenuLevelEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (root == null)
            {
                throw new InvalidOperationException($"{name}: MenuScreen.ShowTitle was called before Initialize.");
            }

            BuildLevelsPage(entries);
            levelsButton.gameObject.SetActive(entries.Count > 0);

            fittedSafeSize = Vector2.negativeInfinity;
            root.gameObject.SetActive(true);
            Fit();
            ShowPage(titlePage);
        }

        /// <summary>Hides the menu, stopping its pop. Does nothing before <see cref="Initialize"/>.</summary>
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
        /// Scales both pages down to the safe area less the padding, when the
        /// safe area's size has changed. Its size in canvas units follows the
        /// canvas scaler as well as the screen, so it is compared on its own.
        /// </summary>
        private void Fit()
        {
            var safeSize = safe.rect.size;
            if (safeSize == fittedSafeSize)
            {
                return;
            }

            fittedSafeSize = safeSize;
            var available = safeSize - 2f * config.MenuPaddingUnits * Vector2.one;
            ScaleToFit(titleFit, available, titlePageSize);
            if (levelsFit != null)
            {
                ScaleToFit(levelsFit, available, levelsPageSize);
            }
        }

        private static void ScaleToFit(RectTransform fit, Vector2 available, Vector2 pageSize)
        {
            var scale = ScreenBands.ContentScale(available, pageSize);
            fit.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Shows <paramref name="page"/> alone and opens it with the pop.</summary>
        private void ShowPage(RectTransform page)
        {
            titleFit.gameObject.SetActive(page == titlePage);
            if (levelsFit != null)
            {
                levelsFit.gameObject.SetActive(page == levelsPage);
            }

            DOTween.Kill(this);
            ShowAtRest();
            group.alpha = 0f;
            page.localScale = Vector3.one * config.ResultPopStartScale;

            DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 1f, config.ResultPopSeconds)
                .SetEase(config.ResultPopEase)
                .SetUpdate(true)
                .SetId(this);
            page.DOScale(1f, config.ResultPopSeconds)
                .SetEase(config.ResultPopEase)
                .SetUpdate(true)
                .SetId(this);
        }

        /// <summary>The menu's look once a pop has finished.</summary>
        private void ShowAtRest()
        {
            group.alpha = 1f;
            titlePage.localScale = Vector3.one;
            if (levelsPage != null)
            {
                levelsPage.localScale = Vector3.one;
            }
        }

        /// <summary>The title page, rows from the top: the game's name, Play, Levels.</summary>
        private void BuildTitlePage()
        {
            var buttonSize = config.MenuButtonSizeUnits;
            titlePageSize = new Vector2(
                Mathf.Max(config.MenuTitleWidthUnits, buttonSize.x),
                config.MenuTitleHeightUnits + config.MenuSectionGapUnits + 2f * buttonSize.y + config.MenuButtonGapUnits);

            // Fit scales the page down to the safe area; the pop scales Page.
            titleFit = UiBuilder.CreateRect("Title fit", safe);
            UiBuilder.PlaceCentered(titleFit, titlePageSize);
            titlePage = UiBuilder.CreateRect("Title page", titleFit);
            UiBuilder.PlaceCentered(titlePage, titlePageSize);

            var top = 0f;
            BuildHeading(titlePage, titlePageSize.x, config.GameTitle, config.MenuTitleFontSize);
            top += config.MenuTitleHeightUnits + config.MenuSectionGapUnits;

            playButton = BuildButton(titlePage, "Play", config.NextButtonColor, config.PlayLabel);
            UiBuilder.Place((RectTransform)playButton.transform, TopMiddle, new Vector2(0f, -top), buttonSize);
            top += buttonSize.y + config.MenuButtonGapUnits;

            levelsButton = BuildButton(titlePage, "Levels", config.MenuButtonColor, config.LevelsLabel);
            UiBuilder.Place((RectTransform)levelsButton.transform, TopMiddle, new Vector2(0f, -top), buttonSize);
        }

        /// <summary>
        /// The level-select page, rows from the top: the heading, the grid of
        /// level buttons, Back. Replaces the page built before; with no
        /// entries there is no page.
        /// </summary>
        private void BuildLevelsPage(IReadOnlyList<MenuLevelEntry> entries)
        {
            // The old page's buttons go with it; nothing is left listening.
            var wasSubscribed = isSubscribed;
            Unsubscribe();
            levelButtons.Clear();
            backButton = null;
            if (levelsFit != null)
            {
                Destroy(levelsFit.gameObject);
                levelsFit = null;
                levelsPage = null;
            }

            if (entries.Count > 0)
            {
                var columns = config.LevelSelectColumns;
                var rows = (entries.Count + columns - 1) / columns;
                var tile = config.LevelTileSizeUnits;
                var pitch = tile + config.LevelTileGapUnits;
                var gridSize = new Vector2(
                    columns * tile + (columns - 1) * config.LevelTileGapUnits,
                    rows * tile + (rows - 1) * config.LevelTileGapUnits);
                var buttonSize = config.MenuButtonSizeUnits;

                levelsPageSize = new Vector2(
                    Mathf.Max(gridSize.x, buttonSize.x),
                    config.MenuTitleHeightUnits + gridSize.y + buttonSize.y + 2f * config.MenuSectionGapUnits);

                levelsFit = UiBuilder.CreateRect("Levels fit", safe);
                UiBuilder.PlaceCentered(levelsFit, levelsPageSize);
                levelsPage = UiBuilder.CreateRect("Levels page", levelsFit);
                UiBuilder.PlaceCentered(levelsPage, levelsPageSize);

                BuildHeading(levelsPage, levelsPageSize.x, config.LevelSelectTitle, config.LevelSelectTitleFontSize);

                var grid = UiBuilder.CreateRect("Grid", levelsPage);
                UiBuilder.Place(
                    grid, TopMiddle, new Vector2(0f, -(config.MenuTitleHeightUnits + config.MenuSectionGapUnits)), gridSize);
                for (var i = 0; i < entries.Count; i++)
                {
                    var position = new Vector2(i % columns * pitch, -(i / columns) * pitch);
                    BuildLevelButton(grid, entries[i], position);
                }

                backButton = BuildButton(levelsPage, "Back", config.MenuButtonColor, config.BackLabel);
                UiBuilder.Place((RectTransform)backButton.transform, BottomMiddle, Vector2.zero, buttonSize);

                levelsFit.gameObject.SetActive(false);
            }

            if (wasSubscribed)
            {
                Subscribe();
            }
        }

        /// <summary>A one-line label filling the top row of <paramref name="page"/>.</summary>
        private void BuildHeading(RectTransform page, float width, string text, float fontSize)
        {
            var rect = UiBuilder.CreateRect("Heading", page);
            UiBuilder.Place(rect, TopMiddle, Vector2.zero, new Vector2(width, config.MenuTitleHeightUnits));
            UiBuilder.AddLabel(rect, config.LabelFont, fontSize, config.MenuTitleColor, false).text = text;
        }

        /// <summary>
        /// One level's button: a rounded square with the level's number, in
        /// the completed tint and with a tick in its top right corner when the
        /// level is completed. Completed or not, it asks for its level.
        /// </summary>
        private void BuildLevelButton(RectTransform grid, MenuLevelEntry entry, Vector2 position)
        {
            var rect = UiBuilder.CreateRect($"Level {entry.Number}", grid);
            var size = Vector2.one * config.LevelTileSizeUnits;
            UiBuilder.Place(rect, TopLeft, position, size);
            var color = entry.IsCompleted ? config.LevelTileCompletedColor : config.LevelTileColor;
            var face = UiBuilder.AddRoundedBox(
                rect, size, config.RoundedRectSprite, color, config.LevelTileCornerUnits, referencePixelsPerUnit);

            var label = UiBuilder.CreateRect("Number", rect);
            UiBuilder.Stretch(label);
            UiBuilder.AddLabel(label, config.LabelFont, config.LevelTileFontSize, config.ResultButtonTextColor, false).text =
                entry.Number.ToString(CultureInfo.InvariantCulture);

            if (entry.IsCompleted)
            {
                BuildTick(rect);
            }

            var levelName = entry.Name;
            levelButtons.Add(new LevelButton(UiBuilder.AddButton(rect, face), () => OnLevelClicked(levelName)));
        }

        /// <summary>The completed mark: a tick of two rounded strokes in a square in the button's top right corner.</summary>
        private void BuildTick(RectTransform tile)
        {
            var side = config.LevelTickSizeUnits;
            var inset = config.LevelTickInsetUnits;
            var tick = UiBuilder.CreateRect("Tick", tile);
            UiBuilder.Place(tick, TopRight, new Vector2(-inset, -inset), Vector2.one * side);

            var vertex = TickVertex * side;
            BuildTickStroke(tick, vertex, TickShortStrokeLength * side, TickShortStrokeDegrees);
            BuildTickStroke(tick, vertex, TickLongStrokeLength * side, TickLongStrokeDegrees);
        }

        /// <summary>A stroke of <paramref name="length"/> starting at <paramref name="vertex"/> and running at <paramref name="degrees"/>.</summary>
        private void BuildTickStroke(RectTransform tick, Vector2 vertex, float length, float degrees)
        {
            var stroke = UiBuilder.CreateRect("Tick stroke", tick);
            var size = new Vector2(length, config.LevelTickThicknessUnits);
            UiBuilder.PlaceCentered(stroke, size);
            var radians = degrees * Mathf.Deg2Rad;
            stroke.anchoredPosition = vertex + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (length * 0.5f);
            stroke.localRotation = Quaternion.Euler(0f, 0f, degrees);
            UiBuilder.AddRoundedBox(
                    stroke, size, config.RoundedRectSprite, config.LevelTickColor, size.y * 0.5f, referencePixelsPerUnit)
                .raycastTarget = false;
        }

        private Button BuildButton(RectTransform parent, string buttonName, Color color, string label)
        {
            var rect = UiBuilder.CreateRect(buttonName, parent);
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
        /// Listens to every button once. Called from <see cref="Initialize"/>
        /// and after the level select is rebuilt, as well as from
        /// <see cref="OnEnable"/>: the buttons do not exist until then.
        /// </summary>
        private void Subscribe()
        {
            if (isSubscribed || playButton == null || levelsButton == null)
            {
                return;
            }

            playButton.onClick.AddListener(OnPlayClicked);
            levelsButton.onClick.AddListener(OnLevelsClicked);
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
            }

            for (var i = 0; i < levelButtons.Count; i++)
            {
                levelButtons[i].Button.onClick.AddListener(levelButtons[i].OnClick);
            }

            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayClicked);
            }

            if (levelsButton != null)
            {
                levelsButton.onClick.RemoveListener(OnLevelsClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBackClicked);
            }

            for (var i = 0; i < levelButtons.Count; i++)
            {
                if (levelButtons[i].Button != null)
                {
                    levelButtons[i].Button.onClick.RemoveListener(levelButtons[i].OnClick);
                }
            }

            isSubscribed = false;
        }

        private void OnPlayClicked()
        {
            PlayRequested?.Invoke();
        }

        private void OnLevelsClicked()
        {
            if (levelsPage != null)
            {
                ShowPage(levelsPage);
            }
        }

        private void OnBackClicked()
        {
            ShowPage(titlePage);
        }

        private void OnLevelClicked(string levelName)
        {
            LevelRequested?.Invoke(levelName);
        }

        /// <summary>A level's button with the listener made for it, kept so the listener can be removed again.</summary>
        private readonly struct LevelButton
        {
            public Button Button { get; }

            public UnityAction OnClick { get; }

            public LevelButton(Button button, UnityAction onClick)
            {
                Button = button;
                OnClick = onClick;
            }
        }
    }
}
