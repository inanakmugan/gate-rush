using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 30's <see cref="Wallet"/>: a new player's wallet holds
    /// the starting gold and no lifetime gold (D22); earning raises both,
    /// spending lowers only the balance and never below zero; zero changes
    /// nothing, a negative amount throws, and sums stop at
    /// <see cref="int.MaxValue"/>.
    /// </summary>
    public class WalletTests
    {
        private const int Balance = 640;
        private const int LifetimeEarned = 140;
        private const int Amount = 25;

        [Test]
        public void Starting_AnyRules_HoldsStartingGoldWithLifetimeZero()
        {
            var rules = new MetaRules(3, TimeSpan.FromMinutes(7), 321);

            var wallet = Wallet.Starting(rules);

            Assert.AreEqual(rules.StartingGold, wallet.Balance);
            Assert.AreEqual(0, wallet.LifetimeEarned);
        }

        [Test]
        public void Starting_NullRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Wallet.Starting(null));
        }

        [Test]
        public void Earn_Amount_RaisesBalanceAndLifetime()
        {
            var wallet = new Wallet(Balance, LifetimeEarned);

            wallet.Earn(Amount);

            Assert.AreEqual(Balance + Amount, wallet.Balance);
            Assert.AreEqual(LifetimeEarned + Amount, wallet.LifetimeEarned);
        }

        [Test]
        public void TrySpend_WithinBalance_LowersOnlyBalance()
        {
            var wallet = new Wallet(Balance, LifetimeEarned);

            var isSpent = wallet.TrySpend(Amount);

            Assert.IsTrue(isSpent);
            Assert.AreEqual(Balance - Amount, wallet.Balance);
            Assert.AreEqual(LifetimeEarned, wallet.LifetimeEarned);
        }

        [Test]
        public void TrySpend_WholeBalance_LeavesZero()
        {
            var wallet = new Wallet(Balance, LifetimeEarned);

            var isSpent = wallet.TrySpend(Balance);

            Assert.IsTrue(isSpent);
            Assert.AreEqual(0, wallet.Balance);
        }

        [Test]
        public void TrySpend_MoreThanBalance_IsFalseAndChangesNothing()
        {
            var wallet = new Wallet(Balance, LifetimeEarned);

            var isSpent = wallet.TrySpend(Balance + 1);

            Assert.IsFalse(isSpent);
            Assert.AreEqual(Balance, wallet.Balance);
            Assert.AreEqual(LifetimeEarned, wallet.LifetimeEarned);
        }

        [Test]
        public void EarnAndTrySpend_Zero_ChangeNothing()
        {
            var wallet = new Wallet(Balance, LifetimeEarned);

            wallet.Earn(0);
            var isSpent = wallet.TrySpend(0);

            Assert.IsTrue(isSpent);
            Assert.AreEqual(Balance, wallet.Balance);
            Assert.AreEqual(LifetimeEarned, wallet.LifetimeEarned);
        }

        [Test]
        public void EarnAndTrySpend_NegativeAmount_Throw()
        {
            var wallet = new Wallet(Balance, LifetimeEarned);

            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Earn(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-1));
            Assert.AreEqual(Balance, wallet.Balance);
            Assert.AreEqual(LifetimeEarned, wallet.LifetimeEarned);
        }

        [Test]
        public void Earn_PastIntMax_StopsAtIntMax()
        {
            var wallet = new Wallet(int.MaxValue - 1, int.MaxValue - 2);

            wallet.Earn(Amount);

            Assert.AreEqual(int.MaxValue, wallet.Balance);
            Assert.AreEqual(int.MaxValue, wallet.LifetimeEarned);
        }

        [Test]
        public void Constructor_NegativeBalanceOrLifetime_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(-1, LifetimeEarned));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(Balance, -1));
        }
    }
}
