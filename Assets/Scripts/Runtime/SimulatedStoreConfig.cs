using System;
using System.Collections.Generic;
using GateRush.Platform;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// What the simulated purchase service sells (Module 36, D72): each gold
    /// pack's product id and the price its test sheet shows. The gold a pack
    /// gives is a rule, in the <see cref="MetaConfig"/>; a price belongs to
    /// the service, as it would with a real store, which would replace this
    /// asset along with <see cref="SimulatedPurchaseService"/>.
    /// </summary>
    /// <remarks>
    /// A missing config, or one with <see cref="Problems"/>, stops the
    /// bootstrap, as the other configs do.
    /// </remarks>
    [CreateAssetMenu(fileName = "SimulatedStoreConfig", menuName = "Gate Rush/Simulated Store Config")]
    public sealed class SimulatedStoreConfig : ScriptableObject
    {
        [Tooltip("The products the simulated store sells, in any order: the product id, matching a gold pack of the Meta Config, and the price text its test sheet shows. Nothing is charged. Ids not empty and unique, Price Text not empty. A pack is shown only when both configs know its id.")]
        [SerializeField] private ProductEntry[] products =
        {
            new ProductEntry("gold_small", "$0.99"),
            new ProductEntry("gold_medium", "$2.49"),
            new ProductEntry("gold_large", "$4.99"),
        };

        /// <summary>
        /// Everything wrong with this asset, each message naming its field;
        /// empty when <see cref="Products"/> can be sold.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            foreach (var problem in SimulatedPurchaseService.ProblemsOf(Products()))
            {
                problems.Add($"{name}: Products {problem}");
            }

            return problems;
        }

        /// <summary>The products, in the asset's order; a new array on each call.</summary>
        public StoreProduct[] Products()
        {
            var entries = products ?? Array.Empty<ProductEntry>();
            var result = new StoreProduct[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                result[i] = entries[i].ToProduct();
            }

            return result;
        }

        /// <summary>One product, as the Inspector edits it.</summary>
        [Serializable]
        private sealed class ProductEntry
        {
            [SerializeField] private string productId;
            [SerializeField] private string priceText;

            public ProductEntry(string productId, string priceText)
            {
                this.productId = productId;
                this.priceText = priceText;
            }

            public StoreProduct ToProduct() => new StoreProduct(productId, priceText);
        }
    }
}
