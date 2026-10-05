# Module 18 — Feedback animations

**Assembly:** `GateRush.Runtime` and `GateRush.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 12, 13, 15, 16, 17
**Phase:** 2.4c, part 2

---

## Responsibility

Make every change a move causes visible as it happens, in the style of the
reference game (D48), instead of appearing when the board is redrawn: a
cleared block breaks into cubes at its gate, a grabbed block lifts with a
white outline, ice breaks, shutters lift, locks open, generators and
elevators deliver their blocks, and a time bonus shows as "+N s".

In scope:

- The grabbed block's lift and outline.
- The exit: a destroyed block breaking into cubes through its gate. The
  peel of a layered block (Module 12) stays as it is.
- Ice breaking on a block that thaws and on a gate that opens.
- A shutter lifting off the blocks beneath it.
- A lock opening: its padlock and chains go.
- A count badge that goes down popping.
- A block spawning from a generator's machine, and an elevator's doors
  opening as a wave rises.
- The "+N s" time bonus next to the timer.
- The win panel after the last level saying that every level is done.

Not in scope, deliberately: the key flying to its lock and chains falling
(later), a refusal shake on grabbing a block that cannot move, snow and
other ambient effects, audio (Phase 7), jokers (Phase 5).

---

## Public surface

```
MoveChanges                         plain C#, Runtime; everything a move changed
    that presentation animates, found by comparing the VisibilityLayer
    picture before and after the move
    IReadOnlyList<ClearedBlock> Clears      // as today (ResolutionDiff)
    thawed blocks, opened gates, opened shutters, opened locks,
    badges whose count went down, spawned blocks with their source
    (generator index or elevator index)
    static MoveChanges Between(LevelContext ctx, BoardState before,
                               BoardState after, Move move, Direction? push)

BoardView
    void Present(BoardState state, MoveChanges changes, Action onDone)
    BeginDrag / Settle / Snap           also start and end the lift

HudView
    void ShowTimeBonus(int seconds)

ResultPanel
    ShowWin(title, hasNext)             unchanged; the caller passes the
                                        all-done title after the last level
