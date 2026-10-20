# Module 39 — Leaderboard

**Assembly:** `GateRush.Platform`, `GateRush.Runtime`, `GateRush.UI`, `GateRush.UI.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 30, 35, 36, 38
**Phase:** 6.5

---

## Responsibility

Fill the Leaderboard tab. Its rows come from an `ILeaderboardService` in
`Platform`, so a real backend is a new implementation and no screen
changes (D70). The first implementation is simulated (D73): a fixed table
of demo players from configuration with the player ranked among them on
lifetime gold (D22), under a line that says so and is never hidden.

In scope:

- `ILeaderboardService` and its data in `Platform`.
- `SimulatedLeaderboardService` and its configuration in `Runtime`.
- The Leaderboard page: the label line, the list of rows, the player's
  row marked and scrolled into view, and a failure message with Retry.
- The builder writes the page's content and the row prefab.

Not in scope: an online backend, accounts, weekly, world or country
tabs, friends, rewards for ranks, the player's rank on Home or Profile.

---

## Public surface

```
ILeaderboardService                 interface, Platform
    bool IsBusy { get; }
    void Fetch(LeaderboardPlayer player, Action<LeaderboardResult> done)

LeaderboardPlayer                   readonly struct, Platform
    string Name, string AvatarId, int Score     // the player's own entry

LeaderboardEntry                    readonly struct, Platform
    int Rank, string Name, string AvatarId, int Score, bool IsPlayer

LeaderboardResult                   sealed class, Platform
    bool IsLoaded                   // false: the fetch failed
    bool IsSimulated                // true: demo players, not online
    IReadOnlyList<LeaderboardEntry> Entries     // by rank; empty when not loaded

SimulatedLeaderboardService         sealed class, Runtime; ILeaderboardService
SimulatedLeaderboardConfig          ScriptableObject, Runtime: the demo players

LeaderboardRanking                  static, plain C#, Platform or Runtime
    // orders the demo players and the player into ranked entries

IFrontEnd                           existing, gains
    event Action LeaderboardRequested
    void ShowLeaderboard(LeaderboardResult result)

