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
    /// <i>Gate Rush → Build UI Prefabs</i>: builds the prefabs the owner does
    /// not lay out by hand — Module 35's Store Page, Leaderboard Page,
    /// Settings, Levels and Lives overlays and Front End, and Module 36's Gold
    /// Pack Card and Not Enough Gold, Message and Test Purchase overlays, with
    /// the Store Page now holding the Store — from code, with every serialized
    /// field wired.
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
    /// <para><b>Tinted graphics carry their colour.</b> Before a prefab is
    /// saved, every <see cref="UiTint"/> in it, those of nested hand-made
    /// instances included, gives its graphic its <see cref="UiConfig"/>
    /// colour, so the Editor shows them as the game does; on a nested
    /// instance the colour is that instance's override. <see cref="UiTint"/>
    /// still applies the colour at runtime.</para>
    /// <para>Texts the game sets from the UI Config are written with the
    /// config's values, for the same reason; texts it sets from the state
    /// are written with placeholders.</para>
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
        private const string RibbonSpritePath = "Assets/ThirdParty/Kenney/UIPack/Red/button_rectangle_flat.png";
        private const string SoundOnSpritePath = "Assets/ThirdParty/Kenney/GameIcons/audioOn.png";
        private const string SoundOffSpritePath = "Assets/ThirdParty/Kenney/GameIcons/audioOff.png";
        private const string HeartSpritePath = "Assets/ThirdParty/Kenney/BoardGameIcons/suit_hearts.png";
        private const string CoinSpritePath = "Assets/ThirdParty/Kenney/BoardGameIcons/token.png";
        private const string CoinStackSpritePath = "Assets/ThirdParty/Kenney/BoardGameIcons/tokens_stack.png";

        // The joker icons: stand-ins until 6.6.
        private const string ClockSpritePath = "Assets/ThirdParty/Kenney/BoardGameIcons/hourglass.png";
        private const string RocketSpritePath = "Assets/ThirdParty/Kenney/BoardGameIcons/exploding.png";
        private const string BroomSpritePath = "Assets/ThirdParty/Kenney/BoardGameIcons/arrow_horizontal.png";
        private const string UiLayerName = "UI";

        // Hand-made prefabs.
        private const string ButtonCloseName = "Button Close";
        private const string ButtonPrimaryName = "Button Primary";
        private const string ButtonRoundName = "Button Round";
        private const string ButtonSecondaryName = "Button Secondary";
        private const string HomePageName = "Home Page";
        private const string LevelButtonName = "Level Button";
        private const string TabBarName = "Tab Bar";
        private const string TopBarName = "Top Bar";

        // The child of Button Round that the sound looks replace.
        private const string ButtonRoundIconName = "Icon";

        // Built prefabs.
        private const string GoldPackCardName = "Gold Pack Card";
        private const string StorePageName = "Store Page";
        private const string LeaderboardPageName = "Leaderboard Page";
        private const string SettingsOverlayName = "Settings Overlay";
        private const string LevelsOverlayName = "Levels Overlay";
        private const string LivesOverlayName = "Lives Overlay";
        private const string NotEnoughGoldOverlayName = "Not Enough Gold Overlay";
        private const string MessageOverlayName = "Message Overlay";
        private const string TestPurchaseOverlayName = "Test Purchase Overlay";
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
        private const string IconName = "Icon";
        private const string CoinName = "Coin";
        private const string CoinStackName = "Coin Stack";
        private const string AmountName = "Amount";
        private const string RibbonName = "Ribbon";
        private const string RibbonLabelName = "Label";
        private const string CardNameLabelName = "Name";
        private const string CardSubLabelName = "Sub";
        private const string GoldHeaderName = "Gold Header";
        private const string GoldPacksName = "Gold Packs";
        private const string JokersHeaderName = "Jokers Header";
        private const string ClockCardName = "Clock Card";
        private const string RocketCardName = "Rocket Card";
        private const string BroomCardName = "Broom Card";
        private const string LivesHeaderName = "Lives Header";
        private const string LivesCardName = "Lives Card";
        private const string TestStoreNoteName = "Test Store Note";
        private const string TextName = "Text";
        private const string ShortfallName = "Shortfall";
        private const string OfferName = "Offer";
        private const string NoteName = "Note";

        // Texts. Labels the game overwrites from the state only show in the
        // Editor; those it sets from the UI Config are written from it.
        private const string StoreTitle = "Store";
        private const string LeaderboardTitle = "Leaderboard";
        private const string SettingsTitle = "Settings";
        private const string LevelsTitle = "Levels";
        private const string LivesTitle = "Lives";
        private const string NotEnoughGoldTitle = "Not Enough Gold";
        private const string SoundText = "Sound";
        private const string LevelsButtonText = "Levels";
        private const string RefillText = "Refill";
        private const string LivesPlaceholder = "5/5";
        private const string StatusPlaceholder = "Full";
        private const string ClockText = "Clock";
        private const string RocketText = "Rocket";
        private const string BroomText = "Broom";
        private const string RefillLivesText = "Refill Lives";
        private const string GetGoldText = "Get Gold";
        private const string OkText = "OK";
        private const string BuyText = "Buy";
        private const string CancelText = "Cancel";

        // Placeholders for the Store's numbers, from the defaults of D72.
        private const int GoldPlaceholder = 1500;
        private const int CountPlaceholder = 1;
        private const int PricePlaceholder = 100;
        private const int ShortfallPlaceholder = 100;
        private const string PriceTextPlaceholder = "$2.49";
        private const string RibbonPlaceholder = "Popular";
        private const string ProductIdPlaceholder = "gold_medium";

        // Colours.
        private const string PageTitleColorHex = "#FFFFFF";
        private const string OverlayTextColorHex = "#3B2466";
        private const string LivesLabelColorHex = "#FFFFFF";
        private const string IconColorHex = "#FFFFFF";
        private const string RibbonLabelColorHex = "#FFFFFF";
        private const string ScrimColorHex = "#000000";
        private const byte ScrimAlpha = 153;
        private const string ViewportColorHex = "#FFFFFF00";

        // Anchor and pivot coordinates: a parent's low edge, middle and high edge.
        private const float AnchorLow = 0f;
        private const float AnchorMid = 0.5f;
        private const float AnchorHigh = 1f;

        // Every scroll view: Unity's default of 1 is far too slow for the
        // mouse wheel at the 1080x1920 reference resolution.
        private const float ScrollSensitivity = 20f;

        // Store and Leaderboard pages.
        private const float PageTitleFontSize = 90f;
        private const float PageTitlePosY = -20f;
        private const float PageTitleHeight = 140f;

        // The Store's scroll and sections.
        private const float StoreSideInset = 40f;
        private const int StoreBottomPadding = 40;
        private const float StoreSpacing = 24f;
        private const float SectionHeaderHeight = 90f;
        private const float SectionHeaderFontSize = 60f;
        private const float TestStoreNoteHeight = 120f;
        private const float TestStoreNoteFontSize = 40f;

        // Every Store card. Its width is the layout's; this one is the Gold
        // Pack Card prefab's own, for the Editor.
        private const float CardPreviewWidth = 1000f;
        private const float CardHeight = 200f;
        private const float CardIconSize = 140f;
        private const float CardIconInset = 30f;
        private const float CardTextLeft = 200f;
        private const float CardTextRight = 360f;
        private const float CardTitleFontSize = 60f;
        private const float CardNameFontSize = 56f;
        private const float CardSubFontSize = 44f;
        private const float CardButtonWidth = 300f;
        private const float CardButtonHeight = 130f;
        private const float CardButtonInset = 30f;
        private const float ButtonCoinSize = 64f;
        private const float ButtonCoinInset = 30f;
        private const float ButtonLabelCoinShift = 70f;
        private const float RibbonWidth = 220f;
        private const float RibbonHeight = 52f;
        private const float RibbonFontSize = 34f;

        // Every overlay.
        private const float OverlayTitleFontSize = 80f;
        private const float OverlayTitlePosY = -30f;
        private const float OverlayTitleHeight = 110f;
        private const float CloseButtonInset = 10f;
        private const float PanelButtonPosY = 70f;
        private const float OverlayTextInset = 60f;

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

        // Message overlay.
        private const float MessagePanelWidth = 820f;
        private const float MessagePanelHeight = 620f;
        private const float MessageTextPosY = -170f;
        private const float MessageTextHeight = 240f;
        private const float MessageTextFontSize = 48f;

        // Not Enough Gold overlay.
        private const float NotEnoughGoldPanelWidth = 820f;
        private const float NotEnoughGoldPanelHeight = 800f;
        private const float CoinStackSize = 220f;
        private const float CoinStackPosY = -150f;
        private const float ShortfallPosY = -400f;
        private const float ShortfallHeight = 140f;
        private const float ShortfallFontSize = 52f;

        // Test Purchase overlay.
        private const float TestPurchasePanelWidth = 860f;
        private const float TestPurchasePanelHeight = 780f;
        private const float OfferPosY = -170f;
        private const float OfferHeight = 110f;
        private const float OfferFontSize = 56f;
        private const float NotePosY = -300f;
        private const float NoteHeight = 200f;
        private const float NoteFontSize = 44f;
        private const float SheetButtonWidth = 340f;
        private const float SheetButtonHeight = 140f;
        private const float SheetButtonOffsetX = 200f;

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
        private const string StoreField = "store";
        private const string HomeField = "home";
        private const string LeaderboardPageField = "leaderboardPage";
        private const string TopBarField = "topBar";
        private const string TabBarField = "tabBar";
        private const string SettingsField = "settings";
        private const string LevelsField = "levels";
        private const string LivesField = "lives";
        private const string NotEnoughGoldField = "notEnoughGold";
        private const string MessageField = "message";
        private const string TestPurchaseField = "testPurchase";
        private const string AmountLabelField = "amountLabel";
        private const string RibbonField = "ribbon";
        private const string RibbonLabelField = "ribbonLabel";
        private const string BuyButtonField = "buyButton";
        private const string PriceLabelField = "priceLabel";
        private const string SubLabelField = "subLabel";
        private const string CoinIconField = "coinIcon";
        private const string ScrollRectField = "scrollRect";
        private const string GoldHeaderField = "goldHeader";
        private const string JokersHeaderField = "jokersHeader";
        private const string LivesHeaderField = "livesHeader";
        private const string TestStoreNoteField = "testStoreNote";
        private const string GoldPacksField = "goldPacks";
        private const string GoldPackCardPrefabField = "goldPackCardPrefab";
        private const string ClockCardField = "clockCard";
        private const string RocketCardField = "rocketCard";
        private const string BroomCardField = "broomCard";
        private const string LivesCardField = "livesCard";
        private const string TitleLabelField = "titleLabel";
        private const string TextLabelField = "textLabel";
        private const string OkButtonField = "okButton";
        private const string ShortfallLabelField = "shortfallLabel";
        private const string GetGoldButtonField = "getGoldButton";
        private const string OfferLabelField = "offerLabel";
        private const string NoteLabelField = "noteLabel";
        private const string CancelButtonField = "cancelButton";

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
        /// Builds the ten prefabs into <paramref name="outputFolder"/>,
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
                var goldPackCard = Save(BuildGoldPackCard(scene, sources), sources, outputFolder);
                var storePage = Save(BuildStorePage(scene, sources, goldPackCard), sources, outputFolder);
                var leaderboardPage = Save(BuildPage(scene, sources, LeaderboardPageName, LeaderboardTitle), sources, outputFolder);
                var settings = Save(BuildSettingsOverlay(scene, sources), sources, outputFolder);
                var levels = Save(BuildLevelsOverlay(scene, sources), sources, outputFolder);
                var lives = Save(BuildLivesOverlay(scene, sources), sources, outputFolder);
                var notEnoughGold = Save(BuildNotEnoughGoldOverlay(scene, sources), sources, outputFolder);
                var message = Save(BuildMessageOverlay(scene, sources), sources, outputFolder);
                var testPurchase = Save(BuildTestPurchaseOverlay(scene, sources), sources, outputFolder);
                var overlays = new OverlayAssets(settings, levels, lives, notEnoughGold, message, testPurchase);
                Save(BuildFrontEnd(scene, sources, storePage, leaderboardPage, overlays), sources, outputFolder);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static string[] OutputNames() => new[]
        {
            GoldPackCardName, StorePageName, LeaderboardPageName, SettingsOverlayName, LevelsOverlayName, LivesOverlayName,
            NotEnoughGoldOverlayName, MessageOverlayName, TestPurchaseOverlayName, FrontEndName
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

        /// <summary>
        /// One gold pack's card, the prefab the Store instantiates per offer:
        /// the coin stack, the amount, the ribbon and the price button.
        /// </summary>
        private static GameObject BuildGoldPackCard(Scene scene, Sources sources)
        {
            var root = CreateRoot(GoldPackCardName, scene, sources.Layer);
            var rect = (RectTransform)root.transform;
            UiBuilder.PlaceCentered(rect, new Vector2(CardPreviewWidth, CardHeight));
            AddCardFrame(rect, sources);

            AddCardIcon(rect, sources.CoinStack, IconColorHex, UiColorRole.Coin);

            var amountRect = UiBuilder.CreateRect(AmountName, rect);
            CardTextBand(amountRect, AnchorLow, AnchorHigh);
            var amount = AddText(amountRect, sources, StoreTexts.GoldAmount(GoldPlaceholder, sources.Config), CardTitleFontSize, OverlayTextColorHex);
            amount.alignment = TextAlignmentOptions.MidlineLeft;

            var ribbon = UiBuilder.CreateRect(RibbonName, rect);
            UiBuilder.Place(ribbon, new Vector2(AnchorLow, AnchorHigh), Vector2.zero, new Vector2(RibbonWidth, RibbonHeight));
            var ribbonImage = ribbon.gameObject.AddComponent<Image>();
            ribbonImage.sprite = sources.Ribbon;
            ribbonImage.type = Image.Type.Sliced;
            ribbonImage.raycastTarget = false;
            var ribbonLabelRect = UiBuilder.CreateRect(RibbonLabelName, ribbon);
            UiBuilder.Stretch(ribbonLabelRect);
            var ribbonLabel = AddText(
                ribbonLabelRect, sources, sources.Config.RibbonFor(ProductIdPlaceholder) ?? RibbonPlaceholder, RibbonFontSize, RibbonLabelColorHex);

            var button = AddCardButton(rect, sources.ButtonPrimary);
            var priceLabel = SetNestedLabel(button, PriceTextPlaceholder);

            var card = root.AddComponent<GoldPackCard>();
            SetReferences(
                card,
                (AmountLabelField, amount),
                (RibbonField, ribbon.gameObject),
                (RibbonLabelField, ribbonLabel),
                (BuyButtonField, Single<Button>(button.gameObject, ButtonPrimaryName)),
                (PriceLabelField, priceLabel));
            return root;
        }

        /// <summary>
        /// The Store tab's page: its title over a vertical scroll of the gold
        /// packs, the three jokers, the lives refill and the test store line.
        /// </summary>
        private static GameObject BuildStorePage(Scene scene, Sources sources, GameObject goldPackCardAsset)
        {
            var root = BuildPage(scene, sources, StorePageName, StoreTitle);
            var config = sources.Config;

            var scroll = UiBuilder.CreateRect(ScrollViewName, root.transform);
            UiBuilder.Stretch(scroll);
            scroll.offsetMax = new Vector2(0f, PageTitlePosY - PageTitleHeight);

            // RectMask2D clips; the clear image catches drags that start
            // between cards, so the whole viewport scrolls.
            var viewport = UiBuilder.CreateRect(ViewportName, scroll);
            UiBuilder.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = ParseColor(ViewportColorHex);
            viewportImage.raycastTarget = true;

            var content = UiBuilder.CreateRect(ContentName, viewport);
            TopStretch(content, StoreSideInset, 0f, 0f);
            var layout = AddColumn(content);
            layout.padding = new RectOffset(0, 0, 0, StoreBottomPadding);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var goldHeader = AddSectionLine(content, sources, GoldHeaderName, config.GoldHeader, SectionHeaderHeight, SectionHeaderFontSize, false);
            var goldPacks = UiBuilder.CreateRect(GoldPacksName, content);
            AddColumn(goldPacks);

            var jokersHeader = AddSectionLine(content, sources, JokersHeaderName, config.JokersHeader, SectionHeaderHeight, SectionHeaderFontSize, false);
            var jokerSub = StoreTexts.JokerCount(CountPlaceholder, config);
            var jokerPrice = StoreTexts.GoldPrice(PricePlaceholder, config);
            var clock = BuildItemCard(content, sources, ClockCardName, sources.Clock, OverlayTextColorHex, null, ClockText, jokerSub, jokerPrice);
            var rocket = BuildItemCard(content, sources, RocketCardName, sources.Rocket, OverlayTextColorHex, null, RocketText, jokerSub, jokerPrice);
            var broom = BuildItemCard(content, sources, BroomCardName, sources.Broom, OverlayTextColorHex, null, BroomText, jokerSub, jokerPrice);

            var livesHeader = AddSectionLine(content, sources, LivesHeaderName, config.LivesHeader, SectionHeaderHeight, SectionHeaderFontSize, false);
            var livesCard = BuildItemCard(
                content, sources, LivesCardName, sources.Heart, IconColorHex, UiColorRole.Heart, RefillLivesText,
                config.LivesFullLabel, config.LivesFullLabel);

            var note = AddSectionLine(content, sources, TestStoreNoteName, config.TestStoreNote, TestStoreNoteHeight, TestStoreNoteFontSize, true);

            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = ScrollSensitivity;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            var screen = root.AddComponent<StoreScreen>();
            SetReferences(
                screen,
                (ScrollRectField, scrollRect),
                (GoldHeaderField, goldHeader),
                (JokersHeaderField, jokersHeader),
                (LivesHeaderField, livesHeader),
                (TestStoreNoteField, note),
                (GoldPacksField, goldPacks),
                (GoldPackCardPrefabField, Single<GoldPackCard>(goldPackCardAsset, GoldPackCardName)),
                (ClockCardField, clock),
                (RocketCardField, rocket),
                (BroomCardField, broom),
                (LivesCardField, livesCard));
            return root;
        }

        /// <summary>
        /// A Store card bought for gold: the icon — tinted by
        /// <paramref name="role"/> when it has one, else drawn in
        /// <paramref name="iconColorHex"/> — the name, the line under it, and
        /// the buy button with the coin beside its label.
        /// </summary>
        private static StoreItemCard BuildItemCard(
            RectTransform parent, Sources sources, string name, Sprite icon, string iconColorHex, UiColorRole? role,
            string title, string subText, string priceText)
        {
            var card = UiBuilder.CreateRect(name, parent);
            AddCardFrame(card, sources);
            AddCardIcon(card, icon, iconColorHex, role);

            var nameRect = UiBuilder.CreateRect(CardNameLabelName, card);
            CardTextBand(nameRect, AnchorMid, AnchorHigh);
            var nameLabel = AddText(nameRect, sources, title, CardNameFontSize, OverlayTextColorHex);
            nameLabel.alignment = TextAlignmentOptions.BottomLeft;

            var subRect = UiBuilder.CreateRect(CardSubLabelName, card);
            CardTextBand(subRect, AnchorLow, AnchorMid);
            var subLabel = AddText(subRect, sources, subText, CardSubFontSize, OverlayTextColorHex);
            subLabel.alignment = TextAlignmentOptions.TopLeft;

            var button = AddCardButton(card, sources.ButtonPrimary);
            var priceLabel = SetNestedLabel(button, priceText);

            // The coin is added to this instance only; the label moves right
            // to make room for it.
            var coin = UiBuilder.CreateRect(CoinName, button);
            UiBuilder.Place(coin, new Vector2(AnchorLow, AnchorMid), new Vector2(ButtonCoinInset, 0f), new Vector2(ButtonCoinSize, ButtonCoinSize));
            var coinImage = UiBuilder.AddIcon(coin, sources.Coin, ParseColor(IconColorHex));
            AddTint(coin.gameObject, coinImage, UiColorRole.Coin);
            var labelRect = priceLabel.rectTransform;
            labelRect.offsetMin += new Vector2(ButtonLabelCoinShift, 0f);
            Record(labelRect);

            var component = card.gameObject.AddComponent<StoreItemCard>();
            SetReferences(
                component,
                (SubLabelField, subLabel),
                (BuyButtonField, Single<Button>(button.gameObject, ButtonPrimaryName)),
                (PriceLabelField, priceLabel),
                (CoinIconField, coin.gameObject));
            return component;
        }

        private static GameObject BuildSettingsOverlay(Scene scene, Sources sources)
        {
            var frame = BuildOverlayFrame(scene, sources, SettingsOverlayName, SettingsTitle, SettingsPanelWidth, SettingsPanelHeight, true);

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
            var frame = BuildOverlayFrame(scene, sources, LevelsOverlayName, LevelsTitle, LevelsPanelWidth, LevelsPanelHeight, true);

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
            scrollRect.scrollSensitivity = ScrollSensitivity;
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
            var frame = BuildOverlayFrame(scene, sources, LivesOverlayName, LivesTitle, LivesPanelWidth, LivesPanelHeight, true);

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

        private static GameObject BuildNotEnoughGoldOverlay(Scene scene, Sources sources)
        {
            var frame = BuildOverlayFrame(
                scene, sources, NotEnoughGoldOverlayName, NotEnoughGoldTitle, NotEnoughGoldPanelWidth, NotEnoughGoldPanelHeight, true);

            var stack = UiBuilder.CreateRect(CoinStackName, frame.Panel);
            UiBuilder.Place(stack, new Vector2(AnchorMid, AnchorHigh), new Vector2(0f, CoinStackPosY), new Vector2(CoinStackSize, CoinStackSize));
            var stackImage = UiBuilder.AddIcon(stack, sources.CoinStack, ParseColor(IconColorHex));
            AddTint(stack.gameObject, stackImage, UiColorRole.Coin);

            var shortfallRect = UiBuilder.CreateRect(ShortfallName, frame.Panel);
            TopStretch(shortfallRect, OverlayTextInset, ShortfallPosY, ShortfallHeight);
            var shortfall = AddWrappingText(
                shortfallRect, sources, StoreTexts.Shortfall(ShortfallPlaceholder, sources.Config), ShortfallFontSize, OverlayTextColorHex);

            var getGold = Nest(sources.ButtonPrimary, frame.Panel);
            PlaceAtPanelBottom(getGold);
            SetNestedLabel(getGold, GetGoldText);

            var overlay = frame.Root.AddComponent<NotEnoughGoldOverlay>();
            SetReferences(
                overlay,
                (CloseButtonField, frame.Close),
                (ShortfallLabelField, shortfall),
                (GetGoldButtonField, Single<Button>(getGold.gameObject, ButtonPrimaryName)));
            return frame.Root;
        }

        /// <summary>A title, one line and an OK button; no close button, since OK is its one button.</summary>
        private static GameObject BuildMessageOverlay(Scene scene, Sources sources)
        {
            var config = sources.Config;
            var frame = BuildOverlayFrame(
                scene, sources, MessageOverlayName, config.PurchaseFailedTitle, MessagePanelWidth, MessagePanelHeight, false);

            var textRect = UiBuilder.CreateRect(TextName, frame.Panel);
            TopStretch(textRect, OverlayTextInset, MessageTextPosY, MessageTextHeight);
            var text = AddWrappingText(textRect, sources, config.PurchaseFailedText, MessageTextFontSize, OverlayTextColorHex);

            var ok = Nest(sources.ButtonPrimary, frame.Panel);
            PlaceAtPanelBottom(ok);
            SetNestedLabel(ok, OkText);

            var overlay = frame.Root.AddComponent<MessageOverlay>();
            SetReferences(
                overlay,
                (TitleLabelField, frame.Title),
                (TextLabelField, text),
                (OkButtonField, Single<Button>(ok.gameObject, ButtonPrimaryName)));
            return frame.Root;
        }

        /// <summary>The simulated store's sheet: the offer, the line saying nothing is charged, Cancel and Buy.</summary>
        private static GameObject BuildTestPurchaseOverlay(Scene scene, Sources sources)
        {
            var config = sources.Config;
            var frame = BuildOverlayFrame(
                scene, sources, TestPurchaseOverlayName, config.TestPurchaseTitle, TestPurchasePanelWidth, TestPurchasePanelHeight, true);

            var offerRect = UiBuilder.CreateRect(OfferName, frame.Panel);
            TopStretch(offerRect, OverlayTextInset, OfferPosY, OfferHeight);
            var offerText = StoreTexts.OfferLine(new GoldOffer(ProductIdPlaceholder, GoldPlaceholder, PriceTextPlaceholder), config);
            var offer = AddText(offerRect, sources, offerText, OfferFontSize, OverlayTextColorHex);

            var noteRect = UiBuilder.CreateRect(NoteName, frame.Panel);
            TopStretch(noteRect, OverlayTextInset, NotePosY, NoteHeight);
            var note = AddWrappingText(noteRect, sources, config.TestPurchaseNote, NoteFontSize, OverlayTextColorHex);

            var cancel = Nest(sources.ButtonSecondary, frame.Panel);
            PlaceSheetButton(cancel, -SheetButtonOffsetX);
            SetNestedLabel(cancel, CancelText);

            var buy = Nest(sources.ButtonPrimary, frame.Panel);
            PlaceSheetButton(buy, SheetButtonOffsetX);
            SetNestedLabel(buy, BuyText);

            var overlay = frame.Root.AddComponent<TestPurchaseOverlay>();
            SetReferences(
                overlay,
                (CloseButtonField, frame.Close),
                (TitleLabelField, frame.Title),
                (OfferLabelField, offer),
                (NoteLabelField, note),
                (BuyButtonField, Single<Button>(buy.gameObject, ButtonPrimaryName)),
                (CancelButtonField, Single<Button>(cancel.gameObject, ButtonSecondaryName)));
            return frame.Root;
        }

        /// <summary>
        /// The front end, set as <see cref="FrontEnd.Show"/> leaves it: Home
        /// shown, the other pages and every overlay hidden. Its root is saved
        /// inactive, so only <see cref="FrontEnd.Show"/> opens it.
        /// </summary>
        private static GameObject BuildFrontEnd(
            Scene scene, Sources sources, GameObject storeAsset, GameObject leaderboardAsset, OverlayAssets overlayAssets)
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
            var settings = NestStretched(overlayAssets.Settings, overlays, false);
            var levels = NestStretched(overlayAssets.Levels, overlays, false);
            var lives = NestStretched(overlayAssets.Lives, overlays, false);
            var notEnoughGold = NestStretched(overlayAssets.NotEnoughGold, overlays, false);
            var message = NestStretched(overlayAssets.Message, overlays, false);
            var testPurchase = NestStretched(overlayAssets.TestPurchase, overlays, false);

            var frontEnd = root.AddComponent<FrontEnd>();
            SetReferences(
                frontEnd,
                (ConfigField, sources.Config),
                (StoreField, Single<StoreScreen>(store.gameObject, StorePageName)),
                (HomeField, Single<HomeScreen>(home.gameObject, HomePageName)),
                (LeaderboardPageField, leaderboard.gameObject),
                (TopBarField, Single<TopBar>(topBar.gameObject, TopBarName)),
                (TabBarField, Single<TabBar>(tabBar.gameObject, TabBarName)),
                (SettingsField, Single<SettingsOverlay>(settings.gameObject, SettingsOverlayName)),
                (LevelsField, Single<LevelsOverlay>(levels.gameObject, LevelsOverlayName)),
                (LivesField, Single<LivesOverlay>(lives.gameObject, LivesOverlayName)),
                (NotEnoughGoldField, Single<NotEnoughGoldOverlay>(notEnoughGold.gameObject, NotEnoughGoldOverlayName)),
                (MessageField, Single<MessageOverlay>(message.gameObject, MessageOverlayName)),
                (TestPurchaseField, Single<TestPurchaseOverlay>(testPurchase.gameObject, TestPurchaseOverlayName)));

            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// The root, scrim, panel and title every overlay shares, and the
        /// close button in the panel's corner when <paramref name="hasClose"/>.
        /// </summary>
        private static OverlayFrame BuildOverlayFrame(
            Scene scene, Sources sources, string name, string title, float width, float height, bool hasClose)
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
            var titleLabel = AddText(titleRect, sources, title, OverlayTitleFontSize, OverlayTextColorHex);

            Button closeButton = null;
            if (hasClose)
            {
                var close = Nest(sources.ButtonClose, panel);
                UiBuilder.Place(close, new Vector2(AnchorHigh, AnchorHigh), new Vector2(-CloseButtonInset, -CloseButtonInset), close.sizeDelta);
                Record(close);
                closeButton = Single<Button>(close.gameObject, ButtonCloseName);
            }

            return new OverlayFrame(root, panel, closeButton, titleLabel);
        }

        private static GameObject CreateRoot(string name, Scene scene, int layer)
        {
            var root = new GameObject(name, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = layer;
            UiBuilder.Stretch((RectTransform)root.transform);
            return root;
        }

        /// <summary>
        /// Gives every tinted graphic under <paramref name="root"/> its colour,
        /// saves <paramref name="root"/> as a prefab named after it, destroys
        /// it, and returns the saved asset.
        /// </summary>
        private static GameObject Save(GameObject root, Sources sources, string outputFolder)
        {
            var path = PrefabPath(outputFolder, root.name);
            try
            {
                ApplyTints(root, sources.Config);
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

        /// <summary>
        /// Tints every graphic of a <see cref="UiTint"/> under
        /// <paramref name="root"/> with <paramref name="config"/>'s colour for
        /// its role; on a nested instance the colour is kept as that
        /// instance's override, so the hand-made prefab stays unwritten.
        /// </summary>
        private static void ApplyTints(GameObject root, UiConfig config)
        {
            var tints = root.GetComponentsInChildren<UiTint>(true);
            for (var i = 0; i < tints.Length; i++)
            {
                tints[i].Apply(config);
                using (var serialized = new SerializedObject(tints[i]))
                {
                    var target = Property(serialized, TintTargetField).objectReferenceValue;
                    if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
                    {
                        Record(target);
                    }
                }
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

        private static void PlaceSheetButton(RectTransform button, float offsetX)
        {
            UiBuilder.Place(button, new Vector2(AnchorMid, AnchorLow), new Vector2(offsetX, PanelButtonPosY), new Vector2(SheetButtonWidth, SheetButtonHeight));
            Record(button);
        }

        private static TMP_Text SetNestedLabel(RectTransform button, string text)
        {
            var label = Single<TMP_Text>(button.gameObject, button.name);
            label.text = text;
            Record(label);
            return label;
        }

        /// <summary>The panel sprite behind a Store card, and its height in the Store's column.</summary>
        private static void AddCardFrame(RectTransform card, Sources sources)
        {
            var image = card.gameObject.AddComponent<Image>();
            image.sprite = sources.Panel;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = CardHeight;
        }

        /// <summary>A Store card's icon at its left; tinted by <paramref name="role"/> when it has one.</summary>
        private static void AddCardIcon(RectTransform card, Sprite sprite, string colorHex, UiColorRole? role)
        {
            var icon = UiBuilder.CreateRect(IconName, card);
            UiBuilder.Place(icon, new Vector2(AnchorLow, AnchorMid), new Vector2(CardIconInset, 0f), new Vector2(CardIconSize, CardIconSize));
            var image = UiBuilder.AddIcon(icon, sprite, ParseColor(colorHex));
            if (role.HasValue)
            {
                AddTint(icon.gameObject, image, role.Value);
            }
        }

        /// <summary>A Button Primary at a Store card's right, at the card button's size.</summary>
        private static RectTransform AddCardButton(RectTransform card, GameObject buttonPrefab)
        {
            var button = Nest(buttonPrefab, card);
            UiBuilder.Place(
                button, new Vector2(AnchorHigh, AnchorMid), new Vector2(-CardButtonInset, 0f), new Vector2(CardButtonWidth, CardButtonHeight));
            Record(button);
            return button;
        }

        /// <summary>Spans a card's text area, between the icon and the button, from <paramref name="yMin"/> to <paramref name="yMax"/> of its height.</summary>
        private static void CardTextBand(RectTransform rect, float yMin, float yMax)
        {
            UiBuilder.SetAnchors(rect, new Vector2(AnchorLow, yMin), new Vector2(AnchorHigh, yMax));
            rect.offsetMin = new Vector2(CardTextLeft, 0f);
            rect.offsetMax = new Vector2(-CardTextRight, 0f);
        }

        /// <summary>A vertical layout that sizes its children to its width and to their preferred heights.</summary>
        private static VerticalLayoutGroup AddColumn(RectTransform rect)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = StoreSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>One line of the Store's column — a section header, or the test store line — at <paramref name="height"/>.</summary>
        private static TMP_Text AddSectionLine(
            RectTransform column, Sources sources, string name, string text, float height, float fontSize, bool isCentred)
        {
            var rect = UiBuilder.CreateRect(name, column);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            var label = UiBuilder.AddLabel(rect, sources.Font, fontSize, ParseColor(PageTitleColorHex), isCentred);
            label.text = text;
            label.alignment = isCentred ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
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

        /// <summary>A centred label that wraps within its rect, in the Button Primary font.</summary>
        private static TextMeshProUGUI AddWrappingText(RectTransform rect, Sources sources, string text, float fontSize, string colorHex)
        {
            var label = UiBuilder.AddLabel(rect, sources.Font, fontSize, ParseColor(colorHex), true);
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
            public OverlayFrame(GameObject root, RectTransform panel, Button close, TMP_Text title)
            {
                Root = root;
                Panel = panel;
                Close = close;
                Title = title;
            }

            public GameObject Root { get; }
            public RectTransform Panel { get; }

            /// <summary>The close button; null for a frame built without one.</summary>
            public Button Close { get; }

            public TMP_Text Title { get; }
        }

        /// <summary>The saved overlay prefabs Front End nests, in stacking order.</summary>
        private readonly struct OverlayAssets
        {
            public OverlayAssets(
                GameObject settings, GameObject levels, GameObject lives, GameObject notEnoughGold, GameObject message, GameObject testPurchase)
            {
                Settings = settings;
                Levels = levels;
                Lives = lives;
                NotEnoughGold = notEnoughGold;
                Message = message;
                TestPurchase = testPurchase;
            }

            public GameObject Settings { get; }
            public GameObject Levels { get; }
            public GameObject Lives { get; }
            public GameObject NotEnoughGold { get; }
            public GameObject Message { get; }
            public GameObject TestPurchase { get; }
        }

        /// <summary>Every asset the build reads, loaded and checked before anything is written.</summary>
        private sealed class Sources
        {
            public int Layer { get; private set; }
            public TMP_FontAsset Font { get; private set; }
            public UiConfig Config { get; private set; }
            public Sprite Panel { get; private set; }
            public Sprite Ribbon { get; private set; }
            public Sprite SoundOn { get; private set; }
            public Sprite SoundOff { get; private set; }
            public Sprite Heart { get; private set; }
            public Sprite Coin { get; private set; }
            public Sprite CoinStack { get; private set; }
            public Sprite Clock { get; private set; }
            public Sprite Rocket { get; private set; }
            public Sprite Broom { get; private set; }
            public GameObject ButtonClose { get; private set; }
            public GameObject ButtonPrimary { get; private set; }
            public GameObject ButtonRound { get; private set; }
            public GameObject ButtonSecondary { get; private set; }
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

                // The texts written from the config are formatted here, so a
                // config that would throw is refused before anything is written.
                var problems = config.Problems();
                if (problems.Count > 0)
                {
                    throw new InvalidOperationException($"{UiConfigPath} has problems: {string.Join(" ", problems)} Nothing was written.");
                }

                return new Sources
                {
                    Layer = layer,
                    Font = font,
                    Config = config,
                    Panel = LoadSprite(PanelSpritePath),
                    Ribbon = LoadSprite(RibbonSpritePath),
                    SoundOn = LoadSprite(SoundOnSpritePath),
                    SoundOff = LoadSprite(SoundOffSpritePath),
                    Heart = LoadSprite(HeartSpritePath),
                    Coin = LoadSprite(CoinSpritePath),
                    CoinStack = LoadSprite(CoinStackSpritePath),
                    Clock = LoadSprite(ClockSpritePath),
                    Rocket = LoadSprite(RocketSpritePath),
                    Broom = LoadSprite(BroomSpritePath),
                    ButtonClose = LoadHandMade(ButtonCloseName),
                    ButtonPrimary = buttonPrimary,
                    ButtonRound = LoadHandMade(ButtonRoundName),
                    ButtonSecondary = LoadHandMade(ButtonSecondaryName),
                    HomePage = LoadHandMade(HomePageName),
                    TabBar = LoadHandMade(TabBarName),
                    TopBar = LoadHandMade(TopBarName),
                    LevelButton = Single<LevelButton>(LoadHandMade(LevelButtonName), LevelButtonName)
                };
            }
        }
    }
}
