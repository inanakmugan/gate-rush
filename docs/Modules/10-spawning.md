# Module 10 — Spawning: generators and elevators at runtime

**Assembly:** `GateRush.Core`, with small follow-ups in `GateRush.Solver` and
`GateRush.Editor`
**Depends on:** Modules 01, 02, 03, 06, 07
**Phase:** 1.13

---

## Responsibility

Make `MoveResolver.CheckSpawnTriggers` real. Until now it has returned `false`
unconditionally, so a level with a generator (M6) or an elevator (M9) can be
authored and saved but never solved: its spawn slots stay dead, `IsSolved` can
never become true, and Validate refuses such levels (D40). This module makes
spawners run, closes phase 1.13, and with it Phase 1.

In scope:

- Generator spawn placement and trigger (M6).
- Elevator wave placement, trigger, and `ElevatorWaveActive` upkeep (M9).
- Spawning at level start (D42).
- Keys whose lock has not spawned yet (D42, replacing today's "held key"
  branch in `ApplyKeyEffects`).
- The follow-ups that were waiting on spawning: canonical criterion 2 in
  `MoveGenerator`, the D40 guard in `ValidationPipeline`, the generator-entry
  TODO in `DraftValidator`, and the missing elevator/generator validation in
  `LevelContext`.

Not in scope: presentation of spawning (the preview of an incoming block, M6),
which is Runtime work.

---

## Public surface

```
LevelContext
    Coord GeneratorSpawnOrigin(int generatorIndex, int queueIndex)
        // where that queued block lands; precomputed (D28)

BoardState
    static BoardState CreateInitial(LevelContext ctx)
        // unchanged signature; now returns the SETTLED initial state (D42)

MoveResolver
    internal virtual bool CheckSpawnTriggers(LevelContext ctx,
                                             SuccessorBuilder builder)
        // no longer a no-op
```

Everything else is behind existing surfaces.

---

## Design decisions (owner)

### Generator placement

A queued block spawns **flush against the generator's edge, aligned to its
offset** (M6, D34). With the block's cells normalised so their minimum is
`(0, 0)` (D30), and `maxX`/`maxY` the largest cell coordinates:

| Edge | Spawn origin |
|---|---|
| Bottom | `(Offset, 0)` |
| Top | `(Offset, Height - 1 - maxY)` |
| Left | `(0, Offset)` |
| Right | `(Width - 1 - maxX, Offset)` |

This is a pure function of immutable level data, so it is precomputed in
`LevelContext` beside `SpecAt` (D28), and the editor reads it from there
rather than deriving it a second time (D31).

### Triggers

- **Generator.** Its queue is not exhausted and every cell the next queued
  block would occupy is free of living blocks. Spawning places the block,
  advances `GeneratorIndex`, and stops; the placed block now occupies those
  cells, so a generator spawns at most once per pass.
- **Elevator.** Waves remain and no living block occupies any cell of the
  region — any block, not only the wave's own: a top-level block authored
  there, or one the player moved in, holds the next wave back. Placing a wave
  puts each block at `Min + RegionOrigin`, advances `ElevatorWaveIndex`, and
  sets `ElevatorWaveActive`.
- **`ElevatorWaveActive`** keeps Module 02's meaning: set when a wave is
  placed, cleared by the first pass that finds the region holding no living
  block at all — a block that entered from outside keeps it set. The same
  pass that finds the region empty clears the flag and, if waves remain,
  places the next one.
- **Every wave is non-empty.** M9 requires a wave to tile its region
  exactly, so an empty wave is a level data error, naming the elevator and
  the wave. The editor already warns about one while it is being built.

### A closed shutter does not stop a spawn (D42)

A spawn is not a move, a joker or a key; it is the level's own machinery
filling empty space. An empty region under a closed shutter receives its wave
hidden, and a generator whose cells sit under a closed shutter still spawns.
The shutter still makes everything under it unreachable and untargetable (M5).

### Spawning at level start (D42)

