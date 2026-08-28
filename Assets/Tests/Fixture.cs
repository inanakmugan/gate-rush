using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Tests
{
    /// <summary>
    /// Builds <see cref="LevelContext"/> and its definition types with the fields
    /// every test leaves at one value pre-filled: level id 1, a 60-second
    /// suggested time budget, a gold reward of 100, an unlock-movement key
    /// effect, no time bonus, a single red 1x1 footprint, and empty collections
    /// for the spawner and obstacle lists a given test does not exercise. A test
    /// then names only the parameters it is actually about.
    /// </summary>
    /// <remarks>
    /// The fixture's job ends at the immutable definition layer. Tests call
    /// <see cref="BoardState.CreateInitial(LevelContext)"/> themselves so the
    /// line that produces mutable state stays visible at the use site.
    /// </remarks>
    internal static class Fixture
    {
        private static readonly Coord[] Cell1x1 = { new Coord(0, 0) };

        /// <summary>
        /// A block that defaults to a single red 1x1 cell with free movement and
        /// no restrictions. Every optional restriction is a named parameter.
        /// </summary>
        internal static BlockDefinition Block(
            int id,
            Coord start,
            IReadOnlyList<Coord> cells = null,
            IReadOnlyList<BlockColor> colors = null,
            MovementAxis axis = MovementAxis.Free,
            int? unfreezeAt = null,
            int? lockId = null,
            int requiredKeys = 0,
            int? keyTarget = null,
            KeyEffect keyEffect = KeyEffect.UnlockMovement,
            int timeBonusSeconds = 0)
        {
            return new BlockDefinition(
                id: id,
                cells: cells ?? Cell1x1,
                colorStack: colors ?? new[] { BlockColor.Red },
                startOrigin: start,
                axis: axis,
                unfreezeAtClearCount: unfreezeAt,
                lockId: lockId,
                requiredKeyCount: requiredKeys,
                keyTargetLockId: keyTarget,
                keyEffect: keyEffect,
                timeBonusSeconds: timeBonusSeconds);
        }

        /// <summary>
        /// A gate on <paramref name="edge"/>, open from the start unless
        /// <paramref name="openAt"/> gives it a clear-count threshold.
        /// </summary>
        internal static GateDefinition Gate(
            int id, BoardEdge edge, int offset, int width, BlockColor color, int? openAt = null)
        {
            return new GateDefinition(id, edge, offset, width, color, openAt);
        }

        /// <summary>
        /// A rectangular shutter covering <paramref name="min"/>..<paramref name="max"/>
        /// inclusive, opening once its threshold is met.
        /// </summary>
        internal static ShutterDefinition Shutter(
            int id, Coord min, Coord max, int threshold = 1, BlockColor? requiredColor = null)
        {
            return new ShutterDefinition(id, min, max, threshold, requiredColor);
        }

        /// <summary>
        /// A generator on <paramref name="edge"/> whose ordered output is
        /// <paramref name="queue"/>. Named <c>Spawner</c> rather than
        /// <c>Generator</c> so it never collides with a test's local
        /// <c>Generator()</c> factory under <c>using static</c>.
        /// </summary>
        /// <remarks>
        /// No caller yet: generators cannot appear in a level until phase 1.13.
        /// Present now so that phase's first generator fixture has one obvious
        /// place to be built, alongside <see cref="Spawned"/> and the rest.
        /// </remarks>
        internal static GeneratorDefinition Spawner(
            int id, BoardEdge edge, int offset, params SpawnedBlock[] queue)
        {
            return new GeneratorDefinition(id, edge, offset, queue);
        }

        /// <summary>
        /// An elevator over <paramref name="min"/>..<paramref name="max"/>
        /// delivering <paramref name="waves"/> in order. Passing no waves leaves
        /// the wave list null, matching a hand-written <c>waves: null</c>.
        /// </summary>
        internal static ElevatorDefinition Elevator(
            int id, Coord min, Coord max, params IReadOnlyList<SpawnedBlock>[] waves)
        {
            return new ElevatorDefinition(id, min, max, waves.Length == 0 ? null : waves);
        }

        /// <summary>
        /// A spawned block with the same single-red-1x1 defaults as
        /// <see cref="Block"/>; its position derives from where it spawns from,
        /// so it carries none.
        /// </summary>
        internal static SpawnedBlock Spawned(
            IReadOnlyList<BlockColor> colors = null,
            IReadOnlyList<Coord> cells = null,
            MovementAxis axis = MovementAxis.Free,
            int? unfreezeAt = null,
            int? lockId = null,
            int requiredKeys = 0,
            int? keyTarget = null,
            KeyEffect keyEffect = KeyEffect.UnlockMovement,
            int timeBonusSeconds = 0)
        {
            return new SpawnedBlock(
                cells: cells ?? Cell1x1,
                colorStack: colors ?? new[] { BlockColor.Red },
                axis: axis,
                unfreezeAtClearCount: unfreezeAt,
                lockId: lockId,
                requiredKeyCount: requiredKeys,
                keyTargetLockId: keyTarget,
                keyEffect: keyEffect,
                timeBonusSeconds: timeBonusSeconds);
        }

        /// <summary>
        /// A <paramref name="width"/> x <paramref name="height"/> level. Every
        /// collection a test does not pass is empty;
        /// <paramref name="levelId"/>, <paramref name="timeBudgetSeconds"/> and
        /// <paramref name="goldReward"/> carry fixed defaults no test varies.
        /// </summary>
        internal static LevelContext Ctx(
            int width,
            int height,
            IReadOnlyList<BlockDefinition> blocks = null,
            IReadOnlyList<GateDefinition> gates = null,
            IReadOnlyList<ShutterDefinition> shutters = null,
            IReadOnlyList<GeneratorDefinition> generators = null,
            IReadOnlyList<ElevatorDefinition> elevators = null,
            IReadOnlyList<Coord> staticWalls = null,
            int levelId = 1,
            int timeBudgetSeconds = 60,
            int goldReward = 100)
        {
            return new LevelContext(
                levelId: levelId,
                width: width,
                height: height,
                staticWalls: staticWalls ?? Array.Empty<Coord>(),
                blocks: blocks ?? Array.Empty<BlockDefinition>(),
                gates: gates ?? Array.Empty<GateDefinition>(),
                shutters: shutters ?? Array.Empty<ShutterDefinition>(),
                generators: generators ?? Array.Empty<GeneratorDefinition>(),
                elevators: elevators ?? Array.Empty<ElevatorDefinition>(),
                suggestedTimeBudgetSeconds: timeBudgetSeconds,
                goldReward: goldReward);
        }
    }
}
