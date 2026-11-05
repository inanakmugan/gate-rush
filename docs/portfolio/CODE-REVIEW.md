# Gate Rush: independent code review

> Kept for reference. Since this review B1 and B3 are fixed (D83) and CI
> with coverage was added, so section 4 gap 1 and the answers to questions
> 2, 6 and 9 are out of date. The remaining findings are listed in
> docs/ROADMAP.md (Follow-ups from the code review).

**Scope**

- All production code under `Assets/Scripts` (Core, Solver, Meta, Platform,
  Serialization, Runtime, UI) and `Assets/Editor`, plus the test suite in
  `Assets/Tests`.
- Correctness and design only. Performance is covered separately in
  `Claude outputs/perf-analysis/` and is not repeated here.

**Baseline:** `main` at `9a3836f` (*docs: close Phase 8 and record the
web-only release (D82)*). About 116,700 lines of C#: roughly 44,000
production, 25,000 editor and 45,000 tests.

**Method**

- Core, Meta and Platform were read in full by hand.
- Runtime, UI/Editor, Solver/Serialization and the tests were each reviewed
  by a separate read-only pass. Every bug reported here was re-checked
  against the source before it was written down.
- Two scratch .NET 8 harnesses outside the repo compiled the engine-free
  assemblies straight from `Assets/` and ran throwaway checks:
  - **Core + Meta + Platform harness:** findings B1, B3 and B13 were
    reproduced.
  - **Core + Solver harness:** solver correctness was checked over 320 random
    boards from the repo's own `RandomBoards`/`SearchCorpus` helpers (see §5).
- The harness and its output are not committed. The repo is unchanged apart
  from this folder.

**Verification labels**

| Label | Meaning |
|---|---|
| **Verified** | Reproduced in a harness |
| **Confirmed in code** | The failure path was traced through the source and its callers, but nothing was executed (it needs Unity) |
| **Plausible** | Depends on engine behaviour that was not run |

**Overall verdict**

- The core is the strongest part of the project. The rules are small, pure,
  heavily documented and heavily tested. I found no correctness bug in
  `MoveResolver`, `BoardState` or any search strategy.
- The defects sit at the edges:
  - time handling (the lives clock, the countdown on pause)
  - persistence failure modes
  - input edge cases
  - the editor tooling
- The main maintainability cost is a handful of very large classes.

---

## 1. Bugs and risky code, by severity

### B1 · Medium · Verified: setting the clock back, then forward again, refills lives

**Where:** `Assets/Scripts/Meta/Lives.cs`, `Settle`, line 171, the `if (nowUtc < RegenStartUtc)` branch.

**What the spec says:** D20 says "Clamp negative elapsed time so moving the
device clock backwards cannot grant lives". Module 30 says "Setting the clock
back never gains a life".

**What the code does:**

- When `nowUtc` is earlier than the wait's start, `Settle` moves
  `RegenStartUtc` back to that earlier time.
- When the clock is put right again, the whole gap counts as time waited.

**Harness output:** 1 life, a 30-minute interval, and 10 real minutes waited.

1. The device clock is set back 3 hours and the game is opened, so the lives
   are read.
2. The clock is restored.

The result is 5 lives, not 1:

```
[OBSERVED] B clock rollback: lives at real time before rollback=1, after rollback+restore=5
```

**Why the tests miss it:**

- The tests (`LivesTests.cs:119-175`) check the prescribed *mechanism*,
  "restart the wait from the earlier time".
- They do not check the *property* the spec actually promises, so the spec
  contradicts itself and the tests pass.
- This can happen without cheating, too: an NTP correction or a timezone or
  clock fix on a phone moves the clock back and then forward.

**Fix:**

- When `nowUtc < RegenStartUtc`, leave `RegenStartUtc` alone and return
  without granting anything, so the wait resumes when the real time catches
  up.
- Alternatively, keep a "latest time seen" high-water mark in the save and
  never credit time before it.
- Add a property test: for any sequence of clock readings, lives at real time
  T never exceed what monotonic time would have given.

