using GateRush.Core;
using GateRush.Editor;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 20's <see cref="EditorBlockLook"/>: a block that starts
    /// frozen shows as ice with its count and nothing of its colours; a
    /// layered block shows its second colour, and its depth only when deeper
    /// than two. "Frozen at level start" is Core's
    /// <see cref="UnlockConditions.IsThresholdMet"/> at zero clears. Module
    /// 24: a padlock shows only with a lock id, a key carries its lock's
    /// colour or none, a zero bonus and a free axis show nothing, and a
    /// frozen block keeps its padlock, key and bonus, raised, without a
    /// layer number.
    /// </summary>
    public class EditorBlockLookTests
    {
        private const float FrozenRaise = 0.3f;
        private const float Tolerance = 1e-5f;

        private static readonly Coord[] OneCell = { new Coord(0, 0) };
        private static readonly BlockColor[] OneColour = { BlockColor.Red };
        private static readonly BlockColor[] TwoColours = { BlockColor.Red, BlockColor.Blue };
        private static readonly BlockColor[] ThreeColours = { BlockColor.Red, BlockColor.Blue, BlockColor.Green };

        /// <summary>The look of a 1×1 block with the given fields; everything not named is the plain default.</summary>
        private static EditorBlockLook Look(
            BlockColor[] colorStack, int? unfreezeAtClearCount = null, MovementAxis axis = MovementAxis.Free,
            int timeBonusSeconds = 0, int? lockId = null, int requiredKeyCount = 0, bool hasKey = false,
            BlockColor? keyMarkColor = null) =>
            EditorBlockLook.Of(
                OneCell, colorStack, unfreezeAtClearCount, axis, timeBonusSeconds, lockId, requiredKeyCount, hasKey,
                keyMarkColor, FrozenRaise);

        [Test]
        public void Of_Frozen_IsIceWithItsCountAndNoInset()
        {
            const int UnfreezeAt = 4;

            var look = Look(ThreeColours, UnfreezeAt);

            Assert.IsTrue(look.IsFrozen);
            Assert.AreEqual(UnfreezeAt, look.Number, "the clears that unfreeze it, not its depth");
            Assert.IsNull(look.BeneathColor, "M3 hides a frozen block's colours");
        }

        [Test]
        public void Of_ThresholdZero_IsNotFrozen()
        {
            // The editor asks Core, so it agrees with the initial state and
            // the resolver on both sides of the line: a threshold already met
            // at zero clears never freezes the block, the first one above it
            // does.
            const int MetAtZeroClears = 0;
            const int FirstUnmet = 1;
            Assert.IsTrue(UnlockConditions.IsThresholdMet(0, new int[0], MetAtZeroClears, null), "Core: met at zero clears");
            Assert.IsFalse(UnlockConditions.IsThresholdMet(0, new int[0], FirstUnmet, null), "Core: unmet at zero clears");

            var met = Look(TwoColours, MetAtZeroClears);
            var unmet = Look(TwoColours, FirstUnmet);
            var none = Look(TwoColours, null);

            Assert.IsFalse(met.IsFrozen);
            Assert.AreEqual(BlockColor.Blue, met.BeneathColor, "it shows as the layered block it is");
            Assert.IsNull(met.Number);
            Assert.IsTrue(unmet.IsFrozen);
            Assert.IsFalse(none.IsFrozen);
            Assert.AreEqual(met.IsFrozen, EditorBlockLook.IsFrozenAtStart(MetAtZeroClears), "the fill asks the same question");
            Assert.AreEqual(unmet.IsFrozen, EditorBlockLook.IsFrozenAtStart(FirstUnmet));
            Assert.AreEqual(none.IsFrozen, EditorBlockLook.IsFrozenAtStart(null));
        }

        [Test]
        public void Of_TwoColours_ShowsTheSecondAndNoDepth()
        {
            var two = Look(TwoColours);
            var one = Look(OneColour);

            Assert.IsFalse(two.IsFrozen);
            Assert.AreEqual(BlockColor.Blue, two.BeneathColor);
            Assert.IsNull(two.Number, "the two colours shown already tell the depth");
            Assert.IsNull(one.BeneathColor, "a single colour has nothing beneath");
            Assert.IsNull(one.Number);
        }

        [Test]
        public void Of_ThreeColours_ShowsDepthThree()
        {
            var look = Look(ThreeColours);

            Assert.AreEqual(BlockColor.Blue, look.BeneathColor);
            Assert.AreEqual(ThreeColours.Length, look.Number);
        }

        [Test]
        public void Of_LockId_ShowsAPadlockWithItsRequiredKeyCount()
        {
            const int Keys = 2;

            var look = Look(OneColour, lockId: 5, requiredKeyCount: Keys);

            Assert.AreEqual(EditorMarkIconKind.Padlock, look.Icon.Kind);
            Assert.AreEqual(Keys, look.Icon.RequiredKeyCount);
        }

        [Test]
        public void Of_NoLockId_ShowsNoPadlockWhateverRequiredKeyCountHolds()
        {
            // A draft loaded from a hand-edited file can still hold a key count
            // on a lock-free block; the editor no longer leaves one behind and
            // never saves one (Module 27).
            var look = Look(OneColour, lockId: null, requiredKeyCount: 3);

            Assert.AreEqual(EditorMarkIconKind.None, look.Icon.Kind);
            Assert.IsNull(look.Icon.RequiredKeyCount);
        }

        [Test]
        public void Of_Key_ShowsTheColourPassedForItsLock_OrNoneWhenNoneIs()
        {
            var found = Look(OneColour, hasKey: true, keyMarkColor: BlockColor.Green);
            var missing = Look(OneColour, hasKey: true, keyMarkColor: null);
            var noKey = Look(OneColour, hasKey: false, keyMarkColor: BlockColor.Green);

            Assert.AreEqual(EditorMarkIconKind.Key, found.Icon.Kind);
            Assert.AreEqual(BlockColor.Green, found.Icon.KeyColor);
            Assert.AreEqual(EditorMarkIconKind.Key, missing.Icon.Kind, "a key whose lock is not found is still a key");
            Assert.IsNull(missing.Icon.KeyColor);
            Assert.AreEqual(EditorMarkIconKind.None, noKey.Icon.Kind);
        }

        [Test]
        public void Of_LockAndKeyTogether_ShowsThePadlockOnly()
        {
            // Core rejects the combination, so only a broken draft holds it.
            var look = Look(OneColour, lockId: 1, requiredKeyCount: 1, hasKey: true, keyMarkColor: BlockColor.Green);

            Assert.AreEqual(EditorMarkIconKind.Padlock, look.Icon.Kind);
            Assert.IsNull(look.Icon.KeyColor);
        }

        [Test]
        public void Of_ZeroTimeBonus_ShowsNoBonusMark_AndAPositiveOneItsSeconds()
        {
            const int Seconds = 5;

            var none = Look(OneColour, timeBonusSeconds: 0);
            var some = Look(OneColour, timeBonusSeconds: Seconds);

            Assert.IsNull(none.TimeBonusSeconds);
            Assert.AreEqual(Seconds, some.TimeBonusSeconds);
        }

        [Test]
        public void Of_FreeBlock_ShowsNoArrow_AndARestrictedOneItsAxis()
        {
            var free = Look(OneColour);
            var across = Look(OneColour, axis: MovementAxis.HorizontalOnly);

            Assert.AreEqual(MovementAxis.Free, free.Axis, "the window draws an arrow for anything but Free");
            Assert.AreEqual(MovementAxis.HorizontalOnly, across.Axis);
        }

        [Test]
        public void Of_Frozen_KeepsItsPadlockAndBonusRaised_AndShowsNoLayerNumber()
        {
            const int UnfreezeAt = 4;
            const int Keys = 2;
            const int Seconds = 5;
            var anchor = MarkLayout.Anchor(OneCell);

            var look = Look(ThreeColours, UnfreezeAt, timeBonusSeconds: Seconds, lockId: 1, requiredKeyCount: Keys);

            Assert.IsTrue(look.IsFrozen);
            Assert.AreEqual(EditorMarkIconKind.Padlock, look.Icon.Kind);
            Assert.AreEqual(Keys, look.Icon.RequiredKeyCount);
            Assert.AreEqual(Seconds, look.TimeBonusSeconds);
            Assert.AreEqual(UnfreezeAt, look.Number, "its one number is the frozen count, not the stack's depth");
            Assert.AreEqual(MarkCrowding.CrowdedPair, look.Slots.Crowding, "two marks share the cell: no slot goes to a layer number");
            Assert.AreEqual(anchor.y + FrozenRaise, look.Slots.Icon.y, Tolerance, "raised above the count");
            Assert.AreEqual(anchor.y + FrozenRaise, look.Slots.Bonus.y, Tolerance, "raised above the count");
        }

        [Test]
        public void Of_Frozen_KeepsItsKeyRaised()
        {
            var anchor = MarkLayout.Anchor(OneCell);

            var look = Look(OneColour, unfreezeAtClearCount: 2, hasKey: true, keyMarkColor: BlockColor.Blue);

            Assert.IsTrue(look.IsFrozen);
            Assert.AreEqual(EditorMarkIconKind.Key, look.Icon.Kind);
            Assert.AreEqual(BlockColor.Blue, look.Icon.KeyColor);
            Assert.AreEqual(anchor.x, look.Slots.Icon.x, Tolerance);
            Assert.AreEqual(anchor.y + FrozenRaise, look.Slots.Icon.y, Tolerance);
        }

        [Test]
        public void Of_LayerNumberAlone_StaysAtTheAnchor_AndMovesAsideForAnotherMark()
        {
            var anchor = MarkLayout.Anchor(OneCell);

            var alone = Look(ThreeColours);
            var locked = Look(ThreeColours, lockId: 1, requiredKeyCount: 1);

            Assert.AreEqual(anchor.x, alone.Slots.Layers.x, Tolerance, "a plain layered block looks as it did");
            Assert.AreEqual(anchor.y, alone.Slots.Layers.y, Tolerance);
            Assert.AreEqual(MarkCrowding.Roomy, alone.Slots.Crowding);
            Assert.Less(locked.Slots.Icon.x, locked.Slots.Layers.x, "the padlock takes the first slot, the depth the next");
        }
    }
}
