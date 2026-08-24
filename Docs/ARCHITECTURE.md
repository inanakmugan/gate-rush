# Architecture

## Guiding principle

**Rules are engine-agnostic; Unity only renders.**

Every rule of this game — how a block moves, when a gate opens, how much gold a
level pays, when a life regenerates — is expressed in plain C# with no reference
to `UnityEngine`. Unity observes that state and draws it.

This is not stylistic. It buys three concrete things:

1. The entire puzzle rule set and the entire economy can be tested in
   milliseconds without opening a scene.
2. The solver can explore millions of board states without the overhead of
   `GameObject`s or `MonoBehaviour`s.
3. WebGL constraints (no threads, no file writes) are confined to one layer
   instead of leaking through the codebase.

## Layers

```
                    ┌───────────────┐
                    │      UI       │  screens, navigation
                    └───────┬───────┘
                            │
                    ┌───────▼───────┐
                    │    Runtime    │  MonoBehaviours, DOTween, input
                    └───┬───────┬───┘
                        │       │
          ┌─────────────▼─┐   ┌─▼──────────────┐
          │     Core      │   │      Meta      │
          │ board rules   │   │ economy, lives │
          └───────┬───────┘   └────────┬───────┘
                  │                    │
                  └────────┬───────────┘
                           │
                  ┌────────▼─────────┐
                  │    Platform      │  interfaces only
                  └──────────────────┘

  ┌──────────┐                    ┌──────────────────┐
  │  Solver  │ ──► Core           │  Serialization   │ ──► Core
  │ (editor  │                    │  (JsonUtility)   │
  │  only)   │                    └──────────────────┘
  └──────────┘
```

Dependencies point **downward only**. `Core` knows nothing about `Runtime`.
`Meta` knows nothing about `Core`. Neither knows anything about `UI`.

### Core — board rules

Pure C#. Owns the puzzle: the grid, the blocks, the gates, and the rules that
govern how a board changes. Contains no rendering, no timing, no input.

### Solver — design-time search

Pure C#, depends on `Core`. Answers one question: *is this board solvable, and
in how few moves?* Used by the Level Editor during authoring and by tests.

**Excluded from player builds by assembly definition.** The shipped game never
searches; it only plays back hand-authored levels the solver has approved.

### Meta — progression and economy

Pure C#. Wallet, lives, streak, inventory, level progress, store catalog,
persistence model. Completely independent of `Core` — the two meet only inside
`Runtime`, when a level ends and its result is reported.

### Serialization — level and save data

Depends on `Core` and on `UnityEngine` (for `JsonUtility`). A **deliberate
exception** to the engine-agnostic rule, isolated to one small layer of
data-transfer objects. `Core` types never carry serialization attributes; the
DTO layer converts between them.

### Platform — engine and OS boundaries

Interfaces that `Core` and `Meta` depend on, with per-platform implementations
supplied at startup:

| Interface | Purpose | Test double |
|---|---|---|
| `ILevelSource` | Retrieve level JSON by index | In-memory dictionary |
| `ISaveStore` | Persist and load a string blob | In-memory dictionary |
| `ITimeProvider` | Current UTC time and frame delta | Manually advanced clock |
| `IHapticService` | Vibration feedback | No-op |

### Runtime — presentation

MonoBehaviours. Reads `Core` and `Meta` state and draws it. Owns DOTween
animation, pointer input, the level countdown, and the visibility overlay
(frozen blocks, shutter fog).

### UI — screens

Home, Store, Leaderboard, Profile, Settings, Win Streak. Screens hold no rules.
A screen never computes whether the player can afford something; it asks `Meta`.

---

## Core concept 1 — static context vs. dynamic state

The single most important structural decision in the project.

**`LevelContext`** holds everything that never changes while a level is played:
grid dimensions, static walls, block shapes, colour stacks, movement axes, gate
positions and widths, lock/key pairings, shutter regions and thresholds,
generator spawn queues, elevator wave contents.

**`BoardState`** holds everything that does change: block positions, which
blocks are alive, how many colours each block has lost, which gates, blocks and
shutters have unlocked, generator and elevator progress, and all counters.

Only `BoardState` is hashed. `LevelContext` is passed by reference alongside it,
shared unmodified across every state the solver visits.

Getting this split wrong is expensive in both directions: putting static data
into the hash makes the solver needlessly slow, and leaving dynamic data out of
the hash makes it **silently incorrect**.

> **Rule for uncertain fields: include them.** An unnecessary field costs
> performance. A missing field costs correctness.

## Core concept 2 — one event, many listeners

Six of the ten mechanics are variations of "unlock something after N
removals." Rather than six systems, there is **one event type**:

```
ColorCleared(blockId, colour)
```

Every removal in the game emits it — a block pushed into a matching gate, a key
effect, a rocket, a broom. Every counter listens to it.

Most blocks carry a single colour, so clearing it removes the block. A few carry
a stack, in which case clearing the outer colour exposes the next one. **The
event does not distinguish these cases**, which is precisely why layered blocks
need no special handling anywhere else in the system.

