# Module 43 — Joker unlocks

**Assembly:** `GateRush.Meta`, `GateRush.Runtime`, `GateRush.UI`, `GateRush.UI.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 19, 31, 33, 36, 37, 40, 41, 42
**Phase:** 7.2

---

## Responsibility

Open the jokers one at a time in the campaign, as D76 decides. The Clock
unlocks at level 5, the Rocket at level 10 and the Broom at level 15. A
new player starts with no jokers; reaching a joker's level gives one of
it, and a New Joker card on that level introduces it. Until then the
joker's button in a level and its card in the Store show it locked.

In scope:

- The unlock rule in `Meta`: whether a joker is unlocked, the joker given
  when the campaign reaches its level, and a locked joker refused when
  bought or used.
- The unlock levels in `MetaConfig`; the starting jokers set to 0.
- The locked look of the joker bar's buttons and the Store's joker cards.
- The New Joker card, in the introduction card of Module 19.
- The joker's button marked on its unlock level until it is used there.
- A streak reward for a locked joker saying when it opens.

Not in scope: the thirty-level campaign and the levels built around each
joker (7.3, D75); new art (7.4); any change to what a joker does (D65 to
D67) or to free play (D66).

---

## Public surface

```
MetaRules                           existing, gains
    int JokerUnlockLevel(JokerKind kind)    // 1-based campaign level
    bool IsJokerUnlocked(JokerKind kind, int campaignPosition)

MetaGame                            existing
    bool IsJokerUnlocked(JokerKind kind)
    CampaignWin ReportCampaignWin(int goldReward)
        // also gives one of each joker whose level the new position reaches
    PurchaseResult TryBuyJoker(JokerKind kind)  // refused, unchanged, when locked
    JokerUse TryUseJoker(JokerKind kind)        // refused, unchanged, when locked

CampaignWin                         existing, gains
    IReadOnlyList<JokerKind> JokersUnlocked     // by this win, in kind order

MetaConfig                          existing, gains
    clockUnlockLevel = 5, rocketUnlockLevel = 10, broomUnlockLevel = 15

JokerButtons, JokerButton           existing; a locked state with the level
JokerBadge                          existing, gains Locked

FrontEndState                       existing, gains
    bool IsJokerUnlocked(JokerKind kind), int JokerUnlockLevel(JokerKind kind)

