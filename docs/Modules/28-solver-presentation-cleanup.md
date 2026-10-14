# Module 28 — Solver and presentation cleanup

**Assembly:** `GateRush.Solver`, `GateRush.Runtime`, `GateRush.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 05, 15, 16, 19, 27
**Phase:** Known debt (before Phase 4)

---

## Responsibility

Clear the last known debt before Phase 4: the solver's stale comments and
one slow search, a few test-suite blemishes, the card illustrations that
copy the board's drawing, and a time-bonus check that silently stops at
two digits. Nothing a player sees changes, and no rule changes.

In scope, one section each below:

1. Solver comments match the code.
2. `ExitCandidates` finds its cheapest route faster, with identical
   results.
3. Test-suite blemishes: duplicate floors, a stale message, a misplaced
   summary.
4. `MechanicIllustration` and `BoardView` share their drawing recipes.
5. A time bonus too wide for the mark check is caught.

Not in scope: new solver strategies, budget tuning, any visual change,
Phase 4.

---

## Public surface

```
Shared drawing recipes              Runtime (name and shape left to you)
    the block, lip, stud, mark and edge-feature recipes that
    BoardView and MechanicIllustration both draw today, in one place

RuntimeConfig                       existing
    WidestBonusSecondsChecked       readable by tests and the editor
                                    (internal or public, as fits)
