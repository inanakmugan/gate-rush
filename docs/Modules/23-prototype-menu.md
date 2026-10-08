# Module 23 — Prototype menu

**Assembly:** `GateRush.Runtime`, `GateRush.Meta`, `GateRush.Platform` (tests in `GateRush.Tests`)
**Depends on:** Modules 12, 17, 19, 21
**Phase:** 2.7

---

## Responsibility

A small front end so the first WebGL build can be opened, browsed and
shown to someone: a title screen, a level select, a way back to the menu
from a level, completed levels remembered across reloads, and a closing
message after the last level (D50).

In scope:

- A **title screen**: the game's name, **Play** and **Levels**.
- A **level select**: every level in catalog order, all playable, the
  completed ones marked.
- A **Menu** button in the HUD and on the result panel.
- **Completed levels** saved through `ISaveStore` (`PlayerPrefs` in the
  player).
- The **end of the level set**: "All Levels Played" with "Thanks for
  playing!" beneath it.

Not in scope: Phase 6's Home, tabs and settings; Phase 4's wallet, lives,
streak and save model beyond the completed levels; audio; a pause menu.

---

## Public surface

```
Platform
    ISaveStore                          interface (ARCHITECTURE.md)
        bool TryLoad(string key, out string value)
        void Save(string key, string value)

Meta
    CompletedLevels                     plain C#
        bool IsCompleted(int levelId)
        bool MarkCompleted(int levelId) // true when newly completed
        string ToData()
        static CompletedLevels FromData(string data)
                                        // unreadable data reads as none

Runtime
    PlayerPrefsSaveStore : ISaveStore
    MenuScreen                          title screen and level select,
                                        built from code like ResultPanel
    LevelBootstrap                      opens on the title screen; starts
                                        the level the menu picks; returns
                                        to the menu
    HudView                             MenuRequested
    ResultPanel                         MenuRequested; a second line on
                                        the end-of-set panel
    RuntimeConfig                       every text, colour and size above
```

Names are negotiable. That the menu is **an overlay in the Level scene,
not a scene of its own**, and that **every level is playable from the
select**, are not.

---

## Design decisions (owner)

### One scene, overlay screens

- The menu is a canvas overlay in the Level scene, built from code as
  `ResultPanel` and `IntroductionCard` are. No scene is loaded.
- Why: a level picked in a menu scene would have to cross into the Level
  scene, and the usual way is a static field, which the project rules
  forbid. `LevelBootstrap` already loads any level by itself.
- A plain Play, and the player build, open on the title screen with no
  level loaded: no board, no countdown, no input to a board.
- Going back to the menu abandons the level: it cancels any drag, stops
  the countdown, clears the board and hides the result panel and any
  card. Nothing is counted against the player (there are no lives yet).

### Play and Levels

- **Play** starts the first level in catalog order that is not
  completed; when every level is completed, the first level.
- **Levels** shows every level in catalog order by its number
  (`LevelCatalog.TryGetNumber`), all of them playable whatever is
  completed. Completed levels carry a mark.
- Why every level is open: someone reviewing the build should reach any
  mechanic at once; a reviewer will not play fifteen levels to see the
  last.
- **Introductions (Module 19)** keep following catalog order: a level
  started from the select shows the cards it introduces, as a level
  started through Module 21's override does.

### Completed levels

- A level is completed when it is won. The set of completed level ids is
  saved through `ISaveStore` under one key, written at once when a level
  is newly completed (`PlayerPrefs.Save` after the write, so a closed tab
  keeps it).
- Data that cannot be read counts as no level completed, with a logged
  warning; it never throws and never stops the game.
- `ISaveStore` is the interface `ARCHITECTURE.md` already names, with
  `PlayerPrefs` behind it in the player. `CompletedLevels` is plain C# in
  `Meta`. This is the only progress kept before Phase 4, whose save model
  takes it over.

### HUD and result panel

- The HUD gets a **Menu** button beside Restart, in the same style.
- The result panel: a win shows Next (when there is a next level),
  Restart and Menu; a loss shows Restart and Menu.
- After the last level the title is `allDoneTitle`, now "All Levels
  Played", with a new `allDoneMessage`, "Thanks for playing!", in a
  smaller line beneath it. An ordinary win and a loss show no second line.

### Title screen

