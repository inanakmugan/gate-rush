# Module 17 — HUD and result panel

**Assembly:** `GateRush.Runtime` and `GateRush.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 12, 15, 16
**Phase:** 2.4c, part 1

---

## Responsibility

Replace the bare timer number and the placeholder result panel with a HUD
and a result panel in the game's style (D46): a timer in minutes and
seconds on a pill with a clock icon, the level number, an on-screen restart
button, the Lilita One font everywhere, and a styled panel for a win or a
loss.

In scope:

- The top HUD: restart button, timer pill, level pill.
- The result panel: backdrop, panel, title, buttons, and a short opening
  pop.
- The font: Lilita One for the HUD, the result panel and every count on the
  board.
- Two generated icons (clock, restart) and the HUD's use of the existing
  rounded-box sprite.
- The HUD and panel respecting the screen's safe area.

Not in scope, deliberately: every board animation and the grabbed-block
highlight (part 2 of 2.4c), gold, lives, pause, jokers and menus (Phases
4–6), audio (Phase 7).

---

## Public surface

```
HudView : MonoBehaviour            builds and updates the HUD under a canvas
    void Initialize(RuntimeConfig config)
    void ShowLevel(int levelNumber)
    void ShowTime(float remainingSeconds)     // hidden when the level has no countdown
    event Action RestartRequested

ResultPanel (Module 12, rebuilt)   builds its panel from code; same events
    void ShowWin(string title, bool hasNext)
    void ShowLoss(string title)
    void Hide()

TimeFormat                         plain C#
    static string MinutesSeconds(float seconds)   // "02:30"; rounds up, never below "00:00"

LevelCatalog (Module 12, extended)
    int NumberOf(string name)      // 1-based position in id order

SafeArea : MonoBehaviour           fits its RectTransform to Screen.safeArea
```

Names are negotiable. That the HUD and panel are built from code — not laid
out by hand in the scene — is not: the layout then lives in reviewable code
and `RuntimeConfig`, and the scene keeps one canvas.

---

## Design decisions (owner)

### The HUD

- **Top band only.** The HUD sits in the top band the camera already keeps
  free (`topBandScreenFraction`, Module 15), inside the safe area. The
  bottom band stays empty for Phase 5's jokers.
- **Restart, left:** a rounded square button in the frame's purple with the
  restart icon. It does what R does, at any moment, effects included
  (Module 12). R keeps working.
- **Timer, centre:** a dark pill with the clock icon and the remaining time
  as `mm:ss`, rounded up so it reads `00:00` only at expiry. Below a
  configured number of seconds the digits turn a warning red. A level with
  no countdown hides the pill.
- **Level, right:** a pill reading "Level N", where N is the level's
  1-based position in `LevelCatalog`'s id order, so ids need not be
  contiguous.
- Sizes, colours and spacing come from `RuntimeConfig`, in canvas units of
  a portrait reference resolution.

### The result panel

- A full-screen backdrop, black at partial alpha, that swallows input to
  the board beneath.
- A rounded panel in the frame's purple with the title in large Lilita
  One: "Level Complete" or "Time's Up", as today.
- **Win:** Next (green) and Restart (orange). After the last level Next is
  hidden, as today. **Loss:** Restart only.
- The panel opens with a short scale-and-fade pop (DOTween, unscaled time,
  killed on disable), its duration and ease in `RuntimeConfig`.
- Timing is unchanged from Module 12: a win shows after the clear effects,
  a time-out at once.

### The font

- **Lilita One** (Google Fonts, SIL Open Font License). The owner adds the
  TTF and its licence file under `Assets/Art/Fonts/` and creates a
  TextMeshPro font asset from it. That asset is `RuntimeConfig`'s label
  font, used by the board's badges and by the HUD and panel.

### Icons

- `ArtGenerator` paints two more greyscale sprites from `ArtRecipe`: a
  clock face with two hands, and a circular restart arrow. Both are white,
  tinted where placed. Buttons and pills reuse the rounded-box sprite
  (Module 16), 9-sliced in UI `Image`s.

### Engine rules that carry over

- Every subscription in `OnEnable` is undone in `OnDisable`; every tween is
  killed when its component is disabled or destroyed; no mutable statics.
- No magic numbers: sizes, colours, spacing, the warning threshold and the
  pop's timing are in `RuntimeConfig`.
- The EventSystem keeps `InputSystemUIInputModule` (Module 12). A press on a
  HUD button must not also start a drag on the board beneath it.

---

## Left to you

- How `HudView` and `ResultPanel` build their hierarchies, and the canvas
  scaler settings (a portrait reference resolution; match between width and
  height as fits a phone and a desktop browser).
- How a press on a UI element is kept from starting a board drag.
- The icon geometry and the new `ArtRecipe` fields.
- Starting values for every new field. The owner tunes them.
- The exact Editor steps: the font asset, generating the icons, assigning
  sprites and font, and the scene change (what to remove, what to add).

---

## Tests

Edit Mode, against the plain classes. The look is checked by hand.

**`TimeFormat`**
- 150 s reads `02:30`; 59.2 s reads `01:00` (rounded up); 0 and negative
  read `00:00`; 600 s reads `10:00`.

**`LevelCatalog.NumberOf`**
- Ids 0, 1, 3, 4: the level with id 3 is number 3.
- An unknown name has no number (or throws — state which).

**Art**
- The two icons are byte-identical across runs; the recipe's new checks.

**By hand (owner)**
- The HUD sits in the top band on a portrait phone aspect and a landscape
  desktop aspect, inside the safe area, and never overlaps the board.
- Restart works from the button during play, during effects and after a
  result; a press on it does not move a block.
- The timer counts down in `mm:ss`, turns red near the end, and hides on a
  level with no countdown.
- The win and loss panels open with the pop; Next and Restart work; the
  board cannot be dragged behind the panel.
- Every count on the board uses Lilita One.
