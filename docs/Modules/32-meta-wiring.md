# Module 32 — Meta wiring

**Assembly:** `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 17, 19, 21, 23, 25, 29 to 31
**Phase:** 4.4

---

## Responsibility

Make the meta game playable in the build. The bootstrap creates one
`MetaGame` over the loaded save and reports every campaign result and
purchase to it (D63). The prototype front end shows the gold and the
lives, starts the campaign from Play and free play from Settings (D58),
asks before an action that costs a life (D57), and offers a refill when
the lives are gone (D62, D64). Everything shown is plain text and the
existing button style; Phase 6 replaces it.

In scope:

- Creating `SystemTimeProvider` and `MetaGame` in the bootstrap, and
  saving after every call that changed the save.
- The campaign and free play as two ways a level is started, and what each
  reports.
- The title page, a Settings page, the no-lives message with its refill
  button, the confirmation prompt, the HUD text and the win panel's gold
  line.
- Plain, tested helpers for the decisions that need no scene.

Not in scope: the store's screen, Home, tabs and a real settings screen
(Phase 6); using jokers (Phase 5); showing the streak (D64); notifications
(Phase 7).

---

## Public surface

```
PlayMode                            enum, Runtime
    Campaign, FreePlay

CampaignLevels                      static, plain C#, Runtime
    static bool TryGetLevel(LevelCatalog catalog, int position, out string name)
        // false past the last level, or with no usable level order

MetaText                            static, plain C#, Runtime
    static string Lives(int count, int cap, TimeSpan? untilNext, RuntimeConfig texts)
    static string Gold(int balance, RuntimeConfig texts)
        // formatting only, from RuntimeConfig's format strings; the time as
        // minutes and seconds, as TimeFormat does

ConfirmPrompt                       MonoBehaviour, Runtime/UI
    void Initialize(RuntimeConfig config)
    void Show(string title)          // "Leave level?" or "Restart level?"
    void Hide()
    bool IsOpen { get; }
    event Action Confirmed           // "Lose a life"
    event Action Cancelled           // "Keep playing"
    event Action ButtonPressed

MenuScreen                          existing
    title page: Play, Settings, a meta line (gold and lives)
    settings page: the sound button, Levels (free play), Back
    a no-lives page: the message, the time to the next life, Refill, Back
    events gain: SettingsRequested and RefillRequested; LevelRequested now
    means free play

HudView                             existing
    void ShowMeta(string text)       // lives and gold, or "Free Play" and gold
    void HideMeta()

ResultPanel                         existing
    ShowWin gains the gold line, shown only for a campaign win

RuntimeConfig                       existing
    every new text, format string, size and colour, checked in Problems()

LevelBootstrap                      existing
    [SerializeField] ConfirmPrompt
    creates SystemTimeProvider and MetaGame in Awake, after the save loads
