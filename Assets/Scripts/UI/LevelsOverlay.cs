using System;
using System.Collections.Generic;
using GateRush.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The Levels overlay (Module 35, D58): a scrolling grid of every level in
    /// the level order, each a <see cref="LevelButton"/> with its number and a
    /// mark when completed. Pressing one asks for that level as free play,
    /// which needs no life.
    /// </summary>
    /// <remarks>
    /// The buttons are created from <c>levelButtonPrefab</c> under
    /// <c>grid</c> the first time the levels are shown, and again only when
    /// their count changes — the level order is read once per session, so in
    /// practice once. Every later show updates the numbers and marks in place.
    /// The grid's layout group places them.
    /// </remarks>
    public sealed class LevelsOverlay : MonoBehaviour
    {
        [Tooltip("The red close button in the panel's corner.")]
        [SerializeField] private Button closeButton;

        [Tooltip("The scroll view's content, with the Grid Layout Group the level buttons are created under.")]
        [SerializeField] private RectTransform grid;

        [Tooltip("The Level Button prefab asset.")]
        [SerializeField] private LevelButton levelButtonPrefab;

        private readonly List<LevelButton> buttons = new List<LevelButton>();

        // The levels the buttons show, index for index; a press reads its
        // level's name here.
        private IReadOnlyList<LevelEntry> entries = Array.Empty<LevelEntry>();

        /// <summary>Raised when the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Raised with a level's file name when its button is pressed.</summary>
        public event Action<string> LevelRequested;

        /// <summary>
        /// Shows <paramref name="levels"/>, one button each in order, creating
        /// the buttons when their count differs from what is shown.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="levels"/> is null.</exception>
        public void Show(IReadOnlyList<LevelEntry> levels, UiConfig config)
        {
            entries = levels ?? throw new ArgumentNullException(nameof(levels));
            if (buttons.Count != levels.Count)
            {
                RebuildButtons(levels.Count, config);
            }

            for (var i = 0; i < buttons.Count; i++)
            {
                buttons[i].Show(levels[i], config);
            }
        }

        /// <summary>Adds each unassigned part, the level button prefab's own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(LevelsOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, grid, nameof(LevelsOverlay), nameof(grid));
            UiParts.AddIfMissing(missing, levelButtonPrefab, nameof(LevelsOverlay), nameof(levelButtonPrefab));
            if (levelButtonPrefab == null)
            {
                return;
            }

            levelButtonPrefab.AddMissing(missing);
            var tints = levelButtonPrefab.GetComponentsInChildren<UiTint>(true);
            for (var i = 0; i < tints.Length; i++)
            {
                tints[i].AddMissing(missing);
            }
        }

        private void RebuildButtons(int count, UiConfig config)
        {
            DestroyButtons();
            for (var i = 0; i < count; i++)
            {
                var button = Instantiate(levelButtonPrefab, grid);
                UiTint.ApplyAll(button, config);
                button.Pressed += OnLevelPressed;
                buttons.Add(button);
            }
        }

        private void DestroyButtons()
        {
            for (var i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (ReferenceEquals(button, null))
                {
                    continue;
                }

                button.Pressed -= OnLevelPressed;
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            buttons.Clear();
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
            DestroyButtons();
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();

        private void OnLevelPressed(LevelButton button)
        {
            var index = buttons.IndexOf(button);
            if (index >= 0 && index < entries.Count)
            {
                LevelRequested?.Invoke(entries[index].Name);
            }
        }
    }
}
