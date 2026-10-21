using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Platform;
using GateRush.Runtime;
using GateRush.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="TopBar"/>'s name under the avatar (Module 42): Show
    /// writes the state's name, a later Show replaces it as a refresh does,
    /// and an unassigned name label is reported as a missing part.
    /// </summary>
    /// <remarks>
    /// The bar is built from code with the fields the hand-made Top Bar
    /// wires, so these tests read no asset. The states come from
    /// <see cref="SaveFixture"/>: one save whose name has Turkish letters, and
    /// a new player's, whose name is the rules' default.
    /// </remarks>
    public class TopBarTests
    {
        private static readonly DateTime Noon = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

        private GameObject root;
        private UiConfig config;
        private AvatarCatalog catalog;
        private TopBar topBar;
        private TMP_Text nameLabel;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<UiConfig>();
            catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
            root = new GameObject("Top Bar", typeof(RectTransform));
            topBar = BuildTopBar(root, out nameLabel);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Show_State_ShowsThePlayersName()
        {
            var state = StateOf(SaveFixture.EveryField());

            topBar.Show(state, config, catalog);

            Assert.AreEqual(SaveFixture.EveryFieldName, nameLabel.text);
        }

        [Test]
        public void Show_AgainWithAnotherName_ShowsTheNewName()
        {
            topBar.Show(StateOf(SaveFixture.EveryField()), config, catalog);
            var renamed = StateOf(SaveFixture.NewPlayerSave(CompletedLevels.Of(Array.Empty<int>()), new SoundSetting()));

            topBar.Show(renamed, config, catalog);

            Assert.AreEqual(SaveFixture.Rules.DefaultPlayerName, nameLabel.text);
            Assert.AreNotEqual(SaveFixture.EveryFieldName, nameLabel.text, "the refresh replaced the earlier name");
        }

        [Test]
        public void AddMissing_NameLabelUnassigned_ReportsIt()
        {
            SetReferences(topBar, ("nameLabel", (Object)null));
            var missing = new List<string>();

            topBar.AddMissing(missing);

            Assert.That(missing, Has.Some.EqualTo("TopBar.nameLabel"), string.Join(" | ", missing));
        }

        /// <summary>The front end's state of <paramref name="save"/> at noon, with no levels and no gold packs.</summary>
        private static FrontEndState StateOf(SaveData save)
        {
            var meta = new MetaGame(save, SaveFixture.Rules, new ManualClock(Noon));
            return FrontEndState.Of(
                meta, SaveFixture.Rules, Noon, new LevelCatalog(Array.Empty<(string, int)>()), true, Array.Empty<StoreProduct>());
        }

        /// <summary>A Top Bar with every field the hand-made prefab wires, its name label returned in <paramref name="name"/>.</summary>
        private static TopBar BuildTopBar(GameObject owner, out TMP_Text name)
        {
            var avatar = UiBuilder.CreateRect("Avatar", owner.transform);
            var avatarImage = avatar.gameObject.AddComponent<Image>();
            var avatarButton = avatar.gameObject.AddComponent<Button>();
            name = AddText(avatar, "Name");

            var bar = owner.AddComponent<TopBar>();
            SetReferences(
                bar,
                ("avatarButton", avatarButton),
                ("avatarImage", avatarImage),
                ("nameLabel", name),
                ("goldLabel", AddText(owner.transform, "Gold Label")),
                ("goldPlusButton", AddButton(owner.transform, "Gold Plus")),
                ("livesCountLabel", AddText(owner.transform, "Lives Count")),
                ("livesStatusLabel", AddText(owner.transform, "Lives Status")),
                ("livesPlusButton", AddButton(owner.transform, "Lives Plus")),
                ("settingsButton", AddButton(owner.transform, "Settings")));
            return bar;
        }

        private static TMP_Text AddText(Transform parent, string name) =>
            UiBuilder.CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();

        private static Button AddButton(Transform parent, string name) =>
            UiBuilder.CreateRect(name, parent).gameObject.AddComponent<Button>();

        private static void SetReferences(Object component, params (string field, Object value)[] fields)
        {
            using (var serialized = new SerializedObject(component))
            {
                foreach (var (field, value) in fields)
                {
                    var property = serialized.FindProperty(field);
                    Assert.IsNotNull(property, $"{component.GetType().Name} has no serialized field '{field}'.");
                    property.objectReferenceValue = value;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
