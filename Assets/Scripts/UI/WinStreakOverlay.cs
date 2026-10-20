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
    /// The Win Streak overlay (Module 37, D61), opened by the streak bar: the
    /// current and best streaks, the ladder with one row per reward of the
    /// table and its state, the rule in one line, and the red close button.
    /// </summary>
    /// <remarks>
    /// The rows are created from <c>rowPrefab</c> under <c>rows</c> the first
    /// time the table is shown, and again only when its length changes; every
    /// later show updates them in place. Nothing in the prefab fixes how many
    /// there are.
    /// </remarks>
    public sealed class WinStreakOverlay : MonoBehaviour
    {
        [Tooltip("The red close button in the panel's corner.")]
        [SerializeField] private Button closeButton;

        [Tooltip("'Streak 4'.")]
        [SerializeField] private TMP_Text streakLabel;

        [Tooltip("'Best 7'.")]
        [SerializeField] private TMP_Text bestLabel;

        [Tooltip("The scroll view's content, with the column the ladder rows are created under.")]
        [SerializeField] private RectTransform rows;

        [Tooltip("The Streak Ladder Row prefab asset.")]
        [SerializeField] private StreakLadderRow rowPrefab;

        [Tooltip("The rule at the bottom.")]
        [SerializeField] private TMP_Text ruleLabel;

        private readonly List<StreakLadderRow> shownRows = new List<StreakLadderRow>();

        /// <summary>Raised when the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Shows the streaks and the ladder of <paramref name="state"/>, the rows' icons from <paramref name="icons"/>.</summary>
        public void Show(FrontEndState state, UiConfig config, JokerIcons icons)
        {
            var rewards = state.StreakRewards ?? Array.Empty<StreakReward>();
            var progress = StreakProgress.Of(state.Streak, rewards);
            streakLabel.text = StreakTexts.CurrentStreak(state.Streak, config);
            bestLabel.text = StreakTexts.BestStreak(state.BestStreak, config);
            ruleLabel.text = config.StreakRuleText;

            if (shownRows.Count != rewards.Count)
            {
                UiParts.Respawn(shownRows, rewards.Count, rowPrefab, rows, config);
            }

            for (var i = 0; i < shownRows.Count; i++)
            {
                var reward = rewards[i];
                shownRows[i].Show(
                    icons.IconOf(reward.Joker),
                    StreakTexts.LadderWins(reward.Wins, config),
                    StreakTexts.LadderReward(reward, config),
                    StreakTexts.LadderState(progress.States[i], config));
            }
        }

        /// <summary>Adds each unassigned part, the row prefab's own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(WinStreakOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, streakLabel, nameof(WinStreakOverlay), nameof(streakLabel));
            UiParts.AddIfMissing(missing, bestLabel, nameof(WinStreakOverlay), nameof(bestLabel));
            UiParts.AddIfMissing(missing, rows, nameof(WinStreakOverlay), nameof(rows));
            UiParts.AddIfMissing(missing, rowPrefab, nameof(WinStreakOverlay), nameof(rowPrefab));
            UiParts.AddIfMissing(missing, ruleLabel, nameof(WinStreakOverlay), nameof(ruleLabel));
            if (rowPrefab != null)
            {
                rowPrefab.AddMissing(missing);
                UiParts.AddMissingTints(missing, rowPrefab);
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
            UiParts.DestroyAll(shownRows);
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();
    }
}
