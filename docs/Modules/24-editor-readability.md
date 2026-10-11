# Module 24 — Level Editor readability

**Assembly:** `GateRush.Editor`, `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 09, 16, 20
**Phase:** Known debt (tooling, before Phase 4)

---

## Responsibility

Make the Level Editor's canvas show what the game shows, so a designer
reads a level without selecting each block. Today four block features are
visible only in the properties panel, and a shutter is hidden under the
blocks it covers. After this module the canvas marks every one of them,
placed by the same rule the board uses.

In scope:

- Block marks on the canvas: time bonus (M10), padlock with its key count
  and key in its lock's colour (M8, D47), and the axis arrow (M7).
- The shutter (M5) drawn over the blocks it covers, with its threshold.
- The same marks on the main grid, in an elevator wave's scope and on a
  generator queue entry's preview and free-draw grid.

Not in scope: the generator's next-block preview on the board (a game
feature, specified separately once the owner shares screenshots); the
editor's time-budget formula; chains on locked blocks; any rule change;
the rest of the editor's known debt.

---

## Public surface

```
MarkLayout                          plain C#, Runtime (existing)
    static BlockMarkSlots Slots(IReadOnlyList<Coord> cells, bool isFrozen,
        bool hasIcon, bool hasLayers, bool hasBonus, float frozenRaiseCells)
        // in cell units: where the icon (padlock or key), the layer number
        // and the time bonus go, and whether the row is a crowded pair, a
        // crowded triple or roomy. BoardView.MarksOf becomes a call to this
        // times the cell size; behaviour unchanged.

BlockMarkSlots                      readonly struct, Runtime
    Vector2 Icon, Layers, Bonus; MarkCrowding Crowding

EditorBlockLook                     Editor (existing, extended)
    static EditorBlockLook Of(... colorStack, unfreezeAtClearCount, axis,
        timeBonusSeconds, lockId, requiredKeyCount, keyMarkColor)
    + MarkIcon Icon            // None, Padlock (with count), Key (with colour or none)
    + int? TimeBonusSeconds
    + MovementAxis Axis
    + BlockMarkSlots Slots

LevelDraft (or a small helper)      Editor
    BlockColor? LockColor(int lockId)
        // the outer colour of the locked block with that lock id, searched
        // over board blocks, wave blocks and queue entries; null when none
```

Names are negotiable. That the editor places marks with the same
`MarkLayout` rule as the board, and does not keep its own copy of it, is
not.

---

## Design decisions (owner)

### The marks

- **Time bonus.** The clock and the bonus text (`TimeBonusMarkText`, so
  "+5" reads as in the game) on a plate in the bonus colours.
- **Locked block.** The padlock with the block's `RequiredKeyCount` on a
  small badge below it. No chains: on the editor's small cells they hide
  the colour, and the padlock alone says "locked". Shown only when
  `LockId` is set, so a stale `RequiredKeyCount` on a lock-free block
  (known debt) never shows.
- **Key.** The key with its gem in the lock's colour: the outer colour of
  the locked block whose `LockId` the key targets, at level start (D47).
  When no block has that lock id the key is drawn with no gem colour (the
  validator already reports the broken link).
- **Axis.** A double-headed arrow through the footprint's centre along
  the block's axis, inset from both ends, as on the board. A free block
  has none.
- **Frozen.** A frozen block stays ice with its count at the anchor, as
  today; its padlock, key and bonus marks are raised above the count, as
  on the board. It shows no layer number (M3 hides colours).
- **Layer number.** The existing depth number moves from the anchor into
  the layer slot when the block has other marks; alone it stays at the
  anchor, so a plain layered block looks as it does today.

### Placement: one rule

`BoardView.MarksOf` decides where a block's marks go and how crowded they
are. That decision moves into `MarkLayout` (cell units, pure) and both the
board and the editor call it. The board's output must not change; the
existing board mark tests and a new equality test guard that. This also
stops the editor becoming a third copy of a drawing rule, next to
`MechanicIllustration` (known debt).

### Icons come from the game

The padlock, key body, key gem and clock are the game's own sprites,
read from `RuntimeConfig` through a reference on `LevelEditorSettings`,
and drawn at the sizes and colours the config gives them (scaled to the
editor's cells). The designer sees the same icon on both screens. When
the reference is missing, `Problems()` reports it and the new marks are
not drawn (the existing block marks keep working), the same pattern as
`areBlockMarksUsable` today.

### The shutter over the blocks

- The shutter is drawn **after** the blocks and their marks: a
  translucent fill in the shutter colour over the whole region and a bold
  outline, so the region reads at a glance and every block under it, with
  its marks, stays visible through it.
- Its threshold sits on a plate at the region's top-left cell, tinted
  with its required colour when it has one.
- Empty cells under a shutter are no longer tinted separately; the
  overlay covers them.
- Drawing only. Hit testing and selection do not change: a click on a
  block under a shutter still selects the block, as today.

Draw order on the canvas: cells, block outlines, block looks and marks,
shutter overlays, elevator borders, selection outline, free-draw cells,
edge markers, drag preview.

### Engine rules that carry over

- No magic numbers. Every new size, alpha and colour is a
  `LevelEditorSettings` field with a `Problems()` check (overlay alpha in
  (0, 1), outline thickness above 0, mark sizes above 0). The colours and
  thickness inside the methods this module edits (`FillOf`'s colours,
  `ShutterTint`, `ElevatorTint`, `DrawRegionBorders`' thickness) move into
  settings too; the rest of the window's magic numbers stay as debt.
- Marks draw only on `Repaint`, like the block number.
- No new sprites.

---

## Left to you

- How sprites are drawn in IMGUI (`GUI.DrawTextureWithTexCoords` from
  `sprite.textureRect`, rotation for the key with
  `GUIUtility.RotateAroundPivot`, or another way).
- How the axis arrow is drawn (`Handles` polygons or the arrow sprite).
- How `LockColor` is found and cached per GUI pass.
- Starting values for every new field. The owner tunes them.
- The exact Editor steps for the owner.

---

## Tests

**`MarkLayout.Slots`**
- For a 1×1 with padlock and bonus, a 1×3 with three marks, an L with two
  marks and a frozen 2×1 with a key, the slots equal what
  `BoardView.MarksOf` placed before the move (in cell units).
- A single mark sits at the anchor, uncrowded; a frozen block's marks are
  raised by `frozenRaiseCells`.

**`EditorBlockLook`**
- A block with `LockId` shows a padlock with its `RequiredKeyCount`; the
  same block with `LockId` cleared shows none, whatever
  `RequiredKeyCount` holds.
- A key shows the colour passed for its lock, or no colour when none is.
- A zero time bonus shows no bonus mark; a free block shows no arrow.
- A frozen block keeps its padlock, key and bonus marks and shows no
  layer number.

**Lock colour**
- A key on the board whose lock is on a wave block, and one whose lock is
  on a queue entry, both find that block's outer colour.
- An unknown lock id gives null.

**Settings**
- A missing `RuntimeConfig` reference and an overlay alpha of 0 or 1 are
  each reported by `Problems()`.

**By hand (owner)**
- Levels 9 (time bonus), 10 (lock and key), 14 (mix) and any level with
  axis blocks, in the editor: every time bonus, lock, key and axis block
  is readable without selecting it, and matches the game.
- Level 11: the shutter region and the blocks under it are both visible;
  the threshold plate shows.
- A generator queue entry and an elevator wave show their blocks' marks.
- Clicking a block under a shutter still selects the block.
