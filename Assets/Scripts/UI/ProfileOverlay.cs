using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The Profile overlay (Module 38, D71): the chosen avatar, large; the
    /// name field with its hint and the refusal line; a scrolling grid of
    /// every avatar of the <see cref="AvatarCatalog"/>, the chosen one marked;
    /// and four statistics read from the save as it is. It decides nothing:
    /// a finished edit and a pressed avatar only ask.
    /// </summary>
    /// <remarks>
    /// <para><b>The name field.</b> TextMeshPro's input field, with its own
    /// keyboard on a phone. While the player edits, a refresh — every second
    /// while a life comes back — never overwrites the text. When the edit
    /// ends, by Enter or by the field losing focus, text that differs from the
    /// name shown is raised as typed; the bootstrap applies the name rule and
    /// either refreshes with the accepted name or calls
    /// <see cref="ShowNameRefused"/>. The refusal line stays until editing
    /// starts again or Profile is opened again.</para>
    /// <para><b>The grid.</b> Its cells are created from
    /// <c>avatarButtonPrefab</c> under <c>grid</c> the first time Profile is
    /// shown, and again only when the catalog's count changes; every later
    /// show updates the sprites and the mark in place. The mark follows
    /// <see cref="AvatarCatalog.ShownId"/>, the avatar actually shown.</para>
    /// </remarks>
    public sealed class ProfileOverlay : MonoBehaviour
    {
        [Tooltip("The red close button in the panel's corner.")]
        [SerializeField] private Button closeButton;

        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("The chosen avatar, large.")]
        [SerializeField] private Image avatarImage;

        [SerializeField] private TMP_InputField nameField;

        [Tooltip("The edit hint under the name field.")]
        [SerializeField] private TMP_Text hintLabel;

        [Tooltip("The line under the hint; empty unless a name was refused.")]
        [SerializeField] private TMP_Text refusalLabel;

        [Tooltip("The scroll view's content, with the Grid Layout Group the avatar cells are created under.")]
        [SerializeField] private RectTransform grid;

        [Tooltip("The Avatar Button prefab asset.")]
        [SerializeField] private AvatarButton avatarButtonPrefab;

        [SerializeField] private TMP_Text levelsCompletedLabel;
        [SerializeField] private TMP_Text levelsCompletedValue;
        [SerializeField] private TMP_Text campaignLevelLabel;
        [SerializeField] private TMP_Text campaignLevelValue;
        [SerializeField] private TMP_Text bestStreakLabel;
        [SerializeField] private TMP_Text bestStreakValue;
        [SerializeField] private TMP_Text lifetimeGoldLabel;
        [SerializeField] private TMP_Text lifetimeGoldValue;

        private readonly List<AvatarButton> cells = new List<AvatarButton>();

        // The catalog the cells show, index for index; a press reads its id here.
        private AvatarCatalog shownCatalog;

        // The name last shown, which a refused edit puts back.
        private string shownName = string.Empty;

        // True from the field's selection until its edit ends; a refresh then
        // leaves the text alone.
        private bool isEditing;

        /// <summary>Raised when the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Raised with the field's text, as typed, when an edit ends with text other than the name shown.</summary>
        public event Action<string> NameChangeRequested;

        /// <summary>Raised with an avatar's id when its cell is pressed.</summary>
        public event Action<string> AvatarChangeRequested;

        /// <summary>
        /// Shows the name, the avatar and the statistics of
        /// <paramref name="state"/>, the texts of <paramref name="config"/>,
        /// and one cell per avatar of <paramref name="catalog"/>, creating the
        /// cells when their count differs from the catalog's.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="catalog"/> is null.</exception>
        public void Show(FrontEndState state, UiConfig config, AvatarCatalog catalog)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            titleLabel.text = config.ProfileTitle;
            hintLabel.text = config.NameEditHint;

            shownName = state.PlayerName ?? string.Empty;
            if (!isEditing)
            {
                nameField.SetTextWithoutNotify(shownName);
            }

            avatarImage.sprite = catalog.IconOf(state.AvatarId);
            ShowCells(state.AvatarId, config, catalog);

            levelsCompletedLabel.text = config.LevelsCompletedLabel;
            levelsCompletedValue.text = ProfileTexts.Statistic(state.LevelsCompleted, config);
            campaignLevelLabel.text = config.CampaignLevelLabel;
            campaignLevelValue.text = ProfileTexts.CampaignLevel(state.CampaignLevelNumber, config);
            bestStreakLabel.text = config.BestStreakLabel;
            bestStreakValue.text = ProfileTexts.Statistic(state.BestStreak, config);
            lifetimeGoldLabel.text = config.LifetimeGoldLabel;
            lifetimeGoldValue.text = ProfileTexts.Statistic(state.LifetimeGold, config);
        }

        /// <summary>Puts the name last shown back in the field and shows the refusal line.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public void ShowNameRefused(UiConfig config)
        {
            var text = ProfileTexts.NameRefused(config);
            nameField.SetTextWithoutNotify(shownName);
            refusalLabel.text = text;
        }

        /// <summary>Empties the refusal line, for Profile being opened again.</summary>
        public void ClearRefusal()
        {
            refusalLabel.text = string.Empty;
        }

        /// <summary>Adds each unassigned part, the avatar button prefab's own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(ProfileOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, titleLabel, nameof(ProfileOverlay), nameof(titleLabel));
            UiParts.AddIfMissing(missing, avatarImage, nameof(ProfileOverlay), nameof(avatarImage));
            UiParts.AddIfMissing(missing, nameField, nameof(ProfileOverlay), nameof(nameField));
            UiParts.AddIfMissing(missing, hintLabel, nameof(ProfileOverlay), nameof(hintLabel));
            UiParts.AddIfMissing(missing, refusalLabel, nameof(ProfileOverlay), nameof(refusalLabel));
            UiParts.AddIfMissing(missing, grid, nameof(ProfileOverlay), nameof(grid));
            UiParts.AddIfMissing(missing, avatarButtonPrefab, nameof(ProfileOverlay), nameof(avatarButtonPrefab));
            UiParts.AddIfMissing(missing, levelsCompletedLabel, nameof(ProfileOverlay), nameof(levelsCompletedLabel));
            UiParts.AddIfMissing(missing, levelsCompletedValue, nameof(ProfileOverlay), nameof(levelsCompletedValue));
            UiParts.AddIfMissing(missing, campaignLevelLabel, nameof(ProfileOverlay), nameof(campaignLevelLabel));
            UiParts.AddIfMissing(missing, campaignLevelValue, nameof(ProfileOverlay), nameof(campaignLevelValue));
            UiParts.AddIfMissing(missing, bestStreakLabel, nameof(ProfileOverlay), nameof(bestStreakLabel));
            UiParts.AddIfMissing(missing, bestStreakValue, nameof(ProfileOverlay), nameof(bestStreakValue));
            UiParts.AddIfMissing(missing, lifetimeGoldLabel, nameof(ProfileOverlay), nameof(lifetimeGoldLabel));
            UiParts.AddIfMissing(missing, lifetimeGoldValue, nameof(ProfileOverlay), nameof(lifetimeGoldValue));
            if (avatarButtonPrefab == null)
            {
                return;
            }

            avatarButtonPrefab.AddMissing(missing);
            UiParts.AddMissingTints(missing, avatarButtonPrefab);
        }

        /// <summary>One cell per avatar of <paramref name="catalog"/>, the one shown for <paramref name="avatarId"/> marked.</summary>
        private void ShowCells(string avatarId, UiConfig config, AvatarCatalog catalog)
        {
            shownCatalog = catalog;
            if (cells.Count != catalog.Count)
            {
                RebuildCells(catalog.Count, config);
            }

            var shownId = catalog.ShownId(avatarId);
            for (var i = 0; i < cells.Count; i++)
            {
                cells[i].Show(catalog.SpriteAt(i), string.Equals(catalog.IdAt(i), shownId, StringComparison.Ordinal));
            }
        }

        private void RebuildCells(int count, UiConfig config)
        {
            DestroyCells();
            for (var i = 0; i < count; i++)
            {
                var cell = Instantiate(avatarButtonPrefab, grid);
                UiTint.ApplyAll(cell, config);
                cell.Pressed += OnCellPressed;
                cells.Add(cell);
            }
        }

        private void DestroyCells()
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (ReferenceEquals(cell, null))
                {
                    continue;
                }

                cell.Pressed -= OnCellPressed;
                if (cell != null)
                {
                    Destroy(cell.gameObject);
                }
            }

            cells.Clear();
        }

        private void OnEnable()
        {
            UiParts.Listen(closeButton, OnCloseClicked);
            if (nameField != null)
            {
                nameField.onSelect.AddListener(OnNameSelected);
                nameField.onEndEdit.AddListener(OnNameEditEnded);
            }
        }

        private void OnDisable()
        {
            UiParts.Unlisten(closeButton, OnCloseClicked);
            if (nameField != null)
            {
                nameField.onSelect.RemoveListener(OnNameSelected);
                nameField.onEndEdit.RemoveListener(OnNameEditEnded);
            }

            isEditing = false;
        }

        private void OnDestroy()
        {
            DestroyCells();
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();

        private void OnNameSelected(string text)
        {
            isEditing = true;
            ClearRefusal();
        }

        private void OnNameEditEnded(string text)
        {
            isEditing = false;
            if (!string.Equals(text, shownName, StringComparison.Ordinal))
            {
                NameChangeRequested?.Invoke(text);
            }
        }

        private void OnCellPressed(AvatarButton cell)
        {
            var index = cells.IndexOf(cell);
            if (shownCatalog != null && index >= 0 && index < shownCatalog.Count)
            {
                AvatarChangeRequested?.Invoke(shownCatalog.IdAt(index));
            }
        }
    }
}
