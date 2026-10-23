using System.Linq;
using GateRush.Platform;
using GateRush.Runtime;
using GateRush.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 39's <see cref="LeaderboardScreen"/> and
    /// <see cref="LeaderboardRow"/>, driven through
    /// <see cref="StubLeaderboardService"/> as a real backend would drive
    /// them: Loading hides the list and Retry; a failed fetch shows its line
    /// and Retry; a simulated table shows the note and one row per entry,
    /// and one that is not simulated hides the note; only the player's row
    /// is in the highlight colour; and the first three ranks carry their own
    /// medal sprite, untinted (Module 45), while the others have none.
    /// </summary>
    /// <remarks>
    /// The screen is built from code with the fields the builder wires, so
    /// these tests read no asset. Each shows one table only: replacing rows
    /// destroys them, which Edit Mode allows only immediately.
    /// </remarks>
    public class LeaderboardScreenTests
    {
        private const int DemoCount = 20;
        private const int PlayerScore = 750;
        private const int MedalRanks = 3;

        private GameObject root;
        private GameObject rowTemplate;
        private UiConfig config;
        private AvatarCatalog catalog;
        private StubLeaderboardService service;
        private LeaderboardScreen screen;
        private ScrollRect scrollRect;
        private TMP_Text noteLabel;
        private TMP_Text statusLabel;
        private Button retryButton;
        private RectTransform rows;
        private Texture2D medalTexture;

        // The gold, silver and bronze medals, by rank.
        private Sprite[] medalSprites;

        [SetUp]
        public void SetUp()
        {
            medalTexture = new Texture2D(1, 1);
            medalSprites = Enumerable.Range(0, MedalRanks)
                .Select(_ => Sprite.Create(medalTexture, new Rect(0f, 0f, 1f, 1f), Vector2.zero))
                .ToArray();
            config = ScriptableObject.CreateInstance<UiConfig>();
            catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
            service = new StubLeaderboardService();

            root = new GameObject("Leaderboard Page", typeof(RectTransform));
            var scroll = UiBuilder.CreateRect("Scroll View", root.transform);
            var viewport = UiBuilder.CreateRect("Viewport", scroll);
            rows = UiBuilder.CreateRect("Content", viewport);
            rows.gameObject.AddComponent<VerticalLayoutGroup>();
            scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = rows;
            noteLabel = AddText(root.transform, "Note");
            statusLabel = AddText(root.transform, "Status");
            var retry = UiBuilder.CreateRect("Retry", root.transform);
            retryButton = retry.gameObject.AddComponent<Button>();
            var retryLabel = AddText(retry, "Label");

            screen = root.AddComponent<LeaderboardScreen>();
            SetReferences(
                screen,
                ("scrollRect", scrollRect),
                ("noteLabel", noteLabel),
                ("statusLabel", statusLabel),
                ("retryButton", retryButton),
                ("retryLabel", retryLabel),
                ("rows", rows),
                ("rowPrefab", BuildRowTemplate()));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(rowTemplate);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(catalog);
            foreach (var sprite in medalSprites)
            {
                Object.DestroyImmediate(sprite);
            }

            Object.DestroyImmediate(medalTexture);
        }

        [Test]
        public void ShowLoading_WhileAFetchIsPending_ShowsLoadingAlone()
        {
            Request();

            Assert.IsTrue(service.IsBusy, "the stub holds the fetch");
            Assert.IsTrue(statusLabel.gameObject.activeSelf);
            Assert.AreEqual(config.LeaderboardLoadingText, statusLabel.text);
            Assert.IsFalse(scrollRect.gameObject.activeSelf, "the list is hidden");
            Assert.IsFalse(retryButton.gameObject.activeSelf, "Retry is hidden");
        }

        [Test]
        public void Show_FailedResult_ShowsTheFailureLineAndRetry()
        {
            Request();

            service.Answer(LeaderboardResult.Failed(false));

            Assert.IsTrue(statusLabel.gameObject.activeSelf);
            Assert.AreEqual(config.LeaderboardFailedText, statusLabel.text);
            Assert.IsTrue(retryButton.gameObject.activeSelf);
            Assert.IsFalse(scrollRect.gameObject.activeSelf, "the list is hidden");
        }

        [Test]
        public void Show_SimulatedTable_ShowsTheNoteAndOneRowPerEntry()
        {
            Request();

            service.Answer(LeaderboardResult.Loaded(Table(), true));

            Assert.IsTrue(noteLabel.gameObject.activeSelf);
            Assert.AreEqual(config.LeaderboardSimulatedNote, noteLabel.text);
            Assert.AreEqual(DemoCount + 1, rows.childCount);
            Assert.IsTrue(scrollRect.gameObject.activeSelf);
            Assert.IsFalse(statusLabel.gameObject.activeSelf, "no status over a table");
            Assert.IsFalse(retryButton.gameObject.activeSelf, "no Retry over a table");
        }

        [Test]
        public void Show_TableNotSimulated_HidesTheNote()
        {
            Request();

            service.Answer(LeaderboardResult.Loaded(Table(), false));

            Assert.IsFalse(noteLabel.gameObject.activeSelf);
        }

        [Test]
        public void Show_Table_DrawsOnlyThePlayersRowInTheHighlightColour()
        {
            Request();

            service.Answer(LeaderboardResult.Loaded(Table(), true));

            var shown = ShownRows();
            var highlighted = shown.Where(row => Part<Image>(row, "background").color == config.LeaderboardHighlightColor).ToList();
            Assert.AreEqual(1, highlighted.Count);
            Assert.AreEqual("You", Part<TMP_Text>(highlighted[0], "nameLabel").text);
            Assert.IsTrue(
                shown.Except(highlighted).All(row => Part<Image>(row, "background").color == config.LeaderboardRowColor),
                "every other row is in the row colour");
        }

        [Test]
        public void Show_Table_TheFirstThreeRanksCarryTheirOwnMedalUntinted()
        {
            Request();

            service.Answer(LeaderboardResult.Loaded(Table(), true));

            var shown = ShownRows();
            for (var i = 0; i < shown.Length; i++)
            {
                var medal = Part<Image>(shown[i], "medal");
                var rank = i + 1;
                Assert.AreEqual(LeaderboardTexts.Rank(rank, config), Part<TMP_Text>(shown[i], "rankLabel").text);
                if (rank <= MedalRanks)
                {
                    Assert.IsTrue(medal.gameObject.activeSelf, $"rank {rank} has a medal");
                    Assert.AreEqual(medalSprites[i], medal.sprite, $"rank {rank}'s own medal");
                    Assert.AreEqual(Color.white, medal.color, $"rank {rank}'s medal is drawn in its own colours");
                }
                else
                {
                    Assert.IsFalse(medal.gameObject.activeSelf, $"rank {rank} has no medal");
                }
            }
        }

        /// <summary>What the front end does on a selection: Loading, then a fetch whose answer is shown.</summary>
        private void Request()
        {
            screen.ShowLoading(config);
            service.Fetch(new LeaderboardPlayer("You", "bear", PlayerScore), result => screen.Show(result, config, catalog));
        }

        private static LeaderboardEntry[] Table()
        {
            var demo = Enumerable.Range(0, DemoCount)
                .Select(i => new LeaderboardPlayer($"Demo {i}", "cow", (i + 1) * 50))
                .ToArray();
            return LeaderboardRanking.Rank(demo, new LeaderboardPlayer("You", "bear", PlayerScore)).ToArray();
        }

        private LeaderboardRow[] ShownRows() =>
            Enumerable.Range(0, rows.childCount).Select(i => rows.GetChild(i).GetComponent<LeaderboardRow>()).ToArray();

        /// <summary>A row with every part the builder gives it, outside the page, to instantiate from.</summary>
        private LeaderboardRow BuildRowTemplate()
        {
            rowTemplate = new GameObject("Leaderboard Row", typeof(RectTransform));
            var background = rowTemplate.AddComponent<Image>();
            var medal = UiBuilder.CreateRect("Medal", rowTemplate.transform).gameObject.AddComponent<Image>();
            var avatar = UiBuilder.CreateRect("Avatar", rowTemplate.transform).gameObject.AddComponent<Image>();
            var row = rowTemplate.AddComponent<LeaderboardRow>();
            SetReferences(
                row,
                ("background", background),
                ("rankLabel", AddText(rowTemplate.transform, "Rank")),
                ("medal", medal),
                ("goldMedal", medalSprites[0]),
                ("silverMedal", medalSprites[1]),
                ("bronzeMedal", medalSprites[2]),
                ("avatar", avatar),
                ("nameLabel", AddText(rowTemplate.transform, "Name")),
                ("scoreLabel", AddText(rowTemplate.transform, "Score")));
            return row;
        }

        private static TMP_Text AddText(Transform parent, string name) =>
            UiBuilder.CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();

        private static T Part<T>(LeaderboardRow row, string field) where T : Object
        {
            using (var serialized = new SerializedObject(row))
            {
                return (T)serialized.FindProperty(field).objectReferenceValue;
            }
        }

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
