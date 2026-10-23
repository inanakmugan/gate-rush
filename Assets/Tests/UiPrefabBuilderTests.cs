using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GateRush.Meta;
using GateRush.Runtime;
using GateRush.UI;
using GateRush.UI.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="UiPrefabBuilder"/> (Modules 35 to 38): the build
    /// writes the nineteen prefabs and nothing else, the Front End it builds
    /// reports no missing part, every overlay's, the Store's, its cards' and
    /// the Gold Pack Card's fields are wired, the hand-made parts stay linked
    /// to their prefabs and their files unwritten, every tinted graphic
    /// carries its UI Config colour, and the front end starts as
    /// <see cref="FrontEnd.Show"/> leaves it. The streak's parts are fully
    /// wired, and the streak bar sits under the Home Page instance's Streak
    /// Area as an object added to it, Home Page's own file unwritten. Module
    /// 38's Avatar Button and Profile overlay are fully wired, the grid has
    /// five columns of the built Avatar Button, and Top Bar's file stays
    /// unwritten with the other hand-made prefabs. Module 39's Leaderboard
    /// page and Leaderboard Row are fully wired, the page creating its rows
    /// from the built row, with Home Page and Top Bar still unwritten. Module
    /// 40's Level UI reports no missing part under a canvas, its views, the
    /// joker buttons and the in-level overlays are fully wired, the overlays
    /// are the built prefabs and start closed under a root saved inactive,
    /// the HUD's texts and colours come from the UI Config, Menu and Restart
    /// are Button Rounds with the home and return icons, and the bar's icons
    /// are the Joker Icons'; the hand-made prefabs, Home Page and Top Bar
    /// among them, stay unwritten. Module 41's Result and Confirm overlays are
    /// fully wired, nested in Level UI's Overlays and saved closed; their
    /// buttons are the theme's prefabs, Lose a life a red Button Primary and
    /// Menu a Button Round with the home icon, neither has a close button,
    /// and their texts come from the UI Config. Module 42's joker buttons are
    /// square Button Primaries with the square face and grey disabled look,
    /// the icon centred above the lip and the red badge hanging off the
    /// bottom-right corner, widening leftwards; every coin draws the owner's
    /// coin or pile of coins. Module 43: the joker buttons and the Store's
    /// cards have the lock wired, centred on their icons and saved hidden;
    /// the joker buttons' glow is saved hidden behind the ring and the
    /// button; a Store card shown locked hides the count and the buy button,
    /// and shown again restores them. Module 45: the build draws the new
    /// interface art, never tinted and never a replaced piece; there is no
    /// pressed look until Module 46; the joker buttons, badge and gap are
    /// about 1.4 times Module 42's and stay inside the row clear of its foot;
    /// the Front End's painting envelops the screen behind everything; and
    /// the Leaderboard Row holds the three medals. Its follow-up: the HUD's
    /// Menu and Restart are one large size, mirrored; the level label sits on
    /// the ribbon above the timer; the icons hang off their pills' left ends;
    /// the pills are symmetric and the gold pill centres alone; every part
    /// stays inside the row, which the top band holds unscaled; the white
    /// glyphs are 55 to 60 percent of their round buttons; every scrim is the
    /// UI Config's; the result panel shows a loss's hourglass and lost life
    /// and its Menu is close to the other buttons' height; Profile's name
    /// field is the pill with white text and the chosen avatar wears the gold
    /// ring; "Next life in" sits above the wait; and the Win Streak rule sits
    /// right under the shipped ladder.
    /// </summary>
    /// <remarks>
    /// The build reads the project's real hand-made prefabs, sprites and UI
    /// Config, so these tests also fail when one of those is missing or
    /// unusable. It runs once, into a temporary folder deleted afterwards.
    /// </remarks>
    public class UiPrefabBuilderTests
    {
        private const string TestFolder = "Assets/UiPrefabBuilderTest";
        private const string HandMadeFolder = "Assets/Prefabs/UI";
        private const string UiConfigPath = "Assets/Config/UiConfig.asset";
        private const string FrontEndName = "Front End";
        private const string GoldPackCardName = "Gold Pack Card";
        private const string StreakMarkerName = "Streak Marker";
        private const string StreakBarName = "Streak Bar";
        private const string StreakLadderRowName = "Streak Ladder Row";
        private const string StreakPrizeName = "Streak Prize";
        private const string StreakAreaName = "Streak Area";
        private const string AvatarButtonName = "Avatar Button";
        private const string ProfileOverlayName = "Profile Overlay";
        private const string LeaderboardRowName = "Leaderboard Row";
        private const string LeaderboardPageName = "Leaderboard Page";
        private const int ProfileGridColumns = 5;
        private const string TintRoleField = "role";
        private const string TintTargetField = "target";
        private const float ExpectedScrollSensitivity = 20f;
        private const float Tolerance = 1e-4f;

        private const string LevelUiName = "Level UI";
        private const string LevelHudName = "Level HUD";
        private const string JokerBarName = "Joker Bar";
        private const string GoldPacksOverlayName = "Gold Packs Overlay";
        private const string JokerIconsPath = "Assets/Config/JokerIcons.asset";
        private const string HomeSpritePath = "Assets/ThirdParty/Kenney/GameIcons/home.png";
        private const string ReturnSpritePath = "Assets/ThirdParty/Kenney/GameIcons/return.png";

        private const string ResultOverlayName = "Result Overlay";
        private const string ConfirmOverlayName = "Confirm Overlay";
        private const string RedButtonSpritePath = "Assets/Art/Sprites/UI/button_rect_red.png";

        private const string CoinSpritePath = "Assets/Art/Sprites/UI/coin.png";
        private const string CoinsSpritePath = "Assets/Art/Sprites/UI/coins.png";

        private const string JokerButtonSpritePath = "Assets/Art/Sprites/UI/button_square_green.png";
        private const string JokerButtonDisabledSpritePath = "Assets/Art/Sprites/UI/button_square_grey.png";
        private const string LockSpritePath = "Assets/Art/Sprites/UI/lock.png";

        // Module 45: the owner's interface art, drawn in its own colours.
        private const string InterfaceArtFolder = "Assets/Art/Sprites/UI/";
        private const string BadgeSpritePath = "Assets/Art/Sprites/UI/badge.png";
        private const string GoldMedalSpritePath = "Assets/Art/Sprites/UI/medal_gold.png";
        private const string SilverMedalSpritePath = "Assets/Art/Sprites/UI/medal_silver.png";
        private const string BronzeMedalSpritePath = "Assets/Art/Sprites/UI/medal_bronze.png";

        // Everything an image the builder itself draws may show since Module
        // 45: the game's own art — the generated avatars among it since the
        // follow-up — and the white Kenney glyphs kept on the new buttons.
        // Anything else is a replaced piece.
        private const string OwnArtFolder = "Assets/Art/";

        private static string[] KeptKenneyGlyphs => new[]
        {
            "Assets/ThirdParty/Kenney/GameIcons/audioOn.png", "Assets/ThirdParty/Kenney/GameIcons/audioOff.png",
            "Assets/ThirdParty/Kenney/GameIcons/phone.png", "Assets/ThirdParty/Kenney/GameIcons/cross.png",
            "Assets/ThirdParty/Kenney/GameIcons/home.png", "Assets/ThirdParty/Kenney/GameIcons/return.png",
            "Assets/ThirdParty/Kenney/UIPack/Green/icon_checkmark.png"
        };

        // The Module 45 follow-up.
        private const string RuntimeConfigPath = "Assets/Config/RuntimeConfig.asset";
        private const string MetaConfigPath = "Assets/Config/MetaConfig.asset";
        private const string RibbonSpritePath = "Assets/Art/Sprites/UI/ribbon.png";
        private const string PillSpritePath = "Assets/Art/Sprites/UI/pill.png";
        private const string HourglassSpritePath = "Assets/Art/Sprites/UI/hourglass.png";
        private const string HeartSpritePath = "Assets/Art/Sprites/UI/heart.png";
        private const string RingSelectSpritePath = "Assets/Art/Sprites/UI/ring_select.png";

        // The Level scene's Canvas Scaler reference height, which the HUD's
        // row is laid out at.
        private const float ReferenceHeight = 1920f;

        // A pill icon is about 1.3 times its pill's height.
        private const float PillIconMinShare = 1.25f;
        private const float PillIconMaxShare = 1.35f;

        // A white glyph on a round button is 45 to 50 percent of it, since the
        // Round Glyph And Top Bar Fix. A Kenney glyph's drawing fills about 64
        // percent of its square (measured on home, return, audioOn, audioOff,
        // phone and plus).
        private const float GlyphMinShare = 0.45f;
        private const float GlyphMaxShare = 0.50f;
        private const float KenneyGlyphFill = 0.64f;

        // The Settings looks: each one's shift of Button Round's glyph box,
        // and the horizontal centre of its glyph's opaque bounds as a share
        // of the 100-pixel file (measured), which the shift centres on the
        // button. A file's own centre is half its width.
        private const float FileCentre = 0.5f;
        private const float GlyphCentreTolerance = 0.005f;

        private static (string look, float shift, float drawnCentre)[] SettingsLooks => new[]
        {
            ("Sound On Look", 0.015f, 0.50f),
            ("Sound Off Look", 0.105f, 0.38f),
            ("Haptics On Look", 0.0225f, 0.49f),
            ("Haptics Off Look", 0.0225f, 0.49f)
        };

        // The result panel's Menu is close to the rectangular buttons' height.
        private const float MenuMinHeightShare = 0.9f;

        // The rule sits right under the ladder: at most this far below it.
        private const float RuleMaxGap = 24f;

        // The Lives overlay's heart, about 150 tall, its count scaled as 48 was
        // in a 120 heart, and where every panel's foot button sits.
        private const float LivesHeartHeight = 150f;
        private const float LivesCountFontPerHeartHeight = 48f / 120f;
        private const float PanelButtonPosY = 70f;

        // A scrolling list's soft edge: 40 units at the top and bottom.
        private const int SoftEdgeUnits = 40;

        // The outlined text on the painting: the material the build makes,
        // its shader, the theme's purple outline, the Store's test store line
        // at 32 on one line, and the Leaderboard's subtitle shrinking from 40
        // no lower than 32. TMP reads an atlas's padding as a range of 1, so
        // the outline (with face dilate and the bold weight) and the underlay
        // (offset, dilate and softness) each stay within it.
        private const string OutlinedTextMaterialPath = "Assets/Art/Fonts/Fredoka-Bold SDF - Outline Underlay.mat";
        private const string DistanceFieldShaderName = "TextMeshPro/Distance Field";
        private const string OutlineColorHex = "#2A1650";
        private const string UnderlayColorHex = "#2A165099";
        private const string UnderlayColorProperty = "_UnderlayColor";

        // A colour read back from a material or a saved prefab may differ from
        // its constant in the last float digits: one step of an 8-bit channel.
        private const float ColourChannelTolerance = 1f / 255f;
        private const string UnderlayKeyword = "UNDERLAY_ON";
        private const string OutlineColorProperty = "_OutlineColor";
        private const string OutlineWidthProperty = "_OutlineWidth";
        private const string FaceDilateProperty = "_FaceDilate";
        private const string WeightBoldProperty = "_WeightBold";
        private const string UnderlayOffsetXProperty = "_UnderlayOffsetX";
        private const string UnderlayOffsetYProperty = "_UnderlayOffsetY";
        private const string UnderlayDilateProperty = "_UnderlayDilate";
        private const string UnderlaySoftnessProperty = "_UnderlaySoftness";
        private const float TestStoreNoteFontSize = 32f;
        private const float LeaderboardNoteMaxFontSize = 40f;
        private const float LeaderboardNoteMinFontSize = 32f;
        private const float PaddingRange = 1f;
        private const float BoldWeightShare = 0.25f;

        // Every overlay's title beside the close button: from 80 down to 56.
        private const float OverlayTitleFontSize = 80f;
        private const float OverlayTitleMinFontSize = 56f;

        // Every overlay with a close button.
        private static string[] ClosedOverlayNames => new[]
        {
            "Settings Overlay", "Levels Overlay", "Lives Overlay", "Not Enough Gold Overlay", "Test Purchase Overlay",
            "Win Streak Overlay", ProfileOverlayName, GoldPacksOverlayName
        };

        // The Store's Refill Lives card, and how far a text-only buy label is
        // raised onto the button's face above its lip.
        private const string LivesCardName = "Lives Card";
        private const float ButtonFaceRaise = 5f;

        // The streak marker: its round base, and the ring over its rim.
        private const string MarkerBaseSpritePath = "Assets/Art/Sprites/UI/button_round_blue.png";

        // Every overlay the build draws over a scrim.
        private static string[] ScrimmedOverlayNames => new[]
        {
            "Settings Overlay", "Levels Overlay", "Lives Overlay", "Not Enough Gold Overlay", "Message Overlay",
            "Test Purchase Overlay", "Win Streak Overlay", "Streak Reward Overlay", ProfileOverlayName, GoldPacksOverlayName,
            ResultOverlayName, ConfirmOverlayName
        };

        // Module 45's joker bar, which the interface review makes about 1.2
        // times larger, with half a button between the buttons; and its
        // lock, about half the button.
        private const float Module45JokerButtonSize = 196f;
        private const float Module45BadgeToButton = 72f / 196f;
        private const float AboutOnePointTwoTimes = 1.18f;
        private const float HalfAButton = 0.5f;
        private const float ShareTolerance = 0.02f;
        private const float LockMinShare = 0.45f;
        private const float LockMaxShare = 0.55f;

        private static string[] OutputNames => new[]
        {
            GoldPackCardName, "Store Page", LeaderboardRowName, "Leaderboard Page", "Settings Overlay", "Levels Overlay", "Lives Overlay",
            "Not Enough Gold Overlay", "Message Overlay", "Test Purchase Overlay", StreakMarkerName, StreakBarName,
            StreakLadderRowName, "Win Streak Overlay", StreakPrizeName, "Streak Reward Overlay", AvatarButtonName,
            ProfileOverlayName, FrontEndName, GoldPacksOverlayName, LevelHudName, JokerBarName, LevelUiName,
            ResultOverlayName, ConfirmOverlayName
        };

        private static (Type type, string prefabName)[] LevelOverlays => new[]
        {
            (typeof(NotEnoughGoldOverlay), "Not Enough Gold Overlay"), (typeof(GoldPacksOverlay), GoldPacksOverlayName),
            (typeof(TestPurchaseOverlay), "Test Purchase Overlay"), (typeof(MessageOverlay), "Message Overlay"),
            (typeof(ResultOverlay), ResultOverlayName), (typeof(ConfirmOverlay), ConfirmOverlayName)
        };

        private static string[] HandMadeNames => new[]
        {
            "Button Close", "Button Primary", "Button Round", "Button Secondary", "Level Button", "Top Bar", "Tab Bar", "Home Page"
        };

        private static Type[] OverlayTypes => new[]
        {
            typeof(SettingsOverlay), typeof(LevelsOverlay), typeof(LivesOverlay),
            typeof(NotEnoughGoldOverlay), typeof(MessageOverlay), typeof(TestPurchaseOverlay),
            typeof(WinStreakOverlay), typeof(StreakRewardOverlay), typeof(ProfileOverlay)
        };

        private string folder;
        private Dictionary<string, byte[]> handMadeBefore;

        [OneTimeSetUp]
        public void BuildOnce()
        {
            handMadeBefore = HandMadeNames.ToDictionary(name => name, name => File.ReadAllBytes(HandMadePath(name)));
            folder = AssetDatabase.GenerateUniqueAssetPath(TestFolder);
            var slash = folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
            UiPrefabBuilder.Build(folder);
        }

        [OneTimeTearDown]
        public void DeleteFolder()
        {
            if (folder != null && AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Build_IntoFolder_WritesExactlyTheTwentyFivePrefabs()
        {
            var names = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
                .ToList();

            CollectionAssert.AreEquivalent(OutputNames, names);
        }

        [Test]
        public void ExistingOutputs_AfterBuild_NamesAllTwentyFive()
        {
            var existing = UiPrefabBuilder.ExistingOutputs(folder);

            CollectionAssert.AreEquivalent(OutputNames.Select(name => $"{folder}/{name}.prefab"), existing);
        }

        [Test]
        public void Build_HandMadePrefabs_StayUnwritten()
        {
            var changed = HandMadeNames
                .Where(name => !File.ReadAllBytes(HandMadePath(name)).SequenceEqual(handMadeBefore[name]))
                .ToList();

            CollectionAssert.IsEmpty(changed, "hand-made prefabs whose files the build changed");
        }

        [Test]
        public void Build_FrontEndUnderCanvas_MissingSetupReportsNothing()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                // FrontEnd requires a Canvas above it, which the prefab leaves
                // to the scene.
                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvas, scene);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadBuilt(FrontEndName), canvas.transform);

                var problem = instance.GetComponent<FrontEnd>().MissingSetup();

                Assert.IsNull(problem, problem);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase(typeof(SettingsOverlay), 9)]
        [TestCase(typeof(LevelsOverlay), 3)]
        [TestCase(typeof(LivesOverlay), 6)]
        [TestCase(typeof(NotEnoughGoldOverlay), 3)]
        [TestCase(typeof(MessageOverlay), 3)]
        [TestCase(typeof(TestPurchaseOverlay), 6)]
        [TestCase(typeof(StoreScreen), 11)]
        [TestCase(typeof(WinStreakOverlay), 6)]
        [TestCase(typeof(StreakRewardOverlay), 4)]
        [TestCase(typeof(StreakBar), 6)]
        [TestCase(typeof(ProfileOverlay), 16)]
        [TestCase(typeof(LeaderboardScreen), 7)]
        [TestCase(typeof(FrontEnd), 20)]
        public void Build_View_EverySerializedReferenceIsAssigned(Type viewType, int expectedFields)
        {
            var view = LoadBuilt(FrontEndName).GetComponentInChildren(viewType, true);
            Assert.IsNotNull(view, viewType.Name);

            AssertEveryReferenceAssigned(view, expectedFields);
        }

        [Test]
        public void Build_StoreItemCards_EveryReferenceIsAssigned()
        {
            var cards = LoadBuilt(FrontEndName).GetComponentsInChildren<StoreItemCard>(true);

            Assert.AreEqual(4, cards.Length, "the Clock, Rocket, Broom and Lives cards");
            foreach (var card in cards)
            {
                AssertEveryReferenceAssigned(card, 6);
            }
        }

        [Test]
        public void Build_StoreItemCards_LockIsTheLockedSpriteOnTheIconSavedHidden()
        {
            var cards = LoadBuilt(FrontEndName).GetComponentsInChildren<StoreItemCard>(true);
            Assert.AreEqual(4, cards.Length, "the Clock, Rocket, Broom and Lives cards");

            foreach (var card in cards)
            {
                using (var serialized = new SerializedObject(card))
                {
                    AssertLockOnIcon(serialized, card.name);
                }
            }
        }

        [Test]
        public void StoreItemCard_ShowLockedThenShow_HidesThenRestoresTheCountAndTheBuyButton()
        {
            var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiConfigPath);
            Assert.IsNotNull(config, UiConfigPath);
            var contents = PrefabUtility.LoadPrefabContents($"{folder}/{FrontEndName}.prefab");
            try
            {
                var card = contents.GetComponentsInChildren<StoreItemCard>(true)[0];
                var count = StoreTexts.JokerCount(3, config);
                var unlocksAt = StoreTexts.UnlocksAt(10, config);
                var price = StoreTexts.GoldPrice(100, config);

                card.ShowLocked(unlocksAt, config);
                var locked = LookOf(card);
                card.Show(count, price, true, true, config);
                var unlocked = LookOf(card);

                Assert.AreEqual(unlocksAt, locked.subText, "a locked card never shows the count held");
                Assert.IsFalse(locked.isBuyShown, "a locked card has no buy button, so no price and no press");
                Assert.IsTrue(locked.isLockShown);
                Assert.AreEqual(config.LockedIconAlpha, locked.iconAlpha, "the icon is dimmed");
                Assert.AreEqual(count, unlocked.subText, "the count comes back once unlocked");
                Assert.IsTrue(unlocked.isBuyShown);
                Assert.IsFalse(unlocked.isLockShown);
                Assert.AreEqual(1f, unlocked.iconAlpha, "the icon is opaque again");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void StoreItemCard_ShowWithoutCoin_CentresTheLabelOnTheFaceAndShowWithCoinRestoresIt()
        {
            var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiConfigPath);
            Assert.IsNotNull(config, UiConfigPath);
            var contents = PrefabUtility.LoadPrefabContents($"{folder}/{FrontEndName}.prefab");
            try
            {
                var card = contents.GetComponentsInChildren<StoreItemCard>(true).Single(item => item.name == LivesCardName);
                TMP_Text label;
                using (var serialized = new SerializedObject(card))
                {
                    label = Label(serialized, "priceLabel");
                }

                var rect = label.rectTransform;
                var builtMin = rect.offsetMin;
                var builtMax = rect.offsetMax;
                var builtAlignment = label.verticalAlignment;

                card.Show(config.LivesFullLabel, config.LivesFullLabel, false, false, config);
                var fullMin = rect.offsetMin;
                var fullMax = rect.offsetMax;
                var fullAlignment = label.verticalAlignment;
                card.Show(config.LivesFullLabel, config.LivesFullLabel, true, true, config);

                Assert.AreEqual(Vector2.zero, rect.anchorMin, "the label spans the button");
                Assert.AreEqual(Vector2.one, rect.anchorMax, "the label spans the button");
                Assert.AreEqual(fullMin.x, -fullMax.x, Tolerance, "full: symmetric about the button's centre, whichever way it was off");
                Assert.AreEqual(ButtonFaceRaise, (fullMin.y + fullMax.y) / 2f, Tolerance, "full: raised onto the face above the lip");
                Assert.AreEqual(VerticalAlignmentOptions.Geometry, fullAlignment, "full: the drawn letters are centred");
                Assert.AreEqual(builtMin, rect.offsetMin, "with the coin: as built");
                Assert.AreEqual(builtMax, rect.offsetMax, "with the coin: as built");
                Assert.AreEqual(builtAlignment, label.verticalAlignment, "with the coin: as built");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void Build_GoldPackCard_EveryReferenceIsAssigned()
        {
            var card = LoadBuilt(GoldPackCardName).GetComponent<GoldPackCard>();
            Assert.IsNotNull(card, nameof(GoldPackCard));

            AssertEveryReferenceAssigned(card, 5);
        }

        [TestCase(StreakMarkerName, typeof(StreakMarker), 4)]
        [TestCase(StreakLadderRowName, typeof(StreakLadderRow), 4)]
        [TestCase(StreakPrizeName, typeof(StreakPrize), 2)]
        public void Build_StreakPartPrefab_EveryReferenceIsAssigned(string prefabName, Type viewType, int expectedFields)
        {
            var view = LoadBuilt(prefabName).GetComponent(viewType);
            Assert.IsNotNull(view, viewType.Name);

            AssertEveryReferenceAssigned(view, expectedFields);
        }

        [Test]
        public void Build_AvatarButton_EveryReferenceIsAssigned()
        {
            var cell = LoadBuilt(AvatarButtonName).GetComponent<AvatarButton>();
            Assert.IsNotNull(cell, nameof(AvatarButton));

            AssertEveryReferenceAssigned(cell, 3);
        }

        [Test]
        public void Build_LeaderboardRow_EveryReferenceIsAssigned()
        {
            var row = LoadBuilt(LeaderboardRowName).GetComponent<LeaderboardRow>();
            Assert.IsNotNull(row, nameof(LeaderboardRow));

            AssertEveryReferenceAssigned(row, 9);
        }

        [Test]
        public void Build_LeaderboardRow_MedalsAreTheThreeMedalSpritesInRankOrder()
        {
            var row = LoadBuilt(LeaderboardRowName).GetComponent<LeaderboardRow>();
            Assert.IsNotNull(row, nameof(LeaderboardRow));

            using (var serialized = new SerializedObject(row))
            {
                Assert.AreEqual(GoldMedalSpritePath, AssetDatabase.GetAssetPath(Reference<Sprite>(serialized, "goldMedal")));
                Assert.AreEqual(SilverMedalSpritePath, AssetDatabase.GetAssetPath(Reference<Sprite>(serialized, "silverMedal")));
                Assert.AreEqual(BronzeMedalSpritePath, AssetDatabase.GetAssetPath(Reference<Sprite>(serialized, "bronzeMedal")));
            }
        }

        [Test]
        public void Build_LeaderboardPage_CreatesItsRowsFromTheBuiltLeaderboardRow()
        {
            var screen = LoadBuilt(LeaderboardPageName).GetComponent<LeaderboardScreen>();
            Assert.IsNotNull(screen, nameof(LeaderboardScreen));

            LeaderboardRow prefab;
            using (var serialized = new SerializedObject(screen))
            {
                prefab = (LeaderboardRow)serialized.FindProperty("rowPrefab").objectReferenceValue;
            }

            Assert.AreEqual($"{folder}/{LeaderboardRowName}.prefab", AssetDatabase.GetAssetPath(prefab), "the built Leaderboard Row");
        }

        [Test]
        public void Build_ProfileOverlay_GridHasFiveColumnsOfTheBuiltAvatarButton()
        {
            var profile = LoadBuilt(ProfileOverlayName).GetComponent<ProfileOverlay>();
            Assert.IsNotNull(profile, nameof(ProfileOverlay));

            var grid = profile.GetComponentInChildren<GridLayoutGroup>(true);
            AvatarButton prefab;
            using (var serialized = new SerializedObject(profile))
            {
                prefab = (AvatarButton)serialized.FindProperty("avatarButtonPrefab").objectReferenceValue;
            }

            Assert.IsNotNull(grid, "the grid's layout group");
            Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, grid.constraint);
            Assert.AreEqual(ProfileGridColumns, grid.constraintCount);
            Assert.AreEqual(((RectTransform)prefab.transform).sizeDelta, grid.cellSize, "cells at the Avatar Button's own size");
            Assert.AreEqual($"{folder}/{AvatarButtonName}.prefab", AssetDatabase.GetAssetPath(prefab), "the built Avatar Button");
        }

        [Test]
        public void Build_StreakBar_IsAnObjectAddedToTheHomePageInstanceUnderItsStreakArea()
        {
            var contents = PrefabUtility.LoadPrefabContents($"{folder}/{FrontEndName}.prefab");
            try
            {
                var bar = contents.GetComponentInChildren<StreakBar>(true);
                var home = contents.GetComponentInChildren<HomeScreen>(true);
                Assert.IsNotNull(bar, nameof(StreakBar));
                Assert.IsNotNull(home, nameof(HomeScreen));
                var barRect = (RectTransform)bar.transform;

                var added = PrefabUtility.GetAddedGameObjects(home.gameObject).Select(addition => addition.instanceGameObject);

                Assert.AreEqual(StreakAreaName, barRect.parent.name);
                Assert.AreEqual(home.transform, barRect.parent.parent, "the Streak Area is the Home Page instance's own");
                CollectionAssert.Contains(added, bar.gameObject, "recorded as an object added to the Home Page instance");
                Assert.AreEqual(
                    $"{folder}/{StreakBarName}.prefab",
                    AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(bar.gameObject)),
                    "a nested instance of the built Streak Bar");
                Assert.AreEqual(Vector2.zero, barRect.anchorMin, "stretched to the Streak Area");
                Assert.AreEqual(Vector2.one, barRect.anchorMax, "stretched to the Streak Area");
                Assert.AreEqual(Vector2.zero, barRect.offsetMin, "stretched to the Streak Area");
                Assert.AreEqual(Vector2.zero, barRect.offsetMax, "stretched to the Streak Area");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void Build_TintedGraphics_CarryTheirUiConfigColour()
        {
            var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiConfigPath);
            Assert.IsNotNull(config, UiConfigPath);
            var tints = LoadBuilt(FrontEndName).GetComponentsInChildren<UiTint>(true)
                .Concat(LoadBuilt(GoldPackCardName).GetComponentsInChildren<UiTint>(true))
                .Concat(LoadBuilt(LeaderboardRowName).GetComponentsInChildren<UiTint>(true))
                .Concat(LoadBuilt(LevelUiName).GetComponentsInChildren<UiTint>(true))
                .ToList();
            var wrong = new List<string>();

            foreach (var tint in tints)
            {
                using (var serialized = new SerializedObject(tint))
                {
                    var role = (UiColorRole)serialized.FindProperty(TintRoleField).intValue;
                    var target = (Graphic)serialized.FindProperty(TintTargetField).objectReferenceValue;
                    if (target == null || target.color != config.ColorOf(role))
                    {
                        wrong.Add($"{tint.name} ({role})");
                    }
                }
            }

            Assert.That(tints.Count, Is.GreaterThan(OverlayTypes.Length), "the white glyphs, the plain shapes and the background are tinted");
            CollectionAssert.IsEmpty(wrong, "tinted graphics without their UI Config colour");
        }

        [Test]
        public void Build_CoinGraphics_AreTheOwnersCoinSpritesDrawnWhite()
        {
            var coins = BuilderDrawnImages()
                .Where(image => image.name == "Coin" || image.name == "Coin Stack")
                .ToList();
            var paths = coins.Select(image => AssetDatabase.GetAssetPath(image.sprite)).ToList();
            var wrong = coins
                .Where(image => (AssetDatabase.GetAssetPath(image.sprite) != CoinSpritePath && AssetDatabase.GetAssetPath(image.sprite) != CoinsSpritePath)
                    || image.color != Color.white)
                .Select(image => $"{image.name}: {AssetDatabase.GetAssetPath(image.sprite)} in {image.color}")
                .ToList();

            CollectionAssert.Contains(paths, CoinSpritePath, "the single coin is drawn somewhere");
            CollectionAssert.Contains(paths, CoinsSpritePath, "the pile of coins is drawn somewhere");
            CollectionAssert.IsEmpty(wrong, "coins drawing another sprite, or not in their own colours");
        }

        [Test]
        public void Build_InterfaceArtTheBuildDraws_IsNeverTinted()
        {
            // The build's own tints only: the Tab Bar keeps its icons' tints,
            // which dim the idle tabs' full-colour icons by design.
            var tinted = OutputNames
                .SelectMany(name => LoadBuilt(name).GetComponentsInChildren<UiTint>(true))
                .Where(tint => PrefabUtility.GetCorrespondingObjectFromSource(tint) == null)
                .Select(tint => (tint, image: TargetOf(tint) as Image))
                .Where(pair => pair.image != null && AssetDatabase.GetAssetPath(pair.image.sprite).StartsWith(InterfaceArtFolder))
                .Select(pair => $"{pair.tint.name} ({RoleOf(pair.tint)})")
                .ToList();

            CollectionAssert.IsEmpty(tinted, "tints on sprites drawn in their own colours");
        }

        [Test]
        public void Build_BuilderDrawnImages_UseNoReplacedSprite()
        {
            var kept = KeptKenneyGlyphs;
            var replaced = BuilderDrawnImages()
                .Where(image => image.sprite != null)
                .Select(image => (image, path: AssetDatabase.GetAssetPath(image.sprite)))
                .Where(pair => !pair.path.StartsWith(OwnArtFolder) && !kept.Contains(pair.path))
                .Select(pair => $"{pair.image.name}: {pair.path}")
                .Distinct()
                .ToList();

            CollectionAssert.IsEmpty(replaced, "images still drawing a piece Module 45 replaced");
        }

        [Test]
        public void Build_ScrollRects_UseTheBuilderScrollSensitivity()
        {
            var scrollRects = LoadBuilt(FrontEndName).GetComponentsInChildren<ScrollRect>(true);

            Assert.AreEqual(
                5, scrollRects.Length, "the Store page's, the Leaderboard page's, the Levels overlay's, the Win Streak overlay's and Profile's");
            foreach (var scrollRect in scrollRects)
            {
                Assert.AreEqual(ExpectedScrollSensitivity, scrollRect.scrollSensitivity, $"'{scrollRect.name}' scroll sensitivity");
            }
        }

        [Test]
        public void Build_FrontEnd_HandMadePartsStayLinkedToTheirPrefabs()
        {
            var frontEnd = LoadBuilt(FrontEndName);

            var topBar = SourcePath(frontEnd.GetComponentInChildren<TopBar>(true));
            var tabBar = SourcePath(frontEnd.GetComponentInChildren<TabBar>(true));
            var home = SourcePath(frontEnd.GetComponentInChildren<HomeScreen>(true));

            Assert.AreEqual(HandMadePath("Top Bar"), topBar);
            Assert.AreEqual(HandMadePath("Tab Bar"), tabBar);
            Assert.AreEqual(HandMadePath("Home Page"), home);
        }

        [Test]
        public void Build_FrontEnd_StartsOnHomeWithNoOverlay()
        {
            var root = LoadBuilt(FrontEndName).transform;

            var pages = root.Find("Safe/Pages");
            var overlays = root.Find("Overlays");

            Assert.IsFalse(root.gameObject.activeSelf, "the root is saved closed");
            Assert.IsNotNull(pages, "Safe/Pages");
            Assert.IsTrue(pages.Find("Home Page").gameObject.activeSelf, "Home Page");
            Assert.IsFalse(pages.Find("Store Page").gameObject.activeSelf, "Store Page");
            Assert.IsFalse(pages.Find("Leaderboard Page").gameObject.activeSelf, "Leaderboard Page");
            Assert.IsNotNull(overlays, "Overlays sits directly under the root, outside Safe");
            Assert.AreEqual(root, overlays.parent);
            foreach (var overlayType in OverlayTypes)
            {
                var overlay = root.GetComponentInChildren(overlayType, true);
                Assert.AreEqual(overlays, overlay.transform.parent, $"{overlayType.Name} is under Overlays");
                Assert.IsFalse(overlay.gameObject.activeSelf, $"{overlayType.Name} starts closed");
            }
        }

        [Test]
        public void Build_LevelUiUnderCanvas_MissingSetupReportsNothing()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                // LevelUi requires a Canvas above it, which the prefab leaves
                // to the scene.
                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvas, scene);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadBuilt(LevelUiName), canvas.transform);

                var problem = instance.GetComponent<LevelUi>().MissingSetup();

                Assert.IsNull(problem, problem);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase(typeof(LevelUi), 9)]
        [TestCase(typeof(LevelHud), 11)]
        [TestCase(typeof(JokerBarView), 3)]
        [TestCase(typeof(GoldPacksOverlay), 5)]
        [TestCase(typeof(NotEnoughGoldOverlay), 3)]
        [TestCase(typeof(TestPurchaseOverlay), 6)]
        [TestCase(typeof(MessageOverlay), 3)]
        [TestCase(typeof(ResultOverlay), 16)]
        [TestCase(typeof(ConfirmOverlay), 8)]
        public void Build_LevelUiView_EverySerializedReferenceIsAssigned(Type viewType, int expectedFields)
        {
            var view = LoadBuilt(LevelUiName).GetComponentInChildren(viewType, true);
            Assert.IsNotNull(view, viewType.Name);

            AssertEveryReferenceAssigned(view, expectedFields);
        }

        [Test]
        public void Build_JokerBarButtons_EveryReferenceIsAssignedAndTheyRiseWhenSelected()
        {
            var buttons = LoadBuilt(JokerBarName).GetComponentsInChildren<JokerBarButton>(true);

            Assert.AreEqual(3, buttons.Length, "the Clock, Rocket and Broom buttons");
            foreach (var button in buttons)
            {
                AssertEveryReferenceAssigned(button, 8);
                using (var serialized = new SerializedObject(button))
                {
                    Assert.That(serialized.FindProperty("selectedRaise").floatValue, Is.GreaterThan(0f), $"{button.name} rises when selected");
                }
            }
        }

        [Test]
        public void Build_JokerBar_IconsAreTheJokerIconsInClockRocketBroomOrder()
        {
            var icons = AssetDatabase.LoadAssetAtPath<JokerIcons>(JokerIconsPath);
            Assert.IsNotNull(icons, JokerIconsPath);
            var bar = LoadBuilt(JokerBarName).GetComponent<JokerBarView>();
            Assert.IsNotNull(bar, nameof(JokerBarView));

            using (var serialized = new SerializedObject(bar))
            {
                Assert.AreEqual(icons.IconOf(JokerKind.Clock), IconOf(serialized, "clock"));
                Assert.AreEqual(icons.IconOf(JokerKind.Rocket), IconOf(serialized, "rocket"));
                Assert.AreEqual(icons.IconOf(JokerKind.Broom), IconOf(serialized, "broom"));
            }
        }

        [Test]
        public void Build_JokerBarButtons_AreSquareButtonPrimariesWithTheSquareSprites()
        {
            var buttons = LoadBuilt(JokerBarName).GetComponentsInChildren<JokerBarButton>(true);
            Assert.AreEqual(3, buttons.Length, "the Clock, Rocket and Broom buttons");

            foreach (var view in buttons)
            {
                using (var serialized = new SerializedObject(view))
                {
                    var button = Reference<Button>(serialized, "button");
                    var size = ((RectTransform)button.transform).rect.size;

                    Assert.AreEqual(HandMadePath("Button Primary"), SourcePath(button), view.name);
                    Assert.That(size.x, Is.GreaterThan(0f), $"{view.name} has a size");
                    Assert.AreEqual(size.x, size.y, $"{view.name} is square");
                    Assert.AreEqual(JokerButtonSpritePath, AssetDatabase.GetAssetPath(button.image.sprite), $"{view.name}'s face");
                    Assert.IsNull(button.spriteState.pressedSprite, $"{view.name} has no pressed look until Module 46");
                    Assert.AreEqual(
                        JokerButtonDisabledSpritePath, AssetDatabase.GetAssetPath(button.spriteState.disabledSprite), $"{view.name} is grey disabled");
                }
            }
        }

        [Test]
        public void Build_JokerBarButtons_LockIsTheLockedSpriteOnTheIconSavedHidden()
        {
            var buttons = LoadBuilt(JokerBarName).GetComponentsInChildren<JokerBarButton>(true);
            Assert.AreEqual(3, buttons.Length, "the Clock, Rocket and Broom buttons");

            foreach (var view in buttons)
            {
                using (var serialized = new SerializedObject(view))
                {
                    AssertLockOnIcon(serialized, view.name);
                    var lockSize = ((RectTransform)Reference<GameObject>(serialized, "lockIcon").transform).sizeDelta.x;
                    var buttonSize = ((RectTransform)view.transform).sizeDelta.x;
                    Assert.That(lockSize / buttonSize, Is.InRange(LockMinShare, LockMaxShare), $"{view.name}'s lock is about half the button");
                }
            }
        }

        [Test]
        public void Build_JokerBarButtons_GlowIsSavedHiddenBehindTheRingAndTheButton()
        {
            var buttons = LoadBuilt(JokerBarName).GetComponentsInChildren<JokerBarButton>(true);
            Assert.AreEqual(3, buttons.Length, "the Clock, Rocket and Broom buttons");

            foreach (var view in buttons)
            {
                using (var serialized = new SerializedObject(view))
                {
                    var glow = Reference<Image>(serialized, "glow").transform;
                    var lift = Reference<RectTransform>(serialized, "lift");
                    var ring = Reference<GameObject>(serialized, "ring").transform;
                    var button = Reference<Button>(serialized, "button").transform;

                    Assert.AreEqual(lift, glow.parent, $"{view.name}'s glow rises with the button");
                    Assert.IsFalse(glow.gameObject.activeSelf, $"{view.name}'s glow is saved hidden");
                    Assert.That(glow.GetSiblingIndex(), Is.LessThan(ring.GetSiblingIndex()), $"{view.name}'s glow draws behind the ring");
                    Assert.That(glow.GetSiblingIndex(), Is.LessThan(button.GetSiblingIndex()), $"{view.name}'s glow draws behind the button");
                }
            }
        }

        [Test]
        public void Build_JokerBarButtons_IconIsCentredAboveTheDepthStrip()
        {
            var buttons = LoadBuilt(JokerBarName).GetComponentsInChildren<JokerBarButton>(true);
            Assert.AreEqual(3, buttons.Length, "the Clock, Rocket and Broom buttons");

            foreach (var view in buttons)
            {
                using (var serialized = new SerializedObject(view))
                {
                    var button = Reference<Button>(serialized, "button");
                    var icon = Reference<Image>(serialized, "icon").rectTransform;
                    var centre = new Vector2(0.5f, 0.5f);

                    Assert.AreEqual(button.transform, icon.parent, $"{view.name}'s icon is on the face");
                    Assert.AreEqual(centre, icon.anchorMin, $"{view.name}'s icon is anchored at the centre");
                    Assert.AreEqual(centre, icon.anchorMax, $"{view.name}'s icon is anchored at the centre");
                    Assert.AreEqual(0f, icon.anchoredPosition.x, $"{view.name}'s icon is centred across");
                    Assert.That(icon.anchoredPosition.y, Is.GreaterThan(0f), $"{view.name}'s icon is raised above the depth strip");
                    Assert.AreEqual(icon.sizeDelta.x, icon.sizeDelta.y, $"{view.name}'s icon is square");
                }
            }
        }

        [Test]
        public void Build_JokerBarButtons_BadgeHangsOffTheBottomRightCornerAndGrowsLeft()
        {
            var buttons = LoadBuilt(JokerBarName).GetComponentsInChildren<JokerBarButton>(true);
            Assert.AreEqual(3, buttons.Length, "the Clock, Rocket and Broom buttons");

            foreach (var view in buttons)
            {
                using (var serialized = new SerializedObject(view))
                {
                    var button = Reference<Button>(serialized, "button");
                    var badge = (RectTransform)Label(serialized, "badgeLabel").transform.parent;
                    var coin = Reference<GameObject>(serialized, "coin");
                    var corner = new Vector2(1f, 0f);
                    var fitter = badge.GetComponent<ContentSizeFitter>();
                    var badgeImage = badge.GetComponent<Image>();
                    Assert.IsNotNull(fitter, $"{view.name}'s badge has a ContentSizeFitter");
                    Assert.IsNotNull(badgeImage, $"{view.name}'s badge has an Image");

                    Assert.AreEqual(button.transform, badge.parent, $"{view.name}'s badge is on the button");
                    Assert.AreEqual(badge, coin.transform.parent, $"{view.name}'s coin is in the badge");
                    Assert.AreEqual(corner, badge.anchorMin, $"{view.name}'s badge is anchored at the bottom-right corner");
                    Assert.AreEqual(corner, badge.anchorMax, $"{view.name}'s badge is anchored at the bottom-right corner");
                    Assert.AreEqual(corner, badge.pivot, $"{view.name}'s badge grows leftwards from the corner");
                    Assert.That(badge.anchoredPosition.x, Is.GreaterThan(0f), $"{view.name}'s badge overlaps the right edge");
                    Assert.That(badge.anchoredPosition.y, Is.LessThan(0f), $"{view.name}'s badge overlaps the bottom edge");
                    Assert.AreEqual(ContentSizeFitter.FitMode.PreferredSize, fitter.horizontalFit, $"{view.name}'s badge widens for a price");
                    Assert.AreEqual(BadgeSpritePath, AssetDatabase.GetAssetPath(badgeImage.sprite), $"{view.name}'s badge is the red badge");
                    Assert.IsNull(badge.GetComponent<UiTint>(), $"{view.name}'s badge is drawn in its own colours");
                }
            }
        }

        [Test]
        public void Build_JokerBar_ButtonsAreAboutOnePointTwoTimesLargerWithHalfAButtonBetweenThemInTheCentre()
        {
            var bar = LoadBuilt(JokerBarName).GetComponent<JokerBarView>();
            Assert.IsNotNull(bar, nameof(JokerBarView));

            using (var serialized = new SerializedObject(bar))
            {
                var clock = (RectTransform)Reference<JokerBarButton>(serialized, "clock").transform;
                var rocket = (RectTransform)Reference<JokerBarButton>(serialized, "rocket").transform;
                var broom = (RectTransform)Reference<JokerBarButton>(serialized, "broom").transform;
                RectTransform badge;
                using (var rocketSerialized = new SerializedObject(rocket.GetComponent<JokerBarButton>()))
                {
                    badge = (RectTransform)Label(rocketSerialized, "badgeLabel").transform.parent;
                }

                var pitch = rocket.anchoredPosition.x - clock.anchoredPosition.x;

                var size = rocket.sizeDelta.x;

                Assert.That(size, Is.GreaterThanOrEqualTo(Module45JokerButtonSize * AboutOnePointTwoTimes), "the buttons");
                Assert.AreEqual(Module45BadgeToButton, badge.sizeDelta.y / size, ShareTolerance, "the badge scales with the button");
                Assert.AreEqual(Module45BadgeToButton, badge.GetComponent<LayoutElement>().minWidth / size, ShareTolerance, "the badge's width too");
                Assert.AreEqual(HalfAButton, (pitch - size) / size, ShareTolerance, "half a button between them");
                Assert.AreEqual(pitch, broom.anchoredPosition.x - rocket.anchoredPosition.x, Tolerance, "evenly spaced");
                Assert.AreEqual(0f, rocket.anchoredPosition.x, Tolerance, "grouped in the centre");
            }
        }

        [Test]
        public void Build_JokerBar_RaisedButtonsAndBadgesStayInsideTheRowClearOfItsFoot()
        {
            var buttons = LoadBuilt(JokerBarName).GetComponentsInChildren<JokerBarButton>(true);
            var fit = LoadBuilt(JokerBarName).GetComponentInChildren<BandFit>(true);
            Assert.AreEqual(3, buttons.Length, "the Clock, Rocket and Broom buttons");
            Assert.IsNotNull(fit, nameof(BandFit));
            Vector2 design;
            using (var serialized = new SerializedObject(fit))
            {
                design = serialized.FindProperty("designSize").vector2Value;
            }

            foreach (var view in buttons)
            {
                using (var serialized = new SerializedObject(view))
                {
                    var slot = (RectTransform)view.transform;
                    var glow = Reference<Image>(serialized, "glow").rectTransform;
                    var badge = (RectTransform)Label(serialized, "badgeLabel").transform.parent;
                    var raise = serialized.FindProperty("selectedRaise").floatValue;
                    var half = slot.sizeDelta.y / 2f;

                    var raisedGlowTop = slot.anchoredPosition.y + half + raise + glow.offsetMax.y;
                    var badgeFoot = slot.anchoredPosition.y - half + badge.anchoredPosition.y;
                    var rightEdge = slot.anchoredPosition.x + half + badge.anchoredPosition.x;

                    Assert.That(raisedGlowTop, Is.LessThanOrEqualTo(design.y / 2f), $"{view.name}'s raised glow stays in the row");
                    Assert.That(badgeFoot, Is.GreaterThan(-design.y / 2f), $"{view.name}'s badge stands off the row's foot");
                    Assert.That(Mathf.Abs(rightEdge), Is.LessThanOrEqualTo(design.x / 2f), $"{view.name}'s badge stays in the row");
                }
            }
        }

        [Test]
        public void Build_FrontEnd_PaintingEnvelopesTheScreenBehindEverything()
        {
            var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiConfigPath);
            Assert.IsNotNull(config, UiConfigPath);
            var frontEnd = LoadBuilt(FrontEndName).GetComponent<FrontEnd>();
            var painting = frontEnd.transform.Find("Background/Painting");
            Assert.IsNotNull(painting, "Background/Painting");
            var image = painting.GetComponent<Image>();
            var fit = painting.GetComponent<AspectRatioFitter>();

            using (var serialized = new SerializedObject(frontEnd))
            {
                Assert.AreEqual(image, Reference<Image>(serialized, "painting"));
                Assert.AreEqual(fit, Reference<AspectRatioFitter>(serialized, "paintingFit"));
            }

            Assert.AreEqual(0, painting.parent.GetSiblingIndex(), "the Background, and so the painting, draws first");
            Assert.AreEqual(config.BackgroundFor(1), image.sprite, "level 1's painting, as a new save shows");
            Assert.IsFalse(image.raycastTarget);
            Assert.AreEqual(AspectRatioFitter.AspectMode.EnvelopeParent, fit.aspectMode, "covers the screen, cropped, never stretched");
            Assert.AreEqual(image.sprite.rect.width / image.sprite.rect.height, fit.aspectRatio, Tolerance, "at the painting's own ratio");
        }

        [Test]
        public void Build_LevelHud_TextsAndColoursComeFromTheConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiConfigPath);
            Assert.IsNotNull(config, UiConfigPath);
            var hud = LoadBuilt(LevelHudName).GetComponent<LevelHud>();
            Assert.IsNotNull(hud, nameof(LevelHud));

            using (var serialized = new SerializedObject(hud))
            {
                var timer = Label(serialized, "timerLabel");
                var bonus = Label(serialized, "timeBonusLabel");
                var level = Label(serialized, "levelLabel");
                var status = Label(serialized, "livesStatusLabel");

                Assert.AreEqual(config.TimerColor, timer.color);
                Assert.AreEqual(config.TimeBonusColor, bonus.color);
                StringAssert.StartsWith(config.LevelLabelFormat.Split('{')[0], level.text, "the level label's own words");
                Assert.AreEqual(config.LivesFullLabel, status.text);
            }
        }

        [Test]
        public void Build_LevelHud_MenuAndRestartAreButtonRoundsWithHomeAndReturnIcons()
        {
            var hud = LoadBuilt(LevelHudName).GetComponent<LevelHud>();
            Assert.IsNotNull(hud, nameof(LevelHud));

            using (var serialized = new SerializedObject(hud))
            {
                var menu = (Button)serialized.FindProperty("menuButton").objectReferenceValue;
                var restart = (Button)serialized.FindProperty("restartButton").objectReferenceValue;

                Assert.AreEqual(HandMadePath("Button Round"), SourcePath(menu));
                Assert.AreEqual(HandMadePath("Button Round"), SourcePath(restart));
                Assert.AreEqual(HomeSpritePath, AssetDatabase.GetAssetPath(menu.transform.Find("Icon").GetComponent<Image>().sprite));
                Assert.AreEqual(ReturnSpritePath, AssetDatabase.GetAssetPath(restart.transform.Find("Icon").GetComponent<Image>().sprite));
            }
        }

        [Test]
        public void Build_LevelUi_OverlaysAreTheBuiltPrefabs()
        {
            var levelUi = LoadBuilt(LevelUiName);

            foreach (var (type, prefabName) in LevelOverlays)
            {
                var overlay = levelUi.GetComponentInChildren(type, true);
                Assert.IsNotNull(overlay, type.Name);
                Assert.AreEqual($"{folder}/{prefabName}.prefab", SourcePath(overlay), $"{type.Name} is the built {prefabName}");
            }
        }

        [Test]
        public void Build_LevelUi_StartsHiddenWithNoOverlay()
        {
            var root = LoadBuilt(LevelUiName).transform;

            var overlays = root.Find("Overlays");

            Assert.IsFalse(root.gameObject.activeSelf, "the root is saved closed");
            Assert.IsNotNull(overlays, "Overlays sits directly under the root");
            foreach (var (type, _) in LevelOverlays)
            {
                var overlay = root.GetComponentInChildren(type, true);
                Assert.AreEqual(overlays, overlay.transform.parent, $"{type.Name} is under Overlays");
                Assert.IsFalse(overlay.gameObject.activeSelf, $"{type.Name} starts closed");
            }

            Assert.IsTrue(root.GetComponentInChildren<LevelHud>(true).gameObject.activeSelf, "the HUD shows with the level");
            Assert.IsTrue(root.GetComponentInChildren<JokerBarView>(true).gameObject.activeSelf, "the bar shows with the level");
        }

        [Test]
        public void Build_ResultOverlay_ButtonsAreTheThemesPrefabsAndMenuHasTheHomeIcon()
        {
            var overlay = LoadBuilt(ResultOverlayName).GetComponent<ResultOverlay>();
            Assert.IsNotNull(overlay, nameof(ResultOverlay));

            using (var serialized = new SerializedObject(overlay))
            {
                var next = Reference<Button>(serialized, "nextButton");
                var winRestart = Reference<Button>(serialized, "winRestartButton");
                var lossRestart = Reference<Button>(serialized, "lossRestartButton");
                var menu = Reference<Button>(serialized, "menuButton");

                Assert.AreEqual(HandMadePath("Button Primary"), SourcePath(next), "Next is green");
                Assert.AreEqual(HandMadePath("Button Secondary"), SourcePath(winRestart), "Restart after a free-play win is blue");
                Assert.AreEqual(HandMadePath("Button Primary"), SourcePath(lossRestart), "Restart after a loss is green");
                Assert.AreEqual(HandMadePath("Button Round"), SourcePath(menu), "Menu is round");
                Assert.AreEqual(HomeSpritePath, AssetDatabase.GetAssetPath(menu.transform.Find("Icon").GetComponent<Image>().sprite));
            }
        }

        [Test]
        public void Build_ConfirmOverlay_LoseALifeIsARedButtonPrimaryAndKeepPlayingStaysGreen()
        {
            var overlay = LoadBuilt(ConfirmOverlayName).GetComponent<ConfirmOverlay>();
            Assert.IsNotNull(overlay, nameof(ConfirmOverlay));
            var primary = AssetDatabase.LoadAssetAtPath<GameObject>(HandMadePath("Button Primary")).GetComponent<Button>();

            using (var serialized = new SerializedObject(overlay))
            {
                var lose = Reference<Button>(serialized, "loseLifeButton");
                var keep = Reference<Button>(serialized, "keepPlayingButton");

                Assert.AreEqual(HandMadePath("Button Primary"), SourcePath(lose));
                Assert.AreEqual(HandMadePath("Button Primary"), SourcePath(keep));
                Assert.AreEqual(RedButtonSpritePath, AssetDatabase.GetAssetPath(lose.image.sprite), "Lose a life's face is red");
                Assert.IsNull(lose.spriteState.pressedSprite, "no pressed look until Module 46");
                Assert.AreEqual(primary.image.sprite, keep.image.sprite, "Keep playing keeps Button Primary's green");
            }
        }

        [Test]
        public void Build_LevelPanels_TextsComeFromTheConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiConfigPath);
            Assert.IsNotNull(config, UiConfigPath);
            var result = LoadBuilt(ResultOverlayName).GetComponent<ResultOverlay>();
            var confirm = LoadBuilt(ConfirmOverlayName).GetComponent<ConfirmOverlay>();
            Assert.IsNotNull(result, nameof(ResultOverlay));
            Assert.IsNotNull(confirm, nameof(ConfirmOverlay));

            using (var serialized = new SerializedObject(result))
            {
                Assert.AreEqual(config.LevelCompleteTitle, Label(serialized, "titleLabel").text);
                Assert.AreEqual(config.ClosingMessage, Label(serialized, "messageLabel").text);
                Assert.AreEqual(config.LifeLostLabel, Label(serialized, "lifeLostLabel").text);
                Assert.AreEqual(config.NextButtonLabel, Label(serialized, "nextLabel").text);
                Assert.AreEqual(config.RestartButtonLabel, Label(serialized, "winRestartLabel").text);
                Assert.AreEqual(config.RestartButtonLabel, Label(serialized, "lossRestartLabel").text);
            }

            using (var serialized = new SerializedObject(confirm))
            {
                Assert.AreEqual(config.LeaveLevelTitle, Label(serialized, "titleLabel").text);
                Assert.AreEqual(config.LoseLifeLabel, Label(serialized, "loseLifeLabel").text);
                Assert.AreEqual(config.KeepPlayingLabel, Label(serialized, "keepPlayingLabel").text);
            }
        }

        [Test]
        public void Build_LevelPanels_HaveNoCloseButton()
        {
            var buttons = LoadBuilt(ResultOverlayName).GetComponentsInChildren<Button>(true)
                .Concat(LoadBuilt(ConfirmOverlayName).GetComponentsInChildren<Button>(true))
                .ToList();

            var closes = buttons.Where(button => SourcePath(button) == HandMadePath("Button Close")).Select(button => button.name).ToList();

            Assert.That(buttons.Count, Is.EqualTo(6), "Next, two Restarts and Menu; Lose a life and Keep playing");
            CollectionAssert.IsEmpty(closes, "the buttons are the way out");
        }

        [Test]
        public void Build_LevelHud_MenuAndRestartAreTheSameLargeRoundButtonsMirroredAtTheSides()
        {
            var hud = LoadBuilt(LevelHudName).GetComponent<LevelHud>();
            Assert.IsNotNull(hud, nameof(LevelHud));
            var roundSize = ((RectTransform)AssetDatabase.LoadAssetAtPath<GameObject>(HandMadePath("Button Round")).transform).sizeDelta;

            using (var serialized = new SerializedObject(hud))
            {
                var menu = (RectTransform)Reference<Button>(serialized, "menuButton").transform;
                var restart = (RectTransform)Reference<Button>(serialized, "restartButton").transform;

                Assert.AreEqual(menu.sizeDelta, restart.sizeDelta, "the same size");
                Assert.That(menu.sizeDelta.x, Is.GreaterThan(roundSize.x), "larger than Button Round's own");
                Assert.AreEqual(new Vector2(0f, 1f), menu.anchorMin, "Menu at the top left");
                Assert.AreEqual(new Vector2(1f, 1f), restart.anchorMin, "Restart at the top right");
                Assert.AreEqual(-menu.anchoredPosition.x, restart.anchoredPosition.x, Tolerance, "mirrored across the row");
                Assert.AreEqual(menu.anchoredPosition.y, restart.anchoredPosition.y, Tolerance, "at one height");
            }
        }

        [Test]
        public void Build_LevelHud_LevelLabelSitsOnTheRibbonAboveTheTimer()
        {
            var hud = LoadBuilt(LevelHudName).GetComponent<LevelHud>();
            Assert.IsNotNull(hud, nameof(LevelHud));

            using (var serialized = new SerializedObject(hud))
            {
                var label = Label(serialized, "levelLabel");
                var timer = Reference<RectTransform>(serialized, "timer");
                var ribbon = (RectTransform)label.transform.parent;

                Assert.AreEqual(RibbonSpritePath, AssetDatabase.GetAssetPath(ribbon.GetComponent<Image>().sprite), "the label is on the ribbon");
                Assert.AreEqual(timer.parent, ribbon.parent, "the ribbon and the timer share the row");
                Assert.AreEqual(0f, ribbon.anchoredPosition.x, Tolerance, "the ribbon is centred");
                Assert.AreEqual(0f, timer.anchoredPosition.x, Tolerance, "the timer is centred");
                Assert.That(ExtentBelowTop(ribbon).bottom, Is.LessThanOrEqualTo(ExtentBelowTop(timer).top + Tolerance), "the timer is under the ribbon");
                Assert.AreEqual(PillSpritePath, AssetDatabase.GetAssetPath(timer.GetComponent<Image>().sprite), "the timer is the pill");
            }
        }

        [Test]
        public void Build_LevelHud_IconsHangOffTheirPillsLeftEnds()
        {
            var contents = PrefabUtility.LoadPrefabContents($"{folder}/{LevelHudName}.prefab");
            try
            {
                var cases = new[]
                {
                    (pill: "Gold Pill", icon: "Coin", sprite: CoinSpritePath, isLarge: true),
                    (pill: "Lives Pill", icon: "Heart", sprite: HeartSpritePath, isLarge: true),
                    (pill: "Timer", icon: "Hourglass", sprite: HourglassSpritePath, isLarge: false)
                };

                foreach (var (pillName, iconName, sprite, isLarge) in cases)
                {
                    var pill = Named(contents, pillName);
                    var icon = Named(contents, iconName);
                    var share = icon.sizeDelta.y / pill.sizeDelta.y;

                    Assert.AreEqual(pill, icon.parent, $"{iconName} is on {pillName}");
                    Assert.AreEqual(sprite, AssetDatabase.GetAssetPath(icon.GetComponent<Image>().sprite), iconName);
                    Assert.AreEqual(new Vector2(0f, 0.5f), icon.anchorMin, $"{iconName} is placed from {pillName}'s left end");
                    Assert.That(icon.anchoredPosition.x, Is.LessThan(0f), $"{iconName} hangs off {pillName}'s left end");
                    Assert.That(icon.anchoredPosition.x + icon.sizeDelta.x, Is.GreaterThan(0f), $"{iconName} still overlaps {pillName}");
                    if (isLarge)
                    {
                        Assert.That(share, Is.InRange(PillIconMinShare, PillIconMaxShare), $"{iconName} is about 1.3 times {pillName}'s height");
                    }
                    else
                    {
                        Assert.That(share, Is.GreaterThan(1f), $"{iconName} is taller than {pillName}");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void Build_LevelHud_PillsAreSymmetricAndTheGoldPillCentresAlone()
        {
            var contents = PrefabUtility.LoadPrefabContents($"{folder}/{LevelHudName}.prefab");
            try
            {
                var pills = Named(contents, "Pills");
                var gold = Named(contents, "Gold Pill");
                var lives = Named(contents, "Lives Pill");

                LayoutRebuilder.ForceRebuildLayoutImmediate(pills);
                var goldX = gold.localPosition.x;
                var livesX = lives.localPosition.x;
                lives.gameObject.SetActive(false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(pills);
                var aloneX = gold.localPosition.x;

                Assert.AreEqual(gold.sizeDelta, lives.sizeDelta, "the pills are the same size");
                Assert.That(livesX, Is.GreaterThan(goldX), "gold at the left, lives at the right");
                Assert.AreEqual(-goldX, livesX, Tolerance, "symmetric about the centre");
                Assert.AreEqual(0f, aloneX, Tolerance, "in free play the gold pill is centred");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void Build_LevelHud_EveryPartStaysInsideTheRow()
        {
            var contents = PrefabUtility.LoadPrefabContents($"{folder}/{LevelHudName}.prefab");
            try
            {
                var row = (RectTransform)contents.GetComponentInChildren<BandFit>(true).transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(Named(contents, "Pills"));
                var bounds = row.rect;
                var corners = new Vector3[4];
                var outside = new List<string>();

                foreach (var part in row.GetComponentsInChildren<RectTransform>(true).Where(part => part != row))
                {
                    part.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var local = row.InverseTransformPoint(corner);
                        if (local.x < bounds.xMin - Tolerance || local.x > bounds.xMax + Tolerance
                            || local.y < bounds.yMin - Tolerance || local.y > bounds.yMax + Tolerance)
                        {
                            outside.Add($"{part.name} at {local}");
                            break;
                        }
                    }
                }

                CollectionAssert.IsEmpty(outside, $"parts outside the {bounds.size} row");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void Build_LevelHud_RowFitsTheTopBandAtTheReferenceUnscaled()
        {
            var runtimeConfig = AssetDatabase.LoadAssetAtPath<RuntimeConfig>(RuntimeConfigPath);
            Assert.IsNotNull(runtimeConfig, RuntimeConfigPath);
            var fit = LoadBuilt(LevelHudName).GetComponentInChildren<BandFit>(true);
            Assert.IsNotNull(fit, nameof(BandFit));

            Vector2 designSize;
            using (var serialized = new SerializedObject(fit))
            {
                designSize = serialized.FindProperty("designSize").vector2Value;
            }

            Assert.AreEqual(designSize, ((RectTransform)fit.transform).sizeDelta, "the row is laid out at its design size");
            Assert.That(
                designSize.y, Is.LessThanOrEqualTo(runtimeConfig.TopBandScreenFraction * ReferenceHeight),
                "the top band holds the row without scaling it at the reference");
        }

        [Test]
        public void Build_RoundButtons_WhiteGlyphsAreFortyFiveToFiftyPercentOfTheButton()
        {
            var shares = new List<(string what, float share)>();

            ForEachLoaded(LevelHudName, contents =>
            {
                using (var serialized = new SerializedObject(contents.GetComponent<LevelHud>()))
                {
                    shares.Add(("HUD Menu", GlyphShare(Reference<Button>(serialized, "menuButton"))));
                    shares.Add(("HUD Restart", GlyphShare(Reference<Button>(serialized, "restartButton"))));
                }
            });
            ForEachLoaded(ResultOverlayName, contents =>
            {
                using (var serialized = new SerializedObject(contents.GetComponent<ResultOverlay>()))
                {
                    shares.Add(("result Menu", GlyphShare(Reference<Button>(serialized, "menuButton"))));
                }
            });
            ForEachLoaded("Settings Overlay", contents =>
            {
                foreach (var look in new[] { "Sound On Look", "Sound Off Look", "Haptics On Look", "Haptics Off Look" })
                {
                    var rect = Named(contents, look);
                    shares.Add((look, GlyphShareOf(rect, (RectTransform)rect.parent)));
                }
            });

            var wrong = shares
                .Where(pair => pair.share < GlyphMinShare || pair.share > GlyphMaxShare)
                .Select(pair => $"{pair.what}: {pair.share:P0}")
                .ToList();

            Assert.AreEqual(7, shares.Count, "the HUD's two, the result panel's Menu and the four Settings looks");
            CollectionAssert.IsEmpty(
                wrong, "glyphs outside 45 to 50 percent of their button; run Gate Rush > Apply Hand-Made Prefab Settings, then rebuild");
        }

        [Test]
        public void Build_RoundButtons_IconsKeepButtonRoundsGlyphAnchors()
        {
            var icons = new List<RectTransform>();

            ForEachLoaded(LevelHudName, contents =>
            {
                using (var serialized = new SerializedObject(contents.GetComponent<LevelHud>()))
                {
                    icons.Add((RectTransform)Reference<Button>(serialized, "menuButton").transform.Find("Icon"));
                    icons.Add((RectTransform)Reference<Button>(serialized, "restartButton").transform.Find("Icon"));
                }

                AssertGlyphBoxes(icons);
            });
            ForEachLoaded(ResultOverlayName, contents =>
            {
                using (var serialized = new SerializedObject(contents.GetComponent<ResultOverlay>()))
                {
                    AssertGlyphBoxes(new[] { (RectTransform)Reference<Button>(serialized, "menuButton").transform.Find("Icon") });
                }
            });
            ForEachLoaded("Settings Overlay", contents =>
            {
                foreach (var (look, shift, _) in SettingsLooks)
                {
                    AssertGlyphBox(Named(contents, look), shift);
                }
            });
        }

        [Test]
        public void Build_SettingsLooks_AreShiftedToCentreTheirDrawnShape()
        {
            ForEachLoaded("Settings Overlay", contents =>
            {
                foreach (var (look, _, drawnCentre) in SettingsLooks)
                {
                    var rect = Named(contents, look);
                    var boxCentre = (rect.anchorMin.x + rect.anchorMax.x) / 2f;
                    var boxWidth = rect.anchorMax.x - rect.anchorMin.x;

                    var shapeCentre = boxCentre + (drawnCentre - FileCentre) * boxWidth;

                    Assert.AreEqual(FileCentre, shapeCentre, GlyphCentreTolerance, $"{look}'s drawn shape is centred on its button");
                }
            });
        }

        [Test]
        public void Build_Overlays_ScrimIsTintedWithTheScrimRole()
        {
            var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiConfigPath);
            Assert.IsNotNull(config, UiConfigPath);
            var wrong = new List<string>();

            foreach (var name in ScrimmedOverlayNames)
            {
                var scrim = LoadBuilt(name).transform.Find("Scrim");
                var image = scrim == null ? null : scrim.GetComponent<Image>();
                var tint = scrim == null ? null : scrim.GetComponent<UiTint>();
                if (image == null || tint == null || RoleOf(tint) != UiColorRole.Scrim || TargetOf(tint) != image
                    || image.color != config.ScrimColor || !image.raycastTarget)
                {
                    wrong.Add(name);
                }
            }

            CollectionAssert.IsEmpty(wrong, "overlays whose scrim is not the UI Config's, tinted by the Scrim role, catching presses");
        }

        [Test]
        public void Build_ResultOverlay_LossArtIsTheHourglassAndTheLifeLostHeartSavedHidden()
        {
            var overlay = LoadBuilt(ResultOverlayName).GetComponent<ResultOverlay>();
            Assert.IsNotNull(overlay, nameof(ResultOverlay));

            using (var serialized = new SerializedObject(overlay))
            {
                var lossArt = (RectTransform)Reference<GameObject>(serialized, "lossArt").transform;
                var lifeLost = (RectTransform)Reference<GameObject>(serialized, "lifeLost").transform;
                var label = Label(serialized, "lifeLostLabel");
                var panel = Reference<RectTransform>(serialized, "panel");
                var buttons = (RectTransform)Reference<Button>(serialized, "nextButton").transform.parent;
                var heart = lifeLost.GetComponentsInChildren<Image>(true).Single();
                var buttonsTop = panel.sizeDelta.y - (buttons.anchoredPosition.y + buttons.sizeDelta.y);

                Assert.AreEqual(HourglassSpritePath, AssetDatabase.GetAssetPath(lossArt.GetComponent<Image>().sprite));
                Assert.AreEqual(HeartSpritePath, AssetDatabase.GetAssetPath(heart.sprite));
                Assert.AreEqual(lifeLost, label.transform.parent, "the '-1' is beside the heart");
                Assert.IsFalse(lossArt.gameObject.activeSelf, "saved hidden: the panel is written as a win");
                Assert.IsFalse(lifeLost.gameObject.activeSelf, "saved hidden");
                Assert.That(ExtentBelowTop(lossArt).bottom, Is.LessThanOrEqualTo(ExtentBelowTop(lifeLost).top + Tolerance), "the heart is under the hourglass");
                Assert.That(ExtentBelowTop(lifeLost).bottom, Is.LessThanOrEqualTo(buttonsTop + Tolerance), "both clear of the buttons");
            }
        }

        [Test]
        public void Build_ResultOverlay_MenuIsCloseToTheRectangleButtonsHeight()
        {
            var overlay = LoadBuilt(ResultOverlayName).GetComponent<ResultOverlay>();
            Assert.IsNotNull(overlay, nameof(ResultOverlay));

            using (var serialized = new SerializedObject(overlay))
            {
                var menu = (RectTransform)Reference<Button>(serialized, "menuButton").transform;
                var next = (RectTransform)Reference<Button>(serialized, "nextButton").transform;

                Assert.That(menu.sizeDelta.y, Is.InRange(MenuMinHeightShare * next.sizeDelta.y, next.sizeDelta.y));
                Assert.AreEqual(menu.sizeDelta.x, menu.sizeDelta.y, Tolerance, "still round");
            }
        }

        [Test]
        public void Build_ProfileOverlay_NameFieldIsThePillWithWhiteText()
        {
            var profile = LoadBuilt(ProfileOverlayName).GetComponent<ProfileOverlay>();
            Assert.IsNotNull(profile, nameof(ProfileOverlay));

            using (var serialized = new SerializedObject(profile))
            {
                var field = Reference<TMP_InputField>(serialized, "nameField");
                var box = (Image)field.targetGraphic;

                Assert.AreEqual(PillSpritePath, AssetDatabase.GetAssetPath(box.sprite), "the dark pill");
                Assert.AreEqual(Color.white, box.color, "drawn in its own colours");
                Assert.AreEqual(Color.white, field.textComponent.color, "white text");
                Assert.IsTrue(field.customCaretColor);
                Assert.AreEqual(Color.white, field.caretColor, "a white caret");
            }
        }

        [Test]
        public void Build_AvatarButton_SelectedMarkIsTheGoldRingOverTheDisc()
        {
            var cell = LoadBuilt(AvatarButtonName).GetComponent<AvatarButton>();
            Assert.IsNotNull(cell, nameof(AvatarButton));

            using (var serialized = new SerializedObject(cell))
            {
                var mark = (RectTransform)Reference<GameObject>(serialized, "selectedMark").transform;
                var icon = Reference<Image>(serialized, "icon");
                var ring = mark.GetComponent<Image>();

                Assert.AreEqual(RingSelectSpritePath, AssetDatabase.GetAssetPath(ring.sprite), "the gold ring");
                Assert.AreEqual(Color.white, ring.color, "drawn in its own colours");
                Assert.IsNull(mark.GetComponent<UiTint>(), "never tinted");
                Assert.IsFalse(ring.raycastTarget, "the disc takes the press");
                Assert.IsFalse(mark.gameObject.activeSelf, "saved hidden");
                Assert.AreEqual(icon.transform.parent, mark.parent);
                Assert.That(mark.GetSiblingIndex(), Is.GreaterThan(icon.transform.GetSiblingIndex()), "drawn over the disc");
                Assert.That(mark.offsetMin.x, Is.LessThan(0f), "around the disc, outside the cell");
            }
        }

        [Test]
        public void Build_LivesOverlay_NextLifeLabelSitsAboveTheWaitFromTheConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiConfigPath);
            Assert.IsNotNull(config, UiConfigPath);
            var overlay = LoadBuilt("Lives Overlay").GetComponent<LivesOverlay>();
            Assert.IsNotNull(overlay, nameof(LivesOverlay));

            using (var serialized = new SerializedObject(overlay))
            {
                var nextLife = Label(serialized, "nextLifeLabel");
                var status = Label(serialized, "statusLabel");

                Assert.AreEqual(config.NextLifeLabel, nextLife.text);
                Assert.IsFalse(nextLife.gameObject.activeSelf, "saved hidden, with the lives full");
                Assert.AreEqual(nextLife.transform.parent, status.transform.parent);
                Assert.That(
                    ExtentBelowTop((RectTransform)nextLife.transform).bottom,
                    Is.LessThanOrEqualTo(ExtentBelowTop((RectTransform)status.transform).top + Tolerance), "above the wait");
            }
        }

        [Test]
        public void Build_LivesOverlay_HeartNextLifeAndWaitStackWithoutOverlap()
        {
            var overlay = LoadBuilt("Lives Overlay").GetComponent<LivesOverlay>();
            Assert.IsNotNull(overlay, nameof(LivesOverlay));

            using (var serialized = new SerializedObject(overlay))
            {
                var count = (RectTransform)Label(serialized, "livesLabel").transform;
                var heart = (RectTransform)count.parent;
                var nextLife = (RectTransform)Label(serialized, "nextLifeLabel").transform;
                var status = (RectTransform)Label(serialized, "statusLabel").transform;
                var refill = (RectTransform)Reference<Button>(serialized, "refillButton").transform;
                var panel = (RectTransform)heart.parent;
                var sprite = heart.GetComponent<Image>().sprite;
                var refillTop = panel.sizeDelta.y - (refill.anchoredPosition.y + refill.sizeDelta.y);
                var title = ExtentBelowTop((RectTransform)panel.Find("Title"));

                Assert.AreEqual(HeartSpritePath, AssetDatabase.GetAssetPath(sprite));
                Assert.AreEqual(LivesHeartHeight, heart.sizeDelta.y, Tolerance, "about 150 tall");
                Assert.AreEqual(
                    LivesCountFontPerHeartHeight * heart.sizeDelta.y, count.GetComponent<TMP_Text>().fontSize, Tolerance, "the count scales with the heart");
                Assert.AreEqual(sprite.rect.width / sprite.rect.height, heart.sizeDelta.x / heart.sizeDelta.y, Tolerance, "at its sprite's own aspect");
                Assert.AreEqual(Vector2.zero, count.anchorMin, "the count is stretched in the heart, so centred");
                Assert.AreEqual(Vector2.one, count.anchorMax, "the count is stretched in the heart, so centred");
                Assert.That(ExtentBelowTop(heart).top, Is.GreaterThan(title.bottom), "the heart is under the title");
                Assert.That(ExtentBelowTop(nextLife).top, Is.GreaterThan(ExtentBelowTop(heart).bottom), "'Next life in' is under the heart");
                Assert.That(ExtentBelowTop(status).top, Is.GreaterThan(ExtentBelowTop(nextLife).bottom), "the wait is under 'Next life in'");
                Assert.That(refillTop, Is.GreaterThan(ExtentBelowTop(status).bottom), "Refill is under the wait");
                Assert.AreEqual(new Vector2(0.5f, 0f), refill.anchorMin, "Refill stays at the panel's foot");
                Assert.AreEqual(PanelButtonPosY, refill.anchoredPosition.y, Tolerance, "Refill stays where it was");
            }
        }

        [TestCase("Store Page")]
        [TestCase(LeaderboardPageName)]
        [TestCase("Levels Overlay")]
        [TestCase(ProfileOverlayName)]
        public void Build_ScrollViews_FadeAtTheirTopAndBottomEdges(string prefabName)
        {
            var scrollRect = LoadBuilt(prefabName).GetComponentsInChildren<ScrollRect>(true).Single();
            var viewport = scrollRect.viewport;
            var mask = viewport.GetComponent<RectMask2D>();
            var layout = scrollRect.content.GetComponent<LayoutGroup>();

            Assert.IsNotNull(mask, $"{prefabName}'s viewport clips with RectMask2D");
            Assert.IsNull(viewport.GetComponent<Mask>(), $"{prefabName}'s viewport has no Mask");
            Assert.AreEqual(new Vector2Int(0, SoftEdgeUnits), mask.softness, $"{prefabName}: soft at the top and bottom only");
            Assert.IsNotNull(layout, $"{prefabName}'s list layout");
            Assert.That(layout.padding.top, Is.GreaterThanOrEqualTo(SoftEdgeUnits), $"{prefabName}: nothing is faded at rest at the top");
            Assert.That(layout.padding.bottom, Is.GreaterThanOrEqualTo(SoftEdgeUnits), $"{prefabName}: nothing is faded at rest at the bottom");
        }

        [Test]
        public void Build_WinStreakOverlay_LadderKeepsAHardEdge()
        {
            var scrollRect = LoadBuilt("Win Streak Overlay").GetComponentsInChildren<ScrollRect>(true).Single();
            var mask = scrollRect.viewport.GetComponent<RectMask2D>();

            Assert.IsNotNull(mask);
            Assert.AreEqual(Vector2Int.zero, mask.softness, "the ladder fits the shipped table and does not scroll, so no row fades");
        }

        [Test]
        public void Build_StorePage_TestStoreNoteIsOneOutlinedLineWithNoPill()
        {
            var screen = LoadBuilt("Store Page").GetComponent<StoreScreen>();
            Assert.IsNotNull(screen, nameof(StoreScreen));

            using (var serialized = new SerializedObject(screen))
            {
                var label = Label(serialized, "testStoreNote");

                Assert.IsNull(label.GetComponent<Image>(), "no pill behind the line");
                Assert.IsNull(label.transform.parent.GetComponent<Image>(), "no pill behind the line");
                Assert.AreEqual(OutlinedTextMaterialPath, AssetDatabase.GetAssetPath(label.fontSharedMaterial), "outlined, with the underlay");
                AssertColourNear(Color.white, label.color, "the text stays white");
                Assert.AreEqual(TestStoreNoteFontSize, label.fontSize, Tolerance);
                Assert.AreEqual(TextWrappingModes.NoWrap, label.textWrappingMode, "one line");
            }
        }

        [Test]
        public void Build_OutlinedTextMaterial_HasThePurpleOutlineAndASoftUnderlayWithinThePadding()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(OutlinedTextMaterialPath);
            Assert.IsNotNull(material, OutlinedTextMaterialPath);
            var font = AssetDatabase.LoadAssetAtPath<GameObject>(HandMadePath("Button Primary")).GetComponentInChildren<TMP_Text>(true).font;
            var outline = material.GetFloat(OutlineWidthProperty) + material.GetFloat(FaceDilateProperty)
                          + font.material.GetFloat(WeightBoldProperty) * BoldWeightShare;
            var underlay = Mathf.Max(Mathf.Abs(material.GetFloat(UnderlayOffsetXProperty)), Mathf.Abs(material.GetFloat(UnderlayOffsetYProperty)))
                           + material.GetFloat(UnderlayDilateProperty) + material.GetFloat(UnderlaySoftnessProperty);

            Assert.AreEqual(DistanceFieldShaderName, material.shader.name);
            Assert.AreEqual(font.material.mainTexture, material.mainTexture, "it samples the button font's atlas");
            AssertColourNear(ParseHex(OutlineColorHex), material.GetColor(OutlineColorProperty), "the theme's purple outline");
            AssertColourNear(ParseHex(UnderlayColorHex), material.GetColor(UnderlayColorProperty), "the soft dark underlay");
            Assert.That(material.GetFloat(OutlineWidthProperty), Is.GreaterThan(0f));
            Assert.IsTrue(material.IsKeywordEnabled(UnderlayKeyword), "the underlay is on");
            Assert.That(material.GetFloat(UnderlayOffsetYProperty), Is.LessThan(0f), "the shadow sits below the letters");
            Assert.That(material.GetFloat(UnderlaySoftnessProperty), Is.GreaterThan(0f), "a soft shadow");
            Assert.That(outline, Is.LessThanOrEqualTo(PaddingRange), "the outline stays within the atlas's padding");
            Assert.That(underlay, Is.LessThanOrEqualTo(PaddingRange), "the underlay stays within the atlas's padding");
        }

        [Test]
        public void Build_PageTextsOnThePainting_UseTheOutlinedMaterial()
        {
            var store = LoadBuilt("Store Page").GetComponent<StoreScreen>();
            var leaderboard = LoadBuilt(LeaderboardPageName).GetComponent<LeaderboardScreen>();
            var labels = new List<TMP_Text>();
            using (var serialized = new SerializedObject(store))
            {
                labels.Add(Label(serialized, "goldHeader"));
                labels.Add(Label(serialized, "jokersHeader"));
                labels.Add(Label(serialized, "livesHeader"));
            }

            TMP_Text note;
            using (var serialized = new SerializedObject(leaderboard))
            {
                note = Label(serialized, "noteLabel");
            }

            labels.Add(note);
            foreach (var label in labels)
            {
                Assert.AreEqual(OutlinedTextMaterialPath, AssetDatabase.GetAssetPath(label.fontSharedMaterial), $"{label.name}: outlined");
            }

            Assert.AreEqual(TextWrappingModes.NoWrap, note.textWrappingMode, "the subtitle is one line");
            Assert.IsTrue(note.enableAutoSizing, "it shrinks only as far as it needs");
            Assert.AreEqual(LeaderboardNoteMaxFontSize, note.fontSizeMax, Tolerance);
            Assert.AreEqual(LeaderboardNoteMinFontSize, note.fontSizeMin, Tolerance, "never below 32");
        }

        [TestCaseSource(nameof(ClosedOverlayNames))]
        public void Build_Overlays_CloseButtonSitsInsideThePanelCornerClearOfTheTitle(string overlayName)
        {
            var runtimeConfig = AssetDatabase.LoadAssetAtPath<RuntimeConfig>(RuntimeConfigPath);
            Assert.IsNotNull(runtimeConfig, RuntimeConfigPath);
            var panel = LoadBuilt(overlayName).transform.Find("Panel");
            Assert.IsNotNull(panel, $"{overlayName}'s panel");
            var close = panel.GetComponentsInChildren<Button>(true).Single(button => SourcePath(button) == HandMadePath("Button Close"));
            var rect = (RectTransform)close.transform;
            var title = (RectTransform)panel.Find("Title");
            var titleLabel = title.GetComponent<TMP_Text>();
            var inset = runtimeConfig.IntroPanelPaddingUnits;
            var corner = new Vector2(1f, 1f);

            Assert.AreEqual(corner, rect.anchorMin, "anchored to the panel's top-right corner");
            Assert.AreEqual(corner, rect.anchorMax, "anchored to the panel's top-right corner");
            Assert.AreEqual(corner, rect.pivot, "by its own top-right corner, so it is fully inside");
            Assert.AreEqual(new Vector2(-inset, -inset), rect.anchoredPosition, "as far in as the introduction card's close button");
            Assert.That(-title.offsetMax.x, Is.GreaterThan(inset + rect.sizeDelta.x), "the title keeps clear of the button");
            Assert.AreEqual(title.offsetMin.x, -title.offsetMax.x, Tolerance, "and stays centred");
            Assert.IsTrue(titleLabel.enableAutoSizing, "a long title shrinks to fit");
            Assert.AreEqual(OverlayTitleFontSize, titleLabel.fontSizeMax, Tolerance);
            Assert.AreEqual(OverlayTitleMinFontSize, titleLabel.fontSizeMin, Tolerance);
        }

        [Test]
        public void Build_StreakPrize_IconAndTextAreCentredAsOneGroup()
        {
            var prize = LoadBuilt(StreakPrizeName);
            var group = prize.GetComponent<HorizontalLayoutGroup>();
            Assert.IsNotNull(group, "the line lays out its icon and label as one group");

            using (var serialized = new SerializedObject(prize.GetComponent<StreakPrize>()))
            {
                var icon = Reference<Image>(serialized, "icon");
                var label = Label(serialized, "label");

                Assert.AreEqual(TextAnchor.MiddleCenter, group.childAlignment, "centred under the title");
                Assert.IsFalse(group.childForceExpandWidth, "the group is only as wide as its icon and label");
                Assert.IsTrue(group.childControlWidth, "the label takes its own width");
                Assert.AreEqual(prize.transform, icon.transform.parent);
                Assert.AreEqual(prize.transform, label.transform.parent);
                Assert.That(icon.transform.GetSiblingIndex(), Is.LessThan(label.transform.GetSiblingIndex()), "the icon, then the text");
            }
        }

        [Test]
        public void Build_StreakMarker_RoundBaseWithTheGoldRingOnTheNext()
        {
            var marker = LoadBuilt(StreakMarkerName).GetComponent<StreakMarker>();
            Assert.IsNotNull(marker, nameof(StreakMarker));

            using (var serialized = new SerializedObject(marker))
            {
                var ring = (RectTransform)Reference<GameObject>(serialized, "highlight").transform;
                var icon = Reference<Image>(serialized, "icon");
                var disc = (RectTransform)marker.transform.Find("Disc");
                var ringImage = ring.GetComponent<Image>();
                var discImage = disc.GetComponent<Image>();

                Assert.AreEqual(MarkerBaseSpritePath, AssetDatabase.GetAssetPath(discImage.sprite), "a round base");
                Assert.AreEqual(Image.Type.Simple, discImage.type, "drawn whole, not as a square tile");
                Assert.AreEqual(RingSelectSpritePath, AssetDatabase.GetAssetPath(ringImage.sprite), "the gold ring");
                AssertColourNear(Color.white, ringImage.color, "drawn in its own colours");
                Assert.IsNull(ring.GetComponent<UiTint>(), "never tinted");
                Assert.IsFalse(ring.gameObject.activeSelf, "saved hidden: only the next reward shows it");
                Assert.That(ring.GetSiblingIndex(), Is.GreaterThan(disc.GetSiblingIndex()), "over the base's rim");
                Assert.That(ring.GetSiblingIndex(), Is.LessThan(icon.transform.GetSiblingIndex()), "under the icon");
                Assert.That(ring.offsetMin.x, Is.LessThan(0f), "around the base");
            }
        }

        [Test]
        public void Build_WinStreakOverlay_RuleSitsRightUnderTheShippedLadder()
        {
            var metaConfig = AssetDatabase.LoadAssetAtPath<MetaConfig>(MetaConfigPath);
            Assert.IsNotNull(metaConfig, MetaConfigPath);
            var rewards = metaConfig.ToRules().StreakRewards.Count;
            var overlay = LoadBuilt("Win Streak Overlay").GetComponent<WinStreakOverlay>();
            Assert.IsNotNull(overlay, nameof(WinStreakOverlay));

            using (var serialized = new SerializedObject(overlay))
            {
                var rows = Reference<RectTransform>(serialized, "rows");
                var rowHeight = Reference<StreakLadderRow>(serialized, "rowPrefab").GetComponent<LayoutElement>().preferredHeight;
                var rule = (RectTransform)Label(serialized, "ruleLabel").transform;
                var scroll = (RectTransform)rows.parent.parent;
                var panel = (RectTransform)scroll.parent;
                var spacing = rows.GetComponent<VerticalLayoutGroup>().spacing;
                var ladder = panel.sizeDelta.y + scroll.offsetMax.y - scroll.offsetMin.y;
                var gap = scroll.offsetMin.y - (rule.anchoredPosition.y + rule.sizeDelta.y);

                Assert.AreEqual(rewards * rowHeight + (rewards - 1) * spacing, ladder, Tolerance, "the ladder shows the shipped table's rows, no more");
                Assert.That(gap, Is.InRange(0f, RuleMaxGap), "the rule right under the ladder");
            }
        }

        private static T Reference<T>(SerializedObject serialized, string field) where T : Object
        {
            var value = (T)serialized.FindProperty(field).objectReferenceValue;
            Assert.IsNotNull(value, field);
            return value;
        }

        private static Sprite IconOf(SerializedObject bar, string field)
        {
            var button = (JokerBarButton)bar.FindProperty(field).objectReferenceValue;
            Assert.IsNotNull(button, field);
            using (var serialized = new SerializedObject(button))
            {
                return ((Image)serialized.FindProperty("icon").objectReferenceValue).sprite;
            }
        }

        private static TMP_Text Label(SerializedObject serialized, string field)
        {
            var label = (TMP_Text)serialized.FindProperty(field).objectReferenceValue;
            Assert.IsNotNull(label, field);
            return label;
        }

        private static string HandMadePath(string name) => $"{HandMadeFolder}/{name}.prefab";

        private static Color ParseHex(string hex)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out var color), hex);
            return color;
        }

        /// <summary><paramref name="actual"/> matches <paramref name="expected"/> channel by channel within <see cref="ColourChannelTolerance"/>.</summary>
        private static void AssertColourNear(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, ColourChannelTolerance, $"{message}: red");
            Assert.AreEqual(expected.g, actual.g, ColourChannelTolerance, $"{message}: green");
            Assert.AreEqual(expected.b, actual.b, ColourChannelTolerance, $"{message}: blue");
            Assert.AreEqual(expected.a, actual.a, ColourChannelTolerance, $"{message}: alpha");
        }

        /// <summary>
        /// Where <paramref name="rect"/> reaches, measured down from its
        /// parent's top edge, for a rect anchored to that edge.
        /// </summary>
        private static (float top, float bottom) ExtentBelowTop(RectTransform rect)
        {
            Assert.AreEqual(1f, rect.anchorMin.y, $"{rect.name} is anchored to its parent's top");
            Assert.AreEqual(1f, rect.anchorMax.y, $"{rect.name} is anchored to its parent's top");
            var top = -(rect.anchoredPosition.y + (1f - rect.pivot.y) * rect.sizeDelta.y);
            return (top, top + rect.sizeDelta.y);
        }

        /// <summary>The one RectTransform named <paramref name="name"/> under <paramref name="root"/>.</summary>
        private static RectTransform Named(GameObject root, string name)
        {
            var found = root.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name == name).ToList();
            Assert.AreEqual(1, found.Count, $"exactly one '{name}' under {root.name}");
            return found[0];
        }

        /// <summary>Runs <paramref name="check"/> on the loaded contents of the built <paramref name="name"/>, so its rects are laid out.</summary>
        private void ForEachLoaded(string name, Action<GameObject> check)
        {
            var contents = PrefabUtility.LoadPrefabContents($"{folder}/{name}.prefab");
            try
            {
                check(contents);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>Each icon is anchored to Button Round's glyph box with no offsets, so it scales with its button.</summary>
        private static void AssertGlyphBoxes(IEnumerable<RectTransform> icons)
        {
            foreach (var icon in icons)
            {
                AssertGlyphBox(icon, 0f);
            }
        }

        /// <summary>
        /// The icon is anchored to Button Round's glyph box moved right by
        /// <paramref name="shift"/> of its button's width, with no offsets.
        /// </summary>
        private static void AssertGlyphBox(RectTransform icon, float shift)
        {
            Assert.IsNotNull(icon, "a round button's icon");
            var offset = new Vector2(shift, 0f);
            Assert.AreEqual(HandMadePrefabSettings.GlyphAnchorMin + offset, icon.anchorMin, $"{icon.name} on {icon.parent.name}");
            Assert.AreEqual(HandMadePrefabSettings.GlyphAnchorMax + offset, icon.anchorMax, $"{icon.name} on {icon.parent.name}");
            Assert.AreEqual(Vector2.zero, icon.anchoredPosition, $"{icon.name} on {icon.parent.name}: no offset");
            Assert.AreEqual(Vector2.zero, icon.sizeDelta, $"{icon.name} on {icon.parent.name}: no offset");
        }

        /// <summary>How much of a round button its own icon's glyph covers.</summary>
        private static float GlyphShare(Button button)
        {
            var icon = button.transform.Find("Icon") as RectTransform;
            Assert.IsNotNull(icon, $"{button.name}'s Icon");
            return GlyphShareOf(icon, (RectTransform)button.transform);
        }

        /// <summary>How much of <paramref name="button"/>'s width the Kenney glyph drawn in <paramref name="icon"/> covers.</summary>
        private static float GlyphShareOf(RectTransform icon, RectTransform button) =>
            Mathf.Min(icon.rect.width, icon.rect.height) / button.rect.width * KenneyGlyphFill;

        private static Graphic TargetOf(UiTint tint)
        {
            using (var serialized = new SerializedObject(tint))
            {
                return serialized.FindProperty(TintTargetField).objectReferenceValue as Graphic;
            }
        }

        /// <summary>
        /// Every image the build itself draws in every built prefab: the
        /// prefab's own objects, those it added to nested instances included,
        /// but not the objects nested prefabs bring, which their own prefab
        /// draws.
        /// </summary>
        private IEnumerable<Image> BuilderDrawnImages() =>
            OutputNames
                .SelectMany(name => LoadBuilt(name).GetComponentsInChildren<Image>(true))
                .Where(image => PrefabUtility.GetCorrespondingObjectFromSource(image) == null);

        private static UiColorRole RoleOf(UiTint tint)
        {
            using (var serialized = new SerializedObject(tint))
            {
                return (UiColorRole)serialized.FindProperty(TintRoleField).intValue;
            }
        }

        /// <summary>
        /// Module 43's lock: the serialized <c>lockIcon</c> is a child of the
        /// <c>icon</c>, centred on it, drawing locked.png, and saved hidden.
        /// </summary>
        private static void AssertLockOnIcon(SerializedObject serialized, string owner)
        {
            var icon = Reference<Image>(serialized, "icon");
            var lockIcon = Reference<GameObject>(serialized, "lockIcon");
            var lockRect = (RectTransform)lockIcon.transform;
            var centre = new Vector2(0.5f, 0.5f);

            Assert.AreEqual(icon.transform, lockRect.parent, $"{owner}'s lock sits on its icon");
            Assert.AreEqual(centre, lockRect.anchorMin, $"{owner}'s lock is centred on the icon");
            Assert.AreEqual(centre, lockRect.anchorMax, $"{owner}'s lock is centred on the icon");
            Assert.AreEqual(Vector2.zero, lockRect.anchoredPosition, $"{owner}'s lock is centred on the icon");
            Assert.AreEqual(LockSpritePath, AssetDatabase.GetAssetPath(lockIcon.GetComponent<Image>().sprite), $"{owner}'s lock");
            Assert.IsFalse(lockIcon.activeSelf, $"{owner}'s lock is saved hidden");
        }

        /// <summary>What a Store card shows: the line under the name, whether the buy button and the lock show, and the icon's opacity.</summary>
        private static (string subText, bool isBuyShown, bool isLockShown, float iconAlpha) LookOf(StoreItemCard card)
        {
            using (var serialized = new SerializedObject(card))
            {
                return (
                    Label(serialized, "subLabel").text,
                    Reference<Button>(serialized, "buyButton").gameObject.activeSelf,
                    Reference<GameObject>(serialized, "lockIcon").activeSelf,
                    Reference<Image>(serialized, "icon").color.a);
            }
        }

        private static void AssertEveryReferenceAssigned(Object component, int expectedFields)
        {
            var checkedFields = 0;
            var unassigned = new List<string>();

            using (var serialized = new SerializedObject(component))
            {
                var property = serialized.GetIterator();
                for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script")
                    {
                        continue;
                    }

                    checkedFields++;
                    if (property.objectReferenceValue == null)
                    {
                        unassigned.Add(property.name);
                    }
                }
            }

            var label = $"{component.GetType().Name} on '{component.name}'";
            CollectionAssert.IsEmpty(unassigned, $"{label}: unassigned fields");
            Assert.AreEqual(expectedFields, checkedFields, $"{label}: every reference field is checked");
        }

        private GameObject LoadBuilt(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.prefab");
            Assert.IsNotNull(prefab, $"the built {name}");
            return prefab;
        }

        private static string SourcePath(Component nested)
        {
            Assert.IsNotNull(nested);
            return AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(nested.gameObject));
        }
    }
}
