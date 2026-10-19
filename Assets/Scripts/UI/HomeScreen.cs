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
    /// Module 6.3, the large green play button reading "Level N" or "All
    /// Played", and one notice line under it. Pressing Play only asks; the
    /// bootstrap decides what follows.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        [SerializeField] private Button playButton;

        [Tooltip("The play button's label.")]
        [SerializeField] private TMP_Text playLabel;

        [Tooltip("The line under the play button, empty until a notice is shown.")]
        [SerializeField] private TMP_Text noticeLabel;

        /// <summary>Raised when Play is pressed.</summary>
        public event Action PlayRequested;

        /// <summary>Shows the campaign level of <paramref name="state"/> on the play button.</summary>
        public void Show(FrontEndState state, UiConfig config)
        {
            playLabel.text = HomeTexts.PlayLabel(state.CampaignLevelNumber, config);
        }

        /// <summary>Shows <paramref name="text"/> under the play button.</summary>
        public void ShowNotice(string text)
        {
            noticeLabel.text = text;
        }

        /// <summary>Empties the notice line.</summary>
        public void ClearNotice()
        {
            noticeLabel.text = string.Empty;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, playButton, nameof(HomeScreen), nameof(playButton));
            UiParts.AddIfMissing(missing, playLabel, nameof(HomeScreen), nameof(playLabel));
            UiParts.AddIfMissing(missing, noticeLabel, nameof(HomeScreen), nameof(noticeLabel));
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
