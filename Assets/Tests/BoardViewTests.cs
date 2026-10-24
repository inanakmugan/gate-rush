using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BoardView"/>'s layout rules shared with its recipes
    /// and effects (Module 46, pass 2): a machine's screen splits along its
    /// longer side into the next block's area and the count's, and an
    /// elevator's two doors stand side by side in the recess inside its rim.
    /// Pass 3: a move's redraw keeps the floor and the frame as the same
    /// objects and redraws the blocks, their marks and the gates, leaving
    /// Board's children in a fresh drawing's order; a restart and another
    /// level draw the floor and the frame afresh. These draw a real board
    /// with the project's Runtime Config, so they need Generate Art to have
    /// run.
    /// </summary>
    public class BoardViewTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void SplitScreen_Wide_PutsTheCountAtTheRightAndShrinksTheBlocksPartAboutItsCentre()
        {
            var screen = new Rect(0f, 0f, 2f, 0.5f);

            var (block, count) = BoardView.SplitScreen(screen, 0.25f, 0.5f);

            Assert.AreEqual(1.5f, count.xMin, Tolerance, "the count's quarter at the right");
            Assert.AreEqual(2f, count.xMax, Tolerance);
            Assert.AreEqual(0.5f, count.height, Tolerance, "the count is the screen's height");
            Assert.AreEqual(0.25f, block.height, Tolerance, "the block's area half its part's height");
            Assert.AreEqual(0.75f, block.center.x, Tolerance, "the block's area centred on its part");
            Assert.AreEqual(0.75f, block.width, Tolerance, "half its part's width");
        }

        [Test]
        public void SplitScreen_Upright_PutsTheCountAtTheBottom()
        {
            var screen = new Rect(0f, 0f, 0.5f, 2f);

            var (block, count) = BoardView.SplitScreen(screen, 0.25f, 1f);

            Assert.AreEqual(0f, count.yMin, Tolerance, "the count at the bottom");
            Assert.AreEqual(0.5f, count.yMax, Tolerance);
            Assert.AreEqual(0.5f, block.yMin, Tolerance, "the block above it");
            Assert.AreEqual(2f, block.yMax, Tolerance);
        }

        [Test]
        public void ElevatorDoorRects_TwoByTwo_StandInTheRecessEitherSideOfTheSeamWithTheGapRoundEach()
        {
            var config = ScriptableObject.CreateInstance<RuntimeConfig>();
            try
            {
                var region = new Rect(0f, 0f, 2f, 2f);
                var inset = config.ElevatorInnerInsetCells;
                var gap = config.ElevatorDoorGapCells;

                var (left, right) = BoardView.ElevatorDoorRects(config, region);

                Assert.AreEqual(inset.x + gap, left.xMin, Tolerance, "the left door inside the recess's left side");
                Assert.AreEqual(1f - gap, left.xMax, Tolerance, "short of the seam by the gap");
                Assert.AreEqual(1f + gap, right.xMin, Tolerance, "the right door past the seam by the gap");
                Assert.AreEqual(2f - inset.x - gap, right.xMax, Tolerance, "inside the recess's right side");
                Assert.AreEqual(inset.y + gap, left.yMin, Tolerance, "above the recess's bottom");
                Assert.AreEqual(2f - inset.y - gap, left.yMax, Tolerance, "below its top");
                Assert.AreEqual(left.size, right.size, "the same size");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        // ----- Pass 3: the floor and the frame are drawn once per drawing -----

        // The groups a redraw keeps, by name under Board.
        private static readonly string[] FloorAndFrameNames = { "Floor underlay", "Floor", "Frame contour", "Frame", "Frame shine" };

        private const int KeyBlock = 0;
        private const int LockedBlock = 1;
        private const int SlidingBlock = 2;
        private const int FrozenGateId = 2;

        private GameObject viewObject;

        [TearDown]
        public void TearDown()
        {
            if (viewObject != null)
            {
                Object.DestroyImmediate(viewObject);
            }
        }

        [Test]
        public void Redraw_AMove_KeepsTheFloorAndTheFrameAsTheSameObjects()
        {
            var (view, ctx, start) = DrawnView();
            var before = FloorAndFrame(view);

            view.Redraw(ClearTheKeyBlock(ctx, start));

            var after = FloorAndFrame(view);
            for (var i = 0; i < FloorAndFrameNames.Length; i++)
            {
                Assert.IsNotNull(after[i], FloorAndFrameNames[i]);
                Assert.AreSame(before[i], after[i], $"{FloorAndFrameNames[i]} is the same object");
            }

            Assert.AreEqual(ctx.Width * ctx.Height, after[1].childCount, "one floor tile per cell, none added");
        }

        [Test]
        public void Redraw_AMove_RedrawsTheBlocksTheirMarksAndTheGates()
        {
            var (view, ctx, start) = DrawnView();
            var board = Board(view);
            var lockedBefore = board.Find($"Block {LockedBlock}");
            var gateBefore = board.Find($"Gate {FrozenGateId}");
            Assert.IsNotNull(board.Find($"Block {KeyBlock}"), "the key block is drawn");
            Assert.IsNotNull(Descendant(lockedBefore, "Padlock"), "the locked block's padlock");
            Assert.IsNull(Descendant(gateBefore, "Arrow"), "the frozen gate has no arrow");

            view.Redraw(ClearTheKeyBlock(ctx, start));

            var lockedAfter = board.Find($"Block {LockedBlock}");
            var gateAfter = board.Find($"Gate {FrozenGateId}");
            Assert.IsNull(board.Find($"Block {KeyBlock}"), "the cleared block and its key are gone");
            Assert.IsNotNull(lockedAfter, "the opened block is drawn");
            Assert.AreNotSame(lockedBefore, lockedAfter, "drawn afresh");
            Assert.IsNull(Descendant(lockedAfter, "Padlock"), "its lock opened with the key (M8)");
            Assert.AreNotSame(gateBefore, gateAfter, "the gate is drawn afresh");
            Assert.IsNotNull(Descendant(gateAfter, "Arrow"), "the gate opened at one clear (M2)");
        }

        [Test]
        public void Redraw_ASlide_DrawsTheBlockAtItsNewOrigin()
        {
            var (view, ctx, start) = DrawnView();
            var board = Board(view);
            var before = board.Find($"Block {SlidingBlock}").localPosition;
            Assert.IsTrue(new MoveResolver().TryApplyMove(
                ctx, start, new Move(SlidingBlock, new Coord(2, 0)), out var slid, out _), "the slide is legal");

            view.Redraw(slid);

            var after = board.Find($"Block {SlidingBlock}").localPosition;
            Assert.AreNotEqual(before, after, "moved");
            Assert.AreEqual(before.y, after.y, Tolerance, "along its row");
            Assert.Less(after.x, before.x, "one cell left");
        }

        [Test]
        public void Redraw_AMove_LeavesBoardsChildrenInTheOrderAFreshDrawingHas()
        {
            var (view, ctx, start) = DrawnView();
            var cleared = ClearTheKeyBlock(ctx, start);

            view.Redraw(cleared);
            var redrawn = ChildNames(Board(view));
            view.Rebuild(cleared);
            var fresh = ChildNames(Board(view));

            CollectionAssert.AreEqual(fresh, redrawn);
        }

        [Test]
        public void Rebuild_ARestart_DrawsTheFloorAndTheFrameAfresh()
        {
            var (view, _, start) = DrawnView();
            var before = FloorAndFrame(view);

            view.Rebuild(start);

            var after = FloorAndFrame(view);
            for (var i = 0; i < FloorAndFrameNames.Length; i++)
            {
                Assert.IsNotNull(after[i], FloorAndFrameNames[i]);
                Assert.AreNotSame(before[i], after[i], $"{FloorAndFrameNames[i]} drawn afresh");
            }
        }

        [Test]
        public void Redraw_AfterInitializeForAnotherLevel_DrawsThatLevelsFloorAndFrame()
        {
            var (view, _, _) = DrawnView();
            var before = FloorAndFrame(view);
            var other = Fixture.Ctx(5, 3, blocks: new[] { Fixture.Block(1, new Coord(0, 0)) });
            Initialize(view, other);

            view.Redraw(BoardState.CreateInitial(other));

            var after = FloorAndFrame(view);
            Assert.AreNotSame(before[1], after[1], "a new floor");
            Assert.AreEqual(other.Width * other.Height, after[1].childCount, "the other level's cells");
            CollectionAssert.AreEqual(
                FloorAndFrameNames, ChildNames(Board(view)).Take(FloorAndFrameNames.Length).ToList(),
                "drawn once, first and in order, with no elevator in this level");
        }

        /// <summary>
        /// A 4×4 level drawn from its start: a red block carrying the key to a
        /// blue locked block, a red gate it is flush against, a blue gate that
        /// opens at one clear, and a yellow block free to slide left.
        /// </summary>
        private (BoardView view, LevelContext ctx, BoardState start) DrawnView()
        {
            var ctx = Fixture.Ctx(
                4, 4,
                blocks: new[]
                {
                    Fixture.Block(1, new Coord(0, 1), keyTarget: 1),
                    Fixture.Block(2, new Coord(2, 2), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 1),
                    Fixture.Block(3, new Coord(3, 0), colors: new[] { BlockColor.Yellow }),
                },
                gates: new[]
                {
                    Fixture.Gate(1, BoardEdge.Left, 1, 1, BlockColor.Red),
                    Fixture.Gate(FrozenGateId, BoardEdge.Right, 3, 1, BlockColor.Blue, openAt: 1),
                });

            viewObject = new GameObject("Board view under test");
            var view = viewObject.AddComponent<BoardView>();
            Initialize(view, ctx);
            var start = BoardState.CreateInitial(ctx);
            view.Rebuild(start);
            return (view, ctx, start);
        }

        /// <summary>Binds <paramref name="view"/> to <paramref name="ctx"/> as the bootstrap does, with the project's Runtime Config.</summary>
        private static void Initialize(BoardView view, LevelContext ctx)
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(RuntimeConfig)}");
            Assert.AreEqual(1, guids.Length, "the project has one Runtime Config");
            var config = AssetDatabase.LoadAssetAtPath<RuntimeConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var machine = config.CreateGeneratorMachine();
            var layout = new BoardLayout(ctx.Width, ctx.Height, config.CellSize, config.FrameThicknessCells, machine.Reach(ctx));
            view.Initialize(config, ctx, layout, new VisibilityLayer(ctx), machine);
        }

        /// <summary>The key block pushed in place into its gate: cleared, its key consumed, the lock and the frozen gate open.</summary>
        private static BoardState ClearTheKeyBlock(LevelContext ctx, BoardState start)
        {
            Assert.IsTrue(new MoveResolver().TryApplyMove(
                ctx, start, new Move(KeyBlock, start.Origins[KeyBlock]), out var cleared, out _), "the push clears it");
            Assert.IsFalse(cleared.Alive[KeyBlock], "the key block is gone");
            return cleared;
        }

        private static Transform Board(BoardView view) => view.transform.Find("Board");

        private static Transform[] FloorAndFrame(BoardView view)
        {
            var board = Board(view);
            return FloorAndFrameNames.Select(name => board.Find(name)).ToArray();
        }

        private static List<string> ChildNames(Transform parent)
        {
            var names = new List<string>(parent.childCount);
            for (var i = 0; i < parent.childCount; i++)
            {
                names.Add(parent.GetChild(i).name);
            }

            return names;
        }

        /// <summary>The first object named <paramref name="name"/> under <paramref name="root"/>, at any depth; null when none is.</summary>
        private static Transform Descendant(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                var found = Descendant(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
