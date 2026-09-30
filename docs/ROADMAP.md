# Roadmap

Ordered so the highest-risk, highest-value work happens first and platform
surprises surface while the project is still small.

---

## Phase 0 — Project setup

Unity 6000.3.22f1, 2D (URP) template, Linear colour space, Input System,
DOTween with its own asmdef, Physics 2D simulation mode set to Script, Force
Text serialisation, visible meta files, single quality level, URP asset tuned
(HDR and post-processing on; MSAA, depth, opaque, 3D lights off).

Folder skeleton and assembly definitions created **before** any code, with
`noEngineReferences` on `Core`, `Solver`, `Meta`, and `Platform`, and
`GateRush.Solver` restricted to the Editor platform. Enabling those flags later
means cleaning up every leak that accumulated in the meantime.

---

## Phase 1 — Puzzle core

*Status: done.*

Grid, blocks, gates, resolution, search, tests, editor. No Unity scene involved
except the editor window.

| # | Module | Spec | Status |
|---|---|---|---|
| 1.1 | `Coord`, `LevelContext`, definitions | `Modules/01-level-context.md` | Done |
| 1.2 | `BoardState` | `Modules/02-board-state.md` | Done |
| 1.3 | `MoveResolver` (fixpoint skeleton, M1 + M7 only) | `Modules/03-move-resolver.md` | Done |
| 1.4 | `MoveGenerator` | `Modules/04-move-generator.md` | Done |
| 1.5 | `ISearchStrategy` + BFS | `Modules/05-search-strategy.md` | Done |
| 1.6 | Test corpus — hand-built 3×3 to 5×5 boards | — | Done |
| 1.7 | Condition system + M2, M3, M10 (and M5's threshold half) | `Modules/06-conditions.md` | Done |
| 1.8 | M8 locks and keys | `Modules/07-locks-and-keys.md` | Done |
| 1.9 | Serialization (JSON DTOs) | `Modules/08-serialization.md` | Done |
| 1.10 | Level Editor — plus SpawnedBlock.RegionOrigin and exact-tiling validation | `Modules/09-level-editor.md` | Done |
| 1.11 | A\* + equivalence tests against BFS | `Modules/05-search-strategy.md` | Done |
| 1.12 | M4 layered blocks — landed with Module 03; editing colour stacks is part of 1.10 | — | Done |
| 1.13 | M6, M9 at runtime — CheckSpawnTriggers. Authoring lands in 1.10 | `Modules/10-spawning.md` | Done |

Gate-compatibility rules (projection span, alignment, orientation) are settled in
1.1–1.3 and carry the heaviest test load. Subtle bugs concentrate there.

**Next after 1.10 — done (D34).** A generator gains a `Width`, mirroring the
gate it is the inverse of (M6): the marker draws at that width, and a queue
block whose projection exceeds it is a warning, the mirror of "a compatible gate
exists but is too narrow". `SpawnedBlockDraft` gains a stable `Id` in the same change —
both touch the level DTO, and no level has been authored yet, so the format can
still change for free. Spec written when reached.

**Added along the way — solver robustness (D35–D41).** Authoring the first real
levels showed the solver could not settle some simple boards. That produced
symmetry reduction (D35), solve results that separate existence from proven
length (D36), the nearest-next-clear strategy (D37), the cancellable Validate
pipeline (D38), and the fixes and rules in D39–D41.

---

## Phase 2 — Playable single level

*Status: playable; visual polish (2.4) in progress.* Levels chain through Next
rather than one hardcoded level.

Board rendering, pointer input, DOTween movement, countdown, win/lose. No
menus, one hardcoded level.

Split into three steps so a design or data-model problem surfaces before
Runtime code exists, and a feel problem surfaces before animation and UI
polish exist:

**2.0 — Author and solver-validate the hardcoded level(s).** Done entirely
in the Level Editor (Save + Validate, already built in 1.10) — no Runtime
code touched. Every board tested so far has been a small hand-built
synthetic corpus; this is the first time the editor, the solver, and
serialization run against a real, non-trivial level. Cheaper to find an
editor or data-model gap here than after 2.1 depends on the level format.

*Status: done.* level-0 to level-5 are authored, validate, and carry their own
id, a placeholder gold reward and a solver-suggested time budget, all set in
the editor. level-3 is for now a copy of level-2. level-1 starts with a ready
opening move; the others need not, which D16 allows: it is a guideline, not a
rule.

**2.1 — Static skeleton.** Render the board, wire pointer input straight to
`MoveResolver`, apply moves instantly — no animation, no countdown, no
win/lose UI. Proves the simulation and input are wired correctly before any
presentation exists. This is the first point the game is actually
playable by hand.

*Spec: `Modules/11-static-skeleton.md`. Status: done.* A level loads from a
JSON asset, draws with placeholder visuals, and plays by hand: the block
follows the finger cell by cell, a push into the facing gate clears in place
(D43), R restarts, and a solved level is logged.

**2.2 — Polish.** DOTween movement, countdown, win/lose UI, layered on top
of 2.1 once the core loop is confirmed correct.

*Spec: `Modules/12-polish.md`. Status: done.* Blocks glide cell by cell,
a cleared block leaves through its gate, the level runs against a countdown
with M10 bonuses, and a result panel offers Restart and Next.

**2.3 — Free drag.** Mentor feedback on the gameplay video: blocks feel
magnetised to the grid. In the reference game a dragged block floats under
the finger and settles into a cell only on release. Presentation and input
only; the rules, the solver and gate behaviour are unchanged (D44).

*Spec: `Modules/13-free-drag.md`. Status: done.* A dragged block floats
under the finger, stops flush against obstacles, slides into corridors
through a corner assist, and settles into the nearest cell on release.

**2.4 — Visual polish before the first shared build (D46).** The first
build goes to a mentor, so the prototype look is replaced first, in three
steps, each its own spec and PR:

- **2.4a — Board and blocks.** Background, frame and walls, studded
  one-piece blocks, arrowed gates, the board filling the screen.
  *Spec: `Modules/15-board-and-blocks.md`. Status: done.*
- **2.4b — State visuals.** Frozen blocks and gates as ice with a count,
  locks with chains and a padlock, keys, shutters, generators as machines
  showing their next block, elevators as lift doors. Keys and locks pair
  by the locked block's colour, one locked block per colour in a level
  (D47, D48). *Spec: `Modules/16-state-visuals.md`. Status: done.*
- **2.4c — HUD and feedback**, in two PRs:
  - *Part 1 — HUD and result panel.* Timer in minutes and seconds, level
    number, restart button, the Lilita One font, a styled result panel.
    *Spec: `Modules/17-hud.md`. Status: done.*
  - *Part 2 — feedback animations.* Exits breaking into cubes, the grabbed
    block lifting with a white outline, ice breaking, shutters lifting,
    spawns from generators, elevator doors opening as a wave rises, and the
    "+N s" time bonus. The key flying to its lock and chains falling are
    left for later; a lock simply updates its count and opens.
    *Spec: `Modules/18-feedback-animations.md`. Status: next.*

**Cleanup after 2.2 — remove the `ClearOuterColor` key effect.** Keys only
unlock movement: a lock stops a block from moving and nothing else, and no
key ever clears a colour. Nothing in this game clears a block in place
except jokers (Phase 5). `ClearOuterColor` is therefore removed from
`KeyEffect`, `MoveResolver`, the A\* heuristic, serialization, the editor and
their tests, and from M8, D41 and D42. No authored level uses it.

*Spec: `Modules/14-keys-only-unlock.md` (D45). Status: done.* `KeyEffect`
is gone, the waiting effect with it, and the level format is version 4.

Watch the zero-distance move here: a drag with a determined direction but no
displacement must still clear a block at a gate. This is the first move of
most levels and is easy to lose in input handling.

**Known debt.** Small items found along the way, none blocking; each is
taken when its area is next touched.

- *Level Editor:* a frozen block does not look frozen on the editor grid;
  the "Empty cells / Fill" metric ignores blocks spawned at level start;
  magic numbers remain in the window's drawing code; a lock-free block
  keeps a stale `RequiredKeyCount`; the solver line shows the "not run"
  colour after a search error; `DraftValidator`'s summary says two checks
  read outside inputs where there are now three; the shutter-threshold
  warning has no test.
- *Solver:* `SolveStatus`, `ISearchStrategy` and `SearchBudget` XML
  comments are out of date; `ExitCandidates`' Dijkstra is slower than it
  needs to be; two floors in `NearestNextClearStrategyTests` now count the
  same boards; a joker test's message still says "effect clear"; a
  `<summary>` in `SearchCorpus` sits above the wrong board.
- *Presentation:* on a 1×1 layered locked block the layer numeral overlaps
  the padlock.

---

## Phase 3 — First WebGL build

Deliberately early. Build, open in a browser, confirm it runs.

Checks: code stripping did not remove anything reflective; portrait framing works
in a landscape page; pointer input behaves as in the editor; `PlayerPrefs`
persistence survives a reload; build size and load time are acceptable; draw
calls and the per-move board rebuild are profiled on a phone (Module 15).

Finding a stripping problem here costs an afternoon. Finding it in Phase 7 costs
a week.

---

## Phase 4 — Meta core

Wallet, lives, streak, inventory, progression, save model, configuration assets.
Pure C#, fully tested, no UI. Verify "win three, lose one, wait forty minutes"
against a fake clock.

---

## Phase 5 — Jokers

Where Phase 1 and Phase 4 meet. Clock, rocket, broom wired into the existing
resolution pipeline. Targeting predicates. Broom chain-reaction tests.

---

## Phase 6 — User interface

Home, bottom tabs (Store / Home / Leaderboard), overlay screens (Profile,
Settings, Win Streak) with a navigation stack, life and gold displays,
insufficient-funds prompt routing to the store, quit-confirmation prompt.

Meta is already tested by this point, so this phase is presentation only.

---

## Phase 7 — Polish

Audio, haptics, notifications, screen transitions, privacy text, the
end-of-content screen after the final authored level.

---

## Phase 8 — Release

itch.io upload, README with a play link, architecture summary, and pointers into
`docs/`.

---

## Working rhythm

One module per session. Plan, approve, implement, test, review, commit. Update
`DECISIONS.md` whenever a real choice is made — the record is worth as much as
the code.

Module specifications are written **when their phase is reached**, not in
advance. A spec written three phases early encodes assumptions the intervening
work will have invalidated.

M5 completed in phase 1.7 apart from its visibility half, which is presentation
(phase 2): unreachability, immovability, untargetability and threshold opening
all landed with the condition system.