A generator or elevator whose target is empty when the level starts spawns
before the first move. `BoardState.CreateInitial` therefore returns the state
after one action-free resolution — spawn triggers and condition re-evaluation
run to a fixpoint, exactly as after a move. Every caller (solver, editor,
runtime) starts from the same settled board, and there is still only one
resolution path (D9).

### A spawned block's starting row

`Alive`, its placed origin, no colours cleared, `KeyConsumed` false.
`Unfrozen` if it carries no threshold or the current `TotalClearCount`
already meets it. `Unlocked` if it carries no lock. `WaitingKeyEffect` as the
key rule below leaves it.

### Keys for a lock that has not spawned (D42)

Today `ApplyKeyEffects` returns early — without consuming the key — when the
lock's block is an unspawned slot. That branch is replaced. The key is
consumed like any other; if it completes the count, the completing key's
effect is stored in the owner slot's `WaitingKeyEffect` (the D41 field; the
slot exists at a fixed index before it spawns, Module 02). When the block
spawns, a waiting effect applies in the same resolution — unless the block
spawns under a closed shutter, in which case it keeps waiting and D41's
release applies it when the shutter opens.

A* is unaffected in principle: `F` already counts a lock whose block is not
yet spawned, and counts it through `WaitingKeyEffect` once its keys complete,
so the proof in `AStarStrategy`'s remarks covers the release-on-spawn case as
one more way a counted lock fires. Confirm this rather than assume it.

### Spawning never clears

A spawned block that lands flush against a compatible open gate is **not**
cleared: it arrived by spawning, not by a move. It waits for a push, like the
four cases in D25. `CheckSpawnTriggers` never reads gate geometry.

### Order within a pass

Generators in `LevelContext.Generators` order, then elevators in
`LevelContext.Elevators` order, each check seeing the cells every earlier
spawn in the same pass has filled. Deterministic, like everything the solver's
guarantees depend on.

A generator's spawn footprint may lie inside an elevator region. They then
contend for the same cells: the generator keeps spawning whenever its cells
are empty, a block it placed holds the next wave back like any other block,
and a placed wave holds the generator back until its cells are empty again.

### What stays as it is

- `IsClearMonotone` stays false on any level with a generator or elevator
  (D37). Nearest-next-clear will report `Indeterminate`, never `Unsolvable`,
  on such levels; the A\* stages still prove what they can.
- `MaxResolutionPasses` already counts every spawn.
- The progress vector already carries spawn indices (D6, D32). Note that a
  spawn can advance it without any clear — a move that vacates a generator's
  cells is enough — which BFS stratification already handles.

### Resolved during implementation

- **The pass bound needs one more argument.** A region can be emptied by a
  move with no clear — a wave's last block slid out — so a pass can clear
  `ElevatorWaveActive` with no clear drained and no spawn. That happens only
  in the first pass of such a move, at most once per resolution, and is
  charged to the elevator's final wave, which was placed in an earlier
  resolution and so is never spent in this one. It relies on a spawned
  block's frozen flag being decided at spawn, against the running counters,
  rather than by the next scan. `MoveResolver.ResolveToFixpoint`'s remarks
  carry the full argument.
- **A generator refills a shared region first.** When clearing a wave block
  frees a generator whose footprint lies in an elevator region, the
  generator — checked first — refills the region in the same pass. The region
  never reads empty, so the next wave waits until the generator's queue is
  done or its block leaves. This is the intended priority: the generator's
  output is dealt with before a new wave arrives.
- **A waiting effect never reaches an unspawned block.** D41's release on a
  shutter opening skips owners that are not alive; an unspawned owner's
  effect is applied by the spawn, never by an opening.

---

## Left to you

- How `CreateInitial` runs the action-free resolution without `BoardState`
  depending on `MoveResolver` in a way that creates a cycle — an internal
  entry point on the resolver is fine.
- The occupancy test the triggers use mid-resolution, where no `BoardState`
  exists yet; follow the pattern `IsInsideClosedShutter` set in D41.
