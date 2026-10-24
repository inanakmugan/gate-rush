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
    /// <para><b>Size.</b> The panel takes the size the UI builder wrote for
    /// the result's <see cref="ResultPanelShape"/> (the UI polish pass): the
    /// middle sits the same gap under the title and over the buttons, and a
    /// win with nothing in its middle has a short panel wide enough for all
    /// three buttons.</para>
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

        [Tooltip("The gold row's coin: spins in on a win, and is where a campaign win's coins fly from (Module 47).")]
        [SerializeField] private RectTransform goldCoin;

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

        [Tooltip("The panel's size for a win with the gold row or the closing message, in canvas units; written by the UI builder.")]
        [SerializeField] private Vector2 rewardPanelSize;

        [Tooltip("The panel's size for a win with nothing in the middle, wide enough for Next, Restart and Menu; written by the UI builder.")]
        [SerializeField] private Vector2 compactPanelSize;

        [Tooltip("The panel's size for a loss that cost nothing, the hourglass in the middle; written by the UI builder.")]
        [SerializeField] private Vector2 lossPanelSize;

        [Tooltip("The panel's size for a loss that cost a life, the hourglass and the heart in the middle; written by the UI builder.")]
        [SerializeField] private Vector2 lifeLostPanelSize;

        /// <summary>Raised when Next is pressed.</summary>
        public event Action NextRequested;

        /// <summary>Raised when either Restart is pressed.</summary>
        public event Action RestartRequested;

        /// <summary>Raised when Menu is pressed.</summary>
        public event Action MenuRequested;

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/>.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>The gold row's coin: where a campaign win's coins fly from.</summary>
        public RectTransform GoldCoin => goldCoin;

        /// <summary>Opens the panel showing <paramref name="result"/>, at its shape's size, with the pop.</summary>
        public void Show(LevelResult result, UiConfig config)
        {
            panel.sizeDelta = SizeOf(ResultPanelShapes.ShapeOf(result));
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
            RestWinPops();
            OverlayPop.Play(this, group, panel, config);
            if (result.IsWin)
            {
                PlayWinPops(result.GoldEarned.HasValue, config);
            }
        }

        /// <summary>Closes the panel, stopping its pop.</summary>
        public void Hide()
        {
            OverlayPop.Rest(this, group, panel);
            RestWinPops();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// A win's pops once the panel's own has played (Module 47): the title
        /// grows and shrinks back once, and, when the gold row shows, its coin
        /// spins in from nothing. On unscaled time, carrying this view as its
        /// id, so <see cref="Hide"/>, a disable and a destroy stop them.
        /// </summary>
        private void PlayWinPops(bool hasGold, UiConfig config)
        {
            var title = titleLabel.rectTransform;
            var grow = config.ResultTitlePopScale - 1f;
            var after = config.OverlayPopSeconds;
            DOVirtual.Float(0f, 1f, config.ResultTitlePopSeconds, t =>
                {
                    var scale = 1f + grow * Mathf.Sin(Mathf.PI * t);
                    title.localScale = new Vector3(scale, scale, 1f);
                })
                .SetDelay(after)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetId(this);

            if (!hasGold)
            {
                return;
            }

            var coin = goldCoin;
            var turn = config.ResultCoinSpinDegrees;
            coin.localScale = Vector3.zero;
            DOVirtual.Float(0f, 1f, config.ResultCoinSpinSeconds, t =>
                {
                    coin.localScale = new Vector3(t, t, 1f);
                    coin.localRotation = Quaternion.Euler(0f, 0f, turn * (1f - t));
                })
                .SetDelay(after)
                .SetEase(config.ResultCoinSpinEase)
                .SetUpdate(true)
                .SetId(this);
        }

        /// <summary>Leaves the title and the coin at rest. Either may be missing during teardown.</summary>
        private void RestWinPops()
        {
            if (titleLabel != null)
            {
                titleLabel.rectTransform.localScale = Vector3.one;
            }

            if (goldCoin != null)
            {
                goldCoin.localScale = Vector3.one;
                goldCoin.localRotation = Quaternion.identity;
            }
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, group, nameof(ResultOverlay), nameof(group));
            UiParts.AddIfMissing(missing, panel, nameof(ResultOverlay), nameof(panel));
            UiParts.AddIfMissing(missing, titleLabel, nameof(ResultOverlay), nameof(titleLabel));
            UiParts.AddIfMissing(missing, goldRow, nameof(ResultOverlay), nameof(goldRow));
            UiParts.AddIfMissing(missing, goldLabel, nameof(ResultOverlay), nameof(goldLabel));
            UiParts.AddIfMissing(missing, goldCoin, nameof(ResultOverlay), nameof(goldCoin));
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
            AddIfUnsized(missing, rewardPanelSize, nameof(rewardPanelSize));
            AddIfUnsized(missing, compactPanelSize, nameof(compactPanelSize));
            AddIfUnsized(missing, lossPanelSize, nameof(lossPanelSize));
            AddIfUnsized(missing, lifeLostPanelSize, nameof(lifeLostPanelSize));
        }

        // A size the builder has not written yet: a prefab built before the
        // sizes existed would otherwise open as an empty panel.
        private static void AddIfUnsized(List<string> missing, Vector2 size, string field)
        {
            if (!(size.x > 0f && size.y > 0f))
            {
                missing.Add($"{nameof(ResultOverlay)}.{field}");
            }
        }

        private Vector2 SizeOf(ResultPanelShape shape)
        {
            switch (shape)
            {
                case ResultPanelShape.Reward:
                    return rewardPanelSize;
                case ResultPanelShape.Compact:
                    return compactPanelSize;
                case ResultPanelShape.Loss:
                    return lossPanelSize;
                case ResultPanelShape.LifeLost:
                    return lifeLostPanelSize;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, "Not a result panel shape.");
            }
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
            RestWinPops();
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
