using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One card of the Store bought for gold (Module 36, D62): a joker, or the
    /// lives refill. Its icon and name are fixed on the prefab; the line under
    /// the name and the buy button are shown from what the Store gives it. A
    /// press raises <see cref="Pressed"/>; what is bought is the Store's
    /// business.
    /// </summary>
    public sealed class StoreItemCard : MonoBehaviour
    {
        [Tooltip("The line under the name: 'You have N', or the lives.")]
        [SerializeField] private TMP_Text subLabel;

        [SerializeField] private Button buyButton;

        [Tooltip("The buy button's label: the price, or 'Full'.")]
        [SerializeField] private TMP_Text priceLabel;

        [Tooltip("The coin beside the price; hidden when the button shows no price.")]
        [SerializeField] private GameObject coinIcon;

        /// <summary>Raised when the buy button is pressed.</summary>
        public event Action Pressed;

        /// <summary>
        /// Shows <paramref name="subText"/> under the name and
        /// <paramref name="priceText"/> on the button, with the coin when
        /// <paramref name="showsCoin"/>; the button can be pressed only when
        /// <paramref name="isPressable"/>, and its label then takes the
        /// pressable colour.
        /// </summary>
        public void Show(string subText, string priceText, bool isPressable, bool showsCoin, UiConfig config)
        {
            subLabel.text = subText;
            priceLabel.text = priceText;
            coinIcon.SetActive(showsCoin);
            buyButton.interactable = isPressable;
            priceLabel.color = isPressable ? config.ButtonLabelColor : config.DisabledButtonLabelColor;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>, named after this card.</summary>
        public void AddMissing(List<string> missing)
        {
            var owner = $"{nameof(StoreItemCard)} '{name}'";
            UiParts.AddIfMissing(missing, subLabel, owner, nameof(subLabel));
            UiParts.AddIfMissing(missing, buyButton, owner, nameof(buyButton));
            UiParts.AddIfMissing(missing, priceLabel, owner, nameof(priceLabel));
            UiParts.AddIfMissing(missing, coinIcon, owner, nameof(coinIcon));
        }

        private void OnEnable()
        {
            UiParts.Listen(buyButton, OnBuyClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(buyButton, OnBuyClicked);
        }

        private void OnBuyClicked() => Pressed?.Invoke();
    }
}
