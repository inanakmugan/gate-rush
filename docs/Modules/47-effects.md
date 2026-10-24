# Module 47 — Effects and transitions

**Assembly:** `GateRush.Runtime` (the board's effects, `RuntimeConfig`), `GateRush.UI` and `GateRush.UI.Editor` (the interface's motion, `UiConfig`, the builder), `GateRush.Editor` (new effect sprites from `ArtGenerator`) (tests in `GateRush.Tests`)
**Depends on:** Modules 12, 18, 20, 34, 35, 41, 45, 46
**Phase:** 7.6

---

## Responsibility

Give the finished look of Modules 45 and 46 the motion to match. The
board already shows every change a move causes (Module 18), but its
debris was drawn in the flat Phase 2 style and some moments still cut
instead of move: the win, the gold a win pays, an elevator that has
finished, a button press, a change of screen. This module restyles the
board's effects in the lit model (D80) and adds the missing motion, on
the board and in the interface.

In scope:

- The board's debris redrawn in the lit model: the exit cubes, the ice
  shards and the gate glow (handed over by Module 46), and the sparkle.
- Block clears: the exit through a gate, the peel's cubes, and a
  joker's clear in place, all with the new debris.
- An elevator that has finished its waves leaves instead of vanishing.
- The win: a short celebration on the board before the result panel,
  and the panel's own entrance.
- Gold flying to its counter: the gold a campaign win pays, and gold
  bought in the Store or inside a level.
- Button presses: every button of the interface answers a press.
- Screen changes: between the front end and a level, between tabs, and
  every overlay opening and closing.

Not in scope: any rule, layout, size or timing of play (the drag, the
settle, the two stages of Module 18 keep their order and durations
unless a value here says otherwise); new sound cues and every volume
(7.7, Module 48: this module raises existing cues only, as listed
below); the layered block's outer shade and the joker icons' size (7.8
notes in ROADMAP); the level editor's drawing; ambient effects (snow,
idle shimmer).

---

## Public surface

```
MoveChanges (Module 18, extended)
    IReadOnlyList<int> FinishedElevators   // elevator indices shown
        before the move and not after: the last wave has cleared

BoardView
    void PlayWin(Action onDone)            // the board's celebration;
        onDone when the result panel may open; skipped at once on restart,
        level change, disable

CoinFlight                         plain C#, UI; where each flying coin
    goes and when, for a given seed
    static IReadOnlyList<CoinPiece> Layout(int amount, CoinFlightSettings s,
                                           int seed)
    // CoinPiece: delay, start offset, arc height, duration, and the share
    // of the amount it adds when it lands; the shares sum to the amount

GoldCounter                        UI; a gold label that can be held at an
    old value while coins are in flight
    void Show(int gold)                    // at once
    void Hold(int shownGold)               // keep showing this value
    void Add(int share)                    // a coin landed: count up, pop
    void Release()                         // show the real value at once

PressFeedback                      UI component on every button
    // scales the button's face down on press and back on release, on
    // unscaled time; does nothing on a button that is not interactable

ScreenFade                         UI; one full-screen fader above every
    canvas
    void Play(Action atMidpoint, Action onDone)
    bool IsBusy                            // input to everything below is
                                           // refused while true
```

Names are negotiable. That the coins' layout and the counter's held
value are plain, tested code, not worked out inside a view, is not.

---

## Design decisions (owner)

### Method

