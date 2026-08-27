# Decision Record

Each entry states the decision, why it was made, and what was rejected. This
file exists so a reader can distinguish decisions from accidents.

---

## D1 — Separate `LevelContext` from `BoardState`

**Decision.** Immutable level data lives in `LevelContext` and is excluded from
hashing. Only mutable state lives in `BoardState`, which is hashed.

**Why.** The solver hashes millions of states. Hashing grid dimensions, block
shapes, and gate widths — none of which ever change — wastes work on every
state. Passing the context by reference costs nothing.

**Rejected.** A single self-contained state object. Simpler to write, but every
hash would carry constant data, and the temptation to mutate shared arrays would
be constant.

**Guard rail.** When unsure whether a field is dynamic, put it in `BoardState`.
An extra hashed field is slow; a missing one is wrong.

---

## D2 — Immutable state with FNV-1a hashing

**Decision.** `BoardState` is immutable; applying an action returns a new
instance. `GetHashCode` uses FNV-1a over every dynamic field, paired with a
full-field `Equals`.

**Why.** Immutability makes the visited set trustworthy and backtracking free.
FNV-1a is fast, disperses well over short byte sequences, and is trivial to
reimplement identically in tests.

**Rejected.** Mutate-and-undo with an explicit action stack. Faster in
principle, but a single missed undo produces corruption that is nearly
impossible to diagnose.

---

## D3 — BFS first, A\* second, behind an interface

**Decision.** `ISearchStrategy` with a breadth-first implementation as the
reference, and A\* added later as an optimisation. Tests assert both return the
same move count on the same corpus.

**Why.** Difficulty is measured in moves, so the solver must return the
*shortest* solution. BFS guarantees that and is simple enough to be obviously
correct. A\* with an admissible heuristic — the count of colours remaining,
since each needs at least one action — returns the same optimum while expanding
far fewer nodes.

**Rejected.** DFS. It finds *a* solution, not the shortest, which would corrupt
the difficulty measure and therefore the time budget.

---

## D4 — Three-valued solve result

**Decision.** `Solvable | Unsolvable | Indeterminate`.

**Why.** Exhausting a search budget is not proof of unsolvability. Reporting it
as such would tell a designer their correct level is broken. The editor renders
the three outcomes in three colours.

**Rejected.** A boolean plus a separate timeout flag. Callers forget to check
the flag; the type system should make that impossible.

---

## D5 — Canonical move set with exhaustive fallback

**Decision.** The move generator has two modes. **Canonical** emits only
gate-aligned positions, zero-distance clears, positions that open or close a
generator or elevator region, and positions where the block rests against an
obstacle. **Exhaustive** emits every reachable position. The editor tries
canonical first and falls back to exhaustive.

**Why.** Under reachability-based movement (D27) a block can reach most of the
free area of the board in a single move, so the branching factor is far above
Rush Hour's 30–40 and grows as the board empties. Plain BFS does not survive
that. The overwhelming majority of reachable positions leave a block resting in
open space, changing nothing.

Under this movement model canonical pruning is not an optional optimisation but
a prerequisite for the solver to terminate on realistic boards.

**Safety argument.** Canonical pruning can only produce **false negatives**. Its
move set is a subset of the player's, so any solution it finds is genuinely
playable, and it can never call an unsolvable board solvable. When the solver was
planned as a generation filter, false negatives were free. As an authoring tool
they are expensive, hence the exhaustive fallback.

---

## D6 — Exploit progress monotonicity

**Decision.** The search treats `(TotalClearCount, generator indices, elevator
indices)` as a monotonically increasing progress vector and stratifies the
search space by it, discarding each stratum's visited set on advance.

**Why.** These values never decrease, so transitions between strata are one-way
and previous strata are unreachable. Peak memory falls from the whole state
space to the largest single stratum.

**Note.** This is the structural difference from Rush Hour, whose state graph is
fully cyclic because nothing is ever removed.

