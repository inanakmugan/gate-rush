# Module 25 — Sound and haptics

**Assembly:** `GateRush.Runtime`, `GateRush.Meta`, `GateRush.Platform` (tests in `GateRush.Tests`)
**Depends on:** Modules 12, 17, 18, 22, 23
**Phase:** 7.1 (brought ahead of Phase 4, D52)

---

## Responsibility

Give the game sound, and a light vibration where the browser allows it.
Every sound follows what the player sees: a board sound starts with the
animation it belongs to (Module 18), not when the move resolves. The
player can turn sound off, and the choice is kept between sessions.

In scope:

- Sound cues for the grabbed block, the exit and the peel, ice breaking,
  shutters, locks, spawns, count pops, the time bonus, the last seconds of
  the timer, the win and the loss, and button taps.
- One sound toggle on the menu screen, saved through `ISaveStore`.
- Haptic pulses on a few moments, through the browser's Vibration API.
- A `SoundConfig` asset that maps each cue to its clips and levels.

Not in scope: music, a settings screen (Phase 6), separate toggles for
sound and vibration, audio for jokers (Phase 5), a sound for the gate pull
(Module 22), native haptics (Phase 8).

---

## Public surface

```
SoundCue                            enum, Runtime
    Lift, Drop, Exit, Peel, IceBreak, ShutterOpen, Unlock, Spawn,
    CountPop, TimeBonus, TimerTick, Win, Loss, UiTap

MoveCues                            plain C#, Runtime
    static IReadOnlyList<SoundCue> Leave(MoveChanges changes)
    static IReadOnlyList<SoundCue> Arrive(MoveChanges changes)
        // the cues a stage of a move's presentation plays: each kind at
        // most once, in a fixed order; empty when the stage shows nothing

TimerTicks                          plain C#, Runtime
    static bool ShouldTick(int previousWholeSeconds, int wholeSeconds,
                           int tickFromSeconds)
        // true once for each whole second from tickFromSeconds down to 1

BoardView                           existing
    event Action<MoveChanges> LeaveStarted
    event Action<MoveChanges> ArriveStarted
        // raised when each stage starts playing; never for a
        // presentation that is skipped or finished at once

SoundConfig                         ScriptableObject, Runtime
    per cue: clips (one or more), volume, pitch range
    master volume, voice count, minimum repeat interval,
    tick-from seconds, haptic pulse lengths
    IReadOnlyList<string> Problems()

SoundPlayer                         MonoBehaviour, Runtime
    void Play(SoundCue cue)
    void Pulse(HapticMoment moment)
    bool IsOn { get; set; }

SoundSetting                        plain C#, Meta
    SaveKey "GateRush.SoundOn"; IsOn; ToData / TryFromData

IHaptics                            interface, Platform
    void Pulse(int milliseconds)

WebHaptics                          Runtime (+ a .jslib under Assets/Plugins/WebGL)
    navigator.vibrate in a Web build; does nothing in the Editor and
    where the browser has no Vibration API
```

Names are negotiable. That a move's cues are decided by plain, tested code
from `MoveChanges`, and played when the stage that shows them starts, is
not.

---

## Design decisions (owner)

### Sounds follow the animations

- `BoardView` raises `LeaveStarted` when the leave stage starts and
  `ArriveStarted` when the arrive stage starts, each with the move's
  `MoveChanges`. The sound player plays `MoveCues.Leave` or
  `MoveCues.Arrive` for it.
- A stage plays each kind of cue **once**, however many blocks it
  concerns: two blocks leaving together make one exit sound, not two
  stacked.
- A presentation that is abandoned (restart, menu) or finished at once
  plays nothing more. Sounds already playing are stopped on restart and on
  the way to the menu.

### The cues

| Cue | When | Suggested Kenney pack |
|---|---|---|
| Lift | A block is grabbed (`BeginDrag`) | Interface Sounds |
| Drop | A drag ends and the block stays on the board: settle with no clear, snap back, cancel | Impact Sounds |
| Exit | Leave stage: a block is destroyed through its gate | Interface Sounds or Digital Audio |
| Peel | Leave stage: a layered block sheds a colour | Interface Sounds |
| IceBreak | Arrive stage: a block thaws or a gate's ice breaks | Impact Sounds (glass) |
| ShutterOpen | Arrive stage: a shutter lifts | Impact Sounds (metal) |
| Unlock | Arrive stage: a lock opens | RPG Audio |
| Spawn | Arrive stage: a generator pushes a block or an elevator raises a wave | Digital Audio |
| CountPop | Arrive stage: a badge's count goes down without reaching its end | Interface Sounds (quiet) |
| TimeBonus | A move earns a time bonus, with the HUD's "+N s" | Digital Audio |
| TimerTick | Each whole second from tick-from down to 1, while the level is being played | Interface Sounds |
| Win | The win panel opens | Music Jingles |
| Loss | The loss panel opens | Music Jingles |
| UiTap | Any button on the menu, the HUD, the result panel and the introduction card | UI Audio |

