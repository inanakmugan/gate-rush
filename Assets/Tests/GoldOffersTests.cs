using GateRush.Meta;
using GateRush.Platform;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 36's <see cref="GoldOffers"/>: a pack is offered only
    /// when the purchase service sells its id, in the rules' order with the
    /// service's price text; every id known to one side only is named, and
    /// two sides that agree give no message.
    /// </summary>
    public class GoldOffersTests
    {
        [Test]
        public void Match_PacksAndProducts_OffersOnlyBothInTheRulesOrderWithTheServicePrice()
        {
            var packs = new[] { new GoldPack("c", 30), new GoldPack("rules_only", 40), new GoldPack("a", 10) };
            var products = new[] { new StoreProduct("a", "$a"), new StoreProduct("service_only", "$s"), new StoreProduct("c", "$c") };

            var offers = GoldOffers.Match(packs, products);

            Assert.AreEqual(2, offers.Count);
            Assert.AreEqual("c", offers[0].ProductId);
            Assert.AreEqual(30, offers[0].Gold);
            Assert.AreEqual("$c", offers[0].PriceText);
            Assert.AreEqual("a", offers[1].ProductId);
            Assert.AreEqual(10, offers[1].Gold);
            Assert.AreEqual("$a", offers[1].PriceText);
        }

        [Test]
        public void Mismatches_IdOnOneSideOnly_NamesEach()
        {
            var packs = new[] { new GoldPack("both", 10), new GoldPack("rules_only", 20) };
            var products = new[] { new StoreProduct("both", "$1"), new StoreProduct("service_only", "$2") };

            var problems = GoldOffers.Mismatches(packs, products);

            Assert.AreEqual(2, problems.Count);
            Assert.That(problems, Has.Some.Contains("'rules_only'"));
            Assert.That(problems, Has.Some.Contains("'service_only'"));
        }

        [Test]
        public void Mismatches_SidesAgree_IsEmpty()
        {
            var packs = new[] { new GoldPack("a", 10), new GoldPack("b", 20) };
            var products = new[] { new StoreProduct("b", "$2"), new StoreProduct("a", "$1") };

            var problems = GoldOffers.Mismatches(packs, products);

            CollectionAssert.IsEmpty(problems);
        }
    }
}
