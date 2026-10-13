# Module 26 — Generator preview

**Assembly:** `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 10, 13, 16, 18
**Phase:** Polish, before Phase 4 (D53)

---

## Responsibility

Show the player where a generator's next block will land. While the player
holds a block that stands on any of the cells the next block needs, the
board shows that block's footprint on those cells, faintly, beneath the
blocks. Letting go hides it. The generator's own screen (D48) is
unchanged.

In scope:

- Deciding which generators preview for a held block, and on which cells:
  plain, tested code.
- Drawing the preview on the board, fading it in on grab and out on
  release.

Not in scope: any rule change (M6 is unchanged; the preview is
presentation only), the Level Editor, the Generator introduction card,
previews outside a drag.

---

## Public surface

```
GeneratorPreview                    plain C#, Runtime
    static IReadOnlyList<GeneratorPreviewTarget> For(
        LevelContext ctx, BoardState state, int heldBlockIndex)
        // every shown generator whose next block's cells, at its spawn
        // origin, include a cell the held block covers at its origin in
        // state; empty when none does

GeneratorPreviewTarget              readonly struct, Runtime
    int GeneratorIndex
    IReadOnlyList<Coord> Cells      // board cells the next block will occupy
    BlockColor? Color               // null when it would spawn frozen
    bool IsFrozen

BoardView                           existing
    BeginDrag(int blockIndex)       also shows the previews for that block
    Settle / Snap / CancelDrag path also hides them
```

Names are negotiable. That the targets are decided by plain, tested code
from `LevelContext` and `BoardState`, using the same spawn origin and the
same next-block rule the resolver and `VisibilityLayer` use, is not.

---

## Design decisions (owner)

### When it shows

- From the moment a block is grabbed until it is released, for every
  generator that is still shown and whose next block's target cells
  include at least one cell the held block covers at its start position. A
  board can show more than one preview at once.
- The test uses the block's position when it was grabbed, not where it is
  being dragged: the preview answers "what comes in if I move this block
  away".
- Release hides it, whatever the outcome: a settle, a snap back, a cancel,
  a restart or the way to the menu. When the move frees the cells, the
  arrive stage spawns the real block from the machine (Module 18) as
  today; the preview is already fading out.
- Nothing shows when no block is held, when the held block covers none of
  the target cells, or for a generator whose queue is empty.

### How it looks

- One piece per target cell: a rounded cell in the next block's outer
  colour, translucent, with a thin rim a little darker, inset slightly
  from the cell edges, as in the reference screenshot. A block that would
  spawn frozen previews in the ice colour.
- No studs, no lip, no marks (padlock, key, time bonus, layers, axis
  arrow): the preview says where and what colour, the machine's screen
  says the rest.
- It sits beneath every block part, the lifted block's outline included,
  and above the board floor. Where a target cell holds a block, that block
  covers the preview, as in the reference. A closed shutter covers it too.
- It fades in over a short time on grab and fades out on release.

### Engine rules that carry over

- No magic numbers: the fill and rim alpha, rim thickness, inset, corner
  radius (or sprite), fade durations and sorting order are `RuntimeConfig`
  fields with `Problems()` checks. The sorting order must sit above the
  floor and below the block lip and the lift outline.
- Reuse existing sprites (the rounded rectangle or the quarter tiles); no
  new art unless neither works, and say so if so.
- Every preview piece sits under the view's transform, every tween carries
  the view as its id, and `Clear`, `Rebuild`, disable and destroy leave
  nothing behind (Module 12).

---

## Left to you

- Whether the pieces are pooled or rebuilt per grab.
- How the fade is tweened, and how a grab during a fade-out is handled.
- Starting values for every new field. The owner tunes them.
- The exact Editor steps for the owner.

---

## Tests

**`GeneratorPreview.For`**
- A block covering one of the next block's cells gives that generator,
  with the next block's cells at its spawn origin and its outer colour.
- A block covering none of them gives nothing.
- A block covering cells of two generators' next blocks gives both.
- A generator whose queue is exhausted gives nothing.
- A next block that would spawn frozen at the current counts gives
  `IsFrozen` and no colour; the same block after enough clears gives its
  colour.
- The cells match `LevelContext.GeneratorSpawnOrigin` plus the queue
  entry's footprint for the generator's current queue index (the central
  decision: the preview lands exactly where the resolver spawns).

**By hand (owner)**
- In the generator level, grabbing the block in front of the machine shows
  the next block's footprint, partly hidden beneath the held block;
  releasing hides it.
- Moving the block away spawns the real block where the preview was.
- Grabbing an unrelated block shows nothing.
- Restart, Menu and Next during a drag leave no preview behind.
