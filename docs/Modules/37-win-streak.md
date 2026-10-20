# Module 37 — Win Streak

**Assembly:** `GateRush.UI`, `GateRush.Runtime`, `GateRush.UI.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 31, 32, 35, 36
**Phase:** 6.3

---

## Responsibility

Show the win streak the meta game has counted since Module 31 (D61). Home
gets a progress bar toward the next reward, a tap on it opens the Win
Streak overlay with the whole reward ladder, and a reward earned by a
campaign win is announced when the player is back on Home. The rules do
not change: `Meta` already counts the streak, resets it on a loss, keeps
the best and pays the jokers; this module only shows them.

In scope:

- `FrontEndState` gains the streak, the best streak and the reward table.
- The Streak Bar on Home, in the Streak Area that Module 35 kept empty.
- The Win Streak overlay: the ladder, the current and best streaks, and
  the rule in one line.
- The Streak Reward overlay, opened on Home after a campaign win that gave
  a reward.
- The builder writes the new prefabs and places the Streak Bar in Home's
  Streak Area inside Front End.

Not in scope: any change to the streak's rules or reward table (D61),
showing the streak on the result panel or warning on the leave prompt that
the streak will be lost (6.6, with the level's own interface), animations
of the bar filling (Phase 7).

---

## Public surface

```
FrontEndState                       existing, gains
    int Streak, int BestStreak
    IReadOnlyList<StreakReward> StreakRewards   // the rules' table, in order

IFrontEnd                           existing, gains
    void ShowStreakReward(int streak, IReadOnlyList<StreakReward> rewards)

StreakProgress                      static, plain C#, UI
    // what the bar shows for a streak and a table: the next reward (or
    // none past the last), the wins toward it, the segment of the bar
    // filled, and each reward's state (reached, next, ahead)

