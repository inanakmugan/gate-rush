using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One level in the level grid (Module 35): its number, and a mark when
    /// it is completed. Every level is pressable, completed or not (D50, D58).
    /// Created by <see cref="LevelsOverlay"/> from a prefab.
    /// </summary>
    public sealed class LevelButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text numberLabel;

        [Tooltip("Shown only for a completed level.")]
        [SerializeField] private GameObject completedMark;

        /// <summary>Raised with this button when it is pressed.</summary>
        public event Action<LevelButton> Pressed;

        /// <summary>Shows <paramref name="entry"/>'s number and completed mark.</summary>
        public void Show(LevelEntry entry, UiConfig config)
        {
            numberLabel.text = HomeTexts.LevelNumber(entry.Number, config);
            completedMark.SetActive(entry.IsCompleted);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, button, nameof(LevelButton), nameof(button));
            UiParts.AddIfMissing(missing, numberLabel, nameof(LevelButton), nameof(numberLabel));
            UiParts.AddIfMissing(missing, completedMark, nameof(LevelButton), nameof(completedMark));
        }

        private void OnEnable()
        {
            UiParts.Listen(button, OnClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(button, OnClicked);
        }

        private void OnClicked() => Pressed?.Invoke(this);
    }
}
