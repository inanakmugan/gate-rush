# Module 31 — Meta game

**Assembly:** `GateRush.Meta`, `GateRush.Serialization`, `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 29, 30
**Phase:** 4.3

---

## Responsibility

Complete the meta game's rules: the win streak and its joker rewards
(D61), the joker inventory, the store (D62), the campaign's position, and
the one `MetaGame` object that `Runtime` reports campaign results and
purchases to (D63). The new sections join the save as format version 3.
Everything is pure C# and tested without a scene, including the roadmap's
"win three, lose one, wait forty minutes".

In scope:

- `JokerKind`, `JokerInventory`, `WinStreak`, `StreakReward` and
  `CampaignProgress` in `Meta`.
- `MetaRules` gains the prices, the streak table and the starting jokers;
  `MetaConfig` gains their fields.
- `MetaGame` with its result types.
- `SaveData` gains the sections; the codec moves to version 3 and reads
  versions 1 to 3.

Not in scope: calling `MetaGame` from the bootstrap, showing anything, and
the prompts (Module 32); using jokers on the board (Phase 5); the store's
screen (Phase 6). Nothing visible changes in this module. Setting every
level's gold reward to 50 is a content change in the Level Editor, done on
main after this module.

---

## Public surface

```
JokerKind                           enum, Meta
    Clock, Rocket, Broom

JokerInventory                      sealed class, Meta
    static JokerInventory Starting(MetaRules rules)
    JokerInventory(int clocks, int rockets, int brooms)   // restore; throws on negatives
    int CountOf(JokerKind kind)
    void Add(JokerKind kind, int count)       // count >= 0; stops at int.MaxValue
    bool TryUse(JokerKind kind)               // false at 0; for Phase 5

WinStreak                           sealed class, Meta
    WinStreak(int current, int best)          // restore; throws on negatives
    int Current { get; }
    int Best { get; }                         // never below Current

StreakReward                        readonly struct or sealed class, Meta
    int Wins { get; }      JokerKind Joker { get; }      int Count { get; }

CampaignProgress                    sealed class, Meta
    CampaignProgress(int position)            // restore; throws when negative
    int Position { get; }                     // index in the level order

MetaRules                           existing, Meta
    gains: int JokerPrice(JokerKind kind); int LivesRefillPrice;
           int StartingJokers(JokerKind kind);
           IReadOnlyList<StreakReward> StreakRewards   // Wins ascending
    // construction is left to you; every value is validated

MetaGame                            sealed class, Meta
    MetaGame(SaveData save, MetaRules rules, ITimeProvider time)
    SaveData Save { get; }
    int CampaignPosition { get; }
    bool CanStartCampaignLevel()              // at least one life now
    CampaignWin ReportCampaignWin(int goldReward)
    bool ReportCampaignLoss()                 // true when a life was lost
    PurchaseResult TryBuyJoker(JokerKind kind)
    PurchaseResult TryRefillLives()

CampaignWin                         result type, Meta
    int GoldEarned; int Streak; IReadOnlyList<StreakReward> RewardsGiven

PurchaseResult                      enum, Meta
    Bought, NotEnoughGold, AlreadyFull        // AlreadyFull only for a refill

SaveData                            existing, Meta
    gains: JokerInventory Jokers; WinStreak Streak; CampaignProgress Campaign

JsonSaveCodec                       existing, Serialization
    CurrentFormatVersion = 3
    SaveDataDto gains: int winStreak; int bestWinStreak; int campaignPosition;
                       int clocks; int rockets; int brooms

MetaConfig                          existing, Runtime
    gains: clock price (100), rocket price (200), broom price (300),
           lives refill price (300), starting clocks, rockets and brooms (1 each),
           streak rewards (3 Clock x1, 5 Rocket x1, 10 Broom x1)