```

Names are negotiable. That the bootstrap is the only caller of `MetaGame`,
that where a level was started decides its mode, and that every change is
saved through `SaveOrWarn`, is not.

---

## Design decisions (owner)

### Two ways a level starts (D58)

- **Campaign:** Play on the title page, and Next after a campaign win. The
  level is the one at `MetaGame.CampaignPosition` in the level order
  (`CampaignLevels.TryGetLevel`).
- **Free play:** a level button in Settings → Levels, Next after a
  free-play win, and every development tool: a level started from the
  editor's Play or Gate Rush → Play Level…, and Page Down and Page Up
  (Module 21, D64).
- The mode is decided where the level starts and kept with the run.
  Restart keeps the mode.
- With no usable level order, Play falls back to the bootstrap's
  serialized level as free play, as Module 23 falls back today, and logs
  it once.

### Starting a campaign level (D57)

- Play, Next after a campaign win, and Restart on a campaign loss's panel
  all check `CanStartCampaignLevel()` first. With no life they open the
  menu's no-lives page instead of starting.
- Starting spends nothing.
- With the position past the last level, Play opens the title page with
  the message "All levels played. Play any level from Settings." and
  starts nothing.

### What each mode reports (D57, D58, D63)

- **Campaign win:** `ReportCampaignWin(levelContext.GoldReward)` at the
  moment the win is decided, where the completed mark is recorded today;
  the mark and the gold are saved in one `SaveOrWarn`.
- **Campaign loss:** `ReportCampaignLoss()` when the countdown runs out,
  and when the player confirms leaving or restarting a level being played.
  Save after every loss, even when it returns false (Module 31's note).
- **Free play:** nothing is reported. A win still records the completed
  mark, as today.
- A level that is already won or lost is not being played: Menu and
  Restart on the result panel never ask and never cost a life. A loss has
  already cost its life.

### The confirmation (D57, D64)

- Menu and Restart while a campaign level is being played open
  `ConfirmPrompt` with "Leave level?" or "Restart level?", the line "You
  will lose a life.", and two buttons, "Lose a life" and "Keep playing".
  The keyboard restart (R) opens it too. Free play never asks.
- While the prompt is open, the countdown is stopped (`Countdown.Stop`,
  then `Start` to resume), the board takes no input, and the HUD's buttons
  do nothing.
- "Keep playing" closes the prompt and resumes from the same remaining
  time.
- "Lose a life" reports the loss, saves, and then goes to the menu or
  restarts. A restart needs a life: with none left after the loss, it
  opens the no-lives page instead.
- The prompt never opens over an introduction card (Module 19); Restart
  and Menu are already held while a card shows.

### The title page and Settings

- Title page: the game's name, Play, Settings, and one meta line, for
  example "Gold 640 · Lives 3/5 · next 12:34", or "Lives 5/5" when full.
  The line is refreshed once a second while the page is open, from
  `ITimeProvider` through `MetaGame`.
- Settings page: the sound button (moved from the title page, Module 25),
  Levels, and Back. Levels opens the existing level select; every level
  stays open and a completed one keeps its mark (D50, D58). Its Back
  returns to Settings.
- No-lives page: "No lives left", "Next life in 12:34" refreshed once a
  second, "Refill lives (300 gold)" with the price from `MetaRules`, and
  Back. The refill button is not interactable when the balance is below
  the price. A refill calls `TryRefillLives()`, saves, and returns to the
  title page; when the lives come back on their own while the page is
  open, it returns there too.

### The HUD and the result panel

- During a campaign level the HUD shows the meta line with gold and lives;
  during free play it shows "Free Play" and the gold. It is in the top
  band, beside the level number; where exactly is left to you within
  `ScreenBands`.
- A campaign win's panel shows one more line under its title: "+50 gold",
  from the level's reward. Free play wins show no gold line. The last
  level's "All Levels Played" title and "Thanks for playing!" message stay
  as they are.

### Engine rules that carry over

- No magic numbers or strings: every text, format string, size and colour
  is a `RuntimeConfig` field with a check in `Problems()`. Prices and
  counts are read from `MetaRules`, never written into a text.
- No mutable statics. UI animations on unscaled time. Every new
  subscription has its unsubscription.
- `MetaGame` and `ITimeProvider` are created once in `Awake`, after the
  save loads; `SystemTimeProvider` is the only clock. The bootstrap's Meta
  Config tooltip is updated to name the prices and the streak table.
- `ConfirmPrompt` is built from code on the screen-space canvas, as
  `ResultPanel` is, over the HUD and under nothing else that can be open
  at the same time.

---

## Left to you

- How the run keeps its mode, and how the bootstrap's flow is split into
  methods.
- Where the meta line sits on the title page and in the HUD, and the
  prompt's layout, within the existing style.
- How the once-a-second refresh avoids rebuilding text every frame (the
  HUD's timer already skips unchanged seconds).
- The exact owner Editor steps, including assigning the Confirm Prompt on
  `Game` with its full Hierarchy path.

---

## Tests

**`CampaignLevels`**
- Position 0 gives the first level of the order; the last position gives
  the last level; one past it is false; a catalog with no usable order is
  false.

**`MetaText`**
- Full lives show the count and the cap without a time; lives below the
  cap show the time to the next as minutes and seconds; gold shows the
  balance. Every text comes from the config's format strings. (Formatting
  only: the rules are `MetaGame`'s and are tested in Module 31.)

**`RuntimeConfig`**
- Each new text or format string that is empty, and each new size or
  colour out of range, is reported by `Problems()`.

**By hand (owner)** — the central checks of this module are in a scene:
- Campaign: Play starts the campaign level, the first level with a save
  from Module 31, whatever is completed; a win shows "+50 gold", raises
  the gold on the HUD and the title page, and Next goes to the next level.
- Loss: letting the time run out costs one life; the HUD and the title
  page show it with the time to the next life.
- Prompt: Menu and Restart while playing a campaign level ask first; while
  asking, the timer stops; "Keep playing" resumes from the same second;
  "Lose a life" costs one life and then leaves or restarts.
- No lives: after losing every life, Play shows the no-lives page; Refill
  is available with 300 gold or more and fills the lives, and not
  available below it.
- Free play: a level from Settings → Levels needs no life, costs none,
  shows "Free Play" on the HUD, pays no gold, and never asks before Menu
  or Restart.
- Development tools: the editor's Play button, Gate Rush → Play Level… and
  Page Down and Page Up play free play.
- Persistence: gold, lives, the campaign position and the completed marks
  survive stopping and pressing Play again, and a web build reload.

---

## Resolved during implementation

- **Surface.** `MenuScreen` gained a `MenuPage` enum (Title, Settings,
  Levels, NoLives), `ShowMeta`, `ShowNotice`, `ShowNoLives`,
  `ShowNextLife` and `ShowTitlePage`, and the `RefillRequested` event. Two
  members of the spec were left out: `SettingsRequested`, because Settings
  opens inside the menu as Levels does and the bootstrap has nothing to
  decide there, and `HudView.HideMeta`, because a loaded level always
  shows the line and the menu hides the whole HUD. `ResultPanel.ShowWin`
  takes the gold line and whether Restart is shown. `MetaText` also
  formats the separator, the gold earned, the next life and the refill
  label, and gives the rounded second the refresh compares.
- **Meta unchanged.** `MetaGame` has no queries for the lives, the gold or
  the price, so the bootstrap reads them from `MetaGame.Save` and
  `MetaRules`; whether the refill is affordable is decided there and the
  menu gets a bool.
- **A campaign win is never replayed.** Not in the spec: Restart on a
  campaign win's panel would have paid the gold again and moved the
  campaign past a level. Following the reference game (D58), that panel
  shows Next and Menu only and R does nothing there; a free-play win keeps
  Restart.
- **One campaign query.** Next after a campaign win loads the level from
  `CampaignLevels.TryGetLevel(CampaignPosition)`, and the same call
  decides whether Next shows and whether the title is All Levels Played,
  so the two never disagree. The last campaign win shows that title, the
  gold line and Thanks for playing!; afterwards Play shows the
  campaign-done notice, even with no life left.
- **The no-lives page** returns to the title page as soon as the first
  life comes back. A refill pressed without enough gold, which the
  disabled button should make impossible, logs an error.
- **Holding.** One query, `IsHeld()`, covers an introduction card and the
  confirmation, and holds the pointer, R and the HUD's buttons. A
  development key while the prompt is open loads its level as free play
  and costs no life.
- **Guards.** A negative gold reward in level data logs an error, the win
  is not reported and the campaign stays on that level, but the completed
  mark is saved. An empty level order counts as unusable, so Play falls
  back to the scene's level as free play.
- **Texts and layout.** The label font has no middle dot, so the separator
  defaults to " | ". The title page keeps room for its notice row so it
  does not jump. The HUD's meta line is a second row under the level
  number. `RuntimeConfig.asset` was saved once after a field was changed
  and changed back, since an unchanged asset is not written.
- **Removed.** `MenuLevels.TryGetStartLevel` (the first-uncompleted rule
  D58 replaced) and `ResultTitle.ForWin`, with their tests; nothing called
  them any more.
- **Tests.** `CampaignLevelsTests`, `MetaTextTests` and the new
  `RuntimeConfigTests` cases. 1920 Edit Mode tests pass.
- **By hand.** Every check in the list above passed from a cleared save,
  including the hidden Restart on a campaign win and the web build reload.
