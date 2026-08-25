# Game Mechanics — Normative Specification

This document defines game rules, not implementation. Where a rule is stated
here, it is authoritative; code that contradicts it is a bug.

## Vocabulary

| Term | Meaning |
|---|---|
| **Cell** | One grid square |
| **Block** | A rigid set of cells with one or more colours. Never rotates. |
| **Colour of a block** | Its outermost remaining colour |
| **Gate** | An opening on a board edge with a colour and a width |
| **Clear** | Removing a block's current colour |
| **Progress counters** | `TotalClearCount` plus per-colour clear counts |

> **A block's colour is its outermost remaining colour.** Gates, rockets, and
> brooms all match against that colour and no other. Colours beneath it are not
> addressable until they become outermost.

Most blocks carry exactly one colour, so clearing it removes the block. Layered
blocks are the uncommon case and require no special handling: clearing the outer
colour simply exposes the next one.

Every clear emits exactly one `ColorCleared` event, whether or not the block
survives it.

---

## M1 — Base mechanic

### Movement

Movement is **dragging, not teleporting**. The player grabs a block and carries
it with a finger; the block advances one cell at a time and stops when the way
is blocked. The rule layer records the *result* of that drag — where the block
started and where it ended.

- A block advances one cell at a time in the four cardinal directions, unless
  restricted (see M7).
- **A block may reach any position connected to its start by a path of such
  steps.** It is not limited to a straight line: it can turn corners. Every
  intermediate position must be fully legal — the block's whole footprint inside
  the grid, clear of static walls, other blocks, and closed shutter regions.
- This is why a block whose only free neighbour is diagonal cannot move there.
  There is no diagonal step; reaching a diagonal cell requires a free orthogonal
  route around. In play the intermediate steps pass instantly, so the motion
  reads as diagonal even though the rule is not.
- **Blocks never rotate.** A vertical 1×2 block is vertical for the whole level.
- Blocks occupy whole cells only; there are no intermediate positions.
- Corridors one cell wide therefore admit only blocks one cell thin in the
  relevant axis. This follows from the rules above; it is not a separate rule.

### Gates and exit compatibility

A block may exit through a gate when **all** of these hold:

1. The gate is open (see M2).
2. The gate's colour equals the block's current colour.
3. The block's footprint is flush against the gate's edge.
4. The block's **projection span** onto that edge is **less than or equal to**
   the gate's width. A small block may exit through a wide gate; a wide block
   may not exit through a narrow one.
5. The block's projected span lies **entirely within** the gate's opening —
   partial overlap does not qualify.

> Projection span = the extent of the block's footprint measured along the
> gate's edge. A vertical 1×2 block projects **1** onto the top and bottom edges
> and **2** onto the left and right edges. This is why orientation determines
> which gates a block can use.

For non-rectangular blocks (L shapes), the projection is of the **whole
footprint**, not only the cells touching the wall. An L block projecting 2 cells
requires a gate of width 2 or more, even if only one of its cells touches the
wall — the rest of the block would collide with the wall.

### Exit is move-triggered, never automatic

A block sitting against a compatible open gate does **not** exit on its own. The
player must deliberately push it into the gate.

Consequences:

- **A block may pass in front of a compatible gate without exiting.** Sliding
  past a gate on the way somewhere else is a legitimate manoeuvre.
- **Zero-distance moves are legal and essential.** A block already flush against
  a compatible open gate exits when the player pushes it toward that gate, even
  though it does not change position. Because levels start tightly packed, this
  is typically the *first move of the level*: the block that starts pre-aligned
  with its gate has nowhere to slide, and is cleared in place.
- **A gate that opens later needs no special rule.** The waiting block is
  cleared by an ordinary zero-distance move once the player pushes it.

`MoveGenerator` must emit zero-distance moves, and `InputController` must
recognise a drag whose direction is determined but whose distance is zero.

### Exit behaviour

On a successful exit the block **clears its colour and remains at the gate
mouth**. It does not return to its previous position and it does not pass
through.

