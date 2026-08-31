# Gate Rush

A Unity reimplementation of the sliding-block puzzle *Block Out!* by Grand Games,
built as a portfolio project with an emphasis on engine-independent game logic,
solver-verified level design, and a documented architecture.

**▶ Play in browser:** *(itch.io link)*

Unity 6000.3.22f1 (6.3 LTS) · Universal Render Pipeline, 2D Renderer · WebGL and
Android

---

## What it is

Blocks slide freely across a tightly packed grid and are pushed out through
colour-matched gates on the board edges. Moves are unlimited; the only pressure
is a countdown. Ten interacting mechanics layer on top: count-gated gates and
blocks, layered colour stacks, shutters, block generators, elevators, axis
restrictions, and lock-and-key pairs.

## What is interesting about it

**The rules do not know Unity exists.** Board logic, search, and the economy are
plain C# in assemblies compiled with engine references disabled — the layer
boundary is a compile error, not a convention. The entire rule set runs in an
Edit Mode test suite in milliseconds, without loading a scene.

**Levels are proved solvable before they ship.** A breadth-first solver runs
inside a custom editor window and answers three ways — solvable in *n* moves,
unsolvable, or indeterminate within budget. The move count also produces the
level's suggested time budget, so difficulty pacing is measured rather than
guessed.

**Ten mechanics compose without knowing about each other.** Every removal in the
game emits one event; every unlock condition listens to that event; the resolver
loops to a fixpoint. A shutter opening can reveal an elevator whose incoming wave
carries a key that unlocks a block — all within a single move, with no mechanic
referencing another.

**The search space is stratified, not cyclic.** Because blocks leave the board
permanently, progress counters increase monotonically, so the state graph is a
one-way layering of strata. The solver discards each stratum's visited set on
advance, bounding memory to the largest single stratum rather than the whole
space. This is the structural difference from Rush Hour–style puzzles, where
nothing is ever removed.

## Architecture

```
Core            board rules            no engine references
Solver          search                 no engine references, editor-only
Meta            economy, lives         no engine references
Platform        service interfaces     no engine references
Serialization   JSON DTOs
Runtime         MonoBehaviours, DOTween, input
UI              screens and navigation
```

Full write-up: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)

## Documentation

| Document                                       | Contents |
|------------------------------------------------|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Layers, dependency rules, core concepts |
| [`docs/MECHANICS.md`](docs/MECHANICS.md)       | Normative rules for all ten mechanics |
| [`docs/DECISIONS.md`](docs/DECISIONS.md)       | Decision record with rejected alternatives |
| [`docs/Modules/`](docs/Modules/)               | Per-module specifications |
| [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md)   | Coding standards |
| [`docs/ROADMAP.md`](docs/ROADMAP.md)           | Build order |

## On process

The implementation was written with Claude Code. The architecture, the mechanic
rules, and the module specifications in `docs/` are mine; they were written first
and the code was written against them. `DECISIONS.md` records what was chosen,
why, and what was rejected — including dropping procedural level generation in
favour of a hand-authoring editor with live solver validation.

## Running the tests

Unity → Window → General → Test Runner → Edit Mode → Run All

The `Core`, `Solver`, `Meta`, and `Serialization` suites require no scene.

## Note on the web build

Progress is stored in browser storage. Clearing site data will reset it.
