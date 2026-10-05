using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>
    /// Which mechanics a level contains, and which of them it is the first
    /// level to contain (Module 19). Introductions are derived from the levels'
    /// own content, never kept by hand: reordering or adding levels moves each
    /// introduction to the first level that then contains its mechanic.
    /// </summary>
    public static class LevelMechanics
    {
        private static readonly LevelMechanic[] InCardOrder = (LevelMechanic[])Enum.GetValues(typeof(LevelMechanic));

        /// <summary>Every <see cref="LevelMechanic"/>, in enum order — the order cards show in.</summary>
        public static IReadOnlyList<LevelMechanic> All => InCardOrder;

        /// <summary>
        /// Every mechanic <paramref name="ctx"/> contains, in enum order: on any
        /// block the level can ever show — top-level, queued in a generator or
        /// in an elevator wave — or as a gate, shutter, generator or elevator.
        /// Never <see cref="LevelMechanic.HowToPlay"/>, which is not content.
        /// </summary>
        /// <remarks>
        /// Something that can never show is not contained: a block, gate or
        /// shutter whose threshold is already met at zero clears starts open
        /// and is never drawn as ice or as a panel, a generator with nothing
        /// queued is never drawn, and neither is an elevator without waves.
        /// "Met at zero clears" is asked of <c>Core</c> — the gate's own rule
        /// and <see cref="UnlockConditions.IsThresholdMet"/>, which the initial
        /// state and the resolver use — not decided again here.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is null.</exception>
        public static IReadOnlyCollection<LevelMechanic> Of(LevelContext ctx)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            var isContained = new bool[InCardOrder.Length];

            // The counters of a level no clear has happened in.
            var noClearsByColor = new int[Enum.GetValues(typeof(BlockColor)).Length];

            // The flat slot space holds top-level blocks, then every generator
            // queue entry, then every elevator wave block (LevelContext.SpecAt),
            // so one pass sees every block the level can ever show.
            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                var spec = ctx.SpecAt(i);

                if (spec.UnfreezeAtClearCount.HasValue
                    && !UnlockConditions.IsThresholdMet(0, noClearsByColor, spec.UnfreezeAtClearCount.Value, null))
                {
                    isContained[(int)LevelMechanic.IceBlock] = true;
                }

                if (spec.ColorStack.Count > 1)
                {
                    isContained[(int)LevelMechanic.LayeredBlock] = true;
                }

                if (spec.Axis != MovementAxis.Free)
                {
                    isContained[(int)LevelMechanic.OneWayBlock] = true;
                }

                if (spec.LockId.HasValue)
                {
                    isContained[(int)LevelMechanic.LockAndKey] = true;
                }

                if (spec.TimeBonusSeconds > 0)
                {
                    isContained[(int)LevelMechanic.TimeBonus] = true;
                }
            }

            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                if (!GateDefinition.IsOpenAtZeroClears(ctx.Gates[g].OpenAtClearCount))
                {
                    isContained[(int)LevelMechanic.IceDoor] = true;
                }
            }

            for (var s = 0; s < ctx.Shutters.Count; s++)
            {
                var shutter = ctx.Shutters[s];
                if (!UnlockConditions.IsThresholdMet(0, noClearsByColor, shutter.Threshold, shutter.RequiredColor))
                {
                    isContained[(int)LevelMechanic.Shutter] = true;
                }
            }

            for (var g = 0; g < ctx.Generators.Count; g++)
            {
                if (ctx.Generators[g].Queue.Count > 0)
                {
                    isContained[(int)LevelMechanic.Generator] = true;
                }
            }

            for (var e = 0; e < ctx.Elevators.Count; e++)
            {
                if (ctx.Elevators[e].Waves.Count > 0)
                {
                    isContained[(int)LevelMechanic.Elevator] = true;
                }
            }

            var contained = new List<LevelMechanic>();
            for (var m = 0; m < InCardOrder.Length; m++)
            {
                if (isContained[(int)InCardOrder[m]])
                {
                    contained.Add(InCardOrder[m]);
                }
            }

            return contained;
        }

        /// <summary>
        /// The mechanics the level at <paramref name="position"/> introduces:
        /// <see cref="LevelMechanic.HowToPlay"/> at position 0 only, then every
        /// mechanic that level contains and no earlier level does, in enum
        /// order.
        /// </summary>
        /// <param name="inOrder">What each level contains (<see cref="Of"/>), in the order the levels are played.</param>
        /// <param name="position">The 0-based position of the level in <paramref name="inOrder"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="inOrder"/> or one of its entries is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a position in <paramref name="inOrder"/>.</exception>
        public static IReadOnlyList<LevelMechanic> IntroducedBy(
            IReadOnlyList<IReadOnlyCollection<LevelMechanic>> inOrder, int position)
        {
            if (inOrder == null)
            {
                throw new ArgumentNullException(nameof(inOrder));
            }

            if (position < 0 || position >= inOrder.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position), position, $"The position must be within [0, {inOrder.Count}).");
            }

            var isSeenEarlier = new bool[InCardOrder.Length];
            for (var level = 0; level < position; level++)
            {
                Mark(isSeenEarlier, inOrder[level], level);
            }

            var isHere = new bool[InCardOrder.Length];
            Mark(isHere, inOrder[position], position);

            var introduced = new List<LevelMechanic>();
            if (position == 0)
            {
                introduced.Add(LevelMechanic.HowToPlay);
            }

            for (var m = 0; m < InCardOrder.Length; m++)
            {
                var mechanic = InCardOrder[m];
                if (mechanic != LevelMechanic.HowToPlay && isHere[(int)mechanic] && !isSeenEarlier[(int)mechanic])
                {
                    introduced.Add(mechanic);
                }
            }

            return introduced;
        }

        private static void Mark(bool[] flags, IReadOnlyCollection<LevelMechanic> mechanics, int level)
        {
            if (mechanics == null)
            {
                throw new ArgumentNullException(nameof(mechanics), $"The level at position {level} has no mechanics collection.");
            }

            foreach (var mechanic in mechanics)
            {
                flags[(int)mechanic] = true;
            }
        }
    }
}