```

Names are negotiable. That what changed is found by plain, tested code — not
worked out inside `BoardView` — is not.

---

## Design decisions (owner)

### Two stages per move

Today `Present` waits for the settle, plays the clear effects on the old
drawing, then redraws from the new state. That order stays, with a second
stage added:

1. **Leave.** After the settle, the clear effects play on the old drawing
   (exit or peel), as today.
2. **Arrive.** The board is redrawn from the new state, and every other
   change plays on the new drawing: what went away (ice, a shutter, a
   padlock and chains) is drawn as a temporary piece over the new drawing
   and animated away; what came in (a spawned block) starts from its source
   and moves to its place; badges whose count went down pop.

`IsBusy` holds through both stages, so a drag never starts on a block that
is still arriving. A stage with nothing to play is skipped at once. Every
temporary piece sits under the view's transform and every tween carries the
view as its id, so a restart, a level change or a disable during either
stage leaves nothing behind (Module 12's rules).

The starting durations keep a move that changes nothing beyond its own
clear as quick as today, and a move with arrivals under about half a second
more. The owner tunes them.

### What changed (`MoveChanges`)

- **Clears** are `ResolutionDiff`'s, unchanged: in play only the moved
  block clears.
- Everything else comes from comparing `VisibilityLayer` before and after
  the move, so the animations follow exactly what the player sees:
  - a block shown frozen before and shown unfrozen after has **thawed**;
  - a gate closed before and open after has **opened**;
  - a shutter closed before and open after has **opened**;
  - a block locked before and unlocked after, and still shown, has
    **unlocked**;
  - a frozen block, a closed gate, a lock, a shutter or a generator whose
    badge number went down but did not reach its end has a **count pop**;
  - a block not shown before and shown after, not revealed by a shutter
    that opened, has **spawned**, with its source: the generator or the
    elevator it came from. Find the source from `LevelContext`'s slot
    layout; check how slots are assigned rather than assume.
- A block revealed by a shutter that opened is not a spawn; the shutter's
  lift reveals it.

### The grabbed block

- On `BeginDrag` the block lifts: its root scales up a little, with a short
  ease, and a white outline appears around its shape — the whole footprint,
  so an L shape has an L-shaped outline. The outline sits under the lip,
  never over the face or the marks.
- On settle, snap or cancel it scales back and the outline fades. A block
  that clears is still lifted when the exit starts; the exit takes it from
  there.
- How the outline is drawn is left to you (for example a generated outline
  quarter set, or the quarters drawn again, white, and slightly larger);
  it must follow concave corners cleanly.

### The exit: cubes

- A destroyed block moves a short way into its gate and breaks into small
  cubes in its colour — the rounded-box sprite with the studs' gloss is
  enough — that burst out through the gate's side of the board, spread,
  turn, shrink and fade. The block itself is gone as the burst starts.
- The pieces are laid out by plain C#: for a given seed, the same pieces;
  a configured number per cell with a cap per block; every piece's motion
  points out through the gate's edge. Seed from the block index and move
  count, never from `UnityEngine.Random`'s shared state.
- The layered peel stays Module 12's.

### Ice, shutters, locks, badges

- **Thaw:** the ice face of the block breaks: a few ice shards (the ice
  colour, frost on top) fly out from the block's centre, fall a little and
  fade, over the unfrozen block already drawn beneath.
- **Gate opening:** the same shards burst from the gate's ice segment over
  the coloured, arrowed gate now drawn.
- **Shutter:** the slatted panel lifts — it shrinks toward its top edge
  while fading — over the blocks now drawn beneath it.
- **Lock opening:** the padlock and chains fade out with a slight scale-up.
  Nothing flies.
- **Count pop:** the badge scales up and back once. A badge whose count
  reaches its end does not pop; its owner's own animation plays instead.

### Spawns

- **Generator:** the new block starts small at the machine's screen and
  grows while moving to its cells; the screen then shows the next block, as
  the redraw already has it.
- **Elevator:** the lift doors slide apart to the region's sides, the wave's
  blocks rise — they start slightly small and low and ease up to full size
  with a small overshoot — and the doors close again beneath them. When no
  wave comes after the last one cleared, the elevator is simply gone, as
  today.

### Time bonus

- When a move earns a time bonus (`LevelSession.TimeBonusEarned`, M10),
  the HUD shows "+N s" next to the timer pill in Lilita One, in a
  configured colour; it rises a little and fades, and the pill pulses once.
  It shows when the bonus is earned, together with the digits' jump.

### After the last level

- The win panel after the last level in `LevelCatalog` reads a configured
  all-done title (for example "All Levels Complete") instead of the win
  title. Restart stays; Next is hidden, as today.

### Engine rules that carry over

- Every new sprite is generated by `ArtGenerator` from `ArtRecipe` (D46);
  greyscale and tinted where a colour varies.
- No magic numbers: every duration, ease, distance, scale, count, cap and
  colour is in `RuntimeConfig` or `ArtRecipe`, with a check in `Problems()`
  where a value can be wrong.
- No mutable statics; tweens carry the view (or the HUD) as their id and
  are killed on disable and destroy; `DOTween.Kill` by id, never globally.
- WebGL is a target: a burst is a handful of sprites per cell, capped per
  block; pool or destroy them, but never leave them behind.

---

## Left to you

- How `MoveChanges` is built, and whether `ResolutionDiff` becomes part of
  it or stays beside it.
- How `BoardView` sequences the two stages, and how a temporary piece is
  drawn for something the new drawing no longer has.
- The outline's drawing, the cube and shard geometry, and whether a burst
  uses sprites with tweens or a particle system — within the budget above.
- Starting values for every new field. The owner tunes them.
- The Editor steps the owner must do: list them precisely at the end.

---

## Tests

Edit Mode, against the plain classes. The look is checked by hand.

**`MoveChanges`**
- A plain move that clears nothing reports nothing.
- A move whose clear thaws a frozen block reports the thaw and no count pop
  for it; a clear that only lowers its count reports a count pop.
- A clear that opens a closed gate reports the opened gate.
- A clear that opens a shutter reports the shutter, and the blocks under it
  are not reported as spawns.
- A clear that completes a lock reports the unlock; one that consumes a key
  without completing it reports a count pop on the padlock.
- A clear that makes a generator spawn reports the spawned block with that
  generator as its source; a clear that brings an elevator's next wave
  reports every block of the wave with that elevator as its source.
- Clears are reported exactly as `ResolutionDiff` reports them today.

**Cube layout**
- The same seed gives the same pieces; the count per block never exceeds
  the cap; every piece moves out through the gate's edge, for all four
  edges.

**Result title**
- The last level's win uses the all-done title; any other level's win uses
  the win title.

**By hand (owner)**
- In the showcase level: grab lifts with an outline, including on an L;
  exits break into cubes through the right side; ice breaks on blocks and
  gates; a shutter lifts; a lock opens; badges pop; a generator and an
  elevator deliver their blocks; a bonus block shows "+N s".
- Restart during each animation leaves a clean board.
- A move with no side effects feels as quick as before.
- The last level's win panel reads the all-done title.

---

## Resolved during implementation

- **What changed.** `MoveChanges` sits beside an unchanged
  `ResolutionDiff` and takes its clears from it. A spawned block's source
  comes from `LevelContext.TryGetSpawner` (with `SpawnerKind`), backed by
  per-slot arrays built with the spawn layout; the slot helpers stay
  internal to Core. A block that appears under a shutter opening in the
  same move is revealed, not spawned.
- **Structure.** `BoardView` draws into a `Board` group and keeps
  temporary pieces and debris under `Effects/Stage` and `Effects/Debris`.
  Each block has a `Body` under its `Root`: the drag and settle move the
  root; the lift, spawns and rises scale the body about the footprint's
  centre. The effects live in `BoardView.Effects.cs`.
- **Debris.** Cubes, shards, the passing block and the gate glow carry
  their own tween id. The redraw between the stages kills only the
  view's id and replaces only `Board`, so debris keeps running through it
  and through later moves' redraws; restart, level change, disable and
  destroy remove it. Debris never holds `IsBusy`.
- **Lift.** The lifted block gets a `SortingGroup` at `liftedBlockOrder`.
  The outline is the block's own quarters drawn again in white, each grown
  only on its outward sides (`LiftOutline`); it retracts to zero width
  rather than fading, which would show seams. `outlineOrder` sits just
  below the block lip and is compared only inside the lifted group, so no
  existing sorting order moved.
- **Exit, reworked against the reference.** A destroyed block is handed to
  debris and slides through its gate at `exitSecondsPerCell`, clipped by
  a `SpriteMask` inside its own sorting group: exactly on the gate's inner
  line on the exit side, and past the grid by a margin derived from the
  lift scale, outline width and lip offset on the other three
  (`GateExit`). Cubes stream from the gate's outer line for the whole
  pass (`BurstLayout.Stream`), and a soft glow (`GateGlow` sprite) lights
  the gate's inner edge. The nudge fields are retired.
- **Win panel.** `BoardView` counts the passes still running; a
  presentation that ends during one reports done when the last pass
  finishes, so the win panel waits for the block without holding input.
- **Randomness.** Bursts use `System.Random` seeded from the burst kind,
  the block or gate index and the move number of the attempt, never
  `UnityEngine.Random`.
- **Time bonus.** "+N s" shows when the bonus is earned, with the digits'
  jump; on a winning move the result panel shows instead. The badge pop
  and the timer pulse are one half-sine each, with no ease field.
- **All-done title.** `ResultTitle` decides "last level" with the catalog
  query that hides Next (`TryGetNext`).
- **Elevator.** The door divider hides while the temporary doors open and
  returns with the real doors.
- **Plain moves.** On a move with no clear and nothing arriving, the
  redraw can end the lift's drop a frame or two early.
- **Assets.** An existing `RuntimeConfig.asset` keeps its stored values,
  so raised defaults (the cube stream's count, cap, size, travel and
  duration) were set by hand.
