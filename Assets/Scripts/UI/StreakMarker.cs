using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One reward's marker on Home's streak bar (Module 37): the joker's icon,
    /// its count when above 1, a check once reached in the current streak and
    /// a highlight on the next reward. <see cref="StreakBar"/> creates one per
    /// reward and places it.
    /// </summary>
    public sealed class StreakMarker : MonoBehaviour
    {
        [SerializeField] private Image icon;

        [Tooltip("The count, empty for 1.")]
        [SerializeField] private TMP_Text countLabel;

        [Tooltip("Shown when the reward was reached in the current streak.")]
        [SerializeField] private GameObject check;

        [Tooltip("Shown on the next reward.")]
        [SerializeField] private GameObject highlight;

        /// <summary>Shows <paramref name="iconSprite"/> and <paramref name="countText"/>, with the check and the highlight of <paramref name="state"/>.</summary>
        public void Show(Sprite iconSprite, string countText, StreakRewardState state)
        {
            icon.sprite = iconSprite;
            countLabel.text = countText;
            check.SetActive(state == StreakRewardState.Collected);
            highlight.SetActive(state == StreakRewardState.Next);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, icon, nameof(StreakMarker), nameof(icon));
            UiParts.AddIfMissing(missing, countLabel, nameof(StreakMarker), nameof(countLabel));
            UiParts.AddIfMissing(missing, check, nameof(StreakMarker), nameof(check));
            UiParts.AddIfMissing(missing, highlight, nameof(StreakMarker), nameof(highlight));
        }
    }
}
