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
    /// The Streak Reward overlay (Module 37): opened on Home with the streak
    /// rewards campaign wins gave since the last announcement — "4 wins in a
    /// row!" with the latest rewarded streak, then one line per joker kind,
    /// merged (<see cref="StreakPrizes"/>) — and a green Collect button. The
    /// jokers are already in the inventory; Collect, like Back, only closes
    /// it. It has no close button, since Collect is its one button.
    /// </summary>
    /// <remarks>
    /// The lines are created from <c>prizePrefab</c> under <c>prizes</c>, and
    /// again only when their number changes.
    /// </remarks>
    public sealed class StreakRewardOverlay : MonoBehaviour
    {
        [Tooltip("'4 wins in a row!'.")]
        [SerializeField] private TMP_Text headlineLabel;

        [Tooltip("The column the prize lines are created under.")]
        [SerializeField] private RectTransform prizes;

        [Tooltip("The Streak Prize prefab asset.")]
        [SerializeField] private StreakPrize prizePrefab;

        [Tooltip("The one button: closes the overlay.")]
        [SerializeField] private Button collectButton;

        private readonly List<StreakPrize> shownPrizes = new List<StreakPrize>();

        /// <summary>Raised when Collect is pressed.</summary>
        public event Action CollectRequested;

        /// <summary>
        /// Shows <paramref name="rewards"/>, merged by joker kind, under a
        /// headline with <paramref name="latestStreak"/>; the icons from
        /// <paramref name="icons"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="rewards"/> is null.</exception>
        public void Show(int latestStreak, IReadOnlyList<StreakReward> rewards, UiConfig config, JokerIcons icons)
        {
            var merged = StreakPrizes.Merge(rewards);
            headlineLabel.text = StreakTexts.RewardHeadline(latestStreak, config);
            if (shownPrizes.Count != merged.Count)
            {
                UiParts.Respawn(shownPrizes, merged.Count, prizePrefab, prizes, config);
            }

            for (var i = 0; i < shownPrizes.Count; i++)
            {
                shownPrizes[i].Show(icons.IconOf(merged[i].Joker), StreakTexts.Prize(merged[i], config));
            }
        }

        /// <summary>Adds each unassigned part, the prize prefab's own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, headlineLabel, nameof(StreakRewardOverlay), nameof(headlineLabel));
            UiParts.AddIfMissing(missing, prizes, nameof(StreakRewardOverlay), nameof(prizes));
            UiParts.AddIfMissing(missing, prizePrefab, nameof(StreakRewardOverlay), nameof(prizePrefab));
            UiParts.AddIfMissing(missing, collectButton, nameof(StreakRewardOverlay), nameof(collectButton));
            if (prizePrefab != null)
            {
                prizePrefab.AddMissing(missing);
                UiParts.AddMissingTints(missing, prizePrefab);
            }
        }

        private void OnEnable()
        {
            UiParts.Listen(collectButton, OnCollectClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(collectButton, OnCollectClicked);
        }

        private void OnDestroy()
        {
            UiParts.DestroyAll(shownPrizes);
        }

        private void OnCollectClicked() => CollectRequested?.Invoke();
    }
}
