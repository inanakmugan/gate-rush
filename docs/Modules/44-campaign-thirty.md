# Module 44 — A campaign of thirty levels

**Assembly:** level data under `Assets/Resources/Levels`, and configuration assets (no code expected)
**Depends on:** Modules 08, 19, 39, 43
**Phase:** 7.3

---

## Responsibility

Grow the campaign from fifteen levels to thirty, as D75 decides. The
fifteen existing levels keep their content and move to new positions;
fifteen new levels are placed between them, so each mechanic and each
joker is followed by levels that practise it. The new levels are designed
with the solver and delivered in three batches of five, each a playable
campaign the owner plays before the next batch.

In scope:

- The new level order, written into the level files.
- Fifteen new levels, each checked by the solver.
- The time budget of every new level, and of a moved level where its new
  place calls for it.
- The demo leaderboard's scores spread again for a campaign that pays
  1,500 gold (D73, D75).

Not in scope: new mechanics, changes to the solver or the editor, the
interface (7.4), and the store prices, which stay (D62).

---

## The order

Levels are played in `levelId` order (`LevelCatalog`). After the change,
file `level-N.json` holds the level at position N, with `levelId` N, and
is shown as "Level N+1". The existing files keep their names and `.meta`
files, so the meta GUIDs do not change; their contents move.

```
 1  The tutorial                          existing 1
 2  The base rules                        existing 2
 3  The base rules, harder                new
 4  Layered blocks                        existing 3
 5  The Clock unlocks: a race             new
 6  Layered blocks, practice              existing 4
 7  One-way blocks                        existing 5
 8  One-way and layered                   existing 6
 9  Ice blocks                            existing 7
10  The Rocket unlocks: an ice block      new
11  Ice doors                             existing 8
12  A mix                                 existing 9
13  Time bonus                            existing 10
14  Time bonus and ice doors, practice    new
15  The Broom unlocks: one colour         new
16  Locks and keys                        existing 11
17  Locks and keys, practice              new
18  Shutters                              existing 12
19  Shutters, practice                    new
20  A mix                                 new
21  Generators                            existing 13
22  Generators, practice                  new
23  Elevators                             existing 14
24  Elevators, practice                   new
25  A mix                                 new
26-29  Harder mixes                       new
30  The finale                            existing 15
```

The mechanic introduction cards (Module 19) and the New Joker cards
(Module 43) follow the order by themselves; no level introduces a
mechanic and a joker together.

---

## Design rules for a new level

- It uses only mechanics introduced at or before its position.
- It passes the level data checks and the editor warnings of MECHANICS
  (Level design conventions): a ready opening move, a gate for every
  colour, no gate too narrow.
- The solver proves it solvable without any joker (MECHANICS, Jokers).
- Its difficulty is measured, not guessed: the shortest solution the
  solver finds, the states it explores, and the share of 150 naive
  playouts (a player who takes a clearing move when there is one, at
  random otherwise) that win. The existing levels give the scale: level 2
  is won by 97 of those players in 100, the mid levels by 20 to 50, the
  hardest by under 10.
- A joker's level is built so the joker visibly helps, and the help is
  measured: the playout win rate after using it once.
- The time budget is about 3.5 to 4 seconds per move of the solver's
  solution, as the existing levels have; the Clock's level is tighter, so
  the Clock matters.
- A campaign win pays 50 gold, as every level does.

---

## Batches

- **Batch 1:** levels 3, 5, 10, 14 and 15. The campaign is then twenty
  levels: 1 to 15 as above, then the existing 11 to 15 as levels 16 to 20.
- **Batch 2:** levels 17, 19, 20, 22 and 24.
- **Batch 3:** levels 25 to 29, the leaderboard's scores, and the final
  order of all thirty.

Each batch is written straight into the level files on the module's
branch; the owner plays it, and any level he finds unfair or dull is
replaced before the next batch.

---

## The leaderboard (D73, D75)

- The twenty demo players' scores spread from about 80 to 1,960, so a
  player who wins the whole campaign (1,500 gold) climbs most of the table
  and does not reach the top by playing alone. The names and avatars stay.

---

## Tests

- The Edit Mode tests that load every level file (Module 08 and the
  catalog's) pass with thirty files.
- No test counts the levels; if one does, it is updated and named in the
  batch's notes.

**By hand (owner), per batch**
- Each new level loads, shows the cards it should (a mechanic's first
  level, a joker's unlock level) and is beatable in its time.
- The joker levels: the joker given on unlock makes the level clearly
  easier.
- The level select in Settings lists every level in the new order.

---

## Resolved during implementation

- **File numbering.** The order section above said `level-N.json` holds
  position N and is shown as Level N+1. The owner found the off-by-one
  confusing, so the files now match the game: `level-N.json` is Level N
  with `levelId` N, from `level-1` to `level-30`, and `level-0` is
  deleted. The Level scene's LevelBootstrap Level field, which pointed at
  `level-0`, now points at `level-1`. Numbering still comes from the
  `levelId` order (`LevelCatalog`), not from the names.
- **How the levels were made.** `Core`, `Solver` and the level
  serializer were compiled outside Unity, with a small stand-in for
  `JsonUtility`, to measure levels in bulk. Candidate boards were
  generated by tiling the board with small polyominoes, placing gates,
  colours and the mechanics each position allows, then kept only when
  the exhaustive nearest-next-clear search proved them solvable, they had
  a ready opening move, and their measures fell in the band wanted for
  their place. The tools are not part of the project.
- **The measures.** For each new level, the A* solution length and the
  playout win rate (150 naive playouts): 3: 20 moves, 72 percent;
  5: 28, 56; 10: 25, 18 (99 after the Rocket clears the ice block);
  14: 26, 37; 15: 27, 17 (the Broom clears eight blocks at once);
  17: 23, 31; 19: 24, 30; 20: 25, 24; 22: 26, 36; 24: 28, 29;
  25: 24, 23; 26: 28, 20; 27: 29, 13; 28: 28, 13; 29: 32, 7.
- **Time budgets.** About 3.7 seconds per move, bonuses left out; the
  Clock's level has 75 seconds for 28 moves. The moved levels keep their
  budgets.
- **The leaderboard.** The twenty demo scores are doubled, from 80 to
  1,960. A player who wins the whole campaign ranks seventh of
  twenty-one.
- **Tests.** No test counted the levels; 2,689 in all, unchanged.
