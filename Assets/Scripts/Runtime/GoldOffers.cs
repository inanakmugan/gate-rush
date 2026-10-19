using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Platform;

namespace GateRush.Runtime
{
    /// <summary>
    /// Pairs the rules' gold packs with the purchase service's products
    /// (Module 36, D72). A pack is offered only when both sides know its
    /// product id: the rules say what it gives, the service what it costs.
    /// An id known to one side only is a configuration problem, which
    /// <see cref="Mismatches"/> names.
    /// </summary>
    public static class GoldOffers
    {
        /// <summary>
        /// Every pack of <paramref name="packs"/> whose id a product of
        /// <paramref name="products"/> has, in the packs' order, with that
        /// product's price text.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static IReadOnlyList<GoldOffer> Match(IReadOnlyList<GoldPack> packs, IReadOnlyList<StoreProduct> products)
        {
            Require(packs, products);
            var offers = new List<GoldOffer>();
            for (var i = 0; i < packs.Count; i++)
            {
                var pack = packs[i];
                var index = IndexOf(products, pack.ProductId);
                if (index >= 0)
                {
                    offers.Add(new GoldOffer(pack.ProductId, pack.Gold, products[index].PriceText));
                }
            }

            return offers.AsReadOnly();
        }

        /// <summary>
        /// One message for each product id known to one side only: a pack the
        /// purchase service does not sell, or a product no pack gives gold
        /// for. Empty when the two sides agree.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static IReadOnlyList<string> Mismatches(IReadOnlyList<GoldPack> packs, IReadOnlyList<StoreProduct> products)
        {
            Require(packs, products);
            var problems = new List<string>();
            for (var i = 0; i < packs.Count; i++)
            {
                if (IndexOf(products, packs[i].ProductId) < 0)
                {
                    problems.Add(
                        $"The gold pack '{packs[i].ProductId}' of the Meta Config is not sold by the purchase service, so the Store does not show it.");
                }
            }

            for (var i = 0; i < products.Count; i++)
            {
                if (!HasPack(packs, products[i].Id))
                {
                    problems.Add(
                        $"The purchase service sells '{products[i].Id}', which no gold pack of the Meta Config gives gold for, so the Store does not show it.");
                }
            }

            return problems;
        }

        private static int IndexOf(IReadOnlyList<StoreProduct> products, string productId)
        {
            for (var i = 0; i < products.Count; i++)
            {
                if (string.Equals(products[i].Id, productId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasPack(IReadOnlyList<GoldPack> packs, string productId)
        {
            for (var i = 0; i < packs.Count; i++)
            {
                if (string.Equals(packs[i].ProductId, productId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Require(IReadOnlyList<GoldPack> packs, IReadOnlyList<StoreProduct> products)
        {
            if (packs == null)
            {
                throw new ArgumentNullException(nameof(packs));
            }

            if (products == null)
            {
                throw new ArgumentNullException(nameof(products));
            }
        }
    }
}