Adding a mechanic later means adding a new *condition type* that listens to this
event. It does not mean touching existing mechanics.

## Core concept 3 — fixpoint resolution

Applying a move is not a single step. One move can start a chain:

> A colour is cleared → the counter hits 4 → a shutter opens → an elevator
> underneath is revealed → its region is already empty → a wave arrives → one of
> those blocks carries a key → a locked block unlocks.

`MoveResolver` therefore loops until nothing changes:

```
1. Validate the move (axis, frozen, locked, shutter, path clear)
2. Move the block
3. If it ends flush against a matching open gate: clear its colour, emit event
   The block STAYS at the gate mouth and now obstructs it
4. Drain the event queue: update counters, apply key effects
   (a key effect may emit new events — they join the queue)
5. Re-evaluate every unlock condition (gates, frozen blocks, shutters)
6. Check every spawn trigger (generators, elevators)
7. If anything changed in 4–6, go back to 4
8. Return the new BoardState
```

Step 7 is what makes mechanics composable without knowing about each other.
Guard it with an iteration limit; exceeding it indicates a cycle in the level
data and should throw rather than hang.

**Jokers enter this same pipeline.** A rocket is not a move, but its consequences
are identical to one. There is exactly one resolution path in the codebase.

```
   Player move ──┐
   Rocket      ──┼──► ResolveToFixpoint ──► new BoardState
   Broom       ──┘
```

## Core concept 4 — progress monotonicity

`TotalClearCount`, every `GeneratorSpawnIndex`, and every `ElevatorWaveIndex`
can only increase. No action can decrease them.

This makes the search space **stratified**: within one stratum only block
positions vary; transitions between strata are one-way. Unlike Rush Hour, whose
state graph is cyclic, this game's graph is a directed acyclic layering of
cyclic sub-graphs.

The solver exploits this: it explores one stratum fully, collects the actions
that advance progress, then moves to the next stratum and **discards the previous
stratum's visited set entirely**. Peak memory drops from "whole state space" to
"largest single stratum."

## Core concept 5 — time is not part of the search

Moves are unlimited; the only pressure is the countdown. The solver therefore
answers "is there a solution and how short is it," and the time budget is
*derived* from that answer at design time.

Time-bonus blocks (M10) never enter `BoardState`. They contribute to the level's
effective time budget, which the editor reports as a suggested value.

## Core concept 6 — truth vs. visibility

Frozen blocks and shuttered regions hide information *from the player*. They
hide nothing from the game.

`BoardState` always holds the truth. A separate `VisibilityLayer` in `Runtime`
decides what the player sees. The solver operates on truth and is unaffected.

Conflating these would make the solver unwritable.

---

## Determinism requirements

The solver's guarantees are worthless if the game can diverge from them at
runtime. Therefore:

- Generator spawn queues and elevator wave contents are **explicit arrays in
  level data**. Nothing is rolled at runtime.
- `MoveGenerator` emits moves in a fixed, documented order.
- No `UnityEngine.Random` in any deterministic path. `System.Random` with an
  explicit seed where randomness is genuinely needed (cosmetic effects only).

## WebGL constraints and how each is absorbed

| Constraint | Absorbed by |
|---|---|
| No file system | `ISaveStore` → `PlayerPrefs` implementation on WebGL |
| No async file reads | Levels ship in `Resources`, loaded synchronously |
| No threads | Solver never runs at runtime — it is editor-only |
| No haptics | `IHapticService` no-op implementation |
| Mouse vs. touch | Unified pointer handling in `InputController` |
| Large build size | Code stripping, Brotli with decompression fallback |

Take the **first WebGL build early** — immediately after the first playable
level exists, not at the end of the project. Code stripping occasionally removes
something only reflection uses, and that failure is far cheaper to diagnose in a
small project than a finished one.

## Assembly definitions

| Assembly | References | Platforms |
|---|---|---|
| `GateRush.Core` | — | All |
| `GateRush.Meta` | `GateRush.Platform` | All |
| `GateRush.Platform` | — | All |
| `GateRush.Serialization` | `GateRush.Core` | All |
| `GateRush.Solver` | `GateRush.Core` | **Editor only** |
| `GateRush.Runtime` | Core, Meta, Serialization, Platform, DOTween | All |
| `GateRush.Editor` | All of the above incl. Solver | Editor only |
| `GateRush.Tests` | All of the above incl. Solver | Editor only |

`GateRush.Core`, `GateRush.Meta`, `GateRush.Platform`, and `GateRush.Solver`
have `noEngineReferences` enabled. This turns the "no `UnityEngine`" rule from a
convention into a compiler error — which is why `Core` defines its own `Coord`
struct rather than using `Vector2Int`.

## Rendering

Universal Render Pipeline with the 2D Renderer, Linear colour space. HDR and
post-processing remain **enabled** so that glow and similar effects stay
available. Disabled: MSAA, depth texture, opaque texture, main and additional
3D lights — none apply to a 2D renderer.

Sprite materials default to `Sprite-Unlit-Default`. Lit materials are introduced
only alongside actual `Light 2D` components; a lit sprite in an unlit scene
renders black.
