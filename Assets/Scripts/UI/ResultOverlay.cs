using System;
using System.Collections.Generic;
using DG.Tweening;
using GateRush.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The result panel over a level (Module 41): the title, the gold a
    /// campaign win paid beside the coin, the closing message after the last
    /// campaign level, and the buttons the <see cref="LevelResult"/> offers —
    /// Next (green), Restart (blue after a free-play win, green after a
    /// loss) and Menu (round, home icon). After a loss the middle shows a
    /// large hourglass and, when the loss cost a life, a heart with "-1"
    /// under it (the Module 45 follow-up). It has no close button: the
    /// buttons are the way out, and Back does nothing here.
    /// </summary>
    /// <remarks>
    /// <para>It holds no rules: which buttons show is the bootstrap's,
    /// decided in the result it is given.</para>
    /// <para><b>Open and closed.</b> <see cref="Show"/> and <see cref="Hide"/>
    /// switch this overlay's own GameObject; it opens with a short fade and
    /// scale pop on unscaled time (<see cref="OverlayPop"/>), carrying this
    /// view as its id, which <see cref="Hide"/>, a disable and a destroy stop,
    /// leaving the panel at rest.</para>
    /// </remarks>
    public sealed class ResultOverlay : MonoBehaviour
    {
        [Tooltip("On the overlay's root: the pop fades it in.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("The grey panel: the pop grows it.")]
        [SerializeField] private RectTransform panel;

        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("The coin and the gold earned: shown after a campaign win that paid.")]
        [SerializeField] private GameObject goldRow;

        [SerializeField] private TMP_Text goldLabel;

        [Tooltip("The closing message: shown after the last campaign level.")]
        [SerializeField] private TMP_Text messageLabel;

        [Tooltip("The large hourglass in the panel's middle: shown after a loss.")]
        [SerializeField] private GameObject lossArt;

        [Tooltip("The heart with '-1' under the hourglass: shown after a loss that cost a life, in the campaign (D57).")]
        [SerializeField] private GameObject lifeLost;

        [Tooltip("The '-1' beside the heart.")]
        [SerializeField] private TMP_Text lifeLostLabel;

        [Tooltip("The green Button Primary: shown when a level follows.")]
        [SerializeField] private Button nextButton;

        [SerializeField] private TMP_Text nextLabel;

        [Tooltip("The blue Button Secondary: Restart after a free-play win.")]
        [SerializeField] private Button winRestartButton;

        [SerializeField] private TMP_Text winRestartLabel;

        [Tooltip("The green Button Primary: Restart after a loss.")]
        [SerializeField] private Button lossRestartButton;

        [SerializeField] private TMP_Text lossRestartLabel;

        [Tooltip("The blue Button Round with the home icon: always shown.")]
        [SerializeField] private Button menuButton;

        /// <summary>Raised when Next is pressed.</summary>
        public event Action NextRequested;

        /// <summary>Raised when either Restart is pressed.</summary>
        public event Action RestartRequested;

        /// <summary>Raised when Menu is pressed.</summary>
        public event Action MenuRequested;

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/>.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Opens the panel showing <paramref name="result"/>, with the pop.</summary>
        public void Show(LevelResult result, UiConfig config)
        {
            titleLabel.text = LevelPanelTexts.ResultTitle(result, config);

            goldRow.SetActive(result.GoldEarned.HasValue);
            if (result.GoldEarned.HasValue)
            {
                goldLabel.text = LevelPanelTexts.GoldEarned(result.GoldEarned.Value, config);
            }

            messageLabel.text = config.ClosingMessage;
            messageLabel.gameObject.SetActive(result.HasClosingMessage);

            lossArt.SetActive(!result.IsWin);
            lifeLostLabel.text = config.LifeLostLabel;
            lifeLost.SetActive(result.LostLife);

            nextLabel.text = config.NextButtonLabel;
            winRestartLabel.text = config.RestartButtonLabel;
            lossRestartLabel.text = config.RestartButtonLabel;
            nextButton.gameObject.SetActive(result.HasNext);
            winRestartButton.gameObject.SetActive(result.IsWin && result.HasRestart);
            lossRestartButton.gameObject.SetActive(!result.IsWin && result.HasRestart);

            gameObject.SetActive(true);
            OverlayPop.Play(this, group, panel, config);
        }

        /// <summary>Closes the panel, stopping its pop.</summary>
        public void Hide()
        {
            OverlayPop.Rest(this, group, panel);
            gameObject.SetActive(false);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, group, nameof(ResultOverlay), nameof(group));
            UiParts.AddIfMissing(missing, panel, nameof(ResultOverlay), nameof(panel));
            UiParts.AddIfMissing(missing, titleLabel, nameof(ResultOverlay), nameof(titleLabel));
            UiParts.AddIfMissing(missing, goldRow, nameof(ResultOverlay), nameof(goldRow));
            UiParts.AddIfMissing(missing, goldLabel, nameof(ResultOverlay), nameof(goldLabel));
            UiParts.AddIfMissing(missing, messageLabel, nameof(ResultOverlay), nameof(messageLabel));
            UiParts.AddIfMissing(missing, lossArt, nameof(ResultOverlay), nameof(lossArt));
            UiParts.AddIfMissing(missing, lifeLost, nameof(ResultOverlay), nameof(lifeLost));
            UiParts.AddIfMissing(missing, lifeLostLabel, nameof(ResultOverlay), nameof(lifeLostLabel));
            UiParts.AddIfMissing(missing, nextButton, nameof(ResultOverlay), nameof(nextButton));
            UiParts.AddIfMissing(missing, nextLabel, nameof(ResultOverlay), nameof(nextLabel));
            UiParts.AddIfMissing(missing, winRestartButton, nameof(ResultOverlay), nameof(winRestartButton));
            UiParts.AddIfMissing(missing, winRestartLabel, nameof(ResultOverlay), nameof(winRestartLabel));
            UiParts.AddIfMissing(missing, lossRestartButton, nameof(ResultOverlay), nameof(lossRestartButton));
            UiParts.AddIfMissing(missing, lossRestartLabel, nameof(ResultOverlay), nameof(lossRestartLabel));
            UiParts.AddIfMissing(missing, menuButton, nameof(ResultOverlay), nameof(menuButton));
        }

        private void OnEnable()
        {
            UiParts.Listen(nextButton, OnNextClicked);
            UiParts.Listen(winRestartButton, OnRestartClicked);
            UiParts.Listen(lossRestartButton, OnRestartClicked);
            UiParts.Listen(menuButton, OnMenuClicked);
        }

        private void OnDisable()
        {
            OverlayPop.Rest(this, group, panel);
            UiParts.Unlisten(nextButton, OnNextClicked);
            UiParts.Unlisten(winRestartButton, OnRestartClicked);
            UiParts.Unlisten(lossRestartButton, OnRestartClicked);
            UiParts.Unlisten(menuButton, OnMenuClicked);
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        private void OnNextClicked() => NextRequested?.Invoke();

        private void OnRestartClicked() => RestartRequested?.Invoke();

        private void OnMenuClicked() => MenuRequested?.Invoke();
    }
}
