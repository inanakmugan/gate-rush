# Gate Rush — Project Instructions

A Unity implementation of a grid-based sliding-block puzzle game, built as a
portfolio project. **Architecture decisions belong to the project owner and are
recorded in `docs/`. Your role is to implement against those specs, not to
redesign them.**

Unity 6000.3.22f1 (6.3 LTS) · Universal Render Pipeline, 2D Renderer · targets
WebGL and Android.

## Reference documents

@./docs/ARCHITECTURE.md
@./docs/MECHANICS.md
@./docs/CONVENTIONS.md

`docs/DECISIONS.md` and `docs/ROADMAP.md` are not auto-loaded. Read them on
request or when a task touches a decision you want to understand.
Module specifications live in `docs/MODULES/`.

## Working agreement

1. **One module per session.** Work from a single spec file in `docs/MODULES/`.
   Do not start adjacent modules "while you're in there."
2. **Plan before writing.** Restate the module's responsibility and public
   surface in your own words, list the files you intend to create, and wait for
   approval before writing code.
3. **Specs are authoritative.** If a spec is ambiguous, incomplete, or looks
   wrong, stop and ask. Do not fill the gap with an assumption and continue.
4. **Tests ship with the module.** Every `Core`, `Solver`, `Meta`, and
   `Serialization` module lands with Edit Mode tests in the same session.
5. **Never run git commands.** The owner commits manually through GitHub
   Desktop. Do not stage, commit, branch, or push. You may suggest a commit
   message; you may not execute one.
6. **Never edit files in `docs/`** unless explicitly asked. Those files are the
   owner's design record.
7. **Never launch Unity from the shell.** The editor is typically already open
   and a second instance will conflict on the project lock. Ask the owner to
   compile and run tests in the Unity Editor and report results back.

## Hard rules

- `GateRush.Core`, `GateRush.Solver`, `GateRush.Meta`, and `GateRush.Platform`
  must not reference `UnityEngine` in any form — including `Vector2Int`,
  `Mathf`, `Random`, `Debug`, and `[SerializeField]`. Their assembly
  definitions have `noEngineReferences` enabled, so violations are compile
  errors. Use the project's own `Coord` struct and `System.*` types.
- `GateRush.Solver` is **editor and test only**. It must never be reachable from
  a player build. Its assembly definition enforces this; do not weaken it.
- **No magic numbers.** Every tunable value (timings, prices, thresholds,
  rewards, animation durations) is read from a configuration asset or level
  data, never hardcoded at a call site.
- **No undo system.** The in-level button is *restart*.
- **No procedural level generation.** Levels are authored by hand in the Level
  Editor and validated by the solver.
- **No `UnityEngine.Random` in deterministic code paths.** Use `System.Random`
  with an explicit seed.
- WebGL is a shipping target: no threads, no file system writes, no blocking
  network calls in runtime code paths.
- Sprite materials default to `Sprite-Unlit-Default`. Under the 2D Renderer, lit
  sprites render black when no `Light 2D` is present.
- **Never write `.meta` files.** Unity generates them on import and assigns each
  a GUID; a hand-written GUID can be malformed or collide with another asset.
  Create the asset file and let the open Editor generate its `.meta`.

## Project layout

```
Assets/Scripts/
  Core/            pure C# — board rules and resolution
  Solver/          pure C#, editor-only — search strategies
  Meta/            pure C# — economy, lives, streak, progression
  Platform/        pure C# — platform service interfaces
  Serialization/   references UnityEngine (JsonUtility) — DTOs only
  Runtime/         MonoBehaviours, presentation, DOTween
  UI/              screens and navigation
Assets/Editor/     Level Editor window, editor tooling
Assets/Tests/      Edit Mode tests
Assets/Resources/Levels/   level JSON files
docs/              design record (do not edit)
```

## Commands

- Run tests: Unity → Window → General → Test Runner → Edit Mode → Run All
- The owner uses JetBrains Rider. Rider-generated `.sln`/`.csproj` files are
  disposable; never hand-edit them.
