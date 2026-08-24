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
    int? KeyId                          // null = carries no key
    int? KeyTargetLockId                // which lock this key opens
    KeyEffect KeyEffect
    int TimeBonusSeconds                // 0 = none
```

Most blocks have a `ColorStack` of length 1. Depth is the ordinary case, not a
variant type.

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

    bool IsInsideGrid(Coord c)
    bool IsStaticWall(Coord c)
    int? ShutterAt(Coord c)             // shutter id covering this cell, if any
```

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
  - `Cells` non-empty, no duplicates, connected
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
- Efficient lookup structures for `IsStaticWall` and `ShutterAt` — precomputed,
  not linear scans. These are called inside the search loop.
- `Coord` hashing and equality.

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
