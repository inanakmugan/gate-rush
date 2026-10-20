# Module 38 — Profile and Settings

**Assembly:** `GateRush.Meta`, `GateRush.Serialization`, `GateRush.Runtime`, `GateRush.UI`, `GateRush.UI.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 25, 29, 31, 35 to 37
**Phase:** 6.4

---

## Responsibility

Give the player a name, a face and a haptics switch, and keep them in the
save (D71). The avatar in the top bar opens Profile, where the player
edits the name, picks one of the thirty animal avatars and sees what the
save already knows about them. Settings gains a Haptics toggle beside
Sound. The save becomes format version 4, and a version 3 save carries
forward with the defaults (D56, D60). A fallback font lets names use the
Turkish letters Fredoka lacks.

In scope:

- `PlayerProfile` and `HapticsSetting` in `Meta`, their defaults in
  `MetaRules`, and the name rule.
- Format version 4 in `Serialization`.
- Haptics switched by its own setting instead of the sound's.
- The Profile overlay, the Haptics row in Settings, and the avatar in the
  top bar opening Profile.
- An avatar catalog asset: the thirty Animal Pack Redux sprites, each
  with its id.
- A dynamic fallback font with the Turkish letters, added to Fredoka's
  font asset.

Not in scope: the leaderboard that shows the name and avatar (6.5),
statistics the save does not hold (D71: first-try wins), photos or custom
avatars, the level's own interface (6.6).

---

## Public surface

```
PlayerProfile                       sealed class, Meta; a section of SaveData
    string Name { get; }
    string AvatarId { get; }
    static bool TryNormalizeName(string raw, out string name)  // the name rule
    bool TrySetName(string raw)     // false and unchanged when the rule refuses
    void SetAvatar(string avatarId) // non-empty; which ids exist is the UI's
    static PlayerProfile Starting(MetaRules rules)

HapticsSetting                      sealed class, Meta; a section of SaveData
    bool IsOn { get; set; }         // true for a new player

MetaRules                           existing, gains
    string DefaultPlayerName        // "Player"
    string DefaultAvatarId          // "bear", the first of the catalog

SaveData                            existing, gains Profile and Haptics

JsonSaveCodec                       existing
    CurrentFormatVersion = 4        // the profile and haptics; version 3
                                    // and older read them as defaults

SoundPlayer                         existing
    bool IsHapticsOn { get; set; }  // pulses follow this, not IsOn

AvatarCatalog                       ScriptableObject, Runtime
    // ordered entries of (id, sprite); IconOf(id) falls back to the
    // first entry for an unknown id; Problems()

FrontEndState                       existing, gains
    string PlayerName, string AvatarId, bool IsHapticsOn
    int LevelsCompleted, int LifetimeGold   // Profile's statistics

IFrontEnd                           existing, gains
    event Action<string> NameChangeRequested
    event Action<string> AvatarChangeRequested
    event Action HapticsToggleRequested
    void ShowNameRefused()          // the line under the name field

