# Module 14 — Keys only unlock

**Assembly:** `GateRush.Core`, with follow-ups in `GateRush.Solver`,
`GateRush.Serialization`, `GateRush.Editor` and the level files
**Depends on:** Modules 02, 03, 05, 07, 08, 09, 10
**Phase:** cleanup after 2.2

---

## Responsibility

Remove the `ClearOuterColor` key effect and everything that exists only for
it (D45). After this module a key has one effect: when the last key a lock
requires is consumed, the lock opens and its block may move. Nothing in the
game clears a block in place except jokers.

In scope:

- `KeyEffect` removed entirely: the enum, every property and constructor
  parameter carrying it, the DTO field, and the editor's "Key effect" field.
- The waiting effect removed (D41, D42): `BoardState.WaitingKeyEffect`,
  `BoardState.CreateInitialWithWaitingKeyEffects`,
  `MoveResolver.ReleaseWaitingKeyEffects` and every waiting branch.
- A\*'s heuristic back to `h = C`; `IsClearMonotone` back to "no generators
  and no elevators".
- `formatVersion` 4, and the six authored levels rewritten to it.
- The tests that exercised any of the above, rewritten or removed.

Not in scope: jokers (they never used the key effect, D9), Runtime
presentation (it never reads a key effect), and any other clean-up noticed
on the way. List those at the end instead of fixing them.

---

## Public surface

Removals only; nothing new is added.

```
Core
    KeyEffect                                   removed
    BlockDefinition.KeyEffect, ctor parameter   removed
    SpawnedBlock.KeyEffect, ctor parameter      removed
    BlockSpec.KeyEffect                         removed
    BoardState.WaitingKeyEffect                 removed
    BoardState.CreateInitialWithWaitingKeyEffects  removed
    LevelContext.IsClearMonotone                no generators, no elevators

Serialization
    LevelSerializer.FormatVersion               4
    BlockDto.keyEffect, SpawnedBlockDto.keyEffect  removed

Editor
    LevelDraft block and queue-entry KeyEffect  removed
```

`MoveResolver.TryApplyMove`, `TryClearBlock` and `TrySweepColor` keep their
signatures and behaviour.

---

## Design decisions (owner)

### A completed lock opens at once (D45)

`ApplyKeyEffects` still consumes a key when its carrier dies, counts the
lock's consumed keys, and acts once when the count reaches
`RequiredKeyCount`. What it does then:

- **The lock's block is alive** — uncovered or under a closed shutter: it is
  unlocked in this resolution. A block under a closed shutter still cannot
  move or be targeted until the shutter opens (M5); the unlock is simply
  already done when it does.
- **The lock's block has not spawned yet:** its slot is unlocked. In the
  code today `SpawnBlock` does not touch `Unlocked` — the slot's flag is set
  at level start — so the block arrives unlocked. Confirm this against
  `SpawnBlock` and `CreateInitial` rather than assume it.
- **The lock's block was destroyed** (by a joker, D11): the key is spent and
  nothing else happens.

A key never emits `ColorCleared`. Keys therefore no longer feed the drain
loop; conditions and spawns still drive it.

### The clear step stays

The private step that removes a block's outer colour, emits the event and
kills the block on its last colour is what a gate exit, `TryClearBlock`
(rocket) and `TrySweepColor` (broom) all go through. It stays exactly as it
is. Rename it (for example `ClearCurrentColor`) so that no identifier named
`ClearOuterColor` is left to suggest a key effect still exists.

### The solver

- **A\*:** `h = C`, colours remaining on living blocks plus colours still
  queued. With no key clear a move removes at most one colour, so this is
  admissible and consistent. The `F` term, its helpers and the long proof
  in `AStarStrategy`'s remarks go; replace the proof with the one-line
  argument above.
- **`IsClearMonotone`:** no generators and no elevators.
  `HasLockWithMixedKeyEffects` goes.
- **`NextClearAbstraction`** starts its copy from `BoardState.CreateInitial`;
  there is nothing waiting to carry.
- **`BlockSymmetry`:** blocks no longer differ by key effect.

### Level format 4

- `formatVersion` 4. Version 3 is refused like every earlier version: the
  six authored levels are rewritten in the same change, and no other level
  file exists (Module 08, D45).
