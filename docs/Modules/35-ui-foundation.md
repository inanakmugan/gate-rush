# Module 35 — UI foundation

**Assembly:** `GateRush.UI` (new), `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 23, 25, 29 to 32
**Phase:** 6.1

---

## Responsibility

Lay the ground every Phase 6 screen stands on, and replace the prototype
title page with Home (D68). A new `GateRush.UI` assembly holds the
screens; `Runtime` talks to them through an interface it owns, so the
dependency points one way. The screens are prefabs the owner lays out in
the Editor with the art of D69; code finds their parts through serialized
fields and only shows what `Runtime` gives it.

In scope:

- The `GateRush.UI` assembly and its test references.
- `IFrontEnd` in `Runtime`, the one way the bootstrap shows and hears the
  front end, and `FrontEndState`, the plain data it shows.
- Navigation: three tabs (Store, Home, Leaderboard) and a stack of
  overlays over them, with Back.
- `UiConfig`, the texts, formats and colours the screens use from code.
- Prefabs: a button family, the top bar, the tab bar, Home, two empty
  tabs, and three overlays: Settings (sound and levels, as the prototype
  has), Levels (free play, D58) and Lives (the refill offer, D62).
- Removing the prototype menu (`MenuScreen`, `MenuLevels`) and its
  `RuntimeConfig` fields.

Not in scope: the Store's goods (6.2), the streak bar and Win Streak
screen (6.3), Profile, the haptics toggle and the save's format 4 (6.4),
the leaderboard (6.5), the level's own interface (6.6), screen transitions
(Phase 7).

---

## Public surface

```
IFrontEnd                           interface, Runtime
    string MissingSetup()           // null when every part is assigned
    bool IsOpen { get; }
    void Show(FrontEndState state)  // Home tab, no overlay
    void Refresh(FrontEndState state)
    void ShowLives()                // the Lives overlay over the current tab
    void ShowNotice(string text)    // a line under Home's play button
    void Hide()
    event Action PlayRequested
    event Action<string> LevelRequested          // free play (D58)
    event Action SoundToggleRequested
    event Action RefillRequested
    event Action ButtonPressed

FrontEndState                       readonly struct, plain C#, Runtime
    int Gold, int Lives, int LifeCap
    TimeSpan? UntilNextLife          // null when full
    int? CampaignLevelNumber         // 1-based; null past the last level
    int RefillPrice, bool CanRefill  // not full and the balance covers it
    bool IsSoundOn
    IReadOnlyList<LevelEntry> Levels // name, number, completed
    static FrontEndState Of(MetaGame meta, MetaRules rules, DateTime nowUtc,
                            LevelCatalog catalog, bool isSoundOn)

UiNavigation                        sealed class, plain C#, UI
    UiTab Tab { get; }               // Store, Home, Leaderboard
    IReadOnlyList<UiOverlay> Overlays { get; }   // Settings, Levels, Lives
    void SelectTab(UiTab tab)        // closes every overlay
    void Open(UiOverlay overlay)
    bool Back()                      // closes the top overlay, else returns
                                     // to Home; false when there is nothing
    void Reset()                     // Home, no overlay
    event Action Changed

HomeTexts                           static, plain C#, UI
    static string PlayLabel(int? campaignLevelNumber, UiConfig config)
    static string LivesStatus(int lives, int cap, TimeSpan? untilNext, UiConfig config)
    static string Gold(int gold, UiConfig config)

UiConfig                            ScriptableObject, UI
    every text, format string and colour the screens set from code,
    checked in Problems()

FrontEnd                            MonoBehaviour, UI; implements IFrontEnd
    [SerializeField] parts: the tab roots, the tab bar, the top bar, the
    overlays, the play button and its label, the notice, UiConfig

LevelBootstrap                      existing
    [SerializeField] MonoBehaviour frontEnd   // must implement IFrontEnd
