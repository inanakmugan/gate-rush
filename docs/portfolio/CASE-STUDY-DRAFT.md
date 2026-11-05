# Gate Rush: building a puzzle game whose levels are proved solvable

> **Draft for the owner to edit.** It is written in your voice. Check every
> claim against your own memory, cut what you would not say in an interview,
> and fill in the `[[...]]` placeholders. Decision numbers (Dxx) refer to
> `docs/DECISIONS.md`. Body: about 1,500 words.

## Goals

I built Gate Rush as my first published game and as a portfolio piece for
mobile studios. It is a sliding-block puzzle. You drag blocks across a packed
grid and push them out through gates of the same colour, against a countdown.
Thirty hand-made levels bring in ten mechanics one by one: layered colours,
count-gated gates, frozen blocks, shutters, generators, elevators, axis
restrictions, locks and keys, and time bonuses. Three jokers sit on top. Around
the puzzle there is a full free-to-play meta game: gold, regenerating lives, a
win streak, a store, a profile and a leaderboard. It ships as a WebGL build on
itch.io [[link]].

I set myself three goals:

1. **Every level is provably solvable.** A countdown puzzle that can silently
   be impossible is broken.
2. **The rules are testable without Unity**, in milliseconds.
3. **The design is written down before the code**, with reasons and rejected
   alternatives: 82 entries in the decision record.

## Key architecture decisions

**The rules do not know Unity exists (D19, D21).** Board rules, the solver,
the meta game and the platform interfaces are plain C#. Their assembly
definitions have `noEngineReferences` enabled, so a stray `Vector2Int` or
`Debug.Log` is a compile error, not a code-review comment. I created these
assemblies before writing any code (Roadmap, Phase 0); turning the flag on
later would have meant cleaning up every leak at once.

**Static context and dynamic state are kept apart (D1, D2).**

- `LevelContext` holds everything that never changes during a level: the
  grid, walls, block shapes, gates, spawn queues.
- `BoardState` holds what does change, and it is immutable and hashed with
  FNV-1a. Applying a move returns a new state.
- The rule I wrote for myself was: *an extra hashed field makes the solver
  slow; a missing one makes it wrong.* So when in doubt, I hashed the field.
- Immutability also gave the solver free backtracking.

**One event, many listeners (D7).** Six of the ten mechanics are "unlock
something after N removals". Instead of six systems there is one event,
`ColorCleared`.

- Every removal raises it (a gate exit, a Rocket, a Broom), and every
  counter listens to it.
- Losing an outer layer raises the same event as being destroyed, so layered
  blocks need no special code anywhere.

**Fixpoint resolution (D8, D9).** One clear can deliver a key that unlocks a
block, cross a shutter's threshold, and empty an elevator so its next wave
arrives. `MoveResolver` repeats "drain events, re-evaluate conditions, check spawns"
until nothing changes. The loop has a bound derived from the level's
contents, and going past it throws, because it means the level data has a
cycle. Jokers go through the same pipeline (D9), so there is exactly one way
to remove a colour in the codebase.

## The solver

The solver answers one question: *is this level solvable, and if so, in how
few moves?* It lives in an editor-only assembly (D18). WebGL has no threads,
and the shipped game never needs to search.

**Movement made it hard.**

- A block reaches any position connected to its start by legal steps, corners
  included (D27). So one "move" can land almost anywhere in the free space,
  and the branching factor is far higher than in classic sliding puzzles.
- Two things keep the search manageable:
  - a canonical move set: gate-aligned positions, zero-distance clears, and
    positions resting against an obstacle (D5)
  - symmetry reduction: identical blocks share one identity, so permuting
    them does not create new states (D35)

**Monotonic progress makes the state graph layered (D6).**

- Cleared colours never come back, and generators and elevators only move
  forward. So the state graph is a one-way series of strata. Inside a
  stratum the graph has cycles; between strata it has none.
- Breadth-first search throws away each stratum's visited set when it
  moves on, so peak memory is the largest stratum, not the whole space.

**Three answers, and quality separate from existence (D4, D36).** A result is
*solvable*, *unsolvable* or *indeterminate within budget*. Running out of
budget is never reported as "unsolvable". A solvable result also says whether
its length is proven shortest. A\* is exhaustive and admissible, so its
answer is a proof. The proven length gives the level's suggested countdown.

**When A\* was not enough (D37, D38).**

- Some of my simplest levels exhausted both BFS and A\*. The heuristic is
  blind before the first clear, and every block has a different colour, so
  symmetry gives nothing.
