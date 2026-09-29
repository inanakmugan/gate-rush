# Module 13 — Free drag: the block floats, the rules stay on the grid

**Assembly:** `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 11, 12
**Phase:** 2.3

---

## Responsibility

Make a dragged block float under the finger instead of hopping from cell to
cell. The block moves freely between cells, stops flush against whatever is
in its way, slides into corridors without catching on corners, and settles
into the nearest cell when released. That cell is the move (D44).

This is presentation and input only. `Core`, the solver, `Move`, the level
format and every gate rule are unchanged:

- A block released flush and aligned with a compatible open gate clears
  (D25). One that only passed in front of a gate does not.
- A push in place still needs a push toward the gate's edge (D43), decided
  exactly as in Module 11.
- The clear and peel effects, the countdown, the result panel and restart
  behave as in Module 12.

Not in scope, deliberately: any change to gate behaviour, a visual nudge of
the block into the gate while pushing, momentum or flicking, block outlines,
and the generator preview. Those are later polish, if ever.

---

## Public surface

As before, every decision lives in a plain C# class with Edit Mode tests;
the MonoBehaviours only wire it to Unity and DOTween.

```
DragController (Module 11, reworked)     plain C#
    DragController(DragSettings settings)
    bool TryBegin(LevelContext ctx, BoardState state, Vector2 pointer)
    Vector2 Update(Vector2 pointer, float deltaSeconds)
        // the block's continuous origin, in cell units; always legal
    Vector2 Position                     // the same, between updates
    Coord NearestOrigin                  // where the block would settle now
    Move? End(Vector2 pointer, out Direction? push)
    void Cancel()
    int BlockIndex
    bool IsDragging

DragSettings                             plain C#, built from RuntimeConfig
    float PushThresholdCells             // unchanged meaning (Module 11)
    float FollowRate                     // pointer smoothing, per second
    float CornerAssistCells              // how far off a corridor still slides in