---

## D7 — One event type for all removals

**Decision.** Every removal — move, key effect, rocket, broom — emits
`ColorCleared(blockId, colour)`. Counters listen to that alone.

**Why.** Six of the ten mechanics are "unlock after N removals." One event plus
a condition system replaces six systems. A new mechanic becomes a new condition
type, not a change to existing code.

**On terminology.** An earlier draft named this event `LayerPeeled` and used
"peel" throughout. That framing put layered blocks — an uncommon mechanic — at
the centre of the vocabulary, implying they were structural when they are not.
The data model is unchanged: a colour stack of depth 1 is the ordinary case, and
using one representation for both avoids branching at every consumer. Only the
naming was corrected.

---

## D8 — Fixpoint resolution loop

**Decision.** `MoveResolver` re-evaluates conditions and spawn triggers until
nothing changes, guarded by an iteration limit.

**Why.** Mechanics compose in ways the author cannot enumerate: a shutter
revealing an elevator whose wave carries a key. Any fixed number of passes would
eventually be wrong.

**Rejected.** Explicit per-mechanic ordering. It would need revisiting on every
new mechanic and would silently mis-handle novel combinations.

---

## D9 — Jokers share the move pipeline

**Decision.** Rocket and broom are inputs to the same resolver as a move, not a
parallel system.

**Why.** Their consequences are identical to a move's: counters advance, gates
open, chains fire. A separate path would drift out of sync — the classic symptom
being "the rocket opened the gate but the shutter didn't."

**Consequence.** Rocket is `ClearOuterColor` on one target — exactly the existing
key effect. Broom is the same effect applied to every block currently showing the
chosen colour. Jokers introduce **no new core concept**.

---

## D10 — The solver ignores jokers

**Decision.** Every level must be solvable with zero jokers.

**Why.** Jokers exist to reduce difficulty and are bought with a currency the
player controls. Making them part of the solvability proof would make that proof
depend on the player's wallet.

---

## D11 — Targeting is not movement

**Decision.** Two independent predicates: `CanMove` and `CanBeTargeted`. Frozen
and locked blocks are immovable but targetable. Blocks under closed shutters are
neither.

**Why.** The rules genuinely differ. Deriving one from the other would force
special cases at every call site.

---

## D12 — Time is outside the search space

**Decision.** The countdown is not in `BoardState`. Time-bonus blocks contribute
to a design-time budget calculation only.

**Why.** Moves are unlimited; time is the sole pressure. Encoding remaining time
would multiply the state space by the number of distinct time values for no gain.

**Consequence.** The solver's move count feeds a *suggested time budget* in the
editor, turning a feel-based number into a measured one.

---

## D13 — Truth and visibility are separate

**Decision.** `BoardState` always holds complete truth. A `VisibilityLayer` in
the presentation layer decides what the player sees.

**Why.** Frozen and shuttered content is hidden from the player, not from the
game. If hiding were modelled in state, the solver could not reason about it.

---

## D14 — No undo

**Decision.** No undo system. The in-level button is restart.

**Why.** Direct observation of the reference game: there is no undo control, and
the only pressure is the clock. Undo would defeat that pressure.

**Note.** A secondary source described unlimited undo. Observation of the running
game overrode it.

---

## D15 — No procedural generation

**Decision.** Levels are hand-authored in a purpose-built editor and validated by
the solver. No runtime or offline generator.

**Why.** Reverse generation — placing blocks inward through gates and recording
the inverse move sequence — would have produced provably solvable boards, and
would have made most gating mechanics free: a block's freeze threshold, a gate's
open threshold, a lock/key pairing, and an axis restriction can all be *read off*
the forward solution order at no search cost.

However, generators (M6) and elevators (M9) require choreographing spawn triggers
coherently in reverse, which is substantially harder. The choice was between
procedural generation over a subset of mechanics, or hand-authoring with full
mechanic coverage. Hand-authoring with live solver validation was chosen: it
supports all ten mechanics and yields a tool that stays useful.

