using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// The boards every <c>ISearchStrategy</c> is tested against: solvable
    /// boards with a hand-verified shortest solution, and boards with no
    /// solution at all. Shared so <c>BreadthFirstStrategyTests</c> and
    /// <c>AStarStrategyTests</c> run the same corpus — the A\*/breadth-first
    /// equivalence test means nothing if the two suites could drift apart.
    /// </summary>
    /// <remarks>
    /// Every solvable board is chosen so canonical pruning does not lengthen the
    /// optimum, so both <c>MoveGenMode</c>s agree on it. A board with no keys has
    /// an optimum of at least its colour count, since every clear then costs a
    /// move of its own; most boards below meet that bound exactly, which is what
    /// makes their optima easy to verify by hand. Grid conventions: y = 0 is the
    /// bottom row, and an edge feature's offset is measured along its edge.
    /// </remarks>
    internal static class SearchCorpus
    {
        internal static IEnumerable<(string name, LevelContext ctx, BoardState initial, int optimum)> SolvableCorpus()
        {
            var slide = Ctx(5, 1, new[] { Block(1, new Coord(2, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            yield return ("lone block slides to its gate", slide, BoardState.CreateInitial(slide), 1);

            var twoInLine = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            yield return ("far block waits for the near one", twoInLine, BoardState.CreateInitial(twoInLine), 2);

            var threeInLine = Ctx(
                7, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0)), Block(3, new Coord(2, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            yield return ("three in a row, forced order", threeInLine, BoardState.CreateInitial(threeInLine), 3);

            var fourInLine = Ctx(
                8, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0)),
                    Block(3, new Coord(2, 0)), Block(4, new Coord(3, 0))
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            yield return ("four in a row, forced order", fourInLine, BoardState.CreateInitial(fourInLine), 4);

            var layered = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 2, 1, BlockColor.Blue)
                });
            yield return ("layered block, one gate per colour", layered, BoardState.CreateInitial(layered), 2);

            var packed = PackedFourColourBoard();
            yield return ("fully packed, every block pre-aligned", packed, BoardState.CreateInitial(packed), 4);

            var loose = LooseBlocksOneGateBoard();
            yield return ("three loose blocks share one gate", loose, BoardState.CreateInitial(loose), 3);

            // M2: the blue gate stays closed until the red clear opens it.
            var countGatedGate = Ctx(
                2, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }) },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Right, 0, 1, BlockColor.Blue, openAt: 1)
                });
            yield return ("count-gated gate opens after the first clear", countGatedGate,
                BoardState.CreateInitial(countGatedGate), 2);

            // M3: the blue block cannot move until the red clear unfreezes it.
            var frozen = Ctx(
                2, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }, unfreezeAt: 1)
                },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Right, 0, 1, BlockColor.Blue)
                });
            yield return ("frozen block thaws after the first clear", frozen, BoardState.CreateInitial(frozen), 2);

            // M5: a colour-bound shutter over the blue block opens on one red clear.
            var shuttered = Ctx(
                2, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }) },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Right, 0, 1, BlockColor.Blue)
                },
                shutters: new[] { Shutter(1, new Coord(1, 0), new Coord(1, 0), threshold: 1, requiredColor: BlockColor.Red) });
            yield return ("shutter opens on a red clear", shuttered, BoardState.CreateInitial(shuttered), 2);

            // M8: the key only unlocks, so the locked block still costs its own move.
            var unlockKey = Ctx(
                2, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: KeyEffect.UnlockMovement),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 1)
                },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Right, 0, 1, BlockColor.Blue)
                });
            yield return ("unlock-movement key frees the locked block", unlockKey, BoardState.CreateInitial(unlockKey), 2);

            // M8: clearing the key block clears the locked block in the same move —
            // two colours, one move.
            var clearKey = Ctx(
                2, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 1)
                },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Right, 0, 1, BlockColor.Blue)
                });
            yield return ("clear-outer-colour key clears the lock for free", clearKey, BoardState.CreateInitial(clearKey), 1);

            var mixed = MixedKeyEffectLockBoard();
            yield return ("mixed key effects, last key consumed decides", mixed, BoardState.CreateInitial(mixed), 2);

            // D35: four interchangeable blocks on an open grid. Every ordering of
            // them is a different labelling of the same board, so this is the
            // board the symmetry collapse exists for.
            var interchangeable = InterchangeableBlocksBoard();
            yield return ("four interchangeable blocks share one gate", interchangeable,
                BoardState.CreateInitial(interchangeable), 4);

            var twoColourGroups = TwoInterchangeableGroupsBoard();
            yield return ("two interchangeable colour groups, one gate each", twoColourGroups,
                BoardState.CreateInitial(twoColourGroups), 6);

            // D41: key effects that wait for a closed shutter.
            var shutteredClear = ShutteredClearOuterColorLockBoard();
            yield return ("shuttered lock waits for its clear-outer-colour key", shutteredClear,
                BoardState.CreateInitial(shutteredClear), 2);

            var shutteredUnlock = ShutteredUnlockMovementLockBoard();
            yield return ("shuttered lock waits to unlock", shutteredUnlock,
                BoardState.CreateInitial(shutteredUnlock), 3);

            var shutteredMixed = ShutteredMixedKeyEffectLockBoard();
            yield return ("shuttered lock with mixed keys, completing key decides what waits", shutteredMixed,
                BoardState.CreateInitial(shutteredMixed), 3);

            var twoWaiting = TwoWaitingClearsUnderOneShutterBoard();
            yield return ("one opening releases two waiting clears", twoWaiting,
                BoardState.CreateInitial(twoWaiting), 3);

            // D42: spawners. Both boards reach their optimum in any clear order,
            // so nearest-next-clear solves them as well.
            var generatorRelease = GeneratorReleasesAWaitingKeyEffectBoard();
            yield return ("generator's block lands and a waiting key clear applies", generatorRelease,
                BoardState.CreateInitial(generatorRelease), 2);

            var elevatorHeld = ElevatorHeldByATopLevelBlockBoard();
            yield return ("elevator wave waits for a top-level block to leave", elevatorHeld,
                BoardState.CreateInitial(elevatorHeld), 2);
        }

        internal static IEnumerable<(string name, LevelContext ctx, BoardState initial)> UnsolvableCorpus()
        {
            var noGate = Ctx(3, 3, new[] { Block(1, new Coord(1, 1)) }, new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Blue) });
            yield return ("block colour has no matching gate", noGate, BoardState.CreateInitial(noGate));

            var obstructed = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }, axis: MovementAxis.VerticalOnly)
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            yield return ("only exit parked shut by an immovable block", obstructed, BoardState.CreateInitial(obstructed));

            var deadLayer = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            yield return ("layered block's second colour has no gate", deadLayer, BoardState.CreateInitial(deadLayer));
        }

        /// <summary>Every board in both corpora, without the optimum.</summary>
        internal static IEnumerable<(string name, LevelContext ctx, BoardState initial)> WholeCorpus()
        {
            return SolvableCorpus()
                .Select(b => (b.name, b.ctx, b.initial))
                .Concat(UnsolvableCorpus());
        }

        /// <summary>
        /// A 2x2 board with no free cell, every block already flush against its
        /// own gate. Optimum 4: four zero-distance clears.
        /// </summary>
        internal static LevelContext PackedFourColourBoard()
        {
            return Ctx(
                2, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }),
                    Block(3, new Coord(0, 1), colors: new[] { BlockColor.Green }),
                    Block(4, new Coord(1, 1), colors: new[] { BlockColor.Yellow })
                },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 1, 1, BlockColor.Blue),
                    Gate(3, BoardEdge.Top, 0, 1, BlockColor.Green),
                    Gate(4, BoardEdge.Top, 1, 1, BlockColor.Yellow)
                });
        }

        /// <summary>
        /// Three red blocks spread over an open 5x5 grid, one red gate at the
        /// bottom-left corner of the left edge. Optimum 3: the corner block clears
        /// in place, and each destroyed block frees the gate for the next, which
        /// reaches it in one move. The open grid gives every block dozens of
        /// pointless destinations — the branching breadth-first search must wade
        /// through and A\* should not.
        /// </summary>
        internal static LevelContext LooseBlocksOneGateBoard()
        {
            return Ctx(
                5, 5,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 2)), Block(3, new Coord(4, 4)) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
        }

        /// <summary>
        /// Four identical red blocks on an open 3x3 grid with one red gate at the
        /// bottom of the left edge. Optimum 4: the block already at (0, 0) clears
        /// in place, and each of the other three reaches that cell in a single
        /// move once it is free. Four colours with no key means four moves is
        /// also the lower bound.
        /// </summary>
        /// <remarks>
        /// The D35 board. All four blocks share one spec, so the labelled state
        /// space carries a factor of 4! of pure duplication that the collapse
        /// removes; the open grid gives the permutations room to actually be
        /// reached, which a one-wide corridor would not.
        /// </remarks>
        internal static LevelContext InterchangeableBlocksBoard()
        {
            return Ctx(
                3, 3,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(2, 0)),
                    Block(3, new Coord(1, 1)),
                    Block(4, new Coord(2, 2))
                },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
        }

        /// <summary>
        /// A fully packed 3x2 board: three green blocks on the bottom row, three
        /// purple on the top, one green gate at (0, 0)'s left edge and one purple
        /// gate at (2, 1)'s right edge. Optimum 6 — the six colours, no key, so
        /// every clear costs its own move.
        /// </summary>
        /// <remarks>
        /// The shape a real authored level takes (D16: packed, with a clear-ready
        /// opening move) reduced to something whose optimum is checkable by hand,
        /// and the two-group case: green and purple each form an interchangeable
        /// group, each funnelling through a single gate.
        /// </remarks>
        internal static LevelContext TwoInterchangeableGroupsBoard()
        {
            return Ctx(
                3, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Green }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Green }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green }),
                    Block(4, new Coord(0, 1), colors: new[] { BlockColor.Purple }),
                    Block(5, new Coord(1, 1), colors: new[] { BlockColor.Purple }),
                    Block(6, new Coord(2, 1), colors: new[] { BlockColor.Purple })
                },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Green),
                    Gate(2, BoardEdge.Right, 1, 1, BlockColor.Purple)
                });
        }

        /// <summary>
        /// A 3x2 board with a static wall at (2, 1). Red at (0, 0) must reach the
        /// red gate on the right edge of row 0; blue at (1, 0) stands in its way,
        /// and blue's own gate — the top edge above column 1 — opens only after
        /// the first clear. Optimum 3: blue steps up to (1, 1), red slides out,
        /// and blue then clears in place. Two colours make the heuristic's lower
        /// bound 2, one short of the optimum.
        /// </summary>
        /// <remarks>
        /// The board that separates "found" from "proven shortest": a solution
        /// longer than the admissible bound, reachable in both move modes (the
        /// wall makes (1, 1) a canonical resting spot). Not in
        /// <see cref="SolvableCorpus"/>, whose boards are chosen to meet their
        /// bound.
        /// </remarks>
        internal static LevelContext StepAsideBeforeFirstClearBoard()
        {
            return Ctx(
                3, 2,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue })
                },
                new[]
                {
                    Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 1, 1, BlockColor.Blue, openAt: 1)
                },
                staticWalls: new[] { new Coord(2, 1) });
        }

        /// <summary>
        /// A 3x2 board where the nearest clear is a trap. A red vertical block at
        /// column 2 carries a lock needing two keys, and there is no red gate: it
        /// can only leave by being cleared, which happens only if the last key
        /// consumed is <see cref="KeyEffect.ClearOuterColor"/>. That key's green
        /// carrier starts flush against the green gate at (0, 0); the
        /// <see cref="KeyEffect.UnlockMovement"/> key's green carrier waits at
        /// (1, 1). Optimum 3: step the first carrier aside to (1, 0), clear the
        /// second, then clear the first — its key completes the lock and clears
        /// the red block for free. Clearing the pre-aligned carrier first
        /// instead spends its effect on a lock that is not yet complete, and the
        /// board can no longer be solved.
        /// </summary>
        /// <remarks>
        /// The counterexample to clear monotonicity for mixed-effect locks: a
        /// clear that turns a solvable board unsolvable. Its
        /// <see cref="LevelContext.IsClearMonotone"/> is false. Found by the
        /// random-board comparison against A\*, then reduced to this.
        /// </remarks>
        internal static LevelContext WastedClearKeyTrapBoard()
        {
            return Ctx(
                3, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Green },
                        keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor),
                    Block(2, new Coord(1, 1), colors: new[] { BlockColor.Green },
                        keyTarget: 1, keyEffect: KeyEffect.UnlockMovement),
                    Block(3, new Coord(2, 0), cells: new[] { new Coord(0, 0), new Coord(0, 1) },
                        lockId: 1, requiredKeys: 2)
                },
                new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Green) });
        }

        /// <summary>
        /// One blue lock needing two keys: a red <see cref="KeyEffect.ClearOuterColor"/>
        /// key and a green <see cref="KeyEffect.UnlockMovement"/> key. Every block
        /// is flush against its own gate on a packed 3x1 row. The key consumed
        /// last decides the effect: green then red clears the lock for free
        /// (2 moves); red then green only unlocks it, costing a third move.
        /// Optimum 2.
        /// </summary>
        /// <remarks>
        /// The regression board for the heuristic's free-clear count. A criterion
        /// requiring <em>every</em> unconsumed key to be
        /// <see cref="KeyEffect.ClearOuterColor"/> gives <c>h = 3</c> here at the
        /// start — an overestimate of the 2-move optimum.
        /// </remarks>
        internal static LevelContext MixedKeyEffectLockBoard()
        {
            return Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 2),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green },
                        keyTarget: 1, keyEffect: KeyEffect.UnlockMovement)
                },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 1, 1, BlockColor.Blue),
                    Gate(3, BoardEdge.Right, 0, 1, BlockColor.Green)
                });
        }

        /// <summary>
        /// A 3x1 row, every block flush above its own bottom gate except the
        /// owner: a red <see cref="KeyEffect.ClearOuterColor"/> key for lock 1,
        /// a green block, and lock 1's blue owner under a green-bound shutter.
        /// Optimum 2: in either order, the key and the green block each cost a
        /// push and the owner's clear comes free — on the key's death if the
        /// shutter is already open, otherwise released by the opening (D41).
        /// </summary>
        internal static LevelContext ShutteredClearOuterColorLockBoard()
        {
            return Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Green }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 1)
                },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 1, 1, BlockColor.Green)
                },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0), 1, BlockColor.Green) });
        }

        /// <summary>
        /// <see cref="ShutteredClearOuterColorLockBoard"/> with an
        /// <see cref="KeyEffect.UnlockMovement"/> key and a blue gate under the
        /// owner. Optimum 3: the key, the green block, then the owner's own push
        /// once it is uncovered and unlocked.
        /// </summary>
        internal static LevelContext ShutteredUnlockMovementLockBoard()
        {
            return Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: KeyEffect.UnlockMovement),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Green }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 1)
                },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 1, 1, BlockColor.Green),
                    Gate(3, BoardEdge.Bottom, 2, 1, BlockColor.Blue)
                },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0), 1, BlockColor.Green) });
        }

        /// <summary>
        /// A 4x1 row: a red <see cref="KeyEffect.ClearOuterColor"/> key and a
        /// red <see cref="KeyEffect.UnlockMovement"/> key, both for lock 1
        /// (which needs both), a green block, and lock 1's blue owner under a
        /// green-bound shutter with its own blue gate. Optimum 3: the
        /// <see cref="KeyEffect.UnlockMovement"/> key first, so the
        /// <see cref="KeyEffect.ClearOuterColor"/> key completes the count and
        /// the owner's clear comes free. The other order still solves, in 4 —
        /// the owner has a gate, so no order traps it.
        /// </summary>
        internal static LevelContext ShutteredMixedKeyEffectLockBoard()
        {
            return Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor),
                    Block(2, new Coord(1, 0), keyTarget: 1, keyEffect: KeyEffect.UnlockMovement),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green }),
                    Block(4, new Coord(3, 0), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 2)
                },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 1, 1, BlockColor.Red),
                    Gate(3, BoardEdge.Bottom, 2, 1, BlockColor.Green),
                    Gate(4, BoardEdge.Bottom, 3, 1, BlockColor.Blue)
                },
                shutters: new[] { Shutter(1, new Coord(3, 0), new Coord(3, 0), 1, BlockColor.Green) });
        }

        /// <summary>
        /// A 5x1 row: two red <see cref="KeyEffect.ClearOuterColor"/> keys (for
        /// locks 1 and 2), a green block, and the two blue owners side by side
        /// under one green-bound shutter. Five colours, optimum 3: three pushes,
        /// two free clears. Pushing both keys first leaves both effects waiting,
        /// and the green push then releases both at once (D41) — the board that
        /// shows the heuristic must count a waiting clear in <c>F</c>: after the
        /// two keys one move remains, and without that <c>h</c> would be 3.
        /// </summary>
        internal static LevelContext TwoWaitingClearsUnderOneShutterBoard()
        {
            return Ctx(
                5, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor),
                    Block(2, new Coord(1, 0), keyTarget: 2, keyEffect: KeyEffect.ClearOuterColor),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green }),
                    Block(4, new Coord(3, 0), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 1),
                    Block(5, new Coord(4, 0), colors: new[] { BlockColor.Blue }, lockId: 2, requiredKeys: 1)
                },
                new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 1, 1, BlockColor.Red),
                    Gate(3, BoardEdge.Bottom, 2, 1, BlockColor.Green)
                },
                shutters: new[] { Shutter(1, new Coord(3, 0), new Coord(4, 0), 1, BlockColor.Green) });
        }

        /// <summary>
        /// A 4x2 board: one horizontal-only red block at (1, 0), flush above the
        /// red bottom gate at offset 1, with a static wall at (3, 0). Optimum 2:
        /// the block cannot be pushed down into the gate in place (D39), so it
        /// slides away and back, and arriving clears it. The wall makes (2, 0) a
        /// resting position, so canonical pruning keeps the slide away and both
        /// modes agree on the optimum.
        /// </summary>
        /// <summary>
        /// 3x2. A red key block (index 0, a <see cref="KeyEffect.ClearOuterColor"/>
        /// key for lock 1) sits flush against a red gate on the left, row 0; a
        /// green block (index 1) sits flush against a green gate on the right,
        /// row 1, on the cell a top-edge generator spawns into. The generator's
        /// one queued block (index 2) is blue and owns lock 1; no blue gate
        /// exists, so only its key can clear it. Optimum 2 in either order:
        /// green first spawns the locked block and the red push then clears it;
        /// red first leaves the effect waiting in the unspawned slot (D42), and
        /// the green push — or merely moving green aside — spawns it and the
        /// effect applies in that resolution. The heuristic is 2 at the start
        /// (three colours, one free clear), and the release-at-spawn edge is
        /// the one A\*'s consistency argument has to cover.
        /// </summary>
        internal static LevelContext GeneratorReleasesAWaitingKeyEffectBoard()
        {
            return Ctx(
                3, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor),
                    Block(2, new Coord(2, 1), colors: new[] { BlockColor.Green })
                },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Right, 1, 1, BlockColor.Green)
                },
                generators: new[]
                {
                    Spawner(1, BoardEdge.Top, 2, 1,
                        Spawned(colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 1))
                });
        }

        /// <summary>
        /// 3x1. A red block (index 0) stands in a one-cell elevator region in
        /// the middle, holding back the one-block blue wave (index 1); a red
        /// gate is on the left, a blue one on the right. Optimum 2: red slides
        /// left and clears, the region empties and the wave arrives, blue slides
        /// right and clears — and the region reading empty again clears
        /// <c>ElevatorWaveActive</c> so the level is solved. Sliding red right
        /// instead also vacates the region, but leaves blue on the wrong side
        /// of it: a dead end the searches must explore and reject.
        /// </summary>
        internal static LevelContext ElevatorHeldByATopLevelBlockBoard()
        {
            return Ctx(
                3, 1,
                new[] { Block(1, new Coord(1, 0)) },
                new[]
                {
                    Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Right, 0, 1, BlockColor.Blue)
                },
                elevators: new[]
                {
                    Elevator(1, new Coord(1, 0), new Coord(1, 0),
                        new[] { Spawned(colors: new[] { BlockColor.Blue }, regionOrigin: new Coord(0, 0)) })
                });
        }

        internal static LevelContext AxisBlockReturnsToCrossAxisGateBoard()
        {
            return Ctx(
                4, 2,
                new[] { Block(1, new Coord(1, 0), axis: MovementAxis.HorizontalOnly) },
                new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) },
                staticWalls: new[] { new Coord(3, 0) });
        }

        /// <summary>
        /// A 3x2 board: one horizontal-only red block at (1, 0), flush above its
        /// only gate — red, on the bottom edge — and boxed in horizontally by
        /// static walls at (0, 0) and (2, 0). Unsolvable: the push down is across
        /// its axis (D39), and it has no room to slide away and arrive back.
        /// Clear-monotone, so nearest-next-clear may prove it.
        /// </summary>
        internal static LevelContext AxisBlockBoxedAboveCrossAxisGateBoard()
        {
            return Ctx(
                3, 2,
                new[] { Block(1, new Coord(1, 0), axis: MovementAxis.HorizontalOnly) },
                new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) },
                staticWalls: new[] { new Coord(0, 0), new Coord(2, 0) });
        }
    }
}
