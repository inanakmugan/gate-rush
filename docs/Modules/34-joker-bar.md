# Module 34 — Joker bar

**Assembly:** `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 18, 19, 23, 25, 32, 33
**Phase:** 5.2

---

## Responsibility

Make the jokers playable. A bar of three buttons under the board shows
each joker's count, or its price in the campaign when none is left. The
Clock's button adds its seconds at once; the Rocket's and the Broom's
select the joker, and a tap on a block uses it (D67). The bootstrap spends
the joker the way D65 and D66 say and only after the use is known to be
valid, applies it through the session, saves, and the board presents the
clear in place with the effects it already has.

The bar is prototype front end like the rest of D64: plain text in the
existing button style, which Phase 6 replaces. The selection rules and the
presentation on the board stay.

In scope:

- `JokerBar`, the three buttons in the bottom band, and the plain helpers
  that decide each button's text and whether it can be pressed.
- The selection (D67) and the input path from a tap to a joker.
- Spending in the campaign and in free play, and saving.
- `MoveChanges` for a joker, and the board's in-place clear.
- Sounds and haptics from the existing cues.

Not in scope: the store's screen and Phase 6's look of the bar; new sounds
or art; a keyboard shortcut for jokers; the streak shown anywhere (D64).

---

## Public surface

```
JokerBar                            MonoBehaviour, Runtime/UI
    void Initialize(RuntimeConfig config)
    void Show()
    void Hide()
    void ShowButton(JokerKind kind, JokerButton button)
    void ShowSelected(JokerKind? kind)
    event Action<JokerKind> JokerPressed
    event Action ButtonPressed

JokerButton                         readonly struct, plain C#, Runtime
    string Label
    bool IsInteractable

JokerButtons                        static, plain C#, Runtime
    static JokerButton Campaign(JokerKind kind, int held, int price, int balance, bool isUsable, RuntimeConfig texts)
    static JokerButton FreePlay(JokerKind kind, int left, bool isUsable, RuntimeConfig texts)
        // isUsable: the level is being played, nothing holds it, and for
        // the Clock it has a countdown

JokerSelection                      sealed class, plain C#, Runtime
    JokerKind? Selected { get; }
    void Press(JokerKind kind)       // Rocket or Broom: select, unselect when
                                     // already selected, or switch
    void Clear()

MoveChanges                         existing
    static MoveChanges ForJoker(LevelContext ctx, BoardState before, BoardState after)

ClearedBlock                        existing
    gains: whether it was cleared in place, with no gate

InputController                     existing
    while a joker is selected, a press on the board raises
    event Action<Coord> BoardTapped instead of starting a drag

RuntimeConfig                       existing
    every new text, format string, size and colour, checked in Problems()

LevelBootstrap                      existing
    [SerializeField] JokerBar
