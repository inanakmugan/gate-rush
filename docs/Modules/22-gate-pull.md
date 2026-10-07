# Module 22 — Gate pull

**Assembly:** `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 13, 18
**Phase:** 2.6

---

## Responsibility

Make an open gate feel like it draws a block in, as in the reference game
(D49): while a block is dragged toward a gate it can leave through, it is
pulled a little toward the gate and the gate glows faintly; released close
enough, it arrives at the gate and clears. Today a block must be released
almost exactly flush, and the gate's mouth feels dead.

In scope:

- Finding the pull's target during a drag.
- Drawing the pull and the gate's faint glow.
- Capturing a release within range.

Not in scope: any change to which blocks a gate accepts (M1, D25, D43),
pushing in place, the exit animation itself (Module 18), the solver.

---

## Public surface

```
DragController (Module 13, extended)      plain C#
    PullTarget? Pull                       // the current target, or none
    Vector2 Update(...)                    // as before; Position may now be
                                           // drawn toward the pull target
    Move? End(...)                         // captures when within range

PullTarget                                 plain struct
    Coord Origin                           // where the block would arrive
                                           // flush and aligned
    int GateIndex
    float Strength                         // 0 at the edge of the range,
                                           // 1 at the origin

DragSettings (extended)
    float PullRangeCells
    float PullAmount                       // how far toward the target, as a
                                           // fraction of the distance, at
                                           // full strength
    float CaptureRangeCells
```

Names are negotiable. That the target and the capture are decided in plain,
tested code, from Core's own rules, is not.

---

## Design decisions (owner)

### The target

- A target is a whole-cell origin `O` where the dragged block, arriving
  there, would be flush and aligned with a compatible open gate: the same
  answer Core gives when the block arrives (`BlockReachability`'s exit gate
  rule, evaluated on the state the drag began on). No new gate logic.
- `O` lies on one axis from the block's drawn position `p`, along an axis
  the block may move (M7), in front of it, and the straight line from `p`
  to `O` is legal by D44's rule (every overlapped origin passes
  `IsFootprintLegal`). Nothing is pulled through or around anything.
- The distance from `p` to `O` along that axis is at most
  `PullRangeCells`.
- **Led toward it.** The pointer target (after smoothing) lies on the gate
  side of `p` along that axis, or past `O`. Dragging away or across never
  pulls.
- Several targets: the nearest wins; a tie goes to the one along the axis
  the pointer is leading on.

### The pull

- `Strength = 1 − distance / PullRangeCells`, eased (ease in config).
- The returned position moves from the swept position toward `O` by
  `PullAmount × Strength × distance`. It stays on the legal line, so it is
  always legal, and the sweep starts from the unpulled position each update
  so the pull never accumulates or fights the finger.
- At the origin itself, with the pointer pushing on toward the gate, the
  block is drawn a little into the gate's mouth (the same amount, toward
  the gate) as a nudge. Release there behaves as today (D43: a push in
  place clears).

### The glow

- While there is a target, `BoardView` shows the target gate's glow
  (Module 18's sprite and placement) at `Strength × pullGlowAlpha`. It is
  not debris: it follows the drag, and fades out over a short time when the
  target goes away, on release, cancel, restart or level change.

### Capture on release

- If, on release, there is a target and the distance is at most
  `CaptureRangeCells`, the move is `Move(block, O)`; the settle tweens to
  `O` and the exit plays as for any arrival.
- Otherwise release is unchanged (D44: nearest cell; D43 for a push in
  place).
- The captured move is reachable by construction (D49): the resolver
  rejecting it is a bug, logged as today.

### Starting values (owner tunes them)

- `pullRangeCells` 0.8, `captureRangeCells` 0.6, `pullAmount` 0.35, a
  soft ease, `pullGlowAlpha` 0.5, glow fade 0.12 s.
- `Problems()`: ranges positive, capture at most pull range, amount in
  (0, 1).

### Engine rules that carry over

- No mutable statics; tweens killed on disable and destroy; no magic
  numbers; nothing depends on frame rate.

---

## Left to you

- How the target search walks the axes (per axis: the first exit origin
  ahead within range, then the legality of the line).
- Where the glow lives in `BoardView` and how it fades.
- Whether `PullTarget` exposes more for the view.
- The exact Editor steps for the owner (expected: none).

---

## Tests

Edit Mode, against `DragController`.

- A block dragged toward a compatible open gate within range has that
  gate's arrival origin as its target; one out of range has none.
- No target through an obstacle, toward a closed gate, toward a gate of
  another colour, or along a forbidden axis (M7).
- Dragging away from the gate removes the target, however close.
- The pulled position is always legal and lies between the swept position
  and the target; it never passes the target.
- Released within capture range, `End` returns `Move(block, O)`; just
  outside it, the nearest cell, as before. (The central decision: a release
  near an open gate clears.)
- The captured move is accepted by `MoveResolver` on corpus boards.
- Pushing in place at a gate still behaves exactly as D43.

**By hand (owner)**
- Approaching a gate pulls the block in smoothly and the gate glows
  faintly; dragging away lets go with no jump.
- Releasing a little short of a gate exits; releasing well short does not.
- No pull toward closed gates, other colours, or through other blocks.
