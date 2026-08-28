using System;
using System.Text.RegularExpressions;
using GateRush.Core;
using GateRush.Serialization;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="LevelSerializer"/>: that a <see cref="LevelContext"/>
    /// survives a JSON round trip field for field, that the shapes
    /// <c>JsonUtility</c> forces on the DTO layer (sentinels, wrapped waves,
    /// string enums) behave, that structural faults are reported here with the
    /// source named, and that semantic faults are left to <c>Core</c>.
    /// </summary>
    public class LevelSerializerTests
    {
        private static string Canonical(string json) => Regex.Replace(json, @"\s+", string.Empty);

        // -- A corpus level exercising every DTO field ---------------------

        /// <summary>
        /// One level that carries, between its parts, every field of every DTO:
        /// a nullable that is set (block 1's unfreeze) and ones that are not, a
        /// layered block, two axis-restricted blocks, two lock/key pairs — one
        /// key of each <see cref="KeyEffect"/> — a time bonus, a colour-bound
        /// shutter and a global one, a generator queue, an elevator with two
        /// waves of unequal length, and a static wall.
        /// </summary>
        private static LevelContext CorpusLevel()
        {
            var blocks = new[]
            {
                Block(1, new Coord(2, 1),
                    cells: new[] { new Coord(0, 0), new Coord(0, 1) },
                    colors: new[] { BlockColor.Blue, BlockColor.Yellow },
                    axis: MovementAxis.VerticalOnly,
                    unfreezeAt: 3,
                    timeBonusSeconds: 5),
                Block(2, new Coord(4, 1), colors: new[] { BlockColor.Green },
                    lockId: 7, requiredKeys: 1),
                Block(3, new Coord(5, 1), colors: new[] { BlockColor.Red },
                    keyTarget: 7, keyEffect: KeyEffect.ClearOuterColor),
                Block(4, new Coord(3, 0), colors: new[] { BlockColor.Orange },
                    lockId: 8, requiredKeys: 1),
                Block(5, new Coord(2, 0), colors: new[] { BlockColor.Purple },
                    keyTarget: 8, keyEffect: KeyEffect.UnlockMovement),
            };

            var gates = new[]
            {
                Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Blue),
                Gate(2, BoardEdge.Top, 0, 2, BlockColor.Green, openAt: 4),
            };

            var shutters = new[]
            {
                Shutter(1, new Coord(3, 3), new Coord(4, 4), threshold: 2, requiredColor: BlockColor.Yellow),
                Shutter(2, new Coord(0, 3), new Coord(1, 4), threshold: 5),
            };

            var generators = new[]
            {
                Spawner(1, BoardEdge.Left, 0,
                    Spawned(colors: new[] { BlockColor.Cyan }, axis: MovementAxis.HorizontalOnly)),
            };

            var elevators = new[]
            {
                Elevator(1, new Coord(0, 0), new Coord(1, 0),
                    new[] { Spawned(colors: new[] { BlockColor.Pink }), Spawned(colors: new[] { BlockColor.Green }) },
                    new[] { Spawned(colors: new[] { BlockColor.Red }, timeBonusSeconds: 4) }),
            };

            return Ctx(6, 6, blocks: blocks, gates: gates, shutters: shutters,
                generators: generators, elevators: elevators,
                staticWalls: new[] { new Coord(0, 5) });
        }

        // -- Round trip ---------------------------------------------------

        [Test]
        public void RoundTrip_CorpusLevel_ReserializesIdentically()
        {
            var once = LevelSerializer.ToJson(CorpusLevel());

            var twice = LevelSerializer.ToJson(LevelSerializer.FromJson(once));

            Assert.AreEqual(once, twice);
        }

        [Test]
        public void RoundTrip_EmptyLevel_ReserializesIdentically()
        {
            var once = LevelSerializer.ToJson(Ctx(3, 3));

            var twice = LevelSerializer.ToJson(LevelSerializer.FromJson(once));

            Assert.AreEqual(once, twice);
        }

        [Test]
        public void FromJson_HandWrittenLevelWithEveryField_ProducesCorrectContext()
        {
            var ctx = LevelSerializer.FromJson(FullyPopulatedJson, "full.json");

            Assert.AreEqual(42, ctx.LevelId);
            Assert.AreEqual(6, ctx.Width);
            Assert.AreEqual(6, ctx.Height);
            Assert.AreEqual(90, ctx.SuggestedTimeBudgetSeconds);
            Assert.AreEqual(250, ctx.GoldReward);

            Assert.AreEqual(1, ctx.StaticWalls.Count);
            Assert.AreEqual(new Coord(0, 5), ctx.StaticWalls[0]);

            var layered = ctx.Blocks[0];
            Assert.AreEqual(1, layered.Id);
            CollectionAssert.AreEqual(new[] { new Coord(0, 0), new Coord(0, 1) }, layered.Cells);
            CollectionAssert.AreEqual(new[] { BlockColor.Blue, BlockColor.Yellow }, layered.ColorStack);
            Assert.AreEqual(new Coord(2, 1), layered.StartOrigin);
            Assert.AreEqual(MovementAxis.VerticalOnly, layered.Axis);
            Assert.AreEqual(3, layered.UnfreezeAtClearCount);
            Assert.IsNull(layered.LockId);
            Assert.IsNull(layered.KeyTargetLockId);
            Assert.AreEqual(KeyEffect.UnlockMovement, layered.KeyEffect);
            Assert.AreEqual(5, layered.TimeBonusSeconds);

            var locked = ctx.Blocks[1];
            Assert.AreEqual(7, locked.LockId);
            Assert.AreEqual(1, locked.RequiredKeyCount);
            Assert.IsNull(locked.UnfreezeAtClearCount);

            var key = ctx.Blocks[2];
            Assert.AreEqual(7, key.KeyTargetLockId);
            Assert.AreEqual(KeyEffect.ClearOuterColor, key.KeyEffect);

            var openGate = ctx.Gates[0];
            Assert.AreEqual(BoardEdge.Bottom, openGate.Edge);
            Assert.AreEqual(2, openGate.Offset);
            Assert.AreEqual(1, openGate.Width);
            Assert.AreEqual(BlockColor.Blue, openGate.Color);
            Assert.IsNull(openGate.OpenAtClearCount);

            var countGate = ctx.Gates[1];
            Assert.AreEqual(BoardEdge.Top, countGate.Edge);
            Assert.AreEqual(2, countGate.Width);
            Assert.AreEqual(BlockColor.Green, countGate.Color);
            Assert.AreEqual(4, countGate.OpenAtClearCount);

            var colourShutter = ctx.Shutters[0];
            Assert.AreEqual(new Coord(3, 3), colourShutter.Min);
            Assert.AreEqual(new Coord(4, 4), colourShutter.Max);
            Assert.AreEqual(2, colourShutter.Threshold);
            Assert.AreEqual(BlockColor.Yellow, colourShutter.RequiredColor);

            Assert.IsNull(ctx.Shutters[1].RequiredColor);

            var generator = ctx.Generators[0];
            Assert.AreEqual(BoardEdge.Left, generator.Edge);
            Assert.AreEqual(1, generator.Queue.Count);
            CollectionAssert.AreEqual(new[] { BlockColor.Cyan }, generator.Queue[0].ColorStack);
            Assert.AreEqual(MovementAxis.HorizontalOnly, generator.Queue[0].Axis);

            var elevator = ctx.Elevators[0];
            Assert.AreEqual(new Coord(0, 0), elevator.Min);
            Assert.AreEqual(new Coord(1, 0), elevator.Max);
            Assert.AreEqual(2, elevator.Waves.Count);
            Assert.AreEqual(2, elevator.Waves[0].Count);
            Assert.AreEqual(1, elevator.Waves[1].Count);
        }

        // -- JsonUtility's limitations, one test each --------------------

        [Test]
        public void RoundTrip_NullNullable_WritesMinusOneAndReturnsNull()
        {
            var json = LevelSerializer.ToJson(Ctx(3, 3, blocks: new[] { Block(1, new Coord(0, 0), unfreezeAt: null) }));

            StringAssert.Contains("\"unfreezeAtClearCount\":-1", Canonical(json));
            Assert.IsNull(LevelSerializer.FromJson(json).Blocks[0].UnfreezeAtClearCount);
        }

        [Test]
        public void RoundTrip_SetNullable_KeepsItsValue()
        {
            var json = LevelSerializer.ToJson(Ctx(3, 3, blocks: new[] { Block(1, new Coord(0, 0), unfreezeAt: 4) }));

            StringAssert.Contains("\"unfreezeAtClearCount\":4", Canonical(json));
            Assert.AreEqual(4, LevelSerializer.FromJson(json).Blocks[0].UnfreezeAtClearCount);
        }

        [Test]
        public void RoundTrip_ElevatorWithTwoUnequalWaves_KeepsBothIntact()
        {
            var elevator = Elevator(1, new Coord(0, 0), new Coord(1, 0),
                new[] { Spawned(colors: new[] { BlockColor.Pink }), Spawned(colors: new[] { BlockColor.Green }) },
                new[] { Spawned(colors: new[] { BlockColor.Red }) });
            var ctx = Ctx(3, 3, elevators: new[] { elevator });

            var restored = LevelSerializer.FromJson(LevelSerializer.ToJson(ctx)).Elevators[0];

            Assert.AreEqual(2, restored.Waves.Count);
            Assert.AreEqual(2, restored.Waves[0].Count);
            Assert.AreEqual(1, restored.Waves[1].Count);
            Assert.AreEqual(BlockColor.Green, restored.Waves[0][1].ColorStack[0]);
        }

        [Test]
        public void RoundTrip_EmptyCollection_StaysEmptyNotNull()
        {
            var restored = LevelSerializer.FromJson(LevelSerializer.ToJson(Ctx(3, 3)));

            Assert.IsNotNull(restored.Gates);
            Assert.AreEqual(0, restored.Gates.Count);
        }

        // -- Enums as strings ------------------------------------------

        [Test]
        public void ToJson_Colours_AppearAsNamesNotIntegers()
        {
            var json = Canonical(LevelSerializer.ToJson(
                Ctx(3, 3, blocks: new[] { Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red } ) })));

            StringAssert.Contains("\"colorStack\":[\"Red\"]", json);
        }

        [Test]
        public void FromJson_UnrecognisedColourName_ThrowsNamingSourceAndField()
        {
            var ex = Assert.Throws<LevelSerializationException>(
                () => LevelSerializer.FromJson(ColourNamedJson("Mauve"), "level-7.json"));

            StringAssert.Contains("level-7.json", ex.Message);
            StringAssert.Contains("colorStack", ex.Message);
            StringAssert.Contains("Mauve", ex.Message);
        }

        [Test]
        public void FromJson_NumericEnumText_IsRejectedLikeAnyOtherBadName()
        {
            Assert.Throws<LevelSerializationException>(
                () => LevelSerializer.FromJson(ColourNamedJson("3"), "numbers.json"));
        }

        [Test]
        public void RoundTrip_EveryBlockColor_Survives()
        {
            foreach (BlockColor colour in Enum.GetValues(typeof(BlockColor)))
            {
                var ctx = Ctx(1, 1, blocks: new[] { Block(1, new Coord(0, 0), colors: new[] { colour }) });

                var restored = LevelSerializer.FromJson(LevelSerializer.ToJson(ctx));

                Assert.AreEqual(colour, restored.Blocks[0].ColorStack[0]);
            }
        }

        [Test]
        public void RoundTrip_EveryMovementAxis_Survives()
        {
            foreach (MovementAxis axis in Enum.GetValues(typeof(MovementAxis)))
            {
                var ctx = Ctx(1, 1, blocks: new[] { Block(1, new Coord(0, 0), axis: axis) });

                var restored = LevelSerializer.FromJson(LevelSerializer.ToJson(ctx));

                Assert.AreEqual(axis, restored.Blocks[0].Axis);
            }
        }

        [Test]
        public void RoundTrip_EveryBoardEdge_Survives()
        {
            foreach (BoardEdge edge in Enum.GetValues(typeof(BoardEdge)))
            {
                var ctx = Ctx(1, 1, gates: new[] { Gate(1, edge, 0, 1, BlockColor.Red) });

                var restored = LevelSerializer.FromJson(LevelSerializer.ToJson(ctx));

                Assert.AreEqual(edge, restored.Gates[0].Edge);
            }
        }

        [Test]
        public void RoundTrip_EveryKeyEffect_Survives()
        {
            foreach (KeyEffect effect in Enum.GetValues(typeof(KeyEffect)))
            {
                var ctx = Ctx(1, 1, blocks: new[] { Block(1, new Coord(0, 0), keyEffect: effect) });

                var restored = LevelSerializer.FromJson(LevelSerializer.ToJson(ctx));

                Assert.AreEqual(effect, restored.Blocks[0].KeyEffect);
            }
        }

        // -- Structural errors ----------------------------------------

        [Test]
        public void FromJson_UnsupportedFormatVersion_IsRejectedBeforeConversion()
        {
            // The colour name is also invalid; the version check must fire first.
            var json = @"{ ""formatVersion"": 999, ""levelId"": 1, ""width"": 1, ""height"": 1,
                ""blocks"": [ { ""id"": 1, ""cells"": [ { ""x"": 0, ""y"": 0 } ],
                ""colorStack"": [ ""NotAColour"" ], ""axis"": ""Free"", ""keyEffect"": ""UnlockMovement"",
                ""unfreezeAtClearCount"": -1, ""lockId"": -1, ""keyTargetLockId"": -1 } ],
                ""staticWalls"": [], ""gates"": [], ""shutters"": [], ""generators"": [], ""elevators"": [] }";

            var ex = Assert.Throws<LevelSerializationException>(() => LevelSerializer.FromJson(json, "v999.json"));

            StringAssert.Contains("v999.json", ex.Message);
            StringAssert.Contains("999", ex.Message);
            StringAssert.Contains("version", ex.Message);
        }

        [Test]
        public void FromJson_RequiredArrayAbsent_IsReportedAsNamedErrorNotNullReference()
        {
            var json = @"{ ""formatVersion"": 1, ""levelId"": 1, ""width"": 3, ""height"": 3,
                ""blocks"": [ { ""id"": 1, ""colorStack"": [ ""Red"" ], ""axis"": ""Free"",
                ""keyEffect"": ""UnlockMovement"", ""unfreezeAtClearCount"": -1, ""lockId"": -1,
                ""keyTargetLockId"": -1 } ] }";

            var ex = Assert.Throws<LevelSerializationException>(() => LevelSerializer.FromJson(json, "no-cells.json"));

            StringAssert.Contains("no-cells.json", ex.Message);
            StringAssert.Contains("block 1", ex.Message);
            StringAssert.Contains("cells", ex.Message);
        }

        [Test]
        public void FromJson_NegativeSentinelOtherThanMinusOne_IsReported()
        {
            var json = @"{ ""formatVersion"": 1, ""levelId"": 1, ""width"": 3, ""height"": 3,
                ""blocks"": [ { ""id"": 1, ""cells"": [ { ""x"": 0, ""y"": 0 } ], ""colorStack"": [ ""Red"" ],
                ""axis"": ""Free"", ""keyEffect"": ""UnlockMovement"", ""unfreezeAtClearCount"": -1,
                ""lockId"": -5, ""keyTargetLockId"": -1 } ] }";

            var ex = Assert.Throws<LevelSerializationException>(() => LevelSerializer.FromJson(json, "bad-sentinel.json"));

            StringAssert.Contains("bad-sentinel.json", ex.Message);
            StringAssert.Contains("lockId", ex.Message);
            StringAssert.Contains("-5", ex.Message);
        }

        [Test]
        public void FromJson_MalformedText_IsReportedAsInvalidJson()
        {
            var ex = Assert.Throws<LevelSerializationException>(
                () => LevelSerializer.FromJson("this is not json at all", "broken.json"));

            StringAssert.Contains("broken.json", ex.Message);
            StringAssert.Contains("JSON", ex.Message);
        }

        [Test]
        public void FromJson_EmptyString_IsReported()
        {
            Assert.Throws<LevelSerializationException>(() => LevelSerializer.FromJson("   ", "empty.json"));
        }

        // -- No duplicated validation --------------------------------

        [Test]
        public void FromJson_StructurallyValidButSemanticallyInvalid_IsRejectedByCoreWithCoresMessage()
        {
            var json = @"{ ""formatVersion"": 1, ""levelId"": 1, ""width"": 5, ""height"": 5,
                ""blocks"": [ { ""id"": 1, ""cells"": [ { ""x"": 0, ""y"": 0 } ], ""colorStack"": [ ""Red"" ],
                ""startOrigin"": { ""x"": 99, ""y"": 99 }, ""axis"": ""Free"", ""keyEffect"": ""UnlockMovement"",
                ""unfreezeAtClearCount"": -1, ""lockId"": -1, ""keyTargetLockId"": -1 } ],
                ""staticWalls"": [], ""gates"": [], ""shutters"": [], ""generators"": [], ""elevators"": [] }";

            // Core's ArgumentException, not this layer's LevelSerializationException:
            // a "helpful" duplicate grid-bounds check here would change the type and fail this.
            var ex = Assert.Throws<ArgumentException>(() => LevelSerializer.FromJson(json, "outside.json"));

            StringAssert.Contains("outside the 5x5 grid", ex.Message);
        }

        // -- Fixtures ------------------------------------------------

        private static string ColourNamedJson(string colourName) =>
            $@"{{ ""formatVersion"": 1, ""levelId"": 1, ""width"": 1, ""height"": 1,
                ""blocks"": [ {{ ""id"": 1, ""cells"": [ {{ ""x"": 0, ""y"": 0 }} ],
                ""colorStack"": [ ""{colourName}"" ], ""axis"": ""Free"", ""keyEffect"": ""UnlockMovement"",
                ""unfreezeAtClearCount"": -1, ""lockId"": -1, ""keyTargetLockId"": -1 }} ],
                ""staticWalls"": [], ""gates"": [], ""shutters"": [], ""generators"": [], ""elevators"": [] }}";

        private const string FullyPopulatedJson = @"{
  ""formatVersion"": 1,
  ""levelId"": 42,
  ""width"": 6,
  ""height"": 6,
  ""staticWalls"": [ { ""x"": 0, ""y"": 5 } ],
  ""blocks"": [
    {
      ""id"": 1,
      ""cells"": [ { ""x"": 0, ""y"": 0 }, { ""x"": 0, ""y"": 1 } ],
      ""colorStack"": [ ""Blue"", ""Yellow"" ],
      ""startOrigin"": { ""x"": 2, ""y"": 1 },
      ""axis"": ""VerticalOnly"",
      ""unfreezeAtClearCount"": 3,
      ""lockId"": -1,
      ""requiredKeyCount"": 0,
      ""keyTargetLockId"": -1,
      ""keyEffect"": ""UnlockMovement"",
      ""timeBonusSeconds"": 5
    },
    {
      ""id"": 2,
      ""cells"": [ { ""x"": 0, ""y"": 0 } ],
      ""colorStack"": [ ""Green"" ],
      ""startOrigin"": { ""x"": 4, ""y"": 1 },
      ""axis"": ""Free"",
      ""unfreezeAtClearCount"": -1,
      ""lockId"": 7,
      ""requiredKeyCount"": 1,
      ""keyTargetLockId"": -1,
      ""keyEffect"": ""UnlockMovement"",
      ""timeBonusSeconds"": 0
    },
    {
      ""id"": 3,
      ""cells"": [ { ""x"": 0, ""y"": 0 } ],
      ""colorStack"": [ ""Red"" ],
      ""startOrigin"": { ""x"": 5, ""y"": 1 },
      ""axis"": ""Free"",
      ""unfreezeAtClearCount"": -1,
      ""lockId"": -1,
      ""requiredKeyCount"": 0,
      ""keyTargetLockId"": 7,
      ""keyEffect"": ""ClearOuterColor"",
      ""timeBonusSeconds"": 0
    }
  ],
  ""gates"": [
    { ""id"": 1, ""edge"": ""Bottom"", ""offset"": 2, ""width"": 1, ""color"": ""Blue"", ""openAtClearCount"": -1 },
    { ""id"": 2, ""edge"": ""Top"", ""offset"": 0, ""width"": 2, ""color"": ""Green"", ""openAtClearCount"": 4 }
  ],
  ""shutters"": [
    { ""id"": 1, ""min"": { ""x"": 3, ""y"": 3 }, ""max"": { ""x"": 4, ""y"": 4 }, ""threshold"": 2, ""requiredColor"": ""Yellow"" },
    { ""id"": 2, ""min"": { ""x"": 0, ""y"": 3 }, ""max"": { ""x"": 1, ""y"": 4 }, ""threshold"": 5, ""requiredColor"": """" }
  ],
  ""generators"": [
    {
      ""id"": 1,
      ""edge"": ""Left"",
      ""offset"": 0,
      ""queue"": [
        {
          ""cells"": [ { ""x"": 0, ""y"": 0 } ],
          ""colorStack"": [ ""Cyan"" ],
          ""axis"": ""HorizontalOnly"",
          ""unfreezeAtClearCount"": -1,
          ""lockId"": -1,
          ""requiredKeyCount"": 0,
          ""keyTargetLockId"": -1,
          ""keyEffect"": ""UnlockMovement"",
          ""timeBonusSeconds"": 0
        }
      ]
    }
  ],
  ""elevators"": [
    {
      ""id"": 1,
      ""min"": { ""x"": 0, ""y"": 0 },
      ""max"": { ""x"": 1, ""y"": 0 },
      ""waves"": [
        {
          ""blocks"": [
            { ""cells"": [ { ""x"": 0, ""y"": 0 } ], ""colorStack"": [ ""Pink"" ], ""axis"": ""Free"",
              ""unfreezeAtClearCount"": -1, ""lockId"": -1, ""requiredKeyCount"": 0, ""keyTargetLockId"": -1,
              ""keyEffect"": ""UnlockMovement"", ""timeBonusSeconds"": 0 },
            { ""cells"": [ { ""x"": 0, ""y"": 0 } ], ""colorStack"": [ ""Green"" ], ""axis"": ""Free"",
              ""unfreezeAtClearCount"": -1, ""lockId"": -1, ""requiredKeyCount"": 0, ""keyTargetLockId"": -1,
              ""keyEffect"": ""UnlockMovement"", ""timeBonusSeconds"": 0 }
          ]
        },
        {
          ""blocks"": [
            { ""cells"": [ { ""x"": 0, ""y"": 0 } ], ""colorStack"": [ ""Red"" ], ""axis"": ""Free"",
              ""unfreezeAtClearCount"": -1, ""lockId"": -1, ""requiredKeyCount"": 0, ""keyTargetLockId"": -1,
              ""keyEffect"": ""UnlockMovement"", ""timeBonusSeconds"": 4 }
          ]
        }
      ]
    }
  ],
  ""suggestedTimeBudgetSeconds"": 90,
  ""goldReward"": 250
}";
    }
}
