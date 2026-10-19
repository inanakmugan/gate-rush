using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The Lives overlay (Module 35, D62): the lives as "3/5", "Full" or the
    /// time to the next life, and the refill button with its price, pressable
    /// whenever the lives are not full (<see cref="FrontEndState.IsLivesFull"/>),
    /// whatever the balance: too little gold opens the Not Enough Gold prompt
    /// (Module 36). It stays open as lives come back; the player closes it.
    /// </summary>
    /// <remarks>
    /// A button that cannot be pressed shows the grey sprite through its
    /// Sprite Swap transition; the label's colour follows from
    /// <see cref="UiConfig"/>, so it stays readable on grey.
    /// </remarks>
    public sealed class LivesOverlay : MonoBehaviour
    {
        [Tooltip("The red close button in the panel's corner.")]
        [SerializeField] private Button closeButton;

        [Tooltip("The lives of the cap, as '3/5'.")]
        [SerializeField] private TMP_Text livesLabel;

        [Tooltip("'Full', or the time to the next life.")]
        [SerializeField] private TMP_Text statusLabel;

        [SerializeField] private Button refillButton;

        [Tooltip("The refill button's label, with the price.")]
        [SerializeField] private TMP_Text refillLabel;

        /// <summary>Raised when the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Raised when the refill button is pressed; it is pressable whenever the lives are not full.</summary>
        public event Action RefillRequested;

        /// <summary>Shows the lives and the refill of <paramref name="state"/>.</summary>
        public void Show(FrontEndState state, UiConfig config)
        {
            livesLabel.text = HomeTexts.LivesFraction(state.Lives, state.LifeCap, config);
            statusLabel.text = HomeTexts.LivesStatus(state.UntilNextLife, config);
            refillLabel.text = HomeTexts.Refill(state.RefillPrice, config);
            var canPress = !state.IsLivesFull;
            refillButton.interactable = canPress;
            refillLabel.color = canPress ? config.ButtonLabelColor : config.DisabledButtonLabelColor;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(LivesOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, livesLabel, nameof(LivesOverlay), nameof(livesLabel));
            UiParts.AddIfMissing(missing, statusLabel, nameof(LivesOverlay), nameof(statusLabel));
            UiParts.AddIfMissing(missing, refillButton, nameof(LivesOverlay), nameof(refillButton));
            UiParts.AddIfMissing(missing, refillLabel, nameof(LivesOverlay), nameof(refillLabel));
        }

        private void OnEnable()
        {
            UiParts.Listen(closeButton, OnCloseClicked);
            UiParts.Listen(refillButton, OnRefillClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(closeButton, OnCloseClicked);
            UiParts.Unlisten(refillButton, OnRefillClicked);
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();

        private void OnRefillClicked() => RefillRequested?.Invoke();
    }
}
