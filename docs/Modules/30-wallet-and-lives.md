# Module 30 — Wallet and lives

**Assembly:** `GateRush.Platform`, `GateRush.Meta`, `GateRush.Serialization`, `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 23, 29
**Phase:** 4.2

---

## Responsibility

Give the meta game its two balances: gold and lives. The wallet keeps the
spendable gold and the lifetime gold earned (D22). The lives regenerate on
UTC timestamps, against a clock that tests can move (D19, D20, D59). Both
become sections of the save, in format version 2, and an older save reads
with them at their defaults (D60). The numbers come from a `MetaConfig`
asset.

In scope:

- `ITimeProvider` in `Platform`, `SystemTimeProvider` in `Runtime`,
  `ManualClock` in the tests.
- `MetaRules`, `Wallet` and `Lives` in `Meta`.
- `SaveData` gains the two sections; the codec moves to version 2 and
  reads version 1.
- `MetaConfig`, a `ScriptableObject` in `Runtime`, and `LevelBootstrap`
  passing its rules to the repository.

Not in scope: when gold is paid and a life is lost, which `MetaGame`
decides in Module 31 (D57, D58); the store and refilling with gold (Module
31); showing gold or lives (Module 32); unlimited lives for a while (D55).
Nothing visible changes in this module.

---

## Public surface

```
ITimeProvider                       interface, Platform
    DateTime UtcNow { get; }        // DateTimeKind.Utc

MetaRules                           sealed class, immutable, Meta
    MetaRules(int lifeCap, TimeSpan lifeRegenInterval, int startingGold)
        // throws ArgumentOutOfRangeException: cap below 1, interval not
        // positive, starting gold negative
    int LifeCap { get; }
    TimeSpan LifeRegenInterval { get; }
    int StartingGold { get; }

Wallet                              sealed class, Meta
    static Wallet Starting(MetaRules rules)   // StartingGold, lifetime 0
    Wallet(int balance, int lifetimeEarned)   // restore; throws on negatives
    int Balance { get; }
    int LifetimeEarned { get; }
    void Earn(int amount)                     // both go up
    bool TrySpend(int amount)                 // false, unchanged, when short
        // a negative amount throws ArgumentOutOfRangeException

Lives                               sealed class, Meta
    static Lives Full(MetaRules rules)
    Lives(MetaRules rules, int count, DateTime regenStartUtc)   // restore
    int CountAt(DateTime nowUtc)
    bool IsFullAt(DateTime nowUtc)
    TimeSpan? UntilNextLifeAt(DateTime nowUtc)  // null when full
    bool TryLose(DateTime nowUtc)               // false at 0 lives
    void Refill(DateTime nowUtc)                // to the cap
    int StoredCount { get; }                    // for the codec
    DateTime RegenStartUtc { get; }             // for the codec

SaveData                            existing, Meta
    SaveData(CompletedLevels, SoundSetting, Wallet, Lives)
    Wallet Wallet { get; }
    Lives Lives { get; }
    static SaveData CreateDefault(MetaRules rules)

ISaveCodec                          existing, Meta
    bool TryDecode(string text, MetaRules rules,
                   out SaveData data, out string problem)
        // rules build the defaults of sections an older version lacks (D60)

SaveRepository                      existing, Meta
    SaveRepository(ISaveStore store, ISaveCodec codec, MetaRules rules)

JsonSaveCodec                       existing, Serialization
    CurrentFormatVersion = 2
    SaveDataDto gains: int gold; int lifetimeGold; int lives;
                       long livesRegenStartUtcTicks

SystemTimeProvider : ITimeProvider  Runtime
    UtcNow => DateTime.UtcNow

MetaConfig                          ScriptableObject, Runtime
    life cap (5), life regen minutes (20), starting gold (500)
    IReadOnlyList<string> Problems()
    MetaRules ToRules()
