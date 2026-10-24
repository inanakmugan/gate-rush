using System;
using DG.Tweening;

namespace GateRush.Runtime
{
    /// <summary>
    /// The timing of the fade between the front end and a level (Module 47):
    /// how long the screen takes to cover and to lift, how both ease, and how
    /// far above the screen's canvas the fade sorts. Built from the interface's
    /// configuration and handed to the bootstrap through
    /// <see cref="IFrontEnd.ScreenFadeSettings"/>, so the values stay in
    /// <c>UiConfig</c> while the fade itself lives here.
    /// </summary>
    public readonly struct ScreenFadeSettings
    {
        /// <summary>The settings, checked.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A time is not positive and finite, or the offset is below 1.</exception>
        public ScreenFadeSettings(float inSeconds, float outSeconds, Ease ease, int sortingOffset)
        {
            RequirePositive(inSeconds, nameof(inSeconds));
            RequirePositive(outSeconds, nameof(outSeconds));
            if (sortingOffset < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sortingOffset), sortingOffset, "The fade must sort above the screen's canvas.");
            }

            InSeconds = inSeconds;
            OutSeconds = outSeconds;
            Ease = ease;
            SortingOffset = sortingOffset;
        }

        /// <summary>Seconds the screen takes to fade to the background colour.</summary>
        public float InSeconds { get; }

        /// <summary>Seconds the fade takes to lift once the screen has changed.</summary>
        public float OutSeconds { get; }

        /// <summary>How both halves ease.</summary>
        public Ease Ease { get; }

        /// <summary>How far above the screen's canvas the fade sorts.</summary>
        public int SortingOffset { get; }

        private static void RequirePositive(float seconds, string parameter)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(parameter, seconds, "A fade's time must be positive and finite.");
            }
        }
    }
}
