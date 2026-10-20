using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="JokerButtons"/> (Modules 34 and 40): what a joker
    /// button's badge shows and whether it can be pressed, in the campaign
    /// (D65) and in free play (D66). In the campaign with none held and a
    /// price above the balance, the button can be pressed and carries the
    /// shortfall, so a press opens Not Enough Gold (D74); with a price the
    /// balance covers it behaves as before; free play is unchanged and never
    /// short of gold.
    /// </summary>
    public class JokerButtonsTests
    {
        private const int Held = 2;
        private const int Price = 200;
        private const int AmpleGold = 1000;

        [Test]
        public void Campaign_SomeHeld_ShowsTheCountAndIsPressable()
        {
            var button = JokerButtons.Campaign(Held, Price, 0, true);

            Assert.AreEqual(JokerBadge.Count, button.Badge);
            Assert.AreEqual(Held, button.Amount);
            Assert.IsTrue(button.IsInteractable, "a held joker needs no gold");
            Assert.IsFalse(button.IsShortOfGold);
        }

        [TestCase(Price)]
        [TestCase(Price + 1)]
        public void Campaign_NoneHeldAndTheBalanceCoversThePrice_ShowsThePriceAndIsPressable(int balance)
        {
            var button = JokerButtons.Campaign(0, Price, balance, true);

            Assert.AreEqual(JokerBadge.Price, button.Badge);
            Assert.AreEqual(Price, button.Amount);
            Assert.IsTrue(button.IsInteractable);
            Assert.IsFalse(button.IsShortOfGold, "bought when it is used, as before (D65)");
            Assert.AreEqual(0, button.Shortfall);
        }

        [TestCase(Price - 1)]
        [TestCase(0)]
        public void Campaign_NoneHeldAndTheBalanceShort_IsPressableAndCarriesTheShortfall(int balance)
        {
            var button = JokerButtons.Campaign(0, Price, balance, true);

            Assert.AreEqual(JokerBadge.Price, button.Badge);
            Assert.AreEqual(Price, button.Amount);
            Assert.IsTrue(button.IsInteractable, "a press opens Not Enough Gold (D74)");
            Assert.IsTrue(button.IsShortOfGold);
            Assert.AreEqual(Price - balance, button.Shortfall);
        }

        [TestCase(Held, AmpleGold)]
        [TestCase(0, 0)]
        public void Campaign_NotUsable_IsNotPressableWhateverIsHeld(int held, int balance)
        {
            var button = JokerButtons.Campaign(held, Price, balance, false);

            Assert.IsFalse(button.IsInteractable);
        }

        [Test]
        public void FreePlay_SomeLeft_ShowsWhatIsLeftAndIsPressable()
        {
            var button = JokerButtons.FreePlay(Held, true);

            Assert.AreEqual(JokerBadge.Count, button.Badge);
            Assert.AreEqual(Held, button.Amount);
            Assert.IsTrue(button.IsInteractable);
        }

        [Test]
        public void FreePlay_NoneLeft_ShowsZeroAndIsNotPressableNorShortOfGold()
        {
            var button = JokerButtons.FreePlay(0, true);

            Assert.AreEqual(JokerBadge.Count, button.Badge);
            Assert.AreEqual(0, button.Amount);
            Assert.IsFalse(button.IsInteractable, "free play is never topped up");
            Assert.IsFalse(button.IsShortOfGold, "free play never reads the gold (D66)");
        }

        [Test]
        public void FreePlay_NotUsable_IsNotPressable()
        {
            var button = JokerButtons.FreePlay(Held, false);

            Assert.IsFalse(button.IsInteractable);
        }

        [Test]
        public void Equals_SameDecision_IsEqual()
        {
            var first = JokerButtons.Campaign(0, Price, Price - 1, true);
            var second = JokerButtons.Campaign(0, Price, Price - 1, true);

            Assert.IsTrue(first == second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [Test]
        public void Equals_DifferentShortfall_IsNotEqual()
        {
            var first = JokerButtons.Campaign(0, Price, Price - 1, true);
            var second = JokerButtons.Campaign(0, Price, Price - 2, true);

            Assert.IsTrue(first != second);
        }
    }
}
