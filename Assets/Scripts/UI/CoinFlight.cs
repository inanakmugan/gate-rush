using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>The numbers a coin flight is laid out from (Module 47): read from <see cref="UiConfig"/>.</summary>
    public sealed class CoinFlightSettings
    {
        /// <summary>Settings with every value given; <see cref="Problems"/> says which are out of range.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A value is out of range; the message lists every problem.</exception>
        public CoinFlightSettings(
            int cap, int goldPerCoin, float startSpreadUnits, float maxDelaySeconds, float secondsMin, float secondsMax,
            float arcMinUnits, float arcMaxUnits)
        {
            var problems = Problems(cap, goldPerCoin, startSpreadUnits, maxDelaySeconds, secondsMin, secondsMax, arcMinUnits, arcMaxUnits);
            if (problems.Count > 0)
            {
                throw new ArgumentOutOfRangeException(null, string.Join(" ", problems));
            }

            Cap = cap;
            GoldPerCoin = goldPerCoin;
            StartSpreadUnits = startSpreadUnits;
            MaxDelaySeconds = maxDelaySeconds;
            SecondsMin = secondsMin;
            SecondsMax = secondsMax;
            ArcMinUnits = arcMinUnits;
            ArcMaxUnits = arcMaxUnits;
        }

        /// <summary>The most coins one flight has. At least 1.</summary>
        public int Cap { get; }

        /// <summary>How much gold one coin stands for before the cap: the count grows with the amount. At least 1.</summary>
        public int GoldPerCoin { get; }

        /// <summary>The radius, in canvas units, of the disc around the source the coins start in. At least 0.</summary>
        public float StartSpreadUnits { get; }

        /// <summary>The latest a coin starts after the flight does, in seconds. At least 0.</summary>
        public float MaxDelaySeconds { get; }

        /// <summary>The shortest a coin's flight takes, in seconds. Positive.</summary>
        public float SecondsMin { get; }

        /// <summary>The longest a coin's flight takes, in seconds. At least <see cref="SecondsMin"/>.</summary>
        public float SecondsMax { get; }

        /// <summary>The lowest a coin's arc rises above the straight line, in canvas units. At least 0.</summary>
        public float ArcMinUnits { get; }

        /// <summary>The highest a coin's arc rises, in canvas units. At least <see cref="ArcMinUnits"/>.</summary>
        public float ArcMaxUnits { get; }

        /// <summary>Every reason these values cannot make settings, one message each, named as <see cref="UiConfig"/>'s inspector shows them.</summary>
        public static IReadOnlyList<string> Problems(
            int cap, int goldPerCoin, float startSpreadUnits, float maxDelaySeconds, float secondsMin, float secondsMax,
            float arcMinUnits, float arcMaxUnits)
        {
            var problems = new List<string>();
            if (!(cap >= 1 && cap <= MaxCap))
            {
                problems.Add($"Coin Flight Cap must be from 1 to {MaxCap}.");
            }

            if (!(goldPerCoin >= 1 && goldPerCoin <= MaxGoldPerCoin))
            {
                problems.Add($"Coin Flight Gold Per Coin must be from 1 to {MaxGoldPerCoin}.");
            }

            if (!(startSpreadUnits >= 0f && startSpreadUnits <= MaxStartSpreadUnits))
            {
                problems.Add($"Coin Flight Start Spread Units must be from 0 to {MaxStartSpreadUnits}.");
            }

            if (!(maxDelaySeconds >= 0f && maxDelaySeconds <= MaxDelayLimitSeconds))
            {
                problems.Add($"Coin Flight Max Delay Seconds must be from 0 to {MaxDelayLimitSeconds}.");
            }

            if (!(secondsMin > 0f && secondsMax >= secondsMin && secondsMax <= MaxSeconds))
            {
                problems.Add($"Coin Flight Seconds must have a positive shortest time (x) no longer than its longest (y), at most {MaxSeconds}.");
            }

            if (!(arcMinUnits >= 0f && arcMaxUnits >= arcMinUnits && arcMaxUnits <= MaxArcUnits))
            {
                problems.Add($"Coin Flight Arc Units must have a lowest arc (x) of at least 0, no higher than its highest (y), at most {MaxArcUnits}.");
            }

            return problems;
        }

        // The bounds of a flight: past them it crowds the screen or drags on.
        // Not tunables; the values inside them are.
        private const int MaxCap = 24;
        private const int MaxGoldPerCoin = 1000;
        private const float MaxStartSpreadUnits = 200f;
        private const float MaxDelayLimitSeconds = 1.5f;
        private const float MaxSeconds = 2f;
        private const float MaxArcUnits = 600f;
    }

    /// <summary>One coin of a flight: when it starts, where, how high it arcs, how long it flies, and what it adds when it lands.</summary>
    public readonly struct CoinPiece : IEquatable<CoinPiece>
    {
        /// <summary>A coin with every field given.</summary>
        public CoinPiece(float delaySeconds, Vector2 startOffset, float arcUnits, float seconds, int share)
        {
            DelaySeconds = delaySeconds;
            StartOffset = startOffset;
            ArcUnits = arcUnits;
            Seconds = seconds;
            Share = share;
        }

        /// <summary>How long after the flight starts this coin leaves, in seconds.</summary>
        public float DelaySeconds { get; }

        /// <summary>Where it starts from the source's centre, in canvas units.</summary>
        public Vector2 StartOffset { get; }

        /// <summary>How high its arc rises above the straight line at the middle of its flight, in canvas units.</summary>
        public float ArcUnits { get; }

        /// <summary>How long its flight takes, in seconds.</summary>
        public float Seconds { get; }

        /// <summary>The gold it adds to the counter when it lands: a whole share of the amount.</summary>
        public int Share { get; }

        /// <inheritdoc />
        public bool Equals(CoinPiece other) =>
            DelaySeconds.Equals(other.DelaySeconds) && StartOffset == other.StartOffset && ArcUnits.Equals(other.ArcUnits)
            && Seconds.Equals(other.Seconds) && Share == other.Share;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CoinPiece other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (DelaySeconds.GetHashCode() * 397) ^ Share;

        /// <inheritdoc />
        public override string ToString() => $"delay {DelaySeconds} from {StartOffset} arc {ArcUnits} for {Seconds}s, +{Share}";
    }

    /// <summary>
    /// Where each flying coin goes and when (Module 47): the coins gold flies
    /// to its counter in. Plain and deterministic: the same amount, settings
    /// and seed always give the same coins.
    /// </summary>
    /// <remarks>
    /// <para><b>Count.</b> One coin per <see cref="CoinFlightSettings.GoldPerCoin"/>
    /// of the amount, rounded up, never more than
    /// <see cref="CoinFlightSettings.Cap"/>; none for no gold.</para>
    /// <para><b>Shares.</b> Whole numbers that sum exactly to the amount: each
    /// coin carries the amount divided by the count, and the first coins one
    /// more each until the remainder is used up.</para>
    /// <para><b>Timing.</b> Coin <c>i</c> of <c>n</c> starts at a random moment
    /// of the <c>i</c>-th <c>n</c>-th of <see cref="CoinFlightSettings.MaxDelaySeconds"/>,
    /// so they leave one after another; its flight takes a time between the
    /// shortest and the longest.</para>
    /// <para><b>Randomness</b> is a <see cref="System.Random"/> seeded
    /// explicitly, its draws made in a fixed order per coin.</para>
    /// </remarks>
    public static class CoinFlight
    {
        /// <summary>The coins of a flight of <paramref name="amount"/> gold.</summary>
        /// <param name="amount">The gold that flies. At least 0.</param>
        /// <param name="settings">Counts, spread, times and arcs.</param>
        /// <param name="seed">The flight's seed: the purchase count, or the level and the attempt.</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is negative.</exception>
        public static IReadOnlyList<CoinPiece> Layout(int amount, CoinFlightSettings settings, int seed)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "A flight carries at least no gold.");
            }

            var count = CountOf(amount, settings);
            var pieces = new CoinPiece[count];
            if (count == 0)
            {
                return pieces;
            }

            var random = new System.Random(seed);
            var share = amount / count;
            var remainder = amount % count;
            for (var i = 0; i < count; i++)
            {
                // Below 1 in double precision, so rounding to a float can reach
                // the maximum delay but never pass it.
                var delay = (float)((i + random.NextDouble()) / count * settings.MaxDelaySeconds);
                var angle = random.NextDouble() * 2.0 * Math.PI;
                var radius = Math.Sqrt(random.NextDouble()) * settings.StartSpreadUnits;
                var offset = new Vector2((float)(Math.Cos(angle) * radius), (float)(Math.Sin(angle) * radius));
                var arc = Lerp(settings.ArcMinUnits, settings.ArcMaxUnits, random.NextDouble());
                var seconds = Lerp(settings.SecondsMin, settings.SecondsMax, random.NextDouble());
                pieces[i] = new CoinPiece(delay, offset, arc, seconds, share + (i < remainder ? 1 : 0));
            }

            return pieces;
        }

        /// <summary>How many coins a flight of <paramref name="amount"/> has: one per Gold Per Coin, rounded up, capped; none for none.</summary>
        public static int CountOf(int amount, CoinFlightSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (amount <= 0)
            {
                return 0;
            }

            var coins = (amount - 1) / settings.GoldPerCoin + 1;
            return Math.Min(coins, settings.Cap);
        }

        private static float Lerp(float from, float to, double t) => from + (to - from) * (float)t;
    }
}
