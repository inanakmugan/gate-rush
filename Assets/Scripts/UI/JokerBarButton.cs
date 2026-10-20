using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One joker's button on the level's joker bar (Module 40): its icon, a
    /// badge with the count or the price beside the coin, greyed while it
    /// cannot be pressed, and drawn raised with a ring around it while its
    /// joker is selected (D67). Shows the <see cref="JokerButton"/>
    /// <see cref="JokerButtons"/> decided and raises <see cref="Pressed"/>;
    /// what a press does is the bootstrap's.
    /// </summary>
    public sealed class JokerBarButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("Holds the ring and the button; raised by Selected Raise while the joker is selected.")]
        [SerializeField] private RectTransform lift;

        [Tooltip("The ring around the button, shown while the joker is selected.")]
        [SerializeField] private GameObject ring;

        [Tooltip("The joker's icon: the Icon Color while pressable, the Disabled Button Label Color while greyed.")]
        [SerializeField] private Image icon;

        [Tooltip("The coin beside a price; hidden for a count.")]
        [SerializeField] private GameObject coin;

        [SerializeField] private TMP_Text badgeLabel;

        [Tooltip("How far the button rises while selected, in canvas units; written by the UI builder.")]
        [SerializeField] private float selectedRaise;

        /// <summary>Raised when the button is pressed.</summary>
        public event Action Pressed;

        /// <summary>
        /// Shows <paramref name="state"/>: the badge's text, the coin for a
        /// price, the price in <see cref="UiConfig.ShortPriceColor"/> while the
        /// balance is short of it, the button greyed while it cannot be
        /// pressed, and raised with its ring while <paramref name="isSelected"/>.
        /// </summary>
        public void Show(JokerButton state, bool isSelected, UiConfig config)
        {
            badgeLabel.text = LevelHudTexts.JokerBadge(state, config);
            badgeLabel.color = state.IsShortOfGold ? config.ShortPriceColor : config.JokerBadgeTextColor;
            coin.SetActive(state.Badge == JokerBadge.Price);
            button.interactable = state.IsInteractable;
            icon.color = state.IsInteractable ? config.IconColor : config.DisabledButtonLabelColor;
            ring.SetActive(isSelected);
            lift.anchoredPosition = new Vector2(0f, isSelected ? selectedRaise : 0f);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, button, nameof(JokerBarButton), nameof(button));
            UiParts.AddIfMissing(missing, lift, nameof(JokerBarButton), nameof(lift));
            UiParts.AddIfMissing(missing, ring, nameof(JokerBarButton), nameof(ring));
            UiParts.AddIfMissing(missing, icon, nameof(JokerBarButton), nameof(icon));
            UiParts.AddIfMissing(missing, coin, nameof(JokerBarButton), nameof(coin));
            UiParts.AddIfMissing(missing, badgeLabel, nameof(JokerBarButton), nameof(badgeLabel));
        }

        private void OnEnable()
        {
            UiParts.Listen(button, OnClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(button, OnClicked);
        }

        private void OnClicked() => Pressed?.Invoke();
    }
}
