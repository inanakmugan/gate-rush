using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where the fade between the front end and a level is (Module 47): idle,
    /// covering the screen, or revealing it again. Plain code with no scene,
    /// so the order of the two actions and the busy flag are tested on their
    /// own; <see cref="ScreenFade"/> drives it from its tweens.
    /// </summary>
    /// <remarks>
    /// <para>The midpoint action runs exactly once, when the screen is covered,
    /// and the done action after it, when the fade has lifted.
    /// <see cref="IsBusy"/> is true from <see cref="TryStart"/> until then;
    /// a start while busy is refused and its actions never run.</para>
    /// <para>The phase moves on before each action runs, so an action that
    /// throws still leaves the sequence able to finish: the fade lifts and
    /// input is never held for good.</para>
    /// </remarks>
    public sealed class ScreenFadeSequence
    {
        private Action atMidpoint;
        private Action onDone;

        /// <summary>Where the fade is.</summary>
        public ScreenFadePhase Phase { get; private set; } = ScreenFadePhase.Idle;

        /// <summary>True from <see cref="TryStart"/> until the fade has lifted or was abandoned.</summary>
        public bool IsBusy => Phase != ScreenFadePhase.Idle;

        /// <summary>
        /// Starts covering the screen, keeping <paramref name="midpoint"/> for
        /// when it is covered and <paramref name="done"/>, which may be null,
        /// for when the fade has lifted. False, with nothing kept, while busy.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="midpoint"/> is null.</exception>
        public bool TryStart(Action midpoint, Action done)
        {
            if (midpoint == null)
            {
                throw new ArgumentNullException(nameof(midpoint));
            }

            if (IsBusy)
            {
                return false;
            }

            atMidpoint = midpoint;
            onDone = done;
            Phase = ScreenFadePhase.Covering;
            return true;
        }

        /// <summary>The screen is covered: runs the midpoint action and starts revealing. Does nothing unless covering.</summary>
        public void Covered()
        {
            if (Phase != ScreenFadePhase.Covering)
            {
                return;
            }

            var action = atMidpoint;
            atMidpoint = null;
            Phase = ScreenFadePhase.Revealing;
            action();
        }

        /// <summary>The fade has lifted: goes idle, then runs the done action. Does nothing unless revealing.</summary>
        public void Revealed()
        {
            if (Phase != ScreenFadePhase.Revealing)
            {
                return;
            }

            var action = onDone;
            onDone = null;
            Phase = ScreenFadePhase.Idle;
            action?.Invoke();
        }

        /// <summary>
        /// Goes idle at once and drops whatever has not run: the fade is being
        /// destroyed with the scene, whose views the actions would reach.
        /// </summary>
        public void Abandon()
        {
            atMidpoint = null;
            onDone = null;
            Phase = ScreenFadePhase.Idle;
        }
    }

    /// <summary>Where a <see cref="ScreenFadeSequence"/> is.</summary>
    public enum ScreenFadePhase
    {
        /// <summary>No fade: the screen is clear.</summary>
        Idle,

        /// <summary>Fading to the background colour; the screen has not changed yet.</summary>
        Covering,

        /// <summary>The screen has changed; the fade is lifting.</summary>
        Revealing
    }
}