Consequence: a block sitting in front of a gate **obstructs that gate** for
every other block. This is a genuine trap and a legitimate difficulty tool.

If the block had further colours beneath, it survives with the next colour
exposed. That colour never matches the gate it is parked at (see M4), so the
player must move it elsewhere — it is now blocking a gate it cannot use.

If the block had no further colours, it is destroyed and its cells become free.

### Static obstacles

Cells may be permanently blocked. They are part of `LevelContext` and never
change.

---

## M2 — Count-gated gates

A gate may carry a threshold N. It is closed and colourless (rendered frozen)
until `TotalClearCount >= N`, then opens permanently.

A closed gate rejects all exits.

---

## M3 — Count-gated blocks

A block may carry a threshold N. Until `TotalClearCount >= N` it is **frozen**:

- It cannot be moved by the player.
- Its colour is hidden from the player (visibility only — the solver sees it).
- It still occupies its cells and still obstructs other blocks.
- **Jokers can still target it.**

Once unfrozen it behaves as a normal block, permanently.

---

## M4 — Layered blocks

A block may carry a stack of colours. Only the outermost is the block's colour;
the rest are not addressable until exposed.

- Each exit clears exactly **one** colour.
- **Adjacent layers must differ.** A colour is never immediately beneath itself.
  This is a hard constraint on level data, not a runtime check.
- The player sees the outer colour and the one beneath it. If the stack is
  deeper than two, a numeral shows the remaining count.
- A block with a single colour is an ordinary block. Depth 1 is the default
  case, not a special case.

Every clear counts toward progress counters using **the colour that was
removed**, regardless of what lies beneath.

Because adjacent layers differ, a block parked at a gate can never be cleared
twice in place: the newly exposed colour cannot match the gate it is standing at.

**Level requirement.** Every colour in a block's stack must have a reachable,
compatible gate somewhere in the level. The editor warns when this fails.

---

## M5 — Shutters

A rectangular region may be covered by a shutter with a threshold N.

- While closed, blocks inside are unknown to the player and **unreachable**: no
  block may move into the region, and blocks inside cannot move or be targeted
  by jokers.
- A shutter is either **global** (opens at `TotalClearCount >= N`) or
  **colour-bound** (opens at `ClearCountByColour[c] >= N`).
- Opening is permanent.

Shutters are the only thing that makes a block untargetable by jokers.

---

## M6 — Generators

A generator sits on a board edge and pushes blocks inward — the inverse of a gate.

- Its output sequence is an **explicit, ordered array in level data**. Nothing is
  random at runtime.
- It spawns the next block when **every cell that block would occupy is empty**.
  If even one is occupied, nothing spawns and the generator waits.
- The player sees the next block's shape and colour before it arrives; when a
  block in front of the generator is grabbed, the target cells preview the
  incoming block's colour. This is presentation only.
- After its sequence is exhausted, the generator is destroyed.
- A level is not complete while any generator still has output pending.

---

## M7 — Axis-restricted blocks

A block may be restricted to horizontal-only or vertical-only movement, shown by
an arrow.

Design implication: such a block must have a compatible gate at one end of its
axis, or it can never be cleared by movement alone. The editor warns when this
is not the case.

---

## M8 — Locks and keys

A block may carry a **lock**; another block may carry a **key**. Lock and key are
paired by an identifier rendered as a colour badge, independent of the blocks'
own colours.

- **Lock identifiers are unique within a level.** The identifier doubles as the
  badge colour shown to the player, so two locked blocks sharing one would be
  unreadable — the player could not tell which key opens which.
- A locked block cannot be moved by the player. Its colour and shape remain
  visible. It still obstructs.
- **Jokers can still target a locked block.**
- A lock may require more than one key; the required count is shown on the lock.
- When a key-carrying block is destroyed, its key is consumed and applied.

A key has one of two effects, chosen by the designer:

