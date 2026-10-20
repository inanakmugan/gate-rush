using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GateRush.UI;
using GateRush.UI.Editor;
using NUnit.Framework;
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
    /// writes the eighteen prefabs and nothing else, the Front End it builds
    /// reports no missing part, every overlay's, the Store's, its cards' and
    /// the Gold Pack Card's fields are wired, the hand-made parts stay linked
    /// to their prefabs and their files unwritten, every tinted graphic
    /// carries its UI Config colour, and the front end starts as
    /// <see cref="FrontEnd.Show"/> leaves it. The streak's parts are fully
    /// wired, and the streak bar sits under the Home Page instance's Streak
    /// Area as an object added to it, Home Page's own file unwritten. Module
    /// 38's Avatar Button and Profile overlay are fully wired, the grid has
    /// five columns of the built Avatar Button, and Top Bar's file stays
    /// unwritten with the other hand-made prefabs.
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
        private const int ProfileGridColumns = 5;
        private const string TintRoleField = "role";
        private const string TintTargetField = "target";
        private const float ExpectedScrollSensitivity = 20f;

        private static string[] OutputNames => new[]
        {
            GoldPackCardName, "Store Page", "Leaderboard Page", "Settings Overlay", "Levels Overlay", "Lives Overlay",
            "Not Enough Gold Overlay", "Message Overlay", "Test Purchase Overlay", StreakMarkerName, StreakBarName,
            StreakLadderRowName, "Win Streak Overlay", StreakPrizeName, "Streak Reward Overlay", AvatarButtonName,
            ProfileOverlayName, FrontEndName
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
        public void Build_IntoFolder_WritesExactlyTheEighteenPrefabs()
        {
            var names = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
                .ToList();

            CollectionAssert.AreEquivalent(OutputNames, names);
        }

        [Test]
        public void ExistingOutputs_AfterBuild_NamesAllEighteen()
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
        [TestCase(typeof(LivesOverlay), 5)]
        [TestCase(typeof(NotEnoughGoldOverlay), 3)]
        [TestCase(typeof(MessageOverlay), 3)]
        [TestCase(typeof(TestPurchaseOverlay), 6)]
        [TestCase(typeof(StoreScreen), 11)]
        [TestCase(typeof(WinStreakOverlay), 6)]
        [TestCase(typeof(StreakRewardOverlay), 4)]
        [TestCase(typeof(StreakBar), 6)]
        [TestCase(typeof(ProfileOverlay), 16)]
        [TestCase(typeof(FrontEnd), 18)]
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
                AssertEveryReferenceAssigned(card, 4);
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

            Assert.That(tints.Count, Is.GreaterThan(OverlayTypes.Length), "the coins, hearts, icons and background are tinted");
            CollectionAssert.IsEmpty(wrong, "tinted graphics without their UI Config colour");
        }

        [Test]
        public void Build_ScrollRects_UseTheBuilderScrollSensitivity()
        {
            var scrollRects = LoadBuilt(FrontEndName).GetComponentsInChildren<ScrollRect>(true);

            Assert.AreEqual(4, scrollRects.Length, "the Store page's, the Levels overlay's, the Win Streak overlay's and Profile's");
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

        private static string HandMadePath(string name) => $"{HandMadeFolder}/{name}.prefab";

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
