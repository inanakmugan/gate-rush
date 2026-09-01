using System.Collections.Generic;
using GateRush.Serialization;

namespace GateRush.Editor
{
    /// <summary>
    /// The Level Editor's undo/redo stack (docs/Modules/09a, Session C). This
    /// reverses <c>09-level-editor.md</c>'s original "no undo" decision — see
    /// <c>DECISIONS.md</c> for why.
    /// </summary>
    /// <remarks>
    /// <para><b>Snapshots, not commands.</b> <see cref="LevelDraft.ToDto"/> is
    /// already a complete snapshot and <c>ToDto -&gt; FromDto</c> is already
    /// proven lossless (<c>LevelDraftTests</c>), so undo does not need to know
    /// how to reverse any individual mutation. Each undo step is simply the
    /// whole level as it stood one mutation ago.</para>
    /// <para><b>The trailing snapshot.</b> <see cref="LevelEditorWindow.Mutated"/>
    /// runs after a mutation has already been applied, so it cannot hand this
    /// class the "before" state directly. Instead this class holds one, kept in
    /// sync on every call to <see cref="Record"/>: the value pushed is always
    /// the snapshot from <em>before</em> the mutation that just happened, and the
    /// held value is then unconditionally refreshed to the mutation's result.</para>
    /// <para><b>Coalescing.</b> A single sustained edit — typing a number into a
    /// properties-panel field — fires the mutation hook once per keystroke. A
    /// push is skipped while <paramref name="focusKey"/> (the focused control's
    /// id, read by the window via <c>GUIUtility.keyboardControl</c>) stays the
    /// same as it was at the last real push: the undo entry already on the stack
    /// still holds the state from before typing started. A focus key of
    /// <c>0</c> — nothing named is focused, the common case for a button click —
    /// never coalesces even if it repeats, since two unrelated discrete actions
    /// (e.g. two "+ Add to queue" clicks) would otherwise look identical.</para>
    /// <para><b>Why a key alone is not enough.</b> Two different objects can
    /// present the same field at the same control id — a block's Axis field and
    /// a wave block's Axis field occupy the same position in an identically
    /// shaped panel. Editing one, changing what is selected, then editing the
    /// other's same-named field must not coalesce them together. The window
    /// calls <see cref="BreakCoalescing"/> whenever the selection changes (and,
    /// transitively, whenever wave scope is entered or left, since both always
    /// clear the selection) so the next <see cref="Record"/> always pushes
    /// regardless of the key. <see cref="Undo"/> and <see cref="Redo"/> break it
    /// too, on their own — moving through history is exactly the kind of event
    /// that makes a repeated key stop meaning "still the same edit", and this
    /// class should not depend on a caller happening to clear the selection
    /// (or doing anything else) on every path that reaches them.</para>
    /// </remarks>
    public sealed class DraftHistory
    {
        private readonly int capacity;
        private readonly List<LevelDto> undoStack = new List<LevelDto>();
        private readonly List<LevelDto> redoStack = new List<LevelDto>();

        private LevelDto pending;
        private bool hasLastPushKey;
        private int lastPushKey;

        public DraftHistory(int capacity)
        {
            this.capacity = capacity;
        }

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        /// <summary>
        /// Clears both stacks and seeds the trailing snapshot with
        /// <paramref name="current"/>. Called when a level is created or loaded
        /// — undoing across a file boundary would restore a draft belonging to a
        /// different level (docs/Modules/09a, C4).
        /// </summary>
        public void Reset(LevelDto current)
        {
            undoStack.Clear();
            redoStack.Clear();
            pending = current;
            hasLastPushKey = false;
        }

        /// <summary>
        /// Forces the next <see cref="Record"/> to push regardless of
        /// <paramref name="focusKey"/> — call whenever what a repeated key could
        /// mean has changed out from under the coalescing check, such as the
        /// selection changing.
        /// </summary>
        public void BreakCoalescing()
        {
            hasLastPushKey = false;
        }

        /// <summary>
        /// Records one mutation: <paramref name="currentAfterMutation"/> is the
        /// draft's state right after the mutation that just ran.
        /// <paramref name="focusKey"/> is the id of whatever control currently
        /// holds keyboard focus, or <c>0</c> if nothing does.
        /// </summary>
        public void Record(LevelDto currentAfterMutation, int focusKey)
        {
            var coalesce = hasLastPushKey && focusKey != 0 && focusKey == lastPushKey;
            if (!coalesce)
            {
                undoStack.Add(pending);
                if (undoStack.Count > capacity)
                {
                    undoStack.RemoveAt(0);
                }

                hasLastPushKey = true;
                lastPushKey = focusKey;
            }

            redoStack.Clear();
            pending = currentAfterMutation;
        }

        /// <summary>Pops the most recent undo entry, or returns <c>null</c> if there is none.</summary>
        public LevelDto Undo()
        {
            if (undoStack.Count == 0)
            {
                return null;
            }

            var target = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            redoStack.Add(pending);
            pending = target;
            hasLastPushKey = false; // a key recorded before this move no longer means "still the same edit"
            return target;
        }

        /// <summary>Pops the most recent redo entry, or returns <c>null</c> if there is none.</summary>
        public LevelDto Redo()
        {
            if (redoStack.Count == 0)
            {
                return null;
            }

            var target = redoStack[redoStack.Count - 1];
            redoStack.RemoveAt(redoStack.Count - 1);
            undoStack.Add(pending);
            pending = target;
            hasLastPushKey = false; // same reasoning as Undo()
            return target;
        }
    }
}
