using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One row of the Win Streak overlay's ladder (Module 37): a reward's
    /// wins, its joker's icon, name and count, and its state. The overlay
    /// creates one per reward in the table.
    /// </summary>
    public sealed class StreakLadderRow : MonoBehaviour
    {
        [Tooltip("'3 wins'.")]
        [SerializeField] private TMP_Text winsLabel;

        [SerializeField] private Image icon;

        [Tooltip("'Clock x1'.")]
        [SerializeField] private TMP_Text rewardLabel;

        [Tooltip("Collected, Next, or empty.")]
        [SerializeField] private TMP_Text stateLabel;

        /// <summary>Shows <paramref name="iconSprite"/> and the three texts.</summary>
        public void Show(Sprite iconSprite, string wins, string reward, string state)
        {
            icon.sprite = iconSprite;
            winsLabel.text = wins;
            rewardLabel.text = reward;
            stateLabel.text = state;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, winsLabel, nameof(StreakLadderRow), nameof(winsLabel));
            UiParts.AddIfMissing(missing, icon, nameof(StreakLadderRow), nameof(icon));
            UiParts.AddIfMissing(missing, rewardLabel, nameof(StreakLadderRow), nameof(rewardLabel));
            UiParts.AddIfMissing(missing, stateLabel, nameof(StreakLadderRow), nameof(stateLabel));
        }
    }
}
