using System;
using GateRush.UI;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 47's <see cref="CoinFlight"/>: the same seed lays out the
    /// same coins; the count grows with the amount and never passes the cap;
    /// the shares are whole and sum to the amount, for 1, the cap's worth and
    /// well over it; every delay, time, arc and start lies in its range; no
    /// gold flies no coins.
    /// </summary>
    public class CoinFlightTests
    {
        private const int Cap = 10;
        private const int GoldPerCoin = 10;
        private const float Spread = 40f;
        private const float MaxDelay = 0.35f;
        private const float SecondsMin = 0.45f;
        private const float SecondsMax = 0.65f;
        private const float ArcMin = 80f;
        private const float ArcMax = 180f;
        private const int Seed = 17;

        // A float's rounding at the ends of a range.
        private const float Tolerance = 1e-4f;

        [Test]
        public void Layout_SameSeed_GivesTheSameCoins()
        {
            var first = CoinFlight.Layout(250, Settings(), Seed);

            var second = CoinFlight.Layout(250, Settings(), Seed);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Layout_DifferentSeeds_GiveDifferentCoins()
        {
            var first = CoinFlight.Layout(250, Settings(), Seed);

            var second = CoinFlight.Layout(250, Settings(), Seed + 1);

            CollectionAssert.AreNotEqual(first, second);
        }

        [TestCase(1, 1)]
        [TestCase(GoldPerCoin, 1)]
        [TestCase(GoldPerCoin + 1, 2)]
        [TestCase(Cap * GoldPerCoin, Cap)]
        [TestCase(100000, Cap)]
        public void Layout_Amount_HasOneCoinPerGoldPerCoinRoundedUpWithinTheCap(int amount, int expectedCoins)
        {
            var coins = CoinFlight.Layout(amount, Settings(), Seed);

            Assert.AreEqual(expectedCoins, coins.Count);
        }

        [TestCase(1)]
        [TestCase(Cap * GoldPerCoin)]
        [TestCase(Cap * GoldPerCoin + 7)]
        [TestCase(100000)]
        public void Layout_Amount_SharesAreWholeAndSumToIt(int amount)
        {
            var coins = CoinFlight.Layout(amount, Settings(), Seed);

            var sum = 0;
            foreach (var coin in coins)
            {
                Assert.That(coin.Share, Is.GreaterThanOrEqualTo(1), "every coin carries gold");
                sum += coin.Share;
            }

            Assert.AreEqual(amount, sum);
        }

        [Test]
        public void Layout_EveryCoin_LiesWithinItsRanges()
        {
            var coins = CoinFlight.Layout(100000, Settings(), Seed);

            foreach (var coin in coins)
            {
                Assert.That(coin.DelaySeconds, Is.InRange(0f, MaxDelay + Tolerance), "delay");
                Assert.That(coin.Seconds, Is.InRange(SecondsMin - Tolerance, SecondsMax + Tolerance), "time");
                Assert.That(coin.ArcUnits, Is.InRange(ArcMin - Tolerance, ArcMax + Tolerance), "arc");
                Assert.That(coin.StartOffset.magnitude, Is.LessThanOrEqualTo(Spread + Tolerance), "start within the spread");
            }
        }

        [Test]
        public void Layout_ManyCoins_LeaveOneAfterAnother()
        {
            var coins = CoinFlight.Layout(100000, Settings(), Seed);

            for (var i = 1; i < coins.Count; i++)
            {
                Assert.That(coins[i].DelaySeconds, Is.GreaterThanOrEqualTo(coins[i - 1].DelaySeconds), $"coin {i}");
            }
        }

        [Test]
        public void Layout_NoGold_HasNoCoins()
        {
            var coins = CoinFlight.Layout(0, Settings(), Seed);

            Assert.IsEmpty(coins);
        }

        [Test]
        public void Layout_NegativeAmount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CoinFlight.Layout(-1, Settings(), Seed));
        }

        [TestCase(0, "Coin Flight Cap")]
        [TestCase(25, "Coin Flight Cap")]
        public void Problems_CapOutOfRange_IsReportedByName(int cap, string label)
        {
            var problems = CoinFlightSettings.Problems(cap, GoldPerCoin, Spread, MaxDelay, SecondsMin, SecondsMax, ArcMin, ArcMax);

            Assert.That(problems, Has.Some.Contains(label));
        }

        [Test]
        public void Problems_ShortestTimeAboveTheLongest_IsReportedByName()
        {
            var problems = CoinFlightSettings.Problems(Cap, GoldPerCoin, Spread, MaxDelay, SecondsMax, SecondsMin, ArcMin, ArcMax);

            Assert.That(problems, Has.Some.Contains("Coin Flight Seconds"));
        }

        private static CoinFlightSettings Settings() =>
            new CoinFlightSettings(Cap, GoldPerCoin, Spread, MaxDelay, SecondsMin, SecondsMax, ArcMin, ArcMax);
    }
}