Exit and Peel can both play in one leave stage. A move that clears nothing
plays only Drop.

### Variation and overlap

- Each cue can have several clips; one is picked at random, with a pitch
  picked within the cue's range. The randomness is a `System.Random` owned
  by the sound player, never `UnityEngine.Random`'s shared state.
- The player keeps a small pool of `AudioSource`s (the voice count). When
  every voice is busy the oldest is reused. The same cue does not restart
  within the minimum repeat interval.
- Levels are a master volume and a volume per cue. No `AudioMixer`: on the
  web only its volume works anyway.

### The sound toggle

- One button on the menu screen, "Sound: On" / "Sound: Off", turns sound
  and vibration on or off together. It starts on.
- The choice is stored through `ISaveStore` under its own key, with the
  same data pattern as `CompletedLevels` (Module 23). Unreadable data
  reads as on.
- With sound off nothing plays and nothing vibrates; turning it back on
  affects the next cue.

### Haptics

- `IHaptics.Pulse(milliseconds)` sits in Platform. The web implementation
  calls `navigator.vibrate` through a `.jslib`, and does nothing in the
  Editor or where the API is missing.
- Pulses: a short one on Exit, a longer one on Win and on Loss. Lengths
  are in `SoundConfig`.
- iOS browsers have no Vibration API, so on an iPhone the web build does
  not vibrate. This is accepted; native haptics belong to Phase 8.

### Audio files and the web

- Clips come from Kenney's CC0 packs, saved under `Assets/Audio/Sfx/`. CC0
  needs no credit; the README thanks Kenney anyway.
- Import settings: Force To Mono, Load Type **Decompress On Load**,
  Preload Audio Data on. On the web, Decompress On Load clips are silent
  while an iPhone is in Silent Mode; the game respects the silent switch,
  which is what players expect.
- Browsers start audio only after the first tap or key press. The menu's
  first tap unlocks it; no sound plays before any input.

### Engine rules that carry over

- No magic numbers: every volume, pitch, interval, count and pulse length
  is a `SoundConfig` field with a check in `Problems()`.
- No mutable statics. The sound player stops its voices on disable and
  destroy.
- `LevelBootstrap` wires the player once, as it wires the HUD and the
  result panel; views raise events and do not call the player directly.

---

## Left to you

- How the pool picks and reuses voices.
- The `.jslib` code and how `WebHaptics` guards the Editor and non-web
  builds.
- How the toggle looks (text is enough; an icon would need
  `ArtGenerator`).
- How Drop is told apart from a move that clears, given that `Settle` runs
  before `Present`.
- Starting volumes, pitch ranges, interval, voice count, tick-from and
  pulse lengths. The owner tunes them.
- The exact Editor steps for the owner, including the import settings.

---

## Tests

**`MoveCues`**
- A destroyed clear gives Exit; a surviving clear gives Peel; one of each
  gives both, Exit first.
- Two destroyed clears give one Exit (the central decision: one per kind
  per stage).
- A thawed block and an opened gate give one IceBreak.
- Arrive gives ShutterOpen, Unlock, Spawn (for a generator and for an
  elevator alike) and CountPop for their changes.
- `MoveChanges.None` gives nothing for either stage.

**`TimerTicks`**
- Going from 6 to 5 with tick-from 5 ticks; from 7 to 6 does not; from 1
  to 0 does not (the loss plays instead).
- A frame that skips a second (from 5 to 3) ticks once.

**`SoundSetting`**
- Round-trips on and off; no data and unreadable data read as on.

**`SoundConfig`**
- A cue with no clip, a volume outside 0 to 1, a pitch at or below 0 or a
  minimum above its maximum, a voice count below 1 and a negative
  tick-from are each reported by `Problems()`.

**By hand (owner)**
- Every cue in the table plays at its moment, in the Editor and in a web
  build.
- Two blocks leaving at once make one exit sound.
- Restart and Menu during a presentation cut its sounds; nothing plays
  afterwards.
- The toggle mutes everything, survives a reload, and starts on in a fresh
  browser.
- On an Android phone, exit, win and loss vibrate; on an iPhone nothing
  vibrates and nothing breaks.

---

## Resolved during implementation

