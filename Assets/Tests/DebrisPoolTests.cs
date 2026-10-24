using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 47's <see cref="DebrisPool"/>: it makes its renderers once,
    /// lends them switched on and takes them back switched off and reset, runs
    /// dry rather than making more, and reuses what comes back.
    /// </summary>
    public class DebrisPoolTests
    {
        private const int Size = 3;
        private const int Order = 37;

        private GameObject parent;
        private Texture2D texture;
        private Sprite sprite;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Debris pool under test");
            texture = new Texture2D(2, 2);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void New_Size_MakesThatManyRenderersSwitchedOffUnderOneRoot()
        {
            var pool = new DebrisPool(parent.transform, Size, null);

            Assert.AreEqual(parent.transform, pool.Root.parent);
            Assert.AreEqual(Size, pool.Root.childCount);
            Assert.AreEqual(0, pool.InUse);
            foreach (var renderer in pool.Root.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.IsFalse(renderer.enabled, $"{renderer.name} is off until lent");
            }
        }

        [Test]
        public void Borrow_Free_LendsARendererSwitchedOnShowingWhatWasAsked()
        {
            var pool = new DebrisPool(parent.transform, Size, null);

            var renderer = pool.Borrow(sprite, null, Color.red, Order);

            Assert.IsNotNull(renderer);
            Assert.IsTrue(renderer.enabled);
            Assert.AreSame(sprite, renderer.sprite);
            Assert.AreEqual(Color.red, renderer.color);
            Assert.AreEqual(Order, renderer.sortingOrder);
            Assert.IsTrue(pool.IsLent(renderer));
            Assert.AreEqual(1, pool.InUse);
        }

        [Test]
        public void Borrow_AllLent_GivesNullAndMakesNoMore()
        {
            var pool = new DebrisPool(parent.transform, Size, null);
            for (var i = 0; i < Size; i++)
            {
                Assert.IsNotNull(pool.Borrow(sprite, null, Color.white, Order), $"renderer {i}");
            }

            var extra = pool.Borrow(sprite, null, Color.white, Order);

            Assert.IsNull(extra, "dry: the piece is skipped");
            Assert.AreEqual(Size, pool.Root.childCount, "nothing made in play");
        }

        [Test]
        public void Return_Lent_SwitchesItOffResetsItAndLendsItAgain()
        {
            var pool = new DebrisPool(parent.transform, 1, null);
            var renderer = pool.Borrow(sprite, null, Color.red, Order);
            renderer.transform.localPosition = new Vector3(2f, 3f, 0f);
            renderer.transform.localScale = new Vector3(4f, 4f, 1f);

            pool.Return(renderer);

            Assert.IsFalse(renderer.enabled);
            Assert.IsNull(renderer.sprite);
            Assert.AreEqual(Vector3.zero, renderer.transform.localPosition);
            Assert.AreEqual(Vector3.one, renderer.transform.localScale);
            Assert.AreEqual(0, pool.InUse);
            Assert.AreSame(renderer, pool.Borrow(sprite, null, Color.white, Order), "the same renderer, reused");
        }

        [Test]
        public void Return_TwiceOrForeignOrNull_ChangesNothing()
        {
            var pool = new DebrisPool(parent.transform, Size, null);
            var renderer = pool.Borrow(sprite, null, Color.white, Order);
            var other = new GameObject("Not the pool's").AddComponent<SpriteRenderer>();
            other.transform.SetParent(parent.transform);
            pool.Return(renderer);

            pool.Return(renderer);
            pool.Return(other);
            pool.Return(null);

            Assert.AreEqual(0, pool.InUse);
            Assert.IsFalse(pool.IsLent(other));
        }

        [Test]
        public void ReturnAll_SomeLent_TakesEveryOneBack()
        {
            var pool = new DebrisPool(parent.transform, Size, null);
            var first = pool.Borrow(sprite, null, Color.white, Order);
            var second = pool.Borrow(sprite, null, Color.white, Order);

            pool.ReturnAll();

            Assert.AreEqual(0, pool.InUse);
            Assert.IsFalse(first.enabled);
            Assert.IsFalse(second.enabled);
        }
    }
}
