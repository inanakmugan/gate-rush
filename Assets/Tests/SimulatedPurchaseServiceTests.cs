using System;
using System.Collections.Generic;
using GateRush.Platform;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 36's <see cref="SimulatedPurchaseService"/>: a confirmed
    /// purchase succeeds and a declined one is cancelled; an id it does not
    /// sell fails without asking; it is busy until the answer, and a purchase
    /// while busy throws; <c>done</c> is called exactly once, after the busy
    /// flag is cleared; and its product list is checked and copied.
    /// </summary>
    public class SimulatedPurchaseServiceTests
    {
        private const string SoldId = "sold";
        private const string OtherSoldId = "also_sold";
        private const string PriceText = "$1.23";

        private Action<bool> pendingAnswer;
        private List<StoreProduct> asked;
        private SimulatedPurchaseService service;

        /// <summary>Product lists each with one problem, named by the problem.</summary>
        public static IEnumerable<TestCaseData> ProductsWithAProblem()
        {
            yield return new TestCaseData((object)new[] { new StoreProduct(string.Empty, PriceText) })
                .SetName("Constructor_ProductWithEmptyId_Throws");
            yield return new TestCaseData((object)new[] { new StoreProduct(SoldId, PriceText), new StoreProduct(SoldId, PriceText) })
                .SetName("Constructor_ProductsWithRepeatedId_Throws");
            yield return new TestCaseData((object)new[] { new StoreProduct(SoldId, " ") })
                .SetName("Constructor_ProductWithBlankPriceText_Throws");
        }

        [SetUp]
        public void SetUp()
        {
            pendingAnswer = null;
            asked = new List<StoreProduct>();
            service = new SimulatedPurchaseService(Products(), Confirm);
        }

        [TestCase(true, PurchaseOutcome.Succeeded, TestName = "Purchase_Confirmed_Succeeds")]
        [TestCase(false, PurchaseOutcome.Cancelled, TestName = "Purchase_Declined_IsCancelled")]
        public void Purchase_Answered_GivesItsOutcome(bool isConfirmed, PurchaseOutcome expected)
        {
            var outcomes = new List<PurchaseOutcome>();
            service.Purchase(SoldId, outcomes.Add);

            pendingAnswer(isConfirmed);

            CollectionAssert.AreEqual(new[] { expected }, outcomes);
            Assert.AreEqual(SoldId, asked[0].Id);
            Assert.AreEqual(PriceText, asked[0].PriceText);
        }

        [Test]
        public void Purchase_UnknownId_FailsWithoutAskingAndIsNotBusy()
        {
            var outcomes = new List<PurchaseOutcome>();

            service.Purchase("unknown", outcomes.Add);

            CollectionAssert.AreEqual(new[] { PurchaseOutcome.Failed }, outcomes);
            CollectionAssert.IsEmpty(asked);
            Assert.IsFalse(service.IsBusy);
        }

        [Test]
        public void IsBusy_UntilAnswered_IsTrueThenFalse()
        {
            service.Purchase(SoldId, _ => { });
            var isBusyWhileAsked = service.IsBusy;

            pendingAnswer(true);

            Assert.IsTrue(isBusyWhileAsked);
            Assert.IsFalse(service.IsBusy);
        }

        [Test]
        public void Purchase_WhileBusy_ThrowsAndLeavesThePendingOne()
        {
            var outcomes = new List<PurchaseOutcome>();
            service.Purchase(SoldId, outcomes.Add);

            Assert.Throws<InvalidOperationException>(() => service.Purchase(OtherSoldId, outcomes.Add));
            pendingAnswer(true);

            Assert.AreEqual(1, asked.Count, "only the first purchase was asked");
            CollectionAssert.AreEqual(new[] { PurchaseOutcome.Succeeded }, outcomes);
        }

        [Test]
        public void Answer_GivenTwice_CallsDoneOnce()
        {
            var outcomes = new List<PurchaseOutcome>();
            service.Purchase(SoldId, outcomes.Add);
            var answer = pendingAnswer;

            answer(true);
            answer(false);

            CollectionAssert.AreEqual(new[] { PurchaseOutcome.Succeeded }, outcomes);
        }

        [Test]
        public void Done_StartsAnotherPurchase_IsAllowed()
        {
            var isBusyInDone = true;
            service.Purchase(SoldId, _ =>
            {
                isBusyInDone = service.IsBusy;
                service.Purchase(OtherSoldId, __ => { });
            });

            pendingAnswer(false);

            Assert.IsFalse(isBusyInDone, "the busy flag is cleared before done runs");
            Assert.AreEqual(2, asked.Count);
            Assert.AreEqual(OtherSoldId, asked[1].Id);
        }

        [Test]
        public void Purchase_ConfirmThrows_IsNotLeftBusy()
        {
            var throwing = new SimulatedPurchaseService(Products(), (_, __) => throw new InvalidOperationException("confirm"));

            Assert.Throws<InvalidOperationException>(() => throwing.Purchase(SoldId, _ => { }));

            Assert.IsFalse(throwing.IsBusy);
        }

        [Test]
        public void Purchase_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => service.Purchase(null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => service.Purchase(SoldId, null));
        }

        [TestCaseSource(nameof(ProductsWithAProblem))]
        public void Constructor_ProductsWithAProblem_Throws(StoreProduct[] products)
        {
            Assert.Throws<ArgumentException>(() => new SimulatedPurchaseService(products, Confirm));
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SimulatedPurchaseService(null, Confirm));
            Assert.Throws<ArgumentNullException>(() => new SimulatedPurchaseService(Products(), null));
        }

        [Test]
        public void Products_SourceChangedAfterConstruction_IsUnchanged()
        {
            var products = Products();
            var copied = new SimulatedPurchaseService(products, Confirm);

            products[0] = new StoreProduct("changed", PriceText);

            Assert.AreEqual(SoldId, copied.Products[0].Id);
        }

        private static StoreProduct[] Products() => new[]
        {
            new StoreProduct(SoldId, PriceText),
            new StoreProduct(OtherSoldId, PriceText),
        };

        private void Confirm(StoreProduct product, Action<bool> answer)
        {
            asked.Add(product);
            pendingAnswer = answer;
        }
    }
}