| Effect | Result |
|---|---|
| `UnlockMovement` | The target block becomes movable |
| `ClearOuterColor` | The target block's current colour is removed immediately |

`ClearOuterColor` emits a `ColorCleared` event like any other clear and feeds the
same counters. It may therefore trigger further unlocks within the same
resolution pass.

A lock is a single per-block flag, not a per-colour property. A newly exposed
colour is never locked.

---

## M9 — Elevators

An elevator occupies a rectangular region of arbitrary size.

- Its waves are **explicit, ordered arrays in level data**.
- The next wave arrives when the region contains **no blocks at all**.
- After its final wave is cleared, the elevator is destroyed.
- A level is not complete while any elevator still has waves pending.

Elevators may sit beneath shutters, and their waves may contain frozen or locked
blocks. No special handling is required: the fixpoint loop composes these.

---

## M10 — Time-bonus blocks

A block may carry a bonus of N seconds, added to the countdown when the block is
**destroyed** (final colour cleared), not on each clear.

Time is not part of `BoardState` and not part of the search space. The editor
sums available bonuses when suggesting a time budget.

---

## Win and loss

**Win:** no living blocks remain, every generator is exhausted, and every
elevator is exhausted.

**Loss:** the countdown reaches zero, or the player leaves the level
voluntarily. Voluntary exit **costs a life** and must be preceded by a
confirmation prompt stating so.

---

## Jokers

Jokers are meta-layer items applied during play. **The solver ignores them
entirely** — every level must be solvable without any joker.

| Joker | Effect |
|---|---|
| **Clock** | Adds N seconds to the countdown. No board effect. |
| **Rocket** | Clears the colour of one player-selected block. |
| **Broom** | Clears the chosen colour from **every** block currently showing it. |

Both rocket and broom match on the block's *current* colour. Neither can reach a
colour beneath it; that colour becomes targetable only once exposed.

Because adjacent layers differ, a broom naturally touches each block at most
once — a block whose red is cleared cannot show red again immediately, so no
special single-pass rule is needed.

### Joker targeting rules

Targeting is **not** the same question as movement. A block may be untargetable
while movable, or targetable while immovable.

| Block condition | Movable by player | Targetable by joker |
|---|---|---|
| Normal | Yes | Yes |
| Frozen (M3) | No | **Yes** |
| Locked (M8) | No | **Yes** |
| Under a closed shutter (M5) | No | **No** |

### Joker clears are ordinary clears

They emit `ColorCleared`, increment all counters, and trigger the same fixpoint
resolution as a move. There is no second removal path in the codebase.

A broom can produce many clears at once, potentially opening several gates,
shutters, and frozen blocks in a single resolution pass, which may in turn
trigger elevators and generators. **This is the single best stress test for the
fixpoint loop and must be present in the test corpus.**

---

## Level design conventions

Not runtime rules, but properties the Level Editor should measure and report.

- **Tight packing.** Levels start with few or no empty cells. This collapses the
  search space and makes the intended order nearly forced, which is what makes a
  fixed time budget fair.
- **A ready opening move.** At least one block should start flush against a
  matching open gate, cleared by a zero-distance move. A fully packed board is
  not deadlocked as long as this holds — clearing consumes no space.
- **Presentation must not teleport.** The block follows the pointer cell by cell
  and halts against obstacles; the player can steer around a blockage without
  releasing. Snapping straight to the destination would misrepresent the rule
  and hide why some destinations are unreachable.

### Editor warnings

- No legal move at level start.
- A colour in some block's stack has no compatible gate anywhere in the level.
- A compatible gate exists but is too narrow for that block's projection.
- An axis-restricted block has no compatible gate at either end of its axis.
- A gate or shutter threshold exceeds the total number of clears available.
- A lock has fewer matching keys than it requires.

### Level data errors (not warnings)

- Two adjacent layers of the same colour.
- Overlapping blocks at start, footprints outside the grid, gates outside their
  edge, keys pointing at non-existent locks.