- I added *nearest-next-clear*, an enforced hill-climbing search. It
  searches the current stratum for any route to the next clear, commits to
  it, and never backtracks.
- In general that is incomplete. I wrote an argument that it is complete when
  "a clear never turns a solvable board unsolvable", which holds for levels
  without generators or elevators.
- I did not trust the argument alone:
  - every "unsolvable" from nearest-next-clear is cross-checked by A\*
  - if the two strategies disagree, the run throws instead of showing either
    answer
  - a random-board property corpus tests the argument
- That corpus found a real hole. A lock whose keys had different effects made
  clear order matter. I narrowed the argument first. Later I removed the
  feature that caused the hole (D45: keys only unlock), because no level
  used it and it carried most of the complexity around keys.

The editor's **Validate** button runs this pipeline on a worker thread with
cooperative cancellation (D38).

## The level pipeline

- **No procedural generation (D15).** I considered reverse generation:
  pushing blocks inward through gates. It would have made most gating
  mechanics free, but it cannot choreograph generators and elevators. So
  levels are authored by hand in a custom editor window with undo (D33).
- **Live warnings.** The editor checks the level against the authoring rules
  in `MECHANICS.md` as you build:
  - a colour with no usable gate
  - a gate narrower than a block's projection
  - a lock with too few keys
  - an elevator wave that does not tile its region exactly
  - a level that copies another level
- **Errors are separate from warnings.** Hard data errors, such as two
  adjacent layers of the same colour, throw on load and name the element.
- **Versioned JSON through a DTO layer (D17)**, shipped in `Resources` and
  loaded synchronously, which suits WebGL.
- **Thirty levels, each passed by the solver.** The final "all mechanics"
  level leaves out the elevator: with everything on one board, no candidate
  could be decided within the solver's budget. I recorded that limit rather
  than ship an unverified level.

## WebGL lessons

- **Build early.** I took the first WebGL build right after the first
  playable level (Roadmap, Phase 3), not at the end.
  - The first itch.io upload showed a loading bar that filled and stopped.
    The cause was a badly packed ZIP: `index.html`, `Build` and
    `TemplateData` must sit at the ZIP root.
- **Hosting decides compression (D51).** I chose Brotli without a
  decompression fallback, which works because itch.io sends the right
  `Content-Encoding` header. GitHub Pages does not send it, so I ruled it out.
- **Audio differs on the web.** Short cues of about 10 ms were silent in the
  browser, though fine in the Editor. WebGL encodes clips as AAC, which needs
  at least 1,024 samples. The fix was 100 ms padded clips imported as PCM.
  The lesson I wrote down: check sound in the web build itself.
- **Platform services behind interfaces (D19).** Saves, haptics (the
  browser's Vibration API through a `.jslib`), purchases (simulated, and it
  says so, D72) and the leaderboard are each one class to swap for a real
  backend.

## What went wrong, and what I learned

- **I modelled movement wrongly at first (D27).** I assumed blocks slid in
  straight lines. Watching how the genre actually plays showed they follow
  the finger around corners. That changed the solver's branching factor
  completely and forced the canonical move set. *Lesson: confirm the core
  rule against real play before building search on top of it.*
- **A spec can contradict itself.** My lives spec says setting the clock
  back never gains a life, but the mechanism it prescribes (restart the wait
  at the earlier time) refills lives once the clock is put right again. A
  review caught it; the tests had encoded the mechanism, not the goal. I
  fixed it with a property test over clock sequences (D83).
  *Lesson: test the property, not the recipe.*
- **The god class.** `LevelBootstrap` grew to about 2,800 lines. It owns the
  level lifecycle, joker flow, meta reporting, saving and front-end
  navigation. Its parts are well documented, but it has no tests of its own,
  so the campaign flow from result to save is checked only by playing.
  *Next time:* a plain-C# `LevelFlow` state machine, tested like `Core`,
  with the MonoBehaviour only forwarding Unity callbacks.
- **CI came late.** For most of the project about 3,400 Edit Mode tests ran
  only when I pressed Run All. I added CI after release: the engine-free
  assemblies build under plain .NET 8 on GitHub Actions, about 750 tests
  run on every push with about 94% coverage. I would set that up on day
  one now, as in my second project, Loop Blast.
- **Done means done (D82).** I closed the project on the web build rather
  than starting an Android port. Reviewers open a link; a phone build would
  have cost weeks they would never see. Phone performance work is listed as
  deferred, not done.

[[OPTIONAL CLOSING: one sentence on what you are building next, or on your
second project, Loop Blast]]
