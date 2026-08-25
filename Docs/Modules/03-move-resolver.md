# Module 03 — `MoveResolver`

**Assembly:** `GateRush.Core`
**Depends on:** Modules 01, 02
**Phase:** 1.3 (movement and gates only), extended in 1.7, 1.8, 1.12, 1.13

---

## Responsibility

Apply an action to a board and resolve **every** consequence that follows, until
the board stops changing.

This is the most important module in the project. Every rule interaction in the
game passes through it.

---

## Public surface

```
readonly struct Move
    int BlockIndex
    Coord TargetOrigin        // MAY equal the current origin — see D25

sealed class MoveResolver
    bool TryApplyMove(LevelContext ctx, BoardState state, Move move,
                      out BoardState result)

    // Joker and key entry points — same pipeline, different trigger
    bool TryClearBlock(LevelContext ctx, BoardState state, int blockIndex,
                       out BoardState result)
    bool TrySweepColor(LevelContext ctx, BoardState state, BlockColor color,
                       out BoardState result)
```

All three return `false` when the action is not legal, leaving `result`
untouched. They never throw for player error.

---

## Resolution algorithm

```
 1. Validate
      block alive
      CanMove is true (axis, frozen, locked, shutter)
      TargetOrigin is REACHABLE from the current origin: there exists a path of
      single-cell orthogonal steps, using only directions the block's
      MovementAxis permits, along which EVERY intermediate position is fully
      legal — whole footprint inside the grid, clear of static walls, clear of
      other living blocks, outside closed shutter regions.
      This is a flood fill, not a straight-line scan. The block may turn corners.
      There is no diagonal step: a target whose only approach is diagonal is
      unreachable.

      ZERO-DISTANCE MOVE: TargetOrigin == current origin. Skip the path check.
      The move is legal only if the block is flush against a compatible open
      gate — otherwise it is a no-op and must be rejected.

 2. Move the block to TargetOrigin

 3. Gate check
      if the footprint is flush against an edge, and a gate there is open,
      matches the block's current colour, has width >= the block's projection
      span, and fully contains that projection:
          increment ClearedColors
          enqueue ColorCleared(blockIndex, clearedColor)
          the block STAYS at the gate mouth and continues to obstruct it
          if the stack is now exhausted: mark dead, free its cells,
              report TimeBonusSeconds to the caller

      A block that ends flush against a compatible gate WITHOUT being pushed
      into it is NOT cleared. Exit is move-triggered (D25).

 4. Drain the event queue
      for each ColorCleared:
          TotalClearCount++
          ClearCountByColor[color]++
          if the block died and carried a key:
              mark the key consumed and apply its effect to the target lock:
                UnlockMovement   -> set Unlocked once enough keys are consumed
                ClearOuterColor  -> clear the target's colour, which enqueues
                                    a NEW ColorCleared event

 5. Re-evaluate unlock conditions
      gates    : TotalClearCount >= OpenAtClearCount      -> open
      blocks   : TotalClearCount >= UnfreezeAtClearCount  -> unfreeze
      shutters : global or per-colour count >= Threshold  -> open

 6. Check spawn triggers
      generators : every target cell empty and queue non-empty -> spawn next
      elevators  : region contains no blocks and waves remain  -> place wave

 7. If anything changed in steps 4-6, return to step 4

 8. Return the new BoardState
```

`TryClearBlock` checks `CanBeTargeted`, clears the block's current colour with no
movement and no gate requirement, then enters step 4.

`TrySweepColor` collects every targetable block whose current colour matches,
clears each, then enters step 4 with all events queued. Because adjacent layers
differ (D26), no block can match twice within one sweep.

---

## Design decisions (owner)

**Movement is reachability-based** (D27). Validation is a flood fill from the
current position, not a straight-line scan. For multi-cell and L-shaped blocks
the whole footprint must be legal at every intermediate step, which is what
prevents such blocks from turning corners in tight spaces.