LeaderboardScreen                   MonoBehaviour, UI: the page's content
LeaderboardRow                      MonoBehaviour, UI
LeaderboardTexts                    static, plain C#, UI
```

Names are negotiable. That the screen knows nothing of where the rows
come from beyond `IsSimulated`, that the ranking is plain C# and tested,
and that a simulated table is always labelled, is not.

---

## Design decisions (owner)

### The simulated table (D73)

- Twenty demo players in `SimulatedLeaderboardConfig`, each a name, an
  avatar id from the catalog and a lifetime gold score. Their scores
  spread from 40 to 980, so a player who wins the campaign's 750 gold
  climbs through most of the table and never reaches the top by playing
  alone. Purchased gold never counts (D22, D72).
- Names are invented handles that read as players ("PuzzleFox",
  "GateKeeper", "Mavi Kedi", …), a mix of English and Turkish, never a
  real person's or brand's name. The config holds them, so they can be
  changed without code.
- The player's entry is their name, avatar and lifetime gold, read when
  the tab is shown.
- Ranking: score descending; the player sits below demo players with the
  same score (they have to pass a score to take its place). Ranks are 1
  to 21, one per row, no shared ranks.
- The simulated service answers at once and never fails; `IsBusy` exists
  for a real backend.

### The Leaderboard page

- Under the title, one line that is never hidden while the result is
  simulated: "Demo players. The online leaderboard is not connected."
- A scrolling list, one row per entry: the rank ("1", "2", …, with a gold,
  silver and bronze mark on the first three), the avatar, the name, and
  the score with the coin icon ("1,250").
- The player's row is drawn in the highlight colour and reads the
  player's name; when the tab opens, the list scrolls so that row is in
  view, near the middle when it can be.
- Selecting the Leaderboard tab asks for a fresh result
  (`LeaderboardRequested`); the bootstrap fetches and calls
  `ShowLeaderboard`. A changed name or avatar shows the next time the tab
  is selected.
- While a fetch is pending the page shows "Loading…"; a failed fetch shows
  "Could not load the leaderboard." and a **Retry** button that asks
  again. The simulated service never reaches either, but the screen
  handles them for a real one, and tests drive them through a test
  service.

### The builder

- It writes the Leaderboard page's content and the Leaderboard Row prefab
  the page instantiates per entry, as the level grid and the Store's gold
  packs do.
- The rank marks use the existing CC0 icons (Game Icons' `medal1` or
  Board Game Icons' `award`), tinted gold, silver and bronze from
  `UiConfig`.

### Configuration

- `SimulatedLeaderboardConfig`: the demo players, checked: names pass the
  name rule of Module 38, avatar ids non-empty, scores 0 or above, at
  least one player. The bootstrap checks the avatar ids against the
  catalog at start and logs any it lacks, as the gold packs do.
- `UiConfig`: the label line, Loading, the failure line, Retry, the score
  format, the rank format, and the three medal colours and the highlight
  colour, checked in `Problems()`.

---

## Left to you

- Where `LeaderboardRanking` lives (Platform or Runtime), as long as it is
  plain C# and tested.
- How the list scrolls the player's row into view.
- Whether rows are recreated on every show or updated in place when the
  count stays the same.
- The prefab hierarchies and sizes, given in the plan as before.

---

## Tests

**`LeaderboardRanking`**
- Demo players in any order come out by score descending with ranks 1 to
  N + 1; the player is placed by score; ties put the player below; a
  player with 0 is last; a player above every demo player is first; the
  player's entry is the only one marked.

**`SimulatedLeaderboardService`**
- `Fetch` answers at once, loaded and simulated, with 21 entries; `done`
  is called exactly once; null arguments throw.

**`SimulatedLeaderboardConfig`**
- A refused name, an empty avatar id, a negative score and an empty table
  are reported.

**`LeaderboardTexts`, `UiConfig`**
- The score with thousands separators, the rank; each new text that is
  empty and each transparent colour is reported.

**Builder**
- The page's content and the row prefab are written with every field
  wired; Home Page and Top Bar stay unwritten.

**By hand (owner)**
- The tab shows the label line and twenty-one rows, the player's marked
  and in view, with the right name, avatar and lifetime gold.
- Winning a campaign level and returning raises the player by score;
  buying a gold pack does not.
- Changing the name or avatar in Profile shows on the next visit to the
  tab.
- The first three ranks carry gold, silver and bronze marks.
- The layout holds on a tall phone and in the web build's window.

---

## Resolved during implementation

- **Where the ranking lives.** `LeaderboardRanking` is in `Runtime`,
  beside the simulated service it serves; `Platform` keeps the interface
  and its data. Demo players with equal scores keep their config order,
  so the table is the same on every fetch.
- **The service contract** is stricter than the sketch: `IsBusy` is
  cleared before `done` runs, a fetch while busy throws, and so does a
  player with a null name or avatar id or a negative score.
- **Stale results.** A result that arrives after the front end closed is
  dropped. A request made while a fetch is pending drops the pending
  result and fetches once more, so a name changed in Profile is never
  shown out of date. A result for another tab is ignored; the next
  selection asks again. Pressing the Leaderboard tab while on it does not
  fetch.
- **The demo players** are twenty invented handles, English and Turkish,
  none using the default avatar so a new player's row stands out. A
  player who has won the campaign's 750 gold ranks 7th of 21.
- **Scrolling.** After the rows are laid out, the player's row is put at
  the viewport's middle, clamped to the content (`LeaderboardScroll`,
  plain C#). The list uses the builder's shared scroll sensitivity.
- **Colours.** The row colour is a `UiConfig` field beside the highlight
  colour, since rows switch between the two at runtime. The medal is
  Game Icons' `medal1`, tinted gold, silver and bronze.
- **Fonts.** The "…" of "Loading…" and the Turkish letters of the demo
  names come from the Nunito fallback.
- **Tests.** The screen tests build the page from code and drive it
  through a stub service, so the test assembly now references
  TextMeshPro and uGUI. 79 new; 2,459 in all.
