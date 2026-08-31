# Module 09 — Level Editor

**Assembly:** `GateRush.Editor` (editor-only)
**Depends on:** `GateRush.Core`, `GateRush.Serialization`, `GateRush.Solver`
**Phase:** 1.10

---

## Responsibility

The tool levels are authored in. A single `EditorWindow` that draws a board,
edits every mechanic, reports what is wrong with a level while you build it, and
proves the level solvable on demand.

This is the module that makes the solver useful. Everything before it built the
ability to answer *is this level solvable*; this is where somebody asks.

### Also in scope: two `Core` changes the editor forces

**`SpawnedBlock` gains a region-relative position.** Deferred since Module 02
(recorded in its spec, in M9, and in the roadmap). An elevator wave has to say
where each of its blocks sits, because a region usually admits several tilings.
Generator output still needs none — its position derives from edge and offset —
so the field is optional and meaningful only for elevator waves.

**Elevator waves must tile their region exactly.** Every cell covered once, no
gaps, no overlaps (M9). Validated in `ElevatorDefinition`'s constructor, which
already knows the region.

`formatVersion` goes to **2**. No migration path is needed: no levels have been
authored yet, so version 1 is simply refused.

### Not in scope

Generators and elevators are *authored* here but do not yet *run* — spawning at
runtime is phase 1.13's `CheckSpawnTriggers`. A level with a generator can be
built, saved, and will read as unsolvable until then. That is expected.

---

## The central decision: a mutable draft

`LevelContext` is immutable and validates on construction. A level being edited
is almost always invalid — you place a block before you place its gate, you
resize a grid before moving what fell outside. An editor cannot hold its working
state in a type that refuses to exist unless correct.

So the editor owns a **`LevelDraft`**: a mutable mirror of the level, using real
types (`int?`, enums, `List<T>`) rather than the DTO layer's sentinels and enum
names. It holds anything, valid or not.

Three conversions, each with a different purpose:

```
JSON  ⇄  LevelDto  ⇄  LevelDraft  →  LevelContext
       (structural)  (editing)      (validation + solving)
```

- **Load:** JSON → `LevelDto` → draft. Stops at the DTO, so a level that is
  structurally readable but semantically broken can still be opened and fixed.
  Loading through `LevelContext` would make a broken file unopenable in the one
  tool that could repair it.
- **Save:** draft → `LevelDto` → JSON. Warnings do not block saving; a
  half-built level is a normal thing to save.
- **Validate / solve:** draft → `LevelContext`. This is where `Core`'s rules
  apply, and a failure here is shown as an error rather than thrown at the user.

**A small change to Module 08 follows:** `LevelSerializer` must expose a stage
that stops at the DTO.

```
static LevelDto ParseDto(string json, string sourceName = null)
static string ToJson(LevelDto dto)
```

The existing `FromJson` becomes `ParseDto` followed by the DTO→Core conversion,
so structural checking is not duplicated.

---

## Public surface

```
GateRush.Editor
    LevelEditorWindow : EditorWindow      the window; owns layout and input
    LevelDraft                            mutable level under edit
    DraftValidator                        the live warnings
    DraftMetrics                          the numbers the panel shows
    LevelSolveRunner                      the two-stage solver invocation
```

Everything except `LevelEditorWindow` is plain logic with no `UnityEditor`
dependency in its signatures, so it can be tested. The window draws and routes
input; it decides nothing.

---

## The window

```
┌─────────────────────────────────────────────────────────────┐
│ New  Open ▾  Save  Save As          Level 42 ▸ Elevator 1 ▸ Wave 2 │
├─────────────────────────────────────────────────────────────┤
│ Select │ Block │ Gate │ Shutter │ Wall │ Generator │ Elevator │
│ ▫ ▬ ▮ ▬▬ ■ ⌐ ⌐ ⌐ ⌐  Free                                    │
├──────────────────────────────────┬──────────────────────────┤
│                                  │  Properties              │
│         the grid                 │                          │
│                                  │  (of the selected thing) │
│                                  │                          │
├──────────────────────────────────┴──────────────────────────┤
│ Warnings (2)                                                │
│  • Block 4 has a green layer but no green gate exists       │
│  • Lock 2 requires 2 keys but only 1 targets it             │
│                                                             │
│ Empty cells 3 · Opening branching 4 · Opening move ✓         │
│ Solver: Solvable in 12 moves (canonical, 3,410 states, 84ms)│
│ Suggested time budget: 55s                    [ Validate ]  │
└─────────────────────────────────────────────────────────────┘
```

