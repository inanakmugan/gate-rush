# Gate Rush

> **Draft for the owner to edit.** Replace every `[[...]]` placeholder and
> remove this note. Everything stated below is taken from the repository and
> `docs/`; check the numbers before you publish.

**A sliding-block puzzle game for the browser, built in Unity 6, where every
level is proved solvable by a solver before it ships.**

[[GAMEPLAY GIF OR VIDEO — 20 to 40 seconds: a zero-distance opening clear, a
corner-turning drag, a layered block, a shutter opening in a chain reaction,
the win panel]]

| | |
|---|---|
| **Play** | [inanakmugan.itch.io/gate-rush](https://inanakmugan.itch.io/gate-rush) (WebGL, desktop and mobile browsers) |
| **Video** | [[YOUTUBE OR VIMEO LINK]] |
| **Source** | [[GITHUB LINK]] |
| **Engine** | Unity 6000.3.25f1 (6.3 LTS), URP 2D Renderer |
| **Status** | Finished: 30 levels, 10 mechanics, 3 jokers, full meta game |

---

## The game

You drag blocks around a tightly packed grid and push them out through gates
of the same colour on the board's edges. A block slides cell by cell and turns
corners, but it never rotates, and it only fits through a gate at least as wide
as the block is in that direction. Moves are unlimited. The pressure comes
from a countdown.

Each level adds one idea on top of the last:

| Mechanic | What it does |
|---|---|
| Count-gated gates | A gate stays frozen until N blocks have been cleared |
| Frozen blocks | A block cannot be moved until N clears |
| Layered blocks | A block has a stack of colours and leaves one layer per exit |
| Shutters | A covered region hides its blocks until a threshold is met |
| Generators | An edge feeds a fixed queue of new blocks into the board |
| Elevators | A region fills with a new wave each time it empties |
| Axis restriction | A block can move only horizontally, or only vertically |
| Locks and keys | Clearing the key blocks unlocks a matching locked block |
| Time bonus | Destroying a block adds seconds to the countdown |

Jokers: **Clock** (adds time), **Rocket** (clears one block's colour) and
**Broom** (clears one colour from every block that shows it).

Around the puzzle there is a free-to-play style meta game:

- gold and lives that come back on a real-time clock
- a win streak that pays out jokers
- a Store with gold packs, sold through a **simulated** purchase service that charges nothing
- a Profile with a name and an avatar
- a Leaderboard that ranks the player among **labelled demo players**

## Screenshots

[[5 SCREENSHOTS, 9:16: Home · a late level · a mechanic introduction card ·
Level Complete · Store]]

## Engineering highlights

- **The rules have no Unity dependency.**
  - The board rules, the solver, the economy and the platform interfaces are
    plain C#.
  - They compile in assemblies with `noEngineReferences` enabled, so a stray
    `UnityEngine` reference is a compile error.
  - The whole rule set is tested in milliseconds without opening a scene.
- **One resolution path.**
  - Every removal emits one `ColorCleared` event: a gate exit, a Rocket or a
    Broom.
  - The resolver repeats until nothing changes. One clear can deliver a key,
    open a shutter, empty an elevator and bring in the next wave, in a single
    move.
  - No mechanic refers to another mechanic.
- **Immutable, hashed board state.**
  - Static level data (`LevelContext`) is kept apart from the changing state
    (`BoardState`). The state is immutable and hashed with FNV-1a.
  - Identical blocks are collapsed for the solver's search, which keeps it
    from exploring the same position many times over.
- **Determinism.** Generator queues and elevator waves are explicit arrays in
  the level data. Nothing is rolled at runtime, so the game plays exactly the
  sequence the solver checked.
- **Testable meta game.**
  - Lives are a count plus a UTC timestamp, read against an injected clock.
  - One `MetaGame` object takes every result and purchase.
  - The save is one versioned JSON document, and older versions are carried
    forward.

## Level editor and solver

[[EDITOR SCREENSHOT: a level open, live warnings, the solver verdict in the footer]]

- **A custom editor window.**
  - Authors every mechanic: blocks, gates, walls, shutters, generators and
    elevator waves.
  - Has undo.
  - Shows live warnings for authoring mistakes: a colour with no usable gate,
    a gate too narrow for a block, a lock without enough keys, an elevator
    wave that does not tile its region, and others.
- **Validate** runs the solver on a background thread, with cancellation:
  1. Exhaustive A\* at a small budget. It is admissible, so its answer is a
     proof, including of the shortest length.
  2. If A\* runs out of budget: nearest-next-clear, an enforced hill-climbing
     search. It is complete on levels without generators or elevators, and
     cross-checked by A\*. If the two disagree, it throws instead of
     reporting.
- **Three possible answers:** solvable, unsolvable, or *indeterminate within
  budget*. A solvable answer also says whether its length is proven shortest.
- The proven solution length gives the level's suggested time budget, so
  difficulty is measured, not guessed.
- The solver is an editor-only assembly and is never shipped in the player
  build.

## Architecture

```
UI ──► Runtime ──► Core      (board rules)
          │   └──► Meta      (economy, lives, save rules) ──► Platform (interfaces)
          └──────► Serialization (JSON DTOs)

Solver ──► Core              (editor only)
```

| Assembly | Role | Engine references |
|---|---|---|
| `GateRush.Core` | Grid, blocks, gates, fixpoint resolver | none |
| `GateRush.Solver` | BFS, A\*, nearest-next-clear (editor only) | none |
| `GateRush.Meta` | Wallet, lives, streak, jokers, save rules | none |
| `GateRush.Platform` | Save store, clock, haptics, purchases, leaderboard | none |
| `GateRush.Serialization` | `JsonUtility` DTOs for levels and saves | yes, isolated |
| `GateRush.Runtime` | MonoBehaviours, input, DOTween presentation | yes |
| `GateRush.UI` | uGUI prefab screens behind an `IFrontEnd` interface | yes |

The design record behind these choices, with the alternatives that were
rejected, is in [`docs/DECISIONS.md`](docs/DECISIONS.md) (82 decisions). The
rules of each mechanic are in [`docs/MECHANICS.md`](docs/MECHANICS.md).

## Tests

- **3,385 Edit Mode tests (NUnit).** None needs a scene. They cover:
  - Core, Solver, Meta and Serialization
  - the editor's validation logic
  - the plain-C# parts of Runtime and UI
- **Solver correctness.** A\* is tested against BFS for equal answers over a
  board corpus. A random-board property corpus checks nearest-next-clear's
  completeness argument.
- **How to run them:** Unity → Window → General → Test Runner → Edit Mode →
  Run All. [[IF YOU ADD CI: badge and one line here]]

## Tech stack

Unity 6.3 LTS · C# · URP 2D Renderer · uGUI + TextMeshPro · Input System ·
DOTween · NUnit · WebGL (Brotli, hosted on itch.io)

## How it was built

[[KEEP OR REWRITE IN YOUR OWN WORDS. The current README says: the
architecture, the mechanic rules and the module specifications in `docs/` are
yours and were written first, and the implementation was written with Claude
Code against them. Interviewers will ask, so say it plainly here.]]

## Credits

- Fonts: Fredoka, Nunito and Lilita One (SIL OFL 1.1).
- Glyphs and most sound effects: Kenney (CC0).
- Interface art: AI-assisted, then cut, cleaned and sized by hand.
- Licences are kept under `Assets/ThirdParty/`.

## Notes

Progress is kept in browser storage. Clearing site data resets it, and so
does a new itch.io upload, because each upload is served from a new path.

[[CONTACT: name · e-mail · LinkedIn]]
