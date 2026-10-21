using System;
using System.Collections.Generic;
using GateRush.Meta;

namespace GateRush.Runtime
{
    /// <summary>
    /// Everything the level's interface shows besides the timer (Modules 40
    /// and 43): the level number or free play, the lives and the wait to the
    /// next, the gold, the three joker buttons — locked or marked in the
    /// campaign — and the selected joker, and the gold
    /// offers of the in-level gold packs. Read once by <see cref="Of"/>, so
    /// the views hold no rule and every number on them comes from here.
    /// </summary>
    /// <remarks>
    /// <para>The buttons are <see cref="JokerButtons"/>' decisions; the
    /// bootstrap reads a press against the same state it drew, so the
    /// picture and the press never disagree.</para>
    /// <para>The wait is kept in whole seconds, rounded up as the countdown
    /// is, so two states read a frame apart are equal unless what they show
    /// differs: <see cref="Equals(LevelUiState)"/> is the bootstrap's
    /// refresh test. The offers compare by reference — the bootstrap matches
    /// them once.</para>
    /// </remarks>
    public readonly struct LevelUiState : IEquatable<LevelUiState>
    {
        private readonly JokerButton clock;
        private readonly JokerButton rocket;
        private readonly JokerButton broom;

        private LevelUiState(
            bool isFreePlay,
            int? levelNumber,
            int lives,
            int lifeCap,
            int? nextLifeSeconds,
            int gold,
            JokerButton clock,
            JokerButton rocket,
            JokerButton broom,
            JokerKind? selectedJoker,
            IReadOnlyList<GoldOffer> goldOffers)
        {
            IsFreePlay = isFreePlay;
            LevelNumber = levelNumber;
            Lives = lives;
            LifeCap = lifeCap;
            NextLifeSeconds = nextLifeSeconds;
            Gold = gold;
            this.clock = clock;
            this.rocket = rocket;
            this.broom = broom;
            SelectedJoker = selectedJoker;
            GoldOffers = goldOffers;
        }

        /// <summary>True in free play (D58): no lives shown, the label reads Free Play.</summary>
        public bool IsFreePlay { get; }

        /// <summary>The level's 1-based number in the level order; null for a level outside it.</summary>
        public int? LevelNumber { get; }

        /// <summary>The lives in the campaign; 0 in free play, where they are not shown.</summary>
        public int Lives { get; }

        /// <summary>The most lives a player holds.</summary>
        public int LifeCap { get; }

        /// <summary>
        /// The whole seconds to the next life, rounded up as the countdown is
        /// (<see cref="MetaText.WholeSeconds"/>); null when the lives are full,
        /// and in free play.
        /// </summary>
        public int? NextLifeSeconds { get; }

        /// <summary>The gold balance.</summary>
        public int Gold { get; }

        /// <summary>The Rocket or Broom selected and not yet used (D67); null when none is.</summary>
        public JokerKind? SelectedJoker { get; }

        /// <summary>The gold packs both the rules and the purchase service know (D72). Never null for a state built by <see cref="Of"/>.</summary>
        public IReadOnlyList<GoldOffer> GoldOffers { get; }

        /// <summary>The button of <paramref name="kind"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public JokerButton Button(JokerKind kind)
        {
            switch (kind)
            {
                case JokerKind.Clock:
                    return clock;
                case JokerKind.Rocket:
                    return rocket;
                case JokerKind.Broom:
                    return broom;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, $"{(int)kind} is not a joker kind.");
            }
        }