**One pipeline for moves and jokers** (D9). Rocket is `TryClearBlock`. Broom is
`TrySweepColor`. There is no second removal path.

**Exit is move-triggered, and zero-distance moves are how a pre-aligned block is
cleared** (D25). Because levels start tightly packed, this is usually the first
move of the level. A block that merely passes in front of a compatible gate is
not cleared.

**A gate opening mid-level needs no special handling.** The waiting block is
cleared by an ordinary zero-distance move once the player pushes it.

**Step 7 is what makes mechanics composable.** A shutter opening can reveal an
elevator whose region is already empty, whose wave carries a key, which unlocks a
block. No fixed number of passes is sufficient; loop to a fixpoint.

**Guard the loop with an iteration limit and throw on overflow.** Exceeding it
means the level data contains a cycle. That is an authoring error and should be
loud, not a hang.

**Blocks stay at the gate after clearing.** Deliberate: the block now obstructs
the gate, and its newly exposed colour cannot match that gate, so the player must
move it away. Do not "helpfully" move it aside.

**Time bonuses leave `Core` as an output, not as state.** `Core` has no concept
of a countdown; it reports seconds earned and the caller applies them.

---

## Left to you

- Projection-span and alignment computation for arbitrary footprints, including
  L shapes where the projection exceeds the number of cells touching the wall.
- Event queue structure.
- Generator target-cell derivation from edge and offset.
- Elevator wave placement.
- The copy strategy for producing the successor `BoardState`.
- Whether the resolver is stateless with static methods or an instance with
  reusable buffers. Prefer whichever keeps allocation low — it runs inside the
  search loop.

---

## Tests

**Movement**
- A block reaches a position around a corner when a free orthogonal route exists.
- A block cannot reach a diagonally adjacent cell when both orthogonal
  neighbours are blocked.
- A 1×1 block turns a corner through a one-cell gap; an L-shaped block in the
  same gap cannot.
- An axis-restricted block reaches only positions along its permitted axis, even
  when a corner route exists for an unrestricted block.
- A block reaches an intermediate empty cell along its route.
- A block cannot pass through another block.
- An axis-restricted block rejects a perpendicular target.
- A frozen block rejects any move; an unfrozen one accepts.
- A locked block rejects any move.
- No block may enter a closed shutter region.

**Zero-distance moves**
- A block flush against a compatible open gate is cleared by a zero-distance
  move.
- A zero-distance move is rejected when the block is not at a compatible gate.
- A block that slides past a compatible gate and stops beyond it is **not**
  cleared.
- A gate that opens mid-resolution leaves its waiting block uncleared until the
  player issues a zero-distance move.

**Gates**
- A 1×2 vertical block exits a width-1 bottom gate.
- The same block is rejected by a width-1 side gate and accepted by a width-2
  one.
- A 2×2 block is rejected by a width-1 gate.
- A 1×1 block is accepted by a width-2 gate.
- An L block whose projection is 2 is rejected by a width-1 gate even though only
  one cell touches the wall.
- A colour mismatch is rejected.
- A closed gate is rejected.
- A partially overlapping projection is rejected.

**Layers**
- A two-colour block clears once and remains at the gate mouth, now showing the
  second colour.
- That block cannot be cleared again at the same gate.
- The counter credits the colour that was removed, not the one beneath.

**Obstruction**
- A block parked at a gate blocks another block from using it.

**Chains**
- A clear crossing a gate threshold opens that gate in the same pass.
- A clear crossing a shutter threshold opens the shutter, and an elevator beneath
  it with an empty region places its wave in the same pass.
- A key whose effect is `ClearOuterColor` triggers a second clear that itself
  crosses a threshold, all inside one resolution.
- A broom sweeping many blocks opens several gates and shutters at once and
  cascades correctly. **This is the primary fixpoint stress test.**
- A construction that would loop forever throws rather than hanging.

**Spawners**
- A generator does not spawn when one target cell is occupied.
- It spawns as soon as that cell is cleared.
- An elevator waits until its region is entirely empty.