```

Names are negotiable. That every question about lives takes the time as an
argument, that the saved state is a count and a timestamp, and that the
codec builds missing sections from the rules, is not.

---

## Design decisions (owner)

### Time

- `ITimeProvider` gives only the current UTC time. Frame delta stays with
  Unity in `Runtime`; `Meta` never needs it. `ARCHITECTURE.md`'s Platform
  table is corrected after the module.
- `Meta` never reads `DateTime.UtcNow`. Every `Lives` member that depends
  on time takes `nowUtc`; the caller asks `ITimeProvider` once and passes
  it on.
- Times are UTC. A `DateTime` that is not `DateTimeKind.Utc` passed to
  `Lives` throws `ArgumentException`: a local time would shift every timer
  by the time zone.

### Lives (D59)

- The saved state is the stored count and the moment the wait for the next
  life started. At the cap, that moment means nothing.
- Asked at `nowUtc`, the count is the stored count plus one life for each
  full interval since the wait started, never above the cap. A member that
  changes the lives first settles them at `nowUtc`: the lives that came
  back are added, and the wait start moves forward by whole intervals, so
  the part of an interval already waited is kept.
- `TryLose` at the cap starts the wait at `nowUtc`; below the cap it
  leaves the wait running. At 0 it returns false and changes nothing.
- A `nowUtc` earlier than the wait's start gives no life, and settling
  then starts the wait again at `nowUtc`. Setting the clock back never
  gains a life.
- `Refill` sets the count to the cap.
- A restored count above the cap reads as the cap; a negative one throws.

### Wallet (D22)

- `Earn` adds to the balance and to the lifetime gold; `TrySpend` takes
  from the balance only. The lifetime gold never goes down.
- The starting gold is not earned: it is in the balance, not in the
  lifetime gold.
- Amounts are never negative: a negative amount throws. Zero is allowed
  and changes nothing. Sums stop at `int.MaxValue` rather than
  overflowing.

### The save, version 2 (D60)

```
{
    "formatVersion": 2,
    "completedLevelIds": [0, 1, 3],
    "soundOn": true,
    "gold": 640,
    "lifetimeGold": 140,
    "lives": 3,
    "livesRegenStartUtcTicks": 638640000000000000
}
```

- The codec writes version 2 and reads versions 1 and 2. A version-1 save
  reads with `Wallet.Starting(rules)` and `Lives.Full(rules)`, the same
  defaults `SaveData.CreateDefault(rules)` gives, built through those
  `Meta` factories and not through numbers of the codec's own.
- In version 2, negative gold, negative lifetime gold, negative lives or
  ticks outside `DateTime`'s range make the save unreadable. They are not
  corrected.
- `SaveRepository`'s problem for an unreadable save no longer lists the
  defaults it uses: it says the defaults of a new player are used, so the
  text stays true as sections are added.
- `SaveFixture.EveryField` and `AssertSameSave` gain the new fields: gold,
  lifetime gold, lives below the cap and a wait start that is not the
  default.

### Configuration

- `MetaConfig` holds the cap, the interval in minutes and the starting
  gold, with the defaults above, and a `[CreateAssetMenu]` entry.
  `Problems()` reports a cap below 1, an interval that is not positive and
  a negative starting gold. `ToRules()` builds the `MetaRules`.
- `LevelBootstrap` gets a serialized `MetaConfig` field and a
  `SystemTimeProvider`, and builds the repository with the rules. A
  missing config or one with problems logs an error and the bootstrap
  stops, as it does for a missing or broken `RuntimeConfig` ("nothing is
  drawn"): the save cannot be read without the rules. It is not soft like
  `SoundConfig`.
- No magic numbers: the defaults live only on `MetaConfig`'s fields. Tests
  build their own `MetaRules`.

### Engine rules that carry over

- `Meta` and `Platform` stay free of `UnityEngine`. No mutable statics.
- `ManualClock` lives in the tests; nothing in the shipped game can set
  the time.

---

## Left to you

- Whether `Lives` settles lazily inside each member or through one private
  helper.
- How `UntilNextLifeAt` rounds; it is never negative.
- The `MetaConfig` asset's location, next to the existing config assets,
  and its tooltips.
- Whether the codec reads its versions through one DTO with defaulted
  fields or one per version, as long as `FromDto` stays the one place
  versions are told apart.
- The exact owner Editor steps: creating the asset and assigning it on the
  bootstrap's object, with the full Hierarchy path.

---

## Tests

**`Lives`** (a `ManualClock`, rules of cap 5 and 20 minutes)
- Full lives are 5, full, with no time until the next.
- Losing one at the cap gives 4; 19 minutes 59 seconds later still 4; at
  20 minutes 5.
- Losing three at once gives 2; 40 minutes later 4, with 20 minutes to the
  next.
- Losing one more while a life is on its way does not restart its wait.
- Regeneration stops at the cap: two hours after losing one, 5.
- The roadmap's case: win three (lives untouched), lose one, wait forty
  minutes: full again, and no more than full. (The central decision: lives
  come back from timestamps against a moved clock.)
- A clock set back an hour gives no life, and the wait then runs from the
  earlier time.
- `TryLose` at 0 is false and changes nothing; `Refill` gives the cap.
- A restored count above the cap reads as the cap; a negative one throws;
  a non-UTC time throws.

**`Wallet`**
- `Starting` holds the starting gold with lifetime 0.
- `Earn` raises both; `TrySpend` lowers only the balance; spending more
  than the balance is false and changes nothing; zero changes nothing; a
  negative amount throws; sums stop at `int.MaxValue`.

**`MetaRules` and `MetaConfig`**
- A cap below 1, an interval that is not positive and negative starting
  gold each throw in `MetaRules` and are each reported by `Problems()`.
  `ToRules()` carries the values over.

**`JsonSaveCodec` and `SaveRepository`**
- Version 2 round-trips `SaveFixture.EveryField()`.
- A literal version-1 save reads with its levels and sound, the starting
  gold, lifetime 0 and full lives. (D60's central case.)
- Negative gold, lifetime gold or lives, and out-of-range ticks, are
  unreadable.
- A fresh store and a carry-over of the old keys both give the starting
  gold and full lives.
- Version 3 is refused as too new.

**By hand (owner)**
- With the save from Module 29 in place: Play, complete a level, stop,
  Play again. No warning in the Console, and the completed marks and the
  sound setting are kept: the version-1 save was read and written back as
  version 2.
- With the Meta Config field cleared on the bootstrap: Play logs one error
  naming it, and nothing is drawn. Assign it again afterwards.
