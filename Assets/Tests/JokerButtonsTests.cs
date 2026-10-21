using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="JokerButtons"/> (Modules 34, 40 and 43): what a joker
    /// button's badge shows and whether it can be pressed, in the campaign
    /// (D65) and in free play (D66). In the campaign with none held and a
    /// price above the balance, the button can be pressed and carries the
    /// shortfall, so a press opens Not Enough Gold (D74); with a price the
    /// balance covers it behaves as before; free play is unchanged and never
    /// short of gold. A locked campaign joker (D76) shows its unlock level and
    /// can never be pressed, short of gold or marked; an unlocked one carries
    /// its mark; free play is never locked or marked.
    /// </summary>
    public class JokerButtonsTests
    {
        private const int Held = 2;
        private const int Price = 200;
        private const int AmpleGold = 1000;
        private const int UnlockLevel = 10;

        [Test]
        public void Campaign_SomeHeld_ShowsTheCountAndIsPressable()
        {
            var button = JokerButtons.Campaign(Held, Price, 0, true, false);

            Assert.AreEqual(JokerBadge.Count, button.Badge);
            Assert.AreEqual(Held, button.Amount);
            Assert.IsTrue(button.IsInteractable, "a held joker needs no gold");
            Assert.IsFalse(button.IsShortOfGold);
            Assert.IsFalse(button.IsLocked, "an unlocked joker is as before");
        }

        [TestCase(Price)]
        [TestCase(Price + 1)]
        public void Campaign_NoneHeldAndTheBalanceCoversThePrice_ShowsThePriceAndIsPressable(int balance)
        {
            var button = JokerButtons.Campaign(0, Price, balance, true, false);

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
            var button = JokerButtons.Campaign(0, Price, balance, true, false);

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
            var button = JokerButtons.Campaign(held, Price, balance, false, false);

            Assert.IsFalse(button.IsInteractable);
        }

        [TestCase(Held, true)]
        [TestCase(Held, false)]
        [TestCase(0, true)]
        [TestCase(0, false)]
        public void Campaign_Mark_IsCarriedWhateverTheBadge(int held, bool isMarked)
        {
            var button = JokerButtons.Campaign(held, Price, AmpleGold, true, isMarked);

            Assert.AreEqual(isMarked, button.IsMarked);
        }

        [Test]
        public void Locked_UnlockLevel_ShowsTheLevelAndIsNeitherPressableShortOfGoldNorMarked()
        {
            var button = JokerButtons.Locked(UnlockLevel);

            Assert.AreEqual(JokerBadge.Locked, button.Badge);
            Assert.IsTrue(button.IsLocked);
            Assert.AreEqual(UnlockLevel, button.Amount, "the badge reads the level it opens at");
            Assert.IsFalse(button.IsInteractable, "a locked joker raises no press (D76)");
            Assert.IsFalse(button.IsShortOfGold, "so it never opens Not Enough Gold");
            Assert.IsFalse(button.IsMarked);
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
        public void FreePlay_AnyAllowance_IsNeverLockedNorMarked([Values(0, Held)] int left)
        {
            var button = JokerButtons.FreePlay(left, true);

            Assert.IsFalse(button.IsLocked);
            Assert.IsFalse(button.IsMarked);
        }

        [Test]
        public void Equals_SameDecision_IsEqual()
        {
            var first = JokerButtons.Campaign(0, Price, Price - 1, true, false);
            var second = JokerButtons.Campaign(0, Price, Price - 1, true, false);

            Assert.IsTrue(first == second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [Test]
        public void Equals_DifferentShortfall_IsNotEqual()
        {
            var first = JokerButtons.Campaign(0, Price, Price - 1, true, false);
            var second = JokerButtons.Campaign(0, Price, Price - 2, true, false);

            Assert.IsTrue(first != second);
        }

        [Test]
        public void Equals_DifferentMark_IsNotEqual()
        {
            var first = JokerButtons.Campaign(Held, Price, AmpleGold, true, false);
            var second = JokerButtons.Campaign(Held, Price, AmpleGold, true, true);

            Assert.IsTrue(first != second, "the view refreshes when the mark comes or goes");
        }
    }
}
