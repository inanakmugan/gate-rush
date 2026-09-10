using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="SelectionKey"/> (docs/Modules/09a, Session C,
    /// follow-up 1): resolving a captured selection against a draft rebuilt via
    /// <c>ToDto</c> -&gt; <c>FromDto</c>, the same round trip undo/redo performs.
    /// One case per selectable type, plus the not-found cases.
    /// </summary>
    public class SelectionKeyTests
    {
        private static LevelDraft Draft()
        {
            var draft = LevelDraft.NewEmpty(6, 6);

            draft.Blocks.Add(new BlockDraft { Id = 1, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red } });
            draft.Gates.Add(new GateDraft { Id = 2, Edge = BoardEdge.Top, Width = 1, Color = BlockColor.Red });
            draft.Shutters.Add(new ShutterDraft { Id = 3, Min = new Coord(1, 1), Max = new Coord(1, 1) });
            draft.Generators.Add(new GeneratorDraft { Id = 4, Edge = BoardEdge.Bottom });
            draft.Elevators.Add(new ElevatorDraft { Id = 5, Min = new Coord(2, 2), Max = new Coord(3, 3) });
            draft.Elevators[0].Waves.Add(new WaveDraft());
            draft.Elevators[0].Waves[0].Blocks.Add(new SpawnedBlockDraft { Cells = { new Coord(0, 0) }, RegionOrigin = new Coord(2, 2) });
            draft.Elevators[0].Waves[0].Blocks.Add(new SpawnedBlockDraft { Cells = { new Coord(0, 0) }, RegionOrigin = new Coord(3, 2) });

            return draft;
        }

        private static LevelDraft Rebuild(LevelDraft draft) => LevelDraft.FromDto(draft.ToDto());

        [Test]
        public void Capture_Resolve_Block_FindsTheSameIdInARebuiltDraft()
        {
            var draft = Draft();

            var key = SelectionKey.Capture(draft.Blocks[0], scopeWave: null);
            var rebuilt = Rebuild(draft);
            var resolved = key.Resolve(rebuilt, scopeWave: null);

            Assert.AreSame(rebuilt.Blocks[0], resolved);
        }

        [Test]
        public void Capture_Resolve_Gate_FindsTheSameIdInARebuiltDraft()
        {
            var draft = Draft();

            var key = SelectionKey.Capture(draft.Gates[0], scopeWave: null);
            var rebuilt = Rebuild(draft);
            var resolved = key.Resolve(rebuilt, scopeWave: null);

            Assert.AreSame(rebuilt.Gates[0], resolved);
        }

        [Test]
        public void Capture_Resolve_Shutter_FindsTheSameIdInARebuiltDraft()
        {
            var draft = Draft();

            var key = SelectionKey.Capture(draft.Shutters[0], scopeWave: null);
            var rebuilt = Rebuild(draft);
            var resolved = key.Resolve(rebuilt, scopeWave: null);

            Assert.AreSame(rebuilt.Shutters[0], resolved);
        }

        [Test]
        public void Capture_Resolve_Generator_FindsTheSameIdInARebuiltDraft()
        {
            var draft = Draft();

            var key = SelectionKey.Capture(draft.Generators[0], scopeWave: null);
            var rebuilt = Rebuild(draft);
            var resolved = key.Resolve(rebuilt, scopeWave: null);

            Assert.AreSame(rebuilt.Generators[0], resolved);
        }

        [Test]
        public void Capture_Resolve_Elevator_FindsTheSameIdInARebuiltDraft()
        {
            var draft = Draft();

            var key = SelectionKey.Capture(draft.Elevators[0], scopeWave: null);
            var rebuilt = Rebuild(draft);
            var resolved = key.Resolve(rebuilt, scopeWave: null);

            Assert.AreSame(rebuilt.Elevators[0], resolved);
        }

        [Test]
        public void Capture_Resolve_WaveBlock_FindsTheSameIndexInTheRebuiltWave()
        {
            var draft = Draft();
            var scopeWave = draft.Elevators[0].Waves[0];

            var key = SelectionKey.Capture(scopeWave.Blocks[1], scopeWave);
            var rebuilt = Rebuild(draft);
            var rebuiltWave = rebuilt.Elevators[0].Waves[0];
            var resolved = key.Resolve(rebuilt, rebuiltWave);

            Assert.AreSame(rebuiltWave.Blocks[1], resolved);
        }

        [Test]
        public void Resolve_BlockDeletedFromTheRebuiltDraft_ReturnsNull()
        {
            var draft = Draft();
            var key = SelectionKey.Capture(draft.Blocks[0], scopeWave: null);

            var rebuilt = Rebuild(draft);
            rebuilt.Blocks.RemoveAt(0);
            var resolved = key.Resolve(rebuilt, scopeWave: null);

            Assert.IsNull(resolved);
        }

        [Test]
        public void Resolve_WaveBlockIndexNoLongerInRangeAfterRebuild_ReturnsNull()
        {
            var draft = Draft();
            var scopeWave = draft.Elevators[0].Waves[0];
            var key = SelectionKey.Capture(scopeWave.Blocks[1], scopeWave);

            var rebuilt = Rebuild(draft);
            var rebuiltWave = rebuilt.Elevators[0].Waves[0];
            rebuiltWave.Blocks.RemoveAt(1);
            var resolved = key.Resolve(rebuilt, rebuiltWave);

            Assert.IsNull(resolved);
        }

        [Test]
        public void Resolve_WaveBlockSameIndexAndOriginButDifferentCellsAfterRebuild_ReturnsNull()
        {
            // Wave [A, B, C]; A is deleted, C shifts to index 0 and is selected;
            // undoing the deletion restores [A, B, C] — index 0 is A again, a
            // different block that happens to share C's RegionOrigin. Matching
            // on index and origin alone would silently select A instead of
            // clearing; the Cells check is what catches this.
            var scopeWave = new WaveDraft();
            scopeWave.Blocks.Add(new SpawnedBlockDraft { Cells = { new Coord(0, 0) }, RegionOrigin = new Coord(2, 2) });

            var key = SelectionKey.Capture(scopeWave.Blocks[0], scopeWave);

            var rebuiltWave = new WaveDraft();
            rebuiltWave.Blocks.Add(new SpawnedBlockDraft
            {
                Cells = { new Coord(0, 0), new Coord(1, 0) },
                RegionOrigin = new Coord(2, 2),
            });

            var resolved = key.Resolve(Draft(), rebuiltWave);

            Assert.IsNull(resolved);
        }

        [Test]
        public void Capture_NullSelection_ReturnsNoneWhichResolvesToNull()
        {
            var draft = Draft();

            var key = SelectionKey.Capture(null, scopeWave: null);
            var resolved = key.Resolve(draft, scopeWave: null);

            Assert.IsNull(resolved);
        }
    }
}
