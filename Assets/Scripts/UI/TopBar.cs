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
    /// state it is given and raises an event for each button; the avatar is
    /// not pressable until Module 6.4.
    /// </summary>
    public sealed class TopBar : MonoBehaviour
    {
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

        /// <summary>Raised when the gold's "+" is pressed.</summary>
        public event Action StoreRequested;

        /// <summary>Raised when the lives' "+" is pressed.</summary>
        public event Action LivesRequested;

        /// <summary>Raised when the gear is pressed.</summary>
        public event Action SettingsRequested;

        /// <summary>Shows the gold and the lives of <paramref name="state"/>.</summary>
        public void Show(FrontEndState state, UiConfig config)
        {
            goldLabel.text = HomeTexts.Gold(state.Gold, config);
            livesCountLabel.text = HomeTexts.LivesCount(state.Lives, config);
            livesStatusLabel.text = HomeTexts.LivesStatus(state.UntilNextLife, config);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, goldLabel, nameof(TopBar), nameof(goldLabel));
            UiParts.AddIfMissing(missing, goldPlusButton, nameof(TopBar), nameof(goldPlusButton));
            UiParts.AddIfMissing(missing, livesCountLabel, nameof(TopBar), nameof(livesCountLabel));
            UiParts.AddIfMissing(missing, livesStatusLabel, nameof(TopBar), nameof(livesStatusLabel));
            UiParts.AddIfMissing(missing, livesPlusButton, nameof(TopBar), nameof(livesPlusButton));
            UiParts.AddIfMissing(missing, settingsButton, nameof(TopBar), nameof(settingsButton));
        }

        private void OnEnable()
        {
            UiParts.Listen(goldPlusButton, OnGoldPlusClicked);
            UiParts.Listen(livesPlusButton, OnLivesPlusClicked);
            UiParts.Listen(settingsButton, OnSettingsClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(goldPlusButton, OnGoldPlusClicked);
            UiParts.Unlisten(livesPlusButton, OnLivesPlusClicked);
            UiParts.Unlisten(settingsButton, OnSettingsClicked);
        }

        private void OnGoldPlusClicked() => StoreRequested?.Invoke();

        private void OnLivesPlusClicked() => LivesRequested?.Invoke();

        private void OnSettingsClicked() => SettingsRequested?.Invoke();
    }
}