### B2 · Medium · Confirmed in code (engine behaviour plausible): the countdown keeps running through pauses, focus loss and hitches

**Where:** `Assets/Scripts/Runtime/LevelBootstrap.cs:1572`, `run.Tick(Time.unscaledDeltaTime)`.

**What the code does:**

- There is no `OnApplicationPause` or `OnApplicationFocus`, and no handling
  of `visibilitychange`, anywhere in Runtime.
- `Countdown.Tick` does not clamp the delta, and `Time.maximumDeltaTime` does
  not apply to `unscaledDeltaTime`.

**Scenario:**

- A multi-second hitch is taken off the countdown in full. Likely causes on
  WebGL are the first-time shader compile, a GC pause, or a mobile browser
  throttling a background tab.
- If the browser resumes the tab with a large frame delta, a campaign level
  can go straight to Lost. `ReportCampaignLoss` then takes a life and resets
  the streak, for a level the player never saw end.

D57 accepts that closing the tab costs nothing. Nothing in the docs covers
switching away and back.

**Fix:**

- Clamp the delta passed to `run.Tick` to a `RuntimeConfig` value, for
  example 0.25 s.
- Stop the countdown in `OnApplicationFocus(false)` / `OnApplicationPause(true)`.
- Add `CountdownTests` for a large delta.

**Verify:** in the itch.io build, switch tabs for 30 s during a level and
watch the timer.

### B3 · Medium · Verified (with a stub codec): an unreadable save is silently overwritten with a fresh profile on the first change

**Where:**

- `Assets/Scripts/Meta/SaveRepository.cs`, `Decode`.
- `Assets/Scripts/Serialization/JsonSaveCodec.cs:235-291`.

**What the code does:**

- When `TryDecode` fails, `Load` returns defaults with `Unreadable`, and the
  stored text is kept.
- The next `SaveOrWarn` (any win, loss or purchase) then writes the defaults
  over it.
- There is no backup key and no "keep the unreadable text aside" step.

**What makes a save unreadable:** the codec is strict. Any of these marks the
whole save Unreadable:

- one negative count
- ticks out of range
- `bestWinStreak < winStreak`
- a `formatVersion` above this build's

**Harness output:**

```
[OBSERVED] C unreadable save overwritten: outcome=Unreadable; stored text after first save='ENCODED' (original is gone, no backup key)
```

**Scenario:** a future build writes one bad field. A cached older build then
meets a newer save, or a codec bug rejects valid saves. In each case every
affected player loses gold, the campaign position and completed levels on
their next level end.

**Fix:**

- On `Unreadable`, copy the text to `GateRush.Save.Unreadable.<timestamp>`
  before anything saves.
- Reset only the offending section, as `ReadProfile` already does for the
  profile (Module 38), rather than the whole save.

### B4 · Medium · Confirmed in code: closing the Level Editor discards unsaved edits without asking

**Where:** `Assets/Editor/LevelEditorWindow.cs:374` (`OnDestroy`).

**What the code does:**

- It only cancels validation. Nothing in `Assets/` sets
  `EditorWindow.hasUnsavedChanges` or `saveChangesMessage`.
- New, Open and Play do ask first (`ConfirmDiscardIfDirty`).

**Scenario:** closing the tab or quitting Unity loses the draft, with undo
history and all.

**Fix:** set `hasUnsavedChanges` from the dirty flag. Unity then prompts on
close and on quit with no further code.

### B5 · Medium-low · Confirmed in code: a missed release leaves a block drawn where the rules do not have it

**Where:**

- `Assets/Scripts/Runtime/InputController.cs:224-235`.
- `DragController.TryBegin` (`DragController.cs:153` calls `Cancel()`).

**What the code does:**

- A press while `drag.IsDragging` is already true starts a new drag.
- `TryBegin` silently cancels the old one, but nothing calls `view.Snap` for
  it, and `DragDropped` is never raised.

**Scenario:**

