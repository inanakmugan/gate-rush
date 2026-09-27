# Module 11 — Static skeleton: a level playable by hand

**Assembly:** `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 01, 02, 03, 08
**Phase:** 2.1

---

## Responsibility

Load one level, draw it, turn pointer drags into moves through `MoveResolver`,
and redraw the board instantly after every resolution. This is the first time
the game is playable by hand, and its purpose is to prove that simulation and
input are wired correctly before any presentation polish exists.

In scope:

- Loading a level JSON through `LevelSerializer`.
- Drawing every level element with placeholder visuals: grid, walls, blocks
  (including layered, frozen, locked and key-carrying ones), gates (open and
  closed), shutters, generators and elevators.
- A simple `VisibilityLayer` (D13): what the player may see is decided here,
  never in `Core`.
- Pointer input: the drag model of M1 — the block follows the finger cell by
  cell, stops against obstacles, and the move is applied on release.
- Number labels wherever a count matters to the player.
- Restart, and a log line when the level is won.

Not in scope, deliberately: DOTween animation, the countdown and time bonuses,
win/lose UI, menus, jokers, Meta, audio, haptics, the generator's
incoming-block preview (M6, presentation only), and `ILevelSource`. Those
belong to 2.2 and later phases.

---

## Public surface

Three plain C# classes hold every decision, so they can be tested without a
scene. The MonoBehaviours only wire them to Unity.

```
LevelSession                    plain C#
    LevelSession(LevelContext ctx)
    LevelContext Context
    BoardState State
    bool IsSolved
    bool TryApply(Move move)            // through MoveResolver.TryApplyMove
    void Restart()                      // back to BoardState.CreateInitial
    event Action StateChanged

DragController                  plain C#
    bool TryBegin(LevelContext ctx, BoardState state, Coord grabbedCell)
    Coord Update(Coord pointerCell)     // the block's displayed origin
    Move? End(Coord pointerCell)        // the move to apply, or none
    void Cancel()
    int BlockIndex
    bool IsDragging

BoardLayout                     plain C#
    grid cell <-> world position, and the camera size that fits the board

RuntimeConfig : ScriptableObject    every tunable: palette, sizes, margins,
                                    drag threshold, tints, label settings

LevelBootstrap : MonoBehaviour      TextAsset level + RuntimeConfig; builds the
                                    session and the views
