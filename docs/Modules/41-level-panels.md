# Module 41 — Result panel, confirmation and introduction card

**Assembly:** `GateRush.Runtime`, `GateRush.UI`, `GateRush.UI.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 19, 23, 32, 37, 40
**Phase:** 6.7

---

## Responsibility

Finish Phase 6 by moving the last code-built screens of the level to the
new theme (D74). The result panel and the leave and restart confirmation
become prefabs in `GateRush.UI`, reached through `ILevelUi` like the HUD
(Module 40). The confirmation names the win streak about to be lost. The
introduction card keeps its drawings and its flow in `Runtime` and only
takes the theme's panel, font and close button.

In scope:

- The result panel and the confirmation as overlays of Level UI, written
  by the builder; `ResultPanel` and `ConfirmPrompt` and their
  `RuntimeConfig` fields removed.
- The streak line on the confirmation.
- The introduction card restyled in place.

Not in scope: the streak on the result panel (D64, D74), new result
animations or stars (Phase 7), any change to what Next, Restart, Menu or
the confirmation do (D57, D58, D64).

---

## Public surface

```
ILevelUi                            existing (Module 40), gains
    void ShowWin(LevelResult result)
    void ShowLoss(LevelResult result)
    void HideResult()
    void ShowConfirm(ConfirmKind kind, int streakAtRisk)
    void HideConfirm()
    bool IsConfirmOpen { get; }
    event Action NextRequested
    event Action ResultRestartRequested, ResultMenuRequested
    event Action ConfirmLoseLife, ConfirmKeepPlaying

LevelResult                         readonly struct, Runtime
    // what the panel shows: the outcome, the gold a campaign win paid,
    // whether Next and Restart are offered, and the closing message after
    // the last level

ConfirmKind                         enum, Runtime: Leave, Restart

ResultOverlay, ConfirmOverlay       MonoBehaviours, UI
```

Names are negotiable. That the bootstrap still decides every outcome,
that `Runtime` never references `GateRush.UI`, and that the confirmation
keeps stopping the level through the one pause of Module 40, is not.

---

## Design decisions (owner)

### The result panel

- An overlay of Level UI over the board, in the theme's frame (scrim,
  grey panel, purple title), with no close button: the buttons are the
  way out.
- **Win:** the title "Level Complete"; under it, in the campaign, the gold
  earned as a coin and "+50"; after the last campaign level, the closing
  message. Buttons: **Next** (green, Button Primary) when a level
  follows, **Restart** (blue, Button Secondary) unless the win was a
  campaign win, and **Menu** (blue round, home icon).
- **Loss:** the title "Time's Up". Buttons: **Restart** (green) and
  **Menu**.
- Which buttons show is decided by the bootstrap, as today; the panel
  only draws `LevelResult`.
- It opens with a short scale-and-fade pop, as today, on unscaled time.
- Escape does nothing while it is open: there is no "back" from a result.

### The confirmation

- An overlay of Level UI in the theme's frame, with no close button.
- The title "Leave level?" or "Restart level?", then the line:
  - "You will lose a life." when the streak is 0;
  - "You will lose a life and your 4-win streak." when it is above 0,
    with the streak from the save.
- Buttons: **Lose a life** (red) and **Keep playing** (green).
- It stops the level through `IsPaused` (Module 40) and clears the joker
  selection, as today. Escape counts as Keep playing.

### The introduction card

- Stays in `Runtime`: its drawings come from `MechanicIllustration` and
  the game's own art (D46), which `GateRush.UI` cannot draw.
- Takes the theme: the grey 9-sliced panel, the Fredoka font asset with
  its Nunito fallback, the purple title colour, and the red round close
  button with the cross. The dim, the sparkles, the illustration, the
  sentence and the order of cards are unchanged.
- The sprites and the font come from configuration, so nothing is drawn
  by hand.

### The builder

- It writes the Result and Confirm overlays and nests them in Level UI's
  overlay stack, inactive. Hand-made prefabs stay unwritten.

### Configuration

- `UiConfig`: the result titles, the button labels, the gold earned
  format, the closing message, the confirmation titles, the two loss
  lines, checked in `Problems()`.
- `RuntimeConfig`: the fields only `ResultPanel` and `ConfirmPrompt` read
  go, with their tests; the introduction card's look fields change to the
  theme's sprites and font; anything still read elsewhere stays and is
  named in the plan.

---

## Left to you

- How `LevelResult` is built from what the bootstrap knows today.
- Whether the result and the confirmation sit in Level UI's existing
  overlay stack or beside it, as long as Escape and the pause behave as
  above.
- How the introduction card receives the theme's sprites and font from
  configuration.
- The prefab hierarchies and sizes, as before.

---

## Tests

**`LevelResult`**
- A campaign win with a level to follow, the last campaign level, a free
  play win, and a loss each offer the right buttons, gold and message.

**Texts, `UiConfig`**
- The confirmation's line with a streak of 0, 1 and 4; the gold line;
  every new text that is empty is reported.

**`RuntimeConfig`**
- The removed fields' tests go; the introduction card's new look fields
  are checked.

**Builder**
- The two overlays are written wired and nested in Level UI; Home Page
  and Top Bar stay unwritten.

**By hand (owner)**
- Winning a campaign level shows Level Complete, the coin and +50, Next
  and Menu; Next starts the next level.
- Winning in free play shows Restart; losing shows Time's Up, Restart and
  Menu.
- Winning the last campaign level shows the closing message.
- With a streak of 4, Menu during a level opens "Leave level?" with "You
  will lose a life and your 4-win streak."; Keep playing resumes the
  countdown; Lose a life leaves and the streak is 0. With no streak the
  line has no streak.
- Escape on the confirmation keeps playing; Escape on the result panel
  does nothing.
- A level with a new mechanic shows the introduction card in the new
  theme with its drawing, and closes as before.
- The layout holds on a tall phone and in the web build's window.
