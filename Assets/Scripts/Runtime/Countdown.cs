using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// The level countdown: a time budget that runs down while the level is
    /// played and ends it at zero. Time stays outside <c>Core</c> (D12, Core
    /// concept 5): nothing here enters <c>BoardState</c>, and the rules never
    /// read it.
    /// </summary>
    /// <remarks>
    /// Driven from outside by <see cref="Tick"/> with the frame's unscaled time,
    /// so it runs the same whatever <c>Time.timeScale</c> is and can be tested
    /// without a clock.
    /// </remarks>
    public sealed class Countdown
    {
        private readonly float budgetSeconds;

        /// <summary>A stopped countdown holding the whole budget.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="budgetSeconds"/> is not positive. A level without a
        /// usable budget runs without a countdown instead of with one that is
        /// already over.
        /// </exception>
        public Countdown(float budgetSeconds)
        {
            if (!(budgetSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(budgetSeconds), budgetSeconds, "The time budget must be positive.");
            }

            this.budgetSeconds = budgetSeconds;
            RemainingSeconds = budgetSeconds;
        }

        /// <summary>Raised once, when the remaining time reaches zero.</summary>
        public event Action Expired;

        /// <summary>Seconds left. Never below zero.</summary>
        public float RemainingSeconds { get; private set; }

        /// <summary>
        /// The remaining time in whole seconds, rounded up: the display reads 1
        /// until the time is truly gone, and 0 only once it has expired. The
        /// same rounding as the HUD's (<see cref="TimeFormat.WholeSeconds"/>).
        /// </summary>
        public int WholeSecondsRemaining => TimeFormat.WholeSeconds(RemainingSeconds);

        /// <summary>True between <see cref="Start"/> and <see cref="Stop"/> or expiry.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>True once the remaining time has reached zero, until <see cref="Reset"/>.</summary>
        public bool HasExpired { get; private set; }

        /// <summary>Starts or resumes the countdown. No effect once it has expired.</summary>
        public void Start()
        {
            if (!HasExpired)
            {
                IsRunning = true;
            }
        }

        /// <summary>Stops the countdown, freezing the remaining time.</summary>
        public void Stop()
        {
            IsRunning = false;
        }

        /// <summary>
        /// Runs the countdown down by <paramref name="deltaSeconds"/>. No effect
        /// unless it is running. On reaching zero — however far past zero the
        /// tick would take it — it holds at zero, stops, and raises
        /// <see cref="Expired"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaSeconds"/> is negative.</exception>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds), deltaSeconds, "Time does not run backward.");
            }

            if (!IsRunning)
            {
                return;
            }

            RemainingSeconds -= deltaSeconds;
            if (RemainingSeconds > 0f)
            {
                return;
            }

            RemainingSeconds = 0f;
            IsRunning = false;
            HasExpired = true;
            Expired?.Invoke();
        }

        /// <summary>
        /// Adds a time-bonus block's seconds (M10). No effect once expired: a
        /// level already lost is not revived.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is negative.</exception>
        public void AddBonus(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "A time bonus may not be negative.");
            }

            if (!HasExpired)
            {
                RemainingSeconds += seconds;
            }
        }

        /// <summary>Back to the whole budget, stopped and not expired — for a restart.</summary>
        public void Reset()
        {
            RemainingSeconds = budgetSeconds;
            IsRunning = false;
            HasExpired = false;
        }
    }
}
