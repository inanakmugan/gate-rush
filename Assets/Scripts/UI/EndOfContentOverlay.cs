using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The End of Content overlay (Module 49): shown on Home once the campaign
    /// is finished, with the title, one line, a Restart button that plays
    /// level 1 in free play with progress kept, and the red close button. The
    /// texts are the UI Config's; pressing a button only asks.
    /// </summary>
    public sealed class EndOfContentOverlay : MonoBehaviour
    {
        [Tooltip("The red close button in the panel's corner.")]
        [SerializeField] private Button closeButton;

        [Tooltip("'You finished every level!', from the UI Config.")]
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("'New levels are coming soon.', from the UI Config.")]
        [SerializeField] private TMP_Text lineLabel;

        [Tooltip("Plays level 1 in free play (D58); nothing in the save changes.")]
        [SerializeField] private Button restartButton;

        [Tooltip("The Restart button's label, from the UI Config.")]
        [SerializeField] private TMP_Text restartLabel;

        /// <summary>Raised when the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Raised when Restart is pressed.</summary>
        public event Action RestartRequested;

        /// <summary>Shows the title, the line and the Restart label of <paramref name="config"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public void Show(UiConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            titleLabel.text = config.EndOfContentTitle;
            lineLabel.text = config.EndOfContentLine;
            restartLabel.text = config.EndOfContentRestartLabel;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(EndOfContentOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, titleLabel, nameof(EndOfContentOverlay), nameof(titleLabel));
            UiParts.AddIfMissing(missing, lineLabel, nameof(EndOfContentOverlay), nameof(lineLabel));
            UiParts.AddIfMissing(missing, restartButton, nameof(EndOfContentOverlay), nameof(restartButton));
            UiParts.AddIfMissing(missing, restartLabel, nameof(EndOfContentOverlay), nameof(restartLabel));
        }

        private void OnEnable()
        {
            UiParts.Listen(closeButton, OnCloseClicked);
            UiParts.Listen(restartButton, OnRestartClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(closeButton, OnCloseClicked);
            UiParts.Unlisten(restartButton, OnRestartClicked);
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();

        private void OnRestartClicked() => RestartRequested?.Invoke();
    }
}