- Rewrite `Assets/Resources/Levels/level-0.json` to `level-5.json`:
  `formatVersion` becomes 4 and every `keyEffect` entry is removed, in
  blocks and in generator and elevator queues. Nothing else in these files
  may change — no reformatting, reordering or re-serialising. Report the
  number of `keyEffect` entries removed per file, and confirm none said
  `ClearOuterColor`.
- The example JSON in `LevelDtos.cs`'s comments follows.

### Engine rules that carry over

- `Core` and `Solver` stay free of `UnityEngine`.
- No `.meta` files are written. `KeyEffect.cs` is deleted by the owner in
  the Unity Project window, so its `.meta` goes with it: write every other
  change as if it were already gone, and do not delete it yourself.

---

## Left to you

- How `ApplyKeyEffects` reads once the waiting branches are gone.
- Whether any remark about `MaxResolutionPasses` or the drain loop counted
  key clears, and how it reads without them. Check; do not assume.
- `BoardState`'s hashing, equality and D35 row comparison without the
  waiting field.
- Which existing tests are deleted, which rewritten, and which merely lose a
  `keyEffect:` argument. List them in the plan.
- `RandomBoards` keeps generating locks and keys, now without an effect.

---

## Tests

**Locks (Core)**
- A lock whose last key is consumed while its block is uncovered unlocks it
  in that resolution, and the block can then be moved.
- A lock whose last key is consumed while its block is under a closed
  shutter unlocks it in that resolution; the block cannot move until the
  shutter opens, and can in the resolution that opens it.
- A lock whose last key is consumed before its block spawns: the block
  spawns unlocked, from a generator and from an elevator wave.
- A lock whose block was destroyed by a joker: the key is consumed and
  nothing else changes.
- Completing a lock emits no `ColorCleared`: the clear counters after the
  move equal those from the key carrier's own clear alone.
- A lock needing two keys opens only on the second, in either order.

**The central decision**
- `TryClearBlock` and `TrySweepColor` behave exactly as before: every
  existing joker test passes unchanged, apart from dropped `keyEffect:`
  arguments.

**Solver**
- The A\* heuristic equals the colours remaining plus colours queued.
- It stays consistent over the whole corpus, and BFS and A\* still agree on
  the optimum (the existing property tests, on the new corpus).
- `IsClearMonotone` is true for a level with locks and keys and no
  spawners, false with a generator or an elevator.

**Serialization**
- Format 4 round-trips a level with locks and keys, in blocks and in
  queues.
- A version-3 file is refused, naming the version.

**By hand (owner)**
- Each of the six levels opens in the Level Editor and validates as before.
- Each plays in the Level scene.

---

## Resolved during implementation

- **`ApplyKeyEffects`** keeps its order: consume the key, count the lock's
  consumed keys, return if the lock is already open (a later key is spent
  and changes nothing), return if the owner was destroyed, otherwise unlock.
  `SuccessorBuilder.ShutterOpen` went with the waiting branches, its only
  readers.
- **The clear step** is `ClearCurrentColor`. With no key clear left, no path
  clears one block twice in a resolution, so its doc no longer gives an
  example; it states the mechanism instead — the colour is read from the
  successor's pending count, not the source state's.
- **Tests.** A key arriving after its lock opened is still covered by its
  own test, replacing the D41 "later key" test. The version-3 refusal test
  injects `"keyEffect": "ClearOuterColor"` into its fixture: the silent
  misread D45 names. `RandomBoards` dropped the effect draw with no
  discarded draw left behind; every seeded property test still passes on
  the new boards, floors unchanged.
- **Search corpus.** Five boards that relied on a key clear were deleted,
  since without one several become unsolvable. The mixed-effect lock board
  became a two-key lock board, and the generator board that released a
  waiting effect gained a gate and became a lock completed before its block
  spawns; its optimum is now 3, and the pipeline test follows. Every
  remaining lock board's optimum equals `C`.
- **Level files.** Only `formatVersion` and the `keyEffect` lines changed:
  89 lines in all, every one `UnlockMovement`. Each file keeps its line
  endings (`level-5.json` is CRLF, the others LF). All six validate as
  before; level-2 and level-3 are still settled by nearest-next-clear after
  the quick A\* stage.
