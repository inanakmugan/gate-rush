using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GateRush.UI.Editor
{
    /// <summary>
    /// <i>Gate Rush → Build UI Prefabs</i>: builds the Module 35 prefabs the
    /// owner does not lay out by hand — Store Page, Leaderboard Page, the
    /// Settings, Levels and Lives overlays, and Front End — from code, with
    /// every serialized field wired.
    /// </summary>
    /// <remarks>
    /// <para><b>The hand-made prefabs are never written.</b> They are read
    /// from <see cref="HandMadeFolder"/> and placed as nested instances, so
    /// they stay linked; every change made to an instance is recorded as an
    /// override of that instance only.</para>
    /// <para><b>Nothing is written when an input is missing.</b> Every asset
    /// the build reads is loaded and checked before the first prefab is
    /// saved; a sprite must import as exactly one sprite, so a sliced sheet
    /// is refused instead of a slice being picked.</para>
    /// <para><b>The open scene is never touched.</b> Prefabs are assembled in
    /// a preview scene that is closed when the build ends.</para>
    /// <para>Overwriting keeps each prefab's GUID, but the objects inside get
    /// new file IDs, so a scene reference into Front End is checked again
    /// after a rebuild.</para>
    /// </remarks>
    public static class UiPrefabBuilder
    {
        /// <summary>Where the hand-made prefabs are read from, whatever the output folder.</summary>
        public const string HandMadeFolder = "Assets/Prefabs/UI";

        /// <summary>Where the menu writes the built prefabs.</summary>
        public const string MenuOutputFolder = "Assets/Prefabs/UI";

        private const string PrefabExtension = ".prefab";
        private const string LogPrefix = "Build UI Prefabs";

        // Inputs.
        private const string UiConfigPath = "Assets/Config/UiConfig.asset";
        private const string PanelSpritePath = "Assets/ThirdParty/Kenney/UIPack/Grey/button_square_flat.png";
        private const string SoundOnSpritePath = "Assets/ThirdParty/Kenney/GameIcons/audioOn.png";
        private const string SoundOffSpritePath = "Assets/ThirdParty/Kenney/GameIcons/audioOff.png";
        private const string HeartSpritePath = "Assets/ThirdParty/Kenney/BoardGameIcons/suit_hearts.png";
        private const string UiLayerName = "UI";

        // Hand-made prefabs.
        private const string ButtonCloseName = "Button Close";
        private const string ButtonPrimaryName = "Button Primary";
        private const string ButtonRoundName = "Button Round";
        private const string HomePageName = "Home Page";
        private const string LevelButtonName = "Level Button";
        private const string TabBarName = "Tab Bar";
        private const string TopBarName = "Top Bar";

        // The child of Button Round that the sound looks replace.
        private const string ButtonRoundIconName = "Icon";

        // Built prefabs.
        private const string StorePageName = "Store Page";
        private const string LeaderboardPageName = "Leaderboard Page";
        private const string SettingsOverlayName = "Settings Overlay";
        private const string LevelsOverlayName = "Levels Overlay";
        private const string LivesOverlayName = "Lives Overlay";
        private const string FrontEndName = "Front End";

        // Child objects.
        private const string TitleName = "Title";
        private const string ScrimName = "Scrim";
        private const string PanelName = "Panel";
        private const string SoundRowName = "Sound Row";
        private const string SoundLabelName = "Label";
        private const string SoundOnLookName = "Sound On Look";
        private const string SoundOffLookName = "Sound Off Look";
        private const string ScrollViewName = "Scroll View";
        private const string ViewportName = "Viewport";
        private const string ContentName = "Content";
        private const string HeartName = "Heart";
        private const string LivesLabelName = "Lives";
        private const string StatusLabelName = "Status";
        private const string BackgroundName = "Background";
        private const string SafeName = "Safe";
        private const string PagesName = "Pages";
        private const string OverlaysName = "Overlays";

        // Texts. The Lives overlay's labels are overwritten at runtime; these
        // only show in the Editor.
        private const string StoreTitle = "Store";
        private const string LeaderboardTitle = "Leaderboard";
        private const string SettingsTitle = "Settings";
        private const string LevelsTitle = "Levels";
        private const string LivesTitle = "Lives";
        private const string SoundText = "Sound";
        private const string LevelsButtonText = "Levels";
        private const string RefillText = "Refill";
        private const string LivesPlaceholder = "5/5";
        private const string StatusPlaceholder = "Full";

        // Colours.
        private const string PageTitleColorHex = "#FFFFFF";
        private const string OverlayTextColorHex = "#3B2466";
        private const string LivesLabelColorHex = "#FFFFFF";
        private const string IconColorHex = "#FFFFFF";
        private const string ScrimColorHex = "#000000";
        private const byte ScrimAlpha = 153;
        private const string ViewportColorHex = "#FFFFFF00";

        // Anchor and pivot coordinates: a parent's low edge, middle and high edge.
        private const float AnchorLow = 0f;
        private const float AnchorMid = 0.5f;
        private const float AnchorHigh = 1f;

        // Store and Leaderboard pages.
        private const float PageTitleFontSize = 90f;
        private const float PageTitlePosY = -20f;
        private const float PageTitleHeight = 140f;

        // Every overlay.
        private const float OverlayTitleFontSize = 80f;
        private const float OverlayTitlePosY = -30f;
        private const float OverlayTitleHeight = 110f;
        private const float CloseButtonInset = 10f;
        private const float PanelButtonPosY = 70f;

        // Settings overlay.
        private const float SettingsPanelWidth = 820f;
        private const float SettingsPanelHeight = 700f;
        private const float SoundRowInset = 80f;
        private const float SoundRowPosY = -200f;
        private const float SoundRowHeight = 140f;
        private const float SoundLabelWidthFraction = 0.5f;
        private const float SoundLabelFontSize = 64f;

        // Levels overlay.
        private const float LevelsPanelWidth = 900f;
        private const float LevelsPanelHeight = 1400f;
        private const float ScrollInset = 40f;
        private const float ScrollTop = 160f;
        private const int LevelGridColumns = 4;

        // Lives overlay.
        private const float LivesPanelWidth = 820f;
        private const float LivesPanelHeight = 800f;
        private const float HeartSize = 240f;
        private const float HeartPosY = -160f;
        private const float LivesFontSize = 72f;
        private const float StatusPosY = -420f;
        private const float StatusHeight = 90f;
        private const float StatusFontSize = 56f;

        // The bar heights the layout was planned with. The build uses the
        // heights on the prefabs and only warns when they differ.
        private const float ExpectedTopBarHeight = 160f;
        private const float ExpectedTabBarHeight = 220f;

        // Serialized field names of the components wired here.
        private const string TintRoleField = "role";
        private const string TintTargetField = "target";
        private const string CloseButtonField = "closeButton";
        private const string SoundButtonField = "soundButton";
        private const string SoundOnLookField = "soundOnLook";
        private const string SoundOffLookField = "soundOffLook";
        private const string LevelsButtonField = "levelsButton";
        private const string GridField = "grid";
        private const string LevelButtonPrefabField = "levelButtonPrefab";
        private const string LivesLabelField = "livesLabel";
        private const string StatusLabelField = "statusLabel";
        private const string RefillButtonField = "refillButton";
        private const string RefillLabelField = "refillLabel";
        private const string ConfigField = "config";
        private const string StorePageField = "storePage";
        private const string HomeField = "home";
        private const string LeaderboardPageField = "leaderboardPage";
        private const string TopBarField = "topBar";
        private const string TabBarField = "tabBar";
        private const string SettingsField = "settings";
        private const string LevelsField = "levels";
        private const string LivesField = "lives";

        // The overwrite prompt.
        private const string DialogTitle = "Build UI Prefabs";
        private const string DialogMessage = "These prefabs already exist and will be overwritten:";
        private const string DialogOverwrite = "Overwrite";
        private const string DialogCancel = "Cancel";

        /// <summary>
        /// Builds into <see cref="MenuOutputFolder"/>, after asking once when
        /// any of the built prefabs already exists there. Logs what it wrote,
        /// or why it wrote nothing.
        /// </summary>
        [MenuItem("Gate Rush/Build UI Prefabs")]
        public static void BuildFromMenu()
        {
            var existing = ExistingOutputs(MenuOutputFolder);
            if (existing.Count > 0
                && !EditorUtility.DisplayDialog(DialogTitle, $"{DialogMessage}\n\n{string.Join("\n", existing)}", DialogOverwrite, DialogCancel))
            {
                Debug.Log($"{LogPrefix}: cancelled; nothing was written.");
                return;
            }

            try
            {
                Build(MenuOutputFolder);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"{LogPrefix}: {exception.Message}");
                return;
            }

            Debug.Log($"{LogPrefix}: wrote {string.Join(", ", OutputNames())} into {MenuOutputFolder}.");
        }

        /// <summary>The paths of the prefabs <see cref="Build"/> writes that already exist in <paramref name="outputFolder"/>.</summary>
        public static IReadOnlyList<string> ExistingOutputs(string outputFolder)
        {
            var existing = new List<string>();
            foreach (var name in OutputNames())
            {
                var path = PrefabPath(outputFolder, name);
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    existing.Add(path);
                }
            }

            return existing;
        }

        /// <summary>
        /// Builds the six prefabs into <paramref name="outputFolder"/>,
        /// overwriting any already there without asking. The hand-made
        /// prefabs are read from <see cref="HandMadeFolder"/>.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="outputFolder"/> is not an existing asset folder.</exception>
        /// <exception cref="InvalidOperationException">An input is missing or unusable; nothing was written.</exception>
        public static void Build(string outputFolder)
        {
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                throw new ArgumentException($"{outputFolder} is not an asset folder.", nameof(outputFolder));
            }

            var sources = Sources.Load();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var storePage = Save(BuildPage(scene, sources, StorePageName, StoreTitle), outputFolder);
                var leaderboardPage = Save(BuildPage(scene, sources, LeaderboardPageName, LeaderboardTitle), outputFolder);
                var settings = Save(BuildSettingsOverlay(scene, sources), outputFolder);
                var levels = Save(BuildLevelsOverlay(scene, sources), outputFolder);
                var lives = Save(BuildLivesOverlay(scene, sources), outputFolder);
                Save(BuildFrontEnd(scene, sources, storePage, leaderboardPage, settings, levels, lives), outputFolder);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static string[] OutputNames() => new[]
        {
            StorePageName, LeaderboardPageName, SettingsOverlayName, LevelsOverlayName, LivesOverlayName, FrontEndName
        };

        private static string PrefabPath(string folder, string name) => $"{folder}/{name}{PrefabExtension}";

        private static GameObject BuildPage(Scene scene, Sources sources, string name, string title)
        {
            var root = CreateRoot(name, scene, sources.Layer);
            var titleRect = UiBuilder.CreateRect(TitleName, root.transform);
            TopStretch(titleRect, 0f, PageTitlePosY, PageTitleHeight);
            AddText(titleRect, sources, title, PageTitleFontSize, PageTitleColorHex);
            return root;
        }

        private static GameObject BuildSettingsOverlay(Scene scene, Sources sources)
        {
            var frame = BuildOverlayFrame(scene, sources, SettingsOverlayName, SettingsTitle, SettingsPanelWidth, SettingsPanelHeight);

            var row = UiBuilder.CreateRect(SoundRowName, frame.Panel);
            TopStretch(row, SoundRowInset, SoundRowPosY, SoundRowHeight);
            var labelRect = UiBuilder.CreateRect(SoundLabelName, row);
            UiBuilder.SetAnchors(labelRect, new Vector2(AnchorLow, AnchorLow), new Vector2(SoundLabelWidthFraction, AnchorHigh));
            var label = AddText(labelRect, sources, SoundText, SoundLabelFontSize, OverlayTextColorHex);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            var sound = Nest(sources.ButtonRound, row);
            UiBuilder.Place(sound, new Vector2(AnchorHigh, AnchorMid), Vector2.zero, sound.sizeDelta);
            Record(sound);
            var soundButton = Single<Button>(sound.gameObject, ButtonRoundName);
            soundButton.transition = Selectable.Transition.None;
            Record(soundButton);

            // The two looks take the place of Button Round's own icon, which
            // stays in the prefab and is switched off on this instance.
            var ownIcon = sound.Find(ButtonRoundIconName) as RectTransform;
            if (ownIcon == null)
            {
                throw new InvalidOperationException($"{ButtonRoundName} has no child '{ButtonRoundIconName}' for the sound looks to replace.");
            }

            ownIcon.gameObject.SetActive(false);
            Record(ownIcon.gameObject);
            var soundOnLook = AddLook(sound, ownIcon, SoundOnLookName, sources.SoundOn);
            var soundOffLook = AddLook(sound, ownIcon, SoundOffLookName, sources.SoundOff);
            soundOffLook.SetActive(false);

            var levels = Nest(sources.ButtonPrimary, frame.Panel);
            PlaceAtPanelBottom(levels);
            SetNestedLabel(levels, LevelsButtonText);

            var overlay = frame.Root.AddComponent<SettingsOverlay>();
            SetReferences(
                overlay,
                (CloseButtonField, frame.Close),
                (SoundButtonField, soundButton),
                (SoundOnLookField, soundOnLook),
                (SoundOffLookField, soundOffLook),
                (LevelsButtonField, Single<Button>(levels.gameObject, ButtonPrimaryName)));
            return frame.Root;
        }

        private static GameObject BuildLevelsOverlay(Scene scene, Sources sources)
        {
            var frame = BuildOverlayFrame(scene, sources, LevelsOverlayName, LevelsTitle, LevelsPanelWidth, LevelsPanelHeight);

            var scroll = UiBuilder.CreateRect(ScrollViewName, frame.Panel);
            UiBuilder.Stretch(scroll);
            scroll.offsetMin = new Vector2(ScrollInset, ScrollInset);
            scroll.offsetMax = new Vector2(-ScrollInset, -ScrollTop);

            // RectMask2D clips; the clear image only catches drags that start
            // between buttons, so the whole viewport scrolls.
            var viewport = UiBuilder.CreateRect(ViewportName, scroll);
            UiBuilder.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = ParseColor(ViewportColorHex);
            viewportImage.raycastTarget = true;

            var content = UiBuilder.CreateRect(ContentName, viewport);
            TopStretch(content, 0f, 0f, 0f);

            // One gap between the columns and at both sides, all equal, so the
            // grid is centred in the viewport with the buttons at their own size.
            var cell = ((RectTransform)sources.LevelButton.transform).sizeDelta;
            var viewportWidth = LevelsPanelWidth - (ScrollInset + ScrollInset);
            var gap = (viewportWidth - LevelGridColumns * cell.x) / (LevelGridColumns + 1);
            if (gap < 0f)
            {
                throw new InvalidOperationException(
                    $"{LevelGridColumns} {LevelButtonName}s of width {cell.x} do not fit the level grid's {viewportWidth} units.");
            }

            var padding = Mathf.FloorToInt(gap);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = LevelGridColumns;
            grid.cellSize = cell;
            grid.spacing = new Vector2(gap, gap);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(padding, padding, padding, padding);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            var overlay = frame.Root.AddComponent<LevelsOverlay>();
            SetReferences(
                overlay,
                (CloseButtonField, frame.Close),
                (GridField, content),
                (LevelButtonPrefabField, sources.LevelButton));
            return frame.Root;
        }

        private static GameObject BuildLivesOverlay(Scene scene, Sources sources)
        {
            var frame = BuildOverlayFrame(scene, sources, LivesOverlayName, LivesTitle, LivesPanelWidth, LivesPanelHeight);

            var heart = UiBuilder.CreateRect(HeartName, frame.Panel);
            UiBuilder.Place(heart, new Vector2(AnchorMid, AnchorHigh), new Vector2(0f, HeartPosY), new Vector2(HeartSize, HeartSize));
            var heartImage = UiBuilder.AddIcon(heart, sources.Heart, ParseColor(IconColorHex));
            AddTint(heart.gameObject, heartImage, UiColorRole.Heart);

            var livesRect = UiBuilder.CreateRect(LivesLabelName, heart);
            UiBuilder.Stretch(livesRect);
            var livesLabel = AddText(livesRect, sources, LivesPlaceholder, LivesFontSize, LivesLabelColorHex);

            var statusRect = UiBuilder.CreateRect(StatusLabelName, frame.Panel);
            TopStretch(statusRect, 0f, StatusPosY, StatusHeight);
            var statusLabel = AddText(statusRect, sources, StatusPlaceholder, StatusFontSize, OverlayTextColorHex);

            var refill = Nest(sources.ButtonPrimary, frame.Panel);
            PlaceAtPanelBottom(refill);
            var refillLabel = SetNestedLabel(refill, RefillText);

            var overlay = frame.Root.AddComponent<LivesOverlay>();
            SetReferences(
                overlay,
                (CloseButtonField, frame.Close),
                (LivesLabelField, livesLabel),
                (StatusLabelField, statusLabel),
                (RefillButtonField, Single<Button>(refill.gameObject, ButtonPrimaryName)),
                (RefillLabelField, refillLabel));
            return frame.Root;
        }

        /// <summary>
        /// The front end, set as <see cref="FrontEnd.Show"/> leaves it: Home
        /// shown, the other pages and every overlay hidden. Its root is saved
        /// inactive, so only <see cref="FrontEnd.Show"/> opens it.
        /// </summary>
        private static GameObject BuildFrontEnd(
            Scene scene, Sources sources, GameObject storeAsset, GameObject leaderboardAsset,
            GameObject settingsAsset, GameObject levelsAsset, GameObject livesAsset)
        {
            var topBarHeight = ((RectTransform)sources.TopBar.transform).sizeDelta.y;
            var tabBarHeight = ((RectTransform)sources.TabBar.transform).sizeDelta.y;
            WarnIfUnexpected(TopBarName, topBarHeight, ExpectedTopBarHeight);
            WarnIfUnexpected(TabBarName, tabBarHeight, ExpectedTabBarHeight);

            var root = CreateRoot(FrontEndName, scene, sources.Layer);

            var background = UiBuilder.CreateRect(BackgroundName, root.transform);
            UiBuilder.Stretch(background);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.raycastTarget = true;
            AddTint(background.gameObject, backgroundImage, UiColorRole.Background);

            var safe = UiBuilder.CreateRect(SafeName, root.transform);
            UiBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            var pages = UiBuilder.CreateRect(PagesName, safe);
            UiBuilder.Stretch(pages);
            pages.offsetMin = new Vector2(0f, tabBarHeight);
            pages.offsetMax = new Vector2(0f, -topBarHeight);
            var store = NestStretched(storeAsset, pages, false);
            var home = NestStretched(sources.HomePage, pages, true);
            var leaderboard = NestStretched(leaderboardAsset, pages, false);

            var topBar = Nest(sources.TopBar, safe);
            TopStretch(topBar, 0f, 0f, topBarHeight);
            Record(topBar);

            var tabBar = Nest(sources.TabBar, safe);
            tabBar.anchorMin = new Vector2(AnchorLow, AnchorLow);
            tabBar.anchorMax = new Vector2(AnchorHigh, AnchorLow);
            tabBar.pivot = new Vector2(AnchorMid, AnchorLow);
            tabBar.anchoredPosition = Vector2.zero;
            tabBar.sizeDelta = new Vector2(0f, tabBarHeight);
            Record(tabBar);

            // Outside Safe, so the scrims cover the safe-area insets too.
            var overlays = UiBuilder.CreateRect(OverlaysName, root.transform);
            UiBuilder.Stretch(overlays);
            var settings = NestStretched(settingsAsset, overlays, false);
            var levels = NestStretched(levelsAsset, overlays, false);
            var lives = NestStretched(livesAsset, overlays, false);

            var frontEnd = root.AddComponent<FrontEnd>();
            SetReferences(
                frontEnd,
                (ConfigField, sources.Config),
                (StorePageField, store.gameObject),
                (HomeField, Single<HomeScreen>(home.gameObject, HomePageName)),
                (LeaderboardPageField, leaderboard.gameObject),
                (TopBarField, Single<TopBar>(topBar.gameObject, TopBarName)),
                (TabBarField, Single<TabBar>(tabBar.gameObject, TabBarName)),
                (SettingsField, Single<SettingsOverlay>(settings.gameObject, SettingsOverlayName)),
                (LevelsField, Single<LevelsOverlay>(levels.gameObject, LevelsOverlayName)),
                (LivesField, Single<LivesOverlay>(lives.gameObject, LivesOverlayName)));

            root.SetActive(false);
            return root;
        }

        /// <summary>The root, scrim, panel, title and close button every overlay shares.</summary>
        private static OverlayFrame BuildOverlayFrame(Scene scene, Sources sources, string name, string title, float width, float height)
        {
            var root = CreateRoot(name, scene, sources.Layer);

            var scrim = UiBuilder.CreateRect(ScrimName, root.transform);
            UiBuilder.Stretch(scrim);
            var scrimImage = scrim.gameObject.AddComponent<Image>();
            Color32 scrimColor = ParseColor(ScrimColorHex);
            scrimColor.a = ScrimAlpha;
            scrimImage.color = scrimColor;
            scrimImage.raycastTarget = true;

            var panel = UiBuilder.CreateRect(PanelName, root.transform);
            UiBuilder.PlaceCentered(panel, new Vector2(width, height));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = sources.Panel;
            panelImage.type = Image.Type.Sliced;

            var titleRect = UiBuilder.CreateRect(TitleName, panel);
            TopStretch(titleRect, 0f, OverlayTitlePosY, OverlayTitleHeight);
            AddText(titleRect, sources, title, OverlayTitleFontSize, OverlayTextColorHex);

            var close = Nest(sources.ButtonClose, panel);
            UiBuilder.Place(close, new Vector2(AnchorHigh, AnchorHigh), new Vector2(-CloseButtonInset, -CloseButtonInset), close.sizeDelta);
            Record(close);

            return new OverlayFrame(root, panel, Single<Button>(close.gameObject, ButtonCloseName));
        }

        private static GameObject CreateRoot(string name, Scene scene, int layer)
        {
            var root = new GameObject(name, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = layer;
            UiBuilder.Stretch((RectTransform)root.transform);
            return root;
        }

        /// <summary>Saves <paramref name="root"/> as a prefab named after it, destroys it, and returns the saved asset.</summary>
        private static GameObject Save(GameObject root, string outputFolder)
        {
            var path = PrefabPath(outputFolder, root.name);
            try
            {
                var asset = PrefabUtility.SaveAsPrefabAsset(root, path, out var isSaved);
                if (!isSaved || asset == null)
                {
                    throw new InvalidOperationException($"{path} could not be saved.");
                }

                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform Nest(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return (RectTransform)instance.transform;
        }

        private static RectTransform NestStretched(GameObject prefab, Transform parent, bool isActive)
        {
            var rect = Nest(prefab, parent);
            UiBuilder.Stretch(rect);
            Record(rect);
            rect.gameObject.SetActive(isActive);
            Record(rect.gameObject);
            return rect;
        }

        /// <summary>Keeps a change made to a nested instance as that instance's override.</summary>
        private static void Record(Object instanceObject)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(instanceObject);
        }

        private static void PlaceAtPanelBottom(RectTransform button)
        {
            UiBuilder.Place(button, new Vector2(AnchorMid, AnchorLow), new Vector2(0f, PanelButtonPosY), button.sizeDelta);
            Record(button);
        }

        private static TMP_Text SetNestedLabel(RectTransform button, string text)
        {
            var label = Single<TMP_Text>(button.gameObject, button.name);
            label.text = text;
            Record(label);
            return label;
        }

        /// <summary>An icon placed exactly where <paramref name="template"/> is, tinted as an icon.</summary>
        private static GameObject AddLook(RectTransform parent, RectTransform template, string name, Sprite sprite)
        {
            var look = UiBuilder.CreateRect(name, parent);
            look.anchorMin = template.anchorMin;
            look.anchorMax = template.anchorMax;
            look.pivot = template.pivot;
            look.anchoredPosition = template.anchoredPosition;
            look.sizeDelta = template.sizeDelta;
            var image = UiBuilder.AddIcon(look, sprite, ParseColor(IconColorHex));
            AddTint(look.gameObject, image, UiColorRole.Icon);
            return look.gameObject;
        }

        /// <summary>A one-line, centred label in the Button Primary font; not a raycast target (<see cref="UiBuilder.AddLabel"/>).</summary>
        private static TextMeshProUGUI AddText(RectTransform rect, Sources sources, string text, float fontSize, string colorHex)
        {
            var label = UiBuilder.AddLabel(rect, sources.Font, fontSize, ParseColor(colorHex), false);
            label.text = text;
            return label;
        }

        private static void AddTint(GameObject owner, Graphic target, UiColorRole role)
        {
            var tint = owner.AddComponent<UiTint>();
            using (var serialized = new SerializedObject(tint))
            {
                Property(serialized, TintRoleField).intValue = (int)role;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            SetReferences(tint, (TintTargetField, target));
        }

        /// <summary>
        /// Assigns serialized object fields by name. A missing field, or a
        /// value the field's type does not take — which Unity would drop
        /// silently — throws.
        /// </summary>
        private static void SetReferences(Object component, params (string field, Object value)[] fields)
        {
            using (var serialized = new SerializedObject(component))
            {
                foreach (var (field, value) in fields)
                {
                    var property = Property(serialized, field);
                    property.objectReferenceValue = value;
                    if (property.objectReferenceValue == null)
                    {
                        throw new InvalidOperationException(
                            $"{component.GetType().Name}.{field} did not take {(value == null ? "null" : value.GetType().Name)}.");
                    }
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static SerializedProperty Property(SerializedObject serialized, string field)
        {
            return serialized.FindProperty(field)
                ?? throw new InvalidOperationException($"{serialized.targetObject.GetType().Name} has no serialized field '{field}'.");
        }

        private static T Single<T>(GameObject root, string what) where T : Component
        {
            var found = root.GetComponentsInChildren<T>(true);
            if (found.Length != 1)
            {
                throw new InvalidOperationException($"{what} must hold exactly one {typeof(T).Name}; it holds {found.Length}.");
            }

            return found[0];
        }

        /// <summary>Anchors <paramref name="rect"/> across its parent's top edge, <paramref name="inset"/> in from both sides.</summary>
        private static void TopStretch(RectTransform rect, float inset, float posY, float height)
        {
            rect.anchorMin = new Vector2(AnchorLow, AnchorHigh);
            rect.anchorMax = new Vector2(AnchorHigh, AnchorHigh);
            rect.pivot = new Vector2(AnchorMid, AnchorHigh);
            rect.anchoredPosition = new Vector2(0f, posY);
            rect.sizeDelta = new Vector2(-(inset + inset), height);
        }

        private static Color ParseColor(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var color))
            {
                throw new InvalidOperationException($"'{hex}' is not an HTML colour.");
            }

            return color;
        }

        private static void WarnIfUnexpected(string prefabName, float actual, float expected)
        {
            if (!Mathf.Approximately(actual, expected))
            {
                Debug.LogWarning(
                    $"{LogPrefix}: {prefabName} is {actual} tall, not the planned {expected}; Front End's pages are laid out with {actual}.");
            }
        }

        private static Sprite LoadSprite(string path)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            if (sprites.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{path} must import as exactly one sprite (Sprite Mode: Single); it has {sprites.Count}. Nothing was written.");
            }

            return sprites[0];
        }

        private static GameObject LoadHandMade(string name)
        {
            var path = PrefabPath(HandMadeFolder, name);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException($"{path} is missing. Nothing was written.");
        }

        private readonly struct OverlayFrame
        {
            public OverlayFrame(GameObject root, RectTransform panel, Button close)
            {
                Root = root;
                Panel = panel;
                Close = close;
            }

            public GameObject Root { get; }
            public RectTransform Panel { get; }
            public Button Close { get; }
        }

        /// <summary>Every asset the build reads, loaded and checked before anything is written.</summary>
        private sealed class Sources
        {
            public int Layer { get; private set; }
            public TMP_FontAsset Font { get; private set; }
            public UiConfig Config { get; private set; }
            public Sprite Panel { get; private set; }
            public Sprite SoundOn { get; private set; }
            public Sprite SoundOff { get; private set; }
            public Sprite Heart { get; private set; }
            public GameObject ButtonClose { get; private set; }
            public GameObject ButtonPrimary { get; private set; }
            public GameObject ButtonRound { get; private set; }
            public GameObject HomePage { get; private set; }
            public GameObject TabBar { get; private set; }
            public GameObject TopBar { get; private set; }
            public LevelButton LevelButton { get; private set; }

            public static Sources Load()
            {
                var layer = LayerMask.NameToLayer(UiLayerName);
                if (layer < 0)
                {
                    throw new InvalidOperationException($"The project has no '{UiLayerName}' layer. Nothing was written.");
                }

                var buttonPrimary = LoadHandMade(ButtonPrimaryName);
                var font = Single<TMP_Text>(buttonPrimary, ButtonPrimaryName).font;
                if (font == null)
                {
                    throw new InvalidOperationException($"{ButtonPrimaryName}'s label has no font asset. Nothing was written.");
                }

                var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiConfigPath);
                if (config == null)
                {
                    throw new InvalidOperationException($"{UiConfigPath} is missing. Nothing was written.");
                }

                return new Sources
                {
                    Layer = layer,
                    Font = font,
                    Config = config,
                    Panel = LoadSprite(PanelSpritePath),
                    SoundOn = LoadSprite(SoundOnSpritePath),
                    SoundOff = LoadSprite(SoundOffSpritePath),
                    Heart = LoadSprite(HeartSpritePath),
                    ButtonClose = LoadHandMade(ButtonCloseName),
                    ButtonPrimary = buttonPrimary,
                    ButtonRound = LoadHandMade(ButtonRoundName),
                    HomePage = LoadHandMade(HomePageName),
                    TabBar = LoadHandMade(TabBarName),
                    TopBar = LoadHandMade(TopBarName),
                    LevelButton = Single<LevelButton>(LoadHandMade(LevelButtonName), LevelButtonName)
                };
            }
        }
    }
}
