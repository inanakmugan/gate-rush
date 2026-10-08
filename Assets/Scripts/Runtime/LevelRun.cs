using System;

namespace GateRush.Runtime
{
    /// <summary>How a level being played has ended, if it has.</summary>
    public enum LevelOutcome
    {
        /// <summary>Still being played.</summary>
        None,

        /// <summary>The board reads solved.</summary>
        Won,

        /// <summary>The countdown reached zero.</summary>
        Lost
    }

    /// <summary>
    /// One attempt at a level: the <see cref="LevelSession"/>, its optional
    /// <see cref="Countdown"/>, and the outcome. Decides the end of the level
    /// — whichever of winning and running out of time comes first decides,
    /// and the other never follows — and feeds time bonuses (M10) into the
    /// countdown.
    /// </summary>
    /// <remarks>
    /// Subscribes to the session and the countdown it is given; the three are
    /// created together, for one level, and are discarded together. Neither is
    /// shared with another run. A run replaced by another level's is simply
    /// dropped; one abandoned with nothing to replace it — the player went to
    /// the menu (Module 23) — is <see cref="Release"/>d, so nothing it was
    /// listening to can decide an outcome afterwards.
    /// </remarks>
    public sealed class LevelRun
    {
        /// <summary>A run of <paramref name="session"/>, not yet started.</summary>
        /// <param name="session">The level being played.</param>
        /// <param name="countdown">
        /// The level's countdown, or null to play without one — a level whose
        /// budget is not positive stays testable.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
        public LevelRun(LevelSession session, Countdown countdown)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Countdown = countdown;

            Session.StateChanged += DecideWin;
            Session.TimeBonusEarned += AddBonus;
            if (Countdown != null)
            {
                Countdown.Expired += DecideLoss;
            }
        }

        /// <summary>The level being played.</summary>
        public LevelSession Session { get; }

        /// <summary>The countdown, or null when the level runs without one.</summary>
        public Countdown Countdown { get; }

        /// <summary>How the level has ended; <see cref="LevelOutcome.None"/> while it is played.</summary>
        public LevelOutcome Outcome { get; private set; }

        /// <summary>Starts the countdown. Call once, when the level has loaded.</summary>
        public void Start()
        {
            Countdown?.Start();
            DecideWin();
        }

        /// <summary>Runs the countdown down by a frame's unscaled time. No effect once the level has ended.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Outcome == LevelOutcome.None)
            {
                Countdown?.Tick(deltaSeconds);
            }
        }

        /// <summary>
        /// Back to the level's initial state with the whole budget running,
        /// whatever the outcome was.
        /// </summary>
        public void Restart()
        {
            Outcome = LevelOutcome.None;
            Countdown?.Reset();
            Session.Restart();
            Countdown?.Start();
        }

        /// <summary>
        /// Abandons the run: stops the countdown and stops listening to the
        /// session and the countdown, so no later move, bonus or tick changes
        /// <see cref="Outcome"/> or the remaining time. Safe to call more than
        /// once. The run is not to be used afterwards.
        /// </summary>
        public void Release()
        {
            Session.StateChanged -= DecideWin;
            Session.TimeBonusEarned -= AddBonus;
            if (Countdown != null)
            {
                Countdown.Expired -= DecideLoss;
                Countdown.Stop();
            }
        }

        private void DecideWin()
        {
            if (Outcome != LevelOutcome.None || !Session.IsSolved)
            {
                return;
            }

            Outcome = LevelOutcome.Won;
            Countdown?.Stop();
        }

        private void DecideLoss()
        {
            if (Outcome == LevelOutcome.None)
            {
                Outcome = LevelOutcome.Lost;
            }
        }

        private void AddBonus(int seconds)
        {
            if (Outcome == LevelOutcome.None)
            {
                Countdown?.AddBonus(seconds);
            }
        }
    }
}
