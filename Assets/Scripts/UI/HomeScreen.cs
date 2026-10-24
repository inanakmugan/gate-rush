using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The Home tab (Module 35): an empty area kept for the streak bar of
    /// Module 6.3 and the large green play button reading "Level N" or "All
    /// Played". Pressing Play only asks; the bootstrap decides what follows.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        [SerializeField] private Button playButton;

        [Tooltip("The play button's label.")]
        [SerializeField] private TMP_Text playLabel;

        /// <summary>Raised when Play is pressed.</summary>
        public event Action PlayRequested;

        /// <summary>Shows the campaign level of <paramref name="state"/> on the play button.</summary>
        public void Show(FrontEndState state, UiConfig config)
        {
            playLabel.text = HomeTexts.PlayLabel(state.CampaignLevelNumber, config);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, playButton, nameof(HomeScreen), nameof(playButton));
            UiParts.AddIfMissing(missing, playLabel, nameof(HomeScreen), nameof(playLabel));
        }

        private void OnEnable()
        {
            UiParts.Listen(playButton, OnPlayClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(playButton, OnPlayClicked);
        }

        private void OnPlayClicked() => PlayRequested?.Invoke();
    }
}