- The mouse is released outside the canvas on WebGL. The tab loses focus
  mid-drag (`runInBackground: 0`). Or `Pointer.current` switches between
  mouse and touch.
- The next press then leaves the first block floating at its dragged
  position until the next full redraw. The board shows one thing and the
  rules hold another.

**Fix:** in `Update`, call `CancelDrag()` (which snaps) before `TryBegin` when
a drag is already in progress.

### B6 · Medium-low · Confirmed in code: a missing nullable field in a level file becomes a real id

**Where:**

- `Assets/Scripts/Serialization/LevelDtos.cs:144-147, 240-241`.
- `LevelSerializer.cs:401-404, 421-424`.

**What the code does:**

- The format uses `-1` for "none".
- `JsonUtility` fills an absent `int` with `0`, and the DTO fields have no
  `= -1` initialiser.

**Scenario:**

- A hand-edited or merge-damaged level drops `"keyTargetLockId"` while lock 0
  exists. That block silently becomes an extra key for lock 0. Every
  validation passes, and the puzzle has changed.
- If both `lockId` and `keyTargetLockId` are dropped, the load throws "carries
  both a lock (id 0) and a key", which is misleading.

**Risk:** low today, because the editor always writes every field.

**Fix:** initialise the sentinel fields to `-1` in the DTOs, which
`JsonUtility` respects.

### B7 · Low-medium · Confirmed in code: a missing top-level array loads as an empty one

**Where:** `LevelSerializer.cs:370-375, 528-538` (`ConvertEach`, `FromCoordDtos`).

**What the code does:**

- A null `gates`, `shutters`, `generators` or `elevators` becomes an empty
  list.
- Module 08 says an absent required array must be reported, but only `cells`,
  `colorStack`, `queue` and `waves` are checked.

**Scenario:** a truncated file without `"blocks"` loads as a level that is
already solved. Without `"gates"`, it loads as one that cannot be won.

**Fix:** extend `RequireArray` to the top-level arrays.

### B8 · Low-medium · Confirmed in code: false shutter-coverage warning for elevators and generators under a shutter

**Where:** `Assets/Editor/DraftValidator.cs:625-670`.

**What the code does:** coverage counts only walls and the starting cells of
`draft.Blocks`.

**Why that is wrong here:**

- M9 and D42 place an empty elevator region's first wave before the first
  move, even under a closed shutter.
- Generators spawn under a closed shutter the same way.

So a shutter over an empty elevator always warns, although play fills it.

**Fix:** when the draft forms a context, measure coverage on
`BoardState.CreateInitial(ctx)`. Worth a one-line spec note too.

### B9 · Low · Confirmed in code: the threshold warning's bound is too loose

**Where:** `DraftValidator.cs:398-449`.

**What the code does:** every threshold is compared against all the clears in
the level, including clears that cannot happen before the threshold is met.

**Scenario:** one ordinary block, plus a block frozen until 2 clears. The
total is 2, so there is no warning. But the frozen block can never thaw.

**Impact:** the solver still reports the level unsolvable, so this is a
weaker warning, not a shipped bug.

**Fix:** subtract the target's own clears. For a shutter, also subtract the
clears of blocks under it.

### B10 · Low · Confirmed in code: a winning move can show a time bonus

**Where:** `LevelSession.Commit` raises `TimeBonusEarned` before
`StateChanged`. `LevelBootstrap.OnTimeBonusEarned` (about line 2441) still
sees `Outcome == None`.

**Scenario:** the move that wins also destroys a time-bonus block. "+N" and
its sound play over the win, after the countdown has stopped.

**Fix:** raise the bonus after the state change, or check `IsSolved` first.

### B11 · Low · Plausible: Escape while editing the profile name closes the whole Profile overlay

**Where:** `Assets/Scripts/UI/FrontEnd.cs:555-567` reads Escape with no check
on text-field focus.

**Scenario:** TMP reverts the edit and, in the same frame,
`navigation.Back()` closes Profile.

**Fix:** skip Back while an `EventSystem` selection is a `TMP_InputField`
being edited.

