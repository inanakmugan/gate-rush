# Module 21 — Play any level

**Assembly:** `GateRush.Runtime` and `GateRush.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 09, 12, 17, 19
**Phase:** 2.5

---

## Responsibility

Let the owner play any level straight away while building the game,
without changing the Level scene: from the Level Editor, from a menu
listing every level, and by stepping between levels during Play. All of it
is development tooling: none of it reaches a release build, and the scene
keeps starting on its own level (the first) so the build behaves as before.

In scope:

- A **Play** button in the Level Editor for the level being edited.
- A **Gate Rush → Play Level…** window listing every level in catalog
  order.
- A one-shot **start-level override** the bootstrap reads when Play starts
  in the editor.
- **Keys** during Play to go to the next or previous level, in the editor
  and in development builds.

Not in scope: a level select for players (Phase 6), saving progress
(Phase 4), cheats beyond moving between levels.

---

## Public surface

```
Runtime
    DevLevelOverride                static, editor-only body
        bool TryTake(out string levelName)
            // the level asked for by the editor tools, once: reading it
            // clears it. Always false outside the editor.

    LevelBootstrap                  on Awake, a taken override replaces the
                                    serialized Level for this Play session
    LevelBootstrap / InputController
        dev keys: next and previous level (editor and development builds)

Editor
    DevLevelOverride writer         sets the override, opens Level.unity
                                    (asking to save open scene changes as
                                    Unity does), enters Play mode
    PlayLevelWindow                 Gate Rush → Play Level…
    LevelEditorWindow               a Play button for the open level
```

Names are negotiable. That the override is **one-shot, editor-only and
never written into the scene** is not: the scene file must not change when
the owner tests a level, and a normal Play must start where a build starts.

---

## Design decisions (owner)

### The override

- Stored in `UnityEditor.SessionState` under one key: it lives for the
  editor session, survives the domain reload that entering Play mode may
  cause, and is gone when Unity closes.
- The bootstrap takes it in `Awake`: if it names a level in
  `Resources/Levels`, that level is loaded instead of the serialized one,
  and the override is cleared at once, so the next plain Play starts on the
  scene's level again. An override naming no level is cleared with a
  warning, and the scene's level loads.
- The runtime side compiles to "no override" outside the editor
  (`#if UNITY_EDITOR`), so a build carries no trace of it.
- **Introductions (Module 19)** still follow catalog order: a level started
  this way shows the cards it introduces, as if reached in order.

### Play from the Level Editor

- A **Play** button in the Level Editor's toolbar. It plays the level file
  currently open in the editor.
- With unsaved changes it asks first: **Save and Play**, **Play the saved
  file**, or **Cancel**. A level never saved to a file cannot be played;
  the button says so.
- A draft that fails validation as a level data error (the editor already
  reports these) is not played; the button explains why.

### Gate Rush → Play Level…

- A small editor window listing every level in `Resources/Levels` in the
  catalog's order, each as its level number and file name ("7 — level-6").
  Clicking one plays it. A level that fails to load is listed greyed out
  with its error as a tooltip.
- The list follows `LevelCatalog`'s rules, so the numbers match the HUD.

### Keys during Play

- **]** goes to the next level and **[** to the previous one, in catalog
  order, at any moment, also during a card or after a result. Both go
  through the bootstrap's own `Load`, exactly as Next does, so cards,
  countdown and HUD behave as on a real level change.
- At the last or first level the key does nothing (a log line says so).
- Active in the editor and in development builds only
  (`UNITY_EDITOR || DEVELOPMENT_BUILD`); a release build ignores them.
- The keys and their behaviour are listed in the bootstrap's tooltip so
  they can be found again.

### Engine rules that carry over

- No mutable statics: `SessionState` is the editor's own store; nothing in
  Runtime keeps a static field for this.
- Entering Play mode from code uses `EditorApplication.EnterPlaymode`, after
  opening the scene through `EditorSceneManager` with Unity's own
  save-changes prompt.
- No magic numbers or strings scattered: the scene path, the session key
  and the key bindings are named constants or settings in one place.

---

## Left to you

- Where the scene path and session key live (`LevelEditorSettings` or
  named constants) and how the editor finds the Level scene.
- How the dev keys are read (the existing `InputController` or a small
  separate component) as long as they go through the bootstrap's `Load`.
- The window's layout.
- The exact Editor steps for the owner.

---

## Tests

Edit Mode, against plain code where there is any.

- The catalog order and numbers the window lists match `LevelCatalog`
  (`TryGetNumber`, `Names`).
- Next and previous in catalog order: from the first level previous gives
  none; from the last, next gives none; ids need not be contiguous.
- An override naming a level that is not in the catalog is reported and
  ignored (where this logic is plain code).

**By hand (owner)**
- Level Editor → Play opens the edited level; with unsaved changes the
  three choices behave as described.
- Gate Rush → Play Level… lists every level with the HUD's numbers and
  plays the clicked one.
- After a played override, a plain Play starts on the scene's level.
- `]` and `[` step through levels with cards and countdown as on Next.
- After all of this, `git status` shows no change to `Level.unity`.