        /// <summary>
        /// The level's interface at <paramref name="nowUtc"/>, for a level
        /// played as <paramref name="mode"/> with <paramref name="levelNumber"/>:
        /// the lives of <paramref name="meta"/>'s save in the campaign, none in
        /// free play (D58); its gold; each joker's button in the campaign
        /// locked with <paramref name="rules"/>' unlock level while
        /// <paramref name="meta"/> says it is locked (D76), otherwise from the
        /// inventory and the price, marked as <paramref name="marks"/> says;
        /// in free play from <paramref name="freePlay"/>'s allowance, never
        /// locked or marked; the Clock usable only with
        /// <paramref name="hasCountdown"/>; and <paramref name="selected"/> and
        /// <paramref name="goldOffers"/> as given. Reading the lives settles
        /// them, which needs no save (<see cref="MetaGame"/>). Allocates
        /// nothing.
        /// </summary>
        /// <param name="isUsable">
        /// Whether a joker could be used now: the level is being played and
        /// nothing holds it (<see cref="JokerButtons"/>).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="meta"/>, <paramref name="rules"/>,
        /// <paramref name="freePlay"/>, <paramref name="marks"/> or
        /// <paramref name="goldOffers"/> is null.
        /// </exception>
        public static LevelUiState Of(
            PlayMode mode,
            int? levelNumber,
            MetaGame meta,
            MetaRules rules,
            DateTime nowUtc,
            FreePlayJokers freePlay,
            JokerMarks marks,
            bool isUsable,
            bool hasCountdown,
            JokerKind? selected,
            IReadOnlyList<GoldOffer> goldOffers)
        {
            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (freePlay == null)
            {
                throw new ArgumentNullException(nameof(freePlay));
            }

            if (marks == null)
            {
                throw new ArgumentNullException(nameof(marks));
            }

            if (goldOffers == null)
            {
                throw new ArgumentNullException(nameof(goldOffers));
            }

            var save = meta.Save;
            var gold = save.Wallet.Balance;
            var isClockUsable = isUsable && hasCountdown;
            if (mode == PlayMode.FreePlay)
            {
                return new LevelUiState(
                    true,
                    levelNumber,
                    0,
                    rules.LifeCap,
                    null,
                    gold,
                    JokerButtons.FreePlay(freePlay.CountOf(JokerKind.Clock), isClockUsable),
                    JokerButtons.FreePlay(freePlay.CountOf(JokerKind.Rocket), isUsable),
                    JokerButtons.FreePlay(freePlay.CountOf(JokerKind.Broom), isUsable),
                    selected,
                    goldOffers);
            }

            var untilNext = save.Lives.UntilNextLifeAt(nowUtc);
            return new LevelUiState(
                false,
                levelNumber,
                save.Lives.CountAt(nowUtc),
                rules.LifeCap,
                untilNext.HasValue ? MetaText.WholeSeconds(untilNext.Value) : (int?)null,
                gold,
                CampaignButton(JokerKind.Clock, meta, rules, marks, gold, isClockUsable),
                CampaignButton(JokerKind.Rocket, meta, rules, marks, gold, isUsable),
                CampaignButton(JokerKind.Broom, meta, rules, marks, gold, isUsable),
                selected,
                goldOffers);
        }

        /// <inheritdoc />
        public bool Equals(LevelUiState other) =>
            IsFreePlay == other.IsFreePlay
            && LevelNumber == other.LevelNumber
            && Lives == other.Lives
            && LifeCap == other.LifeCap
            && NextLifeSeconds == other.NextLifeSeconds
            && Gold == other.Gold
            && clock == other.clock
            && rocket == other.rocket
            && broom == other.broom
            && SelectedJoker == other.SelectedJoker
            && ReferenceEquals(GoldOffers, other.GoldOffers);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is LevelUiState other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = IsFreePlay ? 1 : 0;
                hash = hash * 31 + LevelNumber.GetHashCode();
                hash = hash * 31 + Lives;
                hash = hash * 31 + LifeCap;
                hash = hash * 31 + NextLifeSeconds.GetHashCode();
                hash = hash * 31 + Gold;
                hash = hash * 31 + clock.GetHashCode();
                hash = hash * 31 + rocket.GetHashCode();
                hash = hash * 31 + broom.GetHashCode();
                return hash * 31 + SelectedJoker.GetHashCode();
            }
        }

        private static JokerButton CampaignButton(
            JokerKind kind, MetaGame meta, MetaRules rules, JokerMarks marks, int gold, bool isUsable) =>
            meta.IsJokerUnlocked(kind)
                ? JokerButtons.Campaign(meta.Save.Jokers.CountOf(kind), rules.JokerPrice(kind), gold, isUsable, marks.IsMarked(kind))
                : JokerButtons.Locked(rules.JokerUnlockLevel(kind));
    }
}
