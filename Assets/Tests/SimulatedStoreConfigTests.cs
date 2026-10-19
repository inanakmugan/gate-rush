using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 36's <see cref="SimulatedStoreConfig"/>: a fresh asset
    /// sells D72's three packs with no problem; an empty or repeated product
    /// id, or a blank price text, is reported by name.
    /// </summary>
    public class SimulatedStoreConfigTests
    {
        private const string ProductsField = "products";
        private const string ProductsLabel = "Products";

        private SimulatedStoreConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulatedStoreConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Problems_FreshConfig_IsEmptyAndSellsThreeProducts()
        {
            var problems = config.Problems();

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
            Assert.AreEqual(3, config.Products().Length);
        }

        [TestCase("", "$1", "x", "$2", TestName = "Problems_ProductWithEmptyId_IsReported")]
        [TestCase("same", "$1", "same", "$2", TestName = "Problems_ProductsWithRepeatedId_IsReported")]
        [TestCase("a", "", "b", "$2", TestName = "Problems_ProductWithBlankPriceText_IsReported")]
        public void Problems_ProductWithAProblem_IsReported(string firstId, string firstPrice, string secondId, string secondPrice)
        {
            SetProducts((firstId, firstPrice), (secondId, secondPrice));

            var problems = config.Problems();

            Assert.That(problems, Has.Some.Contains(ProductsLabel), string.Join(" | ", problems));
        }

        private void SetProducts(params (string id, string price)[] products)
        {
            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty(ProductsField);
            Assert.IsNotNull(entries, $"SimulatedStoreConfig has no serialized field '{ProductsField}'.");
            entries.arraySize = products.Length;
            for (var i = 0; i < products.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("productId").stringValue = products[i].id;
                entry.FindPropertyRelative("priceText").stringValue = products[i].price;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
