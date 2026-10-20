using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The avatars a player can choose (Module 38, D71): ordered entries of an
    /// id and a sprite, the thirty Animal Pack Redux sprites in alphabetical
    /// order, each id the file name without its extension. The save keeps the
    /// id, never the position, so reordering or adding entries changes no
    /// player's avatar.
    /// </summary>
    /// <remarks>
    /// An id the catalog no longer has shows the first entry and is kept in
    /// the save until the player picks another. <see cref="ShownId"/> is the
    /// one question every screen asks, so the top bar, Profile's large avatar
    /// and the grid's mark always agree. Filled by
    /// <i>Gate Rush → Fill Avatar Catalog</i>.
    /// </remarks>
    [CreateAssetMenu(fileName = "AvatarCatalog", menuName = "Gate Rush/Avatar Catalog")]
    public sealed class AvatarCatalog : ScriptableObject
    {
        [Tooltip("The avatars in the order Profile's grid shows them. Ids not empty and unique, each with a sprite; the first is shown for an id the catalog does not have. The Meta Config's Default Avatar Id must be one of them.")]
        [SerializeField] private AvatarEntry[] avatars = Array.Empty<AvatarEntry>();

        /// <summary>How many avatars there are.</summary>
        public int Count => Entries.Length;

        private AvatarEntry[] Entries => avatars ?? Array.Empty<AvatarEntry>();

        /// <summary>The id of the avatar at <paramref name="index"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the catalog.</exception>
        public string IdAt(int index) => EntryAt(index)?.Id;

        /// <summary>The sprite of the avatar at <paramref name="index"/>; null when it is not assigned, which <see cref="Problems"/> reports.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the catalog.</exception>
        public Sprite SpriteAt(int index) => EntryAt(index)?.Sprite;

        /// <summary>True when an avatar has exactly the id <paramref name="id"/>.</summary>
        public bool Contains(string id) => IndexOf(id) >= 0;

        /// <summary>
        /// The id of the avatar shown for <paramref name="id"/>: that id when
        /// the catalog has it, otherwise the first entry's. Null for an empty
        /// catalog, which <see cref="Problems"/> reports.
        /// </summary>
        public string ShownId(string id)
        {
            var index = ShownIndex(id);
            return index < 0 ? null : Entries[index]?.Id;
        }

        /// <summary>
        /// The sprite shown for <paramref name="id"/>: the avatar
        /// <see cref="ShownId"/> names. Null for an empty catalog.
        /// </summary>
        public Sprite IconOf(string id)
        {
            var index = ShownIndex(id);
            return index < 0 ? null : Entries[index]?.Sprite;
        }

        /// <summary>
        /// Everything wrong with the catalog, each message naming its element:
        /// no avatar at all, an entry with no sprite, an empty id, or an id an
        /// earlier entry already has. Empty when every entry can be shown.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            var entries = Entries;
            if (entries.Length == 0)
            {
                problems.Add($"{name}: Avatars is empty; fill it with Gate Rush > Fill Avatar Catalog.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var id = entry?.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    problems.Add($"{name}: Avatars element {i}: Id is empty.");
                }
                else if (!seen.Add(id))
                {
                    problems.Add($"{name}: Avatars element {i}: Id '{id}' is used by an earlier element.");
                }

                if (entry?.Sprite == null)
                {
                    problems.Add($"{name}: Avatars element {i}: Sprite is not assigned.");
                }
            }

            return problems;
        }

        private AvatarEntry EntryAt(int index)
        {
            var entries = Entries;
            if (index < 0 || index >= entries.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"The catalog has {entries.Length} avatars.");
            }

            return entries[index];
        }

        private int IndexOf(string id)
        {
            if (id == null)
            {
                return -1;
            }

            var entries = Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && string.Equals(entries[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The index of the avatar shown for <paramref name="id"/>; -1 for an empty catalog.</summary>
        private int ShownIndex(string id)
        {
            var index = IndexOf(id);
            if (index >= 0)
            {
                return index;
            }

            return Entries.Length > 0 ? 0 : -1;
        }

        /// <summary>One avatar, as the Inspector edits it.</summary>
        [Serializable]
        private sealed class AvatarEntry
        {
            [SerializeField] private string id;
            [SerializeField] private Sprite sprite;

            public string Id => id;

            public Sprite Sprite => sprite;
        }
    }
}