**Rejected.** Forward generation — randomise then test — which collapses to a
sub-one-percent acceptance rate once gating mechanics are present.

---

## D16 — Levels start tightly packed with a ready opening move

**Decision.** Levels begin with few or no empty cells, and at least one block
starts flush against a matching open gate.

**Why.** A dense board is not deadlocked as long as one exit is available, since
clearing consumes no space. Density collapses the branching factor, which makes
solver verification tractable and makes the intended order nearly forced — a
prerequisite for a fair fixed time budget. The opening clear also guarantees
immediate feedback on the first tap.

**Observation.** This pattern — full board, one block pre-aligned with its gate —
is the natural output of reverse generation, and is visible throughout the
reference game.

---

## D17 — JSON via `JsonUtility`, with a DTO layer

**Decision.** Level files are JSON, converted through dedicated DTO types.
`GateRush.Serialization` may reference `UnityEngine`; `GateRush.Core` may not.

**Why.** JSON keeps `Core` free of engine types, diffs readably in version
control, and can be produced by tools outside Unity. `JsonUtility` avoids a
third-party dependency.

**Consequences to respect.** `JsonUtility` cannot serialise nullable value types,
dictionaries, jagged collections, or root-level arrays, writes enums as integers,
and only handles public fields. DTOs therefore use `-1` sentinels for "none",
wrap nested collections in intermediate types, wrap everything in a root object,
and accept integer enums. The Level Editor is the authoring surface, so raw JSON
readability is a convenience rather than a requirement.

**Also.** Every level file and every save file carries a `formatVersion` field,
so a later schema change can be migrated rather than silently misread.

**Rejected.** `ScriptableObject`. It would bind level data to `UnityEngine`, take
the `Core` and `Solver` layers with it, and make bulk operations over hundreds of
levels awkward.

---

## D18 — The solver is excluded from player builds

**Decision.** `GateRush.Solver` is an editor-only assembly.

**Why.** Search is unbounded work and WebGL has no threads — running it at
runtime would freeze the tab. Levels are pre-validated, so the shipped game never
needs to search. Enforcing this in the assembly definition makes accidental
inclusion a compile error rather than a performance bug.

**If hints are added later.** Precompute the solution at authoring time and store
it in the level file. The game reads an answer; it does not compute one.

---

## D19 — Platform boundaries behind interfaces

**Decision.** `ILevelSource`, `ISaveStore`, `ITimeProvider`, `IHapticService`,
with per-platform implementations bound at startup.

**Why.** WebGL has no file system and no haptics. Isolating those differences
keeps them out of the rule layers. `ITimeProvider` additionally makes
time-dependent systems testable — life regeneration is verified by advancing a
fake clock instead of waiting forty minutes.

---

## D20 — Real-world time for regeneration and invincibility

**Decision.** Life regeneration and invincibility are driven by UTC timestamps
and elapsed-time arithmetic, not by a running timer, and continue while the game
is closed.

**Why.** A running timer stops when the process does. Storing the moment a life
was lost and computing elapsed time on resume is both simpler and correct.

**Guard.** Clamp negative elapsed time so moving the device clock backwards
cannot grant lives.

---

## D21 — Meta is independent of Core

**Decision.** Wallet, lives, streak, inventory, and progression have no reference
to the puzzle layer. They meet only in `Runtime`, which reports a level result to
`Meta` when a level ends.

**Why.** Economy rules become testable in isolation — "win three, lose one" is a
unit test, not a play session. It also keeps the puzzle layer free of concerns it
should never carry.

---

## D22 — Lifetime gold earned is tracked separately

**Decision.** A cumulative "gold earned" counter exists alongside the spendable
balance.

**Why.** The leaderboard ranks on lifetime earnings, which must not fall when the
player spends.

