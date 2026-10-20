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

*Status: done.* 2.0 to 2.7 are complete: 15 levels that introduce every
mechanic in turn, visual polish, development tools, the gate pull and a
prototype menu. Levels chain through Next and the menu's level select.

Board rendering, pointer input, DOTween movement, countdown, win/lose. Planned
with no menus and one hardcoded level; the level set (2.0) and the prototype
menu (2.7) grew it.

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
the editor. level-1 starts with a ready opening move; the others need not,
which D16 allows: it is a guideline, not a rule. The set later grew to 15
levels, level-0 to level-14, one per mechanic in the owner's plan (How to
Play to Final mix). Levels 8 to 15 were searched for and checked with the
project's own A\* outside the editor, then validated in the Level Editor.
The Final mix leaves out the elevator: with all ten mechanics on one board
no candidate could be decided within the solver's budget.

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
build goes to a mentor, so the prototype look is replaced first, in five
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
    *Spec: `Modules/18-feedback-animations.md`. Status: done.* The exit was
    reworked against the reference: the block slides through its gate,
    clipped at the gate's inner line, while cubes stream out beneath it and
    the gate glows inward.
- **2.4d — Mechanic introductions.** A card over the board the first time
  each mechanic appears, as the reference game does, plus How to Play on
  the first level. Which level introduces what is derived from the levels'
  content, so reordering levels moves the cards with them. The countdown
  waits for the last card. Time-bonus blocks gain a board mark.
  *Spec: `Modules/19-mechanic-introductions.md`. Status: done.*
- **2.4e — Layered blocks and editor marks.** 2.4a and 2.4b both left
  layered blocks (M4) on 2.1's placeholder, a small square per cell, so a
  2×2 layered block reads as four blocks. It becomes one block of the
  outer colour with one inset block of the colour beneath, with its peel
  and the depth on the shared badge. The Level Editor shows layered and
  frozen blocks. *Spec: `Modules/20-layered-blocks.md`. Status: done.* A peel
  also glows its gate, streams cubes of the removed colour and bumps the
  block into the gate, as an exit does.

**2.5 — Play any level.** Development tooling so the owner can test any
level without editing the Level scene: a Play button in the Level Editor,
a Gate Rush → Play Level… window listing every level, a one-shot
editor-only start-level override, and keys to step between levels during
Play in the editor and development builds. The scene keeps starting on the
first level. *Spec: `Modules/21-dev-tools.md`. Status: done.* The keys are
Page Down and Page Up.

**2.6 — Gate pull.** An open gate draws in a block dragged toward it, as in
the reference game: the block is pulled a little toward the gate and the
gate glows faintly, and a release close enough arrives and clears (D49).
*Spec: `Modules/22-gate-pull.md`. Status: done.* The work also fixed an
older bug: a block passing through its gate drew as a white silhouette.

**2.7 — Prototype menu.** A small front end for the first WebGL build: a
title screen with Play and Levels, a level select with every level open and
completed ones marked, a Menu button in the HUD and on the result panel,
completed levels remembered across reloads through `ISaveStore`, and "All
Levels Played" with "Thanks for playing!" after the last level. Not Phase
6's Home and not Phase 4's save model; both take it over later (D50).
*Spec: `Modules/23-prototype-menu.md`. Status: done.*

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

No item is open; the last ones were cleared by Modules 24 to 28.

- *Level Editor:* done in Module 27 (D54).
- *Solver and presentation:* done in Module 28.
- *Generator preview:* done in Module 26 (D53).

---

## Phase 3 — First WebGL build

Deliberately early. Build, open in a browser, confirm it runs.

Checks: code stripping did not remove anything reflective; portrait framing works
in a landscape page; pointer input behaves as in the editor; `PlayerPrefs`
persistence survives a reload; build size and load time are acceptable; draw
calls and the per-move board rebuild are profiled on a phone (Module 15).

Finding a stripping problem here costs an afternoon. Finding it in Phase 7 costs
a week.

**Hosting: itch.io (D51).** An HTML5 upload: a ZIP with `index.html` at its
root, Mobile Friendly, portrait, the embed viewport matching the build's
default canvas size. Compression gzip or Brotli without Decompression
Fallback. Saved data does not carry over between uploads. The build opens
on the prototype menu (2.7, D50).

*Status: done.* Published at https://inanakmugan.itch.io/gate-rush, Brotli
without fallback, a 13 MB ZIP. It runs in desktop browsers and on an iPhone 13
Pro: every level loads (so stripping removed nothing the levels need), touch
and drag work, and completed levels survive a reload. On the phone it feels
heavier than a native app, which the owner finds acceptable for a WebGL build.
Profiling the draw calls and the per-move board rebuild moves to Phase 7, under
optimisation. Learned on the first upload: a page that fills
its loading bar and stops is first worth a fresh ZIP, with `index.html`,
`Build` and `TemplateData` at its root; the first ZIP was the whole fault.

---

## Phase 4 — Meta core

*Status: done.* The save model, gold and lives on a real-time clock, the
win streak with its joker rewards, the store's rules and the campaign are
pure C# behind one `MetaGame`, and the prototype menu and HUD show them in
plain text, with free play from Settings (Modules 29 to 32).