```

Names are negotiable; that the drag's geometry is one plain, tested class is
not.

`DragController.Stepped` and `StepPlayback` exist only to play whole-cell
steps. Nothing needs them once the block floats, so they are removed with
their tests and their `RuntimeConfig` fields (`stepSeconds`,
`maxLagSteps`) — confirm in the plan that nothing else reads them.

---

## Design decisions (owner)

### Where the block may be (D44)

The block's position is a continuous origin `p = (x, y)` in cell units. Along
each axis it overlaps the cells `floor` and `ceil` of that coordinate — one
cell when the coordinate is whole, two otherwise — so `p` overlaps at most
four whole-cell origins.

- **`p` is legal exactly when every whole-cell origin it overlaps is legal by
  `BlockReachability.IsFootprintLegal`** against the state the drag began on.
  The drawn block never covers a cell a whole-cell block could not stand on,
  and the drag still decides nothing on its own (Module 11: Core is the only
  authority).
- This keeps D27's rule without a special case. To pass diagonally from one
  origin to another, the block overlaps both orthogonal neighbours on the
  way, so a block whose only free neighbour is diagonal still cannot go there.
- An axis-restricted block (M7) has its coordinate on the forbidden axis
  fixed at its start, so it only ever overlaps origins along its axis.

### Following the finger

- **Grab.** As in Module 11, but the offset kept is the pointer's continuous
  offset from the block's origin, so the block does not jump at the grab.
- **Target.** Each update the target is the pointer minus that offset,
  projected onto the axes the block may move along.
- **Smoothing is on the pointer, not on the block.** A smoothed pointer
  eases toward the real one at `FollowRate`, frame-rate independent
  (`1 − e^(−rate·dt)`), and the collision sweep below then runs toward the
  smoothed pointer. Smoothing the drawn block after the sweep could ease it
  straight across the corner of a wall between two legal positions.
- **Sweep, one axis at a time.** The block moves toward the target along
  one axis, then the other — the axis with the larger remaining distance
  first, horizontal on a tie — each as far as it can go while staying legal. It stops **exactly flush** against whatever blocks
  it — a whole-cell coordinate — never short of it and never inside it.
- **No tunnelling.** However far the pointer jumps in one frame, the sweep
  checks every whole-cell origin the block would pass over, so it can never
  cross a wall or another block. A fast drag reaches the same place a slow
  one would.
- **Corner assist.** When the block is stopped on one axis and its other
  coordinate is within `CornerAssistCells` of a whole cell from which it
  could continue, that other coordinate is nudged toward that whole cell —
  by no more than the distance the blocked axis wanted to travel this update
  — so the block slides into a corridor instead of catching on its corner.
  Beyond that distance the block simply waits, as in Module 11.
- **No route-finding.** The block still goes where the finger leads and
  never searches a path around an obstacle. The player steers.

### Release decides the move

- **Nearest origin.** The released move is `Move(block, round(p))`, each axis
  rounded to the nearest whole cell. An exact half rounds toward the block's
  start. `round(p)` is one of the origins `p` overlaps, so it is legal, and
  the block got there by a continuous path through legal positions, which
  implies a path of legal single-cell steps. The released move is therefore
  reachable by construction, and a rejected move is still a bug that is
  logged, never swallowed (Module 11).
- **Back at the start.** When `round(p)` is the start, the release is a push
  candidate, decided exactly as in Module 11 and D43: the pointer's
  displacement from where it grabbed, projected and thresholded, and
  `CanClearInPlace(..., direction)`.
- **Settle.** On release the view tweens the block from `p` to the cell it
  settles in — the new origin, or the start when no move was produced —
  with a duration and ease from `RuntimeConfig`. The clear effect, if any,
  starts from the settled cell. Input waits for the settle as it waited for
  queued steps in Module 12.
- **Release does not follow the pointer.** `End` rounds the position the
  player sees; the raw release pointer is used only for the push
  displacement. A final sweep toward it would make the block jump on
  release and bypass the smoothing.

### Timing that carries over from Module 12

- Release: settle, then the clear effects, then the redraw; input waits for
  all three.
- A time-out cancels the drag; the block is shown back at its start, no move
  is applied, and the panel shows at once.
- R and Restart work at any moment, a drag or a settle in progress included.

### Engine rules that carry over

- No mutable statics; every tween a component starts is killed when it is
  disabled or destroyed; every subscription in `OnEnable` is undone in
  `OnDisable`.
- No magic numbers: follow rate, corner assist, settle duration and ease are
  in `RuntimeConfig`, validated like the existing fields.
- WebGL is a target: no threads, and nothing depends on the frame rate.

---

## Left to you

- How the sweep finds the farthest legal position along an axis (for
  example, walking whole-cell boundaries toward the target and checking the
  origins each crossing adds).
- Whether the smoothed pointer lives in `DragController` or in a small
  helper it owns. Either way it is tested without a scene.
- How `BoardView` shows the dragged block at a continuous position while the
  rest of the board stays on the grid.
- Default values for the new `RuntimeConfig` fields. Start soft; the owner
  tunes them in Play Mode.
- The asset steps the owner must do, if any: list them precisely at the end.

---

## Tests

Edit Mode, against `DragController`. The MonoBehaviours are checked by hand.

**Legality**
- Over many random pointer paths on corpus boards, every position `Update`
  returns is legal: every whole-cell origin it overlaps passes
  `IsFootprintLegal`.
- A pointer jumping across a one-cell wall in a single update leaves the
  block flush on its own side of the wall.
- A block dragged into a wall or another block stops exactly flush: its
  coordinate on that axis is a whole number.
- A block whose only free neighbour is diagonal cannot reach it, however the
  pointer moves.
- An axis-restricted block keeps its forbidden coordinate fixed.
- A frozen, locked, shuttered or dead block cannot be grabbed (unchanged).

**Corner assist**
- A block slightly off a one-cell corridor's line, within the assist
  distance, slides into the corridor when dragged along it.
- Beyond the assist distance it waits at the corner.
- The assist never produces an illegal position.

**Smoothing**
- The block approaches a still pointer and converges on it.
- Frame-rate independence: one update of `2·dt` and two updates of `dt`
  arrive at nearly the same position on a free board.

**Release**
- Release rounds each axis to the nearest whole cell; an exact half rounds
  toward the start.
- Every released move over the random pointer paths is accepted by
  `MoveResolver.TryApplyMove` on the state the drag began on.
- A block dragged away and brought back near its start releases as a push
  candidate, and the existing push tests from Module 11 still hold: a push
  into a compatible gate the block is flush against returns
  `Move(block, start)`; a push toward a wall, another block, an edge with no
  usable gate, or below the threshold returns none; in a corner only the
  edge with the usable gate clears.

**Removed**
- `StepPlayback`'s tests and Module 11's step-by-step tests that assert
  whole-cell stepping (`Stepped`, "never a diagonal step") are replaced by
  the legality tests above.

---

## Resolved during implementation

- **`DragSettings`** holds the three drag tunables and a static
  `Problems(...)` that is the single statement of their valid ranges: the
  constructor throws on what it reports, and `RuntimeConfig.Problems()`
  reports the same messages at load. `CornerAssistCells` must be at least 0
  and below 0.5 (0 turns the assist off), so the two whole cells either side
  of a coordinate can never tie. There is no inspector slider: a slider can
  land exactly on 0.5.
- **The sweep** walks whole-cell lines from the ceil (moving up) or floor
  (moving down) of the current coordinate. A line is legal when every
  origin on it that the other coordinate overlaps is; the first illegal
  line leaves the block exactly on the whole cell before it. The first line
  off the board is illegal, so the walk ends whatever the target.
- **Corner assist.** Only the first axis that was blocked is assisted, at
  most once per update. A whole cell qualifies when it is within the assist
  distance (inclusive) and the next line along the blocked axis is legal
  from it. The nudge is capped by the blocked axis's remaining distance to
  its target; on reaching the cell, the blocked axis is swept again in the
  same update.
- **Non-finite input.** A NaN or infinite pointer cannot start a drag and
  is ignored by `Update`. A zero, negative or non-finite frame time leaves
  the smoothed pointer where it is. The drag advances on
  `Time.unscaledDeltaTime`, the countdown's clock.
- **Rounding.** Each axis rounds to `start + sign(d)·ceil(|d| − ½)`, with
  `d` the offset from the start: an exact half stays toward the start.
- **Settle and `IsBusy`.** `BoardView.Settle` places the block at once, with
  no tween, when it is already on its cell (a push in place, or a release
  exactly on a cell), so a clear effect does not wait for nothing. The
  settle tween is reset to null on completion and on every kill — `Snap`,
  `BeginDrag`, `Rebuild`, disable and destroy — so `IsBusy` cannot hold for
  good. `Present` starts the effects when the settle finishes, or at once
  when none is playing.
- **Time-out during a settle.** A settle already under way when time runs
  out finishes on its own: there is no drag to cancel, and the move it
  carries was applied at release. A drag in progress is cancelled and
  snapped back to its start.
- **`InputController`** no longer subscribes to anything: with `Stepped`
  gone, it feeds `drag.Position` to `BoardView.ShowDragged` every frame.
- **Removed:** `StepPlayback` and its tests, `DragController.Stepped`, and
  the `stepSeconds`, `maxLagSteps` and `stepEase` fields. New
  `RuntimeConfig` fields, with soft defaults for tuning in Play Mode:
  `followRate` 20, `cornerAssistCells` 0.3, `settleSeconds` 0.08,
  `settleEase` OutQuad.
