using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Module 33's part of <see cref="MoveResolverTests"/>: the joker entry
    /// points (D9) for the cases Phase 1 left to Phase 5 — a Rocket on a
    /// normal or layered block, refused for a destroyed or unspawned one; a
    /// Broom that takes a frozen match and spares a shuttered one, and clears
    /// one layer per block (D26); a Rocket delivering a key (M8); the time
    /// bonus of only the blocks a joker destroys (M10); and the Broom's chain
    /// reaction through a gate, a shutter, a frozen block and an elevator.
    /// </summary>
    /// <remarks>
    /// The joker cases Phase 1 already covered stay in
    /// <c>MoveResolverTests.cs</c>; these add only what they did not. Grid
    /// convention as elsewhere: y = 0 is the bottom row.
    /// </remarks>
    public partial class MoveResolverTests
    {
        private const int FirstJokerBonus = 4;
        private const int SecondJokerBonus = 9;
        private const int SurvivorJokerBonus = 13;

        // The one count the chain board's gate, shutter and frozen block all
        // wait for: exactly the number of red colours the sweep can reach.
        private const int ChainThreshold = 3;

        // A threshold no clear in its test reaches, so what it guards stays shut.
        private const int UnreachedThreshold = 5;

        // ----- Rocket (TryClearBlock) ----------------------------------------

        [Test]
        public void TryClearBlock_NormalBlock_ClearsItInPlaceAndReportsItsBonus()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(1, 0), timeBonusSeconds: FirstJokerBonus) });
            var state = BoardState.CreateInitial(ctx);

            var cleared = Resolver().TryClearBlock(ctx, state, 0, out var result, out var seconds);

            Assert.IsTrue(cleared);
            Assert.IsFalse(result.Alive[0]);
            Assert.AreEqual(new Coord(1, 0), result.Origins[0], "a Rocket moves nothing");
            Assert.AreEqual(1, result.TotalClearCount);
            Assert.AreEqual(1, result.ClearCountByColor[(int)BlockColor.Red]);
            Assert.AreEqual(FirstJokerBonus, seconds);
        }

        [Test]
        public void TryClearBlock_DestroyedBlock_IsRefusedWithNoResultOrBonus()
        {
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), timeBonusSeconds: FirstJokerBonus),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue })
                });
            var resolver = Resolver();
            resolver.TryClearBlock(ctx, BoardState.CreateInitial(ctx), 0, out var destroyed, out _);

            var cleared = resolver.TryClearBlock(ctx, destroyed, 0, out var result, out var seconds);

            Assert.IsFalse(destroyed.Alive[0]);
            Assert.IsFalse(cleared);
            Assert.IsNull(result);
            Assert.AreEqual(0, seconds);
        }

        [Test]
        public void TryClearBlock_UnspawnedBlock_IsRefusedWithNoResultOrBonus()
        {
            var ctx = GeneratorBehindARedBlock();
            var state = BoardState.CreateInitial(ctx);
            var unspawned = ctx.GeneratorSlot(0, 0);

            var cleared = Resolver().TryClearBlock(ctx, state, unspawned, out var result, out var seconds);

            Assert.IsFalse(state.Alive[unspawned], "the red block holds the generator back");
            Assert.IsFalse(cleared);
            Assert.IsNull(result);
            Assert.AreEqual(0, seconds);
        }

        [Test]
        public void TryClearBlock_LayeredBlock_ExposesTheNextColourAndSurvives()
        {
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(1, 0),
                        colors: new[] { BlockColor.Red, BlockColor.Blue }, timeBonusSeconds: FirstJokerBonus)
                });
            var state = BoardState.CreateInitial(ctx);

            var cleared = Resolver().TryClearBlock(ctx, state, 0, out var result, out var seconds);

            Assert.IsTrue(cleared);
            Assert.IsTrue(result.Alive[0]);
            Assert.AreEqual(1, result.ClearedColors[0]);
            Assert.AreEqual(BlockColor.Blue, result.CurrentColorOf(ctx, 0));
            Assert.AreEqual(1, result.ClearCountByColor[(int)BlockColor.Red], "the removed colour is credited");
            Assert.AreEqual(0, result.ClearCountByColor[(int)BlockColor.Blue]);
            Assert.AreEqual(0, seconds, "the bonus waits for the block's death");
        }

        [Test]
        public void TryClearBlock_DestroysAKeyCarrier_OpensItsLock()
        {
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Blue }, lockId: 1, requiredKeys: 1),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Red }, keyTarget: 1)
                });
            var state = BoardState.CreateInitial(ctx);

            var cleared = Resolver().TryClearBlock(ctx, state, 1, out var result, out _);

            Assert.IsFalse(state.CanMove(ctx, 0));
            Assert.IsTrue(cleared);
            Assert.IsTrue(result.KeyConsumed[1]);
            Assert.IsTrue(result.Unlocked[0]);
            Assert.IsTrue(result.CanMove(ctx, 0));
        }

        // ----- Broom (TrySweepColor) -----------------------------------------

        [Test]
        public void TrySweepColor_FrozenAndShutteredMatches_ClearsTheFrozenOneAndSparesTheShutteredOne()
        {
            var ctx = Ctx(
                3, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), unfreezeAt: UnreachedThreshold),
                    Block(2, new Coord(2, 1))
                },
                shutters: new[] { Shutter(1, new Coord(2, 1), new Coord(2, 1), UnreachedThreshold) });
            var state = BoardState.CreateInitial(ctx);

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Red, out var result, out _);

            Assert.IsTrue(swept);
            Assert.IsFalse(result.Alive[0], "a frozen block is targetable (D11)");
            Assert.IsTrue(result.Alive[1]);
            Assert.AreEqual(0, result.ClearedColors[1], "a block under a closed shutter is not");
            Assert.AreEqual(1, result.TotalClearCount);
        }

        [Test]
        public void TrySweepColor_LayeredBlocks_ClearsOneLayerPerBlock()
        {
            // D26: the first block's red comes back after its blue, and the
            // second hides red beneath blue. Neither is reached by this sweep.
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red, BlockColor.Blue, BlockColor.Red }),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue, BlockColor.Red })
                });
            var state = BoardState.CreateInitial(ctx);

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Red, out var result, out _);

            Assert.IsTrue(swept);
            Assert.AreEqual(1, result.ClearedColors[0]);
            Assert.AreEqual(BlockColor.Blue, result.CurrentColorOf(ctx, 0));
            Assert.AreEqual(0, result.ClearedColors[1]);
            Assert.AreEqual(1, result.TotalClearCount);
        }

        [Test]
        public void TrySweepColor_ABonusBlockThatSurvives_IsLeftOutOfTheSum()
        {
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), timeBonusSeconds: FirstJokerBonus),
                    Block(2, new Coord(1, 0), timeBonusSeconds: SecondJokerBonus),
                    Block(3, new Coord(3, 0),
                        colors: new[] { BlockColor.Red, BlockColor.Blue }, timeBonusSeconds: SurvivorJokerBonus)
                });
            var state = BoardState.CreateInitial(ctx);

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Red, out var result, out var seconds);

            Assert.IsTrue(swept);
            Assert.IsTrue(result.Alive[2]);
            Assert.AreEqual(FirstJokerBonus + SecondJokerBonus, seconds);
        }

        /// <summary>
        /// The extension of <see cref="TrySweepColor_CrossesSeveralThresholdsInOneResolution"/>
        /// the roadmap asks for (Phase 5; MECHANICS, Jokers): one sweep also
        /// empties an elevator, whose next wave arrives in the same resolution.
        /// </summary>
        /// <remarks>
        /// 6x3. Red A at (0,0) and the elevator's first wave, two red 1x1s over
        /// (4,2)-(5,2), are the three reds the sweep reaches. A red block under
        /// a global shutter at (0,2), a blue block frozen at (1,0) and a green
        /// bottom gate at x = 3 all wait for <see cref="ChainThreshold"/>
        /// clears, so the one drain of three clears crosses all three, and no
        /// smaller count would cross any. The elevator is not under the
        /// shutter: a closed shutter keeps its blocks out of the sweep, and an
        /// empty region under one received its wave at level start (D42).
        /// </remarks>
        [Test]
        public void TrySweepColor_CrossesSeveralThresholdsAndBringsAnElevatorsNextWaveInOneResolution()
        {
            var ctx = Ctx(
                6, 3,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }, unfreezeAt: ChainThreshold),
                    Block(3, new Coord(0, 2))
                },
                gates: new[] { Gate(1, BoardEdge.Bottom, 3, 1, BlockColor.Green, openAt: ChainThreshold) },
                shutters: new[] { Shutter(1, new Coord(0, 2), new Coord(0, 2), ChainThreshold) },
                elevators: new[]
                {
                    Elevator(1, new Coord(4, 2), new Coord(5, 2),
                        new[]
                        {
                            Spawned(regionOrigin: new Coord(0, 0)),
                            Spawned(regionOrigin: new Coord(1, 0))
                        },
                        new[]
                        {
                            Spawned(colors: new[] { BlockColor.Blue }, cells: SpawnCellsHorizontal1x2,
                                regionOrigin: new Coord(0, 0))
                        })
                });
            var state = BoardState.CreateInitial(ctx);
            var firstWave = ctx.ElevatorWaveFirstSlot(0, 0);
            var secondWave = ctx.ElevatorWaveFirstSlot(0, 1);

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Red, out var result, out _);

            Assert.IsFalse(state.GateOpen[0]);
            Assert.IsFalse(state.Unfrozen[1]);
            Assert.IsFalse(state.ShutterOpen[0]);
            Assert.AreEqual(1, state.ElevatorWaveIndex[0], "the first wave is on the board at level start");
            Assert.IsTrue(swept);
            Assert.AreEqual(ChainThreshold, result.TotalClearCount);
            Assert.IsFalse(result.Alive[0]);
            Assert.IsFalse(result.Alive[firstWave]);
            Assert.IsFalse(result.Alive[firstWave + 1]);
            Assert.IsTrue(result.GateOpen[0], "gate threshold crossed");
            Assert.IsTrue(result.Unfrozen[1], "frozen block threshold crossed");
            Assert.IsTrue(result.ShutterOpen[0], "shutter threshold crossed");
            Assert.AreEqual(2, result.ElevatorWaveIndex[0]);
            Assert.IsTrue(result.Alive[secondWave], "the emptied region took its next wave");
            Assert.AreEqual(new Coord(4, 2), result.Origins[secondWave]);
            Assert.IsTrue(result.ElevatorWaveActive[0]);
            Assert.IsTrue(result.Alive[2], "the sweep chose its targets before the shutter opened");
            Assert.AreEqual(0, result.ClearedColors[2]);
            Assert.IsTrue(result.CanBeTargeted(ctx, 2), "and the opened shutter exposes it");
        }
    }
}
