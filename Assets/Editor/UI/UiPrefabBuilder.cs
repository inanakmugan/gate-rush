using System;
using System.Collections.Generic;
using GateRush.Meta;
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
    /// Settings, Levels and Lives overlays and Front End, Module 36's Gold
    /// Pack Card and Not Enough Gold, Message and Test Purchase overlays, with
    /// the Store Page now holding the Store, and Module 37's Streak Marker,
    /// Streak Bar, Streak Ladder Row, Win Streak Overlay, Streak Prize and
    /// Streak Reward Overlay, and Module 38's Avatar Button and Profile
    /// Overlay, with the Settings overlay now holding the Haptics row, and
    /// Module 39's Leaderboard Row, with the Leaderboard Page now holding the
    /// leaderboard, and Module 40's Gold Packs Overlay, Level HUD, Joker Bar
    /// and Level UI — from code, with every serialized field wired.
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
    /// <para><b>The streak bar sits inside Home Page without writing it.</b>
    /// It is nested under the Home Page instance's Streak Area in Front End,
    /// where it is recorded as an object added to that instance.</para>
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

        private const string JokerIconsPath = "Assets/Config/JokerIcons.asset";
        private const string FireSpritePath = "Assets/ThirdParty/Kenney/BoardGameIcons/fire.png";
        private const string CheckSpritePath = "Assets/ThirdParty/Kenney/UIPack/Green/icon_checkmark.png";

        private const string AvatarCatalogPath = "Assets/Config/AvatarCatalog.asset";
        private const string PhoneSpritePath = "Assets/ThirdParty/Kenney/GameIcons/phone.png";
        private const string CrossSpritePath = "Assets/ThirdParty/Kenney/GameIcons/cross.png";

        // The Leaderboard's rank mark: white, tinted gold, silver and bronze at runtime.
        private const string MedalSpritePath = "Assets/ThirdParty/Kenney/GameIcons/medal1.png";

        // The streak bar's track and fill: near-white, so a tint shows true.
        // Sliced, so it must have a border.
        private const string BarSpritePath = "Assets/ThirdParty/Kenney/UIPack/Extra/input_rectangle.png";

        // Module 40: the HUD's Menu and Restart icons and the timer's hourglass.
        private const string HomeSpritePath = "Assets/ThirdParty/Kenney/GameIcons/home.png";
        private const string ReturnSpritePath = "Assets/ThirdParty/Kenney/GameIcons/return.png";
        private const string HourglassSpritePath = "Assets/ThirdParty/Kenney/BoardGameIcons/hourglass.png";
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

        // The child of Home Page the streak bar is placed under.
        private const string StreakAreaName = "Streak Area";

        // Built prefabs.
        private const string GoldPackCardName = "Gold Pack Card";
        private const string StorePageName = "Store Page";
        private const string LeaderboardRowName = "Leaderboard Row";
        private const string LeaderboardPageName = "Leaderboard Page";
        private const string SettingsOverlayName = "Settings Overlay";
        private const string LevelsOverlayName = "Levels Overlay";
        private const string LivesOverlayName = "Lives Overlay";
        private const string NotEnoughGoldOverlayName = "Not Enough Gold Overlay";
        private const string MessageOverlayName = "Message Overlay";
        private const string TestPurchaseOverlayName = "Test Purchase Overlay";
        private const string StreakMarkerName = "Streak Marker";
        private const string StreakBarName = "Streak Bar";
        private const string StreakLadderRowName = "Streak Ladder Row";
        private const string WinStreakOverlayName = "Win Streak Overlay";
        private const string StreakPrizeName = "Streak Prize";
        private const string StreakRewardOverlayName = "Streak Reward Overlay";
        private const string AvatarButtonName = "Avatar Button";
        private const string ProfileOverlayName = "Profile Overlay";
        private const string FrontEndName = "Front End";
        private const string GoldPacksOverlayName = "Gold Packs Overlay";
        private const string LevelHudName = "Level HUD";
        private const string JokerBarName = "Joker Bar";
        private const string LevelUiName = "Level UI";

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
        private const string HighlightName = "Highlight";
        private const string DiscName = "Disc";
        private const string CountName = "Count";
        private const string CheckName = "Check";
        private const string FireName = "Fire";
        private const string TrackName = "Track";
        private const string FillName = "Fill";
        private const string MarkersName = "Markers";
        private const string LineName = "Line";
        private const string WinsName = "Wins";
        private const string RewardName = "Reward";
        private const string StateName = "State";
        private const string StreakName = "Streak";
        private const string BestName = "Best";
        private const string RuleName = "Rule";
        private const string HeadlineName = "Headline";
        private const string PrizesName = "Prizes";
        private const string PrizeLabelName = "Label";
        private const string HapticsRowName = "Haptics Row";
        private const string HapticsOnLookName = "Haptics On Look";
        private const string HapticsOffLookName = "Haptics Off Look";
        private const string CrossName = "Cross";
        private const string SelectedName = "Selected";
        private const string AvatarName = "Avatar";
        private const string NameFieldName = "Name Field";
        private const string TextAreaName = "Text Area";
        private const string HintName = "Hint";
        private const string RefusalName = "Refusal";
        private const string StatisticsName = "Statistics";
        private const string LevelsCompletedName = "Levels Completed";
        private const string CampaignLevelName = "Campaign Level";
        private const string BestStreakName = "Best Streak";
        private const string LifetimeGoldName = "Lifetime Gold";
        private const string StatisticLabelName = "Label";
        private const string StatisticValueName = "Value";
        private const string MedalName = "Medal";
        private const string RankName = "Rank";
        private const string ScoreName = "Score";
        private const string StatusName = "Status";
        private const string RetryName = "Retry";
        private const string RowName = "Row";
        private const string MenuName = "Menu";
        private const string RestartName = "Restart";
        private const string TimerName = "Timer";
        private const string HourglassName = "Hourglass";
        private const string DigitsName = "Digits";
        private const string TimeBonusName = "Time Bonus";
        private const string LevelLabelName = "Level";
        private const string GoldPillName = "Gold Pill";
        private const string GoldLabelName = "Gold";
        private const string LivesPillName = "Lives Pill";
        private const string LiftName = "Lift";
        private const string RingName = "Ring";
        private const string ButtonName = "Button";
        private const string BadgeName = "Badge";
        private const string BadgeLabelName = "Label";
        private const string PacksName = "Packs";

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
        private const string RefillLivesText = "Refill Lives";
        private const string GetGoldText = "Get Gold";
        private const string OkText = "OK";
        private const string BuyText = "Buy";
        private const string CancelText = "Cancel";
        private const string WinStreakTitle = "Win Streak";
        private const string StreakRewardTitle = "Streak Reward";
        private const string CollectText = "Collect";

        // Placeholders for the Store's numbers, from the defaults of D72.
        private const int GoldPlaceholder = 1500;
        private const int CountPlaceholder = 1;
        private const int PricePlaceholder = 100;
        private const int ShortfallPlaceholder = 100;
        private const string PriceTextPlaceholder = "$2.49";
        private const string RibbonPlaceholder = "Popular";
        private const string ProductIdPlaceholder = "gold_medium";

        // Placeholders for the win streak's numbers: the reward table of D61,
        // a streak between its first two rewards, and a best above it.
        private const int PlaceholderClockWins = 3;
        private const int PlaceholderRocketWins = 5;
        private const int PlaceholderBroomWins = 10;
        private const int PlaceholderRewardCount = 1;
        private const int PlaceholderMarkerCount = 2;
        private const int StreakPlaceholder = 4;
        private const int BestStreakPlaceholder = 7;

        // Placeholders for Profile: a name, and statistics of a player some
        // way into the campaign, with lifetime gold that shows a separator.
        private const string NamePlaceholder = "Player";
        private const int LevelsCompletedPlaceholder = 12;
        private const int CampaignLevelPlaceholder = 13;
        private const int LifetimeGoldPlaceholder = 1250;

        // The placeholder Leaderboard Row: the first rank, so its medal shows,
        // with a score that shows a separator.
        private const int RankPlaceholder = 1;
        private const int ScorePlaceholder = 1250;

        // Module 40's placeholders: the level's HUD and joker bar as the
        // Editor shows them.
        private const int LevelNumberPlaceholder = 1;
        private const float TimerPlaceholderSeconds = 75f;
        private const int TimeBonusPlaceholder = 20;
        private const int LivesCountPlaceholder = 5;

        // Colours.
        private const string PageTitleColorHex = "#FFFFFF";
        private const string OverlayTextColorHex = "#3B2466";
        private const string LivesLabelColorHex = "#FFFFFF";
        private const string IconColorHex = "#FFFFFF";
        private const string RibbonLabelColorHex = "#FFFFFF";
        private const string ScrimColorHex = "#000000";
        private const byte ScrimAlpha = 153;
        private const string ViewportColorHex = "#FFFFFF00";
        private const string PanelColorHex = "#FFFFFF";

        // The streak bar's track: a dark tint of the theme's purple.
        private const string StreakTrackColorHex = "#2E1A52";

        // The cross over the haptics phone: red, since a white cross would not
        // show on the white phone.
        private const string HapticsCrossColorHex = "#E8384F";

        // The ring behind the chosen avatar in Profile's grid.
        private const string SelectedAvatarColorHex = "#FFCC33";

        // Under Profile's name field: the muted hint, and the refusal line.
        private const string NameHintColorHex = "#6E7090";
        private const string NameRefusalColorHex = "#D93A4A";

        // The HUD's pills and the joker badges, the top bar's pill colour, and
        // the HUD's white text.
        private const string PillColorHex = "#2E1A52";
        private const string HudTextColorHex = "#FFFFFF";

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

        // Settings overlay: the Haptics row under the Sound row, one row
        // height and a gap lower.
        private const float SettingsPanelWidth = 820f;
        private const float SettingsPanelHeight = 880f;
        private const float SoundRowInset = 80f;
        private const float SoundRowPosY = -200f;
        private const float SoundRowHeight = 140f;
        private const float SettingsRowSpacing = 40f;
        private const float HapticsRowPosY = SoundRowPosY - SoundRowHeight - SettingsRowSpacing;
        private const float SoundLabelWidthFraction = 0.5f;
        private const float SoundLabelFontSize = 64f;

        // The cross over the haptics phone: inset by this fraction of the
        // icon on every side, so it covers its middle 70%.
        private const float HapticsCrossInset = 0.15f;

        // Avatar Button, one cell of Profile's grid. The grid sets its size;
        // this one is the prefab's own, the same, for the Editor.
        private const float AvatarCellSize = 140f;
        private const float AvatarIconInset = 6f;
        private const float AvatarSelectedOutset = 8f;

        // Profile overlay.
        private const float ProfilePanelWidth = 900f;
        private const float ProfilePanelHeight = 1600f;
        private const float ProfileAvatarSize = 260f;
        private const float ProfileAvatarPosY = -160f;
        private const float NameFieldWidth = 640f;
        private const float NameFieldHeight = 120f;
        private const float NameFieldPosY = -440f;
        private const float NameFieldTextInset = 24f;
        private const float NameFontSize = 64f;
        private const float NameHintPosY = -570f;
        private const float NameRefusalPosY = -620f;
        private const float NameLineHeight = 50f;
        private const float NameLineFontSize = 36f;
        private const float ProfileGridTop = 690f;
        private const float ProfileGridBottom = 420f;
        private const int ProfileGridColumns = 5;
        private const float StatisticsInset = 40f;
        private const float StatisticsPosY = 40f;
        private const float StatisticsHeight = 340f;
        private const float StatisticLabelFontSize = 40f;
        private const float StatisticValueFontSize = 60f;

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

        // Streak Marker, centred on the bar's track at its reward's place.
        private const float MarkerSize = 84f;
        private const float MarkerHighlightOutset = 8f;
        private const float MarkerIconSize = 56f;
        private const float MarkerCountWidth = 56f;
        private const float MarkerCountHeight = 34f;
        private const float MarkerCountFontSize = 30f;
        private const float CheckWidth = 40f;
        private const float CheckHeight = 36f;
        private const float CheckOffset = 14f;

        // Streak Bar. Front End stretches it to Home Page's Streak Area; this
        // size is the area's at the reference resolution, for the Editor. The
        // track's right inset leaves room for half of the last marker.
        private const float StreakBarPreviewWidth = 1000f;
        private const float StreakBarPreviewHeight = 200f;
        private const float FireInset = 20f;
        private const float FireSize = 110f;
        private const float StreakCountLeft = 140f;
        private const float StreakCountWidth = 80f;
        private const float StreakCountFontSize = 64f;
        private const float TrackLeft = 240f;
        private const float TrackRight = 70f;
        private const float TrackRaise = 30f;
        private const float TrackHeight = 36f;
        private const float LineLeft = 240f;
        private const float LineRight = 20f;
        private const float LineBottom = 10f;
        private const float LineHeight = 56f;
        private const float LineFontSize = 40f;

        // Streak Ladder Row. Its width is the ladder's; this one is the
        // prefab's own, for the Editor.
        private const float LadderRowPreviewWidth = 780f;
        private const float LadderRowHeight = 140f;
        private const float LadderWinsLeft = 30f;
        private const float LadderWinsWidth = 200f;
        private const float LadderIconLeft = 240f;
        private const float LadderIconSize = 90f;
        private const float LadderRewardLeft = 350f;
        private const float LadderRewardRight = 230f;
        private const float LadderStateInset = 30f;
        private const float LadderStateWidth = 200f;
        private const float LadderFontSize = 48f;
        private const float LadderStateFontSize = 44f;

        // Win Streak overlay.
        private const float WinStreakPanelWidth = 900f;
        private const float WinStreakPanelHeight = 1300f;
        private const float StreakLinePosY = -160f;
        private const float StreakLineHeight = 80f;
        private const float StreakLineFontSize = 56f;
        private const float LadderTop = 270f;
        private const float LadderBottom = 300f;
        private const float LadderSpacing = 20f;
        private const float RulePosY = 60f;
        private const float RuleHeight = 220f;
        private const float RuleFontSize = 40f;

        // Streak Prize. Its width is the column's; this one is the prefab's
        // own, for the Editor.
        private const float PrizePreviewWidth = 700f;
        private const float PrizeHeight = 110f;
        private const float PrizeIconLeft = 120f;
        private const float PrizeIconSize = 90f;
        private const float PrizeLabelLeft = 230f;
        private const float PrizeLabelRight = 40f;
        private const float PrizeFontSize = 56f;

        // Streak Reward overlay: tall enough for one line per joker kind
        // between the headline and Collect.
        private const float StreakRewardPanelWidth = 820f;
        private const float StreakRewardPanelHeight = 1040f;
        private const float RewardFireSize = 140f;
        private const float RewardFirePosY = -140f;
        private const float HeadlinePosY = -300f;
        private const float HeadlineHeight = 90f;
        private const float HeadlineFontSize = 56f;
        private const float PrizesPosY = -410f;

        // The Leaderboard Row: the medal, the rank, the avatar and the name
        // from the left; the coin and the score from the right.
        private const float LeaderboardRowPreviewWidth = 1000f;
        private const float LeaderboardRowHeight = 140f;
        private const float RowMedalLeft = 20f;
        private const float RowMedalSize = 72f;
        private const float RowRankLeft = 92f;
        private const float RowRankWidth = 80f;
        private const float RowRankFontSize = 52f;
        private const float RowAvatarLeft = 180f;
        private const float RowAvatarSize = 110f;
        private const float RowNameLeft = 305f;
        private const float RowNameRight = 264f;
        private const float RowNameFontSize = 44f;
        private const float RowCoinRight = 196f;
        private const float RowCoinSize = 56f;
        private const float RowScoreRight = 24f;
        private const float RowScoreWidth = 160f;
        private const float RowScoreFontSize = 48f;

        // The Leaderboard page: the simulated line under the title, the list
        // under it, and the status line and Retry in the middle.
        private const float LeaderboardInset = 40f;
        private const float LeaderboardNotePosY = PageTitlePosY - PageTitleHeight;
        private const float LeaderboardNoteHeight = 100f;
        private const float LeaderboardNoteFontSize = 40f;
        private const float LeaderboardListGap = 20f;
        private const float LeaderboardListTop = -LeaderboardNotePosY + LeaderboardNoteHeight + LeaderboardListGap;
        private const float LeaderboardRowSpacing = 16f;
        private const int LeaderboardListBottomPadding = 40;
        private const float LeaderboardStatusWidth = 900f;
        private const float LeaderboardStatusHeight = 200f;
        private const float LeaderboardStatusFontSize = 52f;
        private const float LeaderboardRetryPosY = -180f;

        // Module 40: the in-level gold packs. The column of Gold Pack Cards
        // sits between the title and the test store line.
        private const float GoldPacksPanelWidth = 860f;
        private const float GoldPacksPanelHeight = 1100f;
        private const float PacksInset = 40f;
        private const float PacksTop = 160f;
        private const float PacksBottom = 200f;
        private const float GoldPacksNoteBottom = 40f;
        private const float GoldPacksNoteHeight = 140f;
        private const float GoldPacksNoteFontSize = 40f;

        // Module 40: the Level HUD's row, laid out at the 1080x1920
        // reference and scaled down by BandFit when the top band (0.14 of
        // the screen's height, 269 units there) is smaller. Menu and Restart
        // keep Button Round's own size; the timer pill is as tall.
        private const float HudRowWidth = 1080f;
        private const float HudRowHeight = 250f;
        private const float HudSideInset = 40f;
        private const float HudTopInset = 20f;
        private const float HudButtonGap = 20f;
        private const float TimerPillWidth = 300f;
        private const float TimerPillHeight = 110f;
        private const float HourglassSize = 70f;
        private const float HourglassInset = 20f;
        private const float TimerDigitsLeft = 100f;
        private const float TimerDigitsRight = 20f;
        private const float TimerFontSize = 64f;
        private const float TimeBonusGap = 16f;
        private const float TimeBonusWidth = 200f;
        private const float TimeBonusFontSize = 64f;
        private const float LevelLabelWidth = 300f;
        private const float LevelLabelFontSize = 60f;
        private const float HudRowGap = 16f;
        private const float HudPillRowTop = HudTopInset + TimerPillHeight + HudRowGap;
        private const float HudPillWidth = 300f;
        private const float HudPillHeight = 80f;
        private const float HudPillGap = 24f;
        private const float PillIconInset = 12f;
        private const float PillCoinSize = 60f;
        private const float PillLabelLeft = 84f;
        private const float PillFontSize = 48f;
        private const float PillHeartInset = 10f;
        private const float PillHeartSize = 70f;
        private const float PillLivesCountFontSize = 40f;
        private const float PillStatusLeft = 90f;
        private const float PillStatusFontSize = 40f;

        // Module 40: the Joker Bar's row, laid out at the reference and
        // scaled down by BandFit when the bottom band (0.10 of the screen's
        // height, 192 units there) is smaller. Each slot sits a little low,
        // so the selected button has room to rise with its ring.
        private const float JokerRowWidth = 1080f;
        private const float JokerRowHeight = 190f;
        private const float JokerSlotWidth = 280f;
        private const float JokerSlotHeight = 150f;
        private const float JokerSlotGap = 40f;
        private const float JokerSlotDrop = 12f;
        private const float JokerRaise = 20f;
        private const float JokerRingOutset = 8f;
        private const float JokerIconSize = 100f;
        private const float JokerIconInset = 30f;
        private const float BadgeWidth = 130f;
        private const float BadgeHeight = 64f;
        private const float BadgeInset = 20f;
        private const int BadgePadding = 10;
        private const float BadgeSpacing = 6f;
        private const float BadgeCoinSize = 44f;
        private const float BadgeFontSize = 44f;

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
        private const string LeaderboardField = "leaderboard";
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
        private const string IconField = "icon";
        private const string CountLabelField = "countLabel";
        private const string CheckField = "check";
        private const string HighlightField = "highlight";
        private const string ButtonField = "button";
        private const string StreakLabelField = "streakLabel";
        private const string FillField = "fill";
        private const string MarkersField = "markers";
        private const string MarkerPrefabField = "markerPrefab";
        private const string LineLabelField = "lineLabel";
        private const string WinsLabelField = "winsLabel";
        private const string RewardLabelField = "rewardLabel";
        private const string StateLabelField = "stateLabel";
        private const string BestLabelField = "bestLabel";
        private const string RowsField = "rows";
        private const string RowPrefabField = "rowPrefab";
        private const string RuleLabelField = "ruleLabel";
        private const string LabelField = "label";
        private const string HeadlineLabelField = "headlineLabel";
        private const string PrizesField = "prizes";
        private const string PrizePrefabField = "prizePrefab";
        private const string CollectButtonField = "collectButton";
        private const string WinStreakField = "winStreak";
        private const string StreakRewardField = "streakReward";
        private const string StreakBarField = "streakBar";
        private const string JokerIconsField = "jokerIcons";
        private const string HapticsLabelField = "hapticsLabel";
        private const string HapticsButtonField = "hapticsButton";
        private const string HapticsOnLookField = "hapticsOnLook";
        private const string HapticsOffLookField = "hapticsOffLook";
        private const string SelectedMarkField = "selectedMark";
        private const string AvatarImageField = "avatarImage";
        private const string NameFieldField = "nameField";
        private const string HintLabelField = "hintLabel";
        private const string RefusalLabelField = "refusalLabel";
        private const string AvatarButtonPrefabField = "avatarButtonPrefab";
        private const string LevelsCompletedLabelField = "levelsCompletedLabel";
        private const string LevelsCompletedValueField = "levelsCompletedValue";
        private const string CampaignLevelLabelField = "campaignLevelLabel";
        private const string CampaignLevelValueField = "campaignLevelValue";
        private const string BestStreakLabelField = "bestStreakLabel";
        private const string BestStreakValueField = "bestStreakValue";
        private const string LifetimeGoldLabelField = "lifetimeGoldLabel";
        private const string LifetimeGoldValueField = "lifetimeGoldValue";
        private const string ProfileField = "profile";
        private const string AvatarCatalogField = "avatarCatalog";
        private const string BackgroundField = "background";
        private const string RankLabelField = "rankLabel";
        private const string MedalField = "medal";
        private const string AvatarField = "avatar";
        private const string NameLabelField = "nameLabel";
        private const string ScoreLabelField = "scoreLabel";
        private const string RetryButtonField = "retryButton";
        private const string RetryLabelField = "retryLabel";
        private const string PacksField = "packs";
        private const string MenuButtonField = "menuButton";
        private const string RestartButtonField = "restartButton";
        private const string TimerField = "timer";
        private const string TimerLabelField = "timerLabel";
        private const string TimeBonusField = "timeBonus";
        private const string TimeBonusLabelField = "timeBonusLabel";
        private const string LevelLabelField = "levelLabel";
        private const string GoldLabelField = "goldLabel";
        private const string LivesPillField = "livesPill";
        private const string LivesCountLabelField = "livesCountLabel";
        private const string LivesStatusLabelField = "livesStatusLabel";
        private const string DesignSizeField = "designSize";
        private const string LiftField = "lift";
        private const string RingField = "ring";
        private const string CoinField = "coin";
        private const string BadgeLabelField = "badgeLabel";
        private const string SelectedRaiseField = "selectedRaise";
        private const string ClockField = "clock";
        private const string RocketField = "rocket";
        private const string BroomField = "broom";
        private const string HudField = "hud";
        private const string JokerBarField = "jokerBar";

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
        /// Builds the twenty-three prefabs into <paramref name="outputFolder"/>,
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
                var leaderboardRow = Save(BuildLeaderboardRow(scene, sources), sources, outputFolder);
                var leaderboardPage = Save(BuildLeaderboardPage(scene, sources, leaderboardRow), sources, outputFolder);
                var settings = Save(BuildSettingsOverlay(scene, sources), sources, outputFolder);
                var levels = Save(BuildLevelsOverlay(scene, sources), sources, outputFolder);
                var lives = Save(BuildLivesOverlay(scene, sources), sources, outputFolder);
                var notEnoughGold = Save(BuildNotEnoughGoldOverlay(scene, sources), sources, outputFolder);
                var message = Save(BuildMessageOverlay(scene, sources), sources, outputFolder);
                var testPurchase = Save(BuildTestPurchaseOverlay(scene, sources), sources, outputFolder);
                var streakMarker = Save(BuildStreakMarker(scene, sources), sources, outputFolder);
                var streakBar = Save(BuildStreakBar(scene, sources, streakMarker), sources, outputFolder);
                var ladderRow = Save(BuildStreakLadderRow(scene, sources), sources, outputFolder);
                var winStreak = Save(BuildWinStreakOverlay(scene, sources, ladderRow), sources, outputFolder);
                var streakPrize = Save(BuildStreakPrize(scene, sources), sources, outputFolder);
                var streakReward = Save(BuildStreakRewardOverlay(scene, sources, streakPrize), sources, outputFolder);
                var avatarButton = Save(BuildAvatarButton(scene, sources), sources, outputFolder);
                var profile = Save(BuildProfileOverlay(scene, sources, avatarButton), sources, outputFolder);
                var overlays = new OverlayAssets(
                    settings, levels, lives, notEnoughGold, message, testPurchase, winStreak, streakReward, profile);
                Save(BuildFrontEnd(scene, sources, storePage, leaderboardPage, streakBar, overlays), sources, outputFolder);

                var goldPacks = Save(BuildGoldPacksOverlay(scene, sources, goldPackCard), sources, outputFolder);
                var levelHud = Save(BuildLevelHud(scene, sources), sources, outputFolder);
                var jokerBar = Save(BuildJokerBar(scene, sources), sources, outputFolder);
                var levelOverlays = new LevelOverlayAssets(notEnoughGold, goldPacks, testPurchase, message);
                Save(BuildLevelUi(scene, sources, levelHud, jokerBar, levelOverlays), sources, outputFolder);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static string[] OutputNames() => new[]
        {
            GoldPackCardName, StorePageName, LeaderboardRowName, LeaderboardPageName, SettingsOverlayName, LevelsOverlayName, LivesOverlayName,
            NotEnoughGoldOverlayName, MessageOverlayName, TestPurchaseOverlayName, StreakMarkerName, StreakBarName,
            StreakLadderRowName, WinStreakOverlayName, StreakPrizeName, StreakRewardOverlayName, AvatarButtonName,
            ProfileOverlayName, FrontEndName, GoldPacksOverlayName, LevelHudName, JokerBarName, LevelUiName
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

            var viewport = AddViewport(scroll);

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
            var clock = BuildJokerCard(content, sources, ClockCardName, JokerKind.Clock, jokerSub, jokerPrice);
            var rocket = BuildJokerCard(content, sources, RocketCardName, JokerKind.Rocket, jokerSub, jokerPrice);
            var broom = BuildJokerCard(content, sources, BroomCardName, JokerKind.Broom, jokerSub, jokerPrice);

            var livesHeader = AddSectionLine(content, sources, LivesHeaderName, config.LivesHeader, SectionHeaderHeight, SectionHeaderFontSize, false);
            var livesCard = BuildItemCard(
                content, sources, LivesCardName, sources.Heart, IconColorHex, UiColorRole.Heart, RefillLivesText,
                config.LivesFullLabel, config.LivesFullLabel);

            var note = AddSectionLine(content, sources, TestStoreNoteName, config.TestStoreNote, TestStoreNoteHeight, TestStoreNoteFontSize, true);

            var scrollRect = AddVerticalScroll(scroll, viewport, content);

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
        /// One row of the Leaderboard, the prefab the page instantiates per
        /// entry: the panel, the medal and the rank, the avatar, the name, and
        /// the score beside the coin. Shown as the first rank in the row
        /// colour; the screen sets every colour at runtime.
        /// </summary>
        private static GameObject BuildLeaderboardRow(Scene scene, Sources sources)
        {
            var config = sources.Config;
            var root = CreateRoot(LeaderboardRowName, scene, sources.Layer);
            var rect = (RectTransform)root.transform;
            UiBuilder.PlaceCentered(rect, new Vector2(LeaderboardRowPreviewWidth, LeaderboardRowHeight));
            var background = AddSliced(rect, sources.Panel, PanelColorHex);
            background.color = config.LeaderboardRowColor;
            root.AddComponent<LayoutElement>().preferredHeight = LeaderboardRowHeight;

            config.TryGetMedalColor(RankPlaceholder, out var medalColor);
            var medal = UiBuilder.CreateRect(MedalName, rect);
            UiBuilder.Place(medal, new Vector2(AnchorLow, AnchorMid), new Vector2(RowMedalLeft, 0f), new Vector2(RowMedalSize, RowMedalSize));
            var medalImage = UiBuilder.AddIcon(medal, sources.Medal, medalColor);

            var rank = UiBuilder.CreateRect(RankName, rect);
            UiBuilder.SetAnchors(rank, new Vector2(AnchorLow, AnchorLow), new Vector2(AnchorLow, AnchorHigh));
            rank.offsetMin = new Vector2(RowRankLeft, 0f);
            rank.offsetMax = new Vector2(RowRankLeft + RowRankWidth, 0f);
            var rankLabel = AddText(rank, sources, LeaderboardTexts.Rank(RankPlaceholder, config), RowRankFontSize, OverlayTextColorHex);

            var avatar = UiBuilder.CreateRect(AvatarName, rect);
            UiBuilder.Place(avatar, new Vector2(AnchorLow, AnchorMid), new Vector2(RowAvatarLeft, 0f), new Vector2(RowAvatarSize, RowAvatarSize));
            var avatarImage = UiBuilder.AddIcon(avatar, sources.AvatarCatalog.SpriteAt(0), ParseColor(IconColorHex));

            var nameLabel = AddBandText(rect, sources, CardNameLabelName, RowNameLeft, RowNameRight, NamePlaceholder, RowNameFontSize);
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            var coin = UiBuilder.CreateRect(CoinName, rect);
            UiBuilder.Place(coin, new Vector2(AnchorHigh, AnchorMid), new Vector2(-RowCoinRight, 0f), new Vector2(RowCoinSize, RowCoinSize));
            var coinImage = UiBuilder.AddIcon(coin, sources.Coin, ParseColor(IconColorHex));
            AddTint(coin.gameObject, coinImage, UiColorRole.Coin);

            var score = UiBuilder.CreateRect(ScoreName, rect);
            UiBuilder.SetAnchors(score, new Vector2(AnchorHigh, AnchorLow), new Vector2(AnchorHigh, AnchorHigh));
            score.offsetMin = new Vector2(-(RowScoreRight + RowScoreWidth), 0f);
            score.offsetMax = new Vector2(-RowScoreRight, 0f);
            var scoreLabel = AddText(score, sources, LeaderboardTexts.Score(ScorePlaceholder, config), RowScoreFontSize, OverlayTextColorHex);
            scoreLabel.alignment = TextAlignmentOptions.MidlineRight;

            var row = root.AddComponent<LeaderboardRow>();
            SetReferences(
                row,
                (BackgroundField, background),
                (RankLabelField, rankLabel),
                (MedalField, medalImage),
                (AvatarField, avatarImage),
                (NameLabelField, nameLabel),
                (ScoreLabelField, scoreLabel));
            return root;
        }

        /// <summary>
        /// The Leaderboard tab's page: its title, the line saying the table is
        /// simulated, the list in a vertical scroll whose rows are created at
        /// runtime, and the status line and Retry, saved hidden, in the
        /// middle.
        /// </summary>
        private static GameObject BuildLeaderboardPage(Scene scene, Sources sources, GameObject rowAsset)
        {
            var config = sources.Config;
            var root = BuildPage(scene, sources, LeaderboardPageName, LeaderboardTitle);

            var note = UiBuilder.CreateRect(NoteName, root.transform);
            TopStretch(note, LeaderboardInset, LeaderboardNotePosY, LeaderboardNoteHeight);
            var noteLabel = AddWrappingText(note, sources, config.LeaderboardSimulatedNote, LeaderboardNoteFontSize, PageTitleColorHex);

            var scroll = UiBuilder.CreateRect(ScrollViewName, root.transform);
            UiBuilder.Stretch(scroll);
            scroll.offsetMin = new Vector2(LeaderboardInset, LeaderboardInset);
            scroll.offsetMax = new Vector2(-LeaderboardInset, -LeaderboardListTop);
            var viewport = AddViewport(scroll);
            var content = UiBuilder.CreateRect(ContentName, viewport);
            TopStretch(content, 0f, 0f, 0f);
            var layout = AddColumn(content);
            layout.spacing = LeaderboardRowSpacing;
            layout.padding = new RectOffset(0, 0, 0, LeaderboardListBottomPadding);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrollRect = AddVerticalScroll(scroll, viewport, content);

            var status = UiBuilder.CreateRect(StatusName, root.transform);
            UiBuilder.PlaceCentered(status, new Vector2(LeaderboardStatusWidth, LeaderboardStatusHeight));
            var statusLabel = AddWrappingText(status, sources, config.LeaderboardLoadingText, LeaderboardStatusFontSize, PageTitleColorHex);
            status.gameObject.SetActive(false);

            var retry = Nest(sources.ButtonPrimary, root.transform);
            retry.name = RetryName;
            UiBuilder.Place(retry, new Vector2(AnchorMid, AnchorMid), new Vector2(0f, LeaderboardRetryPosY), retry.sizeDelta);
            Record(retry);
            Record(retry.gameObject);
            var retryLabel = SetNestedLabel(retry, config.LeaderboardRetryLabel);
            retry.gameObject.SetActive(false);
            Record(retry.gameObject);

            var screen = root.AddComponent<LeaderboardScreen>();
            SetReferences(
                screen,
                (ScrollRectField, scrollRect),
                (NoteLabelField, noteLabel),
                (StatusLabelField, statusLabel),
                (RetryButtonField, Single<Button>(retry.gameObject, ButtonPrimaryName)),
                (RetryLabelField, retryLabel),
                (RowsField, content),
                (RowPrefabField, Single<LeaderboardRow>(rowAsset, LeaderboardRowName)));
            return root;
        }

        /// <summary>A joker's Store card, with the icon from the Joker Icons asset and the name from the UI Config.</summary>
        private static StoreItemCard BuildJokerCard(
            RectTransform parent, Sources sources, string name, JokerKind kind, string subText, string priceText)
        {
            return BuildItemCard(
                parent, sources, name, sources.JokerIcons.IconOf(kind), OverlayTextColorHex, null,
                sources.Config.JokerName(kind), subText, priceText);
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

            var sound = AddToggleRow(frame.Panel, sources, SoundRowName, SoundText, SoundRowPosY);
            var soundOnLook = AddLook(sound.Toggle, sound.OwnIcon, SoundOnLookName, sources.SoundOn);
            var soundOffLook = AddLook(sound.Toggle, sound.OwnIcon, SoundOffLookName, sources.SoundOff);
            soundOffLook.SetActive(false);

            // Off is the phone with the red cross over its middle.
            var haptics = AddToggleRow(frame.Panel, sources, HapticsRowName, sources.Config.HapticsLabel, HapticsRowPosY);
            var hapticsOnLook = AddLook(haptics.Toggle, haptics.OwnIcon, HapticsOnLookName, sources.Phone);
            var hapticsOffLook = AddLook(haptics.Toggle, haptics.OwnIcon, HapticsOffLookName, sources.Phone);
            var cross = UiBuilder.CreateRect(CrossName, hapticsOffLook.transform);
            UiBuilder.SetAnchors(
                cross, new Vector2(HapticsCrossInset, HapticsCrossInset), new Vector2(AnchorHigh - HapticsCrossInset, AnchorHigh - HapticsCrossInset));
            UiBuilder.AddIcon(cross, sources.Cross, ParseColor(HapticsCrossColorHex));
            hapticsOffLook.SetActive(false);

            var levels = Nest(sources.ButtonPrimary, frame.Panel);
            PlaceAtPanelBottom(levels);
            SetNestedLabel(levels, LevelsButtonText);

            var overlay = frame.Root.AddComponent<SettingsOverlay>();
            SetReferences(
                overlay,
                (CloseButtonField, frame.Close),
                (SoundButtonField, sound.Button),
                (SoundOnLookField, soundOnLook),
                (SoundOffLookField, soundOffLook),
                (HapticsLabelField, haptics.Label),
                (HapticsButtonField, haptics.Button),
                (HapticsOnLookField, hapticsOnLook),
                (HapticsOffLookField, hapticsOffLook),
                (LevelsButtonField, Single<Button>(levels.gameObject, ButtonPrimaryName)));
            return frame.Root;
        }

        /// <summary>
        /// One row of the Settings overlay at <paramref name="posY"/>: the
        /// label at its left and a Button Round at its right, with no
        /// transition, whose own icon is switched off on this instance for the
        /// two looks the caller adds in its place.
        /// </summary>
        private static ToggleRow AddToggleRow(RectTransform panel, Sources sources, string name, string labelText, float posY)
        {
            var row = UiBuilder.CreateRect(name, panel);
            TopStretch(row, SoundRowInset, posY, SoundRowHeight);
            var labelRect = UiBuilder.CreateRect(SoundLabelName, row);
            UiBuilder.SetAnchors(labelRect, new Vector2(AnchorLow, AnchorLow), new Vector2(SoundLabelWidthFraction, AnchorHigh));
            var label = AddText(labelRect, sources, labelText, SoundLabelFontSize, OverlayTextColorHex);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            var toggle = Nest(sources.ButtonRound, row);
            UiBuilder.Place(toggle, new Vector2(AnchorHigh, AnchorMid), Vector2.zero, toggle.sizeDelta);
            Record(toggle);
            var button = Single<Button>(toggle.gameObject, ButtonRoundName);
            button.transition = Selectable.Transition.None;
            Record(button);

            // The looks take the place of Button Round's own icon, which stays
            // in the prefab and is switched off on this instance.
            var ownIcon = toggle.Find(ButtonRoundIconName) as RectTransform;
            if (ownIcon == null)
            {
                throw new InvalidOperationException($"{ButtonRoundName} has no child '{ButtonRoundIconName}' for the {name} looks to replace.");
            }

            ownIcon.gameObject.SetActive(false);
            Record(ownIcon.gameObject);
            return new ToggleRow(toggle, ownIcon, button, label);
        }

        private static GameObject BuildLevelsOverlay(Scene scene, Sources sources)
        {
            var frame = BuildOverlayFrame(scene, sources, LevelsOverlayName, LevelsTitle, LevelsPanelWidth, LevelsPanelHeight, true);

            var scroll = UiBuilder.CreateRect(ScrollViewName, frame.Panel);
            UiBuilder.Stretch(scroll);
            scroll.offsetMin = new Vector2(ScrollInset, ScrollInset);
            scroll.offsetMax = new Vector2(-ScrollInset, -ScrollTop);

            var viewport = AddViewport(scroll);

            var content = UiBuilder.CreateRect(ContentName, viewport);
            TopStretch(content, 0f, 0f, 0f);
            AddCenteredGrid(
                content, ((RectTransform)sources.LevelButton.transform).sizeDelta, LevelGridColumns,
                LevelsPanelWidth - (ScrollInset + ScrollInset), LevelButtonName);

            var scrollRect = AddVerticalScroll(scroll, viewport, content);

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
        /// One reward's marker on the streak bar, the prefab the bar
        /// instantiates per reward: the highlight behind (shown on the next
        /// reward), the disc, the joker's icon, its count and the check
        /// (shown once reached). Centred, so the bar only moves its anchor.
        /// </summary>
        private static GameObject BuildStreakMarker(Scene scene, Sources sources)
        {
            var root = CreateRoot(StreakMarkerName, scene, sources.Layer);
            var rect = (RectTransform)root.transform;
            UiBuilder.PlaceCentered(rect, new Vector2(MarkerSize, MarkerSize));

            var highlight = UiBuilder.CreateRect(HighlightName, rect);
            UiBuilder.Stretch(highlight);
            highlight.offsetMin = new Vector2(-MarkerHighlightOutset, -MarkerHighlightOutset);
            highlight.offsetMax = new Vector2(MarkerHighlightOutset, MarkerHighlightOutset);
            var highlightImage = AddSliced(highlight, sources.Panel, PanelColorHex);
            AddTint(highlight.gameObject, highlightImage, UiColorRole.Streak);
            highlight.gameObject.SetActive(false);

            var disc = UiBuilder.CreateRect(DiscName, rect);
            UiBuilder.Stretch(disc);
            AddSliced(disc, sources.Panel, PanelColorHex);

            var icon = UiBuilder.CreateRect(IconName, rect);
            UiBuilder.PlaceCentered(icon, new Vector2(MarkerIconSize, MarkerIconSize));
            var iconImage = UiBuilder.AddIcon(icon, sources.JokerIcons.IconOf(JokerKind.Rocket), ParseColor(OverlayTextColorHex));

            var count = UiBuilder.CreateRect(CountName, rect);
            UiBuilder.Place(count, new Vector2(AnchorHigh, AnchorLow), Vector2.zero, new Vector2(MarkerCountWidth, MarkerCountHeight));
            var countLabel = AddText(
                count, sources, StreakTexts.MarkerCount(PlaceholderMarkerCount, sources.Config), MarkerCountFontSize, OverlayTextColorHex);

            var check = UiBuilder.CreateRect(CheckName, rect);
            UiBuilder.Place(check, new Vector2(AnchorHigh, AnchorHigh), new Vector2(CheckOffset, CheckOffset), new Vector2(CheckWidth, CheckHeight));
            UiBuilder.AddIcon(check, sources.Check, ParseColor(IconColorHex));

            var marker = root.AddComponent<StreakMarker>();
            SetReferences(
                marker,
                (IconField, iconImage),
                (CountLabelField, countLabel),
                (CheckField, check.gameObject),
                (HighlightField, highlight.gameObject));
            return root;
        }

        /// <summary>
        /// The streak bar on Home: one pressable panel holding the fire and
        /// the streak, the track with its fill and the markers' layer, and the
        /// line under it. The markers are created at runtime, one per reward.
        /// </summary>
        private static GameObject BuildStreakBar(Scene scene, Sources sources, GameObject markerAsset)
        {
            var config = sources.Config;
            var placeholder = StreakProgress.Of(StreakPlaceholder, PlaceholderStreakRewards());
            var root = CreateRoot(StreakBarName, scene, sources.Layer);
            var rect = (RectTransform)root.transform;
            UiBuilder.PlaceCentered(rect, new Vector2(StreakBarPreviewWidth, StreakBarPreviewHeight));
            var background = AddSliced(rect, sources.Panel, PanelColorHex);
            background.raycastTarget = true;
            var button = UiBuilder.AddButton(rect, background);

            var fire = UiBuilder.CreateRect(FireName, rect);
            UiBuilder.Place(fire, new Vector2(AnchorLow, AnchorMid), new Vector2(FireInset, 0f), new Vector2(FireSize, FireSize));
            var fireImage = UiBuilder.AddIcon(fire, sources.Fire, ParseColor(IconColorHex));
            AddTint(fire.gameObject, fireImage, UiColorRole.Streak);

            var count = UiBuilder.CreateRect(CountName, rect);
            UiBuilder.Place(count, new Vector2(AnchorLow, AnchorMid), new Vector2(StreakCountLeft, 0f), new Vector2(StreakCountWidth, FireSize));
            var countLabel = AddText(count, sources, StreakTexts.StreakCount(StreakPlaceholder, config), StreakCountFontSize, OverlayTextColorHex);
            countLabel.alignment = TextAlignmentOptions.MidlineLeft;

            // Across the panel between the insets, its middle raised above the panel's.
            var track = UiBuilder.CreateRect(TrackName, rect);
            track.anchorMin = new Vector2(AnchorLow, AnchorMid);
            track.anchorMax = new Vector2(AnchorHigh, AnchorMid);
            track.pivot = new Vector2(AnchorMid, AnchorMid);
            track.offsetMin = new Vector2(TrackLeft, TrackRaise - TrackHeight / 2f);
            track.offsetMax = new Vector2(-TrackRight, TrackRaise + TrackHeight / 2f);
            AddSliced(track, sources.Bar, StreakTrackColorHex);

            var fill = UiBuilder.CreateRect(FillName, track);
            UiBuilder.SetAnchors(fill, new Vector2(AnchorLow, AnchorLow), new Vector2(placeholder.Fill, AnchorHigh));
            var fillImage = AddSliced(fill, sources.Bar, PanelColorHex);
            AddTint(fill.gameObject, fillImage, UiColorRole.Streak);

            var markers = UiBuilder.CreateRect(MarkersName, track);
            UiBuilder.Stretch(markers);

            var line = UiBuilder.CreateRect(LineName, rect);
            BottomStretch(line, 0f, LineBottom, LineHeight);
            line.offsetMin = new Vector2(LineLeft, line.offsetMin.y);
            line.offsetMax = new Vector2(-LineRight, line.offsetMax.y);
            var lineLabel = AddText(line, sources, StreakTexts.BarLine(placeholder, config), LineFontSize, OverlayTextColorHex);
            lineLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var bar = root.AddComponent<StreakBar>();
            SetReferences(
                bar,
                (ButtonField, button),
                (StreakLabelField, countLabel),
                (FillField, fill),
                (MarkersField, markers),
                (MarkerPrefabField, Single<StreakMarker>(markerAsset, StreakMarkerName)),
                (LineLabelField, lineLabel));
            return root;
        }

        /// <summary>One row of the Win Streak ladder: the wins, the joker's icon, the reward and its state.</summary>
        private static GameObject BuildStreakLadderRow(Scene scene, Sources sources)
        {
            var config = sources.Config;
            var reward = new StreakReward(PlaceholderClockWins, JokerKind.Clock, PlaceholderRewardCount);
            var root = CreateRoot(StreakLadderRowName, scene, sources.Layer);
            var rect = (RectTransform)root.transform;
            UiBuilder.PlaceCentered(rect, new Vector2(LadderRowPreviewWidth, LadderRowHeight));
            AddSliced(rect, sources.Panel, PanelColorHex);
            root.AddComponent<LayoutElement>().preferredHeight = LadderRowHeight;

            var wins = UiBuilder.CreateRect(WinsName, rect);
            UiBuilder.SetAnchors(wins, new Vector2(AnchorLow, AnchorLow), new Vector2(AnchorLow, AnchorHigh));
            wins.offsetMin = new Vector2(LadderWinsLeft, 0f);
            wins.offsetMax = new Vector2(LadderWinsLeft + LadderWinsWidth, 0f);
            var winsLabel = AddText(wins, sources, StreakTexts.LadderWins(reward.Wins, config), LadderFontSize, OverlayTextColorHex);
            winsLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var icon = UiBuilder.CreateRect(IconName, rect);
            UiBuilder.Place(icon, new Vector2(AnchorLow, AnchorMid), new Vector2(LadderIconLeft, 0f), new Vector2(LadderIconSize, LadderIconSize));
            var iconImage = UiBuilder.AddIcon(icon, sources.JokerIcons.IconOf(reward.Joker), ParseColor(OverlayTextColorHex));

            var rewardLabel = AddBandText(
                rect, sources, RewardName, LadderRewardLeft, LadderRewardRight, StreakTexts.LadderReward(reward, config), LadderFontSize);

            var state = UiBuilder.CreateRect(StateName, rect);
            UiBuilder.SetAnchors(state, new Vector2(AnchorHigh, AnchorLow), new Vector2(AnchorHigh, AnchorHigh));
            state.offsetMin = new Vector2(-(LadderStateInset + LadderStateWidth), 0f);
            state.offsetMax = new Vector2(-LadderStateInset, 0f);
            var stateLabel = AddText(
                state, sources, StreakTexts.LadderState(StreakRewardState.Next, config), LadderStateFontSize, OverlayTextColorHex);
            stateLabel.alignment = TextAlignmentOptions.MidlineRight;

            var row = root.AddComponent<StreakLadderRow>();
            SetReferences(
                row,
                (WinsLabelField, winsLabel),
                (IconField, iconImage),
                (RewardLabelField, rewardLabel),
                (StateLabelField, stateLabel));
            return root;
        }

        /// <summary>
        /// The Win Streak overlay: the current and best streaks side by side,
        /// the ladder in a scroll view — its rows created at runtime, one per
        /// reward — and the rule at the bottom.
        /// </summary>
        private static GameObject BuildWinStreakOverlay(Scene scene, Sources sources, GameObject rowAsset)
        {
            var config = sources.Config;
            var frame = BuildOverlayFrame(scene, sources, WinStreakOverlayName, WinStreakTitle, WinStreakPanelWidth, WinStreakPanelHeight, true);

            var streak = UiBuilder.CreateRect(StreakName, frame.Panel);
            TopStretch(streak, 0f, StreakLinePosY, StreakLineHeight);
            streak.anchorMax = new Vector2(AnchorMid, AnchorHigh);
            var streakLabel = AddText(streak, sources, StreakTexts.CurrentStreak(StreakPlaceholder, config), StreakLineFontSize, OverlayTextColorHex);

            var best = UiBuilder.CreateRect(BestName, frame.Panel);
            TopStretch(best, 0f, StreakLinePosY, StreakLineHeight);
            best.anchorMin = new Vector2(AnchorMid, AnchorHigh);
            var bestLabel = AddText(best, sources, StreakTexts.BestStreak(BestStreakPlaceholder, config), StreakLineFontSize, OverlayTextColorHex);

            var scroll = UiBuilder.CreateRect(ScrollViewName, frame.Panel);
            UiBuilder.Stretch(scroll);
            scroll.offsetMin = new Vector2(ScrollInset, LadderBottom);
            scroll.offsetMax = new Vector2(-ScrollInset, -LadderTop);
            var viewport = AddViewport(scroll);
            var content = UiBuilder.CreateRect(ContentName, viewport);
            TopStretch(content, 0f, 0f, 0f);
            var layout = AddColumn(content);
            layout.spacing = LadderSpacing;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddVerticalScroll(scroll, viewport, content);

            var rule = UiBuilder.CreateRect(RuleName, frame.Panel);
            BottomStretch(rule, OverlayTextInset, RulePosY, RuleHeight);
            var ruleLabel = AddWrappingText(rule, sources, config.StreakRuleText, RuleFontSize, OverlayTextColorHex);

            var overlay = frame.Root.AddComponent<WinStreakOverlay>();
            SetReferences(
                overlay,
                (CloseButtonField, frame.Close),
                (StreakLabelField, streakLabel),
                (BestLabelField, bestLabel),
                (RowsField, content),
                (RowPrefabField, Single<StreakLadderRow>(rowAsset, StreakLadderRowName)),
                (RuleLabelField, ruleLabel));
            return frame.Root;
        }

        /// <summary>One line of the Streak Reward overlay: a joker's icon and "+1 Rocket".</summary>
        private static GameObject BuildStreakPrize(Scene scene, Sources sources)
        {
            var prize = new JokerPrize(JokerKind.Rocket, PlaceholderRewardCount);
            var root = CreateRoot(StreakPrizeName, scene, sources.Layer);
            var rect = (RectTransform)root.transform;
            UiBuilder.PlaceCentered(rect, new Vector2(PrizePreviewWidth, PrizeHeight));
            root.AddComponent<LayoutElement>().preferredHeight = PrizeHeight;

            var icon = UiBuilder.CreateRect(IconName, rect);
            UiBuilder.Place(icon, new Vector2(AnchorLow, AnchorMid), new Vector2(PrizeIconLeft, 0f), new Vector2(PrizeIconSize, PrizeIconSize));
            var iconImage = UiBuilder.AddIcon(icon, sources.JokerIcons.IconOf(prize.Joker), ParseColor(OverlayTextColorHex));

            var label = AddBandText(
                rect, sources, PrizeLabelName, PrizeLabelLeft, PrizeLabelRight, StreakTexts.Prize(prize, sources.Config), PrizeFontSize);

            var component = root.AddComponent<StreakPrize>();
            SetReferences(component, (IconField, iconImage), (LabelField, label));
            return root;
        }

        /// <summary>
        /// The Streak Reward overlay: the fire, the headline, a column of
        /// prize lines created at runtime, and Collect. No close button:
        /// Collect is its one button, and Back closes it too.
        /// </summary>
        private static GameObject BuildStreakRewardOverlay(Scene scene, Sources sources, GameObject prizeAsset)
        {
            var frame = BuildOverlayFrame(
                scene, sources, StreakRewardOverlayName, StreakRewardTitle, StreakRewardPanelWidth, StreakRewardPanelHeight, false);

            var fire = UiBuilder.CreateRect(FireName, frame.Panel);
            UiBuilder.Place(fire, new Vector2(AnchorMid, AnchorHigh), new Vector2(0f, RewardFirePosY), new Vector2(RewardFireSize, RewardFireSize));
            var fireImage = UiBuilder.AddIcon(fire, sources.Fire, ParseColor(IconColorHex));
            AddTint(fire.gameObject, fireImage, UiColorRole.Streak);

            var headline = UiBuilder.CreateRect(HeadlineName, frame.Panel);
            TopStretch(headline, OverlayTextInset, HeadlinePosY, HeadlineHeight);
            var headlineLabel = AddText(
                headline, sources, StreakTexts.RewardHeadline(PlaceholderRocketWins, sources.Config), HeadlineFontSize, OverlayTextColorHex);

            var prizes = UiBuilder.CreateRect(PrizesName, frame.Panel);
            TopStretch(prizes, OverlayTextInset, PrizesPosY, 0f);
            AddColumn(prizes);
            var fitter = prizes.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var collect = Nest(sources.ButtonPrimary, frame.Panel);
            PlaceAtPanelBottom(collect);
            SetNestedLabel(collect, CollectText);

            var overlay = frame.Root.AddComponent<StreakRewardOverlay>();
            SetReferences(
                overlay,
                (HeadlineLabelField, headlineLabel),
                (PrizesField, prizes),
                (PrizePrefabField, Single<StreakPrize>(prizeAsset, StreakPrizeName)),
                (CollectButtonField, Single<Button>(collect.gameObject, ButtonPrimaryName)));
            return frame.Root;
        }

        /// <summary>
        /// One cell of Profile's avatar grid, the prefab Profile instantiates
        /// per avatar: the ring behind (shown on the chosen one) and the
        /// avatar, which takes the press for the button on the root.
        /// </summary>
        private static GameObject BuildAvatarButton(Scene scene, Sources sources)
        {
            var root = CreateRoot(AvatarButtonName, scene, sources.Layer);
            var rect = (RectTransform)root.transform;
            UiBuilder.PlaceCentered(rect, new Vector2(AvatarCellSize, AvatarCellSize));

            var selected = UiBuilder.CreateRect(SelectedName, rect);
            UiBuilder.Stretch(selected);
            selected.offsetMin = new Vector2(-AvatarSelectedOutset, -AvatarSelectedOutset);
            selected.offsetMax = new Vector2(AvatarSelectedOutset, AvatarSelectedOutset);
            AddSliced(selected, sources.Panel, SelectedAvatarColorHex);
            selected.gameObject.SetActive(false);

            var icon = UiBuilder.CreateRect(IconName, rect);
            UiBuilder.Stretch(icon);
            icon.offsetMin = new Vector2(AvatarIconInset, AvatarIconInset);
            icon.offsetMax = new Vector2(-AvatarIconInset, -AvatarIconInset);
            var iconImage = UiBuilder.AddIcon(icon, sources.AvatarCatalog.SpriteAt(0), ParseColor(IconColorHex));
            iconImage.raycastTarget = true;
            var button = UiBuilder.AddButton(rect, iconImage);

            var cell = root.AddComponent<AvatarButton>();
            SetReferences(
                cell,
                (ButtonField, button),
                (IconField, iconImage),
                (SelectedMarkField, selected.gameObject));
            return root;
        }

        /// <summary>
        /// The Profile overlay: the chosen avatar, large; the name field with
        /// the edit hint and the refusal line under it; the scrolling grid of
        /// avatars, five per row, whose cells are created at runtime; and the
        /// four statistics in two rows.
        /// </summary>
        private static GameObject BuildProfileOverlay(Scene scene, Sources sources, GameObject avatarButtonAsset)
        {
            var config = sources.Config;
            var frame = BuildOverlayFrame(
                scene, sources, ProfileOverlayName, config.ProfileTitle, ProfilePanelWidth, ProfilePanelHeight, true);

            var avatar = UiBuilder.CreateRect(AvatarName, frame.Panel);
            UiBuilder.Place(
                avatar, new Vector2(AnchorMid, AnchorHigh), new Vector2(0f, ProfileAvatarPosY), new Vector2(ProfileAvatarSize, ProfileAvatarSize));
            var avatarImage = UiBuilder.AddIcon(avatar, sources.AvatarCatalog.SpriteAt(0), ParseColor(IconColorHex));

            var nameField = AddNameField(frame.Panel, sources);

            var hint = UiBuilder.CreateRect(HintName, frame.Panel);
            TopStretch(hint, OverlayTextInset, NameHintPosY, NameLineHeight);
            var hintLabel = AddText(hint, sources, config.NameEditHint, NameLineFontSize, NameHintColorHex);

            var refusal = UiBuilder.CreateRect(RefusalName, frame.Panel);
            TopStretch(refusal, OverlayTextInset, NameRefusalPosY, NameLineHeight);
            var refusalLabel = AddWrappingText(refusal, sources, string.Empty, NameLineFontSize, NameRefusalColorHex);

            var scroll = UiBuilder.CreateRect(ScrollViewName, frame.Panel);
            UiBuilder.Stretch(scroll);
            scroll.offsetMin = new Vector2(ScrollInset, ProfileGridBottom);
            scroll.offsetMax = new Vector2(-ScrollInset, -ProfileGridTop);
            var viewport = AddViewport(scroll);
            var content = UiBuilder.CreateRect(ContentName, viewport);
            TopStretch(content, 0f, 0f, 0f);
            var avatarButton = Single<AvatarButton>(avatarButtonAsset, AvatarButtonName);
            AddCenteredGrid(
                content, ((RectTransform)avatarButton.transform).sizeDelta, ProfileGridColumns,
                ProfilePanelWidth - (ScrollInset + ScrollInset), AvatarButtonName);
            AddVerticalScroll(scroll, viewport, content);

            var statistics = UiBuilder.CreateRect(StatisticsName, frame.Panel);
            BottomStretch(statistics, StatisticsInset, StatisticsPosY, StatisticsHeight);
            var levelsCompleted = AddStatistic(
                statistics, sources, LevelsCompletedName, new Vector2(AnchorLow, AnchorMid), new Vector2(AnchorMid, AnchorHigh),
                config.LevelsCompletedLabel, ProfileTexts.Statistic(LevelsCompletedPlaceholder, config));
            var campaignLevel = AddStatistic(
                statistics, sources, CampaignLevelName, new Vector2(AnchorMid, AnchorMid), new Vector2(AnchorHigh, AnchorHigh),
                config.CampaignLevelLabel, ProfileTexts.CampaignLevel(CampaignLevelPlaceholder, config));
            var bestStreak = AddStatistic(
                statistics, sources, BestStreakName, new Vector2(AnchorLow, AnchorLow), new Vector2(AnchorMid, AnchorMid),
                config.BestStreakLabel, ProfileTexts.Statistic(BestStreakPlaceholder, config));
            var lifetimeGold = AddStatistic(
                statistics, sources, LifetimeGoldName, new Vector2(AnchorMid, AnchorLow), new Vector2(AnchorHigh, AnchorMid),
                config.LifetimeGoldLabel, ProfileTexts.Statistic(LifetimeGoldPlaceholder, config));

            var overlay = frame.Root.AddComponent<ProfileOverlay>();
            SetReferences(
                overlay,
                (CloseButtonField, frame.Close),
                (TitleLabelField, frame.Title),
                (AvatarImageField, avatarImage),
                (NameFieldField, nameField),
                (HintLabelField, hintLabel),
                (RefusalLabelField, refusalLabel),
                (GridField, content),
                (AvatarButtonPrefabField, avatarButton),
                (LevelsCompletedLabelField, levelsCompleted.Label),
                (LevelsCompletedValueField, levelsCompleted.Value),
                (CampaignLevelLabelField, campaignLevel.Label),
                (CampaignLevelValueField, campaignLevel.Value),
                (BestStreakLabelField, bestStreak.Label),
                (BestStreakValueField, bestStreak.Value),
                (LifetimeGoldLabelField, lifetimeGold.Label),
                (LifetimeGoldValueField, lifetimeGold.Value));
            return frame.Root;
        }

        /// <summary>
        /// Profile's name field: the input sprite as its box, a masked text
        /// area holding the one-line text, and TextMeshPro's input field over
        /// them — one line, no character limit, so a name too long can be
        /// typed and refused, the text selected on focus and the old name
        /// back on Escape. Its navigation is off, as every button's is.
        /// </summary>
        private static TMP_InputField AddNameField(RectTransform panel, Sources sources)
        {
            var field = UiBuilder.CreateRect(NameFieldName, panel);
            UiBuilder.Place(
                field, new Vector2(AnchorMid, AnchorHigh), new Vector2(0f, NameFieldPosY), new Vector2(NameFieldWidth, NameFieldHeight));
            var box = AddSliced(field, sources.Bar, PanelColorHex);
            box.raycastTarget = true;

            var textArea = UiBuilder.CreateRect(TextAreaName, field);
            UiBuilder.Stretch(textArea);
            textArea.offsetMin = new Vector2(NameFieldTextInset, 0f);
            textArea.offsetMax = new Vector2(-NameFieldTextInset, 0f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var textRect = UiBuilder.CreateRect(TextName, textArea);
            UiBuilder.Stretch(textRect);
            var text = AddText(textRect, sources, NamePlaceholder, NameFontSize, OverlayTextColorHex);

            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = box;
            input.textViewport = textArea;
            input.textComponent = text;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.characterLimit = 0;
            input.onFocusSelectAll = true;
            input.restoreOriginalTextOnEscape = true;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            input.text = NamePlaceholder;
            return input;
        }

        /// <summary>One of Profile's statistics between <paramref name="anchorMin"/> and <paramref name="anchorMax"/>: its label over its value.</summary>
        private static StatisticParts AddStatistic(
            RectTransform parent, Sources sources, string name, Vector2 anchorMin, Vector2 anchorMax, string labelText, string valueText)
        {
            var cell = UiBuilder.CreateRect(name, parent);
            UiBuilder.SetAnchors(cell, anchorMin, anchorMax);

            var labelRect = UiBuilder.CreateRect(StatisticLabelName, cell);
            UiBuilder.SetAnchors(labelRect, new Vector2(AnchorLow, AnchorMid), new Vector2(AnchorHigh, AnchorHigh));
            var label = AddText(labelRect, sources, labelText, StatisticLabelFontSize, OverlayTextColorHex);

            var valueRect = UiBuilder.CreateRect(StatisticValueName, cell);
            UiBuilder.SetAnchors(valueRect, new Vector2(AnchorLow, AnchorLow), new Vector2(AnchorHigh, AnchorMid));
            var value = AddText(valueRect, sources, valueText, StatisticValueFontSize, OverlayTextColorHex);
            return new StatisticParts(label, value);
        }

        /// <summary>The reward table of D61, for the placeholders the streak's prefabs show in the Editor.</summary>
        private static StreakReward[] PlaceholderStreakRewards() => new[]
        {
            new StreakReward(PlaceholderClockWins, JokerKind.Clock, PlaceholderRewardCount),
            new StreakReward(PlaceholderRocketWins, JokerKind.Rocket, PlaceholderRewardCount),
            new StreakReward(PlaceholderBroomWins, JokerKind.Broom, PlaceholderRewardCount)
        };

        /// <summary>
        /// The front end, set as <see cref="FrontEnd.Show"/> leaves it: Home
        /// shown, the other pages and every overlay hidden. Its root is saved
        /// inactive, so only <see cref="FrontEnd.Show"/> opens it.
        /// </summary>
        private static GameObject BuildFrontEnd(
            Scene scene, Sources sources, GameObject storeAsset, GameObject leaderboardAsset, GameObject streakBarAsset,
            OverlayAssets overlayAssets)
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

            // Added to the Home Page instance, which stays linked; its own file
            // is never written.
            var streakArea = home.Find(StreakAreaName) as RectTransform;
            if (streakArea == null)
            {
                throw new InvalidOperationException($"{HomePageName} has no child '{StreakAreaName}' for the streak bar.");
            }

            var streakBar = Nest(streakBarAsset, streakArea);
            UiBuilder.Stretch(streakBar);
            Record(streakBar);

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
            var winStreak = NestStretched(overlayAssets.WinStreak, overlays, false);
            var streakReward = NestStretched(overlayAssets.StreakReward, overlays, false);
            var profile = NestStretched(overlayAssets.Profile, overlays, false);

            var frontEnd = root.AddComponent<FrontEnd>();
            SetReferences(
                frontEnd,
                (ConfigField, sources.Config),
                (StoreField, Single<StoreScreen>(store.gameObject, StorePageName)),
                (HomeField, Single<HomeScreen>(home.gameObject, HomePageName)),
                (LeaderboardField, Single<LeaderboardScreen>(leaderboard.gameObject, LeaderboardPageName)),
                (TopBarField, Single<TopBar>(topBar.gameObject, TopBarName)),
                (TabBarField, Single<TabBar>(tabBar.gameObject, TabBarName)),
                (SettingsField, Single<SettingsOverlay>(settings.gameObject, SettingsOverlayName)),
                (LevelsField, Single<LevelsOverlay>(levels.gameObject, LevelsOverlayName)),
                (LivesField, Single<LivesOverlay>(lives.gameObject, LivesOverlayName)),
                (NotEnoughGoldField, Single<NotEnoughGoldOverlay>(notEnoughGold.gameObject, NotEnoughGoldOverlayName)),
                (MessageField, Single<MessageOverlay>(message.gameObject, MessageOverlayName)),
                (TestPurchaseField, Single<TestPurchaseOverlay>(testPurchase.gameObject, TestPurchaseOverlayName)),
                (WinStreakField, Single<WinStreakOverlay>(winStreak.gameObject, WinStreakOverlayName)),
                (StreakRewardField, Single<StreakRewardOverlay>(streakReward.gameObject, StreakRewardOverlayName)),
                (ProfileField, Single<ProfileOverlay>(profile.gameObject, ProfileOverlayName)),
                (StreakBarField, Single<StreakBar>(streakBar.gameObject, StreakBarName)),
                (JokerIconsField, sources.JokerIcons),
                (AvatarCatalogField, sources.AvatarCatalog));

            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// The gold packs over a level (Module 40): the overlay frame with its
        /// close button, a column the Gold Pack Cards are created under at
        /// runtime, one per offer, and the test store line at the bottom.
        /// </summary>
        private static GameObject BuildGoldPacksOverlay(Scene scene, Sources sources, GameObject goldPackCardAsset)
        {
            var config = sources.Config;
            var frame = BuildOverlayFrame(
                scene, sources, GoldPacksOverlayName, config.GoldPacksTitle, GoldPacksPanelWidth, GoldPacksPanelHeight, true);

            var packs = UiBuilder.CreateRect(PacksName, frame.Panel);
            UiBuilder.Stretch(packs);
            packs.offsetMin = new Vector2(PacksInset, PacksBottom);
            packs.offsetMax = new Vector2(-PacksInset, -PacksTop);
            AddColumn(packs);

            var note = UiBuilder.CreateRect(NoteName, frame.Panel);
            BottomStretch(note, PacksInset, GoldPacksNoteBottom, GoldPacksNoteHeight);
            var noteLabel = AddWrappingText(note, sources, config.TestStoreNote, GoldPacksNoteFontSize, OverlayTextColorHex);

            var overlay = frame.Root.AddComponent<GoldPacksOverlay>();
            SetReferences(
                overlay,
                (CloseButtonField, frame.Close),
                (TitleLabelField, frame.Title),
                (NoteLabelField, noteLabel),
                (PacksField, packs),
                (GoldPackCardPrefabField, Single<GoldPackCard>(goldPackCardAsset, GoldPackCardName)));
            return frame.Root;
        }

        /// <summary>
        /// The level's HUD (Module 40): a row, fitted to the top band, of Menu
        /// and Restart at the left — Button Round instances whose own icon
        /// shows home and return on the instance only — the timer pill with
        /// the hourglass in the centre, the hidden time bonus beside it, the
        /// level label at the right, and under them the gold and lives pills.
        /// Texts the game sets are written with the config's values and
        /// placeholders.
        /// </summary>
        private static GameObject BuildLevelHud(Scene scene, Sources sources)
        {
            var config = sources.Config;
            var root = CreateRoot(LevelHudName, scene, sources.Layer);
            var row = AddBandRow(root.transform, new Vector2(HudRowWidth, HudRowHeight));

            var menu = AddHudButton(row, sources, MenuName, sources.Home, HudSideInset);
            var restart = AddHudButton(row, sources, RestartName, sources.Return, HudSideInset + menu.sizeDelta.x + HudButtonGap);

            var timer = UiBuilder.CreateRect(TimerName, row);
            PlaceBelowTop(timer, AnchorMid, 0f, HudTopInset, new Vector2(TimerPillWidth, TimerPillHeight));
            AddSliced(timer, sources.Panel, PillColorHex);
            var hourglass = UiBuilder.CreateRect(HourglassName, timer);
            UiBuilder.Place(
                hourglass, new Vector2(AnchorLow, AnchorMid), new Vector2(HourglassInset, 0f), new Vector2(HourglassSize, HourglassSize));
            var hourglassImage = UiBuilder.AddIcon(hourglass, sources.Hourglass, ParseColor(IconColorHex));
            AddTint(hourglass.gameObject, hourglassImage, UiColorRole.Icon);
            var digits = UiBuilder.CreateRect(DigitsName, timer);
            HorizontalBand(digits, TimerDigitsLeft, TimerDigitsRight);
            var timerLabel = AddText(digits, sources, LevelHudTexts.Timer(TimerPlaceholderSeconds, config), TimerFontSize, HudTextColorHex);
            timerLabel.color = config.TimerColor;

            // Its left end beside the timer's right end, centred on the timer.
            var bonus = UiBuilder.CreateRect(TimeBonusName, row);
            bonus.anchorMin = new Vector2(AnchorMid, AnchorHigh);
            bonus.anchorMax = new Vector2(AnchorMid, AnchorHigh);
            bonus.pivot = new Vector2(AnchorLow, AnchorMid);
            bonus.anchoredPosition = new Vector2(TimerPillWidth / 2f + TimeBonusGap, -(HudTopInset + TimerPillHeight / 2f));
            bonus.sizeDelta = new Vector2(TimeBonusWidth, TimerPillHeight);
            var bonusLabel = AddText(bonus, sources, LevelHudTexts.TimeBonus(TimeBonusPlaceholder, config), TimeBonusFontSize, HudTextColorHex);
            bonusLabel.color = config.TimeBonusColor;
            bonusLabel.alignment = TextAlignmentOptions.MidlineLeft;
            bonus.gameObject.SetActive(false);

            var level = UiBuilder.CreateRect(LevelLabelName, row);
            UiBuilder.Place(
                level, new Vector2(AnchorHigh, AnchorHigh), new Vector2(-HudSideInset, -HudTopInset), new Vector2(LevelLabelWidth, TimerPillHeight));
            var levelLabel = AddText(
                level, sources, LevelHudTexts.LevelLabel(false, LevelNumberPlaceholder, config), LevelLabelFontSize, HudTextColorHex);
            levelLabel.alignment = TextAlignmentOptions.MidlineRight;

            var pillOffset = (HudPillWidth + HudPillGap) / 2f;
            var pillSize = new Vector2(HudPillWidth, HudPillHeight);
            var goldPill = UiBuilder.CreateRect(GoldPillName, row);
            PlaceBelowTop(goldPill, AnchorMid, -pillOffset, HudPillRowTop, pillSize);
            AddSliced(goldPill, sources.Panel, PillColorHex);
            var coin = UiBuilder.CreateRect(CoinName, goldPill);
            UiBuilder.Place(coin, new Vector2(AnchorLow, AnchorMid), new Vector2(PillIconInset, 0f), new Vector2(PillCoinSize, PillCoinSize));
            var coinImage = UiBuilder.AddIcon(coin, sources.Coin, ParseColor(IconColorHex));
            AddTint(coin.gameObject, coinImage, UiColorRole.Coin);
            var gold = UiBuilder.CreateRect(GoldLabelName, goldPill);
            HorizontalBand(gold, PillLabelLeft, PillIconInset);
            var goldLabel = AddText(gold, sources, HomeTexts.Gold(GoldPlaceholder, config), PillFontSize, HudTextColorHex);
            goldLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var livesPill = UiBuilder.CreateRect(LivesPillName, row);
            PlaceBelowTop(livesPill, AnchorMid, pillOffset, HudPillRowTop, pillSize);
            AddSliced(livesPill, sources.Panel, PillColorHex);
            var heart = UiBuilder.CreateRect(HeartName, livesPill);
            UiBuilder.Place(
                heart, new Vector2(AnchorLow, AnchorMid), new Vector2(PillHeartInset, 0f), new Vector2(PillHeartSize, PillHeartSize));
            var heartImage = UiBuilder.AddIcon(heart, sources.Heart, ParseColor(IconColorHex));
            AddTint(heart.gameObject, heartImage, UiColorRole.Heart);
            var count = UiBuilder.CreateRect(CountName, heart);
            UiBuilder.Stretch(count);
            var countLabel = AddText(
                count, sources, HomeTexts.LivesCount(LivesCountPlaceholder, config), PillLivesCountFontSize, LivesLabelColorHex);
            var status = UiBuilder.CreateRect(StatusName, livesPill);
            HorizontalBand(status, PillStatusLeft, PillIconInset);
            var statusLabel = AddText(status, sources, config.LivesFullLabel, PillStatusFontSize, HudTextColorHex);
            statusLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var hud = root.AddComponent<LevelHud>();
            SetReferences(
                hud,
                (MenuButtonField, Single<Button>(menu.gameObject, ButtonRoundName)),
                (RestartButtonField, Single<Button>(restart.gameObject, ButtonRoundName)),
                (TimerField, timer),
                (TimerLabelField, timerLabel),
                (TimeBonusField, bonus),
                (TimeBonusLabelField, bonusLabel),
                (LevelLabelField, levelLabel),
                (GoldLabelField, goldLabel),
                (LivesPillField, livesPill.gameObject),
                (LivesCountLabelField, countLabel),
                (LivesStatusLabelField, statusLabel));
            return root;
        }

        /// <summary>
        /// A Button Round at the HUD row's top left, <paramref name="left"/>
        /// in, named <paramref name="name"/>, its own icon showing
        /// <paramref name="icon"/> — an override of this instance only, so
        /// Button Round's file stays unwritten.
        /// </summary>
        private static RectTransform AddHudButton(RectTransform row, Sources sources, string name, Sprite icon, float left)
        {
            var button = Nest(sources.ButtonRound, row);
            button.name = name;
            UiBuilder.Place(button, new Vector2(AnchorLow, AnchorHigh), new Vector2(left, -HudTopInset), button.sizeDelta);
            Record(button);
            Record(button.gameObject);

            var ownIcon = button.Find(ButtonRoundIconName);
            var image = ownIcon == null ? null : ownIcon.GetComponent<Image>();
            if (image == null)
            {
                throw new InvalidOperationException($"{ButtonRoundName} has no child '{ButtonRoundIconName}' with an Image for the {name} icon.");
            }

            image.sprite = icon;
            Record(image);
            return button;
        }

        /// <summary>
        /// The level's joker bar (Module 40): a row, fitted to the bottom band,
        /// of the Clock, the Rocket and the Broom, left to right.
        /// </summary>
        private static GameObject BuildJokerBar(Scene scene, Sources sources)
        {
            var root = CreateRoot(JokerBarName, scene, sources.Layer);
            var row = AddBandRow(root.transform, new Vector2(JokerRowWidth, JokerRowHeight));

            var pitch = JokerSlotWidth + JokerSlotGap;
            var clock = BuildJokerSlot(row, sources, JokerKind.Clock, -pitch);
            var rocket = BuildJokerSlot(row, sources, JokerKind.Rocket, 0f);
            var broom = BuildJokerSlot(row, sources, JokerKind.Broom, pitch);

            var bar = root.AddComponent<JokerBarView>();
            SetReferences(bar, (ClockField, clock), (RocketField, rocket), (BroomField, broom));
            return root;
        }

        /// <summary>
        /// One joker's slot, <paramref name="x"/> from the row's centre and a
        /// little low: the lift, raised while selected, holding the ring
        /// (hidden) behind a Button Primary instance whose own label is
        /// switched off on this instance; on the button, the joker's icon from
        /// the Joker Icons asset and the badge with the coin (hidden) and the
        /// count.
        /// </summary>
        private static JokerBarButton BuildJokerSlot(RectTransform row, Sources sources, JokerKind kind, float x)
        {
            var config = sources.Config;
            var slot = UiBuilder.CreateRect(kind.ToString(), row);
            UiBuilder.PlaceCentered(slot, new Vector2(JokerSlotWidth, JokerSlotHeight));
            slot.anchoredPosition = new Vector2(x, -JokerSlotDrop);

            var lift = UiBuilder.CreateRect(LiftName, slot);
            UiBuilder.Stretch(lift);

            var ring = UiBuilder.CreateRect(RingName, lift);
            UiBuilder.Stretch(ring);
            ring.offsetMin = new Vector2(-JokerRingOutset, -JokerRingOutset);
            ring.offsetMax = new Vector2(JokerRingOutset, JokerRingOutset);
            var ringImage = AddSliced(ring, sources.Panel, PanelColorHex);
            AddTint(ring.gameObject, ringImage, UiColorRole.JokerSelected);
            ring.gameObject.SetActive(false);

            var button = Nest(sources.ButtonPrimary, lift);
            button.name = ButtonName;
            UiBuilder.Stretch(button);
            Record(button);
            Record(button.gameObject);
            var buttonComponent = Single<Button>(button.gameObject, ButtonPrimaryName);
            var ownLabel = Single<TMP_Text>(button.gameObject, ButtonPrimaryName);
            ownLabel.gameObject.SetActive(false);
            Record(ownLabel.gameObject);

            var icon = UiBuilder.CreateRect(IconName, button);
            UiBuilder.Place(icon, new Vector2(AnchorLow, AnchorMid), new Vector2(JokerIconInset, 0f), new Vector2(JokerIconSize, JokerIconSize));
            var iconImage = UiBuilder.AddIcon(icon, sources.JokerIcons.IconOf(kind), config.IconColor);

            var badge = UiBuilder.CreateRect(BadgeName, button);
            UiBuilder.Place(badge, new Vector2(AnchorHigh, AnchorMid), new Vector2(-BadgeInset, 0f), new Vector2(BadgeWidth, BadgeHeight));
            AddSliced(badge, sources.Panel, PillColorHex);
            var layout = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(BadgePadding, BadgePadding, 0, 0);
            layout.spacing = BadgeSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var coin = UiBuilder.CreateRect(CoinName, badge);
            var coinElement = coin.gameObject.AddComponent<LayoutElement>();
            coinElement.preferredWidth = BadgeCoinSize;
            coinElement.preferredHeight = BadgeCoinSize;
            var coinImage = UiBuilder.AddIcon(coin, sources.Coin, ParseColor(IconColorHex));
            AddTint(coin.gameObject, coinImage, UiColorRole.Coin);
            coin.gameObject.SetActive(false);

            var label = UiBuilder.CreateRect(BadgeLabelName, badge);
            var placeholder = new JokerButton(JokerBadge.Count, CountPlaceholder, true, 0);
            var badgeLabel = AddText(label, sources, LevelHudTexts.JokerBadge(placeholder, config), BadgeFontSize, HudTextColorHex);
            badgeLabel.color = config.JokerBadgeTextColor;

            var view = slot.gameObject.AddComponent<JokerBarButton>();
            SetReferences(
                view,
                (ButtonField, buttonComponent),
                (LiftField, lift),
                (RingField, ring.gameObject),
                (IconField, iconImage),
                (CoinField, coin.gameObject),
                (BadgeLabelField, badgeLabel));
            SetFloat(view, SelectedRaiseField, JokerRaise);
            return view;
        }

        /// <summary>
        /// The level's interface (Module 40), set as <see cref="LevelUi.Show"/>
        /// leaves it: the Level HUD along the top and the Joker Bar along the
        /// bottom — <see cref="LevelUi.PlaceBands"/> anchors both to their
        /// bands at runtime — and every overlay hidden under Overlays, a
        /// direct child of the root so the scrims cover everything. Its root
        /// is saved inactive, so only <see cref="LevelUi.Show"/> opens it.
        /// </summary>
        private static GameObject BuildLevelUi(
            Scene scene, Sources sources, GameObject hudAsset, GameObject jokerBarAsset, LevelOverlayAssets overlayAssets)
        {
            var root = CreateRoot(LevelUiName, scene, sources.Layer);

            var hud = Nest(hudAsset, root.transform);
            TopStretch(hud, 0f, 0f, HudRowHeight);
            Record(hud);

            var bar = Nest(jokerBarAsset, root.transform);
            BottomStretch(bar, 0f, 0f, JokerRowHeight);
            Record(bar);

            var overlays = UiBuilder.CreateRect(OverlaysName, root.transform);
            UiBuilder.Stretch(overlays);
            var notEnoughGold = NestStretched(overlayAssets.NotEnoughGold, overlays, false);
            var goldPacks = NestStretched(overlayAssets.GoldPacks, overlays, false);
            var testPurchase = NestStretched(overlayAssets.TestPurchase, overlays, false);
            var message = NestStretched(overlayAssets.Message, overlays, false);

            var levelUi = root.AddComponent<LevelUi>();
            SetReferences(
                levelUi,
                (ConfigField, sources.Config),
                (HudField, Single<LevelHud>(hud.gameObject, LevelHudName)),
                (JokerBarField, Single<JokerBarView>(bar.gameObject, JokerBarName)),
                (NotEnoughGoldField, Single<NotEnoughGoldOverlay>(notEnoughGold.gameObject, NotEnoughGoldOverlayName)),
                (GoldPacksField, Single<GoldPacksOverlay>(goldPacks.gameObject, GoldPacksOverlayName)),
                (TestPurchaseField, Single<TestPurchaseOverlay>(testPurchase.gameObject, TestPurchaseOverlayName)),
                (MessageField, Single<MessageOverlay>(message.gameObject, MessageOverlayName)));

            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// The row a band holds, centred at <paramref name="designSize"/>, with
        /// a <see cref="BandFit"/> that scales it down to the band.
        /// </summary>
        private static RectTransform AddBandRow(Transform band, Vector2 designSize)
        {
            var row = UiBuilder.CreateRect(RowName, band);
            UiBuilder.PlaceCentered(row, designSize);
            var fit = row.gameObject.AddComponent<BandFit>();
            using (var serialized = new SerializedObject(fit))
            {
                Property(serialized, DesignSizeField).vector2Value = designSize;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return row;
        }

        /// <summary>
        /// Gives <paramref name="rect"/> a fixed <paramref name="size"/>,
        /// <paramref name="x"/> across from its parent's top edge at
        /// <paramref name="anchorX"/>, its top <paramref name="top"/> below
        /// that edge. Its pivot is its centre, so it scales about its middle.
        /// </summary>
        private static void PlaceBelowTop(RectTransform rect, float anchorX, float x, float top, Vector2 size)
        {
            rect.anchorMin = new Vector2(anchorX, AnchorHigh);
            rect.anchorMax = new Vector2(anchorX, AnchorHigh);
            rect.pivot = new Vector2(AnchorMid, AnchorMid);
            rect.anchoredPosition = new Vector2(x, -(top + size.y / 2f));
            rect.sizeDelta = size;
        }

        /// <summary>Assigns the serialized float <paramref name="field"/> by name; a missing field throws.</summary>
        private static void SetFloat(Object component, string field, float value)
        {
            using (var serialized = new SerializedObject(component))
            {
                Property(serialized, field).floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
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

        /// <summary>
        /// A scroll view's viewport, filling <paramref name="scroll"/>:
        /// RectMask2D clips, and the clear image catches drags that start
        /// between the items, so the whole viewport scrolls.
        /// </summary>
        private static RectTransform AddViewport(RectTransform scroll)
        {
            var viewport = UiBuilder.CreateRect(ViewportName, scroll);
            UiBuilder.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = ParseColor(ViewportColorHex);
            viewportImage.raycastTarget = true;
            return viewport;
        }

        /// <summary>A vertical Scroll Rect on <paramref name="scroll"/> at the builder's scroll sensitivity.</summary>
        private static ScrollRect AddVerticalScroll(RectTransform scroll, RectTransform viewport, RectTransform content)
        {
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = ScrollSensitivity;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return scrollRect;
        }

        /// <summary>A 9-sliced <paramref name="sprite"/> on <paramref name="rect"/> in <paramref name="colorHex"/>; not a raycast target.</summary>
        private static Image AddSliced(RectTransform rect, Sprite sprite, string colorHex)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = ParseColor(colorHex);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Spans <paramref name="rect"/> over its parent's full height, from <paramref name="left"/> to <paramref name="right"/> in from its sides.</summary>
        private static void HorizontalBand(RectTransform rect, float left, float right)
        {
            UiBuilder.Stretch(rect);
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(-right, 0f);
        }

        /// <summary>Anchors <paramref name="rect"/> across its parent's bottom edge, <paramref name="inset"/> in from both sides.</summary>
        private static void BottomStretch(RectTransform rect, float inset, float posY, float height)
        {
            rect.anchorMin = new Vector2(AnchorLow, AnchorLow);
            rect.anchorMax = new Vector2(AnchorHigh, AnchorLow);
            rect.pivot = new Vector2(AnchorMid, AnchorLow);
            rect.anchoredPosition = new Vector2(0f, posY);
            rect.sizeDelta = new Vector2(-(inset + inset), height);
        }

        /// <summary>
        /// A one-line label, left-aligned, in a band over the full height of
        /// <paramref name="parent"/>, from <paramref name="left"/> to
        /// <paramref name="right"/> in from its sides.
        /// </summary>
        private static TextMeshProUGUI AddBandText(
            RectTransform parent, Sources sources, string name, float left, float right, string text, float fontSize)
        {
            var rect = UiBuilder.CreateRect(name, parent);
            HorizontalBand(rect, left, right);
            var label = AddText(rect, sources, text, fontSize, OverlayTextColorHex);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        /// <summary>
        /// A grid of <paramref name="columns"/> cells of <paramref name="cell"/>
        /// size on <paramref name="content"/>, with one gap between the columns
        /// and at both sides, all equal, so the grid is centred in a viewport
        /// <paramref name="viewportWidth"/> wide with the cells at their own
        /// size; the content grows with its rows.
        /// </summary>
        private static void AddCenteredGrid(RectTransform content, Vector2 cell, int columns, float viewportWidth, string cellName)
        {
            var gap = (viewportWidth - columns * cell.x) / (columns + 1);
            if (gap < 0f)
            {
                throw new InvalidOperationException(
                    $"{columns} {cellName}s of width {cell.x} do not fit the grid's {viewportWidth} units.");
            }

            var padding = Mathf.FloorToInt(gap);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = cell;
            grid.spacing = new Vector2(gap, gap);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(padding, padding, padding, padding);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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

        /// <summary>A sprite drawn 9-sliced: it must import as one sprite, with a border.</summary>
        private static Sprite LoadSlicedSprite(string path)
        {
            var sprite = LoadSprite(path);
            if (sprite.border == Vector4.zero)
            {
                throw new InvalidOperationException(
                    $"{path} is drawn 9-sliced, so it needs a border (Sprite Editor: Border); it has none. Nothing was written.");
            }

            return sprite;
        }

        private static JokerIcons LoadJokerIcons()
        {
            var icons = AssetDatabase.LoadAssetAtPath<JokerIcons>(JokerIconsPath);
            if (icons == null)
            {
                throw new InvalidOperationException(
                    $"{JokerIconsPath} is missing (Create > Gate Rush > Joker Icons). Nothing was written.");
            }

            var problems = icons.Problems();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException($"{JokerIconsPath} has problems: {string.Join(" ", problems)} Nothing was written.");
            }

            return icons;
        }

        /// <summary>
        /// The Avatar Catalog, which must report no problem and whose every
        /// sprite must import as exactly one sprite, as every sprite the build
        /// reads must.
        /// </summary>
        private static AvatarCatalog LoadAvatarCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(AvatarCatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"{AvatarCatalogPath} is missing (Gate Rush > Fill Avatar Catalog). Nothing was written.");
            }

            var problems = catalog.Problems();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException($"{AvatarCatalogPath} has problems: {string.Join(" ", problems)} Nothing was written.");
            }

            for (var i = 0; i < catalog.Count; i++)
            {
                LoadSprite(AssetDatabase.GetAssetPath(catalog.SpriteAt(i)));
            }

            return catalog;
        }

        /// <summary>Home Page, which must hold the Streak Area the streak bar is placed under.</summary>
        private static GameObject LoadHomePage()
        {
            var homePage = LoadHandMade(HomePageName);
            if (homePage.transform.Find(StreakAreaName) == null)
            {
                throw new InvalidOperationException(
                    $"{HomePageName} has no child '{StreakAreaName}' for the streak bar. Nothing was written.");
            }

            return homePage;
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

        /// <summary>A Settings row's toggle, the Button Round instance with its own icon switched off, its button and its label.</summary>
        private readonly struct ToggleRow
        {
            public ToggleRow(RectTransform toggle, RectTransform ownIcon, Button button, TMP_Text label)
            {
                Toggle = toggle;
                OwnIcon = ownIcon;
                Button = button;
                Label = label;
            }

            public RectTransform Toggle { get; }

            /// <summary>Button Round's own icon, switched off; the looks take its place.</summary>
            public RectTransform OwnIcon { get; }

            public Button Button { get; }
            public TMP_Text Label { get; }
        }

        /// <summary>One of Profile's statistics: its label and its value.</summary>
        private readonly struct StatisticParts
        {
            public StatisticParts(TMP_Text label, TMP_Text value)
            {
                Label = label;
                Value = value;
            }

            public TMP_Text Label { get; }
            public TMP_Text Value { get; }
        }

        /// <summary>The saved overlay prefabs Front End nests, in stacking order.</summary>
        private readonly struct OverlayAssets
        {
            public OverlayAssets(
                GameObject settings, GameObject levels, GameObject lives, GameObject notEnoughGold, GameObject message, GameObject testPurchase,
                GameObject winStreak, GameObject streakReward, GameObject profile)
            {
                Profile = profile;
                Settings = settings;
                Levels = levels;
                Lives = lives;
                NotEnoughGold = notEnoughGold;
                Message = message;
                TestPurchase = testPurchase;
                WinStreak = winStreak;
                StreakReward = streakReward;
            }

            public GameObject Settings { get; }
            public GameObject Levels { get; }
            public GameObject Lives { get; }
            public GameObject NotEnoughGold { get; }
            public GameObject Message { get; }
            public GameObject TestPurchase { get; }
            public GameObject WinStreak { get; }
            public GameObject StreakReward { get; }
            public GameObject Profile { get; }
        }

        /// <summary>The saved overlay prefabs Level UI nests, in stacking order.</summary>
        private readonly struct LevelOverlayAssets
        {
            public LevelOverlayAssets(GameObject notEnoughGold, GameObject goldPacks, GameObject testPurchase, GameObject message)
            {
                NotEnoughGold = notEnoughGold;
                GoldPacks = goldPacks;
                TestPurchase = testPurchase;
                Message = message;
            }

            public GameObject NotEnoughGold { get; }
            public GameObject GoldPacks { get; }
            public GameObject TestPurchase { get; }
            public GameObject Message { get; }
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
            public Sprite Fire { get; private set; }
            public Sprite Check { get; private set; }
            public Sprite Bar { get; private set; }
            public Sprite Phone { get; private set; }
            public Sprite Cross { get; private set; }
            public Sprite Medal { get; private set; }
            public Sprite Home { get; private set; }
            public Sprite Return { get; private set; }
            public Sprite Hourglass { get; private set; }
            public JokerIcons JokerIcons { get; private set; }
            public AvatarCatalog AvatarCatalog { get; private set; }
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
                    Fire = LoadSprite(FireSpritePath),
                    Check = LoadSprite(CheckSpritePath),
                    Bar = LoadSlicedSprite(BarSpritePath),
                    Phone = LoadSprite(PhoneSpritePath),
                    Cross = LoadSprite(CrossSpritePath),
                    Medal = LoadSprite(MedalSpritePath),
                    Home = LoadSprite(HomeSpritePath),
                    Return = LoadSprite(ReturnSpritePath),
                    Hourglass = LoadSprite(HourglassSpritePath),
                    JokerIcons = LoadJokerIcons(),
                    AvatarCatalog = LoadAvatarCatalog(),
                    ButtonClose = LoadHandMade(ButtonCloseName),
                    ButtonPrimary = buttonPrimary,
                    ButtonRound = LoadHandMade(ButtonRoundName),
                    ButtonSecondary = LoadHandMade(ButtonSecondaryName),
                    HomePage = LoadHomePage(),
                    TabBar = LoadHandMade(TabBarName),
                    TopBar = LoadHandMade(TopBarName),
                    LevelButton = Single<LevelButton>(LoadHandMade(LevelButtonName), LevelButtonName)
                };
            }
        }
    }
}
