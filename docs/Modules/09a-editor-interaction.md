## Next round — small findings from the undo pass and early level authoring

Eight items, none needing a design decision — each is a gap between what a
document says (or an existing pattern already enforces elsewhere) and what
this code does. The ninth formalises a M5 clarification instead; see the note
under it.

### Empty elevator waves produce no warning

`DraftValidator.AddElevatorTilingWarnings` skips a wave with no blocks. M9 says
waves arrive fully packed, so an empty wave is the loudest possible violation,
and `09-level-editor.md` says warnings are what a designer reads while building
— an unfilled wave is exactly such an item. Remove the early `continue`;
`ElevatorTiling.Check` already reports every cell uncovered.

### A block's unfreeze threshold is never checked

`MECHANICS.md` warns when a gate or shutter threshold exceeds the clears
available. An M3 block whose `UnfreezeAtClearCount` exceeds them is permanently
frozen — the same failure, unwarned. Confirm `BlockLike` does not carry the
field, then add the warning beside the gate and shutter ones.

### `RegionMoveRect` with a region larger than the grid

If a loaded level holds a region wider than the grid, the delta clamp's lower
bound exceeds its upper bound and the region is pushed out of bounds. Reachable
only through hand-edited JSON, which this editor exists to repair. When the
bounds invert, the delta is zero.

### Generator markers overlap when a neighbour widens

Two generators sitting side by side on the same edge draw their marker rects
independently; widening one does not account for its neighbour's marker,
so one visually covers the other. Cosmetic only — `AddGeneratorWidthWarnings`
already catches an actual span conflict at the data level.

### No visual boundary between adjacent blocks

`LevelEditorWindow` fills each cell by colour with no stroke at a block's own
outer edge, so two same-coloured blocks sitting next to each other are
indistinguishable from one bigger block. Add a thin outline along each block's
true footprint boundary — cells whose neighbour belongs to a different block,
a wall, or nothing — computed from `Cells`, not the grid lines.

### Placing a preset block does not check grid bounds

`PlaceOrSelectBlock` → `FootprintClear` → `CellClearForBlock` checks a
candidate cell against other blocks and walls only, never against
`draft.Width`/`draft.Height`. `DraftDrag.IsLegalOnBoard` already does this
correctly for moving an existing block; `CellClearForBlock` needs the same
bounds check.

### Gates and generators can be placed overlapping

`AddGateOnEdgeNearest` and `AddGeneratorOnEdgeNearest` add a new edge feature
at the clicked offset with no check against another gate or generator already
occupying that span. M6 is explicit that edge features never overlap and that
an overlap is a level data error — `LevelContext` likely rejects it once
`ToContext()` runs, but nothing stops the click from creating the broken draft
in the first place.

### Gates and generators cannot be dragged

`TrySelectEdgeMarker` selects a gate or generator marker (with the existing
highlight), but `DragKind` has no case for one, so `TryBeginDrag` never starts
a drag for either. Moving one requires typing a new `Offset` into the
properties panel. Add a drag path mirroring `DraftDrag`'s board-block one:
legal while inside the grid and clear of every other edge feature's span on
the same edge.

### Shutter regions are not required to be fully covered

*Formalises a clarification to `MECHANICS.md` M5, not a pre-existing rule —
see the addition there before implementing this one.* Add a warning, parallel
to `AddElevatorTilingWarnings`, over `draft.Shutters`: for each shutter, every
cell in its `Min`–`Max` rectangle should be covered by some living block in
`draft.Blocks`. Report the uncovered cells the same way
`ElevatorWaveNotExactTiling` does. This is a live warning, not a save-blocker,
matching every other check here.

---

## When this round is done

Delete it from this file, as before. Eight of these are gaps between existing
rules and the code; nothing about them reverses a stated decision, so nothing
goes to `DECISIONS.md`. The ninth's rule change already lives in
`MECHANICS.md` M5 — no separate `DECISIONS.md` entry needed for that either.