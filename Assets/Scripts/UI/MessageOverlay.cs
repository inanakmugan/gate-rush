using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The message prompt (Module 36): a title, a text and an OK button,
    /// which closes it, as Back does. Used for a purchase that failed, at its
    /// short size, and for the privacy text (Module 49), at its tall size.
    /// </summary>
    /// <remarks>
    /// <b>Size.</b> The panel and the text's box take the sizes the UI builder
    /// wrote: the short ones for <see cref="Show"/>, the tall ones for
    /// <see cref="ShowLong"/>. The text keeps its top and its width; only its
    /// height changes with the panel's, so OK stays at the panel's foot. The
    /// text shrinks, within the font sizes the builder set, when it would not
    /// fit its box.
    /// </remarks>
    public sealed class MessageOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text textLabel;

        [Tooltip("The one button: closes the prompt.")]
        [SerializeField] private Button okButton;

        [Tooltip("The grey panel, sized by the message's length.")]
        [SerializeField] private RectTransform panel;

        [Tooltip("The panel's size for a short message, in canvas units; written by the UI builder.")]
        [SerializeField] private Vector2 shortPanelSize;

        [Tooltip("The text box's height for a short message, in canvas units; written by the UI builder.")]
        [SerializeField] private float shortTextHeight;

        [Tooltip("The panel's size for a long message such as the privacy text, in canvas units; written by the UI builder.")]
        [SerializeField] private Vector2 longPanelSize;

        [Tooltip("The text box's height for a long message, in canvas units; written by the UI builder.")]
        [SerializeField] private float longTextHeight;

        /// <summary>Raised when OK is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Shows <paramref name="title"/> and <paramref name="text"/>, a short message, at the short size.</summary>
        public void Show(string title, string text)
        {
            ShowSized(title, text, shortPanelSize, shortTextHeight);
        }

        /// <summary>Shows <paramref name="title"/> and <paramref name="text"/>, a long message, at the tall size.</summary>
        public void ShowLong(string title, string text)
        {
            ShowSized(title, text, longPanelSize, longTextHeight);
        }

        /// <summary>Adds each unassigned part, and each size the builder has not written, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, titleLabel, nameof(MessageOverlay), nameof(titleLabel));
            UiParts.AddIfMissing(missing, textLabel, nameof(MessageOverlay), nameof(textLabel));
            UiParts.AddIfMissing(missing, okButton, nameof(MessageOverlay), nameof(okButton));
            UiParts.AddIfMissing(missing, panel, nameof(MessageOverlay), nameof(panel));

            // A size the builder has not written yet: a prefab built before the
            // sizes existed would otherwise open as an empty panel.
            if (!(shortPanelSize.x > 0f && shortPanelSize.y > 0f && shortTextHeight > 0f))
            {
                missing.Add($"{nameof(MessageOverlay)}.{nameof(shortPanelSize)} and {nameof(shortTextHeight)}");
            }

            if (!(longPanelSize.x > 0f && longPanelSize.y > 0f && longTextHeight > 0f))
            {
                missing.Add($"{nameof(MessageOverlay)}.{nameof(longPanelSize)} and {nameof(longTextHeight)}");
            }
        }

        private void ShowSized(string title, string text, Vector2 panelSize, float textHeight)
        {
            panel.sizeDelta = panelSize;
            var textRect = textLabel.rectTransform;
            textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, textHeight);
            titleLabel.text = title;
            textLabel.text = text;
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