```

Names are negotiable. That `Runtime` never references `GateRush.UI`, that
screens hold no rules, and that every number a screen shows comes from
`FrontEndState`, is not.

---

## Design decisions (owner)

### The dependency

- `GateRush.UI` references `Runtime`, `Meta`, `Platform`, TextMeshPro and
  uGUI. `Runtime` does not reference it: `LevelBootstrap` holds the front
  end as a `MonoBehaviour` field and checks at start that it implements
  `IFrontEnd`; otherwise it logs an error and draws nothing, as a missing
  part does today.
- The bootstrap keeps every decision it makes today: what Play does, the
  campaign-done notice, the no-lives case, refilling, saving. It builds
  `FrontEndState` and hands it over; the front end only shows it and
  raises events.
- `FrontEndState.Of` is the one place the numbers are read from the save
  and the rules. The bootstrap refreshes the front end once a second while
  it is open, and only when the state changed.

### Navigation

- Three tabs at the bottom: Store, Home and Leaderboard, Home in the
  middle and selected at start. The selected tab is drawn raised with its
  name; the others show their icon only (the reference game's tab bar).
- Overlays open over the current tab, the newest on top, each with a red
  close button. Selecting a tab closes every overlay.
- Back (the Escape key, which is also Android's back button) closes the
  top overlay; with none open on Store or Leaderboard it returns to Home;
  on Home it does nothing. Back is read only while the front end is open.
- In this module Store and Leaderboard are empty pages with their title;
  their content is 6.2 and 6.5.

### Home

- **Top bar**, left to right: the avatar (the first avatar of the pack,
  not pressable until 6.4); gold with its coin icon and a "+" that selects
  the Store tab; lives with the heart icon, the count inside it, the
  status beside it ("Full", or the time to the next life as m:ss) and a
  "+" that opens the Lives overlay; the settings gear, which opens
  Settings.
- **Below the top bar:** an empty area kept for the streak bar of 6.3.
- **The play button:** large, green, "Level N" from `CampaignLevelNumber`,
  or "All Played" when it is null. Pressing it raises `PlayRequested`; the
  bootstrap decides what follows, as today (a campaign level, the Lives
  overlay when no life is left, or the campaign-done notice).
- **The notice** is one line under the play button, empty until
  `ShowNotice`; it clears when Home is shown again.

### Overlays

- **Settings:** the title, a Sound row with an on/off toggle
  (`SoundToggleRequested`), and a Levels button that opens the Levels
  overlay. Haptics is added in 6.4.
- **Levels:** a scrolling grid of every level in the order, each a button
  with its number and a mark when completed; pressing one raises
  `LevelRequested` with its name. Free play needs no life (D58).
- **Lives:** the hearts as "3/5", the next life's time or "Full", and a
  refill button "Refill (300)" with the price from the rules, pressable
  only when `CanRefill`. Opened by the lives "+" and by the bootstrap when
  Play finds no life. It stays open as lives come back; the player closes
  it.

### Art and theme (D69)

- Buttons are 9-sliced Kenney UI Pack sprites: green
  `button_rectangle_depth_gradient` for the play and primary buttons, blue
  for secondary ones, red `button_round_depth_gradient` with the `cross`
  icon for close, grey for a button that cannot be pressed. Panels use
  `button_square_flat`. Icons come from `Kenney/GameIcons` and
  `Kenney/BoardGameIcons`, tinted from `UiConfig`.
- Text is TextMeshPro with a Fredoka Bold font asset. Fredoka lacks six
  Turkish letters (see Resolved); 6.4 adds a fallback font before player
  names can use them.
- The background is a flat colour from `UiConfig`, the reference game's
  purple.
- Every colour set from code is a `UiConfig` field; colours that never
  change live on the prefab.

### Engine rules that carry over

- No magic numbers or strings in code: texts, formats and colours are
  `UiConfig` fields checked in `Problems()`; layout lives on the prefabs.
- No mutable statics. UI animations, if any, on unscaled time. Every
  subscription has its unsubscription.
- The front end lives under the scene's existing Canvas, under the result
  panel, the confirmation and the introduction card, and over nothing else
  that is open with it.

---

## Left to you

- How `FrontEnd` finds the parts of each prefab (serialized fields on
  small view components, or on `FrontEnd` itself).
- Whether the level grid's buttons are instantiated from a prefab at start
  or on every show.
- The exact `RuntimeConfig` fields and tests that go with the prototype
  menu, and any `MetaText` member left unused.
- The prefab hierarchies, within the decisions above: the owner builds
  them, so your report must give every object, component and field, with
  the sprite and colour of each, in the order to build them.

---

## Tests

**`UiNavigation`**
- Starts on Home with no overlay. Selecting a tab closes every overlay.
  Opening stacks overlays; Back closes the top one, then the next. Back
  with no overlay on Store or Leaderboard returns to Home; on Home it
  returns false. `Changed` is raised once per change and not when nothing
  changed.

**`FrontEndState.Of`**
- Gold, lives and the cap come from the save at the given time;
  `UntilNextLife` is null when full. `CampaignLevelNumber` is the 1-based
  position, null past the last level and with no usable order. `CanRefill`
  is false when full and when the balance is short. `Levels` lists the
  order with completed marks.

**`HomeTexts`**
- "Level N" and the all-played label; "Full" and m:ss; every text from the
  config's formats.

**`UiConfig`, `RuntimeConfig`**
- Each new text or format string that is empty, and each new colour that
  is transparent, is reported by `Problems()`. The prototype menu's fields
  and their tests are gone.

**By hand (owner)**
- Home shows the gold, the lives with "Full" or the time, and "Level N";
  Play starts the campaign level.
- The tabs switch; Store and Leaderboard show their titles; Back from them
  returns to Home.
- The gear opens Settings; Sound toggles and the game is silent; Levels
  opens the grid; a level starts in free play.
- The lives "+" opens Lives; with too little gold the refill button cannot
  be pressed; with enough it fills the lives.
- With no life, Play opens Lives; after the last campaign level, Play
  shows the notice.
- Back closes overlays one at a time; leaving a level returns to Home.
- The layout holds on a tall phone and in the web build's window.

---

## Resolved during implementation

- **Parts.** `FrontEnd` finds the parts of the prefabs only through
  serialized fields on small views: `TopBar`, `TabBar` and its
  `TabButton`s, `HomeScreen`, `LevelButton` and the three overlays.
  `FrontEnd.MissingSetup()` names any part left unassigned, and needs a
  Canvas above the front end.
- **Open and closed.** `Show` and `Hide` switch the front end's own
  GameObject, so nothing in it runs while it is closed; the prefab is
  saved inactive. The overlays share one parent and stack by sibling
  order. Back is the Escape key, Android's back button under the Input
  System.
- **The level grid.** Its buttons are created from the Level Button
  prefab the first time the grid is shown, and again only when the count
  changes; every later show updates the numbers and marks in place.
- **Tints.** `UiTint` colours are applied when the front end opens, and by
  the grid for the buttons it creates. In prefab mode tinted graphics show
  white.
- **Prefabs built from code.** The owner laid out the four buttons, Level
  Button, Top Bar, Tab Bar and Home Page by hand. Store Page, Leaderboard
  Page, the Settings, Levels and Lives overlays and Front End are written
  by *Gate Rush → Build UI Prefabs*, in the Editor-only
  `GateRush.UI.Editor` assembly under `Assets/Editor/UI`. It nests the
  hand-made prefabs as linked instances and never writes them, checks
  every input before the first save, and assembles in a preview scene, so
  the open scene is never touched. A rebuild keeps each prefab's GUID but
  not the file IDs inside it, so `LevelBootstrap`'s reference to the front
  end is checked again after one.
- **Safe area.** Pages, Top Bar and Tab Bar sit under `Safe`; `Overlays`
  is a direct child of the front end's root, so the scrims cover the
  safe-area insets too.
- **Font.** Fredoka's import reports six missing Turkish letters, among
  them Ğ, İ and Ş. The interface is English, so this module needs none of
  them; 6.4 adds a fallback font.
- **Sprites.** The Kenney sprites are imported as Single with Full Rect
  meshes. `audioOn` and `audioOff` came sliced and were set to Single for
  the builder, which refuses a sprite that imports as more than one.
- **Tests.** 52 new, 8 of them for the builder; 2,073 in all.
