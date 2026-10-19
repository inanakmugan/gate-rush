using System;
using System.Collections.Generic;

namespace GateRush.Meta
{
    /// <summary>
    /// One gold pack the store sells for real money (Module 36, D72): the
    /// product id a purchase service sells it under, and the gold it gives.
    /// Its price belongs to the service, not to the rules. Plain data with no
    /// checks of its own: a list is checked whole by <see cref="ProblemsOf"/>,
    /// since whether an id repeats depends on the others.
    /// </summary>
    public readonly struct GoldPack
    {
        /// <summary>The pack <paramref name="productId"/>, giving <paramref name="gold"/>.</summary>
        public GoldPack(string productId, int gold)
        {
            ProductId = productId;
            Gold = gold;
        }

        /// <summary>The product id. Not blank, and unique in a valid list.</summary>
        public string ProductId { get; }

        /// <summary>The gold the pack adds to the balance. At least 1 in a valid list.</summary>
        public int Gold { get; }

        /// <summary>
        /// Everything wrong with <paramref name="packs"/>, one sentence per
        /// problem, each starting with the element's index: a blank
        /// <see cref="ProductId"/>, one already used by an earlier element, or
        /// a <see cref="Gold"/> below 1. Empty when the list is valid; an
        /// empty list is valid and sells nothing. The one check of a list:
        /// <see cref="MetaRules"/> throws on it, and the configuration asset
        /// reports it.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="packs"/> is null.</exception>
        public static IReadOnlyList<string> ProblemsOf(IReadOnlyList<GoldPack> packs)
        {
            if (packs == null)
            {
                throw new ArgumentNullException(nameof(packs));
            }

            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < packs.Count; i++)
            {
                var pack = packs[i];
                if (string.IsNullOrWhiteSpace(pack.ProductId))
                {
                    problems.Add($"element {i}: Product Id is empty.");
                }
                else if (!seen.Add(pack.ProductId))
                {
                    problems.Add($"element {i}: Product Id '{pack.ProductId}' is used by an earlier element.");
                }

                if (pack.Gold < 1)
                {
                    problems.Add($"element {i}: Gold must be at least 1, not {pack.Gold}.");
                }
            }

            return problems;
        }
    }
}