### Tools

**Select** is the default, because most editing is changing something that
already exists rather than adding. Clicking a cell selects whatever occupies it
and opens its properties.

The other tools place their kind of thing at the clicked cell and select it
immediately, so placing and configuring are one gesture.

**Wall** toggles a cell between playable and walled. This is how a non-rectangular
board is drawn — a level shaped like an hourglass is a rectangular grid with its
narrow waist walled off. There is no separate "board shape" concept: shape is a
wall pattern.

### The shape palette

Active with the **Block** tool. Presets — 1×1, 1×2 and 1×3 in both orientations,
2×2, the four rotations of an L — plus **Free**, which adds and removes
individual cells.

Presets and free drawing produce the same thing: a set of cells. A preset is only
"fill these cells in one click". Most blocks are presets; free drawing exists for
the rest.

### Scope: elevator waves

Selecting an elevator shows its wave list in the properties panel:

```
Elevator 1 — region (0,0) to (3,3)

  Waves
  ▸ Wave 1    16/16 cells  ✓
  ▸ Wave 2    14/16 cells  ⚠ 2 uncovered
  ▸ Wave 3    16/16 cells  ✓
  [+ Add wave]
```

Opening a wave **replaces the main grid with that wave's grid** — region-sized,
same tools, same shape palette. The breadcrumb shows where you are and returns
you.

Not a second window. Two windows raise questions with no interesting answers —
which is active, what happens on close, what if there are unsaved changes — and
scope switching inside one window is the familiar pattern.

Generators need no scope. Their queue is an ordered list in the properties panel,
because generator output has no position to author.

---

## Design decisions (owner)

**Warnings are live; the solver is on demand.** Everything the validator reports
is cheap — scans over the draft, no search — so it recomputes on every edit and
is always current. The solver runs only when asked, because it can take seconds.

The two are reported separately and both are visible at once. A level can be
`Unsolvable` while warnings explain exactly why, and a level can have warnings
while still being solvable.

When the solver says `Unsolvable` and there are no warnings, the editor says so
and stops. Explaining *why* a board is geometrically deadlocked is a research
problem, not a feature; the honest answer is that the designer has to look at the
board.

**Two-stage solving.** Canonical first with a 5s / 200k budget; if that finds
nothing, exhaustive with 15s / 1M. Only if both come back empty is the verdict
`Indeterminate`. This is the fallback D5 describes, and it belongs here rather
than in the strategy — the strategy honours a budget, the caller decides the
policy. Both budgets are editable in the window.

**No undo.** Level editing is free-form and reversible by hand: delete the block,
place it again. An undo stack would be a substantial piece of the module for a
tool used by one person on small boards.

The rule this replaces it with: **no irreversible edit without confirmation.**
Anything that discards work asks first.

**Destructive edits confirm.** Shrinking a grid can push blocks, gates and
shutters outside it. Rather than silently dropping them or refusing the resize,
say what will be lost:

> Shrinking to 5×7 removes 3 blocks, 1 gate and 1 shutter. Continue?

The same for closing or switching levels with unsaved changes, matching Unity's
own scene behaviour.

**New levels are empty.** A grid of a size the user picks, nothing in it. A
starter template would encode today's taste and be wrong within a month.

**The window decides nothing.** `LevelDraft`, `DraftValidator`, `DraftMetrics`
and `LevelSolveRunner` hold the logic and are testable; `LevelEditorWindow` draws
them and routes clicks. An editor whose rules live in `OnGUI` cannot be tested at
all.

---

## Warnings

Everything `MECHANICS.md` lists, plus what the board shape adds:

