using System.Collections.Generic;
using GateRush.UI;
using GateRush.UI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 47's rule that every button of the interface answers a
    /// press: every <see cref="Button"/> in every prefab under
    /// <c>Assets/Prefabs/UI</c> — the eight hand-made ones and the
    /// twenty-five the UI builder writes — has a <see cref="PressFeedback"/>
    /// on its own object, reading the UI Config.
    /// </summary>
    /// <remarks>
    /// <para>These read the project's real prefabs, so they fail until the
    /// owner runs <i>Gate Rush → Apply Hand-Made Prefab Settings</i> and then
    /// <i>Gate Rush → Build UI Prefabs</i>. The buttons created at runtime from
    /// these prefabs (the level grid, the store cards, the avatar grid)
    /// inherit their press, so they are covered here.</para>
    /// <para><b>Excluded:</b> the introduction card's two buttons, its
    /// full-screen backdrop and its close button, which
    /// <c>IntroductionCard</c> builds in code in <c>GateRush.Runtime</c> (D46)
    /// and which no prefab holds; <c>Runtime</c> cannot reach
    /// <see cref="PressFeedback"/> or the UI Config, and the owner left the
    /// card's close without a press in this pass. No other button is built
    /// at runtime.</para>
    /// </remarks>
    public class PressFeedbackCoverageTests
    {
        private const string PrefabFolder = "Assets/Prefabs/UI";

        private static IEnumerable<string> PrefabNames => new[]
        {
            // Hand-made (D78).
            "Button Close", "Button Primary", "Button Round", "Button Secondary", "Level Button", "Top Bar", "Tab Bar", "Home Page",

            // Written by the UI builder.
            "Gold Pack Card", "Store Page", "Leaderboard Row", "Leaderboard Page", "Settings Overlay", "Levels Overlay",
            "Lives Overlay", "Not Enough Gold Overlay", "Message Overlay", "Test Purchase Overlay", "Streak Marker", "Streak Bar",
            "Streak Ladder Row", "Win Streak Overlay", "Streak Prize", "Streak Reward Overlay", "Avatar Button", "Profile Overlay",
            "Front End", "Gold Packs Overlay", "Level HUD", "Joker Bar", "Level UI", "Result Overlay", "Confirm Overlay"
        };

        [TestCaseSource(nameof(PrefabNames))]
        public void Prefab_EveryButton_HasAPressReadingTheUiConfig(string name)
        {
            var path = $"{PrefabFolder}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            var config = AssetDatabase.LoadAssetAtPath<UiConfig>(UiPrefabBuilder.UiConfigPath);
            Assert.IsNotNull(config, UiPrefabBuilder.UiConfigPath);

            var missing = UiPrefabBuilder.ButtonsWithoutPress(prefab);

            Assert.IsEmpty(missing, $"{path}: these Buttons have no press: {string.Join(", ", missing)}");
            foreach (var press in prefab.GetComponentsInChildren<PressFeedback>(true))
            {
                Assert.AreEqual(config, press.Config, $"{path}: '{press.name}''s press reads {UiPrefabBuilder.UiConfigPath}");
            }
        }

        [Test]
        public void PrefabNames_CoverEveryPrefabInTheFolder()
        {
            var listed = new HashSet<string>(PrefabNames);
            var found = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                if (!listed.Contains(name))
                {
                    found.Add(assetPath);
                }
            }

            Assert.IsEmpty(found, $"a prefab the coverage does not read: {string.Join(", ", found)}");
        }
    }
}
