using System;
using System.Collections.Generic;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The confirmation before a lost life, over a level (Modules 32 and 41,
    /// D57, D74): "Leave level?" or "Restart level?", the line saying a life
    /// will be lost — and the win streak with it when there is one — and two
    /// buttons, "Lose a life" (red) and "Keep playing" (green). It has no
    /// close button; Back counts as Keep playing, which the level's interface
    /// reads.
    /// </summary>
    /// <remarks>
    /// <para>It holds no rules: the bootstrap opens it, stops and resumes the
    /// countdown, and reports the loss.</para>
    /// <para><b>Open and closed.</b> It opens and closes with every overlay's
    /// motion (<see cref="OverlayMotion"/>, Module 47): <see cref="Show"/> pops
    /// it in, <see cref="Hide"/> closes it with the short reverse and
    /// <see cref="HideNow"/> at once. <see cref="IsOpen"/> is false from the
    /// start of a close, so the level resumes at once and the closing panel
    /// takes no input.</para>
    /// </remarks>
    public sealed class ConfirmOverlay : MonoBehaviour
    {
        [Tooltip("On the overlay's root: its shared open and close (Module 47).")]
        [SerializeField] private OverlayMotion motion;

        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("'You will lose a life.', with the streak when there is one.")]
        [SerializeField] private TMP_Text lineLabel;

        [Tooltip("The red Button Primary: loses a life, then leaves or restarts.")]
        [SerializeField] private Button loseLifeButton;

        [SerializeField] private TMP_Text loseLifeLabel;

        [Tooltip("The green Button Primary: closes the confirmation and resumes the countdown.")]
        [SerializeField] private Button keepPlayingButton;

        [SerializeField] private TMP_Text keepPlayingLabel;

        /// <summary>Raised when "Lose a life" is pressed.</summary>
        public event Action LoseLifeRequested;

        /// <summary>Raised when "Keep playing" is pressed.</summary>
        public event Action KeepPlayingRequested;

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/> or <see cref="HideNow"/>; false from the start of a close.</summary>
        public bool IsOpen => motion != null && motion.IsOpen;

        /// <summary>
        /// Opens the confirmation for <paramref name="kind"/>, naming
        /// <paramref name="streakAtRisk"/> when it is above 0, with the pop.
        /// </summary>
        public void Show(ConfirmKind kind, int streakAtRisk, UiConfig config)
        {
            titleLabel.text = LevelPanelTexts.ConfirmTitle(kind, config);
            lineLabel.text = LevelPanelTexts.ConfirmLine(streakAtRisk, config);
            loseLifeLabel.text = config.LoseLifeLabel;
            keepPlayingLabel.text = config.KeepPlayingLabel;

            motion.Open(config);
        }

        /// <summary>Closes the confirmation with the overlays' short close. Does nothing when it is not open.</summary>
        public void Hide(UiConfig config)
        {
            motion.Close(config);
        }

        /// <summary>Closes the confirmation at once: the level's interface is being shown afresh or hidden.</summary>
        public void HideNow()
        {
            motion.ShowAt(false);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, motion, nameof(ConfirmOverlay), nameof(motion));
            if (motion != null)
            {
                motion.AddMissing(missing);
            }

            UiParts.AddIfMissing(missing, titleLabel, nameof(ConfirmOverlay), nameof(titleLabel));
            UiParts.AddIfMissing(missing, lineLabel, nameof(ConfirmOverlay), nameof(lineLabel));
            UiParts.AddIfMissing(missing, loseLifeButton, nameof(ConfirmOverlay), nameof(loseLifeButton));
            UiParts.AddIfMissing(missing, loseLifeLabel, nameof(ConfirmOverlay), nameof(loseLifeLabel));
            UiParts.AddIfMissing(missing, keepPlayingButton, nameof(ConfirmOverlay), nameof(keepPlayingButton));
            UiParts.AddIfMissing(missing, keepPlayingLabel, nameof(ConfirmOverlay), nameof(keepPlayingLabel));
        }

        private void OnEnable()
        {
            UiParts.Listen(loseLifeButton, OnLoseLifeClicked);
            UiParts.Listen(keepPlayingButton, OnKeepPlayingClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(loseLifeButton, OnLoseLifeClicked);
            UiParts.Unlisten(keepPlayingButton, OnKeepPlayingClicked);
        }

        private void OnLoseLifeClicked() => LoseLifeRequested?.Invoke();

        private void OnKeepPlayingClicked() => KeepPlayingRequested?.Invoke();
    }
}