IntroductionCard                    existing; also shows a New Joker card
```

Names are negotiable. That the unlock is decided in `Meta` from the
campaign position and the configuration, with no new save data, and that
free play never sees a lock, is not.

---

## Design decisions (owner)

### The rule (D76)

- Each joker has an unlock level, a campaign level number counted from 1,
  in `MetaConfig`: Clock 5, Rocket 10, Broom 15.
- A joker is unlocked when the campaign position (0-based, D58) is at or
  past its level's position, that is `position >= level - 1`. It stays
  unlocked after its level is won and for a finished campaign. A level of
  1 means unlocked from the start.
- When a campaign win advances the position onto a joker's level, that
  joker is given once: one added to the inventory. A save already past the
  level when this module arrives gets nothing back for it; its jokers are
  already in the inventory.
- `TryBuyJoker` and `TryUseJoker` refuse a locked joker and change
  nothing; the result says it was locked. The screens never offer it, so
  this is a guard, not a flow.
- A streak reward (D61) for a locked joker is added to the inventory as
  today. The inventory is not shown for a locked joker, so it appears when
  the joker unlocks.
- The starting jokers become 0 of each. The owner sets the three starting
  counts in `MetaConfig.asset` to 0; the code's defaults change too.
- Free play is unchanged: three of each joker on every level (D66), never
  locked, and the New Joker card never shows there.
- The save format does not change: unlocking is derived from the
  position.

### The joker bar in a level

- In the campaign, a locked joker's button is grey and cannot be pressed.
  Its icon is dimmed, a lock icon (Kenney Game Icons `locked.png`, already
  in the project) sits over it, and the corner badge reads the level it
  opens at, "Lv 10", in place of the count or price.
- The leave confirmation and the in-level gold loop (D74) never involve a
  locked joker: its button raises no press.

### The Store

- A locked joker's card shows its icon dimmed with the lock, its name, and
  "Unlocks at Level 10" where the price and the count were. Its buy button
  is hidden or disabled, and pressing the card does nothing.
- An unlocked joker's card is as today.

### The New Joker card

- On a joker's unlock level, in the campaign, when the level loads (first
  load or Next), the introduction card shows a New Joker card after any
  mechanic cards of that level, in the same theme (Module 41): the title
  is the joker's name with "!" ("Rocket!"), the subtitle "New Joker
  Unlocked!", the joker's icon from `JokerIcons` in place of the
  illustration with the sparkles around it, and one sentence. A restart
  shows none, as for mechanic cards (Module 19).
- The countdown waits for the last card to close, as today.
- Starting texts, in configuration:
  - Clock: "Tap it to add {0} seconds to the clock.", with the rules'
    Clock bonus (20) in place of {0}.
  - Rocket: "Tap the Rocket, then a block, to clear its colour."
  - Broom: "Tap the Broom, then a block, to clear that colour from every
    block."
- Nothing about the card is saved: a player who leaves the level before
  winning it sees the card again the next time the level loads in the
  campaign.

### The marked button

- After the New Joker card closes, the joker's button pulses (a gentle
  scale and glow, on unscaled time) until the player uses that joker or
  the level ends. Nothing is saved; the next load of that level marks it
  again.

### The streak reward

- When the Streak Reward overlay (Module 37) gives a locked joker, its
  line adds the level it opens at: "+1 Rocket, opens at Level 10".
- The Win Streak ladder is unchanged.

### Configuration

- `MetaConfig`: the three unlock levels, checked in `Problems()`: each at
  least 1.
- `UiConfig`: the badge's "Lv {0}" format, the Store's "Unlocks at Level
  {0}", the streak line's format, and the lock icon if it is wired there,
  checked in `Problems()`.
- `RuntimeConfig`: the New Joker card's subtitle and the three sentences,
  checked as the mechanic texts are.

### The builder

- It writes the lock and the locked badge into the Joker Bar and the
  Store's joker cards, and the pulse onto the joker buttons. Hand-made
  prefabs stay unwritten.

---

## Left to you

- How `MetaGame` reports the jokers a win unlocked, as long as the
  bootstrap learns it from `ReportCampaignWin` and never works it out
  again on a screen.
- What `TryBuyJoker` and `TryUseJoker` return for a locked joker: a new
  value in `PurchaseResult` and `JokerUse`, or another way, named in the
  plan with every switch it touches.
- How the bootstrap knows the level it loads is a joker's unlock level in
  the campaign, and how the card queue receives the joker after the
  mechanics.
- How the pulse is drawn, within the theme.
- The prefab changes and sizes, given in the plan as before.

---

## Tests

**`MetaRules`, `MetaConfig`**
- `IsJokerUnlocked` before, at and after each level, and with a level of
  1; an unlock level below 1 is reported.

**`MetaGame`**
- A win that moves the position onto a joker's level gives one of it and
  lists it in `JokersUnlocked`; the win after gives nothing more; a win
  onto a level with no joker lists none.
- A locked joker cannot be bought or used: gold and inventory unchanged.
- A streak reward for a locked joker is added to the inventory.
- A new player has no jokers.

**`JokerButtons`, `FrontEndState`**
- A locked joker's button is locked, not interactable, and carries its
  level; an unlocked one is as today; free play is never locked.
- `FrontEndState` reports each joker's unlock state and level.

**Texts, `UiConfig`, `RuntimeConfig`, builder**
- The "Lv 10", "Unlocks at Level 10" and streak lines from their formats;
  each new text that is empty is reported; the Joker Bar and the Store's
  cards are written with the lock wired; Home Page and Top Bar stay
  unwritten.

**By hand (owner)**
- A new save: levels 1 to 4 show three locked buttons with "Lv 5", "Lv 10"
  and "Lv 15"; the Store shows three locked joker cards.
- Winning level 4 and starting level 5 shows the mechanic cards of level 5
  if any, then "Clock!" with its icon; the Clock button holds 1 and pulses
  until it is used; the Store sells the Clock.
- Restarting level 5 shows no card; leaving and coming back shows it
  again.
- A streak of 3 before level 5 gives a Clock with "opens at Level 5"; the
  Clock button shows 2 when it unlocks.
- Rocket at 10 and Broom at 15 behave the same.
- Free play on any level has three of each joker and no card.
- An existing save past level 15 keeps its jokers, all unlocked.

---

## Resolved during implementation

- **Names.** `MetaRules` answers `JokerUnlockLevel`, `IsJokerUnlocked`
  and `JokersUnlockingAt`, the one query behind both the gift on a win and
  the card on a load. `MetaGame` adds `IsJokerUnlocked` and
  `JokersUnlockedAtCampaignLevel`. `CampaignWin.JokersUnlocked` lists the
  jokers a win gave, in kind order; the bootstrap asks Meta at each
  campaign load instead, since the card shows again with no win.
- **Locked results.** `PurchaseResult.Locked` and `JokerUse.Locked`,
  checked before anything else, so a joker held from a streak reward
  stays unusable until it opens. `TrySpendJoker` became a switch over
  `JokerUse`; the purchase switches log a Locked result as a bug.
- **Level 1.** A joker with unlock level 1 is open from the start, is
  never given by a win, and shows its card on level 1 in the campaign.
- **The mark.** `JokerMarks` in `Runtime`, reaching the bar as
  `JokerButton.IsMarked`. It is set when the last card closes, cleared by
  a spend of that joker, and cleared on load, restart, leaving, a loss
  and a win; a restart therefore ends the pulse.
- **The pulse.** A Yoyo tween on unscaled time scales the button to 1.06
  over 0.9 seconds and fades a warm gold glow, 12 units outside the
  button, from 0.25 to full. The raised, selected and pulsing button stays
  inside the bottom band.
- **Locked look.** The bar's lock is 56 units over the icon, the icon at
  0.4 alpha. Every Store card carries an 80-unit lock, shown only on a
  locked joker's card, which also hides the count line and the buy button.
  Kenney's `locked.png` is imported as a single sprite.
- **The card.** `IntroductionCard.Show` takes the jokers after the
  mechanics and the Clock bonus. A joker card draws the icon from
  `JokerIcons`, wired in `RuntimeConfig`, at 360 units in the theme's
  purple.
- **Additions.** `FrontEnd` keeps the last state for the Streak Reward
  overlay and throws `InvalidOperationException` if `ShowStreakReward`
  comes before any `Show`. The bootstrap warns at start about an unlock
  level past the campaign's last level.
- **Tests.** 136 new; 2,689 in all.
