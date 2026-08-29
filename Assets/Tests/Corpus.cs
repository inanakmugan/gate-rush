using GateRush.Core;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// The one "every field of every type" level, shared by the tests that guard
    /// against a schema change silently dropping a field: the serialization
    /// round trip (<c>LevelSerializerTests</c>) and the draft round trip
    /// (<c>LevelDraftTests</c>). A field added to <c>LevelDto</c>, a <c>Core</c>
    /// definition, or a <c>LevelDraft</c> type must be exercised here or both
    /// round trips keep passing while losing data.
    /// </summary>
    internal static class Corpus
    {
        /// <summary>
        /// Carries, between its parts: a nullable that is set (block 1's
        /// unfreeze) and ones that are not, a layered block, axis-restricted
        /// blocks, two lock/key pairs — one key of each <see cref="KeyEffect"/> —
        /// a time bonus, a colour-bound shutter and a global one, a generator
        /// queue, an elevator with two waves of unequal block count that both
        /// tile the region (so each wave block carries a RegionOrigin), and a
        /// static wall.
        /// </summary>
        internal static LevelContext EveryFieldLevel()
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
                // 2x1 region. Wave 1 tiles it with two 1x1 blocks; wave 2 with a
                // single 2x1 block — the waves differ in block count, never in
                // the cells they cover.
                Elevator(1, new Coord(0, 0), new Coord(1, 0),
                    new[]
                    {
                        Spawned(colors: new[] { BlockColor.Pink }, regionOrigin: new Coord(0, 0)),
                        Spawned(colors: new[] { BlockColor.Green }, regionOrigin: new Coord(1, 0)),
                    },
                    new[]
                    {
                        Spawned(
                            colors: new[] { BlockColor.Red },
                            cells: new[] { new Coord(0, 0), new Coord(1, 0) },
                            timeBonusSeconds: 4,
                            regionOrigin: new Coord(0, 0)),
                    }),
            };

            return Ctx(6, 6, blocks: blocks, gates: gates, shutters: shutters,
                generators: generators, elevators: elevators,
                staticWalls: new[] { new Coord(0, 5) });
        }
    }
}
