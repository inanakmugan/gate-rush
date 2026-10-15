using System;

namespace GateRush.Meta
{
    /// <summary>
    /// The player's lives (Module 30, D59): a stored count and the UTC moment
    /// the wait for the next life started. One life comes back for each full
    /// <see cref="MetaRules.LifeRegenInterval"/> since then, up to
    /// <see cref="MetaRules.LifeCap"/>, also while the game is closed (D20). A
    /// section of <see cref="SaveData"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Time is an argument.</b> Every member that depends on time
    /// takes <c>nowUtc</c>; this class never reads the system clock, so a test
    /// moves a clock instead of waiting (D19). A time that is not of
    /// <see cref="DateTimeKind.Utc"/> throws: a local time would shift every
    /// wait by the time zone.</para>
    /// <para><b>Every member that takes nowUtc settles, the queries
    /// included.</b> It first adds the lives that came back by
    /// <c>nowUtc</c> and moves the wait's start forward by the whole intervals
    /// they took, so the part of an interval already waited is kept. A
    /// <c>nowUtc</c> earlier than the wait's start gives no life and restarts
    /// the wait at <c>nowUtc</c> (D20's guard): setting the clock back can cost
    /// part of an interval, never gain a life. So <see cref="CountAt"/>,
    /// <see cref="IsFullAt"/> and <see cref="UntilNextLifeAt"/> may change
    /// <see cref="StoredCount"/> and <see cref="RegenStartUtc"/>. They must:
    /// a player whose clock ran ahead and who then lost every life could
    /// otherwise never restart the wait, since nothing that changes the lives
    /// can run at 0, and would be locked out until the old start came round
    /// again.</para>
    /// <para><b>At the cap no life is coming back</b>, and
    /// <see cref="RegenStartUtc"/> means nothing until a life is lost, which
    /// starts the wait then.</para>
    /// </remarks>
    public sealed class Lives
    {
        // The wait start of full lives: never read while the lives are full,
        // and a valid UTC time so that it saves and restores like any other.
        private static readonly DateTime FullRegenStartUtc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

        private readonly MetaRules rules;

        /// <summary>
        /// Lives as a save restores them: <paramref name="count"/> held, with
        /// the wait for the next one started at <paramref name="regenStartUtc"/>.
        /// A count above the cap — saved before the cap was lowered — is kept
        /// as the cap.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentException"><paramref name="regenStartUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public Lives(MetaRules rules, int count, DateTime regenStartUtc)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "A count of lives may not be negative.");
            }

            ThrowIfNotUtc(regenStartUtc, nameof(regenStartUtc));
            StoredCount = Math.Min(count, rules.LifeCap);
            RegenStartUtc = regenStartUtc;
        }

        /// <summary>
        /// The count as last settled, for the codec: the lives that came back
        /// since are not in it until a member taking <c>nowUtc</c> settles
        /// them. Never above the cap.
        /// </summary>
        public int StoredCount { get; private set; }

        /// <summary>
        /// When the wait for the next life started, as last settled, for the
        /// codec. Meaningless while <see cref="StoredCount"/> is at the cap.
        /// Always of <see cref="DateTimeKind.Utc"/>.
        /// </summary>
        public DateTime RegenStartUtc { get; private set; }

        /// <summary>The lives of a new player: the cap, with no life coming back.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static Lives Full(MetaRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return new Lives(rules, rules.LifeCap, FullRegenStartUtc);
        }

        /// <summary>How many lives there are at <paramref name="nowUtc"/>, after settling at it.</summary>
        /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public int CountAt(DateTime nowUtc)
        {
            Settle(nowUtc);
            return StoredCount;
        }

        /// <summary>True when the lives are at the cap at <paramref name="nowUtc"/>, after settling at it.</summary>
        /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public bool IsFullAt(DateTime nowUtc) => CountAt(nowUtc) >= rules.LifeCap;

        /// <summary>
        /// How long after <paramref name="nowUtc"/> the next life comes back,
        /// after settling at it; null when the lives are full. Exact to the
        /// tick: never negative and never above one interval, which is what a
        /// wait restarted at <paramref name="nowUtc"/> gives. Rounding is for
        /// whoever shows it.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public TimeSpan? UntilNextLifeAt(DateTime nowUtc)
        {
            if (IsFullAt(nowUtc))
            {
                return null;
            }

            // Settled below the cap, the start is never after nowUtc and less
            // than one interval before it.
            var intervalTicks = rules.LifeRegenInterval.Ticks;
            var waitedTicks = (nowUtc - RegenStartUtc).Ticks;
            return TimeSpan.FromTicks(intervalTicks - waitedTicks % intervalTicks);
        }

        /// <summary>
        /// Loses one life at <paramref name="nowUtc"/>, after settling at it.
        /// Losing one at the cap starts the wait at <paramref name="nowUtc"/>;
        /// below the cap the running wait is kept. False, with nothing but the
        /// settling changed, when there is no life to lose.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public bool TryLose(DateTime nowUtc)
        {
            Settle(nowUtc);
            if (StoredCount == 0)
            {
                return false;
            }

            if (StoredCount >= rules.LifeCap)
            {
                RegenStartUtc = nowUtc;
            }

            StoredCount--;
            return true;
        }

        /// <summary>Sets the lives to the cap at <paramref name="nowUtc"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public void Refill(DateTime nowUtc)
        {
            Settle(nowUtc);
            StoredCount = rules.LifeCap;
        }

        /// <summary>
        /// Adds the lives that came back by <paramref name="nowUtc"/> and moves
        /// the wait's start forward by the whole intervals they took, or
        /// restarts the wait at <paramref name="nowUtc"/> when the clock reads
        /// earlier than its start. Nothing changes at the cap.
        /// </summary>
        private void Settle(DateTime nowUtc)
        {
            ThrowIfNotUtc(nowUtc, nameof(nowUtc));
            if (StoredCount >= rules.LifeCap)
            {
                return;
            }

            if (nowUtc < RegenStartUtc)
            {
                RegenStartUtc = nowUtc;
                return;
            }

            var intervalTicks = rules.LifeRegenInterval.Ticks;
            var wholeIntervals = (nowUtc - RegenStartUtc).Ticks / intervalTicks;
            var gained = (int)Math.Min(wholeIntervals, rules.LifeCap - StoredCount);

            // gained is at most wholeIntervals, so the step lands at or before
            // nowUtc and cannot leave DateTime's range.
            StoredCount += gained;
            RegenStartUtc = RegenStartUtc.AddTicks(gained * intervalTicks);
        }

        private static void ThrowIfNotUtc(DateTime time, string paramName)
        {
            if (time.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException($"The time must be of DateTimeKind.Utc, not {time.Kind}.", paramName);
            }
        }
    }
}
