using System;
using System.Collections.Generic;
using GateRush.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.UI
{
    /// <summary>
    /// The gold pack cards a view creates from the Gold Pack Card prefab, one
    /// per offer (Modules 36 and 40): the one path the Store and the level's
    /// gold packs share. The cards are created the first time the offers are
    /// shown and again only when their count changes; every later show
    /// updates them in place. Every card's price button is passed to the
    /// owner's buy handler, and unsubscribed before the card is destroyed.
    /// </summary>
    internal sealed class GoldPackCards
    {
        private readonly List<GoldPackCard> cards = new List<GoldPackCard>();
        private readonly Action<string> onBuy;

        /// <summary>Cards whose price buttons call <paramref name="onBuy"/> with their product id.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="onBuy"/> is null.</exception>
        public GoldPackCards(Action<string> onBuy)
        {
            this.onBuy = onBuy ?? throw new ArgumentNullException(nameof(onBuy));
        }

        /// <summary>
        /// Shows <paramref name="offers"/>, each with its ribbon, on cards made
        /// from <paramref name="prefab"/> under <paramref name="parent"/>,
        /// creating them afresh when their count differs from what is shown.
        /// </summary>
        public void Show(IReadOnlyList<GoldOffer> offers, GoldPackCard prefab, Transform parent, UiConfig config)
        {
            if (cards.Count != offers.Count)
            {
                DestroyAll();
                for (var i = 0; i < offers.Count; i++)
                {
                    var card = Object.Instantiate(prefab, parent);
                    UiTint.ApplyAll(card, config);
                    card.BuyRequested += onBuy;
                    cards.Add(card);
                }
            }

            for (var i = 0; i < cards.Count; i++)
            {
                cards[i].Show(offers[i], StoreTexts.Ribbon(offers[i].ProductId, config), config);
            }
        }

        /// <summary>
        /// The card showing <paramref name="productId"/>, while it is shown on
        /// screen: where that pack's coins fly from (Module 47). False when no
        /// such card exists or it is not active in the hierarchy.
        /// </summary>
        public bool TryGetShown(string productId, out RectTransform card)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                var candidate = cards[i];
                if (candidate != null && candidate.gameObject.activeInHierarchy
                    && string.Equals(candidate.ProductId, productId, StringComparison.Ordinal))
                {
                    card = (RectTransform)candidate.transform;
                    return true;
                }
            }

            card = null;
            return false;
        }

        /// <summary>Unsubscribes from every card and destroys those that still exist.</summary>
        public void DestroyAll()
        {
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (ReferenceEquals(card, null))
                {
                    continue;
                }

                // Unsubscribing from a C# event of a destroyed card is safe.
                card.BuyRequested -= onBuy;
                if (card != null)
                {
                    Object.Destroy(card.gameObject);
                }
            }

            cards.Clear();
        }
    }
}
