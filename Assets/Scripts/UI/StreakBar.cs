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
    /// The streak bar on Home (Module 37, D61): the fire with the current
    /// streak beside it, a bar from 0 to the last reward's wins filled up to
    /// the streak, a marker per reward at its wins' place, and one line toward
    /// the next reward. The whole panel is one button, which opens the Win
    /// Streak overlay. It sits in Home Page's Streak Area, placed there by the
    /// UI builder, and is shown by <see cref="FrontEnd"/>.
    /// </summary>
    /// <remarks>
    /// The markers are created from <c>markerPrefab</c> under <c>markers</c>
    /// the first time the table is shown, and again only when its length
    /// changes; every later show updates them in place. Markers sit at their
    /// wins' share of the bar, the same share the fill uses
    /// (<see cref="StreakProgress"/>), so with two thresholds close together
    /// in a custom table they can overlap.
    /// </remarks>
    public sealed class StreakBar : MonoBehaviour
    {
        [Tooltip("The panel's button: opens the Win Streak overlay.")]
        [SerializeField] private Button button;

        [Tooltip("The current streak, beside the fire.")]
        [SerializeField] private TMP_Text streakLabel;

        [Tooltip("The fill, a child of the track anchored at its left; its right anchor is set to the share filled.")]
        [SerializeField] private RectTransform fill;

        [Tooltip("Stretched over the track; the markers are created under it.")]
        [SerializeField] private RectTransform markers;

        [Tooltip("The Streak Marker prefab asset.")]
        [SerializeField] private StreakMarker markerPrefab;

        [Tooltip("The line under the bar.")]
        [SerializeField] private TMP_Text lineLabel;

        private readonly List<StreakMarker> shownMarkers = new List<StreakMarker>();

        /// <summary>Raised when the bar is pressed.</summary>
        public event Action Pressed;

        /// <summary>Shows the streak and the reward table of <paramref name="state"/>, the markers' icons from <paramref name="icons"/>.</summary>
        public void Show(FrontEndState state, UiConfig config, JokerIcons icons)
        {
            var rewards = state.StreakRewards ?? Array.Empty<StreakReward>();
            var progress = StreakProgress.Of(state.Streak, rewards);
            streakLabel.text = StreakTexts.StreakCount(state.Streak, config);
            lineLabel.text = StreakTexts.BarLine(progress, config);

            fill.anchorMax = new Vector2(progress.Fill, fill.anchorMax.y);
            fill.gameObject.SetActive(progress.Fill > 0f);

            if (shownMarkers.Count != rewards.Count)
            {
                UiParts.Respawn(shownMarkers, rewards.Count, markerPrefab, markers, config);
            }

            for (var i = 0; i < shownMarkers.Count; i++)
            {
                var reward = rewards[i];
                var marker = shownMarkers[i];
                marker.Show(icons.IconOf(reward.Joker), StreakTexts.MarkerCount(reward.Count, config), progress.States[i]);

                // Only the horizontal anchor moves; the prefab's own vertical
                // anchor keeps the marker centred on the track.
                var rect = (RectTransform)marker.transform;
                var position = progress.Positions[i];
                rect.anchorMin = new Vector2(position, rect.anchorMin.y);
                rect.anchorMax = new Vector2(position, rect.anchorMax.y);
                rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
            }
        }

        /// <summary>Adds each unassigned part, the marker prefab's own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, button, nameof(StreakBar), nameof(button));
            UiParts.AddIfMissing(missing, streakLabel, nameof(StreakBar), nameof(streakLabel));
            UiParts.AddIfMissing(missing, fill, nameof(StreakBar), nameof(fill));
            UiParts.AddIfMissing(missing, markers, nameof(StreakBar), nameof(markers));
            UiParts.AddIfMissing(missing, markerPrefab, nameof(StreakBar), nameof(markerPrefab));
            UiParts.AddIfMissing(missing, lineLabel, nameof(StreakBar), nameof(lineLabel));
            if (markerPrefab != null)
            {
                markerPrefab.AddMissing(missing);
                UiParts.AddMissingTints(missing, markerPrefab);
            }
        }

        private void OnEnable()
        {
            UiParts.Listen(button, OnClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(button, OnClicked);
        }

        private void OnDestroy()
        {
            UiParts.DestroyAll(shownMarkers);
        }

        private void OnClicked() => Pressed?.Invoke();
    }
}