- **Looks first, as in Module 46.** Every new or redrawn sprite (cube,
  shard, gate glow, sparkle, the win's burst, the flying coin) is first
  rendered by a small Python reference script in the lit model of D80
  (the same light, ambient, diffuse, specular and occlusion), approved
  by the owner, then ported to `ArtPainter` and `ArtRecipe` and checked
  against the reference by a comparison command, as *Render Lighting
  Comparison* does. The reference scripts and their output stay in the
  git-ignored comparison folder.
- **Motion second.** Timings, eases, counts and distances start from
  values given in the plan and are tuned by the owner in play. The plan
  gives each new field with its starting value and its range.
- **Passes**, each played and reviewed before the next:
  - **Pass 1:** the board's debris redrawn; block clears with it; the
    elevator's exit.
  - **Pass 2:** the win and the gold.
  - **Pass 3:** button presses and screen changes.

### The board's debris (pass 1)

- **Cubes.** A small lit block: the block's body shape at cube size,
  with one stud, shaded by the lit model and tinted by the block's
  colour exactly as a block is, so a cube reads as a broken-off piece
  of the block it came from. Greyscale and tinted at runtime (D46).
- **Shards.** Pieces of the ice's glassy look: the ice colour baked in
  (D80), irregular, with a light edge on the side toward the light.
- **Gate glow.** A soft glow along the gate's inner edge in the gate's
  colour, brighter at its middle, no hard edge; drawn with the board's
  additive look so it lights what is under it rather than covering it.
- **Sparkle.** A small four-pointed white star with a soft halo,
  untinted; used by the win and by gold arriving.
- The Phase 2 versions of these sprites are removed once nothing uses
  them, by name in the plan.
- **Budget.** A burst stays within today's caps per block and per move;
  pieces are pooled or destroyed, never left behind (Module 18's
  rules). The WebGL build on a phone is the target: the plan says the
  most debris renderers alive at once on level 29's busiest move,
  before and after.

### Block clears (pass 1)

- **Through a gate.** As Module 18 resolved: the block slides through
  its gate under a mask while cubes stream from the gate's outer line
  and the gate glows; only the debris changes.
- **A peel.** As Module 20 resolved, with the new cubes and glow.
- **In place (a joker).** The block breaks apart where it stands: the
  new cubes burst outward in every direction from the block's cells,
  with a short white flash on the block as it goes. A Broom's clears
  start together, as today.

### An elevator's exit (pass 1)

- `MoveChanges` reports an elevator shown before the move and not after
  as **finished**, from `VisibilityLayer`, as every other change is
  found.
- In the arrive stage a finished elevator is drawn as a temporary piece
  over the new floor: its doors close if open, then the whole lift
  (rim, hazard stripes, doors) sinks into the floor: it scales down
  toward its region's centre while its soft shadow shrinks with it, and
  fades. Nothing flies.
- It is debris: it never holds `IsBusy`, and restart, level change,
  disable and destroy remove it.

### The win (pass 2)

- When the move that solves the level has finished presenting, the
  board celebrates before the result panel opens: sparkles and a few
  bursts of confetti cubes in the palette's colours rise from the
  board's frame and fall, and the frame pulses once. The celebration's
  length is configured, short (under about a second to the panel's
  start); the panel opens while its tail is still falling.
- The result panel keeps Module 41's pop; on a win its title then
  pops once more, and the coin of the gold row spins in.
- Restart, Next, Menu and every way out of the level stop the
  celebration at once and leave nothing behind.
- The Win cue plays when the celebration starts, not when the panel
  opens.

### Gold flying to its counter (pass 2)

- **Where it applies.** A campaign win that pays gold, from the gold
  row of the result panel to the level HUD's gold pill. A gold pack
  bought in the Store, from the pack's card to the Top Bar's gold. A
  gold pack bought inside a level, from the pack's card to the level
  HUD's gold pill. Gold spent changes the counter at once, as today.
- **The counter is held.** The moment the gold is added to the wallet,
  the counter is held at its old value (`GoldCounter.Hold`); each coin
  that lands adds its share, with a small pop of the pill; when the last
  lands the counter shows exactly the wallet's value. Whatever ends the
  flight early (the panel closed, Next, Restart, the screen changed)
  releases the counter at once to the wallet's value. The saved and
  shown gold never disagree once the flight is over.
- **The coins.** The interface's `coin` sprite, a configured number
  scaled with the amount and capped, laid out by `CoinFlight` from a
  seed: each starts with a small spread around the source, waits a
  short staggered delay, flies on an arc to the counter and shrinks a
  little as it arrives. The shares are whole numbers that sum to the
  amount.
- **Visible counter.** While coins fly, the target counter is drawn
  above any scrim between them, and returns under it when the flight
  ends. The plan says how (a sorting override on the pill, or a copy).
- The coins fly on unscaled time, in the canvas of the counter they fly
  to, and take no input.

### Button presses (pass 3)

- Every button of the interface gets `PressFeedback`: on press its face
  scales to a configured share (about 0.92) with a quick ease, and back
  with a small overshoot on release or when the pointer leaves. The
  icon and label move with the face.
- Built buttons get it from `UiPrefabBuilder`; the hand-made prefabs
  (D78) get it from `HandMadePrefabSettings`, as a step. A test fails
  for any `Button` in a built or hand-made prefab without it.
- The joker buttons keep their lift when selected; the press plays on
  the face inside the lift, so the two never fight.
- A press on a button that is not interactable does nothing.

### Screen changes (pass 3)

- **Front end and level.** Opening a level from the front end, and
  going back, plays one `ScreenFade`: the screen fades to the level's
  background colour, the switch happens at the midpoint (the level is
  built, or the front end shown), and the fade lifts. Input is refused
  for its whole length. Next and Restart inside a level do not fade:
  the board redraws as today.
- **Tabs.** The new page fades in quickly over the old one; the Tab
  Bar's selected tab pops once.
- **Overlays.** Every overlay, in the front end and in the level, opens
  with the one pop of Module 41 (`OverlayPop`) and closes with a short
  reverse: the panel shrinks a little while the overlay fades. The
  navigation state changes at once; a closing overlay takes no input,
  and an overlay opened during another's close is not delayed.
- The streak reward and new joker cards keep their own flows; they
  only gain the shared open and close.

### Sound

No new cues. The existing cues play at these moments: Win when the
celebration starts; UiTap when a coin lands, at most once per a
configured interval, so a flight does not rattle; nothing for presses
beyond today's UiTap on click. New cues and every volume are 7.7.

### Engine rules that carry over

- Every new sprite is generated by `ArtGenerator` from `ArtRecipe`
  (D46, D80); the interface's coin is Module 45's sprite.
- No magic numbers: every duration, ease, delay, distance, scale,
  count, cap and colour is in `RuntimeConfig`, `UiConfig` or
  `ArtRecipe`, with a check in `Problems()` where a value can be wrong.
- No mutable statics. Every tween carries its owner as its id and is
  killed on disable and destroy; `DOTween.Kill` by id, never globally.
  Interface motion runs on unscaled time.
- Randomness from `System.Random` with an explicit seed (the kind, the
  index and the move or purchase number), never `UnityEngine.Random`.
- Every subscription is undone.

---

## Left to you

- How each sprite is painted within the reference the owner approves.
- How the win's confetti and sparkles are laid out (reuse
  `BurstLayout` or a sibling), within the caps.