---

## D23 — Documentation is part of the deliverable

**Decision.** `docs/` is committed to the repository, and `CLAUDE.md` references
those same files rather than duplicating them.

**Why.** These specifications are how the architecture was communicated to an AI
coding assistant; publishing them shows the design work behind the code. A single
source also prevents the published document and the working document from
drifting apart.

---

## D24 — Universal Render Pipeline, Linear colour space

**Decision.** URP with the 2D Renderer, Linear colour space, on Unity
6000.3.22f1 (6.3 LTS).

**Why.** The game needs no lighting, shadows, or custom shaders, and the Built-In
pipeline would produce a smaller WebGL build. URP was chosen for two reasons that
outweigh that: it is the pipeline in use at the studios this project targets, and
it keeps visual effects available without a mid-project pipeline migration, which
grows more expensive the longer it is deferred.

**Note.** Render pipeline does not determine platform support — Built-In targets
Android equally well. The decision is about tooling alignment and future
optionality, not build targets.

**Settings.** HDR and post-processing stay **enabled**, since glow is a plausible
addition and HDR cannot be switched on later without re-tuning every colour.
Disabled: MSAA (it antialiases geometry edges, not sprite alpha), depth texture,
opaque texture, and both main and additional 3D lights. A single quality level is
kept so only one shader variant set is built.

**Convention this implies.** Sprite materials use `Sprite-Unlit-Default`. Under
the 2D Renderer, lit sprites render black when no `Light 2D` is present; lit
materials are introduced only alongside actual lights.

---

## D25 — Exit is a property of the move, not the position

**Decision.** Clearing is triggered by a move, not by where a block sits:

> A move that leaves the block flush and aligned with a compatible open gate
> clears it.

There is no parking on a usable gate. Move a block into that position — right
colour, gate wide enough for the block's projection, projection fully within the
opening — and it clears immediately.

A block *can* sit flush against a compatible open gate without clearing, but
only when it did **not** arrive there by a move. Four cases, all observed in the
reference game:

1. Level start — the block is authored flush against its gate.
2. A frozen block (M3) unfreezes while already flush.
3. A count-gated gate (M2) opens while a block waits in front of it.
4. A shutter (M5) opens, exposing a block already flush against a gate.

In every one of these the player must still push the block in — a zero-distance
move (`Move.TargetOrigin == current origin`). That is what makes zero-distance
moves meaningful rather than redundant, and it is the same interaction as a
level's opening move, which is almost always case 1.

**Why.** Observation of the reference game. A pre-aligned block is not consumed
until it is dragged into its gate, yet a block that is *moved* to rest against a
compatible gate does clear — there is no "leave it parked there safely" state.

**Consequence for the fixpoint loop.** Resolution must never clear a block on
its own. When condition re-evaluation opens a gate, unfreezes a block, or opens
a shutter, it does exactly that and stops; a block left flush against a
now-usable gate waits for the player's next move. Clearing inside the loop is a
plausible-looking mistake and is wrong.

**Consequence for `MoveGenerator`.** Whether a move clears must be readable from
the target position alone. It may not depend on where the block came from.

**Consequences elsewhere.** `Move.TargetOrigin` may equal the current origin;
`MoveGenerator` must emit those moves; `InputController` must accept a drag
whose direction is determined but whose distance is zero.

**Rejected.** A "newly flush" test — clear only if the block was not already
flush against that edge before the move. It makes a move's outcome depend on the
block's previous position, which `MoveGenerator` cannot afford, and it misfires
on a real case: a block already flush against an edge, slid along that edge into
a different gate, is never "newly flush" yet must clear.

**Rejected.** Automatic clearing on adjacency, with no move required. It would
remove zero-distance moves entirely, contradicts observed behaviour, and would
lose blocks by accident while manoeuvring.

---

## D26 — Adjacent layers must differ

