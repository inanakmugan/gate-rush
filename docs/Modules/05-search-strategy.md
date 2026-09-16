# Module 05 — `ISearchStrategy` and breadth-first search

**Assembly:** `GateRush.Solver` (editor and tests only)
**Depends on:** Modules 01–04
**Phase:** 1.5

---

## Responsibility

Decide whether a level is solvable and, if so, return a shortest solution.

---

## Public surface

```
enum SolveStatus
    Solvable
    Unsolvable
    Indeterminate      // budget exhausted; NOT a proof of unsolvability

sealed class SearchBudget
    int MaxDepth
    int MaxExploredStates
    long MaxWallClockMs
    MoveGenMode Mode

sealed class SolveResult
    SolveStatus Status
    IReadOnlyList<Move> Solution       // empty unless Solvable
    int ExploredStateCount
    int PeakFrontierSize
    long ElapsedMs

interface ISearchStrategy
    SolveResult Search(LevelContext ctx, BoardState initial,
                       SearchBudget budget)

sealed class BreadthFirstStrategy : ISearchStrategy
```

---

## Design decisions (owner)

**Three-valued status is mandatory** (D4). Reporting a budget timeout as
`Unsolvable` would tell a designer that a correct level is broken. The editor
renders the three outcomes in three colours.

**Breadth-first, not depth-first.** Difficulty is measured in moves, so the
solver must return the shortest solution. Depth-first would return *a* solution
and corrupt both the difficulty score and the derived time budget.

**Exploit progress monotonicity** (D6). The vector

```
(TotalClearCount, GeneratorIndex[], ElevatorWaveIndex[])
```

never decreases. Stratify the search by it:

1. Explore the current stratum fully — all states sharing the same progress
   vector — using a visited set scoped to that stratum.
2. Collect successors that advance the vector into the next stratum's frontier.
3. On advancing, **release the previous stratum's visited set entirely.** Those
   states are unreachable from here.

Peak memory becomes the largest single stratum rather than the whole space. A
plain global visited set is acceptable for the first working version; add
stratification once correctness is established and confirm move counts are
unchanged.

**Reconstruct the solution by back-pointers,** not by storing a path per node.
Storing a full path per state multiplies memory by solution length.

**Check the budget on every expansion,** not once per level of the search. A
single stratum can exceed the wall-clock budget on its own.

**Two-stage use is the caller's responsibility.** The editor runs `Canonical`
first and falls back to `Exhaustive` with a larger budget only when the first
pass finds nothing. The strategy itself does not decide this.

---

## Left to you

- Queue and back-pointer structures.
- Visited-set lifetime under stratification.
- Budget checkpoints and which limit is reported when several are near.
- Whether `PeakFrontierSize` measures the current stratum or the global frontier
  — document the choice, the editor displays it.
- Cheaper stratum retirement. The visited set can only shrink when the minimum
  queued progress vector rises; the current implementation re-scans for that on
  every expansion. Flagging the moment a vector's queued count hits zero and only
  retiring then would skip most of those scans. Left for a profiling pass —
  correctness does not depend on it.
- The per-vector queued counts (`queuedByVector`) are balanced by hand: one
  `Increment` per enqueue, one `Decrement` per dequeue, and `Decrement` throws on
  a missing key. A future `continue` that skips its `Decrement` would let
  `RetireStrata` retire a stratum early. That does not corrupt the answer — the
  search re-expands the states it wrongly forgot and still returns the shortest
  solution — but it is slower, and no test catches it. If the enqueue/dequeue
  sites grow, fold the count maintenance into the queue wrapper so it cannot
  drift.

---

## Tests

- A trivially solvable board returns `Solvable` with the known minimal move
  count.
- A fully packed board with one pre-aligned block is solved starting from a
  zero-distance move.
- A board with a block whose colour has no matching gate returns `Unsolvable`.
- A board where the only exit is permanently obstructed by a parked block returns
  `Unsolvable`.
- A layered block whose second colour has no matching gate returns `Unsolvable`.
- A deliberately oversized board with a tiny budget returns `Indeterminate`,
  **not** `Unsolvable`.
- Every move in a returned solution replays successfully through `MoveResolver`,
  and the final state satisfies `IsSolved`.
- The returned solution length equals the shortest length for a corpus of boards
  with hand-verified optima.
- Canonical and exhaustive modes return the same move count on boards where both
  succeed.
- Stratified and non-stratified variants return identical move counts across the
  corpus. **This is the regression test that protects the memory optimisation.**
- Results are reproducible: two runs on the same input produce the same solution.

---

## Implemented: `AStarStrategy` (phase 1.11)

Behind the same `ISearchStrategy` interface as `BreadthFirstStrategy`,
sharing `SearchBudget`/`SolveResult` and the same budget-checkpoint cadence
(`SearchBudget.WallClockPollInterval`, moved here from
`BreadthFirstStrategy` so both strategies share it).

**Open set.** A hand-written binary min-heap (`.NET Standard 2.1` has no
built-in `PriorityQueue<T>`). Tie-break order is a total order so heap
layout never affects which node pops first: lower `f = g + h`, then higher
`g` (so a goal state — `h = 0` — pops first among equal-`f` ties), then
lower insertion sequence number.

**Goal test on pop, not on generation** — required because A\*, unlike BFS,
does not expand in depth order.

**Duplicate states.** A `Dictionary<BoardState, Node>` tracks the best
known `g` per state. A strictly shorter path to an already-open state
replaces the record and marks the old entry superseded; superseded heap
entries are skipped on pop rather than removed from the heap (lazy
deletion), and are not counted as expansions.

**No reopening.** With a consistent heuristic, a closed (already-expanded)
node should never need reopening. `Search` throws
`InvalidOperationException` if a shorter path to a closed state is found
anyway — that would mean the heuristic is not actually consistent, which is
a bug to fix, not a case to handle at runtime.

**No stratification**, unlike BFS. A\* interleaves progress strata by
ordering on `f`, so BFS's stratum-retirement optimisation does not apply;
A\*'s memory is bounded by the states it actually touches, already a subset
of what BFS touches on the same board.

**Heuristic:** see D3 for `h = C - F` and why "at least one unconsumed
`ClearOuterColor` key" replaces the plain colour-remaining count.

**Shared corpus.** `BreadthFirstStrategyTests` and `AStarStrategyTests` run
against the same `SearchCorpus`, including a board built specifically to
distinguish "at least one" from "every" in the `F` count (a lock needing
two keys, one `ClearOuterColor` and one `UnlockMovement`; optimum 2).

### `AStarStrategy` tests (in addition to the outcome/budget/reproducibility
tests mirrored from `BreadthFirstStrategyTests`)

- Matches `BreadthFirstStrategy` on status and move count across the whole
  corpus — the proof the heuristic is sound enough to ship.
- Expands strictly fewer states than `BreadthFirstStrategy` on an open board
  with many pointless destinations.
- The heuristic never exceeds the hand-verified optimum, for every corpus
  board (admissibility).
- The heuristic never drops by more than one per move, and is exactly zero
  on a solved state, over every reachable state of every corpus board
  (consistency — checked over the full state graph, not just the start).
- The mixed-key-effect lock board's heuristic is exactly 2, not 3 — the
  regression case for the `F` formula.
