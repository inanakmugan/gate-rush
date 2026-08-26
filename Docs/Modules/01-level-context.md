# Module 01 — `Coord`, `LevelContext` and definition types

**Assembly:** `GateRush.Core` (no engine references)
**Depends on:** nothing
**Phase:** 1.1

---

## Responsibility

Hold everything about a level that **never changes while it is played**. This
data is shared by reference across every state the solver visits and is
deliberately excluded from state hashing.

---

## Public surface

### `Coord`

```
readonly struct Coord
    int X { get; }
    int Y { get; }
    Coord(int x, int y)
    static Coord operator +(Coord a, Coord b)
    static Coord operator -(Coord a, Coord b)
    bool Equals(Coord other)
    int GetHashCode()
    string ToString()
```

Origin is bottom-left. `+X` is right, `+Y` is up.

### Enumerations

```
enum BlockColor      Red, Blue, Green, Yellow, Purple, Orange, Pink, Cyan
enum MovementAxis    Free, HorizontalOnly, VerticalOnly
enum BoardEdge       Top, Bottom, Left, Right
enum Direction       Up, Down, Left, Right
enum KeyEffect       UnlockMovement, ClearOuterColor
```

### `BlockDefinition`

```
sealed class BlockDefinition
    int Id
    IReadOnlyList<Coord> Cells          // relative to origin; never rotated
    IReadOnlyList<BlockColor> ColorStack // index 0 = outermost
    Coord StartOrigin
    MovementAxis Axis
    int? UnfreezeAtClearCount           // null = not frozen
    int? LockId                         // null = not locked
    int RequiredKeyCount                // meaningful only when LockId is set
    int? KeyTargetLockId                // which lock this key opens; null = carries no key
    KeyEffect KeyEffect
    int TimeBonusSeconds                // 0 = none
```

Most blocks have a `ColorStack` of length 1. Depth is the ordinary case, not a
variant type.

A block carries a key when `KeyTargetLockId` has a value — there is no
separate key identifier. Since `LockId` is unique per level (see below),
`KeyTargetLockId` alone identifies the pairing. A key id could not have served
as an identifier anyway: a lock may require more than one key, so multiple
blocks legitimately carry the same badge.

### `GateDefinition`

```
sealed class GateDefinition
    int Id
    BoardEdge Edge
    int Offset                          // position along the edge
    int Width
    BlockColor Color
    int? OpenAtClearCount               // null = open from the start
```

### `ShutterDefinition`

```
sealed class ShutterDefinition
    int Id
    Coord Min                           // inclusive
    Coord Max                           // inclusive
    int Threshold
    BlockColor? RequiredColor           // null = counts all clears
```

### `GeneratorDefinition`

```
sealed class GeneratorDefinition
    int Id
    BoardEdge Edge
    int Offset
    IReadOnlyList<SpawnedBlock> Queue   // ordered, explicit, never randomised
```

`SpawnedBlock` carries the same shape, colour stack, axis and modifier fields as
`BlockDefinition` but no `StartOrigin` — placement derives from the generator's
edge and offset.

### `ElevatorDefinition`

```
sealed class ElevatorDefinition
    int Id
    Coord Min
    Coord Max
    IReadOnlyList<IReadOnlyList<SpawnedBlock>> Waves   // ordered
```

### `BlockSpec` (added in Module 02)

```
readonly struct BlockSpec
    IReadOnlyList<Coord> Cells
    IReadOnlyList<BlockColor> ColorStack
    int? UnfreezeAtClearCount
    int? LockId
```

The fields `BlockDefinition` and `SpawnedBlock` share, unified so a caller that
only needs this data does not have to care which kind of definition backs a
given index. Built from either type by `LevelContext.SpecAt` — see below.

### `LevelContext`

```
sealed class LevelContext
    int LevelId
    int Width, Height
    IReadOnlyList<Coord> StaticWalls
    IReadOnlyList<BlockDefinition> Blocks
    IReadOnlyList<GateDefinition> Gates
    IReadOnlyList<ShutterDefinition> Shutters
    IReadOnlyList<GeneratorDefinition> Generators
    IReadOnlyList<ElevatorDefinition> Elevators
    int SuggestedTimeBudgetSeconds
    int GoldReward
    int TotalBlockCapacity              // size of the flat index SpecAt resolves

    bool IsInsideGrid(Coord c)
    bool IsStaticWall(Coord c)
    int? ShutterAt(Coord c)             // shutter id covering this cell, if any
    int? ShutterPositionAt(Coord c)     // shutter's 0-based position in Shutters, if any
    BlockSpec SpecAt(int blockIndex)    // O(1) across top-level blocks and every spawn slot
```

`TotalBlockCapacity`, `ShutterPositionAt`, and `SpecAt` were added in Module 02
(`BoardState`), which needed O(1) resolution from a flat block index — spanning
top-level blocks and every block any generator or elevator could ever spawn —
back to that block's spec, and from a cell to the array position of the
shutter covering it. Both are precomputed once in the constructor, alongside
`IsStaticWall`'s and `ShutterAt`'s existing lookups, rather than walked on
every call. See `DECISIONS.md` D28 for why this precomputation belongs here
rather than in an external, Module-02-owned cache, and why `BoardState`'s
per-state occupancy cache (also introduced there) is lazy where these are
eager.

---

## Design decisions (owner)

**`Coord` exists because `Core` may not reference `UnityEngine`.** `Vector2Int`
would break the `noEngineReferences` guarantee that makes the layer boundary a
compiler error rather than a habit.

