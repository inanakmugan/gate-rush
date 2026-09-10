using GateRush.Editor;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="DraftHistory"/> (docs/Modules/09a, Session C): the
    /// trailing-snapshot push/refresh, undo/redo, the capacity cap, and
    /// coalescing by focus key — including the two refinements beyond the
    /// module doc's literal wording (a focus key of <c>0</c> never coalesces,
    /// and <see cref="DraftHistory.BreakCoalescing"/> always forces a push).
    /// </summary>
    public class DraftHistoryTests
    {
        private static LevelDto Level(int id) => new LevelDto { levelId = id };

        [Test]
        public void Undo_AfterOnePush_RestoresTheEarlierSnapshotExactly()
        {
            var history = new DraftHistory(50);
            history.Reset(Level(0));

            history.Record(Level(1), focusKey: 0);
            var restored = history.Undo();

            Assert.AreEqual(0, restored.levelId);
        }

        [Test]
        public void Undo_PastTheBottomOfTheStack_IsANoOp()
        {
            var history = new DraftHistory(50);
            history.Reset(Level(0));

            Assert.IsNull(history.Undo());
            Assert.IsNull(history.Undo());
        }

        [Test]
        public void Redo_AfterUndo_Restores_AndANewMutationClearsRedo()
        {
            var history = new DraftHistory(50);
            history.Reset(Level(0));
            history.Record(Level(1), focusKey: 0);

            history.Undo();
            var redone = history.Redo();

            Assert.AreEqual(1, redone.levelId);

            history.Undo();
            Assert.IsTrue(history.CanRedo);

            history.Record(Level(2), focusKey: 0);

            Assert.IsFalse(history.CanRedo);
        }

        [Test]
        public void Record_PastCapacity_DropsTheOldestEntry()
        {
            var history = new DraftHistory(2);
            history.Reset(Level(0));

            history.Record(Level(1), focusKey: 0);
            history.Record(Level(2), focusKey: 0);
            history.Record(Level(3), focusKey: 0);

            // Capacity 2: only the two most recent "before" snapshots survive.
            Assert.AreEqual(2, history.Undo().levelId);
            Assert.AreEqual(1, history.Undo().levelId);
            Assert.IsNull(history.Undo());
        }

        [Test]
        public void Reset_EmptiesBothStacks()
        {
            var history = new DraftHistory(50);
            history.Reset(Level(0));
            history.Record(Level(1), focusKey: 0);
            history.Undo();

            history.Reset(Level(9));

            Assert.IsFalse(history.CanUndo);
            Assert.IsFalse(history.CanRedo);
        }

        [Test]
        public void Record_ConsecutiveEditsToTheSameFocusKey_ProduceOneEntryHoldingTheStateBeforeTheFirstKeystroke()
        {
            var history = new DraftHistory(50);
            history.Reset(Level(0));

            history.Record(Level(1), focusKey: 42);
            history.Record(Level(12), focusKey: 42);
            history.Record(Level(123), focusKey: 42);

            Assert.AreEqual(0, history.Undo().levelId);
            Assert.IsNull(history.Undo());
        }

        [Test]
        public void Record_MovingFocusBetweenTwoKeys_ProducesTwoEntries()
        {
            var history = new DraftHistory(50);
            history.Reset(Level(0));

            history.Record(Level(1), focusKey: 42);
            history.Record(Level(2), focusKey: 43);

            Assert.AreEqual(1, history.Undo().levelId);
            Assert.AreEqual(0, history.Undo().levelId);
            Assert.IsNull(history.Undo());
        }

        [Test]
        public void Record_WithFocusKeyZeroTwice_NeverCoalesces()
        {
            var history = new DraftHistory(50);
            history.Reset(Level(0));

            history.Record(Level(1), focusKey: 0);
            history.Record(Level(2), focusKey: 0);

            Assert.AreEqual(1, history.Undo().levelId);
            Assert.AreEqual(0, history.Undo().levelId);
            Assert.IsNull(history.Undo());
        }

        [Test]
        public void Record_AfterUndo_WithTheSameFocusKey_DoesNotCoalesceWithThePriorEdit()
        {
            // Type into a field, undo, click back into that same field and type
            // again: the key matches what was recorded before the undo, but the
            // undo must have already broken the chain on its own — otherwise the
            // just-restored state never enters the stack and a second undo jumps
            // one step too far.
            var history = new DraftHistory(50);
            history.Reset(Level(0));

            history.Record(Level(1), focusKey: 42);
            history.Undo();
            history.Record(Level(2), focusKey: 42);

            Assert.AreEqual(0, history.Undo().levelId);
            Assert.IsNull(history.Undo());
        }

        [Test]
        public void BreakCoalescing_ForcesTheNextRecordToPush_EvenWithTheSameFocusKey()
        {
            var history = new DraftHistory(50);
            history.Reset(Level(0));

            history.Record(Level(1), focusKey: 42);
            history.BreakCoalescing();
            history.Record(Level(2), focusKey: 42);

            Assert.AreEqual(1, history.Undo().levelId);
            Assert.AreEqual(0, history.Undo().levelId);
            Assert.IsNull(history.Undo());
        }
    }
}
