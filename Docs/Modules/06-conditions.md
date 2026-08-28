# Module 06 — Unlock conditions, and mechanics M2, M3, M10

**Assembly:** `GateRush.Core`
**Depends on:** Modules 01, 02, 03
**Phase:** 1.7

---

## Responsibility

Make `MoveResolver.ReevaluateConditions` real. Until now it has returned `false`
unconditionally, so the fixpoint loop has never run more than one pass outside a
test seam. This module is where the loop starts doing work.

Three unlocks, all reading the same counters:

- **M2** — a gate opens once enough colours have been cleared.
- **M3** — a frozen block unfreezes once enough colours have been cleared.
- **M5, in part** — a shutter opens, either on the total count or on one
  colour's count. Shutters are otherwise phase 1.8's; only their threshold
  evaluation lands here, because it is the same evaluation and splitting it
  would mean writing it twice.

Plus one mechanic that is not a condition at all:

- **M10** — a block carrying a time bonus reports its seconds when it dies.

Locks and keys (M8), generators (M6) and elevators (M9) are **not** in scope.
`ApplyKeyEffects` and `CheckSpawnTriggers` stay as they are.

---

## Public surface

### Threshold evaluation

One predicate answers every unlock in the game:

```
static bool IsThresholdMet(
    int totalClearCount,
    IReadOnlyList<int> clearCountByColor,
    int threshold,
    BlockColor? requiredColor)
```

`requiredColor` null means the total count; otherwise that colour's count.

It takes raw counters rather than a `BoardState` because
`ReevaluateConditions` runs **inside** the fixpoint loop, where the counters live
in the resolver's successor builder and no `BoardState` exists yet. A method on
`BoardState` would be unusable at the only place it is needed.

`BoardState` may expose a convenience wrapper for callers that do hold a state
(the editor will want one), but the predicate itself must be reachable from
inside the loop.

### `MoveResolver` — time bonus output

All three entry points gain an output:

```
bool TryApplyMove(LevelContext ctx, BoardState state, Move move,
                  out BoardState result, out int timeBonusSeconds)

bool TryClearBlock(LevelContext ctx, BoardState state, int blockIndex,
                   out BoardState result, out int timeBonusSeconds)

bool TrySweepColor(LevelContext ctx, BoardState state, BlockColor color,
                   out BoardState result, out int timeBonusSeconds)
```

`timeBonusSeconds` is the **sum over the whole resolution** — a broom can kill
several bonus-carrying blocks in one pass, and a chain can kill more. It is `0`
when the call returns `false`.

### `ReevaluateConditions`

Signature unchanged. It stops being a no-op and returns `true` when it opened,
unfroze, or unlocked anything.

---

## Design decisions (owner)

**One predicate, not three comparisons.** M2, M3 and M5 ask the same question —
*has this counter reached this value?* — and differ only in which counter. Three
inline comparisons would mean three places to fix when a fourth mechanic wants a
colour-bound threshold, and the drift would be silent. Same reasoning as D28 and
D31.

**The definition types do not change.** `GateDefinition.OpenAtClearCount`,
`BlockDefinition.UnfreezeAtClearCount`, `ShutterDefinition.Threshold` and
`ShutterDefinition.RequiredColor` already carry everything the predicate needs.
Gates and blocks pass `null` for the colour.

*Rejected:* extracting a shared `ClearThreshold` struct onto all three definition
types. Tidier in the abstract, but it changes three types to generalise something
only shutters use today, and neither gates nor blocks have any observed need for
a colour-bound threshold.

**Opening never clears.** This is the rule most likely to be got wrong, because
the wrong version looks reasonable: a gate opens, a compatible block is sitting
in front of it, so clear it. That is not how the game behaves.

Clearing is the result of a **move** (D25). Opening a gate, unfreezing a block
and opening a shutter are state changes, not moves. A block left flush against a
now-usable gate stays there and waits for the player to push it in — a
zero-distance move, the same interaction as a level's opening move.

Observation of the reference game gives exactly four cases where a block sits
flush against a compatible open gate without clearing, and three of them are
this module's:

  1. Authored that way at level start *(already covered by Module 03)*
  2. The block unfreezes while already flush **(M3, here)**
  3. The gate opens while a block waits in front of it **(M2, here)**
  4. A shutter opens, exposing a block already flush **(M5, here)**

`ReevaluateConditions` opens things and stops. It never inspects gate geometry
and never emits a `ColorCleared` event.

**Unlocks are permanent.** Once open, a gate stays open; once unfrozen, a block
stays unfrozen; once open, a shutter stays open. Nothing in the game closes them.
This is what makes the progress vector monotonic (D6), so violating it would
break the solver's stratification, not merely a rule.

