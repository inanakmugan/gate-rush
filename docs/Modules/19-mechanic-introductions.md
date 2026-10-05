# Module 19 — Mechanic introductions

**Assembly:** `GateRush.Runtime` and `GateRush.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 12, 16, 17, 18
**Phase:** 2.4d

---

## Responsibility

Introduce every mechanic the first time the player meets it, with a short
card over the board, as the reference game does: a title, a small
"New Item Unlocked!" line, the mechanic drawn in the game's own art with a
few twinkling sparkles, and one sentence saying how it works. The first
level also gets a "How to Play" card. The look follows the reference; it
is our own drawing (D46).

In scope:

- Working out, from the level files alone, which mechanics a level
  introduces.
- The card: overlay, layout, illustration, sparkles, close, and the queue
  when one level introduces several mechanics.
- Holding the countdown until the last card of a level is closed.
- A board mark for time-bonus blocks (M10), which today have none: a
  player cannot be told about a block they cannot see.

Not in scope, deliberately: remembering across sessions which cards were
seen (Phase 4's save model), a hand pointer or guided first move, jokers'
introductions (Phase 5), localisation.

---

## Public surface

```
LevelMechanic                       enum: HowToPlay, IceBlock, IceDoor,
                                    LayeredBlock, OneWayBlock, LockAndKey,
                                    Shutter, Generator, Elevator, TimeBonus

LevelMechanics                      plain C#, Runtime
    static IReadOnlyCollection<LevelMechanic> Of(LevelContext ctx)
        // every mechanic the level contains: top-level blocks, generator
        // queues and elevator waves alike
    static IReadOnlyList<LevelMechanic> IntroducedBy(
        IReadOnlyList<IReadOnlyCollection<LevelMechanic>> inOrder, int position)
        // the mechanics at `position` that no earlier level has, in enum
        // order; HowToPlay at position 0 only

IntroductionCard : MonoBehaviour     built from code on the canvas, like
                                    ResultPanel
    void Show(IReadOnlyList<LevelMechanic> mechanics, Action onAllClosed)
    void Hide()
    bool IsOpen
