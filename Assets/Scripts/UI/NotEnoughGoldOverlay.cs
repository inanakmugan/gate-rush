using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The Not Enough Gold prompt (Module 36, D72): the coin stack, the gold
    /// missing as the bootstrap worked it out, a Get Gold button that leads
    /// to the Store's gold packs, and the red close button.
    /// </summary>
    public sealed class NotEnoughGoldOverlay : MonoBehaviour
    {
        [Tooltip("The red close button in the panel's corner.")]
        [SerializeField] private Button closeButton;

        [Tooltip("'You need N more gold.'")]
        [SerializeField] private TMP_Text shortfallLabel;

        [Tooltip("Selects the Store tab, closing every overlay.")]
        [SerializeField] private Button getGoldButton;

        /// <summary>Raised when the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Raised when Get Gold is pressed.</summary>
        public event Action GetGoldRequested;

        /// <summary>Shows <paramref name="shortfall"/>, the gold missing.</summary>
        public void Show(int shortfall, UiConfig config)
        {
            shortfallLabel.text = StoreTexts.Shortfall(shortfall, config);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(NotEnoughGoldOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, shortfallLabel, nameof(NotEnoughGoldOverlay), nameof(shortfallLabel));
            UiParts.AddIfMissing(missing, getGoldButton, nameof(NotEnoughGoldOverlay), nameof(getGoldButton));
        }

        private void OnEnable()
        {
            UiParts.Listen(closeButton, OnCloseClicked);
            UiParts.Listen(getGoldButton, OnGetGoldClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(closeButton, OnCloseClicked);
            UiParts.Unlisten(getGoldButton, OnGetGoldClicked);
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();

        private void OnGetGoldClicked() => GetGoldRequested?.Invoke();
    }
}
