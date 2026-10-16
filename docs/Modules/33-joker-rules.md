# Module 33 — Joker rules

**Assembly:** `GateRush.Core` (tests only), `GateRush.Meta`, `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 03, 06, 07, 10, 31, 32
**Phase:** 5.1

---

## Responsibility

Make the three jokers work as rules, without any screen. Core already has
the Rocket and Broom entry points (D9): this module proves them with the
tests Phase 1 left for Phase 5, including the Broom's chain reaction.
`MetaGame` learns to spend a joker, buying one on the spot when the
campaign has none left (D65). The level session learns to apply a Rocket,
a Broom and a Clock, and only after their use is known to be valid. Free
play gets its own allowance of three of each per level (D66).

In scope:

- Edit Mode tests for `MoveResolver.TryClearBlock`,
  `MoveResolver.TrySweepColor` and the targeting table (D11).
- `MetaGame.TryUseJoker` and the Clock's seconds in `MetaRules` and
  `MetaConfig`.
- `LevelSession` members to check and apply a Rocket and a Broom; the
  Clock through the run's countdown.
- `FreePlayJokers`, the per-level free-play allowance, in `Runtime`.

Not in scope: the joker bar, aiming, the bootstrap's calls and the
presentation of joker clears (Module 34). Nothing visible changes in this
module. Core's code changes only if a test finds a bug; a bug found is
fixed in this module with its regression test.

---

## Public surface

```
MoveResolver                        existing, Core — no change expected
    bool TryClearBlock(ctx, state, blockIndex, out result, out timeBonusSeconds)
    bool TrySweepColor(ctx, state, color, out result, out timeBonusSeconds)

MetaRules                           existing, Meta
    gains: int ClockBonusSeconds { get; }      // positive

JokerUse                            enum, Meta
    Used, BoughtAndUsed, NotEnoughGold

MetaGame                            existing, Meta
    JokerUse TryUseJoker(JokerKind kind)
        // one from the inventory when it holds one; otherwise, when the
        // balance covers JokerPrice(kind), spends it: the joker is bought and
        // used in one step and never enters the inventory

MetaConfig                          existing, Runtime
    gains: clock bonus seconds (20), free play jokers per level (3)

LevelSession                        existing, Runtime
    bool CanRocket(int blockIndex)
    bool TryRocket(int blockIndex)
    bool CanSweep(BlockColor color)
    bool TrySweep(BlockColor color)
        // Try* apply through MoveResolver, replace State, and raise
        // StateChanged and TimeBonusEarned as TryApply does; false changes
        // nothing

FreePlayJokers                      sealed class, plain C#, Runtime
    FreePlayJokers(int perLevel)
    int CountOf(JokerKind kind)
    bool TryUse(JokerKind kind)
    void Refill()                              // back to perLevel of each
