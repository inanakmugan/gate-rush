using System.Collections.Generic;
using DG.Tweening;
using GateRush.Runtime;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// Module 47, pass 3: a button's press, a tab's fade and pop, an overlay's
    /// close and the fade between the front end and a level. Every time is
    /// unscaled; every motion is meant to be short and subtle.
    /// </summary>
    public sealed partial class UiConfig
    {
        [Header("Button presses (Module 47; unscaled time)")]
        [Tooltip("The scale a button's face shrinks to while pressed: from 0.8 to below 1.")]
        [SerializeField] private float pressScale = 0.92f;

        [Tooltip("Seconds the face takes to shrink on a press: above 0, at most 0.3.")]
        [SerializeField] private float pressSeconds = 0.06f;

        [SerializeField] private Ease pressEase = Ease.OutQuad;

        [Tooltip("Seconds the face takes to return on release or when the pointer leaves: above 0, at most 0.5.")]
        [SerializeField] private float releaseSeconds = 0.18f;

        [Tooltip("How the face returns; with OutBack, Release Overshoot sets how far it springs past its size.")]
        [SerializeField] private Ease releaseEase = Ease.OutBack;

        [Tooltip("The overshoot of the release's ease (DOTween's OutBack overshoot): from 0 to 3. 1.2 springs less than 1 percent past rest.")]
        [SerializeField] private float releaseOvershoot = 1.2f;

        [Header("Tabs (Module 47; unscaled time)")]
        [Tooltip("Seconds the new page takes to fade in over the old one: above 0, at most 0.5.")]
        [SerializeField] private float tabFadeSeconds = 0.12f;

        [SerializeField] private Ease tabFadeEase = Ease.OutQuad;

        [Tooltip("The scale the newly selected tab's raised plate pops to, once: from 1 to 1.3.")]
        [SerializeField] private float tabPopScale = 1.06f;

        [Tooltip("Seconds the tab's pop takes, up and back: above 0, at most 0.5.")]
        [SerializeField] private float tabPopSeconds = 0.18f;

        [Header("Overlay close (Module 47; unscaled time)")]
        [Tooltip("Seconds an overlay takes to close, fading while its panel shrinks: above 0, at most 0.5.")]
        [SerializeField] private float overlayCloseSeconds = 0.12f;

        [Tooltip("The scale the panel shrinks to as its overlay closes: from 0.8 to 1.")]
        [SerializeField] private float overlayCloseScale = 0.95f;

        [SerializeField] private Ease overlayCloseEase = Ease.InQuad;

        [Header("Screen fade between the front end and a level (Module 47; unscaled time)")]
        [Tooltip("Seconds the screen takes to fade to the level's background colour: above 0, at most 1.")]
        [SerializeField] private float screenFadeInSeconds = 0.18f;

        [Tooltip("Seconds the fade takes to lift once the screen has changed: above 0, at most 1.")]
        [SerializeField] private float screenFadeOutSeconds = 0.22f;

        [SerializeField] private Ease screenFadeEase = Ease.InOutQuad;

        [Tooltip("How far above the screen's canvas the fade sorts: above Coin Flight Sorting Offset, so it covers everything; at most 1000.")]
        [SerializeField] private int screenFadeSortingOffset = 50;

        /// <summary>The scale a pressed button's face shrinks to.</summary>
        public float PressScale => pressScale;

        /// <summary>Seconds a press's shrink takes.</summary>
        public float PressSeconds => pressSeconds;

        /// <summary>How a press shrinks the face.</summary>
        public Ease PressEase => pressEase;

        /// <summary>Seconds a release's return takes.</summary>
        public float ReleaseSeconds => releaseSeconds;

        /// <summary>How a release returns the face.</summary>
        public Ease ReleaseEase => releaseEase;

        /// <summary>The overshoot of <see cref="ReleaseEase"/>.</summary>
        public float ReleaseOvershoot => releaseOvershoot;

        /// <summary>Seconds a new page's fade takes.</summary>
        public float TabFadeSeconds => tabFadeSeconds;

        /// <summary>How a new page fades in.</summary>
        public Ease TabFadeEase => tabFadeEase;

        /// <summary>The scale a newly selected tab pops to.</summary>
        public float TabPopScale => tabPopScale;

        /// <summary>Seconds a tab's pop takes.</summary>
        public float TabPopSeconds => tabPopSeconds;

        /// <summary>Seconds an overlay's close takes.</summary>
        public float OverlayCloseSeconds => overlayCloseSeconds;

        /// <summary>The scale a closing overlay's panel shrinks to.</summary>
        public float OverlayCloseScale => overlayCloseScale;

        /// <summary>How an overlay closes.</summary>
        public Ease OverlayCloseEase => overlayCloseEase;

        /// <summary>The fade between the front end and a level, for the bootstrap (<see cref="IFrontEnd.ScreenFadeSettings"/>).</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is out of range; see <see cref="Problems"/>.</exception>
        public ScreenFadeSettings CreateScreenFadeSettings() =>
            new ScreenFadeSettings(screenFadeInSeconds, screenFadeOutSeconds, screenFadeEase, screenFadeSortingOffset);

        // The bounds of pass 3's values. Not tunables; the values inside them are.
        private const float MinPressScale = 0.8f;
        private const float MaxPressSeconds = 0.3f;
        private const float MaxShortSeconds = 0.5f;
        private const float MaxOvershoot = 3f;
        private const float MaxTabPopScale = 1.3f;
        private const float MinCloseScale = 0.8f;
        private const float MaxScreenFadeSeconds = 1f;
        private const int MaxScreenFadeSortingOffset = 1000;

        /// <summary>The constraints on Module 47's presses, tabs, overlay close and screen fade, each message naming its field.</summary>
        private void AddMotionProblems(List<string> problems)
        {
            if (!(pressScale >= MinPressScale && pressScale < 1f))
            {
                problems.Add($"{name}: Press Scale must be from {MinPressScale} to below 1.");
            }

            AddIfOutOfSeconds(problems, pressSeconds, MaxPressSeconds, "Press Seconds");
            AddIfOutOfSeconds(problems, releaseSeconds, MaxShortSeconds, "Release Seconds");

            if (!(releaseOvershoot >= 0f && releaseOvershoot <= MaxOvershoot))
            {
                problems.Add($"{name}: Release Overshoot must be from 0 to {MaxOvershoot}.");
            }

            AddIfOutOfSeconds(problems, tabFadeSeconds, MaxShortSeconds, "Tab Fade Seconds");

            if (!(tabPopScale >= 1f && tabPopScale <= MaxTabPopScale))
            {
                problems.Add($"{name}: Tab Pop Scale must be from 1 to {MaxTabPopScale}.");
            }

            AddIfOutOfSeconds(problems, tabPopSeconds, MaxShortSeconds, "Tab Pop Seconds");
            AddIfOutOfSeconds(problems, overlayCloseSeconds, MaxShortSeconds, "Overlay Close Seconds");

            if (!(overlayCloseScale >= MinCloseScale && overlayCloseScale <= 1f))
            {
                problems.Add($"{name}: Overlay Close Scale must be from {MinCloseScale} to 1.");
            }

            AddIfOutOfSeconds(problems, screenFadeInSeconds, MaxScreenFadeSeconds, "Screen Fade In Seconds");
            AddIfOutOfSeconds(problems, screenFadeOutSeconds, MaxScreenFadeSeconds, "Screen Fade Out Seconds");

            if (!(screenFadeSortingOffset > coinFlightSortingOffset && screenFadeSortingOffset <= MaxScreenFadeSortingOffset))
            {
                problems.Add($"{name}: Screen Fade Sorting Offset must be above Coin Flight Sorting Offset ({coinFlightSortingOffset}) and at most {MaxScreenFadeSortingOffset}, so the fade covers everything.");
            }
        }

        private void AddIfOutOfSeconds(List<string> problems, float seconds, float max, string shownName)
        {
            if (!(seconds > 0f && seconds <= max))
            {
                problems.Add($"{name}: {shownName} must be above 0 and at most {max}.");
            }
        }
    }
}
