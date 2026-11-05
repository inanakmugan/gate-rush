# Review fixes: B1 (lives and the clock) and B3 (unreadable save)

Fixes for two bugs from the code review (`Claude outputs/review/CODE-REVIEW.md`
on `claude/nifty-bohr-qlzhyd`). Only engine-free code (`GateRush.Meta`) and
tests changed. Nothing in Runtime, Editor, UI, Serialization or `docs/` was
touched, and the save format is unchanged.

## B1 · Setting the clock back, then restoring it, refilled lives

**Cause.** `Lives.Settle` moved `RegenStartUtc` back to any earlier reading.
When the clock was put right, the whole gap counted as time waited. The
review's case (1 life, 30-minute interval, clock set back 3 h and read, then
restored, 10 real minutes in all) gave 5 lives instead of 1.

**Fix** (`Assets/Scripts/Meta/Lives.cs`). Time is now counted on a clock of
its own, kept in memory:

- A reading at or after the counted time moves the counted clock to it.
- A reading earlier than the counted time (the device clock set back) moves
  it only by the time since the previous reading, when that moved forward.
- `RegenStartUtc` is a time on that clock, so it never moves back to an
  earlier reading. A new `Lives` (after a load) starts its clock at the
  saved start.
- At the cap, `RegenStartUtc` keeps the latest counted time (it was unused
  there). Losing a life at the cap starts the wait at that time, not at an
  earlier reading. A refill sets it the same way.

Results:

- Set back and restored: only the real time counts (the review's case gives
  1 life with 20 minutes to go). This holds across a save and load too.
- A clock left behind still counts the time between its readings, so a
  player at 0 lives is never locked out. This was Module 30's reason for the
  old "restart the wait" rule.
- A clock set forward gives what it reads, and nothing is added once it is
  corrected. With no trusted clock, a forward jump can't be told apart from
  time that really passed. D20 guards only the backward direction.

**Limits that remain:**

- A clock set back to a time that is still after every earlier reading
  (for example on the very first launch) can't be detected.
- The counted clock is not saved. If the game is closed while the clock is
  behind, the next launch counts from the saved start. That can cost part of
  an interval but never gains a life.

## B3 · An unreadable save was silently overwritten

**Cause.** `SaveRepository.Load` returned defaults for an unreadable save, and
the next `Save` (any win, loss or purchase) wrote over the old text.

**Fix** (`Assets/Scripts/Meta/SaveRepository.cs`):

- `Load` remembers the unreadable text. It still writes nothing at load time.
- The first `Save` afterwards copies that text to
  `SaveRepository.BackupKeyPrefix` (`GateRush.Save.Unreadable`), and only then
  writes the new save.
- A backup key that already holds a *different* save is never replaced. The
  copy goes to `.2`, `.3` and so on. A key that already holds the same text is
  reused without writing.
- If the copy fails, the store's exception reaches the caller (for
  `PlayerPrefs`, `SaveOrWarn` already catches it) and nothing is written under
  `GateRush.Save`. The next `Save` tries the copy again.
- The load problem message now names the backup key.
- In the unrealistic case where all 1000 backup keys hold other saves, `Save`
  throws `InvalidOperationException` and writes nothing, rather than
  overwrite.

## Tests

`Assets/Tests/LivesTests.cs`, 7 new tests:

- `CountAt_ClockSetBackThenRestored_GivesOnlyTheRealTimeWaited`: the review's
  case.
- `TryLose_AtCapWithClockSetBackThenRestored_GivesNoLife`
- `TryLose_AfterRefillWithClockSetBackThenRestored_GivesNoLife`
- `CountAt_ClockSetBackSavedThenRestored_GivesNoLife`: across a save and load.
- `CountAt_RealTimePassing_GivesOneLifePerIntervalKeepingThePartOne`: the
  normal path.
- `CountAt_ClockJumpedForward_GivesWhatItReadsAndNothingMoreOnceCorrected`
- `CountAt_ClockSetBackAndLeftThere_GivesALifePerIntervalOfReadings`: no
  lockout.
- `CountAt_AnyClockSetBackAndRestored_NeverExceedsTheRealClock`: a seeded
  property test over 200 random set-back and restore sequences. It caught a
  gap in my first version of the fix.

All existing Lives tests pass unchanged, including the three no-lockout tests
for a clock set back.

`Assets/Tests/SaveRepositoryTests.Backup.cs` (new partial of
`SaveRepositoryTests`, which is now `partial`) uses a stub codec, so it runs
without the engine. It has 6 tests:

- a corrupt load writes nothing yet
- the first save copies the text first, and only once
- an older backup is kept and the new copy goes to `.2`
- an existing identical backup is not written again
- a failed copy writes nothing and is retried
- a readable save is never copied

`Assets/Tests/SaveRepositoryTests.cs` has 1 new test with the real
`JsonSaveCodec`: `Save_AfterCorruptSave_KeepsTheCorruptTextUnderTheBackupKey`.

**Verification.** I used a scratch .NET 8 + NUnit harness, outside the repo,
that compiles `Core`, `Solver`, `Meta` and `Platform` with every test file that
doesn't reference the engine (54 files):

- With the fix: **762 passed, 0 failed.**
- With the two production files reverted: 9 of the new tests fail, as
  expected. The refill test was then made stricter, so it fails on the old
  code too.

**For the owner, in the Unity Editor:**

- Let the Editor generate the `.meta` for `SaveRepositoryTests.Backup.cs`.
- Run Edit Mode tests. The JsonSaveCodec test above, and the rest of
  `SaveRepositoryTests`, need Unity.

## Docs to update (not edited here)

`docs/Modules/30-wallet-and-lives.md` (around lines 124–129, 223 and 271–276)
still describes the old rule: "restart the wait at `nowUtc`". That rule is
what caused B1. The new rule is in the `Lives` class remarks. Module 29 could
mention the backup key.

## Files

- `Assets/Scripts/Meta/Lives.cs`
- `Assets/Scripts/Meta/SaveRepository.cs`
- `Assets/Tests/LivesTests.cs`
- `Assets/Tests/SaveRepositoryTests.cs`
- `Assets/Tests/SaveRepositoryTests.Backup.cs` (new)
- `Claude outputs/review-fixes/REPORT.md`