```

Names are negotiable. That a joker is spent only after its use is known to
be valid, that buying at 0 never adds to the inventory, and that free play
never reaches `MetaGame`, is not.

---

## Design decisions (owner)

### The Core entry points (D9, D11)

- `TryClearBlock` clears one targetable block's outer colour in place,
  with no movement and no gate; `TrySweepColor` clears that colour from
  every targetable block showing it. Both emit `ColorCleared` and run the
  same fixpoint as a move (ARCHITECTURE, Core concept 3).
- Targeting: normal, frozen and locked blocks can be targeted; a block
  under a closed shutter, a destroyed block and a block not yet spawned
  cannot.
- A layered block hit by a Rocket loses its outer colour and survives with
  the next; a Broom clears each matching block once, never two layers of
  one block (D26).
- A Rocket or Broom that destroys a key-carrying block delivers its key,
  as an exit does (M8).
- The M10 time bonus is reported for every block a joker destroys, summed
  over the resolution.

### Spending a joker (D65)

- `TryUseJoker` takes one from the inventory when there is one: `Used`.
  With none, it spends `JokerPrice(kind)` when the balance covers it:
  `BoughtAndUsed`; the inventory is unchanged. Otherwise `NotEnoughGold`
  and nothing changes. Lifetime gold never changes.
- It does not check the target: the caller asks `LevelSession.CanRocket`
  or `CanSweep` first, then `TryUseJoker`, then `TryRocket` or `TrySweep`,
  which then cannot fail. Module 34 owns that order; this module makes
  each step available and tested.
- It touches neither the streak nor the campaign position.
- The caller saves after `Used` and `BoughtAndUsed` (D63).

### The session

- `CanRocket(blockIndex)` and `CanSweep(color)` answer without changing
  anything.
- `TryRocket` and `TrySweep` apply the joker, replace `State`, raise
  `StateChanged`, and raise `TimeBonusEarned` with the summed bonus when
  it is positive, exactly as `TryApply` does for a move, so `LevelRun`
  decides a win the same way.
- The Clock needs no session member: its seconds go to the run's countdown
  with `Countdown.AddBonus(rules.ClockBonusSeconds)`. A level without a
  countdown cannot use a Clock.

### Free play's allowance (D66)

- `FreePlayJokers` starts with `perLevel` of each kind. `TryUse` takes one
  and is false at 0. `Refill` restores every kind. It is not saved.
- `perLevel` comes from `MetaConfig`, at least 0; it is not part of
  `MetaRules`, since `Meta` knows nothing of free play.

### Configuration

- `MetaConfig` gains the Clock's seconds (20, positive) and the free-play
  allowance (3, at least 0), each checked in `Problems()`. `ToRules()`
  passes the Clock's seconds on.
- No magic numbers; tests build their own `MetaRules` through
  `MetaRulesFixture` with values different from the defaults.

---

## Left to you

- Test fixtures for the Core tests: the existing board builders in
  `Assets/Tests` (`Fixture`, `Corpus`) are the place to start.
- Whether `CanRocket` and `CanSweep` reuse `BoardState.CanBeTargeted`
  directly or a shared helper.
- How the save fixtures change, if at all: nothing new is saved in this
  module.

---

## Tests

**`MoveResolver` jokers** (the central tests of this module)
- A Rocket clears a normal block, a frozen block and a locked block; it is
  refused for a block under a closed shutter, a destroyed block and an
  unspawned one, and the state is unchanged.
- A Rocket on a layered block exposes the next colour; the block survives.
- A Broom clears the colour from every targetable block showing it and
  leaves blocks under a closed shutter alone; with no targetable match it
  is refused.
- A Broom never clears two layers of one block in one sweep.
- The Broom's chain reaction: one sweep crosses a gate threshold, a
  shutter threshold and a frozen block's threshold at once, the opened
  shutter's region empties an elevator, and its next wave arrives, all in
  one resolution. (Roadmap Phase 5; MECHANICS, Jokers.)
- A Rocket destroying a key carrier opens its lock.
- The time bonus of every block a joker destroys is summed.

**`MetaGame.TryUseJoker`**
- With one in the inventory: `Used`, one fewer, gold unchanged.
- With none and enough gold: `BoughtAndUsed`, the price spent, the
  inventory still 0, lifetime gold unchanged.
- With none and too little gold: `NotEnoughGold`, nothing changed.
- The streak and the campaign position never change.

**`LevelSession`**
- `CanRocket` and `CanSweep` change nothing. `TryRocket` and `TrySweep`
  raise `StateChanged` once and `TimeBonusEarned` with the sum when
  positive; refused ones raise nothing.
- A joker that clears the last block makes `IsSolved` true.

**`FreePlayJokers`, `MetaRules`, `MetaConfig`**
- Starts with the allowance; `TryUse` at 0 is false; `Refill` restores all
  three.
- A Clock bonus that is not positive is refused by `MetaRules` and
  reported by `Problems()`; a negative free-play allowance is reported by
  `Problems()`.
