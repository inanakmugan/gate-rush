using System;
using GateRush.Runtime;
using GateRush.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 36's <see cref="StoreTexts"/>, formatting only: the gold
    /// amount with thousands separators; "You have N"; the shortfall line;
    /// the price beside the coin; the lives card and the refill button, "Full"
    /// when full; the test sheet's offer; the ribbons; and every text from the
    /// config.
    /// </summary>
    /// <remarks>
    /// Most tests give the config texts of their own, unlike the defaults, so
    /// a text written into the code instead of read from the config fails.
    /// </remarks>
    public class StoreTextsTests
    {
        private UiConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<UiConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void GoldAmount_DefaultFormat_HasThousandsSeparators()
        {
            var text = StoreTexts.GoldAmount(1500, config);

            Assert.AreEqual("1,500", text);
        }

        [Test]
        public void GoldAmount_Gold_UsesTheGoldAmountFormat()
        {
            SetString("goldAmountFormat", "{0} coins");

            var text = StoreTexts.GoldAmount(4000, config);

            Assert.AreEqual("4000 coins", text);
        }

        [Test]
        public void GoldPrice_Price_UsesTheGoldPriceFormat()
        {
            SetString("goldPriceFormat", "x{0}");

            var text = StoreTexts.GoldPrice(200, config);

            Assert.AreEqual("x200", text);
        }

        [Test]
        public void JokerCount_Count_UsesTheJokerCountFormat()
        {
            SetString("jokerCountFormat", "Held: {0}");

            var text = StoreTexts.JokerCount(3, config);

            Assert.AreEqual("Held: 3", text);
        }

        [Test]
        public void Shortfall_Gold_UsesTheShortfallFormat()
        {
            SetString("shortfallFormat", "{0} short");

            var text = StoreTexts.Shortfall(75, config);

            Assert.AreEqual("75 short", text);
        }

        [Test]
        public void LivesStatus_Full_IsTheFullLabel()
        {
            SetString("livesFullLabel", "Topped up");

            var text = StoreTexts.LivesStatus(5, 5, true, config);

            Assert.AreEqual("Topped up", text);
        }

        [Test]
        public void LivesStatus_NotFull_IsTheFraction()
        {
            SetString("livesFractionFormat", "{0} of {1}");

            var text = StoreTexts.LivesStatus(3, 5, false, config);

            Assert.AreEqual("3 of 5", text);
        }

        [Test]
        public void RefillPrice_Full_IsTheFullLabel()
        {
            SetString("livesFullLabel", "Topped up");

            var text = StoreTexts.RefillPrice(300, true, config);

            Assert.AreEqual("Topped up", text);
        }

        [Test]
        public void RefillPrice_NotFull_IsThePrice()
        {
            SetString("goldPriceFormat", "x{0}");

            var text = StoreTexts.RefillPrice(300, false, config);

            Assert.AreEqual("x300", text);
        }

        [Test]
        public void OfferLine_Offer_HasTheGoldAndTheServicePriceText()
        {
            SetString("testPurchaseOfferFormat", "{0:N0} for {1}!");

            var text = StoreTexts.OfferLine(new GoldOffer("pack", 1500, "$2.49"), config);

            Assert.AreEqual("1,500 for $2.49!", text);
        }

        [Test]
        public void Ribbon_DefaultConfig_PopularAndNone()
        {
            var medium = StoreTexts.Ribbon("gold_medium", config);
            var small = StoreTexts.Ribbon("gold_small", config);

            Assert.AreEqual("Popular", medium);
            Assert.IsNull(small);
        }

        [Test]
        public void EveryText_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StoreTexts.GoldAmount(1, null));
            Assert.Throws<ArgumentNullException>(() => StoreTexts.GoldPrice(1, null));
            Assert.Throws<ArgumentNullException>(() => StoreTexts.JokerCount(1, null));
            Assert.Throws<ArgumentNullException>(() => StoreTexts.Shortfall(1, null));
            Assert.Throws<ArgumentNullException>(() => StoreTexts.LivesStatus(1, 2, false, null));
            Assert.Throws<ArgumentNullException>(() => StoreTexts.RefillPrice(1, false, null));
            Assert.Throws<ArgumentNullException>(() => StoreTexts.OfferLine(default, null));
            Assert.Throws<ArgumentNullException>(() => StoreTexts.Ribbon("pack", null));
        }

        private void SetString(string field, string value)
        {
            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"UiConfig has no serialized field '{field}'.");
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
