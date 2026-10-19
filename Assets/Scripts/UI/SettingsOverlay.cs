using System;
using System.Collections.Generic;
using GateRush.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The Settings overlay (Module 35): the sound toggle and the Levels
    /// button that opens the level grid. The haptics toggle arrives with
    /// Module 6.4. The toggle shows the state it is given; pressing it only
    /// asks.
    /// </summary>
    public sealed class SettingsOverlay : MonoBehaviour
    {
        [Tooltip("The red close button in the panel's corner.")]
        [SerializeField] private Button closeButton;

        [Tooltip("The sound toggle's button; it has no transition of its own, its two looks are below.")]
        [SerializeField] private Button soundButton;

        [Tooltip("Shown while sound is on.")]
        [SerializeField] private GameObject soundOnLook;

        [Tooltip("Shown while sound is off.")]
        [SerializeField] private GameObject soundOffLook;

        [SerializeField] private Button levelsButton;

        /// <summary>Raised when the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Raised when the sound toggle is pressed.</summary>
        public event Action SoundToggleRequested;

        /// <summary>Raised when Levels is pressed.</summary>
        public event Action LevelsRequested;

        /// <summary>Shows the sound setting of <paramref name="state"/> on the toggle.</summary>
        public void Show(FrontEndState state)
        {
            soundOnLook.SetActive(state.IsSoundOn);
            soundOffLook.SetActive(!state.IsSoundOn);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(SettingsOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, soundButton, nameof(SettingsOverlay), nameof(soundButton));
            UiParts.AddIfMissing(missing, soundOnLook, nameof(SettingsOverlay), nameof(soundOnLook));
            UiParts.AddIfMissing(missing, soundOffLook, nameof(SettingsOverlay), nameof(soundOffLook));
            UiParts.AddIfMissing(missing, levelsButton, nameof(SettingsOverlay), nameof(levelsButton));
        }

        private void OnEnable()
        {
            UiParts.Listen(closeButton, OnCloseClicked);
            UiParts.Listen(soundButton, OnSoundClicked);
            UiParts.Listen(levelsButton, OnLevelsClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(closeButton, OnCloseClicked);
            UiParts.Unlisten(soundButton, OnSoundClicked);
            UiParts.Unlisten(levelsButton, OnLevelsClicked);
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();

        private void OnSoundClicked() => SoundToggleRequested?.Invoke();

        private void OnLevelsClicked() => LevelsRequested?.Invoke();
    }
}