Wallet, lives, streak, inventory, progression, save model, configuration
assets. Pure C#, fully tested, shown in the build only as plain text and
one prompt (D55). Verify "win three, lose one, wait forty minutes" against
a fake clock.

Four modules (D55). Each spec is written when the module before it is
merged.

| # | Module | Spec | Status |
|---|---|---|---|
| 4.1 | Save model: one versioned save, the old keys carried over (D56) | `Modules/29-save-model.md` | Done |
| 4.2 | Wallet, lives and `ITimeProvider` (D20, D22, D59, D60) | `Modules/30-wallet-and-lives.md` | Done |
| 4.3 | Win streak, joker inventory, store rules, campaign progress, `MetaGame` (D58, D61 to D63) | `Modules/31-meta-game.md` | Done |
| 4.4 | Runtime wiring: gold and lives as text, the life prompt, free play in settings (D58, D64) | `Modules/32-meta-wiring.md` | Done |

Not in Phase 4: unlimited lives for a while, until something grants it;
jokers on the board (Phase 5); the screens (Phase 6); notifications (Phase
7).

---

## Phase 5 — Jokers

*Status: done.* The joker rules are tested in Core, spent through
`MetaGame` or free play's allowance, and played from a bar under the
board: the Clock on its button, the Rocket and the Broom selected and used
with one tap, presented in place with the board's existing effects
(Modules 33 and 34, D65 to D67).

Where Phase 1 and Phase 4 meet. Clock, rocket, broom wired into the existing
resolution pipeline. Targeting predicates. Broom chain-reaction tests.

Two modules (D65). Each spec is written when the module before it is
merged.

| # | Module | Spec | Status |
|---|---|---|---|
| 5.1 | Joker rules: Core tests, spending and buying jokers, the session, free play's allowance (D65, D66) | `Modules/33-joker-rules.md` | Done |
| 5.2 | Joker bar, selecting and using jokers, and the presentation of joker clears (D65, D66, D67) | `Modules/34-joker-bar.md` | Done |

---

## Phase 6 — User interface

*Status: in progress.* 6.1 is done: a uGUI front end in `GateRush.UI`
with three tabs, an overlay stack, the top bar and Home, built from
prefabs laid out by hand and by an Editor builder (Module 35). 6.2 is
done: the Store sells gold packs through a simulated purchase service, and
jokers and the refill for gold, with a Not Enough Gold prompt that routes
to the packs (Module 36, D72). 6.3 is done: a streak bar on Home, the
Win Streak overlay with the reward ladder, and streak rewards announced
on Home (Module 37). 6.4 is done: the player's name and avatar on a
Profile overlay, a Haptics toggle of its own, save format version 4, and
Nunito as the Turkish fallback font (Module 38). 6.5 is done: the
Leaderboard ranks the player on lifetime gold among twenty labelled demo
players behind `ILeaderboardService` (Module 39, D73).

Home, bottom tabs (Store / Home / Leaderboard), overlay screens (Profile,
Settings, Win Streak) with a navigation stack, life and gold displays,
insufficient-funds prompt routing to the store, quit-confirmation prompt.

Meta is already tested by this point, so this phase is presentation only.

Screens are uGUI prefabs in a `GateRush.UI` assembly, laid out after the
reference game with CC0 art and an open font (D68, D69). Six modules (D68).
Each spec is written when the module before it is merged.

| # | Module | Spec | Status |
|---|---|---|---|
| 6.1 | UI foundation: the assembly, tabs and overlay stack, theme, shared prefabs, Home (D68, D69) | `Modules/35-ui-foundation.md` | Done |
| 6.2 | Store: gold packs through a simulated purchase service, jokers and the refill for gold, and Not Enough Gold routing to it (D62, D72) | `Modules/36-store.md` | Done |
| 6.3 | Win Streak: Home's progress bar and the ladder screen (D61) | `Modules/37-win-streak.md` | Done |
| 6.4 | Profile and Settings: name, avatar, haptics, save format 4 (D71) | `Modules/38-profile-settings.md` | Done |
| 6.5 | Leaderboard behind `ILeaderboardService`, the player ranked among labelled demo players (D70, D73) | `Modules/39-leaderboard.md` | Done |
| 6.6 | The level's interface in the new theme, as prefabs | — | Planned |

---

## Phase 7 — Polish

Audio, haptics, notifications, screen transitions, privacy text, the
end-of-content screen after the final authored level (a first version, "All
Levels Played", came with 2.7).

*7.1 — Sound and haptics* came forward and was built before Phase 4 (Module
25, D52): sound effects tied to the presentation stages, a sound toggle on
the menu, and vibration where the browser allows it. *Status: done.*

Optimisation, carried over from Phase 3: profile the draw calls and the
per-move board rebuild (Module 15), on a phone where possible. The WebGL build
plays on an iPhone 13 Pro but feels heavier than a native app.

---

## Phase 8 — Release

itch.io upload, README with a play link, architecture summary, and pointers into
`docs/`. The itch.io page and the README's play link came early, with
Phase 3.

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
