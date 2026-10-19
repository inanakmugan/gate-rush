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

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="UiPrefabBuilder"/> (Module 35): the build writes the
    /// six prefabs and nothing else, the Front End it builds reports no
    /// missing part, every overlay field is wired, the hand-made parts stay
    /// linked to their prefabs, and the front end starts as
    /// <see cref="FrontEnd.Show"/> leaves it.
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
        private const string FrontEndName = "Front End";

        private static string[] OutputNames => new[]
        {
            "Store Page", "Leaderboard Page", "Settings Overlay", "Levels Overlay", "Lives Overlay", FrontEndName
        };

        private string folder;

        [OneTimeSetUp]
        public void BuildOnce()
        {
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
        public void Build_IntoFolder_WritesExactlyTheSixPrefabs()
        {
            var names = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
                .ToList();

            CollectionAssert.AreEquivalent(OutputNames, names);
        }

        [Test]
        public void ExistingOutputs_AfterBuild_NamesAllSix()
        {
            var existing = UiPrefabBuilder.ExistingOutputs(folder);

            CollectionAssert.AreEquivalent(OutputNames.Select(name => $"{folder}/{name}.prefab"), existing);
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
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadFrontEnd(), canvas.transform);

                var problem = instance.GetComponent<FrontEnd>().MissingSetup();

                Assert.IsNull(problem, problem);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase(typeof(SettingsOverlay), 5)]
        [TestCase(typeof(LevelsOverlay), 3)]
        [TestCase(typeof(LivesOverlay), 5)]
        public void Build_Overlay_EverySerializedReferenceIsAssigned(Type overlayType, int expectedFields)
        {
            var overlay = LoadFrontEnd().GetComponentInChildren(overlayType, true);
            Assert.IsNotNull(overlay, overlayType.Name);
            var checkedFields = 0;
            var unassigned = new List<string>();

            using (var serialized = new SerializedObject(overlay))
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

            CollectionAssert.IsEmpty(unassigned, $"{overlayType.Name}: unassigned fields");
            Assert.AreEqual(expectedFields, checkedFields, $"{overlayType.Name}: every reference field is checked");
        }

        [Test]
        public void Build_FrontEnd_HandMadePartsStayLinkedToTheirPrefabs()
        {
            var frontEnd = LoadFrontEnd();

            var topBar = SourcePath(frontEnd.GetComponentInChildren<TopBar>(true));
            var tabBar = SourcePath(frontEnd.GetComponentInChildren<TabBar>(true));
            var home = SourcePath(frontEnd.GetComponentInChildren<HomeScreen>(true));

            Assert.AreEqual($"{HandMadeFolder}/Top Bar.prefab", topBar);
            Assert.AreEqual($"{HandMadeFolder}/Tab Bar.prefab", tabBar);
            Assert.AreEqual($"{HandMadeFolder}/Home Page.prefab", home);
        }

        [Test]
        public void Build_FrontEnd_StartsOnHomeWithNoOverlay()
        {
            var root = LoadFrontEnd().transform;

            var pages = root.Find("Safe/Pages");
            var overlays = root.Find("Overlays");

            Assert.IsFalse(root.gameObject.activeSelf, "the root is saved closed");
            Assert.IsNotNull(pages, "Safe/Pages");
            Assert.IsTrue(pages.Find("Home Page").gameObject.activeSelf, "Home Page");
            Assert.IsFalse(pages.Find("Store Page").gameObject.activeSelf, "Store Page");
            Assert.IsFalse(pages.Find("Leaderboard Page").gameObject.activeSelf, "Leaderboard Page");
            Assert.IsNotNull(overlays, "Overlays sits directly under the root, outside Safe");
            Assert.AreEqual(root, overlays.parent);
            foreach (var overlayType in new[] { typeof(SettingsOverlay), typeof(LevelsOverlay), typeof(LivesOverlay) })
            {
                var overlay = root.GetComponentInChildren(overlayType, true);
                Assert.AreEqual(overlays, overlay.transform.parent, $"{overlayType.Name} is under Overlays");
                Assert.IsFalse(overlay.gameObject.activeSelf, $"{overlayType.Name} starts closed");
            }
        }

        private GameObject LoadFrontEnd()
        {
            var frontEnd = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{FrontEndName}.prefab");
            Assert.IsNotNull(frontEnd, "the built Front End");
            return frontEnd;
        }

        private static string SourcePath(Component nested)
        {
            Assert.IsNotNull(nested);
            return AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(nested.gameObject));
        }
    }
}
