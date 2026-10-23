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
    /// One joker's button on the level's joker bar (Modules 40, 43 and 46): its
    /// icon, a pill under it with the count, the price beside the coin, or the
    /// level a locked joker opens at; greyed while it cannot be pressed, its icon
    /// dimmed under a lock while locked; drawn raised with a ring around it
    /// while its joker is selected (D67); and pulsing with a glow behind it
    /// while marked (D76). Shows the <see cref="JokerButton"/>
    /// <see cref="JokerButtons"/> decided and raises <see cref="Pressed"/>;
    /// what a press does is the bootstrap's.
    /// </summary>
    /// <remarks>
    /// The pulse runs on unscaled time, carries this component as its id, and
    /// is killed when the mark goes and when this component is disabled or
    /// destroyed; a button still marked when enabled again pulses again.
    /// </remarks>
    public sealed class JokerBarButton : MonoBehaviour
    {
        // The scale and the opacity a look has at rest: not tunables.
        private const float RestScale = 1f;
        private const float OpaqueAlpha = 1f;

        [SerializeField] private Button button;

        [Tooltip("Holds the glow, the ring and the button; raised by Selected Raise while the joker is selected.")]
        [SerializeField] private RectTransform lift;

        [Tooltip("The ring around the button, shown while the joker is selected.")]
        [SerializeField] private GameObject ring;

        [Tooltip("The joker's icon: the Icon Color while pressable, the Disabled Joker Icon Color while greyed, at Locked Icon Alpha while locked.")]
        [SerializeField] private Image icon;

        [Tooltip("The lock over the icon, shown while the joker is locked in the campaign (D76).")]
        [SerializeField] private GameObject lockIcon;

        [Tooltip("The glow behind the button, shown and pulsing while the joker is marked (D76).")]
        [SerializeField] private Image glow;

        [Tooltip("The coin hanging off the pill's left end beside a price; hidden for a count and an unlock level.")]
        [SerializeField] private GameObject coin;

        [Tooltip("The pill's text: the count, the price or the unlock level.")]
        [SerializeField] private TMP_Text badgeLabel;

        [Tooltip("How far the button rises while selected, in canvas units; written by the UI builder.")]
        [SerializeField] private float selectedRaise;

        [Tooltip("How far the pill's text keeps from both of its ends, centring it, in canvas units; written by the UI builder.")]
        [SerializeField] private float labelInset;

        [Tooltip("Where the pill's text starts beside the coin when it shows a price, in from the pill's left end, in canvas units; written by the UI builder.")]
        [SerializeField] private float priceLabelLeft;

        // The mark last shown and the config it was shown with: the pulse
        // reads them while it runs, and OnEnable restarts it from them.
        private bool isMarked;
        private bool isPulsing;
        private UiConfig pulseConfig;

        /// <summary>Raised when the button is pressed.</summary>
        public event Action Pressed;

        /// <summary>
        /// Shows <paramref name="state"/>: the pill's text — centred, or beside
        /// the coin for a price — the price in
        /// <see cref="UiConfig.ShortPriceColor"/> while the balance is short of
        /// it, the button greyed while it cannot be pressed, the icon dimmed
        /// under the lock while locked, raised with its ring while
        /// <paramref name="isSelected"/>, and pulsing while marked.
        /// </summary>
        public void Show(JokerButton state, bool isSelected, UiConfig config)
        {
            var isPrice = state.Badge == JokerBadge.Price;
            badgeLabel.text = LevelHudTexts.JokerBadge(state, config);
            badgeLabel.color = state.IsShortOfGold ? config.ShortPriceColor : config.JokerBadgeTextColor;
            coin.SetActive(isPrice);
            var text = badgeLabel.rectTransform;
            text.offsetMin = new Vector2(isPrice ? priceLabelLeft : labelInset, text.offsetMin.y);
            button.interactable = state.IsInteractable;

            var iconColor = state.IsInteractable ? config.IconColor : config.DisabledJokerIconColor;
            iconColor.a *= state.IsLocked ? config.LockedIconAlpha : OpaqueAlpha;
            icon.color = iconColor;
            lockIcon.SetActive(state.IsLocked);

            ring.SetActive(isSelected);
            lift.anchoredPosition = new Vector2(0f, isSelected ? selectedRaise : 0f);

            pulseConfig = config;
            isMarked = state.IsMarked;
            if (isMarked && !isPulsing)
            {
                StartPulse();
            }
            else if (!isMarked && isPulsing)
            {
                StopPulse();
            }
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, button, nameof(JokerBarButton), nameof(button));
            UiParts.AddIfMissing(missing, lift, nameof(JokerBarButton), nameof(lift));
            UiParts.AddIfMissing(missing, ring, nameof(JokerBarButton), nameof(ring));
            UiParts.AddIfMissing(missing, icon, nameof(JokerBarButton), nameof(icon));
            UiParts.AddIfMissing(missing, lockIcon, nameof(JokerBarButton), nameof(lockIcon));
            UiParts.AddIfMissing(missing, glow, nameof(JokerBarButton), nameof(glow));
            UiParts.AddIfMissing(missing, coin, nameof(JokerBarButton), nameof(coin));
            UiParts.AddIfMissing(missing, badgeLabel, nameof(JokerBarButton), nameof(badgeLabel));
        }

        private void OnEnable()
        {
            UiParts.Listen(button, OnClicked);

            // Disabling killed the pulse of a button left marked.
            if (isMarked && pulseConfig != null)
            {
                StartPulse();
            }
        }

        private void OnDisable()
        {
            UiParts.Unlisten(button, OnClicked);
            StopPulse();
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        /// <summary>
        /// Shows the glow and pulses: one looping tween eases the button up to
        /// <see cref="UiConfig.MarkedPulseScale"/> and back while the glow
        /// brightens from its resting opacity to full and back.
        /// </summary>
        private void StartPulse()
        {
            DOTween.Kill(this);
            isPulsing = true;
            glow.gameObject.SetActive(true);
            ApplyPulse(0f);
            DOVirtual.Float(0f, 1f, pulseConfig.MarkedPulseSeconds, ApplyPulse)
                .SetEase(pulseConfig.MarkedPulseEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetId(this);
        }

        /// <summary>Kills the pulse and leaves the button at rest, the glow hidden. Keeps the mark, for OnEnable.</summary>
        private void StopPulse()
        {
            DOTween.Kill(this);
            isPulsing = false;
            if (button != null)
            {
                button.transform.localScale = Vector3.one * RestScale;
            }

            if (glow != null)
            {
                glow.gameObject.SetActive(false);
            }
        }

        private void ApplyPulse(float strength)
        {
            var scale = Mathf.Lerp(RestScale, pulseConfig.MarkedPulseScale, strength);
            button.transform.localScale = new Vector3(scale, scale, RestScale);

            var color = pulseConfig.MarkedGlowColor;
            color.a *= Mathf.Lerp(pulseConfig.MarkedGlowMinAlpha, OpaqueAlpha, strength);
            glow.color = color;
        }

        private void OnClicked() => Pressed?.Invoke();
    }
}
