# Module 29 — Save model

**Assembly:** `GateRush.Meta`, `GateRush.Serialization`, `GateRush.Runtime` (tests in `GateRush.Tests`)
**Depends on:** Modules 08, 23, 25
**Phase:** 4.1

---

## Responsibility

Put everything the game keeps into one save: one `SaveData`, saved under
one key as versioned JSON (D56). Today the completed levels (Module 23)
and the sound setting (Module 25) each have their own key and their own
text format. After this module both live in the one save, a player who
already has the old keys keeps what they held, and the bootstrap loads
once and saves the whole save on every change. Modules 30 and 31 add their
sections to it.

In scope:

- `SaveData` in `Meta`, holding `CompletedLevels` and `SoundSetting`.
- `SaveRepository` in `Meta`: loading, carrying over the old keys, saving.
- `ISaveCodec` in `Meta`; `JsonSaveCodec` and its DTO in `Serialization`.
- `LevelBootstrap` switched to the repository.

Not in scope: the wallet, lives and `ITimeProvider` (Module 30); the
streak, inventory and store (Module 31); any visible change; a store that
survives a new itch.io upload (D51); deleting the old keys.

---

## Public surface

```
SaveData                            sealed class, Meta
    CompletedLevels Completed { get; }
    SoundSetting Sound { get; }
    static SaveData CreateDefault()
        // no level completed, sound on

ISaveCodec                          interface, Meta
    string Encode(SaveData data)
    bool TryDecode(string text, out SaveData data, out string problem)
        // false for text Encode would not have written, a missing or
        // non-positive formatVersion, or a version newer than the codec
        // knows; data is then null and problem says why

SaveLoadOutcome                     enum, Meta
    Fresh, Loaded, CarriedOver, Unreadable

SaveLoad                            result type, Meta
    SaveData Data { get; }                 // never null
    SaveLoadOutcome Outcome { get; }
    IReadOnlyList<string> Problems { get; } // for the caller to log

SaveRepository                      sealed class, Meta
    const string SaveKey = "GateRush.Save"
    SaveRepository(ISaveStore store, ISaveCodec codec)
    SaveLoad Load()
    void Save(SaveData data)

CompletedLevels                     existing, Meta
    SaveKey renamed LegacySaveKey; TryFromData and FromData stay, to
    read the old key; ToData is removed
    IReadOnlyList<int> Ids { get; }        // ascending
    static CompletedLevels Of(IEnumerable<int> levelIds)

SoundSetting                        existing, Meta
    SaveKey renamed LegacySaveKey; TryFromData stays; ToData is removed

SaveDataDto                         Serialization
    int formatVersion; int[] completedLevelIds; bool soundOn

JsonSaveCodec : ISaveCodec          Serialization
    const int CurrentFormatVersion = 1
```

Names are negotiable. That the loading rules live in `Meta` behind
`ISaveCodec`, and that the save is one key with a `formatVersion`, is not.

---

## Design decisions (owner)

### One save, one key

- The key is `GateRush.Save`. `Save` encodes the whole `SaveData` and
  writes it with a single `ISaveStore.Save`. No section is ever written
  alone (D56).
- The bootstrap saves the whole save when a level is newly completed and
  when the sound is toggled, the two moments it saves today. A failed save
  is logged and the game goes on, remembering the change until it is
  closed (Module 23).

### Loading

`Load` works in this order:

1. The new key exists: decode it. Readable is `Loaded`. Unreadable gives
   the defaults, `Unreadable` and a problem, and nothing is written, so
   the unreadable text stays until the next change replaces it.
2. Otherwise, if either old key exists, carry them over. Each is read with
   its existing `TryFromData`; one that cannot be read gives its default
   and a problem, and the other is kept. The new save is written at once,
   and the outcome is `CarriedOver`.
3. Otherwise the defaults, `Fresh`, and nothing is written.

- Once the new key exists, the old keys are never read again, whatever
  they hold. They are not deleted (D56).
- `Load` never throws for anything the store returns. A store that throws
  is the caller's to handle, as today.

### The JSON

```
{
    "formatVersion": 1,
    "completedLevelIds": [0, 1, 3],
    "soundOn": true
}
```

- `formatVersion` comes first (D17). Ids are written ascending; duplicates
  read as one.
- A version newer than the codec knows is unreadable, never misread (D56).
- Modules 30 and 31 raise the version when they add a section, and a save
  of an older version then reads with the new section at its default.
  Module 29 knows only version 1, but the codec is shaped so that adding a
  version touches one place.
- `JsonUtility` fills a missing field with its default, so the codec does
  not have to notice a missing `soundOn` in version-1 text. `Encode`
  always writes every field.

### Assemblies

- `GateRush.Serialization` gains a reference to `GateRush.Meta` (D56).
  `Meta` still references only `Platform` and stays free of `UnityEngine`;
  no `JsonUtility` there.
- `ARCHITECTURE.md`'s assembly table is updated after the module, with the
  other docs.

