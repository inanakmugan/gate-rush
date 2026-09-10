# Coding Conventions

## Language and comments

- All identifiers, comments, commit messages, and documentation in **English**.
- XML documentation comments on every public type and member. Explain *why* and
  *what contract*, not what the next line does.
- No commented-out code. Delete it; version control remembers.

## Naming

| Kind | Convention |
|---|---|
| Types, methods, properties, events | `PascalCase` |
| Parameters, locals | `camelCase` |
| Private fields | `camelCase`, no prefix |
| Constants | `PascalCase` |
| Interfaces | `IPascalCase` |
| Enum members | `PascalCase`, singular type name |

Boolean members read as assertions: `isFrozen`, `hasKey`, `canExit`.

## Unity-specific

- Serialized fields are `[SerializeField] private`. **Never** `public` fields —
  except in DTOs, where `JsonUtility` requires them.
- Expose read access through properties when other components need it.
- Every `OnEnable`/subscription has a matching `OnDisable`/unsubscription.
- No `GameObject.Find`, no `SendMessage`, no singletons reachable from `Core`.
- Persistence goes through `ISaveStore`, never `PlayerPrefs` directly.

## Core, Solver, Meta, Platform

These assemblies have `noEngineReferences` enabled. Consequences:

- Use the project's `Coord` struct, not `Vector2Int`.
- Use `System.Math`, not `Mathf`.
- Use `System.Random`, not `UnityEngine.Random`.
- No logging. Return results; let callers decide how to report them.

Note that `[Serializable]` is `System.Serializable` and is available here;
`[SerializeField]` is not.

## Immutability

- `BoardState`, `Coord`, `Move`, and all `LevelContext` types are immutable.
- `readonly struct` for small value types; `sealed class` with `readonly` fields
  for larger ones.
- Mutating methods do not exist. Applying an action returns a new state.

## Collections

- Public surfaces expose `IReadOnlyList<T>`, never `List<T>` or arrays.
- Avoid LINQ in solver hot paths — allocation matters when visiting millions of
  states. LINQ is fine in editor and UI code.

## Error handling

- Invalid **level data** throws with a message naming the offending element.
  Authoring errors should be loud.
- Invalid **player input** returns `false` or a failure result. Tapping an
  immovable block is not exceptional.
- Fixpoint iteration overflow throws. It indicates a cycle in level data.

## Configuration

No magic numbers. Every tunable lives in a configuration asset or in level data:

- Economy: prices, rewards, life cap, regeneration interval
- Streak: thresholds and reward tables
- Jokers: clock bonus seconds
- Presentation: animation durations, easing curves
- Per level: time budget, gold reward

Values may have defaults in the config asset. They may not appear at call sites.

## Rendering

- Sprite materials default to `Sprite-Unlit-Default`. Switch to `Lit` only
  alongside an actual `Light 2D`.
- Glow is achieved through bloom (HDR colour values plus a post-processing
  volume) before reaching for 2D lights.

## Tests

- Edit Mode, NUnit, one test class per production class.
- Naming: `MethodName_Condition_ExpectedResult`.
- Arrange–Act–Assert with blank-line separation.
- Board fixtures are built by an explicit test builder, not by loading files.
- Every bug fixed gains a regression test in the same session.

## Commits

The owner commits manually through GitHub Desktop. Suggested format:

```
<type>(<scope>): <summary>

Design: <the decision this encodes, when non-obvious>
```

Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`.
Scopes: `core`, `solver`, `meta`, `editor`, `runtime`, `ui`, `platform`.

Example:

```
feat(core): add BoardState with FNV-1a hashing

Design: immutable state so the solver's visited set can deduplicate in O(1).
GetHashCode covers every dynamic field; omitting one would be silently
incorrect rather than merely slow.
```
