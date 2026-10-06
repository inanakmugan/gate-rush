using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>What a burst is for; part of its seed, so two bursts of one move never share pieces.</summary>
    public enum BurstKind
    {
        /// <summary>A destroyed block streaming cubes out beneath its gate as it passes through.</summary>
        Exit,

        /// <summary>The ice of a thawing block breaking.</summary>
        Thaw,

        /// <summary>The ice of an opening gate breaking.</summary>
        GateOpening,

        /// <summary>
        /// A surviving layered block streaming cubes of its removed colour out
        /// beneath its gate as it peels. Last, so the kinds before it keep
        /// their seeds.
        /// </summary>
        Peel
    }

    /// <summary>One piece of a burst, in cell units: where it starts, how far it goes, and how it looks.</summary>
    public readonly struct BurstPiece : IEquatable<BurstPiece>
    {
        /// <summary>A piece with every field given.</summary>
        public BurstPiece(Vector2 start, Vector2 travel, float size, float spinDegrees, float delaySeconds)
        {
            Start = start;
            Travel = travel;
            Size = size;
            SpinDegrees = spinDegrees;
            DelaySeconds = delaySeconds;
        }

        /// <summary>Where the piece's centre starts, in the same cell units as the burst's areas.</summary>
        public Vector2 Start { get; }

        /// <summary>How far and which way the piece moves over its flight, in cells.</summary>
        public Vector2 Travel { get; }

        /// <summary>The piece's side at the start, in cells.</summary>
        public float Size { get; }

        /// <summary>How far the piece turns over its flight, in degrees, counter-clockwise.</summary>
        public float SpinDegrees { get; }

        /// <summary>How long after the burst starts the piece starts, in seconds.</summary>
        public float DelaySeconds { get; }

        /// <inheritdoc />
        public bool Equals(BurstPiece other) =>
            Start == other.Start && Travel == other.Travel && Size.Equals(other.Size)
            && SpinDegrees.Equals(other.SpinDegrees) && DelaySeconds.Equals(other.DelaySeconds);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is BurstPiece other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Start.GetHashCode() * 397) ^ Travel.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => $"{Start} +{Travel} size {Size} spin {SpinDegrees} delay {DelaySeconds}";
    }

    /// <summary>
    /// Lays out the pieces of a burst (Module 18): the cubes a destroyed block
    /// streams out beneath its gate while it passes through (<see cref="Stream"/>), and the ice shards of a
    /// thaw or a gate opening, flying out from a centre. Plain and
    /// deterministic: the same arguments always give the same pieces.
    /// </summary>
    /// <remarks>
    /// <para><b>Count.</b> <see cref="BurstSettings.CountPerArea"/> per area,
    /// capped at <see cref="BurstSettings.Cap"/>. Piece <c>i</c> starts in area
    /// <c>i mod n</c>, so a capped burst still spreads over every area.</para>
    /// <para><b>Direction.</b> Through an edge, every piece starts from that
    /// edge's outward direction; radially, from the centre toward its start.
    /// It then turns by less than a right angle
    /// (<see cref="BurstSettings.SpreadDegrees"/>) and travels a positive
    /// distance, so its motion always has a positive part along its base
    /// direction: out through the edge, or away from the centre.</para>
    /// <para><b>Randomness</b> is a <see cref="System.Random"/> seeded
    /// explicitly (<see cref="Seed"/>), never <c>UnityEngine.Random</c>'s
    /// shared state. Its draws are made in a fixed order per piece.</para>
    /// </remarks>
    public static class BurstLayout
    {
        // The multiplier of the polynomial hash in Seed: an odd prime, the
        // usual choice for mixing a few small integers. Not a look parameter.
        private const int SeedMultiplier = 31;

        // A float fraction such as 0.1 is a hair above its decimal value, so
        // a tenth of ten cubes computes as just over one and would round up
        // to two. Taking this much off before rounding up keeps a product
        // meant to be whole at that whole. Far below one cube, so it never
        // changes any other count. Not a look parameter.
        private const double FractionRoundingSlack = 1e-4;

        /// <summary>
        /// A seed for the burst of <paramref name="kind"/> on thing
        /// <paramref name="index"/> — a block slot or a gate position — on the
        /// level's move number <paramref name="moveNumber"/>. A fixed integer
        /// mix: <c>System.HashCode</c> would differ between runs.
        /// </summary>
        public static int Seed(BurstKind kind, int index, int moveNumber)
        {
            unchecked
            {
                var seed = (int)kind + 1;
                seed = seed * SeedMultiplier + index;
                seed = seed * SeedMultiplier + moveNumber;
                return seed;
            }
        }

        /// <summary>
        /// The pieces of one burst over <paramref name="areas"/>.
        /// </summary>
        /// <param name="areas">Where pieces start: rectangles in cell units, for example one per footprint cell. At least one.</param>
        /// <param name="outward">
        /// The edge the pieces burst out through, as its outward direction; null
        /// for a radial burst from <paramref name="centre"/>.
        /// </param>
        /// <param name="centre">The point a radial burst flies out from, in the areas' units. Unused when <paramref name="outward"/> is given.</param>
        /// <param name="seed">The burst's seed (<see cref="Seed"/>).</param>
        /// <param name="settings">Counts, sizes, distances and angles.</param>
        /// <exception cref="ArgumentNullException"><paramref name="areas"/> or <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="areas"/> is empty, or <paramref name="outward"/> is zero.</exception>
        public static IReadOnlyList<BurstPiece> Pieces(
            IReadOnlyList<Rect> areas, Vector2? outward, Vector2 centre, int seed, BurstSettings settings)
        {
            if (areas == null)
            {
                throw new ArgumentNullException(nameof(areas));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (areas.Count == 0)
            {
                throw new ArgumentException("A burst needs at least one area.", nameof(areas));
            }

            if (outward.HasValue && outward.Value == Vector2.zero)
            {
                throw new ArgumentException("An outward direction must not be zero.", nameof(outward));
            }

            var random = new System.Random(seed);
            var count = Math.Min(settings.CountPerArea * areas.Count, settings.Cap);
            var pieces = new BurstPiece[count];

            for (var i = 0; i < count; i++)
            {
                var area = areas[i % areas.Count];
                var start = new Vector2(
                    area.xMin + (float)random.NextDouble() * area.width,
                    area.yMin + (float)random.NextDouble() * area.height);
                var delay = (float)random.NextDouble() * settings.MaxDelaySeconds;

                pieces[i] = Piece(random, start, outward ?? start - centre, delay, settings);
            }

            return pieces;
        }

        /// <summary>
        /// How many cubes a block of <paramref name="cellCount"/> cells
        /// streams out at its gate: a destroyed block's count —
        /// <see cref="BurstSettings.CountPerArea"/> per footprint cell, capped
        /// at <see cref="BurstSettings.Cap"/> — times
        /// <paramref name="countFraction"/>, rounded up. So a fraction of 1 is
        /// the destroyed block's own count, 0 is none, a fraction worth any
        /// real part of a cube gives at least one, and no fraction passes the
        /// cap. The one rule for an exit's stream and a peel's.
        /// </summary>
        /// <param name="cellCount">Cells in the block's footprint. At least 1.</param>
        /// <param name="countFraction">The part of a destroyed block's stream to give, from 0 to 1.</param>
        /// <param name="settings">Counts, sizes, distances and angles.</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellCount"/> or <paramref name="countFraction"/> is out of range.</exception>
        public static int StreamCount(int cellCount, float countFraction, BurstSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (cellCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cellCount), cellCount, "A block has at least one cell.");
            }

            if (!(countFraction >= 0f && countFraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(countFraction), countFraction, "A stream gives from none to all of a destroyed block's cubes.");
            }

            var whole = Math.Min(settings.CountPerArea * cellCount, settings.Cap);
            return (int)Math.Ceiling(whole * (double)countFraction - FractionRoundingSlack);
        }

        /// <summary>
        /// The cubes a block streams out at its gate — a destroyed block while
        /// it passes through, or, at <paramref name="countFraction"/> of that,
        /// a layered block while it peels: <see cref="StreamCount"/> of them.
        /// Each starts at a random
        /// point on the gate's outer line (<see cref="GateExit.OuterLine"/>) —
        /// beneath the gate, outside the board — and flies outward within the
        /// spread. Their delays are spread evenly over the pass, piece
        /// <c>i</c> of <c>n</c> starting at a random moment of the <c>i</c>-th
        /// <c>n</c>-th of it, so the cubes come out continuously rather than
        /// in clumps; every delay lies in <c>[0, passSeconds]</c>.
        /// <see cref="BurstSettings.MaxDelaySeconds"/> is not read.
        /// </summary>
        /// <param name="width">The grid's width, in cells.</param>
        /// <param name="height">The grid's height, in cells.</param>
        /// <param name="edge">The gate's edge.</param>
        /// <param name="offset">The gate's offset along its edge.</param>
        /// <param name="span">The gate's width, in cells. At least 1.</param>
        /// <param name="frameThicknessCells">The frame's thickness: how far the outer line lies past the grid.</param>
        /// <param name="cellCount">Cells in the passing block's footprint. At least 1.</param>
        /// <param name="passSeconds">How long the block takes to pass. At least 0.</param>
        /// <param name="seed">The stream's seed (<see cref="Seed"/>).</param>
        /// <param name="settings">Counts, sizes, distances and angles.</param>
        /// <param name="countFraction">The part of a destroyed block's stream to give, from 0 to 1 (<see cref="StreamCount"/>).</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A count, span, duration or fraction is out of range.</exception>
        public static IReadOnlyList<BurstPiece> Stream(
            int width, int height, BoardEdge edge, int offset, int span, float frameThicknessCells,
            int cellCount, float passSeconds, int seed, BurstSettings settings, float countFraction = 1f)
        {
            // Null settings, a block without cells and a fraction outside 0
            // to 1 are StreamCount's to refuse.
            var count = StreamCount(cellCount, countFraction, settings);

            if (span < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(span), span, "A gate spans at least one cell.");
            }

            if (!(passSeconds >= 0f) || float.IsInfinity(passSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(passSeconds), passSeconds, "A pass lasts a finite time, at least 0.");
            }

            GateExit.OuterLine(width, height, edge, offset, span, frameThicknessCells, out var from, out var to);
            var outward = GateExit.Outward(edge);
            var random = new System.Random(seed);
            var pieces = new BurstPiece[count];

            for (var i = 0; i < count; i++)
            {
                var start = Vector2.Lerp(from, to, (float)random.NextDouble());

                // Below 1 in double precision, so rounding the product to a
                // float can reach passSeconds but never pass it.
                var fraction = (i + random.NextDouble()) / count;
                var delay = (float)(fraction * passSeconds);

                pieces[i] = Piece(random, start, outward, delay, settings);
            }

            return pieces;
        }

        /// <summary>
        /// One piece from <paramref name="start"/>: its direction turned from
        /// <paramref name="baseDirection"/> by less than a right angle, a
        /// positive travel, a size and a spin, drawn in that fixed order. A zero
        /// base direction — a radial piece starting on its centre — takes a
        /// random one.
        /// </summary>
        private static BurstPiece Piece(
            System.Random random, Vector2 start, Vector2 baseDirection, float delay, BurstSettings settings)
        {
            var baseAngle = baseDirection.sqrMagnitude > 0f
                ? Mathf.Atan2(baseDirection.y, baseDirection.x)
                : (float)(random.NextDouble() * 2.0 * Math.PI);
            var turn = (float)(random.NextDouble() * 2.0 - 1.0) * settings.SpreadDegrees * Mathf.Deg2Rad;
            var angle = baseAngle + turn;
            var distance = Lerp(settings.TravelMinCells, settings.TravelMaxCells, random.NextDouble());

            return new BurstPiece(
                start,
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                Lerp(settings.SizeMinCells, settings.SizeMaxCells, random.NextDouble()),
                (float)(random.NextDouble() * 2.0 - 1.0) * settings.SpinDegrees,
                delay);
        }

        private static float Lerp(float from, float to, double t) => from + (to - from) * (float)t;
    }
}
