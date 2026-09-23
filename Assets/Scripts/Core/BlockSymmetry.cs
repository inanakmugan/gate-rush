using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GateRush.Core
{
    /// <summary>
    /// Which of a level's block indices are interchangeable: groups of indices
    /// whose <see cref="BlockSpec"/>s are identical, so that swapping their
    /// dynamic rows produces the same board rather than a different one.
    /// <see cref="BoardState"/> sorts each group's rows into a canonical order
    /// before hashing and comparing, which is what stops a search from
    /// exploring every permutation of "which identical block sits where" as a
    /// distinct state (see <c>DECISIONS.md</c> D35).
    /// </summary>
    /// <remarks>
    /// <para><b>What makes two indices interchangeable.</b> Every field
    /// <see cref="LevelContext.SpecAt"/> fixes per index, with one deliberate
    /// exception: shape, colour stack, movement axis, unfreeze threshold, lock
    /// id, required key count, key target and key effect must all match.
    /// <see cref="BlockSpec.TimeBonusSeconds"/> is excluded, because time is
    /// outside the search space (<c>DECISIONS.md</c> D12) — the solver never
    /// reads a bonus, so two blocks differing only in one behave identically
    /// under every rule a search applies, and refusing to group them would
    /// leave exactly the duplication this type exists to remove.</para>
    ///
    /// <para><b>Why lock owners are always alone.</b> Lock ids are unique
    /// within a level (M8), so no two indices can share a spec that carries
    /// one. A locked block is therefore never grouped, and the permutations
    /// this type admits never disturb <see cref="LevelContext.LockOwnerIndex"/>.
    /// Key carriers may group freely: identical specs target the same lock with
    /// the same effect, so they are interchangeable to
    /// <see cref="LevelContext.KeyIndicesForLock"/>'s consumers, which count
    /// consumed keys rather than distinguish them.</para>
    ///
    /// <para><b>Groups of one are omitted.</b> A singleton group constrains
    /// nothing, so <see cref="Groups"/> holds only groups of two or more and a
    /// level with no repeated spec yields <see cref="IsTrivial"/> — the case
    /// <see cref="BoardState"/> answers by skipping canonicalisation entirely.</para>
    ///
    /// <para>Computed once per <see cref="LevelContext"/>, alongside its other
    /// precomputed lookups and for the same reason (D28): it is a pure function
    /// of immutable level data, bounded by authored content rather than by how
    /// many states a search visits.</para>
    /// </remarks>
    public sealed class BlockSymmetry
    {
        /// <summary>
        /// The symmetry of a level in which no two blocks are interchangeable —
        /// also the correct value for a state built without a level behind it,
        /// as this assembly's tests do.
        /// </summary>
        public static readonly BlockSymmetry None = new BlockSymmetry(Array.Empty<int[]>());

        private readonly int[][] groups;

        private BlockSymmetry(int[][] groups)
        {
            this.groups = groups;

            var view = new IReadOnlyList<int>[groups.Length];
            for (var i = 0; i < groups.Length; i++)
            {
                view[i] = new ReadOnlyCollection<int>(groups[i]);
            }

            Groups = new ReadOnlyCollection<IReadOnlyList<int>>(view);
        }

        /// <summary>
        /// The interchangeable groups, each holding two or more flat block
        /// indices in ascending order. Groups are ordered by their lowest
        /// member, and no index appears in more than one.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<int>> Groups { get; }

        /// <summary>
        /// True when no two blocks in the level share a spec, so canonicalising
        /// a state would reorder nothing.
        /// </summary>
        public bool IsTrivial => groups.Length == 0;

        /// <summary>
        /// The raw groups, for <see cref="BoardState"/>'s per-state
        /// canonicalisation — a hot path that runs once per constructed state
        /// and must not pay for the read-only wrappers <see cref="Groups"/>
        /// exposes. Never handed outside this assembly, and never mutated.
        /// </summary>
        internal int[][] RawGroups => groups;

        /// <summary>
        /// Groups the indices of <paramref name="specs"/> — a level's flat
        /// block-index space, as <see cref="LevelContext.SpecAt"/> resolves it —
        /// by spec equality. Runs once per level at load: a quadratic scan over
        /// authored content, not over search states.
        /// </summary>
        public static BlockSymmetry Of(IReadOnlyList<BlockSpec> specs)
        {
            if (specs == null || specs.Count < 2)
            {
                return None;
            }

            var grouped = new bool[specs.Count];
            var groups = new List<int[]>();
            var members = new List<int>();

            for (var i = 0; i < specs.Count; i++)
            {
                if (grouped[i])
                {
                    continue;
                }

                members.Clear();

                for (var j = i + 1; j < specs.Count; j++)
                {
                    if (grouped[j] || !AreInterchangeable(specs[i], specs[j]))
                    {
                        continue;
                    }

                    if (members.Count == 0)
                    {
                        members.Add(i);
                        grouped[i] = true;
                    }

                    members.Add(j);
                    grouped[j] = true;
                }

                if (members.Count > 0)
                {
                    groups.Add(members.ToArray());
                }
            }

            return groups.Count == 0 ? None : new BlockSymmetry(groups.ToArray());
        }

        /// <summary>
        /// Whether two specs make their blocks interchangeable. See the type
        /// remarks for which fields participate and why
        /// <see cref="BlockSpec.TimeBonusSeconds"/> does not.
        /// </summary>
        private static bool AreInterchangeable(in BlockSpec a, in BlockSpec b)
        {
            return a.Axis == b.Axis
                && a.UnfreezeAtClearCount == b.UnfreezeAtClearCount
                && a.LockId == b.LockId
                && a.RequiredKeyCount == b.RequiredKeyCount
                && a.KeyTargetLockId == b.KeyTargetLockId
                && a.KeyEffect == b.KeyEffect
                && SameColorStack(a.ColorStack, b.ColorStack)
                && SameShape(a.Cells, b.Cells);
        }

        /// <summary>
        /// Colour stacks match element for element: a stack is ordered, and the
        /// outermost colour is the only one a gate can match (M4).
        /// </summary>
        private static bool SameColorStack(IReadOnlyList<BlockColor> a, IReadOnlyList<BlockColor> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Shapes match as sets of cells, not as sequences: both are already
        /// shifted to a <c>(0, 0)</c> minimum corner (D30), so the same shape
        /// authored with its cells listed in a different order is the same
        /// shape. Containment one way suffices because the counts are equal and
        /// <c>BlockValidation.ValidateCells</c> has rejected duplicates in both,
        /// which makes each list a set.
        /// </summary>
        private static bool SameShape(IReadOnlyList<Coord> a, IReadOnlyList<Coord> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (!Contains(b, a[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Contains(IReadOnlyList<Coord> cells, Coord cell)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
