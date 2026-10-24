using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// Module 47, pass 2: the gold that flies to its counter, the counter's
    /// pop and lift above a scrim, and the result panel's win pops. Every time
    /// is unscaled.
    /// </summary>
    public sealed partial class UiConfig
    {
        [Header("Gold flying to its counter (Module 47; unscaled time)")]
        [Tooltip("The most coins one flight has: from 1 to 24.")]
        [SerializeField] private int coinFlightCap = 10;

        [Tooltip("Gold one coin stands for before the cap, so the count grows with the amount: from 1 to 1000.")]
        [SerializeField] private int coinFlightGoldPerCoin = 10;

        [Tooltip("The radius of the disc around the source the coins start in, in canvas units: from 0 to 200.")]
        [SerializeField] private float coinFlightStartSpreadUnits = 40f;

        [Tooltip("The latest a coin leaves after the flight starts, in seconds; the coins leave one after another across it: from 0 to 1.5.")]
        [SerializeField] private float coinFlightMaxDelaySeconds = 0.35f;

        [Tooltip("How long one coin's flight takes, in seconds: x the shortest, y the longest, above 0 and at most 2.")]
        [SerializeField] private Vector2 coinFlightSeconds = new Vector2(0.45f, 0.65f);

        [Tooltip("How high a coin's arc rises above the straight line, in canvas units: x the lowest, y the highest, from 0 to 600.")]
        [SerializeField] private Vector2 coinFlightArcUnits = new Vector2(80f, 180f);

        [Tooltip("A flying coin's side, in canvas units. Above 0.")]
        [SerializeField] private float coinFlightSizeUnits = 64f;

        [Tooltip("A coin's scale when it lands, from above 0 to 1: it shrinks a little as it arrives.")]
        [SerializeField] private float coinFlightEndScale = 0.7f;

        [SerializeField] private Ease coinFlightEase = Ease.InOutQuad;

        [Tooltip("Seconds a campaign win's coins wait after the result panel opens, so its pop and the coin's spin come first: from 0 to 2.")]
        [SerializeField] private float coinFlightWinDelaySeconds = 0.45f;

        [Tooltip("The least time between two UiTap cues of landing coins, in seconds, so a flight does not rattle: from 0 to 1.")]
        [SerializeField] private float coinTapMinIntervalSeconds = 0.08f;

        [Tooltip("The scale the gold pill pops to as a coin lands: from 1 to 1.5.")]
        [SerializeField] private float goldPopScale = 1.12f;

        [Tooltip("Seconds the gold pill's pop takes, up and back: above 0, at most 1.")]
        [SerializeField] private float goldPopSeconds = 0.15f;

        [Tooltip("How far above the screen's canvas the gold pill sorts while coins fly to it, so it shows over any scrim: from 1 to 100.")]
        [SerializeField] private int goldLiftSortingOffset = 10;

        [Tooltip("How far above the screen's canvas the flying coins sort: above Gold Lift Sorting Offset, so they land over the pill; at most 101.")]
        [SerializeField] private int coinFlightSortingOffset = 11;

        [Header("Result panel on a win (Module 47; unscaled time)")]
        [Tooltip("The scale the title pops to once the panel has opened: from 1 to 1.5.")]
        [SerializeField] private float resultTitlePopScale = 1.12f;

        [Tooltip("Seconds the title's pop takes, up and back: above 0, at most 1.")]
        [SerializeField] private float resultTitlePopSeconds = 0.25f;

        [Tooltip("Seconds the gold row's coin takes to spin in, growing from nothing: above 0, at most 2.")]
        [SerializeField] private float resultCoinSpinSeconds = 0.4f;

        [Tooltip("How far the gold row's coin turns as it spins in, in degrees: from 0 to 1080.")]
        [SerializeField] private float resultCoinSpinDegrees = 360f;

        [SerializeField] private Ease resultCoinSpinEase = Ease.OutBack;

        /// <summary>The most coins one flight has: the size of each coin pool.</summary>
        public int CoinFlightCap => coinFlightCap;

        /// <summary>A flying coin's side, in canvas units.</summary>
        public float CoinFlightSizeUnits => coinFlightSizeUnits;

        /// <summary>A coin's scale when it lands.</summary>
        public float CoinFlightEndScale => coinFlightEndScale;

        /// <summary>How a coin moves along its flight.</summary>
        public Ease CoinFlightEase => coinFlightEase;

        /// <summary>Seconds a campaign win's coins wait after the result panel opens.</summary>
        public float CoinFlightWinDelaySeconds => coinFlightWinDelaySeconds;

        /// <summary>The least time between two UiTap cues of landing coins.</summary>
        public float CoinTapMinIntervalSeconds => coinTapMinIntervalSeconds;

        /// <summary>The scale the gold pill pops to as a coin lands.</summary>
        public float GoldPopScale => goldPopScale;

        /// <summary>Seconds the gold pill's pop takes.</summary>
        public float GoldPopSeconds => goldPopSeconds;

        /// <summary>How far above the screen's canvas the gold pill sorts while coins fly to it.</summary>
        public int GoldLiftSortingOffset => goldLiftSortingOffset;

        /// <summary>How far above the screen's canvas the flying coins sort.</summary>
        public int CoinFlightSortingOffset => coinFlightSortingOffset;

        /// <summary>The scale the win's title pops to.</summary>
        public float ResultTitlePopScale => resultTitlePopScale;

        /// <summary>Seconds the win's title pop takes.</summary>
        public float ResultTitlePopSeconds => resultTitlePopSeconds;

        /// <summary>Seconds the gold row's coin takes to spin in.</summary>
        public float ResultCoinSpinSeconds => resultCoinSpinSeconds;

        /// <summary>How far the gold row's coin turns as it spins in, in degrees.</summary>
        public float ResultCoinSpinDegrees => resultCoinSpinDegrees;

        /// <summary>How the gold row's coin spins in.</summary>
        public Ease ResultCoinSpinEase => resultCoinSpinEase;

        /// <summary>The layout values of a coin flight (<see cref="CoinFlight"/>).</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is out of range; see <see cref="Problems"/>.</exception>
        public CoinFlightSettings CreateCoinFlightSettings() =>
            new CoinFlightSettings(
                coinFlightCap, coinFlightGoldPerCoin, coinFlightStartSpreadUnits, coinFlightMaxDelaySeconds,
                coinFlightSeconds.x, coinFlightSeconds.y, coinFlightArcUnits.x, coinFlightArcUnits.y);

        // The bounds of the counter's and the panel's pops and of the sorting
        // offsets. Not tunables; the values inside them are.
        private const float MaxPopScale = 1.5f;
        private const float MaxPopSeconds = 1f;
        private const float MaxSpinSeconds = 2f;
        private const float MaxSpinDegrees = 1080f;
        private const float MaxWinDelaySeconds = 2f;
        private const float MaxTapIntervalSeconds = 1f;
        private const int MaxSortingOffset = 100;

        /// <summary>The constraints on Module 47's gold flight and the win's pops, each message naming its field.</summary>
        private void AddGoldFlightProblems(List<string> problems)
        {
            foreach (var problem in CoinFlightSettings.Problems(
                         coinFlightCap, coinFlightGoldPerCoin, coinFlightStartSpreadUnits, coinFlightMaxDelaySeconds,
                         coinFlightSeconds.x, coinFlightSeconds.y, coinFlightArcUnits.x, coinFlightArcUnits.y))
            {
                problems.Add($"{name}: {problem}");
            }

            if (!(coinFlightSizeUnits > 0f) || float.IsInfinity(coinFlightSizeUnits))
            {
                problems.Add($"{name}: Coin Flight Size Units must be positive and finite.");
            }

            if (!(coinFlightEndScale > 0f && coinFlightEndScale <= 1f))
            {
                problems.Add($"{name}: Coin Flight End Scale must be above 0 and at most 1.");
            }

            if (!(coinFlightWinDelaySeconds >= 0f && coinFlightWinDelaySeconds <= MaxWinDelaySeconds))
            {
                problems.Add($"{name}: Coin Flight Win Delay Seconds must be from 0 to {MaxWinDelaySeconds}.");
            }

            if (!(coinTapMinIntervalSeconds >= 0f && coinTapMinIntervalSeconds <= MaxTapIntervalSeconds))
            {
                problems.Add($"{name}: Coin Tap Min Interval Seconds must be from 0 to {MaxTapIntervalSeconds}.");
            }

            if (!(goldPopScale >= 1f && goldPopScale <= MaxPopScale))
            {
                problems.Add($"{name}: Gold Pop Scale must be from 1 to {MaxPopScale}.");
            }

            if (!(goldPopSeconds > 0f && goldPopSeconds <= MaxPopSeconds))
            {
                problems.Add($"{name}: Gold Pop Seconds must be above 0 and at most {MaxPopSeconds}.");
            }

            if (!(goldLiftSortingOffset >= 1 && goldLiftSortingOffset <= MaxSortingOffset))
            {
                problems.Add($"{name}: Gold Lift Sorting Offset must be from 1 to {MaxSortingOffset}.");
            }

            if (!(coinFlightSortingOffset > goldLiftSortingOffset && coinFlightSortingOffset <= MaxSortingOffset + 1))
            {
                problems.Add($"{name}: Coin Flight Sorting Offset must be above Gold Lift Sorting Offset ({goldLiftSortingOffset}) and at most {MaxSortingOffset + 1}, so the coins land over the pill.");
            }

            if (!(resultTitlePopScale >= 1f && resultTitlePopScale <= MaxPopScale))
            {
                problems.Add($"{name}: Result Title Pop Scale must be from 1 to {MaxPopScale}.");
            }

            if (!(resultTitlePopSeconds > 0f && resultTitlePopSeconds <= MaxPopSeconds))
            {
                problems.Add($"{name}: Result Title Pop Seconds must be above 0 and at most {MaxPopSeconds}.");
            }

            if (!(resultCoinSpinSeconds > 0f && resultCoinSpinSeconds <= MaxSpinSeconds))
            {
                problems.Add($"{name}: Result Coin Spin Seconds must be above 0 and at most {MaxSpinSeconds}.");
            }

            if (!(resultCoinSpinDegrees >= 0f && resultCoinSpinDegrees <= MaxSpinDegrees))
            {
                problems.Add($"{name}: Result Coin Spin Degrees must be from 0 to {MaxSpinDegrees}.");
            }
        }
    }
}