ProfileOverlay                      MonoBehaviour, UI
AvatarButton                        MonoBehaviour, UI: one cell of the grid
ProfileTexts                        static, plain C#, UI
```

Names are negotiable. That the name rule lives in `Meta` and is tested,
that the save stores the avatar by its id and never by its position, and
that an older save is read with the defaults rather than refused, is not.

---

## Design decisions (owner)

### The name

- Trimmed, then 1 to 16 characters. Allowed: letters of any alphabet
  (`char.IsLetter`), digits, spaces, and `-` `_` `.`. Runs of spaces
  inside are collapsed to one. Anything else is refused as a whole; the
  name is never silently changed beyond trimming and collapsing.
- Refused input keeps the old name, puts it back in the field, and shows
  one line under it: "Use 1 to 16 letters, digits, spaces, - _ or .".
- A new player and an older save are named "Player" (D71).
- The name is saved when the field's editing ends (Enter, or the field
  losing focus) and it changed.

### The avatar

- Thirty avatars, `Kenney/AnimalPackRedux/Round`, in alphabetical order;
  each id is the file name without its extension ("bear", "buffalo", …).
- The save keeps the id. An id the catalog no longer has shows the first
  avatar and is kept in the save until the player picks another.
- Picking an avatar saves at once. The chosen one is marked in the grid.
- The top bar's avatar shows the chosen one and opens Profile when
  pressed.

### The Profile overlay

- Opened by the top bar's avatar; Back and the red close button close it.
- From the top: the title "Profile"; the chosen avatar, large; the name
  field with a pencil icon or "Tap to edit" hint, and the refusal line
  under it, empty unless refused; the grid of thirty avatars, scrolling,
  five per row; four statistics in two rows: Levels Completed, Campaign
  Level, Best Streak, Lifetime Gold. Each reads from the save as it is.
- Campaign Level shows "Level N" as Home's button does, or "All Played".

### Settings

- A Haptics row under the Sound row, with the same round toggle: the
  phone icon when on, the phone icon with a cross over it when off.
- Haptics and sound are independent: sound off leaves vibration on, and
  the other way round.
- The browser only vibrates where it can (WebHaptics); on iPhone the
  toggle exists and nothing vibrates, as today.

### The save, format version 4

- `SaveDataDto` gains `playerName`, `avatarId` and `hapticsOn`.
- A version 3 or older save reads them as a new player has them, from
  the rules (D60): "Player", the first avatar, haptics on. Before this
  version vibration followed the sound setting; it is not carried over.
- A version 4 save with a name the rule refuses or an empty avatar id is
  read with the default for that field and logged, not refused: losing a
  whole save over a name would be worse than resetting the name.

### The fallback font

- Fredoka lacks Ğ, ğ, İ, Ş and ş. A rounded open font with them, Nunito
  ExtraBold (SIL Open Font License), is imported as a dynamic TextMeshPro
  font asset and added to `Fredoka-Bold SDF`'s fallback list, so every
  text picks the missing letters from it.
- It lives under `Assets/ThirdParty/Fonts/Nunito` with its licence; the
  README credits it (D69).

### The builder

- It writes the Profile overlay, the avatar grid cell, and the Haptics
  row in the Settings overlay, and adds Profile under Overlays in Front
  End.
- Top Bar is hand-made and never written. Making its avatar pressable
  (a `Button` on the avatar image and two new `TopBar` fields) is an
  owner step in the Editor, given in the report.

### Configuration

- `MetaConfig`: the default name and avatar id, checked: the name passes
  the name rule, the id is non-empty.
- `AvatarCatalog`: every entry has a sprite and a unique, non-empty id;
  the default avatar id of `MetaConfig` must be one of them (checked by
  the bootstrap at start, as the gold packs are).
- `UiConfig`: the new texts (Profile, the refusal line, the statistic
  labels, the edit hint, Haptics), checked in `Problems()`.

---

## Left to you

- Whether `PlayerProfile` holds the name rule's limits as constants or
  takes them from the rules; the limits themselves are fixed above.
- How the name field keeps focus and selection on mobile web, within the
  spec; TMP_InputField's own mobile keyboard is acceptable.
- The prefab hierarchies and sizes, given in the plan as for Modules 35
  to 37, and how the avatar grid's cells are created.
- How the Haptics toggle's off look is drawn from the phone and cross
  icons.

---

## Tests

**`PlayerProfile`**
- The name rule: trimming, collapsing spaces, 1 and 16 characters kept,
  0 and 17 refused, Turkish letters and other alphabets kept, digits and
  `- _ .` kept, any other symbol refused; `TrySetName` leaves the name
  unchanged when refused. `SetAvatar` refuses an empty id. `Starting`
  takes the rules' defaults.

**`HapticsSetting`, `SaveData`, `MetaRules`, `MetaConfig`**
- A new player has haptics on, the default name and avatar. Bad defaults
  in the config are reported.

**`JsonSaveCodec`**
- Version 4 round-trips the name, the avatar id and haptics. Versions 1,
  2 and 3 read them as defaults while keeping every other section. A
  version 4 save with a refused name or an empty avatar id reads the
  default for that field. A version 5 save is refused.

**`AvatarCatalog`**
- `IconOf` for a known id, for an unknown id (the first entry), problems
  for a missing sprite, an empty id and a repeated id.

**`FrontEndState`**
- The name, avatar id, haptics, levels completed and lifetime gold come
  from the save.

**`ProfileTexts`, `UiConfig`, `UiNavigation`, builder**
- The statistic texts and the refusal line from the config; each new text
  that is empty is reported; Profile stacks and closes with Back; the new
  prefabs are written wired and Top Bar's file is unchanged.

**By hand (owner)**
- A new save shows "Player" and the bear in the top bar and on Profile.
- A name with Ğ, İ and Ş is kept, saved across a restart, and drawn with
  every letter; a name of 17 characters or with an emoji is refused with
  the line, and the old name comes back.
- Picking an avatar changes the top bar at once and survives a restart.
- The statistics match: levels completed, the campaign level, the best
  streak, the lifetime gold.
- Sound off with haptics on still vibrates on an Android phone; haptics
  off with sound on plays sound and does not vibrate.
- A version 3 save from before this module loads with every gold, life,
  joker and streak kept, named "Player".
- On a phone browser, the name field opens the keyboard and Enter saves.

---

## Resolved during implementation

- **Resets, not refusals.** `ISaveCodec.TryDecode` gains an `out` list of
  the fields it reset. A version 4 save with a refused name or an empty
  avatar id reads the default for that field; `SaveRepository` turns each
  reset into a load problem, which the bootstrap logs as a warning, and
  the save still loads. Nothing is written at load (D56).
- **The name rule** keeps its limits as constants in `PlayerProfile`.
  "Digits" are any Unicode decimal digit. A decomposed I with a combining
  dot and letters outside the Basic Multilingual Plane are refused, since
  the name is never normalized beyond trimming and collapsing spaces;
  Turkish keyboards send the precomposed letters. The refusal line's "1 to
  16" is configuration text, kept in step by hand.
- **The avatar.** `AvatarCatalog.ShownId` gives the top bar, the large
  avatar and the grid mark one answer, so an id the catalog no longer has
  shows and marks the first avatar everywhere. A whitespace-only id counts
  as empty. *Gate Rush → Fill Avatar Catalog* writes the catalog from the
  thirty Round avatars in name order.
- **The name field** is TMP_InputField's single line with the mobile
  keyboard. It is not overwritten while focused, though the front end
  refreshes every second; editing ends on Enter or losing focus, and a
  request is raised only when the text changed. Escape cancels the edit
  and may also close Profile in the same frame.
- **Haptics.** `SoundPlayer.Pulse` follows `IsHapticsOn`; `IsOn` gates
  only sounds. The off look is the phone icon with a red cross over it.
- **Top Bar** is hand-made: its avatar got a `Button` and the two new
  `TopBar` fields in the Editor.
- **Nunito ExtraBold** is a dynamic font asset in the fallback list of
  `Fredoka-Bold SDF`.
- **Not checked by hand yet:** vibration independent of the sound on an
  Android phone, and the name field with a phone's keyboard; both wait
  for the next web build.
- **Tests.** 110 new; 2,380 in all.