BoardView : MonoBehaviour           draws the state; rebuilt on StateChanged
VisibilityLayer                     decides what is shown (may be plain C#)
InputController : MonoBehaviour     Input System pointer -> DragController
```

Names and the exact split among the MonoBehaviours are negotiable; the three
plain classes and where the decisions live are not.

---

## Design decisions (owner)

### Core is the only authority

`Runtime` never decides whether something is legal. The drawn board is always
`LevelSession.State`, and the only way it changes is `MoveResolver`. The drag
preview checks each single step with `BlockReachability.IsFootprintLegal`
against the current state — a `Core` rule — rather than its own collision
test, so preview and resolution cannot disagree. `BoardState.Origins` keeps
literal block indices (D35 only affects hashing), so a view bound to a block
index stays bound to the same block.

### The drag follows the finger (M1, D27)

- **Grab.** Pointer down on a cell occupied by a living block that
  `BoardState.CanMove` allows. The offset between the grabbed cell and the
  block's origin is kept, so the block does not jump to the pointer.
- **Follow.** Each frame, the pointer's cell (minus the grab offset) is the
  target. The block steps one cell at a time toward it, only in directions its
  `MovementAxis` permits, taking a step only if the new footprint is legal. It
  prefers the axis with the larger remaining distance and tries the other when
  that one is blocked; when neither step is legal it stops and waits. Several
  steps may happen in one frame after a fast drag.
- **No route-finding.** The block goes where the finger leads, cell by cell.
  It does not search a path around an obstacle; the player steers around it
  without releasing, which is exactly the behaviour M1 describes. Every
  position the block reaches is legal and connected to its start, so the
  released move is reachable by construction.
- **Instant.** The block snaps from cell to cell. Smoothing is 2.2's DOTween
  work, layered on top of the same stepping.

### Release decides the move

- **The block moved.** Release applies `Move(block, displayed origin)`. A
  block that ends flush and aligned with a compatible open gate clears (D25);
  one that only passed in front of a gate does not, because passing is not
  ending.
- **The block is back at its start.** This is a push candidate (the
  zero-distance move of M1 and D25). Take the drag's direction: the dominant
  axis of the pointer's displacement from where it grabbed, if that
  displacement is at least `RuntimeConfig`'s drag threshold. If a step in that
  direction would take the block off the board — it is flush against that
  edge — apply `Move(block, start)` and let `MoveResolver` decide whether it
  clears (`CanClearInPlace`, including D39's axis rule). Otherwise apply
  nothing.
- **A rejected move is a bug.** Every move the drag produces is legal by
  construction. If `TryApply` returns false, log an error naming the move; do
  not swallow it.

This is the case ROADMAP warns about: a drag with a determined direction and
no displacement must still clear a block at a gate. It is also the first move
of most levels.

### What the player sees (D13, `VisibilityLayer`)

- **Closed shutter:** an opaque cover over its region, with its remaining
  count. Blocks under it are not drawn.
- **Frozen block (M3):** its shape in a frozen tint, colour hidden, with its
  remaining count.
- **Locked block (M8):** its colour and shape as normal, plus a lock badge in
  the lock identifier's badge colour and the number of keys still required.
- **Key-carrying block:** a key badge in the target lock's badge colour.
- **Layered block (M4):** the outer colour and the one beneath it; a numeral
  when more than two colours remain.
- **Closed gate (M2):** drawn frozen and colourless, with its remaining count.
- **Generator:** a marker on its edge with the number of blocks still queued.
- **Elevator:** its region outlined, with the number of waves still to come.
- **Walls:** a distinct fill.

Remaining counts are the clears still needed (`threshold − current count`).
All colours, tints and sizes come from `RuntimeConfig`. Labels use
TextMeshPro (part of `com.unity.ugui` in this Unity version).

### Loading, restart, win

- `LevelBootstrap` holds a `TextAsset`; the level is swapped by dragging a
  different JSON onto it. `LevelSerializer.FromJson(asset.text, asset.name)`
  builds the context; a level that fails to load logs the error and draws
  nothing.
- The **R** key restarts from `BoardState.CreateInitial`. There is no undo
  (D14).
- When `IsSolved` first becomes true, log it once. No UI.

### Engine rules that apply here

- Enter Play Mode runs **without a domain reload**, so static fields are not
  reset between Play sessions. No mutable statics in `Runtime`; all state lives
  on instances created by the bootstrap.
- Every subscription in `OnEnable` has its matching unsubscription in
  `OnDisable` (CONVENTIONS).
- Input goes through the **Input System** package (the project's active input
  handler); mouse and touch go through one pointer path.
- Sprites use `Sprite-Unlit-Default`. No magic numbers: everything tunable is
  in `RuntimeConfig`.
- WebGL is a target: no threads, no file IO.

---

## Left to you

- How the MonoBehaviours are split, and whether `BoardView` rebuilds or
  updates its objects on each change. A rebuild is acceptable in 2.1.
- How block shapes are drawn: one sprite per cell, a composed outline, or a
  mesh. Placeholder quality is fine; readability of layered, frozen and locked
  blocks is what matters.
- Camera fitting: an orthographic camera sized so the board plus the
  configured margin fits the screen in both directions, re-fitted when the
  screen size changes.
- The scene and assets. Write the `.cs` files and the asmdef changes; the
  owner creates the scene, the `RuntimeConfig` asset (through a
  `CreateAssetMenu` entry) and any sprite. List those steps precisely at the
  end of the implementation.
- `GateRush.Runtime`'s asmdef gains references to the Input System and
  TextMeshPro assemblies; `GateRush.Tests` gains `GateRush.Runtime`.

---

## Tests

Edit Mode, against the plain classes. The MonoBehaviours are checked by hand.

**`DragController`**
- A drag along a free corridor follows the pointer cell by cell and releases
  as a move to the last reached origin.
- A drag into a wall or another block stops at the last legal origin, and
  moving the pointer around the obstacle lets the block continue — the
  steering case.
- A block never takes a diagonal step, and never reaches an origin that
  `BlockReachability.IsReachable` would reject: checked over many random
  pointer paths on a corpus board.
- An axis-restricted block ignores pointer movement across its axis.
- A frozen, locked, shuttered or dead block cannot be grabbed.
- A drag with no displacement toward the edge the block is flush against
  returns `Move(block, start)`; the same drag toward a neighbouring block,
  or below the threshold, returns no move.
- A drag that leaves and comes back to the start behaves the same way: the
  final direction decides.

**`LevelSession`**
- `TryApply` of a legal move replaces the state and raises `StateChanged`
  once; an illegal move changes nothing and raises nothing.
- A zero-distance move at a compatible gate clears the block (the ready
  opening move, end to end).
- `Restart` returns to the initial state, spawned blocks included.
- `IsSolved` becomes true after the last clear of a solved corpus level.

**`BoardLayout`**
- Cell to world to cell round-trips on every cell of a non-square board.
- The fitted camera size contains the whole board plus the margin in both a
  portrait and a landscape aspect.
