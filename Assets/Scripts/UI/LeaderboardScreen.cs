using System;
using System.Collections.Generic;
using GateRush.Platform;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The Leaderboard tab's page (Module 39, D70, D73): under the title, the
    /// line saying the table is simulated, never hidden while it is; a
    /// scrolling list of one row per entry, the player's marked and scrolled
    /// into view; "Loading…" while a fetch is pending; and a failure line
    /// with Retry. It knows nothing of where the rows come from beyond
    /// <see cref="LeaderboardResult.IsSimulated"/>, and decides nothing: Retry
    /// only asks.
    /// </summary>
    /// <remarks>
    /// <para>The rows are created from <c>rowPrefab</c> under <c>rows</c> the
    /// first time a table is shown, and again only when the number of entries
    /// changes; every later show updates them in place.</para>
    /// <para>The note follows the last result shown: Loading and a failure
    /// leave it as it was.</para>
    /// </remarks>
    public sealed class LeaderboardScreen : MonoBehaviour
    {
        // The row's top-left and bottom-left corners in GetWorldCorners' order.
        private const int BottomLeftCorner = 0;
        private const int TopLeftCorner = 1;
        private const int CornerCount = 4;

        [Tooltip("The list's Scroll Rect: scrolled so the player's row is in view.")]
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("The line saying the table is demo players; shown while the result is simulated (D73).")]
        [SerializeField] private TMP_Text noteLabel;

        [Tooltip("'Loading…' or the failure line; hidden while a table shows.")]
        [SerializeField] private TMP_Text statusLabel;

        [Tooltip("Shown only after a failed fetch.")]
        [SerializeField] private Button retryButton;

        [SerializeField] private TMP_Text retryLabel;

        [Tooltip("The scroll view's content, with the column the rows are created under.")]
        [SerializeField] private RectTransform rows;

        [Tooltip("The Leaderboard Row prefab asset.")]
        [SerializeField] private LeaderboardRow rowPrefab;

        private readonly List<LeaderboardRow> shownRows = new List<LeaderboardRow>();
        private readonly Vector3[] corners = new Vector3[CornerCount];

        /// <summary>Raised when Retry is pressed.</summary>
        public event Action RetryRequested;

        /// <summary>Shows "Loading…" alone, with the list and Retry hidden.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public void ShowLoading(UiConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            ShowStatus(config.LeaderboardLoadingText, false, config);
        }

        /// <summary>
        /// Shows <paramref name="result"/>: a loaded table as one row per
        /// entry, avatars from <paramref name="catalog"/>, scrolled so the
        /// player's row is in view; a failed one as the failure line and
        /// Retry. The note shows exactly while the result is simulated.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public void Show(LeaderboardResult result, UiConfig config, AvatarCatalog catalog)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            noteLabel.text = config.LeaderboardSimulatedNote;
            noteLabel.gameObject.SetActive(result.IsSimulated);
            if (!result.IsLoaded)
            {
                ShowStatus(config.LeaderboardFailedText, true, config);
                return;
            }

            statusLabel.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(false);
            scrollRect.gameObject.SetActive(true);

            var entries = result.Entries;
            if (shownRows.Count != entries.Count)
            {
                UiParts.Respawn(shownRows, entries.Count, rowPrefab, rows, config);
            }

            var playerIndex = -1;
            for (var i = 0; i < shownRows.Count; i++)
            {
                var entry = entries[i];
                shownRows[i].Show(entry, catalog.IconOf(entry.AvatarId), config);
                if (entry.IsPlayer)
                {
                    playerIndex = i;
                }
            }

            ScrollTo(playerIndex);
        }

        /// <summary>Adds each unassigned part, the row prefab's own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, scrollRect, nameof(LeaderboardScreen), nameof(scrollRect));
            UiParts.AddIfMissing(missing, noteLabel, nameof(LeaderboardScreen), nameof(noteLabel));
            UiParts.AddIfMissing(missing, statusLabel, nameof(LeaderboardScreen), nameof(statusLabel));
            UiParts.AddIfMissing(missing, retryButton, nameof(LeaderboardScreen), nameof(retryButton));
            UiParts.AddIfMissing(missing, retryLabel, nameof(LeaderboardScreen), nameof(retryLabel));
            UiParts.AddIfMissing(missing, rows, nameof(LeaderboardScreen), nameof(rows));
            UiParts.AddIfMissing(missing, rowPrefab, nameof(LeaderboardScreen), nameof(rowPrefab));

            if (scrollRect != null && scrollRect.content == null)
            {
                missing.Add($"{nameof(LeaderboardScreen)}.{nameof(scrollRect)}.content");
            }

            if (scrollRect != null && scrollRect.viewport == null)
            {
                missing.Add($"{nameof(LeaderboardScreen)}.{nameof(scrollRect)}.viewport");
            }

            if (rowPrefab != null)
            {
                rowPrefab.AddMissing(missing);
                UiParts.AddMissingTints(missing, rowPrefab);
            }
        }

        /// <summary><paramref name="text"/> alone in place of the list, with Retry when <paramref name="hasRetry"/>.</summary>
        private void ShowStatus(string text, bool hasRetry, UiConfig config)
        {
            scrollRect.gameObject.SetActive(false);
            statusLabel.text = text;
            statusLabel.gameObject.SetActive(true);
            retryLabel.text = config.LeaderboardRetryLabel;
            retryButton.gameObject.SetActive(hasRetry);
        }

        /// <summary>
        /// Scrolls so the row at <paramref name="index"/> is centred where it
        /// can be (<see cref="LeaderboardScroll"/>); to the top for -1, a table
        /// without the player.
        /// </summary>
        private void ScrollTo(int index)
        {
            scrollRect.StopMovement();
            var content = scrollRect.content;
            var offset = 0f;
            if (index >= 0)
            {
                // Rows created this frame are placed by the layout only at the
                // end of it; the offset needs them placed now.
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                ((RectTransform)shownRows[index].transform).GetWorldCorners(corners);
                var top = content.InverseTransformPoint(corners[TopLeftCorner]).y;
                var bottom = content.InverseTransformPoint(corners[BottomLeftCorner]).y;
                var contentRect = content.rect;
                offset = LeaderboardScroll.CenteredOffset(
                    contentRect.yMax - top, top - bottom, contentRect.height, scrollRect.viewport.rect.height);
            }

            // The content is anchored and pivoted at the viewport's top, so
            // zero is its top and a larger y scrolls further down.
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, offset);
        }

        private void OnEnable()
        {
            UiParts.Listen(retryButton, OnRetryClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(retryButton, OnRetryClicked);
        }

        private void OnDestroy()
        {
            UiParts.DestroyAll(shownRows);
        }

        private void OnRetryClicked() => RetryRequested?.Invoke();
    }
}
