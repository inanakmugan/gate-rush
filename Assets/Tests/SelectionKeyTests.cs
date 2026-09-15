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
            draft.Generators.Add(new GeneratorDraft { Id = 4, Edge = BoardEdge.Bottom, Width = 1 });
            draft.Elevators.Add(new ElevatorDraft { Id = 5, Min = new Coord(2, 2), Max = new Coord(3, 3) });
            draft.Elevators[0].Waves.Add(new WaveDraft());
            draft.Elevators[0].Waves[0].Blocks.Add(
                new SpawnedBlockDraft { Id = 1, Cells = { new Coord(0, 0) }, RegionOrigin = new Coord(2, 2) });
            draft.Elevators[0].Waves[0].Blocks.Add(
                new SpawnedBlockDraft { Id = 2, Cells = { new Coord(0, 0) }, RegionOrigin = new Coord(3, 2) });

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
        public void Capture_Resolve_WaveBlock_FindsTheSameIdInTheRebuiltWave()
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
        public void Resolve_WaveBlockDeletedFromTheRebuiltWave_ReturnsNull()
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
        public void Resolve_WaveBlockAtANewIndexAmongReshapedNeighbours_StillResolvesById()
        {
            // The case D33's index-and-shape check could only half-answer. Wave
            // [A, B, C]; A is deleted, so C is selected at index 1; undoing the
            // deletion restores [A, B, C] and C is at index 2, with a block at its
            // old index whose shape also differs from what was captured. An index
            // would land on the wrong block, and index-plus-shape would give up
            // and clear the selection; the id finds C (D34).
            var beforeUndo = new WaveDraft();
            beforeUndo.Blocks.Add(new SpawnedBlockDraft
            {
                Id = 2, Cells = { new Coord(0, 0) }, RegionOrigin = new Coord(3, 2),
            });
            beforeUndo.Blocks.Add(new SpawnedBlockDraft
            {
                Id = 3, Cells = { new Coord(0, 0) }, RegionOrigin = new Coord(4, 2),
            });

            var key = SelectionKey.Capture(beforeUndo.Blocks[1], beforeUndo);

            var restored = new WaveDraft();
            restored.Blocks.Add(new SpawnedBlockDraft
            {
                Id = 1, Cells = { new Coord(0, 0), new Coord(1, 0) }, RegionOrigin = new Coord(0, 2),
            });
            restored.Blocks.Add(new SpawnedBlockDraft
            {
                Id = 2, Cells = { new Coord(0, 0), new Coord(0, 1) }, RegionOrigin = new Coord(3, 2),
            });
            restored.Blocks.Add(new SpawnedBlockDraft
            {
                Id = 3, Cells = { new Coord(0, 0) }, RegionOrigin = new Coord(4, 2),
            });

            var resolved = key.Resolve(Draft(), restored);

            Assert.AreSame(restored.Blocks[2], resolved);
        }

        [Test]
        public void Capture_WaveBlockNotInTheScopeWave_ReturnsNone()
        {
            // An id means nothing outside its own list, so a block that is not in
            // the wave being captured against cannot be named by this key.
            var scopeWave = new WaveDraft();
            var stray = new SpawnedBlockDraft { Id = 1, Cells = { new Coord(0, 0) } };

            var key = SelectionKey.Capture(stray, scopeWave);

            Assert.IsNull(key.Resolve(Draft(), scopeWave));
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