- **Surface.** Added beyond the spec: `HapticMoment` (Exit, Win, Loss);
  `SoundPlayer.Initialize(SoundConfig, IHaptics)` and `StopVoices()`;
  `SoundConfig.TryGetCue`, `PulseMilliseconds` and a Random Seed field
  (every value is valid, so it has no check);
  `InputController.DragStarted` and `DragDropped`; `ButtonPressed` on
  `HudView`, `ResultPanel`, `IntroductionCard` and `MenuScreen`.
  `MoveChanges.Of` is internal, visible to the tests through a new
  `Assets/Scripts/Runtime/AssemblyInfo.cs` that matches Core's and
  Solver's, so a stage with two clears can be tested.
- **Stage events.** `PlayLeave` and `PlayArrive` raise their event every
  time they run, also when the stage has no tween: ice shards are debris,
  so an arrive stage with only a thaw still plays IceBreak. `Rebuild`,
  `Clear` and `FinishPresentationAtOnce` never reach them, so abandoned
  and finished-at-once presentations stay silent.
- **Drop.** `Settle` runs before a move's outcome is known, but
  `OnMoveApplied` has its `MoveChanges` in the same call, so the bootstrap
  plays Drop there when `Clears` is empty; a clearing move sounds from its
  leave stage. A release with no move, a rejected move and `CancelDrag`
  raise `DragDropped`. Accepted gap: on the logged path where a move's
  clear cannot be worked out, that move plays Drop instead of Exit. A drag
  held when the time runs out plays Drop and Loss together.
- **Taps and stopping.** `ButtonPressed` is raised after a button's own
  request, so Restart and Menu stop the voices first and the tap is still
  heard; turning sound off plays no tap, turning it on does. Voices stop
  on restart, on every level load (Next included, which cuts the win
  jingle) and on the way to the menu, after `CancelDrag`. `PlayCue`,
  `Pulse` and `StopSounds` also check that the player is still alive,
  since `InputController.OnDisable` cancels a drag while the scene is torn
  down.
- **Configuration.** A missing Sound Player or Sound Config logs one error
  and the game runs silently; `SoundConfig.Problems()` is logged once at
  start and a cue with no clip is silent. `Problems()` also reports a
  pitch above 3, the engine's limit, and an empty clip slot beside
  assigned ones. The Sound On / Sound Off labels live in `RuntimeConfig`
  with the other menu texts. The title page is three button rows; with
  Levels hidden the sound button moves up into its row.
- **Sounds.** From Kenney's CC0 packs, under `Assets/Audio/Sfx/`, mono,
  Decompress On Load, Preload Audio Data on: UiTap `click_003`; Exit
  `confirmation_001`; Peel `maximize_009`; IceBreak
  `impactGlass_light_000` to `004`; ShutterOpen `metalLatch`; Unlock
  `metalClick`, raised by 7 dB to sit with the others; Spawn `phaseJump1`;
  CountPop `rollover4`; TimeBonus `powerUp8`; TimerTick `switch10`; Win
  `jingles_STEEL10`; Loss `jingles_STEEL11`. Candidates were picked by
  measuring length, loudness, brightness and pitch movement, and very low
  thuds (around 100 Hz) were left out because phone speakers barely play
  them. Per-cue volumes were set from measured loudness so the cues sit at
  similar levels.
  *Later (7.7).* The volumes were set again for the finished game, by
  LUFS and by ear: Exit 0.26, Peel 0.25, TimeBonus 0.30, IceBreak 0.60,
  ShutterOpen 0.58, Spawn 0.41, Unlock 1.0 (events, within about 3 dB);
  Win 0.23 and Loss 0.22 (about 4 dB under the events, since they play
  in a quiet moment); CountPop 0.65, TimerTick 0.54, UiTap 0.45; Lift
  0.80 and Drop 0.50.
- **Lift and Drop.** Pack sounds for both grew tiresome within a level,
  because they are the only sounds that play on every move. They were
  replaced by sounds made for the game: `lift_soft` (a button click with
  its top cut, about 10 ms) and `drop_soft` (a wood tap with its top cut,
  shortened to 60 ms and lowered by 6 dB), both at volume 0.3 with a
  slight pitch spread. Sounds that play on every move are judged over a
  whole level, not one move; they should be felt more than heard.
  *Later (Phase 8).* In the web build UiTap and Lift were silent and Drop
  faint. Unity encodes a web build's clips as AAC, which needs at least
  1024 samples (about 23 ms) and can alter the first 1024: `click_003` and
  `lift_soft` were 10 ms, and `drop_soft`'s attack fell in that span. The
  three are now WAV files of 100 ms: 1024 samples of silence, the sound
  unchanged, then silence; their old .meta files were renamed, so the
  references hold. A padded clip re-encoded as Vorbis crackled after the
  sound, so the three are imported as PCM, which the importer does not
  normalize, in the Editor or on the web. Their volumes were set again by
  ear: Lift 0.55, Drop 0.35, UiTap 0.20. Measured in the build, they play
  at the same level on the web as in the Editor. Native haptics were not
  built: the web is the only target (D82).
