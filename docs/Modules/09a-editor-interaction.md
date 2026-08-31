# Level Editor — working revisions

**Working document.** Revisions to the Level Editor are worked out here, one
round at a time, while the tool is still taking shape. Completed sessions are
removed and new ones written in their place — this file is a queue of work, never
a history.

When the editor is finished its content folds into `09-level-editor.md`, which
stays the single description of how the editor works; reversed decisions move to
`DECISIONS.md`; and this file is deleted.

**Revises:** `09-level-editor.md`
**Assembly:** `GateRush.Editor`
**Phase:** 1.10

---

## Current round — after the first hands-on use

The original spec described the editor's *tools* but not its *interaction*. It
said what each tool places and left out what happens when you click something
that is already there, how you move a block once placed, and how a region gets
its size. Using the window for twenty minutes made all three obvious, along with
four smaller gaps.

It also got undo wrong, and that reversal is the substantive part of this round.

Three sessions, in order. Each is a hand-off: code, tests where there is logic to
test, compile and run before the next begins.

---

# Session A — placement, selection, and four small fixes

## A1. Clicking something that exists selects it

**Today:** every tool places on every click, so a second click on a block puts
another block on top of it. The level is invalid the moment you misclick, and the
only way to select a block is to not misclick.

**Rule:** clicking a cell that is already occupied **selects what is there**,
whatever tool is active. Placement happens only on empty cells.

That is the standard behaviour of every tile editor, and it collapses two
problems into one change: you cannot stack blocks, and you no longer need a
separate gesture to select one.

**Occupied means** a living block's footprint covers the cell. Precedence when
several things claim it:

1. A block — it is what you manipulate most.
2. A shutter or elevator region — regions legitimately cover blocks, so a click
   lands on the block first; clicking a region cell with no block selects the
   region.
3. A wall.

Gates and generators sit on edges and are picked by their own markers, outside
the grid, so they do not enter this ordering.

**Wall tool:** a cell under a block cannot become a wall — `Core` rejects a block
overlapping a static wall. So the Wall tool on an occupied cell selects the block
rather than doing nothing silently.

**Region tools:** Shutter and Elevator still create at the clicked cell, unless
that cell is already inside a region of the same kind, in which case they select
it. Regions may overlap blocks; that is legal and stays legal.

## A2. Regions get numeric bounds

The properties panel for a shutter and an elevator gains **Min X, Min Y, Max X,
Max Y**, clamped to the grid, with `Min ≤ Max` enforced.

Changing an elevator's region invalidates its waves' tilings. That is a warning,
not a block — `DraftTiling` already reports it and the wave list already shows
`n/m cells`.

Drag-to-draw arrives in Session B; these fields stay afterwards for precision.

## A3. The shape palette moves under the tool row

It sits at the far right today, away from the tool that governs it. Move it to a
third row directly under the toolbar, visible only while the Block tool is
active.

## A4. Generators get an edge marker

Gates already draw one. A generator needs its own, distinguishable at a glance:
gates are rectangles in their block colour, generators are triangles pointing
inward, in a neutral colour — a generator's queue can be any mix of colours, so
none of them is *its* colour.

**Editor markers are functional, not final art.** The game's visuals are a
separate concern in phase 2 and share nothing with these.

While you are here, confirm shutter and elevator regions are visibly
distinguishable from each other and from walls on the grid. If they are not, make
them so.

## A5. Cell size gets a ceiling

Maximising the window grows the cells without growing the text in them, and a
200-pixel cell holding 11-point text looks wrong.

**Cap the cell size** at something comfortable — 64 pixels is a reasonable
starting point — and centre the grid in its area once capped. A large window then
shows a normally proportioned board with space around it.

Scaling the font with the cell was the alternative and is worse: the text becomes
absurd long before the cell does, and Unity's editor styles do not handle dynamic
sizing well. The real point is that a cell past a certain size buys nothing —
clicking a 64-pixel target is already easy.

A zoom control can come later if it turns out to be wanted. The default should be
"stop at a sensible size", not "fill whatever space exists".

## Session A tests

Most of this is layout, which is not unit tested. What is:

- **Occupancy and pick precedence.** Given a draft, the cell-to-thing resolution
  returns the block over the region, the region over the wall, and nothing for an
  empty cell. This is a pure function over a draft and belongs beside the other
  Step 3 logic, not in the window.
- **Region bounds clamping.** Values outside the grid clamp; `Min > Max` is
  corrected rather than stored.
