using System;
using System.Collections.Generic;
using GateRush.UI;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 40's <see cref="LevelOverlayStack"/>, the overlays over a
    /// level: the player emptying the stack raises AllClosed once, which
    /// resumes the level, and Get Gold's replacement never passes through an
    /// empty stack; Clear empties it without AllClosed; the test purchase
    /// sheet answers exactly once — yes for Buy after the sheet closed, no for
    /// anything else that closes it — and a second sheet while one waits
    /// throws; Back closes only the top overlay and does nothing with none
    /// open.
    /// </summary>
    public class LevelOverlayStackTests
    {
        private LevelOverlayStack stack;
        private int allClosed;
        private List<bool> answers;

        [SetUp]
        public void SetUp()
        {
            stack = new LevelOverlayStack();
            allClosed = 0;
            answers = new List<bool>();
            stack.AllClosed += () => allClosed++;
        }

        [Test]
        public void CloseIfOnTop_LastOverlay_RaisesAllClosedOnce()
        {
            stack.Open(UiOverlay.NotEnoughGold);

            var isClosed = stack.CloseIfOnTop(UiOverlay.NotEnoughGold);

            Assert.IsTrue(isClosed);
            Assert.IsFalse(stack.IsOpen);
            Assert.AreEqual(1, allClosed);
        }

        [Test]
        public void CloseIfOnTop_NotTheTopOverlay_ChangesNothing()
        {
            stack.Open(UiOverlay.GoldPacks);
            stack.Open(UiOverlay.Message);

            var isClosed = stack.CloseIfOnTop(UiOverlay.GoldPacks);

            Assert.IsFalse(isClosed);
            CollectionAssert.AreEqual(new[] { UiOverlay.GoldPacks, UiOverlay.Message }, stack.Overlays);
        }

        [Test]
        public void ReplaceTop_GetGold_SwapsWithoutEverEmptyingTheStack()
        {
            stack.Open(UiOverlay.NotEnoughGold);
            var countsSeen = new List<int>();
            stack.Changed += () => countsSeen.Add(stack.Overlays.Count);

            stack.ReplaceTop(UiOverlay.GoldPacks);

            CollectionAssert.AreEqual(new[] { UiOverlay.GoldPacks }, stack.Overlays);
            CollectionAssert.AreEqual(new[] { 1 }, countsSeen, "one change, never seen empty");
            Assert.AreEqual(0, allClosed, "the level stays paused on the way to the gold packs");
        }

        [Test]
        public void ClosingAnOverlayBelowTheTop_DoesNotRaiseAllClosed()
        {
            stack.Open(UiOverlay.GoldPacks);
            stack.Open(UiOverlay.Message);

            stack.CloseIfOnTop(UiOverlay.Message);

            Assert.IsTrue(stack.IsOpen);
            Assert.AreEqual(0, allClosed);
        }

        [Test]
        public void Back_NoneOpen_ChangesNothing()
        {
            var isBack = stack.Back();

            Assert.IsFalse(isBack);
            Assert.AreEqual(0, allClosed);
        }

        [Test]
        public void Back_TwoOpen_ClosesOnlyTheTop()
        {
            stack.Open(UiOverlay.GoldPacks);
            stack.Open(UiOverlay.Message);

            var isBack = stack.Back();

            Assert.IsTrue(isBack);
            CollectionAssert.AreEqual(new[] { UiOverlay.GoldPacks }, stack.Overlays);
            Assert.AreEqual(0, allClosed);
        }

        [Test]
        public void Clear_OverlaysOpen_EmptiesWithoutAllClosed()
        {
            stack.Open(UiOverlay.NotEnoughGold);

            stack.Clear();

            Assert.IsFalse(stack.IsOpen);
            Assert.AreEqual(0, allClosed, "the level is being replaced or left");
        }

        [Test]
        public void Clear_ThenTheNextLastClose_RaisesAllClosedAgain()
        {
            stack.Open(UiOverlay.NotEnoughGold);
            stack.Clear();
            stack.Open(UiOverlay.Message);

            stack.CloseIfOnTop(UiOverlay.Message);

            Assert.AreEqual(1, allClosed);
        }

        [Test]
        public void Buy_WaitingSheet_AnswersYesOnceAfterTheSheetClosed()
        {
            stack.Open(UiOverlay.GoldPacks);
            var openWhenAnswered = new List<UiOverlay>();
            stack.OpenTestPurchase(isBought =>
            {
                answers.Add(isBought);
                openWhenAnswered.AddRange(stack.Overlays);
            });

            stack.Buy();
            stack.Buy();

            CollectionAssert.AreEqual(new[] { true }, answers);
            CollectionAssert.AreEqual(new[] { UiOverlay.GoldPacks }, openWhenAnswered, "the sheet was closed before the answer");
            Assert.IsFalse(stack.IsAwaitingAnswer);
        }

        [Test]
        public void CloseOrBack_WaitingSheet_AnswersNoOnce()
        {
            stack.Open(UiOverlay.GoldPacks);
            stack.OpenTestPurchase(answers.Add);

            stack.Back();
            stack.Clear();

            CollectionAssert.AreEqual(new[] { false }, answers);
        }

        [Test]
        public void Clear_WaitingSheet_AnswersNo()
        {
            stack.Open(UiOverlay.GoldPacks);
            stack.OpenTestPurchase(answers.Add);

            stack.Clear();

            CollectionAssert.AreEqual(new[] { false }, answers);
        }

        [Test]
        public void OpenTestPurchase_WhileOneWaits_Throws()
        {
            stack.OpenTestPurchase(answers.Add);

            Assert.Throws<InvalidOperationException>(() => stack.OpenTestPurchase(answers.Add));
            CollectionAssert.IsEmpty(answers, "the waiting sheet is not answered");
        }

        [Test]
        public void OpenTestPurchase_NullAnswer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => stack.OpenTestPurchase(null));
        }
    }
}
