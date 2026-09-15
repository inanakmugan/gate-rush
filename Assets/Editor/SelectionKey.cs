using System.Linq;

namespace GateRush.Editor
{
    /// <summary>
    /// Identifies whatever is selected by identity rather than by reference, so
    /// undo/redo (docs/Modules/09a, Session C, follow-up 1) can reselect the
    /// equivalent object after <see cref="LevelDraft.FromDto"/> has rebuilt the
    /// draft and left every old reference — including the window's selection —
    /// pointing at a discarded draft.
    /// </summary>
    /// <remarks>
    /// Every draft type this key can name carries an <c>Id</c> that round-trips
    /// through the DTO, so every case resolves the same way: find the object with
    /// that id. <see cref="BlockDraft"/>, <see cref="GateDraft"/>,
    /// <see cref="ShutterDraft"/>, <see cref="GeneratorDraft"/> and
    /// <see cref="ElevatorDraft"/> are looked up in the draft's own lists;
    /// a <see cref="SpawnedBlockDraft"/> is looked up within one wave, since its
    /// id is unique to that wave and not across the level (D34). A generator's
    /// queue entries are never the selection — the generator is — so a queue
    /// entry's id is never captured here.
    /// <para>An id, not an index: restoring a deletion shifts every later index
    /// down, so "index 1" can mean a different block before and after the same
    /// undo. Until <see cref="SpawnedBlockDraft"/> had an id of its own, a wave
    /// block was matched by index and its captured origin and cells re-checked to
    /// catch exactly that — a stopgap D33 recorded as waiting for D34's id, and
    /// which the id now replaces outright.</para>
    /// </remarks>
    public readonly struct SelectionKey
    {
        private enum Kind { None, Block, Gate, Shutter, Generator, Elevator, WaveBlock }

        private readonly Kind kind;
        private readonly int id;

        private SelectionKey(Kind kind, int id)
        {
            this.kind = kind;
            this.id = id;
        }

        /// <summary>No selection, or a selection this key cannot represent.</summary>
        public static readonly SelectionKey None = new SelectionKey(Kind.None, 0);

        /// <summary>
        /// Captures <paramref name="selection"/> as it stands before a draft
        /// rebuild. <paramref name="scopeWave"/> is the wave the current scope
        /// points at, or <c>null</c> when not in wave scope; a
        /// <see cref="SpawnedBlockDraft"/> that is not in that wave cannot be
        /// named by this key, because its id means nothing outside its own list.
        /// </summary>
        public static SelectionKey Capture(object selection, WaveDraft scopeWave)
        {
            switch (selection)
            {
                case BlockDraft block:
                    return new SelectionKey(Kind.Block, block.Id);
                case GateDraft gate:
                    return new SelectionKey(Kind.Gate, gate.Id);
                case ShutterDraft shutter:
                    return new SelectionKey(Kind.Shutter, shutter.Id);
                case GeneratorDraft generator:
                    return new SelectionKey(Kind.Generator, generator.Id);
                case ElevatorDraft elevator:
                    return new SelectionKey(Kind.Elevator, elevator.Id);
                case SpawnedBlockDraft waveBlock when scopeWave != null:
                    return scopeWave.Blocks.Contains(waveBlock)
                        ? new SelectionKey(Kind.WaveBlock, waveBlock.Id)
                        : None;

                default:
                    return None;
            }
        }

        /// <summary>
        /// Finds the equivalent object in <paramref name="draft"/>, or
        /// <c>null</c> if it no longer exists. <paramref name="scopeWave"/> must
        /// be the wave at the same scope indices in the rebuilt draft — scope
        /// indices themselves survive undo unchanged (docs/Modules/09a,
        /// Session C) — and is where a <see cref="Kind.WaveBlock"/> id is
        /// resolved.
        /// </summary>
        public object Resolve(LevelDraft draft, WaveDraft scopeWave)
        {
            // A lambda inside a struct cannot capture an instance field (CS1673)
            // — it would need to capture "this", and a struct's "this" is a
            // by-ref parameter, not an addressable local a closure can hold.
            // Copying to a local gives the lambdas below an ordinary variable
            // to capture instead.
            var targetId = id;

            switch (kind)
            {
                case Kind.Block:
                    return draft.Blocks.FirstOrDefault(b => b.Id == targetId);
                case Kind.Gate:
                    return draft.Gates.FirstOrDefault(g => g.Id == targetId);
                case Kind.Shutter:
                    return draft.Shutters.FirstOrDefault(s => s.Id == targetId);
                case Kind.Generator:
                    return draft.Generators.FirstOrDefault(g => g.Id == targetId);
                case Kind.Elevator:
                    return draft.Elevators.FirstOrDefault(e => e.Id == targetId);
                case Kind.WaveBlock:
                    return scopeWave?.Blocks.FirstOrDefault(b => b.Id == targetId);

                default:
                    return null;
            }
        }
    }
}