- **Cell size cap.** `EditorGridLayout` never returns a cell larger than the cap,
  and the grid centres in its area when the cap binds.

---

# Session B — dragging

## B1. Blocks move by dragging

This is the editor's central gesture. Designing a level is mostly rearranging
blocks, and doing that through delete-and-replace is what made the window feel
heavy.

- **Mouse down** on a block: capture the mouse (`GUIUtility.hotControl`), record
  which block and the offset within it that was grabbed, so it does not jump.
- **Drag:** compute the candidate origin from the pointer and the grab offset,
  and repaint. Draw the block at the candidate position, tinted to read as a
  preview, and tinted differently when the position is not legal.
- **Mouse up:** validate — every cell inside the grid, clear of walls, clear of
  other living blocks — then apply, or revert to where it started.
- **Escape** during a drag cancels it.

Capturing the mouse is the part that is easy to leave out and hard to diagnose:
without it, moving the pointer outside the window loses the drag events and the
block sticks to the cursor.

## B2. Regions are drawn by dragging

With the Shutter or Elevator tool, press on an empty cell and drag: the
rectangle follows the pointer and is created on release. A region may be moved by
dragging its interior.

**Not in scope:** resize handles on region edges. The numeric fields from A2
cover that, and edge handles are a disproportionate amount of hit-testing for the
benefit.

## Session B tests

- Drag validation is a pure function — candidate origin plus draft yields legal
  or not — and is tested directly: inside the grid, clear of walls, clear of other
  blocks, and the block's own cells excluded from that last check.
- The grab offset is preserved: a block grabbed by its top-right cell and dropped
  two cells right moves exactly two cells.
- A rejected drop leaves the draft byte-for-byte as it was.

---

# Session C — undo

## C1. The decision this reverses

The original spec said no undo, on the reasoning that level editing is free-form
and reversible by hand — delete the block, place it again.

That reasoning does not survive contact with the tool. The hand reaches for
Ctrl+Z before the conscious thought arrives, and an editor that does not answer
feels broken regardless of whether the edit could have been undone manually.

It was also more expensive than it needed to be in my estimate, because I was
imagining a command stack. It is not one.

## C2. Snapshots, not commands

`LevelDraft.ToDto()` already produces a complete snapshot, and `ToDto → FromDto`
is already proven lossless by a test over the shared corpus. So:

- The undo stack is a `List<LevelDto>`, capped at 50 entries.
- Every mutation pushes `ToDto()` **before** applying itself.
- Ctrl+Z pops, converts back through `FromDto`, and replaces the draft.
- Redo is a second stack, filled by undo and cleared by any new mutation.
  Ctrl+Y and Ctrl+Shift+Z.

Memory is not a consideration: a level's DTO is a few kilobytes.

## C3. A drag is one undo step

The snapshot is pushed on mouse **down**, not per frame. Otherwise a single drag
across the board leaves fifty entries and Ctrl+Z moves the block one cell at a
time.

The same applies to any other gesture that mutates continuously.

## C4. What undo does not cover

- **Loading a level clears both stacks.** Undoing across a file boundary would
  restore a draft belonging to a different level.
- **Grid resize keeps its confirmation.** Undo makes it recoverable, but
  restoring five removed objects is a large undo to discover after the fact, and
  saying what will be lost beforehand is clearer than offering to reverse it
  afterwards.
- **Selection is not restored.** If what was selected no longer exists after an
  undo, the selection clears.

## Session C tests

- Push, mutate, undo: the draft equals its earlier `ToDto` output exactly.
- Undo past the bottom of the stack is a no-op, not an exception.
- Redo after undo restores; a new mutation clears the redo stack.
- The stack caps at 50 and drops the oldest entry.
- Loading a level empties both stacks.
- Selection clears when the selected object does not survive an undo.

---

## When this round is done

Delete Sessions A, B and C from this file. What replaces them is whatever the
next round of hands-on use turns up.

Their content does **not** get appended to `09-level-editor.md` one round at a
time — that would leave the module spec reading as a changelog. It folds in once,
when the editor is finished, as a description of how the editor works.

Session C is the exception and goes in sooner: it reverses a stated decision, and
that belongs in `DECISIONS.md` while the reasoning is fresh. Record the original
argument, what using the tool showed, and why the implementation turned out
cheap. A decision reversed with its reasoning intact is worth more than one that
was right the first time.
