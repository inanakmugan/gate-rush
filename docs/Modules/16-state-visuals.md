# Module 16 — State visuals

**Assembly:** `GateRush.Runtime` and `GateRush.Editor`, with one rule in
`GateRush.Core` (tests in `GateRush.Tests`)
**Depends on:** Modules 07, 10, 11, 15
**Phase:** 2.4b

---

## Responsibility

Give every level mechanic's state its own art, in the style Module 15 set
(D46) and following the reference game (D48), replacing the 2.1
placeholders: frozen blocks and closed gates as
ice, locks as chains and a padlock, keys marked with their lock's colour
(D47), shutters, generators as machines showing their next block, and
elevators as lift doors. Also lands D47's one rule in `Core`: one locked
block per colour in a level.

In scope:

- New generated sprites in `ArtRecipe` / `ArtPainter` for everything below,
  and the shared number badge.
- Drawing every state in `BoardView`, from what `VisibilityLayer` decides.
- `VisibilityLayer` changes: lock and key colours from D47, the generator's
  next block, no elevator wave count.
- The layout making room for generator machines outside the frame.
- D47 in `Core`: two locked blocks sharing an outer colour are a level data
  error.

Not in scope, deliberately: every animation (ice breaking, chains falling,
the key flying to its lock, doors opening, shutters lifting, snow), the
font, the HUD, and the grabbed-block highlight. Those are 2.4c. Layered
blocks (M4) keep their current look.

---

## Public surface

```
Core
    LevelContext                    rejects two locked blocks sharing an outer
                                    colour at level start (D47), naming both

Runtime
    VisibilityLayer.BlockVisual
        BlockColor? LockColor       // replaces LockId: the lock's colour (D47)
        BlockColor? KeyMarkColor    // replaces KeyTargetLockId: the colour its
                                    // key is marked with
    VisibilityLayer
        GeneratorVisual Generator(BoardState state, int generatorIndex)
            // Queued (count), NextCells and NextColor of the next queued
            // block; replaces GeneratorQueued
        bool ElevatorPresent(BoardState state, int elevatorIndex)
            // replaces ElevatorWavesToCome: the elevator is drawn until its
            // final wave is cleared; its wave count is never shown

    RuntimeConfig                   lockBadgePalette, unknownBadgeColor and
                                    TryGetBadgeColor go; new sprites, colours
                                    and sizes for this module

Editor
    ArtRecipe / ArtPainter          the new sprites
```

Names are negotiable.

---

## Design decisions (owner)

### The number badge

Every count the player reads — frozen blocks and gates, locks, shutters,
generators — sits on one shared badge: a small rounded rectangle in a warm
brown-orange with a lighter rim, and the number in cream on top. The badge
grows with the number of digits. The font stays TMP's default until 2.4c.

### Frozen blocks and closed gates (M2, M3)

- **Frozen block:** the block's shape from the quarter pieces in an ice
  colour, with its lip in a darker ice, no studs, a frost overlay per cell
  (a few pale diagonal streaks, untinted), and the badge with the clears
  still needed at the centre of the footprint. The block's colour stays
  hidden (M3).
- **Closed gate:** the gate's own segment of the frame, drawn like an open
  gate but in ice with the frost overlay, no arrow, and the badge at its
  middle. When it opens it becomes the ordinary coloured, arrowed gate.

### Locks and keys (M8, D47)

- **Locked block:** drawn normally in its colour, with chains across it
  (a chain sprite tiled along its length, one strip per row run of the
  footprint; see below), and a padlock at the centre of the footprint
  with the number of keys still required on it. The padlock is gold;
  it carries no colour of its own.
- **Key-carrying block:** drawn normally in its colour, with a key icon at
  the centre of its footprint: a gold key whose gem is tinted the key's
  mark colour — the outer colour, at level start, of the locked block it
  opens (D47).
- `LevelBootstrap`'s check for lock ids outside the badge palette goes with
  the palette.

### Shutters (M5)

A closed shutter is a panel over its whole region: horizontal slats in a
deep purple, a gold-orange border, and the badge at its centre. A
colour-bound shutter tints its badge's rim with the colour it counts.
The blocks under it are not drawn, as today.

### Generators (M6)

- A generator is a **machine outside the frame**, on the edge it feeds,
  centred on its span: a rounded body a little wider than the span and
  about one cell deep, a dark screen on it, and the badge with the number
  of blocks still queued at its outer end.
- **The screen shows the next queued block** at all times: its shape, in
  miniature, drawn with the same quarter pieces in its colour. An exhausted
  generator is not drawn (M6).
- This replaces M6's grab-time preview on the target cells; the reference
  game shows the next block on the machine instead.
- **Layout:** the camera fit counts a machine's depth beyond the frame on
  every edge that holds a generator, so a machine is never cut off.

### Elevators (M9)

- An elevator is a pair of lift doors on the floor of its region: dark
  purple panels with faint vertical lines, a gold divider down the middle,
  and a thin gold-orange border around the region. They sit above the floor
  and below the blocks, so a wave stands on the doors.
