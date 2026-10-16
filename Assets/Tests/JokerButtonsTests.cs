using System;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 34's <see cref="JokerButtons"/>: what a joker button
    /// reads and whether it can be pressed, in the campaign (D65) and in free
    /// play (D66). The config's names and formats are set to texts no default
    /// has, so every expected text is built from them: a text written into the
    /// code instead of read from the config would fail.
    /// </summary>
    public class JokerButtonsTests
    {
        private const string CountFormat = "<{0}|{1}>";
        private const string PriceFormat = "[{0}:{1}]";
        private const string RocketName = "Rkt";
        private const int Held = 2;
        private const int Price = 200;
        private const int AmpleGold = 1000;

        private RuntimeConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<RuntimeConfig>();
            var serialized = new SerializedObject(config);
            serialized.FindProperty("jokerCountFormat").stringValue = CountFormat;
            serialized.FindProperty("jokerPriceFormat").stringValue = PriceFormat;
            serialized.FindProperty("clockLabel").stringValue = "Clk";
            serialized.FindProperty("rocketLabel").stringValue = RocketName;
            serialized.FindProperty("broomLabel").stringValue = "Brm";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Campaign_SomeHeld_ShowsTheNameAndTheCountAndIsPressable()
        {
            var button = JokerButtons.Campaign(JokerKind.Rocket, Held, Price, 0, true, config);

            Assert.AreEqual($"<{RocketName}|{Held}>", button.Label);
            Assert.IsTrue(button.IsInteractable, "a held joker needs no gold");
        }

        [TestCase(Price)]
        [TestCase(Price + 1)]
        public void Campaign_NoneHeldAndTheBalanceCoversThePrice_ShowsThePriceAndIsPressable(int balance)
        {
            var button = JokerButtons.Campaign(JokerKind.Rocket, 0, Price, balance, true, config);

            Assert.AreEqual($"[{RocketName}:{Price}]", button.Label);
            Assert.IsTrue(button.IsInteractable);
        }

        [Test]
        public void Campaign_NoneHeldAndTheBalanceShort_ShowsThePriceAndIsNotPressable()
        {
            var button = JokerButtons.Campaign(JokerKind.Rocket, 0, Price, Price - 1, true, config);

            Assert.AreEqual($"[{RocketName}:{Price}]", button.Label);
            Assert.IsFalse(button.IsInteractable);
        }

        [TestCase(Held)]
        [TestCase(0)]
        public void Campaign_NotUsable_IsNotPressableWhateverIsHeld(int held)
        {
            var button = JokerButtons.Campaign(JokerKind.Rocket, held, Price, AmpleGold, false, config);

            Assert.IsFalse(button.IsInteractable);
        }

        [Test]
        public void FreePlay_SomeLeft_ShowsTheNameAndWhatIsLeftAndIsPressable()
        {
            var button = JokerButtons.FreePlay(JokerKind.Rocket, Held, true, config);

            Assert.AreEqual($"<{RocketName}|{Held}>", button.Label);
            Assert.IsTrue(button.IsInteractable);
        }

        [Test]
        public void FreePlay_NoneLeft_ShowsZeroAndIsNotPressable()
        {
            var button = JokerButtons.FreePlay(JokerKind.Rocket, 0, true, config);

            Assert.AreEqual($"<{RocketName}|0>", button.Label);
            Assert.IsFalse(button.IsInteractable, "free play is never topped up");
        }

        [Test]
        public void FreePlay_NotUsable_IsNotPressable()
        {
            var button = JokerButtons.FreePlay(JokerKind.Rocket, Held, false, config);

            Assert.IsFalse(button.IsInteractable);
        }

        [Test]
        public void Campaign_EachKind_ShowsItsOwnNameFromTheConfig([Values] JokerKind kind)
        {
            var button = JokerButtons.Campaign(kind, Held, Price, 0, true, config);

            Assert.AreEqual($"<{config.JokerName(kind)}|{Held}>", button.Label);
            Assert.That(new[] { "Clk", RocketName, "Brm" }, Has.Member(config.JokerName(kind)));
        }

        [Test]
        public void Campaign_UndefinedKind_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => JokerButtons.Campaign((JokerKind)99, Held, Price, 0, true, config));
        }
    }
}
