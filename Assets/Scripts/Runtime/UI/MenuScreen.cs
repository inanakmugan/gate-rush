using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>The page <see cref="MenuScreen"/> shows.</summary>
    public enum MenuPage
    {
        /// <summary>The game's name, the meta line, Play and Settings.</summary>
        Title,

        /// <summary>The sound button, Levels and Back.</summary>
        Settings,

        /// <summary>The level select: one button per level, and Back to Settings.</summary>
        Levels,

        /// <summary>No lives left: the time to the next life, Refill and Back.</summary>
        NoLives
    }

    /// <summary>
    /// The prototype menu (Module 23, D50; Module 32, D58, D64): a title page
    /// with the game's name, a meta line with the gold and the lives, Play
    /// and Settings; a Settings page with the sound button (Module 25),
    /// Levels and Back; a level-select page with one button per level and
    /// Back; and a no-lives page with the time to the next life, a refill
    /// button and Back. An overlay in the Level scene, drawn over the
    /// background while no level is loaded. Builds its hierarchy from code
    /// and holds no rules — the bootstrap says which levels to list
    /// (<see cref="MenuLevels.Entries"/>), writes every meta text, says
    /// whether the refill is affordable, and acts on what a button asks for.
    /// </summary>
    /// <remarks>
    /// <para>Put this component on the screen-space canvas, which stays
    /// active; it builds one <c>Menu</c> child under its own transform, last
    /// among the canvas's children, and shows and hides that child. Call
    /// <see cref="Initialize"/> before anything else.</para>
    /// <para><b>No level is locked.</b> Every entry gets a button that asks
    /// for its level, completed or not; a completed one only looks different:
    /// its own tint and a tick. A level started from the select is free
    /// play (D58).</para>
    /// <para><b>Pages.</b> Settings, the level select and every Back move
    /// between pages here; nothing for the bootstrap to decide. Play,
    /// a level's button, the sound button and Refill ask the bootstrap.
    /// Without a level to list, Levels is hidden and Back moves up into its
    /// row.</para>
    /// <para><b>Texts.</b> The meta line and the no-lives page's line are set
    /// by the bootstrap, only when they change; this component formats
    /// nothing.</para>
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

        // The rows of buttons on each page, from the top. Not tunables: they
        // are the pages' order. Settings' Back moves up into Levels' row
        // while Levels is hidden.
        private const int PlayRow = 0;
        private const int SettingsRow = 1;
        private const int TitleButtonRows = 2;
        private const int SoundRow = 0;
        private const int LevelsRow = 1;
        private const int SettingsBackRow = 2;
        private const int SettingsButtonRows = 3;

        private readonly List<LevelButton> levelButtons = new List<LevelButton>();

        private RuntimeConfig config;
        private float referencePixelsPerUnit;
        private RectTransform root;
        private CanvasGroup group;
        private RectTransform safe;

        private PageView titlePage;
        private Button playButton;
        private Button settingsButton;
        private TMP_Text metaLabel;
        private TMP_Text noticeLabel;

        private PageView settingsPage;
        private Button soundButton;
        private Button levelsButton;
        private Button settingsBackButton;
        private TMP_Text soundLabel;

        // What the sound button reads; kept so a menu built again reads the same.
        private bool isSoundOn = true;

        private PageView noLivesPage;
        private TMP_Text nextLifeLabel;
        private Button refillButton;
        private TMP_Text refillLabel;
        private Button noLivesBackButton;

        // Null while there is no level to list.
        private PageView levelsPage;
        private Button levelsBackButton;

        private Vector2 fittedSafeSize;
        private bool isSubscribed;

        /// <summary>Raised when the player presses Play: the campaign (D58).</summary>
        public event Action PlayRequested;

        /// <summary>Raised when the player presses a level's button, with that level's file name: free play (D58).</summary>
        public event Action<string> LevelRequested;

        /// <summary>
        /// Raised when the player presses the sound button (Module 25). The
        /// listener changes the setting and answers with <see cref="ShowSound"/>.
        /// </summary>
        public event Action SoundToggleRequested;

        /// <summary>
        /// Raised when the player presses the no-lives page's refill button,
        /// which is only pressable when <see cref="ShowNoLives"/> said the
        /// refill is affordable (D62).
        /// </summary>
        public event Action RefillRequested;

        /// <summary>
        /// Raised when the player presses any button of the menu, after the
        /// button's own event — so a level that starts, or sound that was just
        /// turned on, is in place before the press itself sounds (Module 25).
        /// </summary>
        public event Action ButtonPressed;

        /// <summary>True from <see cref="ShowTitle"/> until <see cref="Hide"/>.</summary>
        public bool IsOpen => root != null && root.gameObject.activeSelf;

        /// <summary>The page showing; meaningful only while <see cref="IsOpen"/>.</summary>
        public MenuPage Page { get; private set; }

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
            levelsBackButton = null;
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
            BuildSettingsPage();
            BuildNoLivesPage();

            fittedSafeSize = Vector2.negativeInfinity;
            root.gameObject.SetActive(false);

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>
        /// Opens the menu on its title page reading
        /// <paramref name="metaLine"/>, with no notice, and with the level
        /// select rebuilt from <paramref name="entries"/> — one button per
        /// entry, in order. With no entries the Levels button is hidden:
        /// there is nothing to select.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="entries"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Initialize"/> has not been called.</exception>
        public void ShowTitle(IReadOnlyList<MenuLevelEntry> entries, string metaLine)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            RequireInitialized(nameof(ShowTitle));

            BuildLevelsPage(entries);
            var hasLevels = entries.Count > 0;
            levelsButton.gameObject.SetActive(hasLevels);
            PlaceButton(settingsBackButton, SettingsButtonTop, hasLevels ? SettingsBackRow : LevelsRow, config.MenuButtonSizeUnits);

            metaLabel.text = metaLine;
            noticeLabel.text = string.Empty;

            fittedSafeSize = Vector2.negativeInfinity;
            root.gameObject.SetActive(true);
            Fit();
            ShowPage(MenuPage.Title);
        }

        /// <summary>
        /// Sets the title page's meta line, on whichever page shows, so the
        /// title page reads it when it is next shown. Does nothing before
        /// <see cref="Initialize"/>.
        /// </summary>
        public void ShowMeta(string metaLine)
        {
            if (metaLabel != null)
            {
                metaLabel.text = metaLine;
            }
        }

        /// <summary>
        /// Opens the title page with <paramref name="text"/> under the meta
        /// line — the notice after the last campaign level. It stays until
        /// the title page is left or opened again by <see cref="ShowTitle"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The menu is not open.</exception>
        public void ShowNotice(string text)
        {
            RequireOpen(nameof(ShowNotice));
            if (Page != MenuPage.Title)
            {
                ShowPage(MenuPage.Title);
            }

            noticeLabel.text = text;
        }

        /// <summary>
        /// Opens the no-lives page reading <paramref name="nextLifeLine"/>,
        /// with the refill button labelled <paramref name="refillText"/> and
        /// pressable only when <paramref name="canRefill"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The menu is not open.</exception>
        public void ShowNoLives(string nextLifeLine, string refillText, bool canRefill)
        {
            RequireOpen(nameof(ShowNoLives));
            nextLifeLabel.text = nextLifeLine;
            refillLabel.text = refillText;
            refillButton.interactable = canRefill;
            ShowPage(MenuPage.NoLives);
        }

        /// <summary>Sets the no-lives page's line, for the once-a-second refresh. Does nothing before <see cref="Initialize"/>.</summary>
        public void ShowNextLife(string nextLifeLine)
        {
            if (nextLifeLabel != null)
            {
                nextLifeLabel.text = nextLifeLine;
            }
        }

        /// <summary>Opens the title page, as its Back buttons do.</summary>
        /// <exception cref="InvalidOperationException">The menu is not open.</exception>
        public void ShowTitlePage()
        {
            RequireOpen(nameof(ShowTitlePage));
            ShowPage(MenuPage.Title);
        }

        /// <summary>
        /// Makes the sound button read on or off, now and whenever the menu
        /// is built again. May be called before <see cref="Initialize"/>.
        /// </summary>
        public void ShowSound(bool isOn)
        {
            isSoundOn = isOn;
            if (soundLabel != null)
            {
                soundLabel.text = isOn ? config.SoundOnLabel : config.SoundOffLabel;
            }
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

        private void RequireInitialized(string member)
        {
            if (root == null)
            {
                throw new InvalidOperationException($"{name}: MenuScreen.{member} was called before Initialize.");
            }
        }

        private void RequireOpen(string member)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException($"{name}: MenuScreen.{member} was called while the menu is not open; call ShowTitle first.");
            }
        }

        /// <summary>
        /// Scales every page down to the safe area less the padding, when the
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
            titlePage.ScaleToFit(available);
            settingsPage.ScaleToFit(available);
            noLivesPage.ScaleToFit(available);
            levelsPage?.ScaleToFit(available);
        }

        /// <summary>Shows <paramref name="page"/> alone and opens it with the pop. The title page's notice goes when the page changes.</summary>
        private void ShowPage(MenuPage page)
        {
            if (page != Page)
            {
                noticeLabel.text = string.Empty;
            }

            Page = page;
            var shown = PageOf(page);
            titlePage.SetActive(shown == titlePage);
            settingsPage.SetActive(shown == settingsPage);
            noLivesPage.SetActive(shown == noLivesPage);
            levelsPage?.SetActive(shown == levelsPage);

            DOTween.Kill(this);
            ShowAtRest();
            group.alpha = 0f;
            shown.Content.localScale = Vector3.one * config.ResultPopStartScale;

            DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 1f, config.ResultPopSeconds)
                .SetEase(config.ResultPopEase)
                .SetUpdate(true)
                .SetId(this);
            shown.Content.DOScale(1f, config.ResultPopSeconds)
                .SetEase(config.ResultPopEase)
                .SetUpdate(true)
                .SetId(this);
        }

        private PageView PageOf(MenuPage page)
        {
            switch (page)
            {
                case MenuPage.Title:
                    return titlePage;
                case MenuPage.Settings:
                    return settingsPage;
                case MenuPage.NoLives:
                    return noLivesPage;
                case MenuPage.Levels:
                    // The Levels button is hidden while there is no page.
                    return levelsPage ?? settingsPage;
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), page, "Not a menu page.");
            }
        }

        /// <summary>The menu's look once a pop has finished.</summary>
        private void ShowAtRest()
        {
            group.alpha = 1f;
            titlePage.Content.localScale = Vector3.one;
            settingsPage.Content.localScale = Vector3.one;
            noLivesPage.Content.localScale = Vector3.one;
            if (levelsPage != null)
            {
                levelsPage.Content.localScale = Vector3.one;
            }
        }

        /// <summary>The top of the title page's first button row, from the page's top.</summary>
        private float TitleButtonTop =>
            config.MenuTitleHeightUnits + config.MenuMetaHeightUnits + config.MenuNoticeHeightUnits + config.MenuSectionGapUnits;

        /// <summary>The top of the Settings and no-lives pages' first button row, from the page's top.</summary>
        private float SettingsButtonTop => config.MenuTitleHeightUnits + config.MenuSectionGapUnits;

        private float NoLivesButtonTop => config.MenuTitleHeightUnits + config.MenuMetaHeightUnits + config.MenuSectionGapUnits;

        /// <summary>
        /// The title page, rows from the top: the game's name, the meta line,
        /// the notice (empty until set), Play, Settings.
        /// </summary>
        private void BuildTitlePage()
        {
            var buttonSize = config.MenuButtonSizeUnits;
            var size = new Vector2(
                Mathf.Max(config.MenuTitleWidthUnits, buttonSize.x),
                TitleButtonTop + ButtonRowsHeight(TitleButtonRows));
            titlePage = new PageView("Title", safe, size);

            BuildHeading(titlePage.Content, size.x, config.GameTitle, config.MenuTitleFontSize);
            metaLabel = BuildLine(
                titlePage.Content, "Meta", config.MenuTitleHeightUnits, size.x, config.MenuMetaHeightUnits, config.MenuMetaFontSize, false);
            noticeLabel = BuildLine(
                titlePage.Content, "Notice", config.MenuTitleHeightUnits + config.MenuMetaHeightUnits, size.x,
                config.MenuNoticeHeightUnits, config.MenuNoticeFontSize, true);

            playButton = BuildButton(titlePage.Content, "Play", config.NextButtonColor, config.PlayLabel, buttonSize, out _);
            PlaceButton(playButton, TitleButtonTop, PlayRow, buttonSize);

            settingsButton = BuildButton(titlePage.Content, "Settings", config.MenuButtonColor, config.SettingsLabel, buttonSize, out _);
            PlaceButton(settingsButton, TitleButtonTop, SettingsRow, buttonSize);
        }

        /// <summary>The Settings page, rows from the top: its heading, the sound button, Levels, Back.</summary>
        private void BuildSettingsPage()
        {
            var buttonSize = config.MenuButtonSizeUnits;
            var size = new Vector2(
                Mathf.Max(config.MenuTitleWidthUnits, buttonSize.x),
                SettingsButtonTop + ButtonRowsHeight(SettingsButtonRows));
            settingsPage = new PageView("Settings", safe, size);

            BuildHeading(settingsPage.Content, size.x, config.SettingsTitle, config.LevelSelectTitleFontSize);

            soundButton = BuildButton(settingsPage.Content, "Sound", config.MenuButtonColor, config.SoundOnLabel, buttonSize, out soundLabel);
            PlaceButton(soundButton, SettingsButtonTop, SoundRow, buttonSize);
            ShowSound(isSoundOn);

            levelsButton = BuildButton(settingsPage.Content, "Levels", config.MenuButtonColor, config.LevelsLabel, buttonSize, out _);
            PlaceButton(levelsButton, SettingsButtonTop, LevelsRow, buttonSize);

            settingsBackButton = BuildButton(settingsPage.Content, "Back", config.MenuButtonColor, config.BackLabel, buttonSize, out _);
            PlaceButton(settingsBackButton, SettingsButtonTop, SettingsBackRow, buttonSize);
        }

        /// <summary>The no-lives page, rows from the top: its heading, the time to the next life, Refill, Back.</summary>
        private void BuildNoLivesPage()
        {
            var buttonSize = config.MenuButtonSizeUnits;
            var wideSize = config.MenuWideButtonSizeUnits;
            var size = new Vector2(
                Mathf.Max(config.MenuTitleWidthUnits, Mathf.Max(buttonSize.x, wideSize.x)),
                NoLivesButtonTop + wideSize.y + config.MenuButtonGapUnits + buttonSize.y);
            noLivesPage = new PageView("No lives", safe, size);

            BuildHeading(noLivesPage.Content, size.x, config.NoLivesTitle, config.LevelSelectTitleFontSize);
            nextLifeLabel = BuildLine(
                noLivesPage.Content, "Next life", config.MenuTitleHeightUnits, size.x, config.MenuMetaHeightUnits,
                config.MenuMetaFontSize, false);

            refillButton = BuildButton(noLivesPage.Content, "Refill", config.NextButtonColor, string.Empty, wideSize, out refillLabel);
            PlaceButton(refillButton, NoLivesButtonTop, 0, wideSize);

            // Refill may be taller than the other buttons, so Back is placed
            // from Refill's foot rather than by row.
            noLivesBackButton = BuildButton(noLivesPage.Content, "Back", config.MenuButtonColor, config.BackLabel, buttonSize, out _);
            PlaceButton(noLivesBackButton, NoLivesButtonTop + wideSize.y + config.MenuButtonGapUnits, 0, buttonSize);
        }

        private float ButtonRowsHeight(int rows)
        {
            var buttonSize = config.MenuButtonSizeUnits;
            return rows * buttonSize.y + (rows - 1) * config.MenuButtonGapUnits;
        }

        /// <summary>
        /// Places a button of <paramref name="size"/> in row
        /// <paramref name="row"/> of a column of menu-button rows whose first
        /// row's top is <paramref name="top"/> below the page's top.
        /// </summary>
        private void PlaceButton(Button button, float top, int row, Vector2 size)
        {
            var rowTop = top + row * (config.MenuButtonSizeUnits.y + config.MenuButtonGapUnits);
            UiBuilder.Place((RectTransform)button.transform, TopMiddle, new Vector2(0f, -rowTop), size);
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
            levelsBackButton = null;
            if (levelsPage != null)
            {
                levelsPage.Destroy();
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

                var size = new Vector2(
                    Mathf.Max(gridSize.x, buttonSize.x),
                    config.MenuTitleHeightUnits + gridSize.y + buttonSize.y + 2f * config.MenuSectionGapUnits);
                levelsPage = new PageView("Levels", safe, size);

                BuildHeading(levelsPage.Content, size.x, config.LevelSelectTitle, config.LevelSelectTitleFontSize);

                var grid = UiBuilder.CreateRect("Grid", levelsPage.Content);
                UiBuilder.Place(
                    grid, TopMiddle, new Vector2(0f, -(config.MenuTitleHeightUnits + config.MenuSectionGapUnits)), gridSize);
                for (var i = 0; i < entries.Count; i++)
                {
                    var position = new Vector2(i % columns * pitch, -(i / columns) * pitch);
                    BuildLevelButton(grid, entries[i], position);
                }

                levelsBackButton = BuildButton(levelsPage.Content, "Back", config.MenuButtonColor, config.BackLabel, buttonSize, out _);
                UiBuilder.Place((RectTransform)levelsBackButton.transform, BottomMiddle, Vector2.zero, buttonSize);

                levelsPage.SetActive(false);
                fittedSafeSize = Vector2.negativeInfinity;
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

        /// <summary>An empty line of text across <paramref name="page"/>, <paramref name="top"/> below its top.</summary>
        private TMP_Text BuildLine(RectTransform page, string lineName, float top, float width, float height, float fontSize, bool wraps)
        {
            var rect = UiBuilder.CreateRect(lineName, page);
            UiBuilder.Place(rect, TopMiddle, new Vector2(0f, -top), new Vector2(width, height));
            var label = UiBuilder.AddLabel(rect, config.LabelFont, fontSize, config.MenuTitleColor, wraps);
            label.text = string.Empty;
            return label;
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

        private Button BuildButton(RectTransform parent, string buttonName, Color color, string label, Vector2 size, out TMP_Text labelText)
        {
            var rect = UiBuilder.CreateRect(buttonName, parent);
            rect.sizeDelta = size;
            var face = UiBuilder.AddRoundedBox(
                rect, size, config.RoundedRectSprite, color, config.MenuButtonCornerUnits, referencePixelsPerUnit);

            var text = UiBuilder.CreateRect("Label", rect);
            UiBuilder.Stretch(text);
            labelText = UiBuilder.AddLabel(text, config.LabelFont, config.MenuButtonFontSize, config.ResultButtonTextColor, false);
            labelText.text = label;

            return UiBuilder.AddButton(rect, face);
        }

        /// <summary>
        /// Listens to every button once. Called from <see cref="Initialize"/>
        /// and after the level select is rebuilt, as well as from
        /// <see cref="OnEnable"/>: the buttons do not exist until then.
        /// </summary>
        private void Subscribe()
        {
            if (isSubscribed || playButton == null)
            {
                return;
            }

            playButton.onClick.AddListener(OnPlayClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
            soundButton.onClick.AddListener(OnSoundClicked);
            levelsButton.onClick.AddListener(OnLevelsClicked);
            settingsBackButton.onClick.AddListener(OnTitleBackClicked);
            refillButton.onClick.AddListener(OnRefillClicked);
            noLivesBackButton.onClick.AddListener(OnTitleBackClicked);
            if (levelsBackButton != null)
            {
                levelsBackButton.onClick.AddListener(OnSettingsClicked);
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

            RemoveListener(playButton, OnPlayClicked);
            RemoveListener(settingsButton, OnSettingsClicked);
            RemoveListener(soundButton, OnSoundClicked);
            RemoveListener(levelsButton, OnLevelsClicked);
            RemoveListener(settingsBackButton, OnTitleBackClicked);
            RemoveListener(refillButton, OnRefillClicked);
            RemoveListener(noLivesBackButton, OnTitleBackClicked);
            RemoveListener(levelsBackButton, OnSettingsClicked);

            for (var i = 0; i < levelButtons.Count; i++)
            {
                RemoveListener(levelButtons[i].Button, levelButtons[i].OnClick);
            }

            isSubscribed = false;
        }

        /// <summary>Removes <paramref name="listener"/> from <paramref name="button"/>, which may be gone with the scene.</summary>
        private static void RemoveListener(Button button, UnityAction listener)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(listener);
            }
        }

        private void OnPlayClicked()
        {
            PlayRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnSettingsClicked()
        {
            ShowPage(MenuPage.Settings);
            ButtonPressed?.Invoke();
        }

        private void OnLevelsClicked()
        {
            if (levelsPage != null)
            {
                ShowPage(MenuPage.Levels);
            }

            ButtonPressed?.Invoke();
        }

        private void OnSoundClicked()
        {
            SoundToggleRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnRefillClicked()
        {
            RefillRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnTitleBackClicked()
        {
            ShowPage(MenuPage.Title);
            ButtonPressed?.Invoke();
        }

        private void OnLevelClicked(string levelName)
        {
            LevelRequested?.Invoke(levelName);
            ButtonPressed?.Invoke();
        }

        /// <summary>
        /// One page of the menu: a rect the fit scales down to the safe area,
        /// and inside it the content the pop scales.
        /// </summary>
        private sealed class PageView
        {
            private readonly RectTransform fit;
            private readonly Vector2 size;

            public PageView(string pageName, RectTransform parent, Vector2 size)
            {
                this.size = size;
                fit = UiBuilder.CreateRect($"{pageName} fit", parent);
                UiBuilder.PlaceCentered(fit, size);
                Content = UiBuilder.CreateRect($"{pageName} page", fit);
                UiBuilder.PlaceCentered(Content, size);
                fit.gameObject.SetActive(false);
            }

            /// <summary>What the page shows; the pop scales it.</summary>
            public RectTransform Content { get; }

            public void SetActive(bool isActive) => fit.gameObject.SetActive(isActive);

            public void ScaleToFit(Vector2 available)
            {
                var scale = ScreenBands.ContentScale(available, size);
                fit.localScale = new Vector3(scale, scale, 1f);
            }

            public void Destroy() => UnityEngine.Object.Destroy(fit.gameObject);
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
