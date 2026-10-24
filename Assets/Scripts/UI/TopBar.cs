using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The bar over every tab (Module 35): the avatar, the gold with its "+",
    /// the lives with their status and "+", and the settings gear. Shows the
    /// state it is given and raises an event for each button; the avatar
    /// shows the chosen one and opens Profile (Module 38), with the player's
    /// name under it (Module 42).
    /// </summary>
    public sealed class TopBar : MonoBehaviour
    {
        [Tooltip("The Button on the Avatar image: opens Profile. The image must be a raycast target.")]
        [SerializeField] private Button avatarButton;

        [Tooltip("The Avatar image: shows the chosen avatar.")]
        [SerializeField] private Image avatarImage;

        [Tooltip("The player's name under the avatar; cut with an ellipsis within the avatar's column.")]
        [SerializeField] private TMP_Text nameLabel;

        [SerializeField] private TMP_Text goldLabel;

        [Tooltip("The '+' beside the gold: selects the Store tab.")]
        [SerializeField] private Button goldPlusButton;

        [Tooltip("The number inside the heart.")]
        [SerializeField] private TMP_Text livesCountLabel;

        [Tooltip("'Full', or the time to the next life.")]
        [SerializeField] private TMP_Text livesStatusLabel;

        [Tooltip("The '+' beside the lives: opens the Lives overlay.")]
        [SerializeField] private Button livesPlusButton;

        [Tooltip("The gear: opens Settings.")]
        [SerializeField] private Button settingsButton;

        // The gold pill a coin flight lands on (Module 47).
        private GoldPill goldPill;

        /// <summary>Raised when the gold's "+" is pressed.</summary>
        public event Action StoreRequested;

        /// <summary>The gold label's counter (Module 47): what <see cref="Show"/> feeds, and what a coin flight holds and adds to.</summary>
        public GoldCounter GoldCounter => Gold.Counter;

        /// <summary>The gold pill's own canvas, which a coin flight lifts over a scrim.</summary>
        public Canvas GoldCanvas => Gold.Canvas;

        /// <summary>Where coins land: the gold pill.</summary>
        public RectTransform GoldTarget => Gold.Rect;

        private GoldPill Gold => goldPill ?? (goldPill = new GoldPill(goldLabel));

        /// <summary>Raised when the lives' "+" is pressed.</summary>
        public event Action LivesRequested;

        /// <summary>Raised when the gear is pressed.</summary>
        public event Action SettingsRequested;

        /// <summary>Raised when the avatar is pressed.</summary>
        public event Action ProfileRequested;

        /// <summary>
        /// Shows the avatar, the name, the gold and the lives of
        /// <paramref name="state"/>; the avatar as <paramref name="catalog"/>
        /// shows its id. The front end calls this on every refresh, so a new
        /// name shows at once.
        /// </summary>
        public void Show(FrontEndState state, UiConfig config, AvatarCatalog catalog)
        {
            avatarImage.sprite = catalog.IconOf(state.AvatarId);

            // Only a default state has no name; the name rule gives every save one.
            nameLabel.text = state.PlayerName ?? string.Empty;
            Gold.Show(state.Gold, config);
            livesCountLabel.text = HomeTexts.LivesCount(state.Lives, config);
            livesStatusLabel.text = HomeTexts.LivesStatus(state.UntilNextLife, config);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, avatarButton, nameof(TopBar), nameof(avatarButton));
            UiParts.AddIfMissing(missing, avatarImage, nameof(TopBar), nameof(avatarImage));
            UiParts.AddIfMissing(missing, nameLabel, nameof(TopBar), nameof(nameLabel));
            UiParts.AddIfMissing(missing, goldLabel, nameof(TopBar), nameof(goldLabel));
            UiParts.AddIfMissing(missing, goldPlusButton, nameof(TopBar), nameof(goldPlusButton));
            UiParts.AddIfMissing(missing, livesCountLabel, nameof(TopBar), nameof(livesCountLabel));
            UiParts.AddIfMissing(missing, livesStatusLabel, nameof(TopBar), nameof(livesStatusLabel));
            UiParts.AddIfMissing(missing, livesPlusButton, nameof(TopBar), nameof(livesPlusButton));
            UiParts.AddIfMissing(missing, settingsButton, nameof(TopBar), nameof(settingsButton));
        }

        private void OnEnable()
        {
            UiParts.Listen(avatarButton, OnAvatarClicked);
            UiParts.Listen(goldPlusButton, OnGoldPlusClicked);
            UiParts.Listen(livesPlusButton, OnLivesPlusClicked);
            UiParts.Listen(settingsButton, OnSettingsClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(avatarButton, OnAvatarClicked);
            UiParts.Unlisten(goldPlusButton, OnGoldPlusClicked);
            UiParts.Unlisten(livesPlusButton, OnLivesPlusClicked);
            UiParts.Unlisten(settingsButton, OnSettingsClicked);
            goldPill?.Rest();
        }

        private void OnDestroy()
        {
            goldPill?.Kill();
        }

        private void OnAvatarClicked() => ProfileRequested?.Invoke();

        private void OnGoldPlusClicked() => StoreRequested?.Invoke();

        private void OnLivesPlusClicked() => LivesRequested?.Invoke();

        private void OnSettingsClicked() => SettingsRequested?.Invoke();
    }
}
