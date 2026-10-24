using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The gold packs over a level (Module 40, D72, D74): a title, a column of
    /// gold pack cards created from the Gold Pack Card prefab, one per offer,
    /// the line saying the store is simulated, which is never hidden, and the
    /// red close button. Opened by Not Enough Gold's Get Gold, so the player
    /// buys gold without leaving the level. A press raises
    /// <see cref="GoldPackRequested"/> with the pack's product id.
    /// </summary>
    public sealed class GoldPacksOverlay : MonoBehaviour
    {
        [Tooltip("The red close button in the panel's corner.")]
        [SerializeField] private Button closeButton;

        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("The line saying the store is simulated; never hidden (D72).")]
        [SerializeField] private TMP_Text noteLabel;

        [Tooltip("The layout group the gold pack cards are created under.")]
        [SerializeField] private RectTransform packs;

        [Tooltip("The Gold Pack Card prefab asset.")]
        [SerializeField] private GoldPackCard goldPackCardPrefab;

        private GoldPackCards cards;

        /// <summary>Raised when the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Raised with a gold pack's product id when its price button is pressed.</summary>
        public event Action<string> GoldPackRequested;

        // Made on first use, so its buy handler is this instance's.
        private GoldPackCards Cards => cards ?? (cards = new GoldPackCards(OnGoldPackPressed));

        /// <summary>Shows the title, the note and a card for each of <paramref name="offers"/>.</summary>
        public void Show(IReadOnlyList<GoldOffer> offers, UiConfig config)
        {
            titleLabel.text = config.GoldPacksTitle;
            noteLabel.text = config.TestStoreNote;
            Cards.Show(offers ?? Array.Empty<GoldOffer>(), goldPackCardPrefab, packs, config);
        }

        /// <summary>The card of <paramref name="productId"/> while the overlay shows it: where its coins fly from (Module 47).</summary>
        public bool TryGetShownCard(string productId, out RectTransform card) => Cards.TryGetShown(productId, out card);

        /// <summary>Adds each unassigned part, the card prefab's own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(GoldPacksOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, titleLabel, nameof(GoldPacksOverlay), nameof(titleLabel));
            UiParts.AddIfMissing(missing, noteLabel, nameof(GoldPacksOverlay), nameof(noteLabel));
            UiParts.AddIfMissing(missing, packs, nameof(GoldPacksOverlay), nameof(packs));
            UiParts.AddIfMissing(missing, goldPackCardPrefab, nameof(GoldPacksOverlay), nameof(goldPackCardPrefab));
            if (goldPackCardPrefab != null)
            {
                goldPackCardPrefab.AddMissing(missing);
                UiParts.AddMissingTints(missing, goldPackCardPrefab);
            }
        }

        private void OnEnable()
        {
            UiParts.Listen(closeButton, OnCloseClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(closeButton, OnCloseClicked);
        }

        private void OnDestroy()
        {
            cards?.DestroyAll();
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();

        private void OnGoldPackPressed(string productId) => GoldPackRequested?.Invoke(productId);
    }
}
