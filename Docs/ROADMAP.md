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

Grid, blocks, gates, resolution, search, tests, editor. No Unity scene involved
except the editor window.

| # | Module | Spec |
|---|---|---|
| 1.1 | `Coord`, `LevelContext`, definitions | `MODULES/01-level-context.md` |
| 1.2 | `BoardState` | `MODULES/02-board-state.md` |
| 1.3 | `MoveResolver` (fixpoint skeleton, M1 + M7 only) | `MODULES/03-move-resolver.md` |
| 1.4 | `MoveGenerator` | `MODULES/04-move-generator.md` |
| 1.5 | `ISearchStrategy` + BFS | `MODULES/05-search-strategy.md` |
| 1.6 | Test corpus — hand-built 3×3 to 5×5 boards | — |
| 1.7 | Condition system + M2, M3, M10 | spec written when reached |
| 1.8 | M8 locks/keys, M5 shutters | spec written when reached |
| 1.9 | Serialization (JSON DTOs) | spec written when reached |
| 1.10 | Level Editor window | spec written when reached |
| 1.11 | A\* + equivalence tests against BFS | spec written when reached |
| 1.12 | M4 layered blocks | spec written when reached |
| 1.13 | M6 generators, M9 elevators — includes adding a region-relative
         position to SpawnedBlock for elevator waves | spec written when reached |

Gate-compatibility rules (projection span, alignment, orientation) are settled in
1.1–1.3 and carry the heaviest test load. Subtle bugs concentrate there.

---

## Phase 2 — Playable single level

Board rendering, pointer input, DOTween movement, countdown, win/lose. No menus,
one hardcoded level.

Watch the zero-distance move here: a drag with a determined direction but no
displacement must still clear a block at a gate. This is the first move of most
levels and is easy to lose in input handling.

---

## Phase 3 — First WebGL build

Deliberately early. Build, open in a browser, confirm it runs.

Checks: code stripping did not remove anything reflective; portrait framing works
in a landscape page; pointer input behaves as in the editor; `PlayerPrefs`
persistence survives a reload; build size and load time are acceptable.

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