```

Names are negotiable. That a joker is spent only after its use is checked,
that pressing a button spends nothing, and that the board presents a joker
through the same `Present` call as a move, is not.

---

## Design decisions (owner)

### The bar

- Three buttons in one row in the bottom band
  (`RuntimeConfig.BottomBandScreenFraction`), which is empty today, in the
  order Clock, Rocket, Broom, within the safe area as the HUD is.
- Campaign: a button shows the joker's name and how many are held, for
  example "Rocket 2". With none held it shows the price instead, "Rocket
  200 gold", and can be pressed only when the balance covers the price.
- Free play: the name and how many are left of the level's allowance
  (D66). At 0 it cannot be pressed.
- No button can be pressed while the level is not being played (a result
  is showing, or the countdown ran out), while an introduction card or the
  confirmation is open, or, for the Clock, in a level without a countdown.
- The selected joker's button shows a highlight colour from
  `RuntimeConfig`.
- The bar is shown with the HUD while a level is loaded and hidden on the
  menu. Its buttons raise `ButtonPressed` for the tap sound, as the HUD's
  do.
- The labels are refreshed after every use, purchase, win, loss and
  restart, and whenever the gold changes.

### Selecting and using (D67)

- The Clock's button uses it at once: spend, then
  `Countdown.AddBonus(rules.ClockBonusSeconds)`, shown beside the timer
  with the HUD's existing time bonus text and the `TimeBonus` cue. It also
  clears any selection.
- The Rocket's and the Broom's buttons go through `JokerSelection.Press`:
  select, unselect on a second press, switch on the other button. Nothing
  is spent.
- While a joker is selected, a press on the board raises `BoardTapped`
  with the cell instead of starting a drag. A press on the UI stays the
  UI's. A press while the view is busy is ignored, as a drag would be, and
  the selection stays.
- The block on the tapped cell is found the way the drag finds the block
  it grabs. Rocket: valid when `CanRocket(block)`. Broom: the colour is
  the one the player sees on that block
  (`VisibilityLayer.Block(...).OuterColor`); a block with no shown colour
  (frozen, or not shown) cannot choose it; valid when `CanSweep(colour)`.
- An invalid tap does nothing: no sound, no spending, the selection stays.
- A valid tap spends, applies, clears the selection, saves when the
  campaign changed, and presents.
- Menu, Restart (R too), opening the confirmation, the countdown running
  out, a win, and loading a level all clear the selection.

### Spending (D65, D66)

- The order is fixed: check (`CanRocket`, `CanSweep`, or the Clock's
  playable-with-a-countdown check), then spend, then apply (`TryRocket`,
  `TrySweep`, `AddBonus`), then save.
- Campaign: `MetaGame.TryUseJoker(kind)`. `Used` or `BoughtAndUsed`:
  apply, then `SaveOrWarn`. `NotEnoughGold` cannot happen when the button
  was pressable; if it does, log an error, apply nothing and clear the
  selection.
- Free play: `FreePlayJokers.TryUse(kind)`; false logs an error, as above.
  Nothing is saved. The bootstrap creates the allowance from
  `MetaConfig.FreePlayJokersPerLevel` and refills it whenever a level is
  loaded or restarted in free play, development tools included.
- A `TryRocket` or `TrySweep` that returns false after a valid check is a
  bug: log an error. The joker is already spent; this is not undone.
- Restart in the campaign does not give back jokers spent in the attempt.

### Presentation

- `MoveChanges.ForJoker` reports every block whose outer colour the joker
  removed as an in-place clear, with its removed and exposed colour, and
  everything else exactly as `Between` does for a move: thaws, openings,
  unlocks, badge pops and spawns.
- The board presents a joker through `BoardView.Present`, like a move, so
  the leave stage, then the arrive stage, and `IsBusy` behave the same.
- A destroyed block breaks apart where it stands: the shard burst the
  board already plays for a thaw and a gate's ice (`BurstLayout.Pieces`),
  in the block's colour, while the block fades. It does not travel and no
  gate glows.
- A layered block that survives plays the existing peel (`PeelEffect`) to
  its exposed colour, without the bump toward a gate and without the
  gate's glow.
- A Broom's clears all start together.
- Cues: a destroyed block plays `Exit` with the `Exit` haptic pulse; a
  peel plays `Peel`; the arrive stage plays its usual cues; a time bonus
  from destroyed bonus blocks shows and sounds as after a move. No new
  sound or art.
- A joker that wins the level records the win the moment it is applied and
  opens the panel after the presentation, as a move does. The bootstrap
  shares one path for a move and a joker after the state changed.

### Engine rules that carry over

- No magic numbers or strings: every text, format string, size and colour
  is a `RuntimeConfig` field with a check in `Problems()`. Prices and
  counts come from `MetaRules`, `MetaGame` and `FreePlayJokers`.
- No mutable statics. UI animations on unscaled time. Every new
  subscription has its unsubscription.
- `JokerBar` is built from code on the screen-space canvas, as
  `ResultPanel` and `ConfirmPrompt` are, under the result panel and the
  confirmation.

---

## Left to you

- How `ClearedBlock` tells an in-place clear from a gate clear, and how
  the board's leave stage branches on it.
- Whether the block lookup of `DragController` becomes shared or
  `BoardTapped` carries the block.
- The bar's layout within the bottom band, the highlight's colour and the
  labels' exact format strings.
- The exact owner Editor steps, including assigning the Joker Bar on
  `Game` with its full Hierarchy path.

---

## Tests

**`JokerButtons`**
- Campaign with some held: the name and the count, pressable. With none
  held: the price, pressable exactly when the balance covers it. Not
  usable: not pressable, whatever is held.
- Free play: the name and what is left, pressable above 0; at 0, or not
  usable, not pressable.
- Every text comes from the config's format strings; prices and counts are
  never written into a text.

**`JokerSelection`**
- Nothing is selected at first. Press selects; pressing the same kind
  again unselects; pressing the other kind switches. `Clear` unselects.
  Pressing the Clock, or an undefined kind, throws.

**`MoveChanges.ForJoker`**
- A Rocket destroying a block: one in-place clear with no exposed colour.
  A Rocket on a layered block: one in-place clear with the exposed colour.
- A Broom: one in-place clear per swept block, and none for a block under
  a closed shutter.
- The chain board of Module 33: the thaw, the gate and shutter openings
  and the elevator's spawn are reported as for a move, and the block the
  opened shutter shows is revealed, not spawned.

**`RuntimeConfig`**
- Each new text or format string that is empty, and each new size or
  colour out of range, is reported by `Problems()`.

**By hand (owner)** — the central checks of this module are in a scene:
- Campaign: each button shows its count; with none held it shows the price
  and is not pressable when the gold is short.
- Clock: pressing it adds the configured seconds, shows them beside the
  timer, and spends one; with none held and enough gold, it spends the
  price and the count stays 0.
- Rocket: pressing it highlights it; pressing again unhighlights it and
  nothing is spent. Selected, a tap on an empty cell or a block under a
  closed shutter does nothing; a tap on a block clears its outer colour in
  place, the block breaking apart or peeling, and spends one.
- Broom: a tap on a frozen block does nothing; a tap on a block sweeps
  that colour from every targetable block at once. A sweep that opens a
  gate, a shutter or a frozen block plays those openings after the clears.
- While selected, the timer keeps running and dragging is not possible;
  Menu, Restart, R and the confirmation clear the selection.
- A joker that clears the last block wins the level, and the campaign win
  is paid as usual.
- Free play: three of each, no gold involved; the allowance is full again
  after Restart and on the next level; at 0 the button cannot be pressed.
- Persistence: jokers spent and gold spent on them survive stopping and
  pressing Play again.
