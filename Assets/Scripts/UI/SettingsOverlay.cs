using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The Settings overlay (Modules 35, 38 and 49): the sound toggle, the
    /// haptics toggle under it, the Privacy row under that, and the Levels
    /// button that opens the level grid. The two toggles are independent:
    /// sound off leaves vibration on, and the other way round. They show the
    /// state they are given; pressing one, or Privacy, only asks.
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

        [Tooltip("The haptics row's label, from the UI Config.")]
        [SerializeField] private TMP_Text hapticsLabel;

        [Tooltip("The haptics toggle's button; it has no transition of its own, its two looks are below.")]
        [SerializeField] private Button hapticsButton;

        [Tooltip("Shown while haptics are on: the phone.")]
        [SerializeField] private GameObject hapticsOnLook;

        [Tooltip("Shown while haptics are off: the phone with a cross over it.")]
        [SerializeField] private GameObject hapticsOffLook;

        [Tooltip("The Privacy row's label, from the UI Config.")]
        [SerializeField] private TMP_Text privacyLabel;

        [Tooltip("The Privacy row's button: opens the privacy text over Settings (Module 49).")]
        [SerializeField] private Button privacyButton;

        [SerializeField] private Button levelsButton;

        /// <summary>Raised when the close button is pressed.</summary>
        public event Action CloseRequested;

        /// <summary>Raised when the sound toggle is pressed.</summary>
        public event Action SoundToggleRequested;

        /// <summary>Raised when the haptics toggle is pressed.</summary>
        public event Action HapticsToggleRequested;

        /// <summary>Raised when Levels is pressed.</summary>
        public event Action LevelsRequested;

        /// <summary>Raised when the Privacy row's button is pressed.</summary>
        public event Action PrivacyRequested;

        /// <summary>Shows the sound and haptics settings of <paramref name="state"/> on the toggles, and the haptics and privacy labels of <paramref name="config"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public void Show(FrontEndState state, UiConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            soundOnLook.SetActive(state.IsSoundOn);
            soundOffLook.SetActive(!state.IsSoundOn);
            hapticsLabel.text = config.HapticsLabel;
            hapticsOnLook.SetActive(state.IsHapticsOn);
            hapticsOffLook.SetActive(!state.IsHapticsOn);
            privacyLabel.text = config.PrivacyTitle;
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, closeButton, nameof(SettingsOverlay), nameof(closeButton));
            UiParts.AddIfMissing(missing, soundButton, nameof(SettingsOverlay), nameof(soundButton));
            UiParts.AddIfMissing(missing, soundOnLook, nameof(SettingsOverlay), nameof(soundOnLook));
            UiParts.AddIfMissing(missing, soundOffLook, nameof(SettingsOverlay), nameof(soundOffLook));
            UiParts.AddIfMissing(missing, hapticsLabel, nameof(SettingsOverlay), nameof(hapticsLabel));
            UiParts.AddIfMissing(missing, hapticsButton, nameof(SettingsOverlay), nameof(hapticsButton));
            UiParts.AddIfMissing(missing, hapticsOnLook, nameof(SettingsOverlay), nameof(hapticsOnLook));
            UiParts.AddIfMissing(missing, hapticsOffLook, nameof(SettingsOverlay), nameof(hapticsOffLook));
            UiParts.AddIfMissing(missing, privacyLabel, nameof(SettingsOverlay), nameof(privacyLabel));
            UiParts.AddIfMissing(missing, privacyButton, nameof(SettingsOverlay), nameof(privacyButton));
            UiParts.AddIfMissing(missing, levelsButton, nameof(SettingsOverlay), nameof(levelsButton));
        }

        private void OnEnable()
        {
            UiParts.Listen(closeButton, OnCloseClicked);
            UiParts.Listen(soundButton, OnSoundClicked);
            UiParts.Listen(hapticsButton, OnHapticsClicked);
            UiParts.Listen(privacyButton, OnPrivacyClicked);
            UiParts.Listen(levelsButton, OnLevelsClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(closeButton, OnCloseClicked);
            UiParts.Unlisten(soundButton, OnSoundClicked);
            UiParts.Unlisten(hapticsButton, OnHapticsClicked);
            UiParts.Unlisten(privacyButton, OnPrivacyClicked);
            UiParts.Unlisten(levelsButton, OnLevelsClicked);
        }

        private void OnCloseClicked() => CloseRequested?.Invoke();

        private void OnSoundClicked() => SoundToggleRequested?.Invoke();

        private void OnHapticsClicked() => HapticsToggleRequested?.Invoke();

        private void OnPrivacyClicked() => PrivacyRequested?.Invoke();

        private void OnLevelsClicked() => LevelsRequested?.Invoke();
    }
}
