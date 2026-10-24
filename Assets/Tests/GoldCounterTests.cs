using System.Collections.Generic;
using GateRush.UI;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 47's <see cref="GoldCounter"/>: not held it shows the
    /// wallet; held it shows the held value whatever the wallet says, and each
    /// share adds to it with a pop; after the last share and a release it
    /// shows the wallet; a release at any point shows the wallet at once; and
    /// the label is written only when the number changes. The rule the tests
    /// guard: once a flight is over, the counter never shows anything but the
    /// wallet's value.
    /// </summary>
    public class GoldCounterTests
    {
        private readonly List<int> written = new List<int>();
        private int pops;
        private GoldCounter counter;

        [SetUp]
        public void SetUp()
        {
            written.Clear();
            pops = 0;
            counter = new GoldCounter(value => written.Add(value), () => pops++);
        }

        [Test]
        public void Show_NotHeld_ShowsTheWallet()
        {
            counter.Show(120);

            Assert.AreEqual(120, counter.Shown);
            Assert.IsFalse(counter.IsHeld);
        }

        [Test]
        public void Show_Held_KeepsShowingTheHeldValue()
        {
            counter.Show(100);
            counter.Hold(100);

            counter.Show(150);

            Assert.AreEqual(100, counter.Shown);
        }

        [Test]
        public void Add_Held_AddsTheShareAndPops()
        {
            counter.Show(100);
            counter.Hold(100);
            counter.Show(150);

            counter.Add(20);

            Assert.AreEqual(120, counter.Shown);
            Assert.AreEqual(1, pops);
        }

        [Test]
        public void Add_EveryShareThenRelease_ShowsExactlyTheWallet()
        {
            counter.Show(100);
            counter.Hold(100);
            counter.Show(150);

            counter.Add(20);
            counter.Add(20);
            counter.Add(10);
            counter.Release();

            Assert.AreEqual(150, counter.Shown);
            Assert.IsFalse(counter.IsHeld);
        }

        [Test]
        public void Release_PartWayThrough_ShowsTheWalletAtOnce()
        {
            counter.Show(100);
            counter.Hold(100);
            counter.Show(150);
            counter.Add(20);

            counter.Release();

            Assert.AreEqual(150, counter.Shown);
        }

        [Test]
        public void Release_WalletSpentDuringTheFlight_ShowsTheWalletNotTheSum()
        {
            // Gold spent while held: the wallet is the truth when the flight ends.
            counter.Show(100);
            counter.Hold(100);
            counter.Show(150);
            counter.Add(50);
            counter.Show(90);

            counter.Release();

            Assert.AreEqual(90, counter.Shown);
        }

        [Test]
        public void Add_NotHeld_IsIgnored()
        {
            counter.Show(100);

            counter.Add(20);

            Assert.AreEqual(100, counter.Shown);
            Assert.AreEqual(0, pops);
        }

        [Test]
        public void Show_AfterRelease_ShowsTheWalletAgainAtOnce()
        {
            counter.Show(100);
            counter.Hold(100);
            counter.Release();

            counter.Show(80);

            Assert.AreEqual(80, counter.Shown);
        }

        [Test]
        public void Show_SameValueTwice_WritesTheLabelOnce()
        {
            counter.Show(100);
            counter.Show(100);
            counter.Show(100);

            CollectionAssert.AreEqual(new[] { 100 }, written);
        }
    }
}
