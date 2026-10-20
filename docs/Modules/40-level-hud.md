# Module 40 — Level HUD, joker bar and in-level gold

**Assembly:** `GateRush.Runtime`, `GateRush.UI`, `GateRush.UI.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 32 to 39
**Phase:** 6.6

---

## Responsibility

Move the two parts of the level's interface that stay on screen while a
level is played, the HUD and the joker bar, from code-built prototypes in
`Runtime` to prefabs in `GateRush.UI` in the Phase 6 theme (D68, D74). Add
the in-level gold loop: in the campaign, a joker the player holds none of
and cannot afford opens Not Enough Gold over the level, with the countdown
stopped, and Get Gold opens the gold packs there, so the player buys gold
and goes on playing without leaving the level (D72, D74).

In scope:

- `ILevelUi` in `Runtime`, the one way the bootstrap shows and hears the
  level's HUD, joker bar and in-level overlays; implemented in
  `GateRush.UI`, as `IFrontEnd` is.
- The HUD and the joker bar as prefabs, written by the builder.
- The in-level overlay stack: Not Enough Gold, the gold packs, the test
  purchase sheet and the message, reusing the front end's prefabs.
- Stopping and resuming the countdown and the board around them.
- Removing `HudView`, `JokerBar` and their `RuntimeConfig` fields.

Not in scope: the result panel, the leave and restart confirmation and the
introduction card (6.7, Module 41); new joker art (the stand-ins of Module
36 stay); any change to the joker rules (D65 to D67).

---

## Public surface

```
ILevelUi                            interface, Runtime
    string MissingSetup()
    void Show(LevelUiState state)   // the HUD and the bar for a level
    void Refresh(LevelUiState state)
    void ShowTime(float remainingSeconds)
    void ShowTimeBonus(int seconds)
    void Hide()
    void ShowNotEnoughGold(int shortfall)
    void ConfirmTestPurchase(GoldOffer offer, Action<bool> answer)
    void ShowPurchaseFailed()
    bool IsOverlayOpen { get; }     // the countdown and the board stop while true
    event Action RestartRequested, MenuRequested
    event Action<JokerKind> JokerPressed
    event Action<string> GoldPackRequested
    event Action OverlaysClosed     // the last in-level overlay closed
    event Action ButtonPressed

LevelUiState                        readonly struct, Runtime
    level number or free play, lives, cap, wait to the next life, gold,
    the three joker buttons (JokerButton), the selected joker,
    the gold offers

LevelUi                             MonoBehaviour, UI; implements ILevelUi
LevelHud, JokerBarView              MonoBehaviours, UI

LevelBootstrap                      existing
    [SerializeField] MonoBehaviour levelUi     // must implement ILevelUi
```

Names are negotiable. That `Runtime` never references `GateRush.UI`, that
the views hold no rules, and that the joker buttons' text and state still
come from `JokerButtons` in `Runtime`, is not.

---

## Design decisions (owner)

### The HUD

- In the top band (`ScreenBands`), left to right:
  - two round buttons: Menu (home icon) and Restart (return icon), as
    today, raising the same events;
  - the timer pill in the centre, with the hourglass icon and "m:ss"; the
    time bonus "+20" pops beside it as today;
  - on the right, "Level N" in the campaign or "Free Play".
- Under them, a row of two pills as in the top bar: the gold with the coin
  and the lives with the heart and its count, with "Full" or the wait. In
  free play the lives pill is not shown (D58). Neither pill is pressable in
  a level.
- Texts, formats and colours come from `UiConfig`; the prototype's
  `RuntimeConfig` fields for the HUD go.

### The joker bar

- In the bottom band, three large buttons: Clock, Rocket, Broom, with
  their icons from `JokerIcons` (Module 37).
- Each button carries a badge: the count held, or in the campaign with
  none held the price with the coin. A button that cannot be pressed is
  greyed. The selected joker (D67) is drawn raised with a highlight ring.
- The labels and states come from `JokerButtons` as today; only the look
  changes.

### In-level gold (D74)

- In the campaign, a joker with none held and a price above the balance
  can be pressed. Its press, whatever the kind, opens Not Enough Gold with
  the shortfall, instead of selecting or using the joker. A joker with
  none held that the balance covers behaves as today (D65, D67).
- Not Enough Gold, the gold packs, the test purchase sheet and the message
  form a small overlay stack over the level, the same prefabs as the front
  end's. **Get Gold** opens the gold packs; a pack goes through the
  purchase service and its test sheet as on the Store (D72).
- While any of them is open the countdown stops, the board takes no input
  and any selected joker stays selected; when the last one closes the
  countdown resumes, as after the leave confirmation (D64).
- After buying gold the player presses the joker again; nothing is bought
  or used for them on return.
- Free play is unchanged: at 0 the button cannot be pressed (D66).

### The builder

- It writes the Level HUD, the Joker Bar and Level UI prefabs. Level UI
  holds the HUD, the bar and an overlay stack with nested instances of
  Not Enough Gold, a Gold Packs overlay built for the level from the Gold
  Pack Card, Test Purchase and Message.
- The owner places Level UI under the scene's Canvas and assigns it to
  `LevelBootstrap`, as Front End was in Module 35.
- The bands keep their sizes from `ScreenBands`; the prefabs fill them.

### Configuration

- `UiConfig`: every new text and format (Free Play, the level and timer
  formats, the time bonus format, the joker badge formats, the gold packs
  title), checked in `Problems()`.
- `RuntimeConfig`: the HUD's and the joker bar's fields that only the
  prototypes used are removed, with their tests; any still read elsewhere
  stays and is named in the plan.

---

## Left to you

- How `LevelUiState` carries the joker buttons and the selection, as long
  as `JokerButtons` still decides them.
- How the bootstrap's existing pause for the leave confirmation is shared
  with the in-level overlays, so the countdown and the board stop for
  either.
- The prefab hierarchies and sizes, as before, and how the overlays are
  shared with the front end's without the front end's code.
- What the in-level gold packs overlay looks like within the theme: a
  short list of the gold pack cards, the test store line, a close button.

---

## Tests

**`JokerButtons`**
- In the campaign with none held and a price above the balance, the
  button can be pressed and says so; with a price the balance covers it
  behaves as today; free play is unchanged.

**`LevelUiState`**
- The level number or free play, the lives, the wait, the gold, the
  buttons and the offers come from the session, the save and the rules.

**Texts, `UiConfig`, builder**
- The HUD's texts from the config; each new text that is empty is
  reported; the new prefabs are written wired; Home Page and Top Bar stay
  unwritten.

**By hand (owner)**
- A campaign level shows the new HUD: Menu, Restart, the timer, "Level
  N", the gold and lives pills. A free-play level shows "Free Play" and no
  lives pill.
- The joker bar shows the icons and counts; selecting the Rocket raises
  it; the Clock adds 20 seconds with the "+20".
- With no Rocket held and too little gold, pressing it opens Not Enough
  Gold, the countdown stops and the board ignores drags; Get Gold, a pack
  and Buy add the gold; closing the overlays resumes the countdown; the
  Rocket can then be bought and used with one press and one tap.
- Menu and Restart still open the leave confirmation.
- The layout holds on a tall phone and in the web build's window, and the
  board still fits between the two bands.
