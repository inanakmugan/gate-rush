using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The test purchase sheet (Module 36, D72): the simulated store's
    /// confirmation of a gold pack. It names the gold and the price, says
    /// that the store is simulated and no money is charged, and asks Buy or
    /// Cancel. It never imitates a platform's own sheet. Cancel and the close
    /// button both ask to close it, which the front end answers as Cancel.
    /// </summary>
    public sealed class TestPurchaseOverlay : MonoBehaviour
    {
        [Tooltip("The red close button in the panel's corner: counts as Cancel.")]
        [SerializeField] private Button closeButton;

        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("'1,500 gold for $2.49'.")]
        [SerializeField] private TMP_Text offerLabel;

        [Tooltip("The line saying the store is simulated and nothing is charged.")]
        [SerializeField] private TMP_Text noteLabel;

        [SerializeField] private Button buyButton;
        [SerializeField] private Button cancelButton;

        /// <summary>Raised when Buy is pressed.</summary>
        public event Action BuyRequested;

        /// <summary>Raised when Cancel or the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Shows <paramref name="offer"/> with the sheet's title and its simulated-store line.</summary>
        public void Show(GoldOffer offer, UiConfig config)
        {
            titleLabel.text = config.TestPurchaseTitle;
            offerLabel.text = StoreTexts.OfferLine(offer, config);
            noteLabel.text = config.TestPurchaseNote;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(TestPurchaseOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, titleLabel, nameof(TestPurchaseOverlay), nameof(titleLabel));
            UiParts.AddIfMissing(missing, offerLabel, nameof(TestPurchaseOverlay), nameof(offerLabel));
            UiParts.AddIfMissing(missing, noteLabel, nameof(TestPurchaseOverlay), nameof(noteLabel));
            UiParts.AddIfMissing(missing, buyButton, nameof(TestPurchaseOverlay), nameof(buyButton));
            UiParts.AddIfMissing(missing, cancelButton, nameof(TestPurchaseOverlay), nameof(cancelButton));
        }

        private void OnEnable()
        {
            UiParts.Listen(closeButton, OnCloseClicked);
            UiParts.Listen(cancelButton, OnCloseClicked);
            UiParts.Listen(buyButton, OnBuyClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(closeButton, OnCloseClicked);
            UiParts.Unlisten(cancelButton, OnCloseClicked);
            UiParts.Unlisten(buyButton, OnBuyClicked);
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();

        private void OnBuyClicked() => BuyRequested?.Invoke();
    }
}
