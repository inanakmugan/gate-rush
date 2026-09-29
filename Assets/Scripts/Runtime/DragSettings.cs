using System;
using System.Collections.Generic;

namespace GateRush.Runtime
{
    /// <summary>
    /// The tunables of one drag (Module 13): the push threshold, the pointer
    /// smoothing rate and the corner assist distance. Built from
    /// <c>RuntimeConfig</c>; immutable.
    /// </summary>
    /// <remarks>
    /// <see cref="Problems"/> is the single statement of what a valid value is.
    /// The constructor throws on what it reports, and <c>RuntimeConfig</c>
    /// reports the same messages at load, so the two can never disagree.
    /// </remarks>
    public sealed class DragSettings
    {
        /// <summary>
        /// The corner assist distance must stay below this: at half a cell the
        /// two whole cells either side of a coordinate could tie as the
        /// nearest.
        /// </summary>
        public const float MaxCornerAssistCellsExclusive = 0.5f;

        /// <summary>Settings for one drag.</summary>
        /// <param name="pushThresholdCells">See <see cref="PushThresholdCells"/>.</param>
        /// <param name="followRate">See <see cref="FollowRate"/>.</param>
        /// <param name="cornerAssistCells">See <see cref="CornerAssistCells"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Any value <see cref="Problems"/> reports; the message lists them all.
        /// </exception>
        public DragSettings(float pushThresholdCells, float followRate, float cornerAssistCells)
        {
            var problems = Problems(pushThresholdCells, followRate, cornerAssistCells);
            if (problems.Count > 0)
            {
                throw new ArgumentOutOfRangeException(null, string.Join(" ", problems));
            }

            PushThresholdCells = pushThresholdCells;
            FollowRate = followRate;
            CornerAssistCells = cornerAssistCells;
        }

        /// <summary>
        /// How far, in cells, the pointer must travel from where it grabbed
        /// before a release at the start reads as a push (Module 11). Positive:
        /// with no threshold, a tap with no displacement would have no direction.
        /// </summary>
        public float PushThresholdCells { get; }

        /// <summary>
        /// How fast, per second, the smoothed pointer closes on the real one:
        /// each update covers <c>1 − e^(−rate·dt)</c> of the gap, so the lag is
        /// the same at any frame rate. Positive and finite.
        /// </summary>
        public float FollowRate { get; }

        /// <summary>
        /// How far, in cells, a block stopped on one axis may be off a whole
        /// cell on the other and still be nudged onto it, so it slides into a
        /// corridor instead of catching on its corner. Zero turns the assist
        /// off; below <see cref="MaxCornerAssistCellsExclusive"/>.
        /// </summary>
        public float CornerAssistCells { get; }

        /// <summary>
        /// Every reason these values cannot make settings, one message per
        /// field, named as the config's inspector shows it; empty when they are
        /// valid.
        /// </summary>
        public static IReadOnlyList<string> Problems(float pushThresholdCells, float followRate, float cornerAssistCells)
        {
            var problems = new List<string>();

            if (!(pushThresholdCells > 0f))
            {
                problems.Add("Push Threshold Cells must be positive.");
            }

            if (!(followRate > 0f) || float.IsInfinity(followRate))
            {
                problems.Add("Follow Rate must be positive and finite.");
            }

            if (!(cornerAssistCells >= 0f && cornerAssistCells < MaxCornerAssistCellsExclusive))
            {
                problems.Add($"Corner Assist Cells must be at least 0 and below {MaxCornerAssistCellsExclusive}.");
            }

            return problems;
        }
    }
}