- The precomputed layout behind `GeneratorSpawnOrigin` (per generator, per
  queue entry, or by flat index).
- **Canonical criterion 2** in `MoveGenerator`: positions that vacate or
  occupy the cells the generator's *next* queued block needs. Criterion 3
  (elevator regions) already exists.
- **`ValidationPipeline`**: remove the D40 spawner guard and its test.
  Keep `ValidationOutcome`; it costs nothing and the next unsupported case
  will want it.
- **`DraftValidator`**: fill `BlockLike.Origin` for generator queue entries
  from `GeneratorSpawnOrigin` (the TODO(1.13) left there), so the axis
  warning counts cross-axis gates for them too.
- **`LevelContext` validation** that phase 1.13 makes load-bearing, each a
  level data error naming the element:
  - every elevator region lies inside the grid and covers no static wall;
  - elevator regions do not overlap each other;
  - every generator's spawn footprint, for every queued block, lies inside
    the grid and covers no static wall.

---

## Tests

**Generator placement**
- For each edge, a queued 1×1, a 1×2 along the edge and an L block land at the
  origin the table above gives.
- A queued block whose footprint would leave the grid or cover a wall is
  rejected at construction, naming the generator and the entry.

**Generator trigger**
- A generator does not spawn while one target cell is occupied, and spawns in
  the resolution that frees it.
- A move that vacates the cells without clearing anything still triggers the
  spawn.
- After the last queued block spawns, the generator never spawns again, and
  the level can be solved.

**Elevator trigger**
- The next wave waits while any living block — the wave's own or any other —
  occupies the region.
- The resolution that empties the region places the next wave at
  `Min + RegionOrigin` for each block, and `ElevatorWaveIndex` advances.
- `ElevatorWaveActive` is set when a wave is placed and cleared by the pass
  that finds the region empty; a foreign block in the region keeps it set.
  `IsSolved` is false until the final wave is cleared.
- The tightest pass bound: one elevator, one one-block single-colour wave,
  the block moved out of the region without clearing — the flag clears in a
  pass with no clear and no spawn, and `MaxResolutionPasses` still holds.
- An empty wave is rejected at construction, naming the elevator and wave.
- A generator whose footprint lies inside an elevator region: its block holds
  the wave back, and a placed wave holds the generator back.

**Level start (D42)**
- A generator whose cells are empty at start has spawned in the state
  `CreateInitial` returns; so has an elevator whose region is empty.
- A level with no spawners gets exactly the same initial state as before.

**Shutters (D42)**
- An empty region under a closed shutter receives its wave; the wave's blocks
  cannot move or be targeted until the shutter opens.
- A generator whose cells are under a closed shutter still spawns.

**Keys for unspawned locks (D42)**
- A key whose lock has not spawned is consumed; when it completes the count,
  the effect waits in the owner slot.
- When the owner spawns uncovered, the waiting effect applies in that
  resolution: `UnlockMovement` unlocks it, `ClearOuterColor` also clears its
  outer colour.
- When the owner spawns under a closed shutter, the effect keeps waiting and
  applies when the shutter opens.

**Never clears**
- A block spawning flush against a compatible open gate is not cleared, and a
  zero-distance push then clears it.

**Chains**
- Clearing the last block of an elevator wave, which carries a
  `ClearOuterColor` key, clears a locked block's outer colour; that clear
  crosses a shutter threshold; and the emptied region receives the next wave
  — all in one resolution, as ARCHITECTURE's core concept 3 describes.

**Solver**
- Corpus boards with a generator and with an elevator: BFS and A\* agree on
  the optimum, the heuristic stays consistent over the full state graph, and
  nearest-next-clear returns `Solvable` or `Indeterminate`, never `Unsolvable`.
- Canonical mode emits a move that vacates a generator's next spawn cells.
- `ValidationPipeline` returns a verdict for a spawner level.
- `RandomBoards` gains generators and elevators, and every existing property
  test still passes on the expanded corpus.
