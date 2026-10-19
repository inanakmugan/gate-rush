using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The message prompt (Module 36): a title, one line and an OK button,
    /// which closes it, as Back does. Used for a purchase that failed.
    /// </summary>
    public sealed class MessageOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text textLabel;

        [Tooltip("The one button: closes the prompt.")]
        [SerializeField] private Button okButton;

        /// <summary>Raised when OK is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Shows <paramref name="title"/> and <paramref name="text"/>.</summary>
        public void Show(string title, string text)
        {
            titleLabel.text = title;
            textLabel.text = text;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, titleLabel, nameof(MessageOverlay), nameof(titleLabel));
            UiParts.AddIfMissing(missing, textLabel, nameof(MessageOverlay), nameof(textLabel));
            UiParts.AddIfMissing(missing, okButton, nameof(MessageOverlay), nameof(okButton));
        }

        private void OnEnable()
        {
            UiParts.Listen(okButton, OnOkClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(okButton, OnOkClicked);
        }

        private void OnOkClicked() => CloseRequested?.Invoke();
    }
}
