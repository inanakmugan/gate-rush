using System.Collections.Generic;
using GateRush.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One row of the Leaderboard (Module 39): the rank with a gold, silver or
    /// bronze medal on the first three, the avatar, the name, and the score
    /// beside the coin. The player's row is drawn in the highlight colour.
    /// Created by <see cref="LeaderboardScreen"/> from a prefab, one per entry;
    /// it raises nothing.
    /// </summary>
    public sealed class LeaderboardRow : MonoBehaviour
    {
        [Tooltip("The row's panel: the highlight colour on the player's row, the row colour on every other.")]
        [SerializeField] private Image background;

        [SerializeField] private TMP_Text rankLabel;

        [Tooltip("The medal beside the rank; shown on the first three ranks only, in their colours.")]
        [SerializeField] private Image medal;

        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("The lifetime gold, as '1,250'.")]
        [SerializeField] private TMP_Text scoreLabel;

        /// <summary>Shows <paramref name="entry"/> with <paramref name="avatarSprite"/>, in the colours of <paramref name="config"/>.</summary>
        public void Show(LeaderboardEntry entry, Sprite avatarSprite, UiConfig config)
        {
            background.color = entry.IsPlayer ? config.LeaderboardHighlightColor : config.LeaderboardRowColor;
            rankLabel.text = LeaderboardTexts.Rank(entry.Rank, config);
            var hasMedal = config.TryGetMedalColor(entry.Rank, out var medalColor);
            medal.gameObject.SetActive(hasMedal);
            if (hasMedal)
            {
                medal.color = medalColor;
            }

            avatar.sprite = avatarSprite;
            nameLabel.text = entry.Name;
            scoreLabel.text = LeaderboardTexts.Score(entry.Score, config);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, background, nameof(LeaderboardRow), nameof(background));
            UiParts.AddIfMissing(missing, rankLabel, nameof(LeaderboardRow), nameof(rankLabel));
            UiParts.AddIfMissing(missing, medal, nameof(LeaderboardRow), nameof(medal));
            UiParts.AddIfMissing(missing, avatar, nameof(LeaderboardRow), nameof(avatar));
            UiParts.AddIfMissing(missing, nameLabel, nameof(LeaderboardRow), nameof(nameLabel));
            UiParts.AddIfMissing(missing, scoreLabel, nameof(LeaderboardRow), nameof(scoreLabel));
        }
    }
}