### B12 · Low · Confirmed in code: editor tooling can half-write or overwrite assets

- **`UiPrefabBuilder.cs:1256-1308`:**
  - It saves 26 prefabs one at a time; the doc says twenty-five.
  - A throw in `RequirePressFeedback` partway leaves the set half-written,
    while the exception doc says "nothing was written".
- **`LevelEditorSettings.GetOrCreate` (`LevelEditorSettings.cs:676-688`):**
  if the asset cannot be loaded yet, for example a window restored before the
  import after a Library rebuild, `CreateAsset` writes defaults over the
  committed asset.
- **`LevelEditorWindow.cs:3702-3709`:** an `IOException` from
  `File.WriteAllText` escapes `OnGUI`, inside an open
  `BeginHorizontal` group.
- **`DraftHistory.Redo`:** it does not enforce the history cap.

### B13 · Low (design) · Verified: Core accepts a corner push in either direction

**Where:**

- `Assets/Scripts/Core/Move.cs` carries no direction.
- `MoveResolver.TryApplyMove` clears a zero-distance move through any
  compatible gate on an edge the axis allows.

**What the spec says:** D43 says a corner block clears only when pushed
toward the gate's edge. That is enforced only in
`DragController.End` → `BlockReachability.CanClearInPlace(..., push)`.

**Harness output:**

```
[OBSERVED] A corner push: TryApplyMove(zero-distance) cleared=True; CanClearInPlace(Left)=False
```

**Impact:**

- Nothing is wrong for the player, because input filters the push.
- Nothing is wrong for the solver either: a solution may push a corner block
  "the wrong way", but a way that clears always exists.
- Still, a MECHANICS rule lives in Runtime. Any second input path (a
  replay, a bot, a tutorial script) would bypass it.

**Fix:** a `Direction? Push` on `Move`, validated in the resolver.

### Lower-risk notes

- **`BlockReachability.ReachableOrigins`:** it returns its internal `List`
  as `IReadOnlyList`, and the next scan mutates that list. Today's only
  caller (`MoveGenerator`) uses it at once. Document it as "valid until the
  next call", or copy.
- **Canonical-mode searches:** they return `Unsolvable` when they exhaust a
  *pruned* graph (`BreadthFirstStrategy.cs:214-217`,
  `AStarStrategy.cs:261-264`). It is documented as "within the requested
  mode", and `LevelSolveRunner` only trusts a canonical `Solvable`. But a new
  caller reading `Status` alone would take a false negative for a proof. A
  distinct `ExhaustedPruned` status would remove the trap.
- **A missing save field reads as 0:** for example `livesRegenStartUtcTicks`,
  which refills all lives, or `soundOn`, which turns sound off, though the
  default is on (`JsonSaveCodec.cs:155-195`). This is accepted in Module 29
  because `Encode` writes every field. Fine, as long as nobody hand-migrates
  a save.
- **`SaveOrWarn` (`LevelBootstrap.cs:696`):** it catches only
  `PlayerPrefsException`. A codec bug throwing anything else escapes into
  `Update` or a UI callback.

## 2. Violations of the project's own rules