```

Names are negotiable. That `MetaGame` is the only way `Runtime` changes
the meta game, that it never saves, and that every number comes from
`MetaRules`, is not.

---

## Design decisions (owner)

### MetaGame (D63)

- `MetaGame` wraps one `SaveData` and changes it in place. It never saves:
  every call that changes the save says so in its result, and the caller
  saves the whole save (Module 32 does it through the bootstrap's
  `SaveOrWarn`).
- It reads the time once per call from `ITimeProvider` and passes it to
  `Lives`. It never reads the system clock.
- Free play reports nothing to it (D58). It has no free-play member.

### A campaign win

`ReportCampaignWin(goldReward)` does, in order:

1. Earns `goldReward` into the wallet, balance and lifetime (D22, D58). A
   negative reward throws; zero is allowed.
2. Adds one to the streak, raising the best streak when it passes it.
3. Gives the jokers of every `StreakReward` whose `Wins` equals the new
   streak (D61). With the default table, at most one.
4. Advances the campaign position by one.

It returns the gold earned, the new streak and the rewards given. It
spends no life and needs none: a win costs nothing (D57).

### A campaign loss (D57)

- `ReportCampaignLoss()` loses one life at the current time and sets the
  streak to 0, keeping the best. The campaign position does not move.
- It is the one call for every kind of loss: the countdown running out,
  Menu while playing, and Restart while playing. Telling those apart is
  `Runtime`'s job in Module 32.
- With no life left it still resets the streak and returns false. Module
  32 never lets a campaign level start without a life, so this is a guard,
  not a path.

### Starting a campaign level

- `CanStartCampaignLevel()` is true when there is at least one life at the
  current time. Starting spends nothing (D57).

### The store (D62)

- `TryBuyJoker(kind)`: when the balance covers the price, spends it and
  adds one joker: `Bought`. Otherwise `NotEnoughGold` and nothing changes.
- `TryRefillLives()`: full lives give `AlreadyFull` and cost nothing;
  otherwise, when the balance covers the price, spends it and refills:
  `Bought`; otherwise `NotEnoughGold`.
- Lifetime gold never changes in the store.

### The win streak (D61)

- The streak counts campaign wins in a row; any campaign loss sets it to
  0. The best streak is the highest the streak has reached and never goes
  down.
- A reward is given when the streak becomes exactly its `Wins`, so
  reaching 3 again after a loss pays the Clock again. Past the last
  threshold, nothing more.
- `StreakRewards` are validated: `Wins` positive and strictly ascending,
  `Count` at least 1.

### The joker inventory

- Counts per kind, never negative. `Add` with a negative count throws.
  `TryUse` takes one and is false at 0; nothing calls it before Phase 5.
- A new player has the starting jokers: 1 of each by default.

### The campaign position (D58, D63)

- The position is the index of the campaign level in the level order: 0
  for a new player, raised by one on each campaign win.
- `Meta` does not know how many levels there are. Whether the position is
  past the last level, and which level it names, is the catalog's question
  in Module 32.

### The save, version 3 (D60)

```
{
    "formatVersion": 3,
    "completedLevelIds": [0, 1, 3],
    "soundOn": true,
    "gold": 640,
    "lifetimeGold": 140,
    "lives": 3,
    "livesRegenStartUtcTicks": 638640000000000000,
    "winStreak": 2,
    "bestWinStreak": 6,
    "campaignPosition": 4,
    "clocks": 1,
    "rockets": 0,
    "brooms": 2
}
```

- The codec writes version 3 and reads versions 1 to 3. A save older than
  version 3 reads the new sections as a new player has them: streak and
  best streak 0, position 0, and `JokerInventory.Starting(rules)`. As in
  Module 30, these come through the `Meta` factories and `MetaRules`,
  never numbers of the codec's own.
- In version 3, a negative value in any new field, or a best streak below
  the streak, makes the save unreadable.
- `SaveFixture.EveryField` and `AssertSameSave` gain the new fields, each
  away from its default.

### Configuration

- `MetaConfig` gains the prices, the starting jokers and the streak table,
  with the defaults above, each checked in `Problems()`: prices and
  starting counts not negative, the table's `Wins` positive and strictly
  ascending, and its counts at least 1. `ToRules()` passes them on.
- The asset at `Assets/Config/MetaConfig.asset` picks up the new defaults
  when its fields are added; the owner checks them in the Inspector.

### Engine rules that carry over

- `Meta` stays free of `UnityEngine` and of `Core`. No mutable statics; no
  magic numbers outside `MetaConfig`'s fields. Tests build their own
  `MetaRules` with values that differ from the defaults.

---

## Left to you

- How `MetaRules` is constructed now that it holds more values: a longer
  constructor, a small settings object, or another shape, as long as it
  stays immutable and validates everything.
- Whether `CampaignWin` and `StreakReward` are structs or classes.
- How the streak table is serialized on `MetaConfig` (an array of a
  serializable struct is enough) and shown in the Inspector.
- Whether the codec keeps one DTO with defaulted fields, as long as
  `TryFromDto` stays the one place versions are told apart.

---

## Tests

**`MetaGame`** (a `ManualClock`, rules that differ from the defaults)
- The roadmap's case: win three campaign levels, lose one, wait forty
  minutes. The streak goes 1, 2, 3, then 0 with best 3; the gold rises by
  the three rewards; the third win gives the 3-win reward; the loss costs
  one life; forty minutes later the lives are full again. (The central
  decision: one object applies D57, D58 and D61 together.)
- A win pays the reward into balance and lifetime, raises the position by
  one and spends no life.
- A loss costs one life, resets the streak, keeps the best and leaves the
  position.
- Reaching a threshold again after a loss pays its reward again; passing
  the last threshold pays nothing.
- `CanStartCampaignLevel` is false at 0 lives and true again once a life
  comes back on the clock.
- `TryBuyJoker`: bought with enough gold, `NotEnoughGold` and nothing
  changed without; lifetime gold unchanged either way.
- `TryRefillLives`: `AlreadyFull` with full lives and nothing spent;
  bought below the cap; `NotEnoughGold` without enough gold.
- A negative gold reward throws.

**`JokerInventory`, `WinStreak`, `CampaignProgress`**
- Starting counts come from the rules; `Add`, `TryUse` at 0, negatives
  throwing, and saturation at `int.MaxValue`.
- A restored best streak below the current one, or negative values, throw.

**`MetaRules` and `MetaConfig`**
- A negative price or starting count, a reward table out of order or with
  a repeated or non-positive `Wins`, and a count below 1 are each refused
  by `MetaRules` and reported by `Problems()`.

**`JsonSaveCodec` and `SaveRepository`**
- Version 3 round-trips `SaveFixture.EveryField()`.
- Literal version-1 and version-2 saves read with a streak of 0, position
  0 and the starting jokers, and keep everything they had.
- Negative new fields and a best streak below the streak are unreadable;
  version 4 is refused as too new.

**By hand (owner)**
- With the save from Module 30 in place: Play, complete a level, stop,
  Play again. No warning in the Console, and the completed marks and sound
  are kept.
- In the Inspector, `Assets/Config/MetaConfig` shows the new fields with
  their defaults.

---

## Resolved during implementation

- **Surface.** As specified. `MetaRules` takes seven arguments, the
  per-kind prices and starting counts grouped in a `JokerAmounts` struct.
  `StreakReward` and `JokerAmounts` are plain readonly structs;
  `CampaignWin` has an internal constructor, like `SaveLoad`.
  `WinStreak.Starting()` and `CampaignProgress.Starting()` exist so the
  codec and the repository build a new player's sections through `Meta`,
  never zeros of their own (D60). `RecordWin`, `Reset` and `Advance` are
  public, like `Wallet.Earn`. An internal `JokerKinds` helper holds the
  kind range and the one exception for an undefined kind.
- **One table check.** `StreakReward.ProblemsOf` is the only check of a
  reward table: `MetaRules` throws on its first problem and
  `MetaConfig.Problems()` reports them all, so an empty `Problems()` still
  guarantees `ToRules()` cannot throw. An empty table is allowed and gives
  no rewards.
- **A loss always changes the save.** `ReportCampaignLoss` resets the
  streak and settles the lives even when it returns false at 0 lives, so
  Module 32 saves after every loss.
- **Joker purchases.** One joker per purchase is a private constant of
  `MetaGame`: a rule of D62, not a tunable.
- **Configuration.** `MetaConfig` groups its fields under Lives and gold,
  Store, Starting jokers and Win streak. The table is an array of a
  private serializable `StreakRewardEntry`, shown as a list in the
  Inspector. The existing asset was saved once in the Editor so it stores
  the new fields. `LevelBootstrap` is unchanged; its Meta Config tooltip
  still names only the Module 30 values, for Module 32 to update.
- **Tests.** `MetaRulesFixture.Build()` takes named optional values, every
  default different from `MetaConfig`'s, including the reward table (3
  Rocket x2, 5 Clock x3, 8 Broom x1); `SaveFixture.Rules` comes from it.
  Each reward-table problem has its own test. Added beyond the spec: zero
  prices and starting jokers allowed, an empty table accepted by
  `MetaRules`, `MetaConfig` and `MetaGame`, and a version-2 save carrying
  later fields that are ignored. 1878 Edit Mode tests pass.
- **By hand.** The new `MetaConfig` fields showed their defaults, a reward
  element could be added and removed, and the version-2 save of Module 30
  read and was written back as version 3 with no warning.
