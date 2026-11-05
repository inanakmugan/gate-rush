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
    /// they took, so the part of an interval already waited is kept. So
    /// <see cref="CountAt"/>, <see cref="IsFullAt"/> and
    /// <see cref="UntilNextLifeAt"/> may change <see cref="StoredCount"/> and
    /// <see cref="RegenStartUtc"/>. They must: a player at 0 lives has nothing
    /// else that settles.</para>
    /// <para><b>Only real elapsed time gives a life (D20's guard).</b> Time is
    /// counted on a clock of its own, kept in memory, that never runs ahead
    /// of real time while the device clock does not: a reading at or after
    /// it moves it to that reading; a reading earlier than it — the device
    /// clock set back — moves it only by the time since the previous reading,
    /// when that moved forward. A clock set back and restored therefore counts
    /// only the time that really passed, and a clock left behind still counts
    /// the time between its readings, so a player at 0 lives is never locked
    /// out. The saved <see cref="RegenStartUtc"/> is a time on that clock and
    /// never moves back; a new instance starts its clock there. A clock set
    /// forward gives what it reads: without a trusted clock nothing can tell
    /// it from time really passed.</para>
    /// <para><b>At the cap no life is coming back</b>, and
    /// <see cref="RegenStartUtc"/> holds the latest time counted until a life
    /// is lost, which starts the wait there — not at an earlier reading of a
    /// clock set back.</para>
    /// </remarks>
    public sealed class Lives
    {
        // The start of a new player's full lives: earlier than any reading, so
        // the first one becomes the latest time counted, and a valid UTC time
        // so that it saves and restores like any other.
        private static readonly DateTime FullRegenStartUtc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

        private readonly MetaRules rules;

        // The clock time is counted on, and the last reading of the device
        // clock: null until the first member taking nowUtc. In memory only.
        private DateTime? countedUtc;
        private DateTime lastReadingUtc;

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
        /// codec, as a time on the counted clock (see the class remarks); while
        /// <see cref="StoredCount"/> is at the cap, the latest time counted.
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
        /// after settling at it, by the counted clock; null when the lives are
        /// full. Exact to the tick: never negative and never above one
        /// interval. Rounding is for whoever shows it.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public TimeSpan? UntilNextLifeAt(DateTime nowUtc)
        {
            if (IsFullAt(nowUtc))
            {
                return null;
            }

            // Settled below the cap, the start is never after the counted time
            // and less than one interval before it.
            var intervalTicks = rules.LifeRegenInterval.Ticks;
            var waitedTicks = (countedUtc.Value - RegenStartUtc).Ticks;
            return TimeSpan.FromTicks(intervalTicks - waitedTicks % intervalTicks);
        }

        /// <summary>
        /// Loses one life at <paramref name="nowUtc"/>, after settling at it.
        /// Losing one at the cap starts the wait at the counted time, which is
        /// <paramref name="nowUtc"/> unless the clock was set back; below the
        /// cap the running wait is kept. False, with nothing but the
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

            // Settling at the cap left the counted time in the start.
            StoredCount--;
            return true;
        }

        /// <summary>Sets the lives to the cap at <paramref name="nowUtc"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not of <see cref="DateTimeKind.Utc"/>.</exception>
        public void Refill(DateTime nowUtc)
        {
            Settle(nowUtc);
            StoredCount = rules.LifeCap;
            RegenStartUtc = countedUtc.Value;
        }

        /// <summary>
        /// Moves the counted clock by <paramref name="nowUtc"/>, then adds the
        /// lives that came back by the counted time and moves the wait's start
        /// forward by the whole intervals they took. At the cap the start
        /// keeps the counted time instead.
        /// </summary>
        private void Settle(DateTime nowUtc)
        {
            ThrowIfNotUtc(nowUtc, nameof(nowUtc));
            var counted = Count(nowUtc);
            if (StoredCount >= rules.LifeCap)
            {
                RegenStartUtc = counted;
                return;
            }

            var intervalTicks = rules.LifeRegenInterval.Ticks;
            var wholeIntervals = (counted - RegenStartUtc).Ticks / intervalTicks;
            var gained = (int)Math.Min(wholeIntervals, rules.LifeCap - StoredCount);

            // gained is at most wholeIntervals, so the step lands at or before
            // the counted time and cannot leave DateTime's range.
            StoredCount += gained;
            RegenStartUtc = StoredCount >= rules.LifeCap ? counted : RegenStartUtc.AddTicks(gained * intervalTicks);
        }

        /// <summary>
        /// The counted time after reading <paramref name="nowUtc"/>: never
        /// before <see cref="RegenStartUtc"/>, and moved forward only by a
        /// reading after it or by the real time between two readings before it.
        /// </summary>
        private DateTime Count(DateTime nowUtc)
        {
            DateTime counted;
            if (!countedUtc.HasValue)
            {
                // The first reading: the saved start is the latest time known.
                counted = nowUtc > RegenStartUtc ? nowUtc : RegenStartUtc;
            }
            else if (nowUtc >= countedUtc.Value)
            {
                counted = nowUtc;
            }
            else if (nowUtc > lastReadingUtc)
            {
                // The clock reads behind the counted time and moved forward:
                // that much real time passed. Held at DateTime's end, which
                // only a clock once set there can reach.
                var stepTicks = Math.Min((nowUtc - lastReadingUtc).Ticks, (DateTime.MaxValue - countedUtc.Value).Ticks);
                counted = countedUtc.Value.AddTicks(stepTicks);
            }
            else
            {
                counted = countedUtc.Value;
            }

            countedUtc = counted;
            lastReadingUtc = nowUtc;
            return counted;
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