**Decision.** A layered block may never have the same colour in two consecutive
positions of its stack. Violations are level data errors, not runtime cases.

**Why.** A rule of the reference game, and it removes work: a broom naturally
touches each block at most once, so no single-pass rule is needed; and a block
parked at a gate can never be cleared twice in place, so the resolver needs no
special handling for that case.

**Consequence.** Every colour in a block's stack needs a reachable compatible
gate somewhere in the level, since the block must travel to a different gate for
each layer. The editor warns when this fails.

---

## D27 — Movement is reachability, not straight-line sliding

**Decision.** A block may move to any position connected to its current one by a
path of single-cell orthogonal steps, where every intermediate position is fully
legal. It is not restricted to a straight line and may turn corners.

**Why.** Observation of the reference game. An earlier model assumed a block slid
along one axis and stopped, and that model was wrong in both directions: it
forbade legal moves that turn a corner, and — because the motion reads as
diagonal on screen — it invited the opposite error of allowing true diagonal
steps.

The decisive observation is a block whose only free neighbour is diagonal: it
cannot move there. So there is no diagonal step. What looks diagonal is a run of
orthogonal steps executed too fast to see individually.

**Not teleportation.** The player drags the block; it advances cell by cell and
halts against obstacles. `Core` records the drag's endpoints, not a jump. The two
are consistent because reachability is exactly the set of places a finger could
carry the block — anywhere unreachable, the block would simply stop on the way.
Blocks occupy whole cells only.

**Consequences.**
- Path validation in `MoveResolver` becomes a flood fill over legal positions,
  not a straight-line scan. For multi-cell blocks the whole footprint must be
  legal at every step, so an L-shaped block may fail to turn a corner a 1×1 block
  manages.
- `MoveGenerator` enumerates the flood fill's output. This is cheaper than
  scanning each direction separately: one traversal yields the complete set.
- Axis restrictions (M7) still apply — a restricted block uses only its permitted
  step directions during the traversal.
- Branching rises substantially, especially in the mid-game as the board empties.
  Canonical pruning (D5) becomes mandatory rather than merely helpful.
- "Maximum slide" is no longer meaningful as a canonical criterion and is
  replaced by "resting against an obstacle."

**Unaffected.** `Move` already carried a target position rather than a direction
and distance, so its shape is unchanged. Gate compatibility, the event model,
fixpoint resolution, and progress monotonicity are all untouched.

---

## D28 — Precomputed lookups live on `LevelContext`; the occupancy map is lazy per state

**Decision.** `LevelContext` gains two more precomputed lookups alongside
`staticWallLookup` and its shutter-cell lookup: `specByIndex`, an O(1) map
from a block's flat index — spanning top-level blocks and every
generator/elevator spawn slot — to its `BlockSpec`, and a position-based
shutter lookup. These are exposed as `SpecAt`, `ShutterPositionAt`, and
`TotalBlockCapacity`. `BoardState` gains a per-state occupancy map (`int[]`,
cell to living block index or none), built lazily on first use and cached for
the state's lifetime.

**Why.** `IsCellFree` and the predicates built on it (`CanMove`,
`CanBeTargeted`) sit directly in the move generator's flood fill (Module 04),
which calls them potentially hundreds of times per state across millions of
states during a search. Two different costs were compounding there: resolving
a block index to its spec, and resolving a shutter cell to its array
position, both by re-walking data that never changes once a level is loaded;
and answering "what's at this cell" by scanning every block's footprint from
scratch on every single call, even though the board only changes once per
state, not once per call.

**The eager/lazy split.** `specByIndex` and the shutter position lookup are
built eagerly, in `LevelContext`'s constructor, alongside the lookups already
there — they are small, bounded by authored level content rather than by
search size, and every level that gets solved needs both immediately.
`BoardState`'s hash is likewise eager, because every state the search
constructs is hashed at least once — that is the entire point of the visited
set. The occupancy map is the exception: built lazily, on first need, because
most states a search constructs are discarded as duplicates — rejected by
hash or equality — before move generation ever runs on them. An eager
occupancy map would charge every one of those discarded states for a scan
whose result is never read; a lazy one costs nothing until a state actually
survives into the frontier.