- No legal move at level start.
- A colour in some block's stack has no compatible gate anywhere in the level.
- A compatible gate exists but is too narrow for that block's projection.
- An axis-restricted block has no compatible gate at either end of its axis.
- A gate or shutter threshold exceeds the total number of clears available.
- A lock has fewer matching keys than it requires.
- **A gate opens onto a walled cell** and can therefore never be used.
- **An elevator wave does not tile its region exactly** — n cells uncovered, or
  blocks overlapping.
- No block starts flush against a matching open gate (D16's ready opening move).

Warnings never block saving. They are what a designer reads while building.

Note what is *not* a warning: whether a particular block can physically reach a
particular gate through a narrow corridor. That needs reachability analysis, and
it is exactly the question the solver answers.

## Metrics

- Empty cell count and fill ratio (D16's tight packing).
- Branching factor at the opening position.
- Whether a ready opening move exists.
- Solver's explored-state count and largest stratum.
- **Suggested time budget**, derived from the solution length plus available M10
  bonuses. This is what turns a feel-based number into a measured one (D12).

---

## Left to you

- Grid drawing and hit-testing in `OnGUI`, and how the same code serves both the
  main grid and a wave's grid.
- How the properties panel is built per selection type without becoming one long
  `switch`.
- Where `LevelDraft` ↔ `LevelDto` conversion lives, and how it stays honest as
  the schema grows — a field added to one and not the other is the failure mode
  to design against.
- Whether the `Resources/Levels` dropdown watches the folder or refreshes on
  demand.
- How a wave's tiling status is computed cheaply enough to show live.
- **The solve runs synchronously on the UI thread.** A level that hits both
  budgets freezes the editor for up to their sum (default 5s + 15s). A progress
  bar is raised for each stage so it reads as working rather than hung, but it
  cannot show real progress — the search is opaque. Acceptable because most
  levels finish in milliseconds and both budgets are editable in
  `LevelEditorSettings`. Making the search step-able (so the window can drive it
  from `EditorApplication.update` and stay responsive) is real work and should
  be a deliberate decision, not a surprise six months from now.

---

## Tests

The window is not unit tested; everything it calls is.

**`SpawnedBlock.RegionOrigin` and exact tiling**
- A wave whose blocks tile the region exactly is accepted.
- A wave leaving a cell uncovered is rejected, naming the elevator, the wave and
  the count.
- A wave with two blocks overlapping is rejected.
- A wave with a block extending outside the region is rejected.
- Generator queues need no position and are unaffected.

**Serialization**
- `formatVersion` 2 round-trips; version 1 is refused.
- `RegionOrigin` round-trips for elevator waves and is absent for generator
  output.
- `ParseDto` returns a DTO for JSON that is structurally sound but semantically
  invalid — the case that lets a broken level be opened and repaired.

**`LevelDraft`**
- Draft → DTO → draft is lossless, over a level exercising every field.
- Draft → `LevelContext` produces the right context for a valid draft.
- Draft → `LevelContext` surfaces `Core`'s error, unchanged, for an invalid one.
- A draft can hold states `LevelContext` would reject — a block outside the grid,
  a key pointing at no lock — without throwing.

**`DraftValidator`** — one test per warning, each asserting the warning fires
when it should and, importantly, does not fire when it should not. A validator
that warns about everything is as useless as one that warns about nothing.

**`DraftMetrics`**
- Empty cell count and fill ratio on a known board.
- Opening branching factor matches `MoveGenerator`'s output for that board.
- Ready-opening-move detection, positive and negative.
- Suggested time budget rises with solution length and with available bonuses.

**Grid resize**
- Growing a grid changes nothing else.
- Shrinking reports exactly what falls outside, before removing anything.
- Confirming removes exactly that set and nothing more.

**`LevelSolveRunner`**
- A level canonical mode solves is not run through exhaustive.
- A level canonical mode misses is retried exhaustively and solved.
- A level neither mode solves within budget reports `Indeterminate`, not
  `Unsolvable`.
- A genuinely unsolvable level reports `Unsolvable` from the first stage.