- How the finished elevator's temporary piece is built from its drawn
  parts.
- How `GoldCounter` is wired to the views that show gold today, and how
  the counter is lifted above a scrim.
- Where `ScreenFade` lives, and how the bootstrap calls it.
- Starting values for every new field. The owner tunes them.
- The owner's Editor steps: menu commands only, listed in order.

---

## Tests

Edit Mode, against the plain classes, the builder and the generator.
The look and the feel are checked by hand.

**`ArtRecipe`, `ArtGenerator`**
- Every new parameter is checked in `Problems()`.
- The generator writes every new sprite at the recipe's size; the same
  recipe gives the same pixels; the cube is tinted, the shard baked in
  the ice colour, the sparkle untinted.

**`MoveChanges`**
- A move that clears the last wave of an elevator reports it finished;
  a move that brings its next wave does not; a plain move reports
  nothing.

**`CoinFlight`**
- The same seed gives the same layout; the count never exceeds the cap;
  the shares are whole and sum to the amount, for amounts of 1, the
  cap, and well over it; every delay and duration is within its
  configured range.

**`GoldCounter`**
- Held, it shows the held value whatever the wallet says; each share
  adds to it; after the last share it shows the wallet's value; Release
  at any point shows the wallet's value at once. This test fails if the
  counter is ever left showing a value other than the wallet's after a
  flight ends.

**`ScreenFade`**
- The midpoint action runs exactly once, before done; `IsBusy` is true
  from Play to done and false after; a second Play while busy is
  refused.

**Builder and hand-made prefabs**
- Every `Button` in a built prefab and in each hand-made prefab has
  `PressFeedback`.

**By hand (owner), per pass**
- Pass 1: cubes, shards and the gate glow match the approved
  references in play; exits, peels and joker clears read clearly; a
  finished elevator sinks away; the busiest move of level 29 is not
  slower on a phone in the web build.
- Pass 2: a campaign win celebrates, then the panel opens, and the coins
  fly to the gold pill, which counts up to exactly the wallet; a gold
  pack in the Store and inside a level does the same to its counter;
  Next, Restart and Menu during the flight leave the right gold shown.
- Pass 3: every button answers a press; the front end and a level fade
  into each other with no input getting through; tabs and overlays open
  and close smoothly; nothing waits on a closing overlay.