**Rejected.** An external cache in `GateRush.Core`, keyed off the
`LevelContext` reference, holding the same precomputed data without touching
Module 01. Rejected for two reasons. First, it would mean two modules
independently know how to flatten `Generators`/`Elevators` into one index
space and walk shutter geometry — knowledge that is really a property of
`LevelContext`'s own structure, so a change to one walk and not the other
would break silently rather than fail to compile. Second, it raises a
lifetime question a field on `LevelContext` never has to answer: keyed off a
reference with no natural owner, when is an entry evicted? A field dies with
the `LevelContext` it belongs to — in the Level Editor, opening level after
level does not accumulate cache entries the way an external table keyed by
reference would.

**Note.** Closing a module in a prior session does not make it untouchable.
Architecture grows as needs surface during later modules; this entry is that
growth, recorded.

**Later addition (Module 03).** `LevelContext` also precomputes
`MaxResolutionPasses` — the fixpoint-loop iteration bound `MoveResolver` needs
(D8). It is the total number of monotonic progress steps the level can contain:
every clearable colour across every block (top-level and spawned), every
generator spawn, and every elevator wave. This is a pure function of immutable
level data and was being recomputed on every `MoveResolver` call over the whole
flat block-index space; it belongs here for the same reason `specByIndex` does.

---

## D29 — `BlockSpec` carries `Axis`

**Decision.** `MovementAxis Axis` is added to `BlockSpec`, alongside the
`Cells`, `ColorStack`, `UnfreezeAtClearCount`, and `LockId` it already unifies
from `BlockDefinition` and `SpawnedBlock`.

**Why.** `MoveResolver` (Module 03) must honour axis restrictions (M7) for every
block it moves, and it addresses blocks by flat index — the space spanning
top-level blocks plus every generator/elevator spawn slot that `SpecAt`
resolves. Reading `Axis` from `LevelContext.Blocks[i]` instead would only work
for top-level blocks and would reintroduce the second index-to-definition lookup
path that D28 consolidated into `SpecAt`. `Axis` is the same family of field as
the ones `BlockSpec` already carries, so this is a one-line addition to the
struct and its two constructors.

**Note.** Same principle as D28's closing note: closing a module in an earlier
session does not freeze it. A later module surfacing a real need for one more
field on a Module 01 type is normal growth, recorded here rather than worked
around.

---

## D30 — Block cells are normalised at construction

**Decision.** `BlockDefinition` and `SpawnedBlock` shift their `Cells` at
construction so the component-wise minimum is `(0, 0)`. `BlockDefinition` adds
the same offset to `StartOrigin` in compensation; absolute cell positions
(`StartOrigin + cell`) are unchanged.

**Why.** Nothing in the authoring format requires a block's cells to start at
`(0, 0)`, so without normalisation a block's origin could sit outside the grid
while its whole footprint is inside it. Normalising makes "a block's origin is
one of its occupied grid cells" an invariant. That lets the reachability flood
fill in `MoveResolver` — and in `MoveGenerator` (Module 04), the hottest loop in
the project — index a visited-set array by origin cell instead of hashing a
coordinate, and it simplifies the bounding-box arithmetic in the gate-projection
checks.

**Consequence.** A caller that reads `StartOrigin` back gets the normalised
value — the grid cell of the footprint's minimum corner — not necessarily the
number it passed in. The transform preserves every absolute position, so no
level behaves differently.

**Rejected.** Rejecting non-normalised input as an authoring error. It would
push a purely mechanical fix onto the level author for no benefit; the editor
would have to normalise before saving regardless.

---