- **No wave count is shown**; the reference game shows none.
- The elevator is drawn until its final wave has been cleared (M9).

### D47 in Core

`LevelContext` rejects a level in which two locked blocks — top-level
blocks, generator queues and elevator waves alike — share their outer
colour at level start, with a message naming both blocks and the colour.
No authored level has a lock, so no level changes. The editor surfaces the
error like every other level data error; no new warning.

### Engine rules that carry over

- All art is generated by `ArtGenerator` from `ArtRecipe`, greyscale and
  tinted at runtime where a colour varies (D46); what must not be tinted —
  the key's gold, the frost — is its own sprite.
- No magic numbers: every size, colour and sorting order is in
  `RuntimeConfig` or `ArtRecipe`.
- The drag, settle, exit and peel effects keep working: every new piece of a
  block sits under its root, and the exit fade reaches it.

---

## Left to you

- How each sprite is painted, and which are 9-sliced or tiled.
- The exact geometry of chains, padlock, key, slats, machine and doors,
  within the look described above.
- How the generator's miniature is scaled and centred on its screen.
- How `BoardLayout`'s fit learns the machine depth per edge.
- Starting values for every new field. The owner tunes them.
- The Editor steps the owner must do: list them precisely at the end.

---

## Tests

**Core (D47)**
- Two locked top-level blocks of the same colour are rejected, naming both.
- A locked block in an elevator wave and a locked top-level block of the
  same colour are rejected; so are two in one generator queue.
- Two locked blocks of different colours are accepted.
- A layered locked block counts by its outer colour: red over blue does not
  clash with a blue lock.

**`VisibilityLayer`**
- A locked block's `LockColor` is its outer colour at level start; an
  unlocked block has none.
- A key's `KeyMarkColor` is its target lock's colour; no key, no mark.
- `Generator` reports the queued count and the next block's cells and
  colour, and nothing once exhausted.
- `ElevatorPresent` is true while waves remain or a wave is still on the
  board, false after the final wave is cleared.

**`BoardLayout`**
- A generator on the left edge widens the fitted view by the machine depth
  on that side only; a board without generators fits as before.

**Art**
- The new sprites are byte-identical across two runs of an unchanged
  recipe.

**By hand (owner)**
- No authored level has every mechanic, so the owner builds a showcase
  level in the Level Editor for this check; whether it ships is decided
  then. In it: frozen blocks and gates read as ice with a
  count, locks as chains and a padlock with a count, keys carry the right
  colour, shutters and elevators look right, and a generator's screen shows
  the block that then arrives.
- Drag, settle, clear and peel still look right on blocks with the new
  marks.

---

## Resolved during implementation

- **Chains follow the footprint.** An X across the bounding box of a T or
  L would cross its empty corners and, sorted above block faces, draw over
  a neighbour there. `MarkLayout.ChainStrips` gives one horizontal strip per
  contiguous run of cells in each row, through the row's middle and inset
  at its ends, so a chain never leaves its block and no mask is needed. On
  a tall block that is one short strip per row; it reads as chain wrapped
  round the block and was kept.
- **Anchor.** Marks sit at the centre of the footprint's bounding box when
  every cell touching that point belongs to the block (T blocks included);
  otherwise at the nearest cell centre, ties to the lowest row and then the
  leftmost cell, which on an L is the bend. A frozen block's badge stays at
  the anchor and a padlock or key on it is raised by a configured offset.
- **Badge.** A full-size rounded box behind a smaller fill forms the rim, so
  it follows the badge's pill shape and a colour-bound shutter just
  recolours the back box. Width grows with the number of digits.
- **Generator machine.** It sits behind the frame, so the frame stays
  continuous over the generator's span, and its screen starts at the
  frame's outer edge. Its reach beyond the frame, for the camera fit, uses
  the badge width at the starting queue length — the widest it will show —
  so the camera never moves mid-level. The miniature is drawn in ice when
  the queued block would spawn frozen at the current counts, using the
  resolver's own check, and carries no chains or key.
- **`RandomBoards`** needed no change: a board it builds holds at most one
  lock. Four test fixtures with two locks of one colour were recoloured.
- **Recipe checks** that depend on each other (frost inset on the corner
  radius, the panel on the face's outline and rim) may report together; the
  range tests assert the named problem is present, not that it is alone.
  Tiled sprites' stripes must divide a cell exactly, so tiles meet cleanly.
- **Config.** Sorting orders are renumbered; their defaults are constants
  shared with a *Reset Sorting Orders* context menu on `RuntimeConfig`.
  Gate arrow and gate frost share one order, and block frost and the axis
  arrow reuse the stud and gloss orders, since a frozen or arrowed block
  has no studs. `frozenTint` became `iceColor` (kept through
  `FormerlySerializedAs`); `shutterColor` became `shutterSlatColor`, so the
  old grey could not override the new purple. `machineCornerCells` was
  added. The lock badge palette, placeholder bars and their fields are
  gone.
- **Showcase.** level-6 holds every mechanic with a state visual and was
  used for the check; the Level scene starts on it until the build's level
  set exists.
