using System;
using System.Globalization;
using GateRush.Runtime;

namespace GateRush.UI
{
    /// <summary>
    /// The Store's texts (Module 36): formatting only, every text from
    /// <see cref="UiConfig"/>'s formats and labels, read with the invariant
    /// culture. The numbers come from <see cref="FrontEndState"/>; nothing here
    /// decides a rule.
    /// </summary>
    public static class StoreTexts
    {
        /// <summary>A gold pack's amount, through <see cref="UiConfig.GoldAmountFormat"/>: "1,500" by default.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string GoldAmount(int gold, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.GoldAmountFormat, gold);
        }

        /// <summary>A price in gold on a buy button, shown beside the coin.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string GoldPrice(int price, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.GoldPriceFormat, price);
        }

        /// <summary>How many of a joker the player holds: "You have N" by default.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string JokerCount(int count, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.JokerCountFormat, count);
        }

        /// <summary>
        /// A locked joker's card line, in place of its count and price (D76):
        /// "Unlocks at Level 10" by default.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string UnlocksAt(int unlockLevel, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.UnlocksAtFormat, unlockLevel);
        }

        /// <summary>The Not Enough Gold line, with <paramref name="shortfall"/> as the bootstrap worked it out.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string Shortfall(int shortfall, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.ShortfallFormat, shortfall);
        }

        /// <summary>
        /// The lives card's line: <see cref="UiConfig.LivesFullLabel"/> when
        /// <paramref name="isFull"/>, otherwise the lives of the cap.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string LivesStatus(int lives, int cap, bool isFull, UiConfig config)
        {
            Require(config);
            return isFull ? config.LivesFullLabel : HomeTexts.LivesFraction(lives, cap, config);
        }

        /// <summary>
        /// The refill button's label on the Store: <see cref="UiConfig.LivesFullLabel"/>
        /// when <paramref name="isFull"/>, otherwise the price beside the coin.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string RefillPrice(int price, bool isFull, UiConfig config)
        {
            Require(config);
            return isFull ? config.LivesFullLabel : GoldPrice(price, config);
        }

        /// <summary>The test purchase sheet's offer: the gold and the service's price text.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string OfferLine(GoldOffer offer, UiConfig config)
        {
            Require(config);
            return string.Format(CultureInfo.InvariantCulture, config.TestPurchaseOfferFormat, offer.Gold, offer.PriceText);
        }

        /// <summary>The ribbon on <paramref name="productId"/>'s card; null when it has none.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string Ribbon(string productId, UiConfig config)
        {
            Require(config);
            return config.RibbonFor(productId);
        }

        private static void Require(UiConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
        }
    }
}
