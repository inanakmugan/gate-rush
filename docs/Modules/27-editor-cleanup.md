# Module 27 — Level Editor cleanup

**Assembly:** `GateRush.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 09, 24
**Phase:** Known debt (tooling, before Phase 4)

---

## Responsibility

Fix the suggested time budget so that time bonuses matter again (D54), and
clear the Level Editor's remaining known debt in one pass. Each item is
small; together they make the editor's numbers and warnings trustworthy.

In scope, one section each below:

1. The suggested time budget no longer includes time bonuses.
2. The "Empty cells / Fill" metric counts the blocks a level starts with.
3. A block with no lock never keeps a key count.
4. The solver line shows a search error as an error.
5. `DraftValidator`'s summary matches its checks.
6. The shutter-threshold warning gets its test.
7. The window's remaining canvas magic numbers move into settings.

Not in scope: the solver's own known debt, the presentation debt, any rule
change, re-tuning existing levels.

---

## Public surface

```
TimeBudgetFormula                   existing
    int Suggest(int solutionMoves)
        // base + perMove * moves, rounded up; bonuses are no longer a
        // parameter

DraftMetrics                        existing
    + int AvailableTimeBonusSeconds // shown beside the suggestion, not
                                    // added to it
    EmptyCellCount / FillRatio      // from the level's starting board

SolverLineKind                      existing
    + Error
```

Names are negotiable. That the suggested budget leaves bonuses out, and
that the fill metric reads the same starting board the game and the solver
use, is not.

---

## Design decisions (owner)

### 1. Time budget without bonuses (D54)

- The suggestion is `base + perMove * moves`, rounded up to the rounding
  step. Time bonuses are not added.
- The editor still shows the total of the level's time bonuses next to the
  suggestion, as information ("+N s in bonuses"), so the designer sees how
  much cushion the player will collect.
- Existing levels keep their saved budgets; nothing is re-saved by this
  change.

### 2. Fill metric from the starting board

- "Empty cells" and "Fill" count the cells occupied when the level starts,
  as the player first sees it: authored blocks and every block that spawns
  before the first move (a generator whose cells are empty at start, and
  whatever else the initial state spawns).
- They are computed from the initial `BoardState` the solver already
  builds for the draft, not from the draft's block list, so the metric
  cannot drift from the rules again.

### 3. No key count without a lock

- Clearing a block's lock id in the properties panel resets its required
  key count to the default; the field is shown only while a lock id is
  set, as today.
- A block or queue entry with no lock id saves the default key count,
  whatever the draft holds, so a stale value can never reach level data.
- Report whether any level file under `Assets/Resources/Levels` holds a
  non-default key count on a block with no lock; do not edit level files.

### 4. Search error on the solver line

- A solve that ends in an error shows its own state on the solver line,
  with its own colour in `LevelEditorSettings` and the error's message,
  instead of the "not run" colour.

### 5. Validator summary

- The XML summary of `DraftValidator` names the checks that read outside
  inputs as they are now. Check every count and list in its comments
  against the code.

### 6. Shutter-threshold test

- The warning for a shutter threshold higher than the clears the level can
  produce gets a test, beside the existing gate-threshold test.

### 7. Canvas magic numbers

- Literal colours, sizes and thicknesses left in the window's canvas
  drawing code move into `LevelEditorSettings` with `Problems()` checks,
  following Module 24. Geometry that is not a tunable (halves, quarter
  turns, whole pixels) stays in code as named constants.
- The properties panel's layout numbers are out of scope.

### Engine rules that carry over

- No magic numbers, no mutable statics; Edit Mode tests for every
  behaviour change.
- Existing settings keep their values; a new field's default equals the
  literal it replaces, so the editor looks the same.

---

## Left to you

- How the error state is carried from `LevelSolveRunner` to the solver
  line.
- Which literals count as canvas drawing; list them in the plan.
- The wording of the bonus note and the error line.

---

## Tests

**Time budget**
- The suggestion for a given length is the same with and without time
  bonuses in the draft (the central decision).
- It rises with solution length and rounds up to the step.
- `AvailableTimeBonusSeconds` sums every bonus in the draft: placed
  blocks, wave blocks and queue entries.

**Fill metric**
- A draft whose generator spawns before the first move counts the spawned
  block's cells as occupied.
- A draft with no spawns gives the same numbers as before.

**Key count**
- Clearing a lock id resets the key count to the default.
- Saving a lock-free block with a stale key count writes the default.

**Solver line**
- An error result maps to `SolverLineKind.Error`; no result maps to
  `NotRun`.

**Validator**
- A shutter threshold above the available clears is reported; one at or
  below is not.

**Settings**
- Each new settings field has a `Problems()` check, and the defaults
  report nothing beyond what they did before.

**By hand (owner)**
- Validate levels 9 and 14: the suggestion no longer includes their
  bonuses, and the bonus total shows beside it.
- Validate a level whose generator spawns before the first move: the fill
  metric counts that block.
- Remove a lock from a block: the key count resets.
- The canvas looks the same as before.