```

Names are negotiable. That a recipe exists once, and that the board and
the cards look exactly as they do today, is not.

---

## Design decisions (owner)

### 1. Solver comments

- The XML comments of `SolveStatus`, `ISearchStrategy` and `SearchBudget`
  describe the code as it is. Check every statement, count and list in
  them against the code and the strategies that exist today.

### 2. `ExitCandidates`

- Find why its Dijkstra is slower than it needs to be (for example a
  linear scan for the next node, repeated allocation, or a search that
  keeps going after its answer is known) and fix that, keeping its results
  identical.
- The existing tests must pass unchanged. Add a test that compares the new
  route costs with a test-local copy of the old algorithm over the solver
  test corpus and finds them equal, or state in the plan why the existing
  tests already cover every path.
- State the cost before and after in the plan (what was linear or
  repeated, and what it becomes). Unity cannot be launched from the coding
  session, so no timing is required.
- No `PriorityQueue<,>` unless the project's API level has it; a small
  binary heap is fine.

### 3. Test-suite blemishes

- The two floors in `NearestNextClearStrategyTests` that now count the
  same boards are either made to count different boards, so each still
  guards something, or merged into one, with the reason in a comment.
- The joker test message that still says "effect clear" uses today's term.
- The `<summary>` in `SearchCorpus` that sits above the wrong board moves
  to its board, and every other summary there is checked against its
  board.

### 4. One drawing recipe

- Every recipe `MechanicIllustration` copies from `BoardView` (the block's
  face and lip, studs, layered inset, frozen look, marks, gates, shutters,
  generator machines, elevator doors, whatever the cards draw) moves into
  shared code that both call. A recipe drawn only by the board stays where
  it is.
- The cards and the board look exactly as before: same sprites, sizes,
  colours and sorting orders. This is a refactor, not a redesign.
- If one recipe cannot be shared without changing how either side looks,
  leave it, say why in the plan, and keep a comment on both copies saying
  they must change together.
- Remove the Presentation known-debt line about the mirroring only when
  nothing is left mirrored.

### 5. Time bonuses wider than the check

- `RuntimeConfig.Problems()` checks the time-bonus mark against bonuses up
  to `WidestBonusSecondsChecked` (99). Make sure no level can go past it
  unnoticed:
  - an Edit Mode test loads every level under `Assets/Resources/Levels`
    and asserts every time bonus (placed blocks, wave blocks, queue
    entries) is at most that value;
  - the Level Editor's validator warns about a block whose bonus exceeds
    it.

### Engine rules that carry over

- No magic numbers, no mutable statics; Edit Mode tests for every
  behaviour change.
- Existing assets keep their values; nothing needs re-saving.

---

## Left to you

- The structure of the shared recipes (static helpers, a small drawing
  class, extension methods) and where they live in Runtime.
- How the Dijkstra is sped up, within the rules above.
- Whether the two duplicate floors are merged or made distinct.

---

## Tests

**Solver**
- The existing solver and `ExitCandidates` tests pass unchanged.
- The new route-cost comparison (or the stated coverage argument) holds
  over the corpus.

**Presentation**
- The existing `BoardView`, `MechanicIllustration` and mark tests pass
  unchanged.

**Time bonuses**
- Every shipped level's bonuses fit `WidestBonusSecondsChecked`.
- A draft with a 100-second bonus gets the new validator warning; 99 does
  not.

**By hand (owner)**
- Play a few levels and open every mechanic card: board and cards look
  exactly as before.
- Validate a level in the editor with a bonus of 100: the warning shows.

---

## Resolved during implementation

- **Solver comments.** `SolveStatus` now says nearest-next-clear can
  report Unsolvable from one dead-end stratum on a clear-monotone level
  and Indeterminate on a level with a generator or elevator.
  `ISearchStrategy` names three strategies, not two, drops "A* arrives in
  phase 1.11", and lists the exceptions that exist. `SearchBudget` says
  MaxDepth is not a hard stop for BFS and A*, describes each limit per
  strategy, and describes Validate's order as it runs today.
- **`ExitCandidates`.** The next origin came from a scan of every cell,
  O(V) per settled node; it now comes from a reused array-backed binary
  heap ordered by cost, then origin index, with stale entries skipped and
  hopeless relaxations not pushed. The tie-break is the node the scan
  picked, so the settle order and the results are identical. An internal
  seam (`CandidateCount`, `RouteCost`) lets a test compare every
  candidate's unpruned cost with `LegacyExitCandidates`, a test-local copy
  of the old class, over every `SearchCorpus` stratum and seeded random
  boards, capped at 12 strata per board and 120 states per stratum. Its
  floors were reasoned, not measured.
- **Tests.** `IsClearMonotone` is exactly "no generators and no
  elevators", so the two floors that counted the same boards are merged,
  with a comment. The joker message names the rocket clear and the key
  block's own exit (D45). The axis board's summary moved to its board, the
  class summary names all three suites, and two "no key" bound
  justifications that stopped mattering with D45 were corrected.
- **Shared recipes.** `BoardRecipes<TGroup, TPiece>` holds every recipe
  the cards copied, in cells, and draws through `IRecipeSurface<TGroup,
  TPiece>` (group, quarters, sprite, sliced, tiled, label). `BoardView`
  implements it over its existing SpriteRenderer helpers;
  `MechanicIllustration` over Images in hierarchy order. Recipes return
  the parts the effects animate. Board-only drawing (floor, frame,
  outline, glow, preview, bursts) and the How to Play arrow stay where
  they were. No recipe is left mirrored.
- **Board unchanged.** Every object name and sorting order the board
  creates is the same as before; a script comparison of the old and new
  code found no name or order added or lost. Mark positions are now
  computed in cells and scaled once, so `OpenLock` converts the padlock
  position; positions may differ in the last float bit. On the cards, a
  block's face pieces sit under a Face group, the machine's body lip sits
  under a Lip group named Body, and a layered block's studs are scaled by
  `localScale`, all invisible.
- **Time bonuses.** `RuntimeConfig.WidestBonusSecondsChecked` is a public
  const. `DraftValidator` reports `TimeBonusBeyondMarkCheck` for placed
  blocks, wave blocks and queue entries above it. `ShippedLevelsTests`,
  the first test to read the real level files, loads them through
  `Resources.LoadAll` and `LevelSerializer.FromJson` as the game does and
  checks every slot of every level.
