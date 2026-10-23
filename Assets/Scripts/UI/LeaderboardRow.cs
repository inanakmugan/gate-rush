using System.Collections.Generic;
using GateRush.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One row of the Leaderboard (Module 39): the rank with a gold, silver or
    /// bronze medal on the first three — each its own sprite since Module 45 —
    /// the avatar, the name, and the score
    /// beside the coin. The player's row is drawn in the highlight colour.
    /// Created by <see cref="LeaderboardScreen"/> from a prefab, one per entry;
    /// it raises nothing.
    /// </summary>
    public sealed class LeaderboardRow : MonoBehaviour
    {
        // The ranks that carry a medal (Module 39): fixed by the spec, not tuned.
        private const int GoldRank = 1;
        private const int SilverRank = 2;
        private const int BronzeRank = 3;

        [Tooltip("The row's panel: the highlight colour on the player's row, the row colour on every other.")]
        [SerializeField] private Image background;

        [SerializeField] private TMP_Text rankLabel;

        [Tooltip("The medal beside the rank; shown on the first three ranks only, drawing their own medal in its own colours.")]
        [SerializeField] private Image medal;

        [Tooltip("The first rank's medal sprite (Module 45); written by the UI builder.")]
        [SerializeField] private Sprite goldMedal;

        [Tooltip("The second rank's medal sprite; written by the UI builder.")]
        [SerializeField] private Sprite silverMedal;

        [Tooltip("The third rank's medal sprite; written by the UI builder.")]
        [SerializeField] private Sprite bronzeMedal;

        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("The lifetime gold, as '1,250'.")]
        [SerializeField] private TMP_Text scoreLabel;

        /// <summary>Shows <paramref name="entry"/> with <paramref name="avatarSprite"/>, in the colours of <paramref name="config"/>.</summary>
        public void Show(LeaderboardEntry entry, Sprite avatarSprite, UiConfig config)
        {
            background.color = entry.IsPlayer ? config.LeaderboardHighlightColor : config.LeaderboardRowColor;
            rankLabel.text = LeaderboardTexts.Rank(entry.Rank, config);
            var medalSprite = MedalOf(entry.Rank);
            medal.gameObject.SetActive(medalSprite != null);
            if (medalSprite != null)
            {
                medal.sprite = medalSprite;
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
            UiParts.AddIfMissing(missing, goldMedal, nameof(LeaderboardRow), nameof(goldMedal));
            UiParts.AddIfMissing(missing, silverMedal, nameof(LeaderboardRow), nameof(silverMedal));
            UiParts.AddIfMissing(missing, bronzeMedal, nameof(LeaderboardRow), nameof(bronzeMedal));
            UiParts.AddIfMissing(missing, avatar, nameof(LeaderboardRow), nameof(avatar));
            UiParts.AddIfMissing(missing, nameLabel, nameof(LeaderboardRow), nameof(nameLabel));
            UiParts.AddIfMissing(missing, scoreLabel, nameof(LeaderboardRow), nameof(scoreLabel));
        }

        /// <summary>The medal of <paramref name="rank"/>: gold, silver and bronze for the first three; null for every other rank.</summary>
        private Sprite MedalOf(int rank)
        {
            switch (rank)
            {
                case GoldRank:
                    return goldMedal;
                case SilverRank:
                    return silverMedal;
                case BronzeRank:
                    return bronzeMedal;
                default:
                    return null;
            }
        }
    }
}