| # | Rule (source) | Where | Note |
|---|---|---|---|
| R1 | "Runtime changes all of it through one object, `MetaGame`" (ARCHITECTURE, D63) | `LevelBootstrap.cs:806` `save.Completed.MarkCompleted` (free play too, though D58 says free play reports nothing), `:515` sound, `:530` haptics, `:548` `profile.TrySetName`, `:573` `SetAvatar` | Route them through `MetaGame`, or record the settings/profile exception in D63 |
| R2 | CLAUDE.md: "targets WebGL and Android" | CLAUDE.md vs D82 ("no Android or iOS build is made") | Stale project instructions. `LevelBootstrap.cs:487` always builds `WebHaptics`, which is right for D82 but wrong for the CLAUDE.md claim |
| R3 | "Texts, formats and colours set from code live in `UiConfig`" (ARCHITECTURE) | `UiPrefabBuilder.cs` holds about 770 `private const`s, including colours baked into prefabs (`OverlayTextColorHex`, `NameRefusalColorHex`, `TextOutlineColorHex`, …, lines 361-426); `TimeFormat.cs:42` `"{0:00}:{1:00}"` | Two sources of truth for colour |
| R4 | No magic numbers / no dead tunables | `ArtRecipe.Mechanics.cs:265, 517, 543` (`lockCountDropPixels`, `elevatorDoorGapPixels`, `elevatorHandleOffsetPixels`) are never read, and the lock-count drop disagrees with `RuntimeConfig` (12 px vs 20/128 cells); `RuntimeConfig` `FrameLipOrder` (`:2161`) and `BlockLipOrder` (`:2188`) are serialised, validated and reset, but nothing draws with them | Inspector values that do nothing are worse than constants |
| R5 | "Public surfaces expose `IReadOnlyList<T>`, never `List<T>` or arrays" (CONVENTIONS) | `SimulatedLeaderboardConfig.cs:66` `Players()`; `SimulatedStoreConfig.cs:46` `Products()`; `LevelDraft.cs:53-58, 609-680` (public `List<T>` with setters) | The editor model may be a deliberate exception; if so, document it |
| R6 | "XML documentation comments on every public type and member" | About 250 undocumented public members, mostly `LevelDraft.cs` (68), `UiPrefabBuilder.cs` (61), `DraftValidator.cs` (13), and the nested `UiConfig.RibbonEntry`/`BackgroundEntry` | Core, Solver and Meta are fully documented |
| R7 | "Invalid level data throws with a message naming the offending element" | `LevelContext.cs:997, 1007`: "A key targets lock N, which does not exist" does not say which block | — |
| R8 | Tests: `MethodName_Condition_ExpectedResult`; one test class per production class | 80 of 2,237 test methods have no condition segment (12 in `MoveResolverTests`, e.g. `:197` `TryApplyMove_CannotPassThroughAnotherBlock`); 12 test classes match no production class (`MoveResolverJokerTests`, `MoveResolverSpawningTests`, `ShippedLevelsTests`, …) | The split test classes are sensible; amend the convention rather than the code |

**Checked and clean:**

- No `UnityEngine` in the four engine-free assemblies, which is enforced by
  `noEngineReferences`.
- `GateRush.Solver` is still Editor-only, and nothing in Runtime or UI
  references it.
- `PlayerPrefs` is used only in `PlayerPrefsSaveStore`.
- No `GameObject.Find`, `SendMessage` or `Find*ObjectOfType`.
- No `UnityEngine.Random`.
- No threads in runtime code. The threads in `ValidationPipeline` and
  `LevelEditorWindow` are editor-only.
- No commented-out code, no public serialised fields, and no hand-written
  `.meta` files.
- Every `OnEnable` subscription has its `OnDisable`.

## 3. Design and maintainability

### D1 · `LevelBootstrap` (2,838 lines) is the project's single point of fragility

**What it owns:**

- scene validation (`HasEverythingAssigned`)
- the level catalog
- save load and save
- sound, haptics and profile settings
- the leaderboard
- the store and coin flights
- campaign flow: start, next, lives, end of content, streak announcements
- the level lifecycle: load, fade, restart, leave, confirmation, charging a
  life
- countdown ticking
- joker targeting and spending
- working out move changes and their fallbacks
- camera and safe-area fitting
- dev keys

**Why that matters:**