**Time is an output, not state (D12).** `Core` has no concept of a countdown. It
reports seconds earned and the caller decides what to do with them: the game adds
them to the clock, the editor sums them when suggesting a level's time budget,
and the solver ignores them entirely.

**Two `out` parameters, not a result struct.** A `MoveOutcome` struct would scale
better if a third output appears, but `TryApplyMove` is the hottest call in the
solver and returning a struct copies where an `out` does not. Two outputs are
still readable. If phase 1.8 needs a third, revisit then — with a real need in
hand rather than a hypothetical one.

**Evaluation order within a pass does not matter.** Each unlock reads counters
and writes its own flag; none reads another's. The fixpoint loop re-runs until
nothing changes, so an unlock enabled by another unlock is caught on the next
pass regardless of the order within this one. Do not add ordering logic.

---

## Left to you

- Where `IsThresholdMet` lives and what the builder exposes so it can be called
  mid-resolution.
- How `ReevaluateConditions` avoids re-scanning everything on every pass. A level
  with many gates and blocks re-checks all of them on each pass of every
  resolution, and most passes change nothing. Correctness first; note what you
  would do if this shows up in a profile.
- How the time bonus is accumulated through the resolution and surfaced to the
  three entry points.
- `MaxResolutionPasses` (D28) stays as it is. `DrainEvents` empties the whole
  event queue before `ReevaluateConditions` runs, and opening a gate, unfreezing
  a block and opening a shutter emit no events, so any action this phase resolves
  in about two passes regardless of how many thresholds one clear crosses — the
  broom cascade included. The existing bound (the sum of every colour stack, plus
  spawns) is now very loose but still correct and cheap to keep. Revisit at phase
  1.13, when generator and elevator spawns create genuinely multi-pass chains.

---

## Tests

**Threshold evaluation**
- The total-count form is met at exactly the threshold, not one before.
- The colour-bound form counts only its own colour: clearing four red does not
  satisfy a threshold of three yellow.
- A colour-bound threshold is satisfied by its colour alone, regardless of how
  many others were cleared.

**M2 — gates**
- A gate with no threshold is open in the initial state.
- A gate below its threshold rejects an exit.
- Crossing the threshold opens the gate within the same resolution.
- Once open it stays open across further moves.

**M3 — frozen blocks**
- A frozen block rejects every move and still obstructs others.
- Crossing the threshold unfreezes it within the same resolution.
- Once unfrozen it stays unfrozen.

**M5 — shutters, threshold half only**
- A closed shutter's region is unreachable and its blocks immovable.
- A global-threshold shutter opens on total count.
- A colour-bound shutter opens on its colour's count and not on others.
- **After opening, a block that was under it is targetable** (`CanBeTargeted`).
  The solver does not use this, but the jokers in phase 5 will, and a break here
  would surface a long way from its cause.

**Opening never clears — the three cases**
- A gate opens while a compatible block sits flush against it. The block is
  **not** cleared. A subsequent zero-distance move clears it.
- A block unfreezes while already flush against a compatible open gate. **Not**
  cleared. A zero-distance move clears it.
- A shutter opens, exposing a block flush against a compatible open gate. **Not**
  cleared. A zero-distance move clears it.

Each of these must assert both halves — that nothing was cleared, *and* that the
zero-distance move then works. Asserting only the first would pass on a resolver
that had broken the exit rule entirely.

**Chains — the fixpoint loop's first real work**
- One clear crosses a gate threshold, and that gate is open by the end of the
  same resolution.
- One clear crosses two thresholds at once (a gate and a frozen block); both
  land in the same resolution.
- A broom clearing several blocks crosses several thresholds at once. **This is
  the primary stress test** — it is the one action that can produce many clears
  in a single pass.
- A colour-bound shutter and a global-threshold gate cross on the same clear.

**M10 — time bonus**
- A block with no bonus reports `0`.
- A block with a bonus reports it **only when it dies**, not on each clear of a
  layered stack.
- A broom killing three bonus-carrying blocks reports the sum.
- A failed move reports `0`.
- The solver's results are unchanged by the presence of time bonuses — add a
  bonus to a corpus board and assert the same optimum. Time must stay outside the
  search space (D12).

**Regression**
- The `ScriptedLoopResolver` seam from Module 03 still passes. It now tests the
  loop mechanism through `CheckSpawnTriggers`, which remains a no-op, while
  `ReevaluateConditions` does real work — both paths still need to sustain the
  loop independently.
