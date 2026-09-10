## Next round — small findings from the undo pass

Three items that surfaced while testing Sessions A–C. None needs a design
decision; each is a gap between what a document says and what the code does.

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

---

## When this round is done

Delete it from this file, as before. None of these three reverses a stated
decision, so nothing goes to `DECISIONS.md`.