```

Names are negotiable. That introductions are derived from the levels' own
content, by plain tested code, and not authored by hand per level, is not:
the owner reorders and adds levels freely, and the cards must follow.

---

## Design decisions (owner)

### Which level introduces what

- A level **contains** a mechanic when any block it can ever show has it —
  top-level, queued in a generator, or in an elevator wave:
  - **Ice Block:** a block with a count threshold (M3).
  - **Ice Door:** a gate with a count threshold (M2).
  - **Layered Block:** a block with two or more colours (M4).
  - **One-Way Block:** an axis-restricted block (M7).
  - **Lock & Key:** a locked block (M8); one card covers both.
  - **Shutter**, **Generator**, **Elevator:** the level has one (M5, M6, M9).
  - **Time Bonus:** a block with a time bonus (M10).
- A level **introduces** the mechanics it contains that no earlier level in
  `LevelCatalog`'s order contains. The first level also introduces
  **How to Play**.
- The bootstrap reads every level once at start-up to work this out. A
  level that fails to load contributes nothing, with a warning naming it;
  the rest still work.

### When cards show

- When a level loads (first load or Next), its cards show one after
  another, in `LevelMechanic` order. A restart shows none: the player has
  just seen them.
- **The countdown does not run while a card is open.** It starts when the
  last card closes. A level with no cards starts as today.
- While a card is open the board takes no drag, and the HUD restart button
  and the R key do nothing: the backdrop covers everything.
- Nothing is remembered across sessions: reopening the game shows the
  cards again on their levels. Persistence comes with Phase 4.

### The card (reference look, our art)

- A full-screen dim over the board (the result panel's backdrop colour and
  alpha), swallowing input.
- **Title** in large Lilita One with an outline, for example "Ice Door!".
  **Subtitle** "New Item Unlocked!" in smaller white Lilita One. How to Play
  uses its own subtitle ("Welcome!").
- **Illustration** in the middle: the mechanic drawn with the game's own
  generated sprites, as it looks on the board — an ice block with its
  badge, an ice gate segment, a layered block showing the colour beneath,
  a block with the axis arrow, a block with chains and padlock beside a
  block with a key, a shutter panel with its badge, a generator machine
  with a block on its screen, lift doors, a block with the time-bonus mark.
  How to Play shows a block and a gate of the same colour with an arrow
  between them.
- **Sparkles:** a handful of small four-point stars around the
  illustration, each twinkling (scale and alpha) on its own phase. A new
  generated sprite.
- **Text box** below: a rounded panel in a cream colour with the frame's
  purple border, one or two lines of dark text.
- A round **close button** (red, white cross) at the top right. A tap
  anywhere on the card also closes it.
- The card opens with the result panel's pop and closes with a short fade.
- Titles and texts live in `RuntimeConfig`, one entry per mechanic, so the
  owner edits them in the Inspector. Starting texts:

| Mechanic | Title | Text |
|---|---|---|
| How to Play | How to Play | Drag each block out through a gate of its colour! |
| Ice Block | Ice Block! | Clear blocks to melt the ice. The number shows how many. |
| Ice Door | Ice Door! | Clear blocks to crack open the Ice Door! |
| Layered Block | Layered Block! | Each exit peels one layer. The colour beneath goes next. |
| One-Way Block | One-Way Block! | This block only slides along its arrow. |
| Lock & Key | Lock & Key! | Clear the block with the key to open the lock of its colour. |
| Shutter | Shutter! | Clear blocks to lift the shutter and see what is beneath. |
| Generator | Generator! | The machine sends its next block when there is room. Its screen shows what comes next. |
| Elevator | Elevator! | Clear every block on the lift to bring up the next wave. |
| Time Bonus | Time Bonus! | Clear this block to win extra seconds. |

### The time-bonus mark (M10)

- A block with a time bonus carries the clock icon (Module 17's sprite) on
  a small badge with "+N", in a bonus colour, at the centre of its
  footprint, placed like the key icon. It shares `MarkLayout`'s rules with
  the other marks; a block that carries both a key and a bonus places both
  without overlap.
- `VisibilityLayer` reports the bonus seconds of a shown block.

### Engine rules that carry over

- The card is built from code under the one canvas, between the HUD and
  the result panel; no hand-made prefab.
- New sprites (sparkle, close button if needed) come from `ArtGenerator` /
  `ArtRecipe` (D46).
- No magic numbers; every size, colour, timing and text in `RuntimeConfig`
  or `ArtRecipe`, checked by `Problems()`.
- No mutable statics; subscriptions undone in `OnDisable`; tweens killed on
  disable and destroy, with unscaled time.

---

## Left to you

- How the illustration is built on the canvas from the board's sprites
  (UI images posed like the board's quarters, or another way that keeps it
  crisp and identical in look to the board). A render texture only with a
  stated reason.
- How the bootstrap holds the countdown and blocks input while a card is
  open, and how it reads every level once.
- The sparkle geometry and twinkle timing.
- Starting values for every new field. The owner tunes them.
- The exact Editor steps for the owner.

---

## Tests

Edit Mode, against the plain classes. The look is checked by hand.

**`LevelMechanics.Of`**
- A level with one plain block contains nothing.
- Each mechanic is found on a top-level block or feature, and also when it
  appears only in a generator queue or an elevator wave (ice, layered,
  one-way, lock, bonus).
- A single-colour block is not layered; a two-colour block is.

**`LevelMechanics.IntroducedBy`**
- The first level introduces How to Play plus everything it contains.
- A later level introduces only what no earlier level contains, in enum
  order.
- A level whose mechanics all appeared earlier introduces nothing.
- Reordering the levels moves the introduction with the first level that
  contains it (the central decision: introductions follow content, not a
  hand-kept list).

**`VisibilityLayer`**
- A shown block reports its time bonus; a block without one reports none.

**`RuntimeConfig`**
- Every `LevelMechanic` has a non-empty title and text; a missing one is a
  problem naming the mechanic.

**By hand (owner)**
- Level 1 opens with How to Play (and any mechanic it contains); the timer
  stays still until the last card closes.
- Each later level shows exactly the cards for its new mechanics, one after
  another; a restart shows none; Next shows the next level's.
- While a card is open, no drag, no restart button, no R.
- Every card reads well on a portrait phone and a landscape desktop.
- Time-bonus blocks carry their mark on the board.

---

## Resolved during implementation

- **Detection.** One pass over `LevelContext`'s flat slot space sees
  top-level blocks, generator queues and elevator waves alike. A feature
  that never shows does not count: a block, gate or shutter whose
  threshold is met at zero clears (asked of Core's own checks), a
  generator with an empty queue, an elevator with no waves.
- **Reading levels.** `BuildCatalog` already parses every level once; it
  keeps each level's mechanics too, laid out in `LevelCatalog.Names`
  order. A level that fails to load keeps its existing error, which now
  says it introduces no mechanic.
- **Holding.** `Load` does not start the run while cards are open; the
  last card's close starts it. `LevelRun` and `Countdown` are unchanged:
  an unstarted countdown ignores ticks. One query, `IntroductionCard.IsOpen`,
  holds the pointer, R and the HUD restart button. `Load` hides the card
  first, dropping its callback, so a run started is always the one the
  cards were shown for.
- **Card.** Built from code between the HUD and the result panel. The
  close button sits in its own row above the title, so a long title never
  runs under it; a tap anywhere also closes. It opens with the result
  panel's pop and closes with a fade.
- **Title.** White, outlined in the frame colour darkened as a lip is
  (`LipFill(FrameColor)`), on the title's own material copy, so no other
  label's material changes.
- **Illustrations.** `MechanicIllustration` draws with UI images over the
  board's sprites, layout rules and cell-relative config; its composing
  recipes mirror `BoardView`'s and must change with them (known debt).
  The Lock & Key card shows one key on the padlock. The How to Play arrow
  has its own size (`introHowToPlayArrowCells`).
- **Sparkles.** Positions, sizes and phases are authored in config;
  nothing is random.
- **Time-bonus mark.** The count badge's shape in bonus colours, the clock
  at its left and "+N". With a key or padlock on the same block the two
  sit side by side (`MarkLayout.PairedMarks`), at a crowded scale where
  the footprint has no room; the opening-lock effect uses the same
  position. `Problems()` checks overlap up to a two-digit bonus.
- **Badge padding.** `RuntimeConfig` first lacked a public
  `BadgePaddingCells` getter, which the new mark needs; added.
