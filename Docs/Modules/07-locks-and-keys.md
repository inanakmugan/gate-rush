# Module 07 — Locks and keys (M8)

**Assembly:** `GateRush.Core`
**Depends on:** Modules 01, 02, 03, 06
**Phase:** 1.8

---

## Responsibility

Fill in `MoveResolver.ApplyKeyEffects`, the last empty extension point in the
drain loop. A block that carries a key consumes it when it dies, and the key
applies to the lock it targets.

This is where the fixpoint loop first closes a cycle. Phase 1.7's chains ran one
way: a clear advances a counter, a counter opens something, and the opened thing
emits nothing. A key effect can *clear a colour*, which emits a new event, which
advances a counter, which opens something. The loop now feeds itself.

### What is not in scope

**M5 is already complete.** The roadmap pairs shutters with this phase, but
everything M5 needs landed earlier: unreachability through `IsCellFree`,
immovability through `CanMove`, untargetability through `CanBeTargeted`, and
threshold opening in phase 1.7. Only the visibility half — hiding contents from
the player — remains, and that is presentation (phase 2). Update `ROADMAP.md`
rather than inventing work here.

**Generators and elevators** (M6, M9) stay untouched. `CheckSpawnTriggers`
remains a no-op.

---

## Public surface

No new public types. Three changes behind existing surfaces:

### `BlockDefinition` and `SpawnedBlock` — one new validation

A block may carry a lock **or** a key, never both. `LockId` and
`KeyTargetLockId` cannot both have values. Reject at construction, naming the
offending block.

### `BlockSpec` — the fields the resolver needs by flat index

`RequiredKeyCount`, `KeyTargetLockId` and `KeyEffect` join `LockId`, for the same
reason `Axis` did in D29: the resolver addresses blocks by flat index and must not
need a second lookup path for spawned slots.

### `LevelContext` — two precomputed lookups

Resolving "which block owns lock *n*" and "which blocks carry keys for lock *n*"
by scanning every block would run inside the drain loop, once per key consumed.
Precompute both in the constructor, beside the lookups D28 already put there.

`LockId` is unique per level, so the owner lookup is one block, not a set.

---

## Design decisions (owner)

**A block is either a lock or a key.** Observed in the reference game, and it
bounds the chain: a key's effect can clear its target, but that target holds a
lock, so it cannot itself carry a key. One key produces at most one extra clear.
Without this rule the drain loop could cascade arbitrarily deep through key
chains, and `MaxResolutionPasses` would be doing real work rather than catching
authoring errors.

**The effect fires once, when the required count is reached.** A lock needing two
keys does nothing on the first; the second triggers the effect exactly once. Not
once per key.

**Both effects remove the lock.** `UnlockMovement` removes it and stops.
`ClearOuterColor` removes it *and* clears one colour. This matches the rule that
a newly exposed colour is never locked (`MECHANICS.md`, M8): a layered target
survives the effect as an ordinary unlocked block showing its next colour.

*Consequence:* `ClearOuterColor` on a single-colour target both unlocks and kills
it, which is fine — the unlock is invisible but harmless, and treating the two
effects uniformly keeps the branch small.

**A key consumed against a dead target does nothing.** The player can destroy a
locked block with a rocket or a broom before its key ever arrives (locked blocks
are targetable, D11). When that key later dies, mark it consumed and apply
nothing. The key-carrying block stays on the board as an ordinary block and still
has to be cleared through its own gate.

*Rejected:* removing the key block when its target dies. It raises three
questions with no good answers — how many blocks vanish when a lock needs two
keys, whether the removal emits `ColorCleared` and so advances counters, and what
happens to a level whose thresholds depended on that block's clear. A rare corner
case does not warrant a mechanic; it warrants one `if`.

**Keys are consumed on death, not on each clear.** A layered key-carrying block
that sheds a colour and survives has not delivered its key. Only the clear that
empties its stack does.

**Jokers do not differentiate.** `CanBeTargeted` answers one question, and locked
blocks answer yes to it. The rocket clears a locked block's outer colour; the
broom includes locked blocks showing its colour. Neither joker gets a special
case, or `CanBeTargeted` would stop meaning one thing.

---

## Where it happens

`ApplyKeyEffects` runs inside `DrainEvents`, once per dequeued event:

```
1. Did this block just die?      builder.IsAlive(event.BlockIndex) == false
   No  -> return. A shed layer delivers nothing.

2. Does it carry a key?          spec.KeyTargetLockId has a value
   No  -> return.

3. Mark the key consumed.        builder.ConsumeKey(event.BlockIndex)

4. Count consumed keys for that lock, using the precomputed key list.
   Below RequiredKeyCount -> return.

5. Find the lock's owner through the precomputed lookup.
   Owner already dead      -> return. The key is spent; nothing to apply.
   Owner already unlocked  -> return. Cannot happen with a once-only trigger,
                              but guard rather than assume.

6. Unlock the owner.             builder.Unlock(ownerIndex)

7. If the effect is ClearOuterColor, call ClearOuterColor on the owner. That
   enqueues a fresh event, which this same drain loop then processes — the
   counters it advances, and the thresholds they cross, are handled exactly as
   any other clear.
```

Step 7 is why the loop closes. Nothing else about `DrainEvents` changes.

---

## Left to you

- Where the two lookups live on `LevelContext` and what they expose.
- Whether counting consumed keys per lock is a scan of that lock's key list or a
  running count on the builder. The list is short; measure before optimising.
- The `SuccessorBuilder` mutators (`ConsumeKey`, `Unlock`) and their copy-on-write
  slots, following the existing pattern.
- Whether `MaxResolutionPasses` still holds. One key adds at most one extra clear,
  so a resolution's pass count should not grow — but a broom consuming several
  keys at once produces several extra clears in one drain, and that is the first
  action in the project that can do so. Confirm rather than assume.

---

## Tests

**Validation**
- A block carrying both a lock and a key is rejected at construction, and the
  message names the block.
- A block carrying only a lock, and one carrying only a key, are both accepted.

**Key consumption**
- A key-carrying block that dies has its key marked consumed.
- A layered key-carrying block that sheds one colour and survives does **not**
  consume its key.
- A key is consumed exactly once even if the resolution continues afterwards.

**`UnlockMovement`**
- A locked block rejects every move while locked, and still obstructs.
- Its key dies; the block is unlocked in the same resolution and moves on the
  next.
- With `RequiredKeyCount` 2: one key leaves it locked, the second unlocks it.
- Once unlocked it stays unlocked across later moves.

**`ClearOuterColor`**
- Its key dies; the target's outer colour is cleared **and** the lock removed.
- A layered target survives showing its next colour, unlocked and movable.
- A single-colour target dies.
- The counters credit the colour that was removed from the target, not the key
  block's colour.

**Dead target**
- A rocket destroys a locked block; its key block then dies. The key is consumed,
  nothing is applied, nothing throws.
- The same through a broom.
- The key-carrying block is still on the board and still has to be cleared.

**Jokers**
- A rocket clears a locked block's outer colour.
- A broom includes locked blocks showing its colour.

**Chains — the loop closing on itself**
- A key's `ClearOuterColor` effect clears a target, and that clear crosses a gate
  threshold; the gate is open by the end of the same resolution.
- The same clear crosses an unfreeze threshold.
- A broom that consumes several keys at once applies every one of their effects
  within one resolution.
- A key's effect kills its target, and that death's own event is drained in the
  same pass rather than deferred.

**Bound**
- A construction where a key's effect would cascade indefinitely is impossible by
  the lock-or-key rule; assert instead that a chain of *n* independent keys
  resolves without exceeding `MaxResolutionPasses`.