**Definition types are classes, not structs.** Created once per level, never
copied in hot paths, so reference semantics are correct here. Only `Coord` and
per-state types are structs.

**Nullable value types are used freely in `Core`.** The `-1` sentinel rule from
`DECISIONS.md` D17 applies only to the DTO layer, which must satisfy
`JsonUtility`.

**Blocks carry their own restrictions rather than belonging to a type
hierarchy.** A block may simultaneously be frozen, locked, axis-restricted,
layered, and time-bearing. An enum of block types would combinatorially explode;
a flat set of optional fields does not.

**Spawn contents are explicit and ordered.** Any runtime randomness would void
the solver's guarantee.

---

## Left to you

- Validation in constructors, with messages that name the offending element:
  - grid dimensions positive
  - `Cells` non-empty, no duplicates, **orthogonally (4-directional) connected**.
    Diagonal-only contact is rejected: a diagonally joined shape has no
    well-defined projection span onto a board edge, which M1's gate-exit rule
    depends on.
  - `ColorStack` non-empty
  - **no two adjacent entries in `ColorStack` share a colour** (D26)
  - `StartOrigin` places the whole footprint inside the grid, clear of static
    walls
  - no two blocks overlapping at start
  - gate `Offset + Width` within its edge
  - shutter `Min <= Max`, region inside the grid
  - every `KeyTargetLockId` refers to an existing lock
  - every lock has at least `RequiredKeyCount` keys pointing at it
  - `RequiredKeyCount >= 1` whenever `LockId` is set
  - The lock/key cross-reference scans (the two bullets above) cover **every**
    block: top-level `Blocks`, plus every `SpawnedBlock` inside
    `GeneratorDefinition.Queue` and `ElevatorDefinition.Waves`. This is
    referential integrity only — confirming a key with the right count exists
    somewhere in level data. Whether a spawned key is actually *reachable* at
    the point its lock needs it is a solver and editor-warning concern, not a
    constructor check.
  - **`LockId` is unique across the whole level**, counting `SpawnedBlock`s in
    generator queues and elevator waves as well as top-level `Blocks`. A lock's
    identifier doubles as the badge colour shown to the player (see M8 in
    `MECHANICS.md`), so two locked blocks sharing an id would be unreadable —
    the player could not tell which key opens which.
  - **`Id` is unique within each of `Blocks`, `Gates`, `Shutters`,
    `Generators`, and `Elevators`.** These ids are looked up and reported in
    error messages elsewhere in the codebase; a duplicate silently shadows an
    earlier element.
  - **Shutter regions must not overlap.** Two shutters covering the same cell
    is an authoring error, not a case `ShutterAt` should resolve by picking one
    arbitrarily.
  - gate `Width >= 1`
  - `TimeBonusSeconds >= 0`
  - `UnfreezeAtClearCount >= 0` when set
  - shutter `Threshold >= 0`
  - When two blocks overlap at start, the error names **both** blocks, not just
    the second one placed.
  - Every entry in `StaticWalls` is inside the grid, and `StaticWalls` contains
    no duplicates.
- Efficient lookup structures for `IsStaticWall`, `ShutterAt`/`ShutterPositionAt`,
  and `SpecAt` — precomputed, not linear scans or per-call walks. These are
  called inside the search loop.
- `Coord` hashing and equality.
- `ElevatorDefinition` must defensively copy each inner wave list, not just the
  outer `Waves` list — otherwise a caller retains a mutable reference into an
  already-constructed, supposedly-immutable level.

---

## Tests

- Rejects a block whose footprint leaves the grid.
- Rejects two blocks overlapping at start.
- Rejects a `ColorStack` with two adjacent identical colours.
- Accepts a `ColorStack` where the same colour appears non-adjacently.
- Rejects a key targeting a non-existent lock.
- Rejects a lock requiring more keys than exist for it.
- `ShutterAt` returns the correct shutter for interior, edge, and outside cells.
- `Coord` equality and hashing behave consistently for equal values.
- Rejects a duplicate `LockId` shared by two top-level blocks.
- Rejects a duplicate `LockId` shared between a top-level block and a
  `SpawnedBlock` in a generator queue.
- Rejects a duplicate `Id` within `Blocks`, `Gates`, `Shutters`, `Generators`,
  and `Elevators` respectively.
- Rejects two shutter regions that overlap.
- Rejects a gate with `Width` less than 1.
- Rejects a negative `TimeBonusSeconds`, a negative `UnfreezeAtClearCount`, and
  a negative shutter `Threshold`.
- Mutating the caller's wave list after constructing an `ElevatorDefinition`
  does not change the stored `Waves`.
- The error thrown for two overlapping blocks at start names both block ids.
- A lock requiring more than one key is satisfied by two separate blocks that
  both set `KeyTargetLockId` to it (no key id needed to tell them apart).
- Rejects a static wall coordinate outside the grid.
- Rejects a duplicate static wall coordinate.
- `SpecAt` returns a top-level block's own cells and colour stack.
- `SpecAt` returns a generator's queued spec, and an elevator's wave spec,
  for indices beyond the top-level block range.
- `TotalBlockCapacity` counts top-level blocks plus every generator-queued and
  elevator-wave block.
- `ShutterPositionAt` returns a shutter's 0-based position in `Shutters`
  (distinct from `ShutterAt`'s id) for a covered cell, and null otherwise.
