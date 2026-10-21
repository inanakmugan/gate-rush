using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One card of the Store bought for gold (Modules 36 and 43, D62, D76): a
    /// joker, or the lives refill. Its icon and name are fixed on the prefab;
    /// the line under the name and the buy button are shown from what the
    /// Store gives it. A locked joker's card shows its icon dimmed under a
    /// lock, the line saying when it unlocks in place of the count, and no
    /// buy button, so pressing it does nothing. A press raises
    /// <see cref="Pressed"/>; what is bought is the Store's business.
    /// </summary>
    public sealed class StoreItemCard : MonoBehaviour
    {
        // The icon's opacity on a card that is not locked: not a tunable.
        private const float OpaqueAlpha = 1f;

        [Tooltip("The card's icon: drawn at the UI Config's Locked Icon Alpha while locked, opaque otherwise.")]
        [SerializeField] private Image icon;

        [Tooltip("The lock over the icon, shown while the joker is locked (D76). The lives card has one too, never shown.")]
        [SerializeField] private GameObject lockIcon;

        [Tooltip("The line under the name: 'You have N', the lives, or when a locked joker unlocks.")]
        [SerializeField] private TMP_Text subLabel;

        [SerializeField] private Button buyButton;

        [Tooltip("The buy button's label: the price, or 'Full'.")]
        [SerializeField] private TMP_Text priceLabel;

        [Tooltip("The coin beside the price; hidden when the button shows no price.")]
        [SerializeField] private GameObject coinIcon;

        /// <summary>Raised when the buy button is pressed.</summary>
        public event Action Pressed;

        /// <summary>
        /// Shows an unlocked card: <paramref name="subText"/> under the name
        /// and <paramref name="priceText"/> on the button, with the coin when
        /// <paramref name="showsCoin"/>; the button can be pressed only when
        /// <paramref name="isPressable"/>, and its label then takes the
        /// pressable colour. The icon is opaque, the lock hidden and the buy
        /// button shown again after <see cref="ShowLocked"/>.
        /// </summary>
        public void Show(string subText, string priceText, bool isPressable, bool showsCoin, UiConfig config)
        {
            SetLocked(false, config);
            subLabel.text = subText;
            priceLabel.text = priceText;
            coinIcon.SetActive(showsCoin);
            buyButton.interactable = isPressable;
            priceLabel.color = isPressable ? config.ButtonLabelColor : config.DisabledButtonLabelColor;
        }

        /// <summary>
        /// Shows a locked joker's card (D76): <paramref name="unlocksAtText"/>
        /// in place of the line under the name, so the count held is never
        /// shown; the buy button, its price and coin hidden; and the icon
        /// dimmed under the lock.
        /// </summary>
        public void ShowLocked(string unlocksAtText, UiConfig config)
        {
            SetLocked(true, config);
            subLabel.text = unlocksAtText;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>, named after this card.</summary>
        public void AddMissing(List<string> missing)
        {
            var owner = $"{nameof(StoreItemCard)} '{name}'";
            UiParts.AddIfMissing(missing, icon, owner, nameof(icon));
            UiParts.AddIfMissing(missing, lockIcon, owner, nameof(lockIcon));
            UiParts.AddIfMissing(missing, subLabel, owner, nameof(subLabel));
            UiParts.AddIfMissing(missing, buyButton, owner, nameof(buyButton));
            UiParts.AddIfMissing(missing, priceLabel, owner, nameof(priceLabel));
            UiParts.AddIfMissing(missing, coinIcon, owner, nameof(coinIcon));
        }

        private void SetLocked(bool isLocked, UiConfig config)
        {
            lockIcon.SetActive(isLocked);
            buyButton.gameObject.SetActive(!isLocked);
            var color = icon.color;
            color.a = isLocked ? config.LockedIconAlpha : OpaqueAlpha;
            icon.color = color;
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
