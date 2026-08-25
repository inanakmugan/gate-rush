# Module 04 — `MoveGenerator`

**Assembly:** `GateRush.Solver`
**Depends on:** Modules 01, 02
**Phase:** 1.4

---

## Responsibility

Enumerate the moves available from a board state, in one of two modes.

---

## Public surface

```
enum MoveGenMode
    Canonical      // pruned; fast
    Exhaustive     // every reachable position

sealed class MoveGenerator
    IEnumerable<Move> Generate(LevelContext ctx, BoardState state,
                               MoveGenMode mode)
```

---

## Modes

### `Exhaustive`

For every movable block, run a flood fill from its current position over
single-cell orthogonal steps in the directions its axis permits, accepting only
positions where the whole footprint is legal. Emit every position found — plus
the **zero-distance move** whenever the block is already flush against a
compatible open gate.

One traversal per block yields the complete set. Do not scan direction by
direction.

### `Canonical`

The same flood fill, filtered to positions that can plausibly matter:

1. Positions where the block becomes **flush and aligned with a compatible
   gate** (right colour, sufficient width, full containment).
2. Positions that **vacate or occupy a generator's spawn cells**.
3. Positions that **vacate or occupy an elevator region**.
4. Positions where the block **rests against an obstacle** — a wall or another
   block — in at least one direction. This replaces the old "maximum slide"
   criterion, which is meaningless once a block can turn corners.
5. The **zero-distance move** when the block is already at a compatible gate.

---

## Design decisions (owner)

**Zero-distance moves are mandatory in both modes** (D25). Because levels start
tightly packed, the pre-aligned block usually has nowhere to slide, and the
zero-distance clear is the level's opening move. Omitting it would make most
levels appear unsolvable.

**Canonical pruning can only produce false negatives** (D5). Its move set is a
strict subset of the player's, so any solution it finds is one a human could
play, and it can never declare an unsolvable board solvable. That asymmetry is
what makes the pruning safe.

**Enumeration is a flood fill** (D27). Movement is reachability-based, so one
traversal per block produces the complete move set. This is cheaper than the old
per-direction scan, not more expensive.

**Why pruning is necessary at all.** A block can reach most of the free area in
one move, so branching far exceeds Rush Hour's 30–40 and grows as the board
empties. Unpruned breadth-first search does not survive that. Canonical mode is a
prerequisite for termination on realistic boards, not an optimisation.

**Enumeration order must be deterministic** in both modes: ascending block index,
then direction in `Direction` enum order, then ascending distance, with the
zero-distance move emitted first for its block. Reproducible tests depend on it.

**Only `CanMove` blocks are enumerated.** Jokers are not moves and never appear
in search output (D10).

---

## Left to you

- Path scanning for multi-cell and non-rectangular footprints.
- Computing the set of gate-aligned positions per block per direction.
- Deriving generator spawn cells and elevator regions for criteria 3 and 4.
- Avoiding duplicate emissions when one position satisfies several criteria.
- Allocation strategy — this runs once per expanded node.

---

## Tests

- Exhaustive mode emits one move per reachable position, including positions
  reached by turning a corner.
- No move is emitted for a diagonally adjacent cell whose orthogonal approaches
  are both blocked.
- Canonical mode emits a strict subset of exhaustive mode, over a corpus.
- **Every canonical move is present in the exhaustive set.** Property-style test.
- Canonical mode includes positions where the block rests against an obstacle.
- Canonical mode includes a gate-aligned position that rests against nothing.
- **A zero-distance move is emitted for a block flush against a compatible open
  gate, in both modes.**
- No zero-distance move is emitted when the block is not at a compatible gate.
- On a fully packed board where only one block is pre-aligned with its gate,
  exactly one move is generated.
- An axis-restricted block yields moves in two directions only.
- A frozen or locked block yields no moves.
- Enumeration order is stable across runs.
