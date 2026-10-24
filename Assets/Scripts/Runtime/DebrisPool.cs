using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The board's debris renderers (Module 47): a fixed set of sprite
    /// renderers made once and lent out for every cube, shard, sparkle and
    /// gate glow, so nothing is made or destroyed while a level is played.
    /// </summary>
    /// <remarks>
    /// <para><b>Lent by switching.</b> Every renderer stays where it was made,
    /// active, under one root; lending one switches it on, returning it
    /// switches it off and puts it back as it was made. Nothing is moved in
    /// the hierarchy and no object is activated or deactivated, so returning
    /// is safe while the view is being disabled.</para>
    /// <para><b>Run dry.</b> <see cref="Borrow"/> gives null while every
    /// renderer is out, and the caller skips that piece. Nothing is logged:
    /// a burst can ask for many pieces in one frame.</para>
    /// <para>Borrowing and returning allocate nothing.</para>
    /// </remarks>
    public sealed class DebrisPool
    {
        private const string RootName = "Debris Pool";
        private const string PieceName = "Piece";

        private readonly SpriteRenderer[] renderers;
        private readonly Dictionary<SpriteRenderer, int> indices;
        private readonly bool[] isLent;
        private readonly Stack<int> free;

        /// <summary>
        /// Makes <paramref name="size"/> renderers, switched off, under a new
        /// root at the origin of <paramref name="parent"/>, each drawn with
        /// <paramref name="material"/> until a borrower says otherwise.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="parent"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is below 1.</exception>
        public DebrisPool(Transform parent, int size, Material material)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (size < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "A pool holds at least one renderer.");
            }

            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            Root = root.transform;
            Material = material;

            renderers = new SpriteRenderer[size];
            indices = new Dictionary<SpriteRenderer, int>(size);
            isLent = new bool[size];
            free = new Stack<int>(size);
            for (var i = size - 1; i >= 0; i--)
            {
                var piece = new GameObject(PieceName);
                piece.transform.SetParent(Root, false);
                var renderer = piece.AddComponent<SpriteRenderer>();
                renderers[i] = renderer;
                indices.Add(renderer, i);
                Reset(renderer);
                free.Push(i);
            }
        }

        /// <summary>The root every renderer sits under.</summary>
        public Transform Root { get; }

        /// <summary>The material a returned renderer is set back to.</summary>
        public Material Material { get; }

        /// <summary>How many renderers the pool holds.</summary>
        public int Size => renderers.Length;

        /// <summary>How many are lent out now.</summary>
        public int InUse => renderers.Length - free.Count;

        /// <summary>
        /// Lends a renderer, switched on, showing <paramref name="sprite"/> with
        /// <paramref name="material"/> in <paramref name="color"/> at sorting
        /// order <paramref name="order"/>, at the root's origin, unturned and at
        /// scale 1; or null when every renderer is out.
        /// </summary>
        public SpriteRenderer Borrow(Sprite sprite, Material material, Color color, int order)
        {
            if (free.Count == 0)
            {
                return null;
            }

            var index = free.Pop();
            isLent[index] = true;
            var renderer = renderers[index];
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.enabled = true;
            return renderer;
        }

        /// <summary>
        /// Takes <paramref name="renderer"/> back, switched off and set back as
        /// it was made. Does nothing for null — a renderer destroyed with the
        /// view — a renderer already back, or one this pool did not lend.
        /// </summary>
        public void Return(SpriteRenderer renderer)
        {
            if (renderer == null || !indices.TryGetValue(renderer, out var index) || !isLent[index])
            {
                return;
            }

            isLent[index] = false;
            Reset(renderer);
            free.Push(index);
        }

        /// <summary>
        /// Takes every lent renderer back. The caller kills whatever would
        /// return one of them later first: a renderer taken back here may be
        /// lent again at once, and a late return would take it from its new
        /// borrower.
        /// </summary>
        public void ReturnAll()
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                if (isLent[i])
                {
                    Return(renderers[i]);
                }
            }
        }

        /// <summary>Whether <paramref name="renderer"/> is one of this pool's and lent out now.</summary>
        public bool IsLent(SpriteRenderer renderer) =>
            renderer != null && indices.TryGetValue(renderer, out var index) && isLent[index];

        private void Reset(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.enabled = false;
            renderer.sprite = null;
            renderer.sharedMaterial = Material;
            renderer.color = Color.white;
            var transform = renderer.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }
}