- The game's name, **Play** and **Levels**, all from config. No credit
  line (owner's decision).

### Development tools (Module 21)

- A level started from the editor tools (the override) skips the menu and
  plays at once, as today. Page Down and Page Up keep working inside a
  level.
- A plain Play must not bypass the menu. Whether the bootstrap's
  serialized first-level field stays, for example as a fallback when the
  catalog is unusable, is left to you.

### Hosting on itch.io (D51)

- The build is an HTML5 upload on itch.io, so the menu must fit a portrait
  viewport and work by touch; on a phone itch.io always launches the game
  in full screen.
- Unity keeps `PlayerPrefs` in the browser's IndexedDB under a path that
  changes with every new upload. Completed levels therefore survive
  reloads of one upload but start empty after a new build is uploaded.
  Accepted for the prototype. `ISaveStore` is the seam: a store that
  outlives uploads (for example `localStorage` under a game-specific key,
  through a `.jslib`) can replace the `PlayerPrefs` one later without
  touching `Meta`.

### Engine rules that carry over

- No mutable statics; tweens killed on disable and destroy; no magic
  numbers or strings: every text, colour, size and duration lives in
  `RuntimeConfig`, with `Problems()` checks. UI animations run on
  unscaled time.
- WebGL: `PlayerPrefs` only, no file system.

---

## Left to you

- The layout: sizes, grid columns, how the completed mark looks, any
  small pop or fade.
- Whether the title screen and the level select are one component or
  two, and where they live (`Runtime/UI` exists and is empty).
- The save key and the data format of `CompletedLevels`, as long as
  unreadable data reads as none.
- The exact Editor steps for the owner.

---

## Tests

Edit Mode.

- `CompletedLevels`: a marked level is completed; marking twice reports
  newly completed once; `ToData` then `FromData` gives the same set;
  empty, null and garbage data give an empty set without throwing.
- Play's start rule, as plain code: the first level in catalog order not
  completed; with every level completed, the first level; ids need not be
  contiguous.
- The level select's entries, as plain code: every catalog level is
  listed and playable whatever is completed, with its number and its
  completed mark. (The central decision: no level is locked.)
- `RuntimeConfig.Problems()` reports empty new texts and non-positive new
  sizes.

**By hand (owner)**
- A plain Play and the build open on the title screen.
- Play starts the first uncompleted level; Levels lists 1 to 15 and starts
  any of them, with its introduction cards.
- Menu from the HUD and from the result panel returns to the title screen
  with a clean board and no countdown running.
- Winning marks the level; after a reload of the build the mark is still
  there.
- After the last level: "All Levels Played", "Thanks for playing!",
  Restart and Menu.
- The Level Editor's Play and Gate Rush → Play Level… still go straight
  into their level.

---

## Resolved during implementation

- **Saved data.** `CompletedLevels` is saved under the constant
  `CompletedLevels.SaveKey` ("GateRush.CompletedLevels") as comma-separated
  ids in ascending order, invariant culture. Null or empty data is an empty
  set. `TryFromData` reports unreadable data so `Runtime` can log it, since
  `Meta` does not log; `FromData` never throws.
- **Saving.** A win is saved when the winning move is applied, before the
  exit animation, so a tab closed mid-animation keeps it. Only levels in the
  level order are marked. A `PlayerPrefsException` is logged, not thrown,
  and the mark then lasts for the session.
- **Menu rules.** `MenuLevels.TryGetStartLevel` answers false for a level
  order with no levels. `MenuLevels.Entries` has no locked state at all.
  `LevelCatalog` gained `Ids`, parallel to `Names`, and `ResultTitle` gained
  `IsLastLevel`, so the title and the second line come from one query.
- **Going to the menu.** In order: input is unbound
  (`InputController.Unbind`, which cancels a drag and is safe to repeat), the
  time-bonus listener goes, the run is released (`LevelRun.Release` stops the
  countdown and unsubscribes it from its session and countdown) and dropped,
  the board is cleared (which abandons a presentation without reporting it),
  and the card and the result panel are hidden with their tweens killed.
  Nothing of the level can act afterwards.
- **Fallbacks.** With an unusable level order, Play starts the scene's level
  and Levels is hidden. An editor override naming no loadable file opens the
  title screen (Module 21 is superseded there).
- **Look.** The result panel keeps Next and Restart on its first row and
  puts Menu on a second, in a neutral colour; it grew to 860 x 860 units.
  The menu reuses existing button colours and the result panel's pop, and
  `menuTitleWidthUnits` keeps the game's name from being cut off on a narrow
  screen. The HUD's Menu button is a three-bar icon drawn from rounded boxes.
- **Testing in the editor.** Play Mode writes the machine's real
  `PlayerPrefs`; Edit → Clear All PlayerPrefs resets the completed levels.