## D31 — Reachability and gate-exit geometry extracted to `BlockReachability`

**Decision.** The corner-turning flood fill (D27) and the gate-projection /
compatibility check both move out of `MoveResolver` into a new engine-free
`GateRush.Core.BlockReachability`. `MoveResolver` keeps a private instance and
delegates; `MoveGenerator` (Module 04, `GateRush.Solver`) keeps its own private
instance and calls the same code.

**Why.** `MoveResolver.IsReachable` validates a single move against the flood
fill; `MoveGenerator` enumerates that same fill's entire output once per expanded
search node. Two copies would drift, and a drift is silently wrong in the
direction that matters least visibly: the generator would offer a move the
resolver then rejects, or omit one the player could make. One implementation
makes that unrepresentable — the same reasoning as D28's `SpecAt` and the
parameter removal from `ClearOuterColor`.

**Rejected.** Reimplementing the flood fill inside `MoveGenerator`, kept
deliberately identical, with a cross-reference comment in both files. That is
precisely the arrangement D28 rejected: two modules independently knowing one
rule, so a change to one and not the other breaks silently rather than failing to
compile. A comment is not a mechanism.

**Buffers, not sharing.** `BlockReachability` holds the reused BFS frontier and
an origin-indexed visited map — D30 makes a block's origin a grid cell, so it
indexes an array with no hashing. Those buffers are overwritten per scan and
`ReachableOrigins` returns the live buffer by reference, so an instance must
never be shared between callers or reused across a nested call, which
`ISearchStrategy` (Module 05) may introduce. Each of `MoveResolver` and
`MoveGenerator` constructs its own. The stateless predicates
(`IsAtCompatibleExitGate`, `IsRestingAgainstObstacle`, `IsFootprintLegal`) are
static and touch no buffer, which is what lets the generator call them while
iterating the live `reached` list.

**Note.** Same principle as D28's and D29's closing notes: a later module
surfacing a real need to refactor an earlier one is normal growth, recorded here
rather than worked around.

---

## D32 — The progress vector is a `Core` type, read from `BoardState`

**Decision.** The monotonic progress vector that stratifies the search (D6) —
`(TotalClearCount, generator spawn indices, elevator wave indices)` — is a
`GateRush.Core` type, `ProgressVector`, obtained from the
`BoardState.ProgressVector` property. It is not defined in `GateRush.Solver`
where its first consumer (Module 05's `BreadthFirstStrategy`) lives.

**Why.** Every component is a `BoardState` field, every component is hashed, and
the monotonicity — no action decreases any component — is an invariant
`MoveResolver` maintains. The knowledge of *which* fields form the vector is
therefore `BoardState`'s. Defining it in the solver would split that knowledge
from the code that defines the fields: when phase 1.13 adds generator and
elevator progress to `BoardState`, nothing on the solver side would signal that
the vector must grow a component. `AStarStrategy` (phase 1.11) needs the same
vector, so it must not be trapped inside one strategy.

**Ordering.** `ProgressVector.CompareTo` is lexicographic — `TotalClearCount`,
then the spawn indices in `LevelContext` order. Lexicographic order is a linear
extension of the componentwise partial order, which is what lets the search
retire a stratum's visited set the moment every live frontier node sits at a
strictly greater vector: nothing componentwise ≤ the retired vector can still be
produced.

**Excluded.** `ElevatorWaveActive`. It is set when a wave arrives and cleared
when the region empties, so it is not monotonic and cannot be a stratification
key. States that differ only by it stay distinct under `BoardState` hashing.

**Rejected.** A `ProgressVector` in `GateRush.Solver` built from `BoardState`'s
public getters. Same silent-drift failure as D31 — one rule (which fields advance
monotonically) known in two places.

**Note.** Same principle as D28's, D29's, and D31's closing notes: a later module
surfacing a real need to add a small type to `Core` is normal growth, recorded
rather than worked around.
