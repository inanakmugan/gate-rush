using System.Collections.Generic;
using System.Linq;
using GateRush.Core;

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
    /// <see cref="BlockDraft"/>, <see cref="GateDraft"/>, <see cref="ShutterDraft"/>,
    /// <see cref="GeneratorDraft"/> and <see cref="ElevatorDraft"/> all carry an
    /// <c>Id</c> that round-trips through the DTO, so those resolve by id.
    /// <see cref="SpawnedBlockDraft"/> has none: the only one ever selected is a
    /// wave block, identified by its index within the current wave, since wave
    /// scope is preserved across undo. A generator's queue entries are never the
    /// selection — the generator is — so they need no key of their own.
    /// <para><b>Index alone is not identity.</b> Undoing a deletion shifts every
    /// later index down, so "index 1" can mean a different block before and
    /// after the same undo — restoring wave [A, B, C], deleting A, selecting C
    /// (now index 1), then undoing must not land on B, which the restored draft
    /// also has at index 1. The key therefore also carries the captured block's
    /// <see cref="SpawnedBlockDraft.RegionOrigin"/> and <see cref="SpawnedBlockDraft.Cells"/>,
    /// and <see cref="Resolve"/> re-checks both before accepting the
    /// index-matched candidate. Two blocks with identical origin and cells
    /// cannot coexist in one valid wave, so a match is a real match; the only
    /// case this newly clears rather than restores is undoing a move of the
    /// selected block itself, where its origin no longer matches — the
    /// conservative outcome. A stable <c>Id</c> on <see cref="SpawnedBlockDraft"/>,
    /// the same identity the other five draft types already carry, would make
    /// this check unnecessary; that belongs with the generator-width round,
    /// which is already changing that DTO, not here.</para>
    /// </remarks>
    public readonly struct SelectionKey
    {
        private enum Kind { None, Block, Gate, Shutter, Generator, Elevator, WaveBlock }

        private readonly Kind kind;
        private readonly int id;
        private readonly int waveBlockIndex;
        private readonly Coord? waveBlockRegionOrigin;
        private readonly IReadOnlyList<Coord> waveBlockCells;

        private SelectionKey(
            Kind kind, int id, int waveBlockIndex, Coord? waveBlockRegionOrigin, IReadOnlyList<Coord> waveBlockCells)
        {
            this.kind = kind;
            this.id = id;
            this.waveBlockIndex = waveBlockIndex;
            this.waveBlockRegionOrigin = waveBlockRegionOrigin;
            this.waveBlockCells = waveBlockCells;
        }

        /// <summary>No selection, or a selection this key cannot represent.</summary>
        public static readonly SelectionKey None = new SelectionKey(Kind.None, 0, 0, null, null);

        /// <summary>
        /// Captures <paramref name="selection"/> as it stands before a draft
        /// rebuild. <paramref name="scopeWave"/> is the wave the current scope
        /// points at, or <c>null</c> when not in wave scope — needed only to find
        /// a <see cref="SpawnedBlockDraft"/>'s index and shape.
        /// </summary>
        public static SelectionKey Capture(object selection, WaveDraft scopeWave)
        {
            switch (selection)
            {
                case BlockDraft block:
                    return new SelectionKey(Kind.Block, block.Id, 0, null, null);
                case GateDraft gate:
                    return new SelectionKey(Kind.Gate, gate.Id, 0, null, null);
                case ShutterDraft shutter:
                    return new SelectionKey(Kind.Shutter, shutter.Id, 0, null, null);
                case GeneratorDraft generator:
                    return new SelectionKey(Kind.Generator, generator.Id, 0, null, null);
                case ElevatorDraft elevator:
                    return new SelectionKey(Kind.Elevator, elevator.Id, 0, null, null);
                case SpawnedBlockDraft waveBlock when scopeWave != null:
                {
                    var index = scopeWave.Blocks.IndexOf(waveBlock);
                    return index >= 0
                        ? new SelectionKey(Kind.WaveBlock, 0, index, waveBlock.RegionOrigin, waveBlock.Cells.ToList())
                        : None;
                }

                default:
                    return None;
            }
        }

        /// <summary>
        /// Finds the equivalent object in <paramref name="draft"/>, or
        /// <c>null</c> if it no longer exists. <paramref name="scopeWave"/> must
        /// be the wave at the same scope indices in the rebuilt draft — scope
        /// indices themselves survive undo unchanged (docs/Modules/09a, Session C).
        /// A <see cref="Kind.WaveBlock"/> candidate at the captured index is
        /// accepted only if its <see cref="SpawnedBlockDraft.RegionOrigin"/> and
        /// <see cref="SpawnedBlockDraft.Cells"/> still match what was captured.
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
                {
                    if (scopeWave == null || waveBlockIndex < 0 || waveBlockIndex >= scopeWave.Blocks.Count)
                    {
                        return null;
                    }

                    var candidate = scopeWave.Blocks[waveBlockIndex];
                    return candidate.RegionOrigin == waveBlockRegionOrigin && SameCells(candidate.Cells, waveBlockCells)
                        ? (object)candidate
                        : null;
                }

                default:
                    return null;
            }
        }

        private static bool SameCells(IReadOnlyList<Coord> a, IReadOnlyList<Coord> b) =>
            a.Count == b.Count && a.All(b.Contains);
    }
}