### Engine rules that carry over

- No magic numbers or strings: the key and the version are named
  constants, and the bootstrap's log texts follow its existing pattern.
- No mutable statics.
- The bootstrap builds the repository once from `PlayerPrefsSaveStore` and
  `JsonSaveCodec`, where it builds the store today. `MenuLevels` and the
  level select keep reading `CompletedLevels` and do not change.

---

## Left to you

- Whether `SaveLoad` is a struct or a class, and the wording of problems.
- How the codec checks the version before trusting the rest of the text,
  for example by reading a small header DTO first.
- How `CompletedLevels.Of` and `Ids` are built, and whether `SoundSetting`
  gains a constructor taking its value.
- How the tests that used `ToData` are rewritten: the old formats are now
  only read, so their tests use literal strings.
- Whether you add the asmdef reference yourself, using the GUID in
  `GateRush.Meta.asmdef.meta` (read only), or leave it to the owner's
  Editor steps.

---

## Tests

**`SaveRepository`** (an in-memory `ISaveStore` and the real
`JsonSaveCodec`)
- An empty store gives the defaults and `Fresh`, and writes nothing.
- Save, then Load through a new repository over the same store, gives the
  same completed ids and sound setting, and `Loaded`.
- Only the old keys, `0,2` and `off`, give levels 0 and 2 with sound off,
  `CarriedOver`, and the new key written.
- After a carry-over, changing the old keys changes nothing: the next Load
  is `Loaded` from the new key. (The central decision: the new key wins.)
- The new key present alongside the old keys: the old keys are ignored.
- One old key unreadable: the readable one is kept, the other is its
  default, and there is one problem.
- Only one old key present: it is carried over, the other is its default,
  and there is no problem.
- An unreadable new key gives the defaults, `Unreadable` and a problem,
  and the stored text is unchanged after Load.
- A new key with a future `formatVersion` is `Unreadable`, and the stored
  text is unchanged.

**`JsonSaveCodec`**
- `Encode` writes `formatVersion` first and the ids ascending.
- Round trips of an empty and a non-empty set, with sound on and off.
- Duplicate ids read as one.
- Text that is not JSON, an empty string, a missing or zero
  `formatVersion`, and a version above the current one each give false, a
  problem and null data.

**`CompletedLevels` and `SoundSetting`**
- The existing `TryFromData` tests keep passing against literal strings in
  the old formats.
- `Of` and `Ids` round-trip, ascending, with duplicates read as one.

**By hand (owner)**
- On the machine that already played: after the update the completed marks
  and the sound setting are as before.
- Toggle sound, complete a level, then reload (a new Play in the Editor,
  and a reload of a web build): both are kept.
- After Edit → Clear All PlayerPrefs: no level completed, sound on.

---

## Resolved during implementation

- **Surface.** As specified, with: a public `SaveData` constructor, which
  the codec needs, throwing on a null section; `SaveLoad` as a sealed
  class with an internal constructor; `SaveRepository.Save` and
  `CompletedLevels.Of` throwing on null. `SoundSetting` gained no
  constructor: an object initialiser sets `IsOn`. `SaveHeaderDto` is
  internal.
- **A failed carry-over write.** Not in the spec. If writing the
  carried-over save throws, for example on a full WebGL quota, `Load`
  would have stopped `Awake` before the menu. `SaveRepository` catches any
  exception from that one write, since `Meta` cannot name
  `PlayerPrefsException`, and still returns the data as `CarriedOver` with
  a problem. Until a save succeeds, the old keys are read again at the
  next start. A store that throws while reading still reaches the caller.
- **Logging.** Each problem is logged as a warning. A carry-over logs one
  info line naming both old keys, worded to stay true when the write
  failed. The problem for an unreadable save names the defaults of version
  1; Module 30 makes it general.
- **Codec.** The header DTO is read first; an exception from `JsonUtility`
  or a null result is refused, and so are whitespace and a JSON array.
  `Encode` is pretty-printed, as `LevelSerializer` is. `FromDto` takes the
  version but has one branch until Module 30 adds a section.
- **Bootstrap.** `LoadSave` replaces `LoadCompletedLevels` and
  `LoadSoundSetting`, and `SaveOrWarn` keeps the two failure messages
  unchanged. The bootstrap no longer uses `GateRush.Platform` directly.
- **Assemblies.** The reference to `GateRush.Meta` was added to
  `GateRush.Serialization.asmdef` by its GUID.
- **Tests.** `MemorySaveStore` records every saved key, seeds old data
  without counting a save, and can fail its saves.
  `SaveFixture.EveryField` and `AssertSameSave` cover every field of the
  save; Modules 30 and 31 add their sections to both. The `ToData` round
  trips were replaced by reads of literal strings in the old formats.
- **By hand.** The carry-over, keeping changes across a new Play and a web
  reload, and a clean start after Clear All PlayerPrefs all behave as
  specified.
