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

**Array ownership is a hard contract, not a convention.** The internal
constructor that builds a state from explicit field arrays stores every array
by reference and computes the hash once, from their contents at that moment.
It never defensively copies. A caller that mutates an array afterwards
corrupts that state silently — contents and cached hash disagree, `Equals`
stops reflecting reality, and the visited set can no longer tell two
genuinely different states apart. This is what makes **structural sharing**
between predecessor and successor states legal and deliberate: two states may
validly hold the very same array instance for a field that did not change
between them, which is exactly the technique the move resolver (Module 03)
will want when producing successors — build a new array only for what
changed, and hand the rest through unchanged. The one rule that makes this
safe: every array passed to the constructor is thereafter treated as
consumed, never mutated in place.

---

## Index scheme for spawned blocks (resolved during implementation)

Per-block arrays (`Origins`, `ClearedColors`, `Alive`, `Unfrozen`, `Unlocked`,
`KeyConsumed`) have a **fixed length**, `LevelContext.TotalBlockCapacity` — the
level's total block capacity — top-level blocks plus every block any generator
or elevator could ever spawn.

Index `i` resolves to its `BlockSpec` via `LevelContext.SpecAt(i)`, which is a pure
function of the immutable `LevelContext`: no new dynamic field is needed to
record which generator/elevator entry produced a given slot. That resolution
was first written as a private, per-call method inside `BoardState`, then
moved onto `LevelContext` itself (alongside its other precomputed lookups,
`IsStaticWall` and `ShutterAt`) once it became clear the module generating
moves (Module 04) would call it enough times per state, across enough states,
that a per-call walk over `Generators`/`Elevators` was not acceptable — see
`DECISIONS.md` D28 for the full reasoning, including the rejected alternative
of keeping this cache in `BoardState`/an external table instead.

A not-yet-spawned slot starts with `Alive = false` and stays inert until the
module that performs spawning (Phase 1.13) flips it, at that same fixed index;
arrays never resize. Read this way, "indices grow during play" (as stated
above) means the set of *active* (`Alive == true`) indices grows, not that
array length changes — which also means `Alive == false` is not by itself
proof a block was destroyed; see `IsSolved` below.

**`Origins` for a not-yet-spawned slot** is `BoardState.UnspawnedOrigin`, a
coordinate that is never inside any grid (`Coord(-1, -1)`), rather than a
placeholder that looks like real data. A generator's eventual spawn position
depends on projecting its edge and offset through the spawned block's shape —
an algorithm this module does not own (see Module 03's "Left to you"). An
elevator's per-block placement within its region has no representation at all
in today's `LevelContext`: `SpawnedBlock` carries no position. Rather than
guess, a not-yet-spawned slot gets a sentinel that fails loudly if read
without checking `Alive` first — it won't satisfy `LevelContext.IsInsideGrid`,
and `IsCellFree` will never treat it as free.

This gap is tracked, not overlooked: Phase 1.13 adds a region-relative
position field to `SpawnedBlock` for elevator waves, alongside validation that
each wave tiles its region exactly (see M9 in `MECHANICS.md` and the phase
1.13 row in `ROADMAP.md`). The generator half is resolved by Module 03, which
owns the edge-and-offset projection. Until both land, an unspawned slot has no
meaningful position and the sentinel is the honest representation.

**`ElevatorWaveActive`.** `ElevatorWaveIndex[e]` is the count of waves already
placed for elevator `e`. `ElevatorWaveActive[e]` is true while the most
recently placed wave still occupies its region, false once that region has
read empty again. This is technically re-derivable from `Alive` plus cell
geometry rather than history that must be stored — unlike the counters this
module deliberately keeps un-derived — but it is kept as an explicit field
anyway, since a `bool` costs nothing to hash and it may simplify the module
that maintains it. **Module 03 must conform to this exact meaning.**

---

## Occupancy map (added during implementation)

`IsCellFree` originally scanned every living block's footprint on every call
to answer "what's at this cell." Once it became clear Module 04's flood fill
would call it hundreds of times per state across millions of states, that scan
was replaced with a per-state `int[]` — cell to living block index, or none —
built the first time a state needs it and cached for the rest of that
instance's life.

This cache is **lazy**, unlike `BoardState`'s hash, which is eager. The
difference is deliberate: every state the search constructs is hashed at least
once (that is the point of the visited set), but most states are discarded as
duplicates by that same hash/equality check before move generation ever runs
on them. An eager occupancy map would charge every discarded state for a scan
whose result is never read. See `DECISIONS.md` D28.

---

## Left to you

- The FNV-1a implementation and the exact field ordering (document it).
- The array-copy strategy used when producing a successor.
- `IsCellFree`: bounds, static walls, closed shutter regions, and living block
  footprints, with `ignoreBlockIndex` excluded so a block does not collide with
  itself while moving. Backed by the lazy occupancy map described above.
- `ClearCountByColor` is sized to the whole `BlockColor` enum (8 entries), so
  every state hashes and compares eight integers regardless of how many
  colours a given level actually uses. Not changed now — there is nothing to
  profile yet — but worth revisiting once Module 05 (search) gives real
  numbers to measure against, since most levels will use only a handful of
  the eight.

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
