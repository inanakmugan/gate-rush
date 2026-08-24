# Module 02 — `BoardState`

**Assembly:** `GateRush.Core`
**Depends on:** Module 01
**Phase:** 1.2

---

## Responsibility

Represent the board at one instant. This type is the key of the solver's visited
set, so it must hash and compare correctly and cheaply.

Everything that can change during play lives here. Nothing else does.

---

## Public surface

```
sealed class BoardState
    // Per block, indexed by block index (not id)
    IReadOnlyList<Coord> Origins
    IReadOnlyList<byte> ClearedColors     // how many colours already removed
    IReadOnlyList<bool> Alive
    IReadOnlyList<bool> Unfrozen
    IReadOnlyList<bool> Unlocked

    // Per gate / shutter
    IReadOnlyList<bool> GateOpen
    IReadOnlyList<bool> ShutterOpen

    // Spawner progress
    IReadOnlyList<int> GeneratorIndex
    IReadOnlyList<int> ElevatorWaveIndex
    IReadOnlyList<bool> ElevatorWaveActive

    // Progress counters
    int TotalClearCount
    IReadOnlyList<int> ClearCountByColor
    IReadOnlyList<bool> KeyConsumed

    static BoardState CreateInitial(LevelContext ctx)

    BlockColor CurrentColorOf(LevelContext ctx, int blockIndex)
    bool IsFullyCleared(LevelContext ctx, int blockIndex)
    IEnumerable<Coord> OccupiedCells(LevelContext ctx, int blockIndex)
    bool IsCellFree(LevelContext ctx, Coord c, int ignoreBlockIndex = -1)

    bool CanMove(LevelContext ctx, int blockIndex)
    bool CanBeTargeted(LevelContext ctx, int blockIndex)

    bool IsSolved(LevelContext ctx)

    int GetHashCode()
    bool Equals(object obj)
```

Blocks spawned by generators and elevators are appended to the same arrays, so
indices grow during play. `LevelContext` must expose spawned-block definitions in
a matching order; choose a scheme and document it in the class summary.

`CurrentColorOf` is the single point of truth for "what colour is this block."
Gates, rockets, and brooms all match against it. Nothing addresses a colour
beneath the current one.

---

## Design decisions (owner)

**Every dynamic field participates in the hash.** Some counters are in principle
derivable from `ClearedColors` and `Alive`. Do not derive them. A redundant field
costs nanoseconds per state; a missing field produces a solver that silently
reports wrong answers.

**FNV-1a, 32-bit, over the fields in a fixed declared order.** Cache the result
in a readonly field computed in the constructor — the visited set requests it
repeatedly.

**`Equals` performs a full field comparison after the hash matches.** Hash
collisions are rare, not impossible, and a collision here corrupts the search.

**`CanMove` and `CanBeTargeted` are separate predicates**, per `DECISIONS.md`
D11:

| Condition | `CanMove` | `CanBeTargeted` |
|---|---|---|
| Normal, alive | true | true |
| Frozen (M3) | **false** | **true** |
| Locked (M8) | **false** | **true** |
| Inside a closed shutter (M5) | false | **false** |
| Dead | false | false |

**`IsSolved` requires three conditions:** no living blocks, every generator queue
exhausted, every elevator wave list exhausted with nothing remaining inside.

**Structural sharing is permitted but optional.** A straightforward full copy per
state is acceptable first. If profiling later shows copying dominates, revisit —
but do not pre-optimise into copy-on-write before the search is proven correct.

---

## Left to you

- The FNV-1a implementation and the exact field ordering (document it).
- The array-copy strategy used when producing a successor.
- `IsCellFree`: bounds, static walls, closed shutter regions, and living block
  footprints, with `ignoreBlockIndex` excluded so a block does not collide with
  itself while moving.
- Index scheme for dynamically spawned blocks.

---

## Tests

- Two states built independently from identical data are `Equals` and share a
  hash code.
- Changing any single dynamic field changes the hash. Write this table-driven,
  covering **every** field — this is the test that protects against the failure
  mode this module exists to avoid.
- `CanMove` and `CanBeTargeted` return the full truth table above.
- `IsSolved` is false while a generator still has queued output, even with no
  blocks on the board.
- `IsSolved` is false while an elevator still has pending waves.
- `CurrentColorOf` returns the next colour in the stack after a partial clear.
- `IsCellFree` treats a closed shutter region as occupied and an open one as
  free.
