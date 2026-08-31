# Module 08 — Level serialization

**Assembly:** `GateRush.Serialization`
**Depends on:** Module 01 (`GateRush.Core`), and `UnityEngine` for `JsonUtility`
**Phase:** 1.9

---

## Responsibility

Convert a `LevelContext` to JSON and back, through a layer of data-transfer
objects that exist only for that purpose.

This is the one layer allowed to reference `UnityEngine` while `Core` is not
(D17). `Core` types carry no serialization attributes and know nothing about
this module; the DTOs know both sides and translate.

Nothing here decides game rules. If a question feels like a rule, it belongs in
`Core`.

---

## Public surface

```
static class LevelSerializer
    static string ToJson(LevelContext ctx)
    static LevelContext FromJson(string json, string sourceName = null)
```

`sourceName` is the file name or other origin, used only in error messages. A
malformed level is diagnosed by a human reading an exception, so every message
must say which file and which element.

### DTO types

One per `Core` definition type, plus two shapes `JsonUtility` forces on us:

```
LevelDto          root object
CoordDto          { x, y }
BlockDto
GateDto
ShutterDto
GeneratorDto
ElevatorDto
WaveDto           wrapper: { SpawnedBlockDto[] blocks }
SpawnedBlockDto
```

All `[Serializable]`, all **public fields** — `JsonUtility` ignores properties.
This is the documented exception to `CONVENTIONS.md`'s "never public fields"
rule; DTOs are data carriers, not objects.

---

## Design decisions (owner)

**Enums serialise as strings, not integers.** `JsonUtility` writes them as
integers by default. Three reasons not to accept that:

A diff of `"color": 3` → `"color": 5` says nothing; `"Yellow"` → `"Orange"` says
what changed, and these files will be read in commit history.

Inserting a value into the middle of `BlockColor` silently reassigns every
existing level's colours, with no error anywhere. A string cannot be silently
wrong — an unrecognised name fails to parse.

The cost is one `Enum.Parse` per field in a conversion that runs once per level
load, never in a hot path.

**Coordinates are `CoordDto { x, y }`, not parallel arrays.** An earlier framing
offered parallel `cellsX`/`cellsY` arrays; a struct is better on both counts.
It reads clearly in JSON, and it makes a length mismatch structurally impossible
rather than something the DTO layer has to check for. A validation you do not
need to write is better than one you write correctly.

**Nullable ints use `-1` as a sentinel.** `JsonUtility` cannot serialise `int?`.
Every nullable field in `Core` — `OpenAtClearCount`, `UnfreezeAtClearCount`,
`LockId`, `KeyTargetLockId` — is non-negative when present, so `-1` cannot
collide with a real value. Any other negative is a structural error, not a
second way of writing "none".

`ShutterDefinition.RequiredColor` is `BlockColor?`, not an int; represent
"none" as an empty or absent string.

**Elevator waves need a wrapper.** `JsonUtility` cannot serialise a jagged
collection, so `ElevatorDto.waves` is `WaveDto[]`, each wrapping its own
`SpawnedBlockDto[]`. It reads well enough in JSON and costs one type.

**Every file carries `formatVersion`.** The first field of `LevelDto`. A file
whose version is not the one this build understands is rejected immediately,
before any conversion is attempted — a silently misread level is far worse than
a refused one. When the schema first changes, this is what makes migration
possible instead of a hunt for corrupted data.

**Structural validation here, semantic validation in `Core`.** Two different
questions, in two places, with no overlap:

> This layer asks: *can this JSON become a `LevelContext` at all?*
> `Core` asks: *is this `LevelContext` a valid level?*

So the DTO layer checks the things `Core` will never see — a wrong
`formatVersion`, an enum name that does not parse, a required array that is null
because the field was absent, a negative value where only `-1` means anything.

It does **not** re-check anything `Core` already validates: blocks inside the
grid, cells connected, lock ids unique, keys pointing at real locks. Duplicating
those means two places to fix when a rule changes, and one of them will be
missed.

---

## Left to you

- Whether `ToJson` pretty-prints. It costs nothing and these files are read by
  humans in diffs; the counter-argument is file size, which is negligible here.
- The exact conversion helpers for sentinels and enum names, and where they live
  so both directions share one implementation.
- Error message wording, subject to the rule above: name the source and the
  element.
- Whether `FromJson` reports the first structural error or collects several. One
  is simpler; several is kinder to an editor that wants to show a list. Say
  which you chose and why.

---

## Tests

**Round trip**
- Every level in a corpus survives `ToJson` → `FromJson` → `ToJson` with
  identical output. Build the corpus to include, between them, every field of
  every DTO — a nullable that is set and one that is not, a layered block, an
  axis-restricted block, a lock, a key of each effect, a time bonus, a
  colour-bound shutter and a global one, a generator queue, an elevator with two
  waves, static walls.
- **Separately:** a hand-written JSON with every field populated produces a
  `LevelContext` whose every field is correct. Round-trip alone cannot catch a
  field that both directions drop consistently.

**`JsonUtility`'s limitations, one test each**
- A `null` nullable round-trips as `-1` and comes back null.
- A set nullable round-trips as its value.
- An elevator with two waves of different lengths round-trips intact.
- An empty collection round-trips as empty, not null.

**Enums as strings**
- Colours appear in the JSON as names, not integers.
- An unrecognised colour name throws, naming the source and the field.
- Every value of every serialised enum round-trips. Table-driven, so a value
  added later without a mapping fails a test rather than a level load.

**Structural errors**
- A `formatVersion` this build does not understand is rejected before conversion.
- A required array that is absent from the JSON is reported as a named error,
  not a `NullReferenceException`.
- A negative value other than `-1` in a sentinel field is reported.
- Malformed JSON is reported as such rather than propagating a raw parse
  exception.

**No duplicated validation**
- A JSON that is structurally fine but semantically invalid — a block outside the
  grid, say — is rejected by `Core`'s constructor with `Core`'s message. Assert
  the message, so that a later "helpful" duplicate check in this layer changes
  which error surfaces and fails the test.