- It has no test class. The D57 rules ("does this leave cost a life, is this
  result reported once, can this level start") exist only inside a
  MonoBehaviour and are checked only by playing.
- The Runtime pass traced them and found them correct today. A refactor
  could break them silently.

**Proposed split:**

- a plain-C# `LevelFlow` state machine, with inputs for move, joker,
  timeout, leave and restart, and outputs for meta reports and save
  requests; tested like `Core`
- `StoreFlow` and `ProfileFlow` presenters
- a `CameraFit` component
- the MonoBehaviour shrinks to forwarding Unity callbacks

### D2 · Very large classes elsewhere

- **`RuntimeConfig.cs`:** 3,867 lines and 427 serialised fields, covering
  board art, sorting orders, drag tuning, effects, introduction-card layout
  and texts, and validation. Split it into per-concern ScriptableObjects
  (`DragSettings`, `BurstSettings` and `ScreenFadeSettings` already show the
  pattern).
- **`LevelEditorWindow.cs`:** 4,072 lines covering IMGUI drawing, grid input
  (`1726-2394`), draft mutations (`2394-2587`), the inspector (`2587-3175`),
  the validation worker (`3405-3590`), file IO and Play. Extract a
  `DraftDocument` (path, dirty flag, history, save and load; this also fixes
  B4), a `ValidationRun`, and an inspector.
- **`UiPrefabBuilder.cs`:** 4,248 lines, one static class for 26 prefabs.
  One builder per prefab family would also make B12's all-or-nothing write
  practical.

### D3 · Duplication

- **In `LevelBootstrap`:**
  - `OnMoveApplied` and `OnJokerApplied` (`:2093`, `:2305`) repeat the same
    try, fallback and log block.
  - "Front end if open, else level UI" appears three times (`:1333`,
    `:1390`, `:1406`).
  - `ForgetShownMeta(); RefreshMeta();` appears about ten times.
- **In UI:**
  - `FrontEnd` re-implements `LevelOverlayStack`'s pending test-purchase
    answer and close-if-on-top (`FrontEnd.cs:590-596, 1124-1133` vs
    `LevelOverlayStack.cs:30, 92-117`).
  - `Contains(IReadOnlyList<UiOverlay>, …)` is copied three times.
  - Rebuild-on-count-change plus subscribe/unsubscribe is written four
    times.
- **In the editor:** `DraftValidator.AddLockKeyWarnings` (`:457`) repeats
  Core's lock-key data error, so an invalid draft shows the same message
  twice.

### D4 · Dead code

- `UiBuilder.AddRoundedBox` (`UiBuilder.cs:72`).
- `RuntimeConfig`: `BadgePaddingCells`, `BadgeShadowMarginCells`,
  `LayerEdgeCells`, `ShardSprite`, `UiLitMaterial` (`:1537, 1540, 1519, 1477,
  1241`).
- `UiConfig`: `JokerSelectedColor`, `BackgroundColor`, `IdleTabIconColor`,
  `StreakColor` (`:696-723`).
- `ResizeImpact.RemovedElementCount`.
- `LevelBootstrap`'s `isLeaderboardAskedAgain` branch (`:647-688`) is
  unreachable while `SimulatedLeaderboardService.IsBusy` is always false.
  This one is fine to keep, because it is there for a real service.
- One-shot tools whose work is done: `ThirdPartyCleanup` (deletes its
  allow-list immediately, with no confirmation dialog; every listed file is
  already gone) and `ThirdPartyFontMove`. Delete them and their tests.

### D5 · Naming

- `LevelContext.SuggestedTimeBudgetSeconds` is the *shipped* countdown
  (`LevelBootstrap.cs:1654`). "Suggested" is the editor's word. Rename it to
  `TimeBudgetSeconds` at the next level-format bump.
- `ArtGenerator.cs:351` recreates the outline font with a new GUID whenever
  its settings change. Today both references are re-assigned. A future
  reference would go missing silently.

## 4. Test gaps

**The suite overall**

- 3,385 Edit Mode tests: 2,022 `[Test]` and 1,080 `[TestCase]` rows, in
  2,237 methods. None is ignored, and none depends on the real clock or on
  `UnityEngine.Random`.
- Core, Solver, Meta and Serialization each have a test class per
  production class, except `BlockValidation`, which is tested indirectly.
- The `MECHANICS.md` checklist is well covered:
  - fixpoint overflow
  - D43 corner pushes
  - M7 arrival at a side gate
  - D45 locks under shutters and in unspawned waves
  - an elevator held back by a player's block
  - D42 spawning under a shutter
  - the win requiring exhausted spawners
  - save migration v1→v4

**Gaps, by risk**

1. **No CI and no coverage measurement.** There is no `.github/`, and no
   `com.unity.testtools.codecoverage`. The tests run only when the owner
   presses Run All.
2. **The campaign flow is untested.** These have no tests:
   - `LevelBootstrap` (result → `MetaGame` → save)
   - `InputController`
   - `IntroductionCard`
   - `MechanicIllustration`
   - `SoundPlayer`
   - `LevelEditorWindow`

   This is D1 above; extracting `LevelFlow` makes it testable.
3. **The lives tests check the mechanism, not the property** (B1). Add a
   randomised clock-sequence test.
4. **No Countdown test for a large delta, or for a pause** (B2).
5. **The broom cascade never reaches a generator.**
   - `MECHANICS.md` asks for a broom stress test that opens gates, shutters
     and frozen blocks and "may in turn trigger elevators and generators".
   - `MoveResolverJokerTests.cs:216` covers the gate, frozen block, shutter
     and elevator, but no joker test makes a generator spawn.
6. **No test for an unreadable save followed by a save** (B3).
7. **UI behaviour is tested only through prefab wiring.** `UiPrefabBuilderTests`
   checks children and references, not what `FrontEnd`, `LevelUi` or the
   overlays do with a state.
8. **Two test files load shipped JSON from `Resources`.** `ShippedLevelsTests`
   and `CampaignBoardFitTests` do it on purpose, to check the shipped content,
   so that is legitimate. `UiPrefabBuilderTests` creates and deletes asset
   folders (`:328, :337`), which makes it the one suite that can leave the
   project dirty if it is interrupted.

## 5. What was verified, and how

| Finding | Status | How |
|---|---|---|
| B1 lives clock rollback | **Verified** | Core+Meta harness, `MetaRulesFixture` rules, 30 min interval |
| B3 unreadable save overwritten | **Verified** | Same harness, in-memory `ISaveStore`, a codec that refuses the text |
| B13 corner push accepted by Core | **Verified** | Same harness, 3×3 board, bottom gate, block at (0,0) |
| Solver: stratified BFS = plain BFS = A\* (status and length); A\* `h` admissible; nearest-next-clear never contradicts BFS and never claims a proven length it lacks; canonical mode never says Unsolvable where exhaustive BFS solved | **Verified, no defect** | Core+Solver harness over the repo's `RandomBoards` (200 boards 3×3, 120 boards 4×4 with generators and elevators). Zero mismatches. Nearest-next-clear returned one non-optimal length, correctly left unproven |
| B2, B4–B12 | Confirmed in code / Plausible | Source and callers traced; they need the Unity runtime |

---

## 6. Ten questions a senior reviewer at a mobile studio would ask

**1. "Your countdown is 30 seconds and a player gets a phone call. What happens?"**

*Honest answer:* this build ships on the web only. Nothing handles focus or
pause, and the countdown ticks on raw `unscaledDeltaTime` (B2). Whether the
time away is charged depends on how the browser resumes the tab.

*Say:* "That's a gap. I'd stop the timer on focus loss, clamp the per-frame
delta from config, and add a test for a large delta. On a native build I'd
hook `OnApplicationPause`. It's an afternoon's fix, and I can show you
exactly where it goes."

**2. "How do you stop players from changing the device clock to get lives?"**

*Honest answer:*

- Lives are timestamps read against an injected `ITimeProvider` (D20, D59),
  so this is testable. But there is no server time, so moving the clock
  forward always works.
- The review also found that setting the clock *back* and then restoring it
  grants lives, which contradicts my own spec (B1).

*Say:* "Offline, you can only limit the damage. I'd fix the rollback by never
crediting time before the latest moment seen. A shipped F2P game would take
server time at session start and keep local time as the fallback, and the
`ITimeProvider` seam means only one class changes."

**3. "Why not just use MonoBehaviours and ScriptableObjects for the rules, like most Unity projects?"**

*Honest answer:*

- Core, Meta and the solver need to run millions of states and thousands of
  tests without a scene.
- `noEngineReferences` makes the boundary a compile error (D19, D21).
- The cost is a translation layer: DTOs, and `RuntimeConfig` feeding plain
  config objects.

*Say:* "Rules in plain C#, Unity for presentation. That's why 3,400 tests run
in seconds, and why the solver can use the very `MoveResolver` the game uses."

**4. "How do you know a level is solvable, and how long does the solver take?"**

*Honest answer:*

1. Exhaustive A\* runs first. It is admissible and verified against BFS.
2. Then nearest-next-clear, which is complete only without generators or
   elevators, with an A\* cross-check that throws on disagreement.
3. The answer is one of three, and quality is separate from existence (D4,
   D36–D38).
4. The final level had to drop the elevator, because no candidate could be
   decided within budget.

*Say:* the actual timings from the editor footer for two or three levels, and
the honest limit: "Spawner-heavy levels can come back Indeterminate. Then I
simplify the level rather than ship it unverified."

**5. "What's the biggest class in the project, and what would you do with it?"**

*Honest answer:* `LevelBootstrap`, at 2,838 lines with no tests. It mixes the
level lifecycle, meta reporting, the store, the profile, the camera and dev
keys (D1).

*Say:* "That's my main regret. I'd extract a plain-C# `LevelFlow` state
machine, so the life-charging rules are unit-tested like the core, and leave
the MonoBehaviour as a thin adapter. In my second project, Loop Blast, the
game runs as a deterministic simulation under test, and that's the model I'd
follow."

**6. "What happens if the save is corrupted, or a new version can't read it?"**

*Honest answer:*

- The save is versioned and migrated v1→v4 (D56, D60), and a newer version is
  refused, never misread.
- But any decode failure loads defaults, and the next save overwrites the old
  text, so the progress is gone for good (B3).

*Say:* "Migration is solid and tested. Failure handling is too blunt: I'd
keep the unreadable copy under a backup key and reset only the broken
section. Right now one bad field costs a player everything."

**7. "How would you add real IAP, ads or a backend leaderboard?"**

*Honest answer:* `IPurchaseService` and `ILeaderboardService` already exist.
The simulated implementations say they are simulated (D70, D72, D73). There
is no receipt validation, no consumable/ownership model and no restore flow.

*Say:* "The seams are there. Unity IAP would be one new `IPurchaseService`,
with server-side receipt validation before `MetaGame.ReportGoldPurchase`
credits gold. Today that call trusts the purchase service, which is fine only
because nothing is real."

**8. "How do you test the UI?"**

*Honest answer:*

- Screens are dumb: they show a state and raise events (D68). Their
  prefabs' wiring is tested.
- Their behaviour (what `FrontEnd` does with a given `FrontEndState`) is not
  unit-tested, and there are no Play Mode tests.

*Say:* "The rules a screen could get wrong don't live in the screen. That's
the point of `FrontEndState`. Next I'd add Play Mode smoke tests for the main
flows, and CI to run them."

**9. "Is there CI? How do you stop regressions?"**

*Honest answer:* no. There is no CI and no coverage measurement in this repo.
The discipline is a regression test per bug fix, in the same session.

*Say:* "Not here, and I'd fix that first. Gate Rush was my first project.
Loop Blast runs its tests in CI with about 95% coverage, which is what I'd
set up on day one now."

**10. "How much of this did you write, and how much did an AI write?"**

*Honest answer:* the README says so plainly. The architecture, the rules, the
module specs and the 82 decisions are the owner's, written first. The
implementation was written with Claude Code against those specs.

*Say:* "I designed it and wrote the specs; the implementation was AI-assisted
under those specs and my review. What I'm offering you is the design
judgement, and I can defend every decision in the record. For example: why
exit is move-triggered (D25), why the save rules live in `Meta` with the JSON
inverted into `Serialization` (D56), and why I removed key effects when the
corpus found a hole (D45)." Then be ready to walk through `MoveResolver` and
`BoardState` live. Interviewers will test whether you know the code.
