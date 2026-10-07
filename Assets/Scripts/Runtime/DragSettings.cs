using System;
using System.Collections.Generic;

namespace GateRush.Runtime
{
    /// <summary>
    /// The tunables of one drag (Module 13): the push threshold, the pointer
    /// smoothing rate and the corner assist distance — and of the gate pull
    /// (Module 22): its range, amount, capture range, follow rate, nudge
    /// bound and ease. Built from <c>RuntimeConfig</c>; immutable.
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

        /// <summary>
        /// The nudge into a gate's mouth must stay below this: a block shown
        /// half a cell or more into its gate reads as already leaving.
        /// </summary>
        public const float MaxNudgeCellsExclusive = 0.5f;

        /// <summary>Settings for one drag.</summary>
        /// <param name="pushThresholdCells">See <see cref="PushThresholdCells"/>.</param>
        /// <param name="followRate">See <see cref="FollowRate"/>.</param>
        /// <param name="cornerAssistCells">See <see cref="CornerAssistCells"/>.</param>
        /// <param name="pullRangeCells">See <see cref="PullRangeCells"/>.</param>
        /// <param name="pullAmount">See <see cref="PullAmount"/>.</param>
        /// <param name="captureRangeCells">See <see cref="CaptureRangeCells"/>.</param>
        /// <param name="pullFollowRate">See <see cref="PullFollowRate"/>.</param>
        /// <param name="nudgeMaxCells">See <see cref="NudgeMaxCells"/>.</param>
        /// <param name="pullEase">See <see cref="PullEase"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pullEase"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Any value <see cref="Problems"/> reports; the message lists them all.
        /// </exception>
        public DragSettings(
            float pushThresholdCells, float followRate, float cornerAssistCells,
            float pullRangeCells, float pullAmount, float captureRangeCells, float pullFollowRate,
            float nudgeMaxCells, Func<float, float> pullEase)
        {
            if (pullEase == null)
            {
                throw new ArgumentNullException(nameof(pullEase));
            }

            var problems = Problems(
                pushThresholdCells, followRate, cornerAssistCells, pullRangeCells, pullAmount, captureRangeCells,
                pullFollowRate, nudgeMaxCells);
            if (problems.Count > 0)
            {
                throw new ArgumentOutOfRangeException(null, string.Join(" ", problems));
            }

            PushThresholdCells = pushThresholdCells;
            FollowRate = followRate;
            CornerAssistCells = cornerAssistCells;
            PullRangeCells = pullRangeCells;
            PullAmount = pullAmount;
            CaptureRangeCells = captureRangeCells;
            PullFollowRate = pullFollowRate;
            NudgeMaxCells = nudgeMaxCells;
            PullEase = pullEase;
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
        /// How far, in cells, from the origin where a block would arrive at a
        /// gate it can leave through the gate starts to pull it (D49).
        /// Positive and finite.
        /// </summary>
        public float PullRangeCells { get; }

        /// <summary>
        /// The most of the way to the pull's origin the block is drawn while
        /// it is outside the capture range: the fraction of its distance from
        /// the origin at full strength. Above 0 and at most 1. Within the
        /// capture range the block is drawn all the way, whatever this is.
        /// </summary>
        public float PullAmount { get; }

        /// <summary>
        /// How far, in cells, from the pull's origin a release still arrives
        /// there (D49), and within which the block is drawn latched onto the
        /// origin. Positive, and at most <see cref="PullRangeCells"/>: nothing
        /// is captured that is not also pulled.
        /// </summary>
        public float CaptureRangeCells { get; }

        /// <summary>
        /// How fast, per second, the pulled fraction closes on the value the
        /// pull asks for: each update covers <c>1 − e^(−rate·dt)</c> of the
        /// gap, the same at any frame rate, so the block slides into the latch
        /// and out of it instead of jumping. Positive and finite.
        /// </summary>
        public float PullFollowRate { get; }

        /// <summary>
        /// The furthest, in cells, a block standing on its gate's origin is
        /// drawn into the gate's mouth while the pointer pushes on toward the
        /// gate. Its own bound, independent of <see cref="PullAmount"/>: the
        /// nudge follows the pointer's overshoot up to this and no further.
        /// Zero turns the nudge off; below <see cref="MaxNudgeCellsExclusive"/>.
        /// </summary>
        public float NudgeMaxCells { get; }

        /// <summary>
        /// The pull's ease: maps the linear strength, 0 at the edge of the
        /// range and 1 at the origin, to the strength used. A plain function,
        /// so the drag needs no tweening library; whatever it returns is
        /// clamped to 0–1 by the drag, so no ease can pull a block past its
        /// target.
        /// </summary>
        public Func<float, float> PullEase { get; }

        /// <summary>
        /// Every reason these values cannot make settings, one message per
        /// field, named as the config's inspector shows it; empty when they are
        /// valid.
        /// </summary>
        public static IReadOnlyList<string> Problems(
            float pushThresholdCells, float followRate, float cornerAssistCells,
            float pullRangeCells, float pullAmount, float captureRangeCells, float pullFollowRate, float nudgeMaxCells)
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

            var isPullRangeValid = pullRangeCells > 0f && !float.IsInfinity(pullRangeCells);
            if (!isPullRangeValid)
            {
                problems.Add("Pull Range Cells must be positive and finite.");
            }

            if (!(pullAmount > 0f && pullAmount <= 1f))
            {
                problems.Add("Pull Amount must be above 0 and at most 1.");
            }

            // Against the pull range only when that is itself valid, so one
            // bad field gives one message.
            if (!(captureRangeCells > 0f)
                || float.IsInfinity(captureRangeCells)
                || (isPullRangeValid && captureRangeCells > pullRangeCells))
            {
                problems.Add("Capture Range Cells must be positive and at most Pull Range Cells.");
            }

            if (!(pullFollowRate > 0f) || float.IsInfinity(pullFollowRate))
            {
                problems.Add("Pull Follow Rate must be positive and finite.");
            }

            if (!(nudgeMaxCells >= 0f && nudgeMaxCells < MaxNudgeCellsExclusive))
            {
                problems.Add($"Nudge Max Cells must be at least 0 and below {MaxNudgeCellsExclusive}.");
            }

            return problems;
        }
    }
}
