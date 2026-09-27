# Module 12 — Polish: movement, clearing, countdown and results

**Assembly:** `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Module 11
**Phase:** 2.2

---

## Responsibility

Turn the 2.1 skeleton into something that feels like the game: blocks glide
instead of snapping, a cleared block visibly leaves through its gate, the
level runs against a countdown, and the level ends on a result panel.

In scope:

- Block movement animated with DOTween while dragging.
- A clear effect for every block cleared by the resolution.
- The level countdown, including M10 time bonuses.
- A result panel for a win ("Level Complete") and a loss ("Time's Up"), with
  Restart and, after a win, Next.

Not in scope, deliberately: block outlines, the generator's incoming-block
preview (M6), the time-bonus popup, open/unfreeze/shutter animations, lives,
gold, the quit prompt and menus. Those belong to later phases.

---

## Public surface

As in 2.1, every decision lives in plain C# classes with Edit Mode tests; the
MonoBehaviours only wire them to Unity and DOTween.

```
Countdown                          plain C#
    Countdown(float budgetSeconds)
    float RemainingSeconds
    bool IsRunning
    bool HasExpired
    void Start()
    void Stop()                        // freezes the remaining time
    void Tick(float deltaSeconds)      // no effect unless running
    void AddBonus(int seconds)         // M10; no effect once expired
    event Action Expired               // raised once

LevelSession (Module 11, extended)
    event Action<int> TimeBonusEarned  // seconds from a resolution, when > 0

ResolutionDiff                     plain C#
    static IReadOnlyList<ClearedBlock> Between(LevelContext ctx,
                                               BoardState before,
                                               BoardState after)
    // ClearedBlock: block index, the colour removed, whether it died,
    // and the edge of the gate it was cleared through

StepPlayback                       plain C#
    void Enqueue(Coord origin)         // from DragController.Stepped
    ... the order and speed at which queued steps are shown

LevelCatalog                       plain C#
    LevelCatalog(IEnumerable<(string name, int levelId)> levels)
    bool TryGetNext(int levelId, out string name)   // smallest greater id
```

Names are negotiable; that each of these decisions is a plain, tested class
is not.

---

## Design decisions (owner)

### Movement follows the drag, step by step

- `DragController.Stepped` (Module 11) feeds `StepPlayback`. The block's view
  plays the queued steps **in order**, each as a short tween of one cell. It
  never tweens straight to a later origin, so a block is never shown crossing
  a cell it did not pass through.
- When the queue grows — a fast drag — the per-step duration shortens so the
  view never trails the logical origin by more than a configured number of
  cells. It catches up; it does not skip.
- On release the view finishes its queue before the board is redrawn, so a
  released block does not jump.
- Durations, the catch-up limit and easing come from `RuntimeConfig`.

### A cleared block leaves visibly

- After a resolution, `ResolutionDiff` compares the states before and after
  and lists every block whose outer colour was removed. In play that is only
  ever the dragged block, at the gate it was pushed into: nothing in this
  game clears a block in place. (Jokers will, and get their own effects in
  Phase 5.)
- **A destroyed block** shrinks and fades toward the gate it was cleared
  through.
- **A layered block that survives** plays a short "peel" on the removed outer
  colour, then shows the next one.
- The board is redrawn from the new state when the effects finish. Input is
  ignored while they play, so a drag can never start from a stale picture.
  Their durations are short and come from `RuntimeConfig`.
- Spawns and condition changes still appear instantly, as in 2.1.

### The countdown

- The budget is the level's `SuggestedTimeBudgetSeconds`. The countdown
  **starts when the level loads** and ticks with unscaled frame time.
- A resolution that destroys a time-bonus block adds its seconds (M10), through
  `LevelSession.TimeBonusEarned`; `MoveResolver` already reports them.
- At zero the level is lost: the countdown stops, input stops, and the
  "Time's Up" panel shows. A win stops the countdown the moment the state
  reads solved. Whichever comes first decides; the other never follows.
- A level whose budget is not positive logs an error naming the level and
  runs without a countdown, so an unfinished level is still testable.
- Time stays outside `Core` (D12, Core concept 5): the countdown is a
  `Runtime` class and never enters `BoardState`.
- The remaining time is shown at the top of the screen, whole seconds.

### The result panel

- A screen-space uGUI canvas with a TextMeshPro title and buttons.
- **Win:** "Level Complete", with **Next** and **Restart**. **Loss:**
  "Time's Up", with **Restart**.
- **Restart** rebuilds the level from its initial state and restarts the
  countdown; **R** does the same, as in 2.1.
- **Next** loads the level with the smallest `levelId` greater than the
  current one among the JSON files in `Resources/Levels`, found through
  `LevelCatalog`. After the last level, Next is hidden. Levels are loaded with
  `Resources.LoadAll<TextAsset>("Levels")`, which works on WebGL; the
  bootstrap's `TextAsset` field still picks the first level.
- Gold, lives and the quit prompt are Phase 4 and 6 work; the panel shows
  none of them.

### Engine rules that carry over

- Enter Play Mode runs without a domain reload, and DOTween keeps static
  state. Every tween a component starts is killed when that component is
  disabled or destroyed; no mutable statics of our own.
- Every subscription in `OnEnable` is undone in `OnDisable`.
- No magic numbers: every duration, colour, size and limit is in
  `RuntimeConfig`.

---

## Left to you

- How `StepPlayback` shortens durations under a backlog, and whether it is
  driven by DOTween callbacks or by `Update`.
- The exact look of the clear and peel effects, within the constraints above.
- How the view waits for effects before redrawing (a DOTween sequence, a
  callback, or a small state flag).
- Layout of the timer and the panel. Placeholder styling is fine.
- The scene and asset steps the owner must do (a canvas, TMP, any prefab):
  list them precisely at the end.

---

## Tests

**`Countdown`**
- Does not tick before `Start`; ticks down after it; never goes below zero.
- Raises `Expired` exactly once, at zero, however large the last tick.
- `Stop` freezes the remaining time; ticks after it change nothing.
- `AddBonus` extends a running countdown; after expiry it changes nothing.

**`LevelSession`**
- `TimeBonusEarned` fires with the block's bonus when a move destroys a
  time-bonus block, and not for a clear that leaves the block alive.

**`ResolutionDiff`**
- A single-colour block cleared at a gate is listed as destroyed, with that
  gate's edge.
- A layered block cleared at a gate is listed as surviving, with the removed
  colour.
- A move that clears nothing gives an empty list.

**`StepPlayback`**
- Steps come out in the order they went in; none is skipped.
- A backlog above the limit shortens the per-step duration; an empty or short
  queue plays at the configured duration.

**`LevelCatalog`**
- Next is the smallest greater id, not the next file name.
- The highest id has no next.
- Ids need not be contiguous: with 0, 1, 3 and 4, the next after 1 is 3.