StreakTexts                         static, plain C#, UI
StreakBar                           MonoBehaviour, UI: the bar on Home
WinStreakOverlay                    MonoBehaviour, UI
StreakRewardOverlay                 MonoBehaviour, UI
```

Names are negotiable. That the screens hold no rules, that the streak and
the table come only from `FrontEndState`, and that a reward is announced
from what `MetaGame.ReportCampaignWin` returned, never recomputed on a
screen, is not.

---

## Design decisions (owner)

### The Streak Bar on Home

- Under the top bar, in Home's Streak Area, the full width of the area: a
  pressable panel holding:
  - on the left, the fire icon with the current streak inside or beside
    it ("4");
  - a horizontal bar from 0 to the last reward's wins (10 by default),
    filled up to the current streak;
  - a marker on the bar at each reward's wins, showing that reward's joker
    icon and its count when above 1. A reward reached in the current
    streak shows a check; the next one is highlighted;
  - one line under the bar: "2 more wins: Rocket" toward the next reward,
    or "Every reward collected" past the last one.
- With a streak of 0 the bar is empty and the line reads "Win 3 in a row:
  Clock".
- Past the last reward the bar stays full; the streak keeps counting in
  the number (D61).
- Pressing the bar opens the Win Streak overlay.

### The Win Streak overlay

- An overlay on the stack, opened by the Streak Bar; Back and the red
  close button close it.
- The title "Win Streak", then the current streak and the best ("Streak
  4", "Best 7").
- The ladder: one row per reward in the table, in order: the wins ("3
  wins"), the joker's icon and name with its count ("Clock x1"), and a
  state: **Collected** (reached in the current streak), **Next** (the first
  not reached), or nothing for the rest.
- One line at the bottom: "Win campaign levels in a row. A lost level
  resets the streak. Free play does not count."
- The ladder has as many rows as the table; it is built from
  `StreakRewards`, not fixed in the prefab.

### Announcing a reward

- When `ReportCampaignWin` returns rewards, the bootstrap keeps them until
  the front end is next shown on Home, then calls `ShowStreakReward`. The
  result panel stays as it is (D64).
- The Streak Reward overlay: the title "Streak Reward", "4 wins in a row!"
  with the streak reached, each reward given as an icon and "+1 Rocket",
  and a green **Collect** button that closes it. The jokers are already in
  the inventory; Collect only closes the overlay.
- A reward is announced once. Leaving for a new level before seeing it,
  or closing the game, loses the announcement but never the jokers.
- A streak lost to a loss is not announced; the bar shows it.

### Icons and art

- The streak uses `Kenney/BoardGameIcons/fire.png`, tinted by a new
  `UiTint` role, Streak, with an orange colour in `UiConfig`.
- The jokers use the Store's icons (Module 36): hourglass, exploding and
  arrow_horizontal, through one place both screens read, so 6.6 changes
  them once.
- The bar's track and fill are 9-sliced UI Pack sprites; the fill is
  orange, the track a dark tint of the background.

### The builder

- It writes the Streak Bar, the Win Streak overlay with a ladder row
  prefab, and the Streak Reward overlay, and adds the two overlays under
  Overlays in Front End.
- Home Page is hand-made and is never written. The Streak Bar is placed in
  Front End as a nested instance under the Home Page instance's Streak
  Area, stretched to it, recorded as an added object of that instance;
  `FrontEnd` holds the bar's reference.

### Configuration

- `UiConfig`: every new text and format ("{0} more wins: {1}", "Win {0} in
  a row: {1}", "Every reward collected", "Streak {0}", "Best {0}", "{0}
  wins", "{0} x{1}", "{0} wins in a row!", "+{0} {1}", Collected, Next,
  the rule line), the joker names, and the Streak colour, checked in
  `Problems()`.
- The reward table stays in `MetaConfig` (D61).

---

## Left to you

- How `StreakProgress` describes the bar; it must be plain C# and tested.
- Whether the markers sit on the bar at their wins' position or evenly
  spaced; positions must stay readable with the default table, where 3
  and 5 are close.
- How the bootstrap keeps an unannounced reward between the win and Home.
- The prefab hierarchies and sizes, given in the plan as for Modules 35
  and 36, and how the ladder rows are created.
- Where the joker icons are kept so the Store and the streak share them.

---

## Tests

**`FrontEndState`**
- The streak, the best and the reward table come from the save and the
  rules.

**`StreakProgress`**
- Streak 0, 2, 3, 4, 5, 9, 10 and 12 with the default table: the next
  reward, the wins toward it, the fill, and each reward's state. An empty
  table gives no next reward and an empty bar. A table of one reward.
  Reaching a threshold makes it Collected and the following one Next.

**`StreakTexts`**
- Each line for those streaks, the ladder's wins and counts, and the
  reward lines; every text from the config.

**`UiNavigation`**
- The two new overlays stack and close with Back.

**`UiConfig`**
- Each new text and format that is empty or unusable, and a transparent
  Streak colour, is reported.

**Builder**
- The new prefabs are written with every field wired; the Streak Bar sits
  under the Home Page instance's Streak Area in Front End; Home Page's own
  file is unchanged.

**By hand (owner)**
- A new player's Home shows an empty bar and "Win 3 in a row: Clock".
- Winning campaign levels fills the bar; at 3, 5 and 10 wins the Streak
  Reward overlay opens on Home with the right joker, and the Store's count
  for it went up by one.
- Losing a campaign level (the countdown, Menu or Restart while playing)
  empties the bar; the best streak in the overlay stays.
- Free play wins change nothing.
- The Win Streak overlay shows Collected and Next correctly, and closes
  with Back and the close button.
- Past 10 wins the bar stays full, the number keeps counting, and no
  reward is announced.
- The layout holds on a tall phone and in the web build's window.

---

## Resolved during implementation

- **Announcing rewards.** The player usually presses Next on the result
  panel and goes straight to the next level, so dropping an unannounced
  reward when a level loads would have hidden most of them. The bootstrap
  keeps every reward a campaign win gave, across levels and restarts,
  with the latest rewarded streak, and announces them all once when Home
  is next shown; the list is emptied first, so nothing is announced
  twice. Only closing the game loses the announcement, never the jokers.
  In the campaign-done case the announcement follows the notice, which
  closes every overlay.
- **Merging.** The Streak Reward overlay merges the rewards by joker kind
  in Clock, Rocket, Broom order (`StreakPrizes`), so two Clocks read "+2
  Clock"; the headline uses the latest rewarded streak. The panel is
  taller than planned so all three kinds fit above Collect.
- **The bar.** `StreakProgress` gives the fill, the next reward, the wins
  to it and every marker's state from one call, so the checks and the
  fill always agree. Markers sit at their wins' share of the bar; a custom
  table with two thresholds one win apart would overlap them. One win to
  go has its own format ("1 more win: Rocket"). With an empty table the
  line is empty.
- **Joker icons** live in a `JokerIcons` asset in `Runtime`, read by the
  Store and the streak screens, and later by 6.6's joker bar. The joker
  names are `UiConfig` fields, so the Store's cards take them from there.
- **The builder** writes sixteen prefabs and places the Streak Bar under
  the Home Page instance's Streak Area in Front End as an added object;
  Home Page's own file is never written. The scroll views share one
  helper.
- **UiConfig.asset** reads "Streak: {0}" and "Best: {0}"; the code's
  defaults are without the colon.
- **Tests.** 100 new; 2,270 in all.
