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
    /// The level's HUD in the top band (Module 40, laid out anew by the Module
    /// 45 follow-up): Menu at the left and Restart at the right; centred, a
    /// ribbon with "Level N" or "Free Play", the timer with the hourglass
    /// under it, and under that the gold pill and, in the campaign, the lives
    /// pill (D58) — with the lives pill hidden, the gold pill centres in its
    /// row. Shows the <see cref="LevelUiState"/> and the
    /// time it is given and raises an event for each button; it holds no
    /// rules, and neither pill can be pressed.
    /// </summary>
    /// <remarks>
    /// <para><b>Timer.</b> The text and its colour change only when the
    /// displayed second does, so a frame allocates nothing; the colour
    /// follows the displayed second (<see cref="LevelHudTexts.IsTimerWarning"/>).</para>
    /// <para><b>Time bonus.</b> Added seconds show beside the timer, rising
    /// and fading, while the timer pulses once. The tweens run on unscaled
    /// time with this view as their id, and are killed when it is disabled
    /// or destroyed, when the timer is hidden, and by
    /// <see cref="CancelTimeBonus"/>.</para>
    /// </remarks>
    public sealed class LevelHud : MonoBehaviour
    {
        [Tooltip("The Button Round with the home icon: asks to leave the level.")]
        [SerializeField] private Button menuButton;

        [Tooltip("The Button Round with the return icon: asks to restart the level.")]
        [SerializeField] private Button restartButton;

        [Tooltip("The timer pill: hidden for a level without a countdown, pulsed when time is added.")]
        [SerializeField] private RectTransform timer;

        [SerializeField] private TMP_Text timerLabel;

        [Tooltip("The added seconds beside the timer; saved hidden at its resting place.")]
        [SerializeField] private RectTransform timeBonus;

        [SerializeField] private TMP_Text timeBonusLabel;

        [Tooltip("'Level N' in the campaign, 'Free Play' in free play.")]
        [SerializeField] private TMP_Text levelLabel;

        [SerializeField] private TMP_Text goldLabel;

        [Tooltip("The lives pill: hidden in free play (D58).")]
        [SerializeField] private GameObject livesPill;

        [Tooltip("The number inside the heart.")]
        [SerializeField] private TMP_Text livesCountLabel;

        [Tooltip("'Full', or the time to the next life.")]
        [SerializeField] private TMP_Text livesStatusLabel;

        private Vector2 bonusRestPosition;
        private bool hasBonusRest;
        private int shownSeconds = -1;

        /// <summary>Raised when Menu is pressed.</summary>
        public event Action MenuRequested;

        /// <summary>Raised when Restart is pressed.</summary>
        public event Action RestartRequested;

        /// <summary>Shows the level label, the gold and, in the campaign, the lives of <paramref name="state"/>.</summary>
        public void Show(LevelUiState state, UiConfig config)
        {
            levelLabel.text = LevelHudTexts.LevelLabel(state.IsFreePlay, state.LevelNumber, config);
            goldLabel.text = HomeTexts.Gold(state.Gold, config);
            livesPill.SetActive(!state.IsFreePlay);
            livesCountLabel.text = HomeTexts.LivesCount(state.Lives, config);
            livesStatusLabel.text = LevelHudTexts.LivesStatus(state.NextLifeSeconds, config);
        }

        /// <summary>Shows the timer with <paramref name="remainingSeconds"/>, rounded up. Cheap to call every frame.</summary>
        public void ShowTime(float remainingSeconds, UiConfig config)
        {
            if (!timer.gameObject.activeSelf)
            {
                timer.gameObject.SetActive(true);
            }

            var whole = TimeFormat.WholeSeconds(remainingSeconds);
            if (whole == shownSeconds)
            {
                return;
            }

            shownSeconds = whole;
            timerLabel.text = LevelHudTexts.Timer(remainingSeconds, config);
            timerLabel.color = LevelHudTexts.IsTimerWarning(remainingSeconds, config) ? config.TimerWarningColor : config.TimerColor;
        }

        /// <summary>Hides the timer, for a level without a countdown, and stops a time bonus that is showing.</summary>
        public void HideTime()
        {
            CancelTimeBonus();
            timer.gameObject.SetActive(false);
            shownSeconds = -1;
        }

        /// <summary>
        /// Shows <paramref name="seconds"/> beside the timer, rising and
        /// fading, and pulses the timer once. A bonus still showing is
        /// replaced.
        /// </summary>
        public void ShowTimeBonus(int seconds, UiConfig config)
        {
            CancelTimeBonus();
            timeBonusLabel.text = LevelHudTexts.TimeBonus(seconds, config);
            timeBonus.gameObject.SetActive(true);

            var rest = BonusRestPosition();
            var color = config.TimeBonusColor;
            var rise = new Vector2(0f, config.TimeBonusRiseUnits);
            DOVirtual.Float(0f, 1f, config.TimeBonusSeconds, t =>
                {
                    timeBonus.anchoredPosition = rest + rise * t;
                    var faded = color;
                    faded.a *= 1f - t;
                    timeBonusLabel.color = faded;
                })
                .SetEase(config.TimeBonusEase)
                .SetUpdate(true)
                .SetId(this)
                .OnComplete(() => timeBonus.gameObject.SetActive(false));

            // Linear time: the sine is the pulse's whole shape.
            var pulse = config.TimerPulseScale - 1f;
            DOVirtual.Float(0f, 1f, config.TimerPulseSeconds, t =>
                {
                    var scale = 1f + pulse * Mathf.Sin(Mathf.PI * t);
                    timer.localScale = new Vector3(scale, scale, 1f);
                })
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetId(this)
                .OnComplete(() => timer.localScale = Vector3.one);
        }

        /// <summary>Stops a time bonus that is showing and hides it, leaving the timer at rest.</summary>
        public void CancelTimeBonus()
        {
            DOTween.Kill(this);
            if (timeBonus != null)
            {
                timeBonus.anchoredPosition = BonusRestPosition();
                timeBonus.gameObject.SetActive(false);
            }

            if (timer != null)
            {
                timer.localScale = Vector3.one;
            }
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, menuButton, nameof(LevelHud), nameof(menuButton));
            UiParts.AddIfMissing(missing, restartButton, nameof(LevelHud), nameof(restartButton));
            UiParts.AddIfMissing(missing, timer, nameof(LevelHud), nameof(timer));
            UiParts.AddIfMissing(missing, timerLabel, nameof(LevelHud), nameof(timerLabel));
            UiParts.AddIfMissing(missing, timeBonus, nameof(LevelHud), nameof(timeBonus));
            UiParts.AddIfMissing(missing, timeBonusLabel, nameof(LevelHud), nameof(timeBonusLabel));
            UiParts.AddIfMissing(missing, levelLabel, nameof(LevelHud), nameof(levelLabel));
            UiParts.AddIfMissing(missing, goldLabel, nameof(LevelHud), nameof(goldLabel));
            UiParts.AddIfMissing(missing, livesPill, nameof(LevelHud), nameof(livesPill));
            UiParts.AddIfMissing(missing, livesCountLabel, nameof(LevelHud), nameof(livesCountLabel));
            UiParts.AddIfMissing(missing, livesStatusLabel, nameof(LevelHud), nameof(livesStatusLabel));
        }

        private void OnEnable()
        {
            UiParts.Listen(menuButton, OnMenuClicked);
            UiParts.Listen(restartButton, OnRestartClicked);
        }

        private void OnDisable()
        {
            CancelTimeBonus();
            UiParts.Unlisten(menuButton, OnMenuClicked);
            UiParts.Unlisten(restartButton, OnRestartClicked);
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        /// <summary>Where the time bonus rests: its place on the prefab, taken the first time it is needed, before any tween moves it.</summary>
        private Vector2 BonusRestPosition()
        {
            if (!hasBonusRest)
            {
                bonusRestPosition = timeBonus.anchoredPosition;
                hasBonusRest = true;
            }

            return bonusRestPosition;
        }

        private void OnMenuClicked() => MenuRequested?.Invoke();

        private void OnRestartClicked() => RestartRequested?.Invoke();
    }
}
