using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The Store tab's page (Modules 36 and 43, D62, D72, D76): a vertical
    /// scroll of three sections — the gold packs, the three jokers, each shown
    /// locked until the campaign unlocks it, and the lives refill — and
    /// under them the line saying the store is simulated, which is never
    /// hidden. Shows the <see cref="FrontEndState"/> it is given and raises an
    /// event for each buy button; whether the player can afford something is
    /// never asked here: every purchase can be pressed, and the bootstrap
    /// answers a refusal with the Not Enough Gold prompt.
    /// </summary>
    /// <remarks>
    /// The gold pack cards are created from <c>goldPackCardPrefab</c> under
    /// <c>goldPacks</c> the first time the offers are shown, and again only
    /// when their count changes; every later show updates them in place. The
    /// joker and refill cards are fixed on the page.
    /// </remarks>
    public sealed class StoreScreen : MonoBehaviour
    {
        [Tooltip("The page's Scroll Rect: scrolled to the top when the Store is selected.")]
        [SerializeField] private ScrollRect scrollRect;

        [SerializeField] private TMP_Text goldHeader;
        [SerializeField] private TMP_Text jokersHeader;
        [SerializeField] private TMP_Text livesHeader;

        [Tooltip("The line under the sections saying the store is simulated; never hidden (D72).")]
        [SerializeField] private TMP_Text testStoreNote;

        [Tooltip("The layout group the gold pack cards are created under.")]
        [SerializeField] private RectTransform goldPacks;

        [Tooltip("The Gold Pack Card prefab asset.")]
        [SerializeField] private GoldPackCard goldPackCardPrefab;

        [SerializeField] private StoreItemCard clockCard;
        [SerializeField] private StoreItemCard rocketCard;
        [SerializeField] private StoreItemCard broomCard;
        [SerializeField] private StoreItemCard livesCard;

        private GoldPackCards packCards;
        private bool isSubscribed;

        /// <summary>Raised with a gold pack's product id when its price button is pressed.</summary>
        public event Action<string> GoldPackRequested;

        /// <summary>Raised with a joker's kind when its buy button is pressed.</summary>
        public event Action<JokerKind> JokerPurchaseRequested;

        /// <summary>Raised when the refill button is pressed; it is pressable whenever the lives are not full.</summary>
        public event Action RefillRequested;

        /// <summary>
        /// Shows the headers, the gold offers, the jokers' counts and prices,
        /// the lives and the refill of <paramref name="state"/>, creating the
        /// gold pack cards when their count differs from what is shown.
        /// </summary>
        public void Show(FrontEndState state, UiConfig config)
        {
            goldHeader.text = config.GoldHeader;
            jokersHeader.text = config.JokersHeader;
            livesHeader.text = config.LivesHeader;
            testStoreNote.text = config.TestStoreNote;

            PackCards.Show(state.GoldOffers ?? Array.Empty<GoldOffer>(), goldPackCardPrefab, goldPacks, config);

            ShowJoker(clockCard, JokerKind.Clock, state, config);
            ShowJoker(rocketCard, JokerKind.Rocket, state, config);
            ShowJoker(broomCard, JokerKind.Broom, state, config);

            var isFull = state.IsLivesFull;
            livesCard.Show(
                StoreTexts.LivesStatus(state.Lives, state.LifeCap, isFull, config),
                StoreTexts.RefillPrice(state.RefillPrice, isFull, config),
                !isFull,
                !isFull,
                config);
        }

        /// <summary>Scrolls to the top, so the gold packs are the first thing seen.</summary>
        public void ScrollToTop()
        {
            scrollRect.StopMovement();

            // The content is anchored and pivoted at the viewport's top, so
            // zero is its top.
            scrollRect.content.anchoredPosition = Vector2.zero;
        }

        /// <summary>Adds each unassigned part, the cards' and the card prefab's own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, scrollRect, nameof(StoreScreen), nameof(scrollRect));
            UiParts.AddIfMissing(missing, goldHeader, nameof(StoreScreen), nameof(goldHeader));
            UiParts.AddIfMissing(missing, jokersHeader, nameof(StoreScreen), nameof(jokersHeader));
            UiParts.AddIfMissing(missing, livesHeader, nameof(StoreScreen), nameof(livesHeader));
            UiParts.AddIfMissing(missing, testStoreNote, nameof(StoreScreen), nameof(testStoreNote));
            UiParts.AddIfMissing(missing, goldPacks, nameof(StoreScreen), nameof(goldPacks));
            UiParts.AddIfMissing(missing, goldPackCardPrefab, nameof(StoreScreen), nameof(goldPackCardPrefab));
            UiParts.AddIfMissing(missing, clockCard, nameof(StoreScreen), nameof(clockCard));
            UiParts.AddIfMissing(missing, rocketCard, nameof(StoreScreen), nameof(rocketCard));
            UiParts.AddIfMissing(missing, broomCard, nameof(StoreScreen), nameof(broomCard));
            UiParts.AddIfMissing(missing, livesCard, nameof(StoreScreen), nameof(livesCard));

            if (scrollRect != null && scrollRect.content == null)
            {
                missing.Add($"{nameof(StoreScreen)}.{nameof(scrollRect)}.content");
            }

            foreach (var card in new[] { clockCard, rocketCard, broomCard, livesCard })
            {
                if (card != null)
                {
                    card.AddMissing(missing);
                }
            }

            if (goldPackCardPrefab != null)
            {
                goldPackCardPrefab.AddMissing(missing);
                var tints = goldPackCardPrefab.GetComponentsInChildren<UiTint>(true);
                for (var i = 0; i < tints.Length; i++)
                {
                    tints[i].AddMissing(missing);
                }
            }
        }

        /// <summary>
        /// A joker's card: locked, with the level it unlocks at and neither
        /// count nor price nor buy button, while the campaign has not reached
        /// it (D76); otherwise the count held and the price.
        /// </summary>
        private static void ShowJoker(StoreItemCard card, JokerKind kind, FrontEndState state, UiConfig config)
        {
            if (!state.IsJokerUnlocked(kind))
            {
                card.ShowLocked(StoreTexts.UnlocksAt(state.JokerUnlockLevel(kind), config), config);
                return;
            }

            card.Show(
                StoreTexts.JokerCount(state.JokerCount(kind), config),
                StoreTexts.GoldPrice(state.JokerPrice(kind), config),
                true,
                true,
                config);
        }

        // Made on first use, so its buy handler is this instance's.
        private GoldPackCards PackCards => packCards ?? (packCards = new GoldPackCards(OnGoldPackPressed));

        private void OnEnable()
        {
            if (isSubscribed || clockCard == null || rocketCard == null || broomCard == null || livesCard == null)
            {
                return;
            }

            clockCard.Pressed += OnClockPressed;
            rocketCard.Pressed += OnRocketPressed;
            broomCard.Pressed += OnBroomPressed;
            livesCard.Pressed += OnRefillPressed;
            isSubscribed = true;
        }

        private void OnDisable()
        {
            if (!isSubscribed)
            {
                return;
            }

            // Unsubscribing from a C# event of a destroyed card is safe, so
            // this runs during teardown as well.
            clockCard.Pressed -= OnClockPressed;
            rocketCard.Pressed -= OnRocketPressed;
            broomCard.Pressed -= OnBroomPressed;
            livesCard.Pressed -= OnRefillPressed;
            isSubscribed = false;
        }

        private void OnDestroy()
        {
            packCards?.DestroyAll();
        }

        private void OnGoldPackPressed(string productId) => GoldPackRequested?.Invoke(productId);

        private void OnClockPressed() => JokerPurchaseRequested?.Invoke(JokerKind.Clock);

        private void OnRocketPressed() => JokerPurchaseRequested?.Invoke(JokerKind.Rocket);

        private void OnBroomPressed() => JokerPurchaseRequested?.Invoke(JokerKind.Broom);

        private void OnRefillPressed() => RefillRequested?.Invoke();
    }
}
