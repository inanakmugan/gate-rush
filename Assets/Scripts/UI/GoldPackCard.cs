using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One gold pack on the Store (Module 36, D72): the coin stack, the
    /// amount, a ribbon where the pack has one, and a button with the price
    /// text of the purchase service. Created by <see cref="StoreScreen"/> from
    /// the Gold Pack Card prefab, one per offer. A press raises
    /// <see cref="BuyRequested"/> with the pack's product id.
    /// </summary>
    public sealed class GoldPackCard : MonoBehaviour
    {
        [Tooltip("The gold, as '1,500'.")]
        [SerializeField] private TMP_Text amountLabel;

        [Tooltip("The ribbon; hidden for a pack without one.")]
        [SerializeField] private GameObject ribbon;

        [SerializeField] private TMP_Text ribbonLabel;

        [SerializeField] private Button buyButton;

        [Tooltip("The buy button's label: the price as the purchase service shows it.")]
        [SerializeField] private TMP_Text priceLabel;

        private string productId;

        /// <summary>Raised when the price button is pressed, with the product id last shown.</summary>
        public event Action<string> BuyRequested;

        /// <summary>The product id last shown; null before the first <see cref="Show"/>.</summary>
        public string ProductId => productId;

        /// <summary>
        /// Shows <paramref name="offer"/>, with <paramref name="ribbonText"/>
        /// on the ribbon, or no ribbon when it is null or empty.
        /// </summary>
        public void Show(GoldOffer offer, string ribbonText, UiConfig config)
        {
            productId = offer.ProductId;
            amountLabel.text = StoreTexts.GoldAmount(offer.Gold, config);
            priceLabel.text = offer.PriceText;
            var hasRibbon = !string.IsNullOrEmpty(ribbonText);
            ribbon.SetActive(hasRibbon);
            ribbonLabel.text = hasRibbon ? ribbonText : string.Empty;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, amountLabel, nameof(GoldPackCard), nameof(amountLabel));
            UiParts.AddIfMissing(missing, ribbon, nameof(GoldPackCard), nameof(ribbon));
            UiParts.AddIfMissing(missing, ribbonLabel, nameof(GoldPackCard), nameof(ribbonLabel));
            UiParts.AddIfMissing(missing, buyButton, nameof(GoldPackCard), nameof(buyButton));
            UiParts.AddIfMissing(missing, priceLabel, nameof(GoldPackCard), nameof(priceLabel));
        }

        private void OnEnable()
        {
            UiParts.Listen(buyButton, OnBuyClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(buyButton, OnBuyClicked);
        }

        private void OnBuyClicked()
        {
            // Not shown yet: nothing to buy.
            if (productId != null)
            {
                BuyRequested?.Invoke(productId);
            }
        }
    }
}
