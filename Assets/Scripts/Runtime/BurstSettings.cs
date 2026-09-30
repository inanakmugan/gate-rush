using System;
using System.Collections.Generic;

namespace GateRush.Runtime
{
    /// <summary>
    /// The layout tunables of one kind of burst (Module 18): the cubes a
    /// destroyed block breaks into, or the ice shards of a thaw or a gate
    /// opening. Built from <c>RuntimeConfig</c>; immutable. Read by
    /// <see cref="BurstLayout"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Problems"/> is the single statement of what a valid value is.
    /// The constructor throws on what it reports, and <c>RuntimeConfig</c>
    /// reports the same messages at load, so the two can never disagree. Its
    /// messages name the config's fields by the prefix they share, so one
    /// statement serves both bursts.
    /// </remarks>
    public sealed class BurstSettings
    {
        /// <summary>
        /// The spread must stay below this: at a right angle a piece could
        /// travel along the gate's edge instead of out through it.
        /// </summary>
        public const float MaxSpreadDegreesExclusive = 90f;

        /// <summary>Settings for one kind of burst.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Any value <see cref="Problems"/> reports; the message lists them all.
        /// </exception>
        public BurstSettings(
            int countPerArea, int cap, float sizeMinCells, float sizeMaxCells, float travelMinCells, float travelMaxCells,
            float spreadDegrees, float spinDegrees, float maxDelaySeconds)
        {
            var problems = Problems(
                "Burst", countPerArea, cap, sizeMinCells, sizeMaxCells, travelMinCells, travelMaxCells,
                spreadDegrees, spinDegrees, maxDelaySeconds);
            if (problems.Count > 0)
            {
                throw new ArgumentOutOfRangeException(null, string.Join(" ", problems));
            }

            CountPerArea = countPerArea;
            Cap = cap;
            SizeMinCells = sizeMinCells;
            SizeMaxCells = sizeMaxCells;
            TravelMinCells = travelMinCells;
            TravelMaxCells = travelMaxCells;
            SpreadDegrees = spreadDegrees;
            SpinDegrees = spinDegrees;
            MaxDelaySeconds = maxDelaySeconds;
        }

        /// <summary>Pieces per area — per cell of a block, per cell of a gate's span. At least 1.</summary>
        public int CountPerArea { get; }

        /// <summary>The most pieces one burst may have, however many areas it covers. At least 1.</summary>
        public int Cap { get; }

        /// <summary>The smallest piece's side, in cells. Positive.</summary>
        public float SizeMinCells { get; }

        /// <summary>The largest piece's side, in cells. At least <see cref="SizeMinCells"/>.</summary>
        public float SizeMaxCells { get; }

        /// <summary>
        /// The shortest distance a piece travels, in cells. Positive, so every
        /// piece really moves in its direction.
        /// </summary>
        public float TravelMinCells { get; }

        /// <summary>The longest distance a piece travels, in cells. At least <see cref="TravelMinCells"/>.</summary>
        public float TravelMaxCells { get; }

        /// <summary>
        /// How far, in degrees either way, a piece's direction may turn from
        /// its base direction. At least 0 and below
        /// <see cref="MaxSpreadDegreesExclusive"/>.
        /// </summary>
        public float SpreadDegrees { get; }

        /// <summary>The most a piece turns over its flight, in degrees either way. At least 0.</summary>
        public float SpinDegrees { get; }

        /// <summary>The latest a piece may start after the burst does, in seconds. At least 0.</summary>
        public float MaxDelaySeconds { get; }

        /// <summary>
        /// Every reason these values cannot make settings, one message per
        /// field, each starting with <paramref name="prefix"/> and the field's
        /// name as the config's inspector shows it; empty when they are valid.
        /// </summary>
        /// <param name="prefix">The field names' shared start in the config, for example <c>"Cube"</c>.</param>
        public static IReadOnlyList<string> Problems(
            string prefix, int countPerArea, int cap, float sizeMinCells, float sizeMaxCells,
            float travelMinCells, float travelMaxCells, float spreadDegrees, float spinDegrees, float maxDelaySeconds)
        {
            var problems = new List<string>();

            if (countPerArea < 1)
            {
                problems.Add($"{prefix} Count Per Cell must be at least 1.");
            }

            if (cap < 1)
            {
                problems.Add($"{prefix} Cap must be at least 1.");
            }

            if (!(sizeMinCells > 0f && sizeMaxCells >= sizeMinCells) || float.IsInfinity(sizeMaxCells))
            {
                problems.Add($"{prefix} Size Cells must have a positive smallest size (x) no larger than its largest (y).");
            }

            if (!(travelMinCells > 0f && travelMaxCells >= travelMinCells) || float.IsInfinity(travelMaxCells))
            {
                problems.Add($"{prefix} Travel Cells must have a positive shortest travel (x) no longer than its longest (y).");
            }

            if (!(spreadDegrees >= 0f && spreadDegrees < MaxSpreadDegreesExclusive))
            {
                problems.Add($"{prefix} Spread Degrees must be at least 0 and below {MaxSpreadDegreesExclusive}.");
            }

            if (!(spinDegrees >= 0f) || float.IsInfinity(spinDegrees))
            {
                problems.Add($"{prefix} Spin Degrees must be at least 0 and finite.");
            }

            if (!(maxDelaySeconds >= 0f) || float.IsInfinity(maxDelaySeconds))
            {
                problems.Add($"{prefix} Max Delay Seconds must be at least 0 and finite.");
            }

            return problems;
        }
    }
}
