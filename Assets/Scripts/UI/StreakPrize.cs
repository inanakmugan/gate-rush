using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One line of the Streak Reward overlay (Module 37): a joker's icon and
    /// "+1 Rocket". The overlay creates one per joker kind given.
    /// </summary>
    public sealed class StreakPrize : MonoBehaviour
    {
        [SerializeField] private Image icon;

        [Tooltip("'+1 Rocket'.")]
        [SerializeField] private TMP_Text label;

        /// <summary>Shows <paramref name="iconSprite"/> and <paramref name="text"/>.</summary>
        public void Show(Sprite iconSprite, string text)
        {
            icon.sprite = iconSprite;
            label.text = text;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, icon, nameof(StreakPrize), nameof(icon));
            UiParts.AddIfMissing(missing, label, nameof(StreakPrize), nameof(label));
        }
    }
}
