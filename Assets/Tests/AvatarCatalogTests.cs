using System.Collections.Generic;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 38's <see cref="AvatarCatalog"/>: a known id shows its
    /// own sprite, an unknown, null or empty one the first entry's, and
    /// <see cref="AvatarCatalog.ShownId"/> names the same avatar
    /// <see cref="AvatarCatalog.IconOf"/> draws; the entries keep their order;
    /// and a missing sprite, an empty id, a repeated id and an empty catalog
    /// are each reported, while a full catalog reports nothing.
    /// </summary>
    public class AvatarCatalogTests
    {
        private const string AvatarsField = "avatars";
        private const string IdField = "id";
        private const string SpriteField = "sprite";

        private AvatarCatalog catalog;
        private Texture2D texture;
        private readonly List<Sprite> sprites = new List<Sprite>();

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
            texture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(catalog);
            foreach (var sprite in sprites)
            {
                Object.DestroyImmediate(sprite);
            }

            sprites.Clear();
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void IconOf_KnownId_IsItsOwnSprite()
        {
            var bear = NewSprite();
            var cow = NewSprite();
            SetEntries(("bear", bear), ("cow", cow));

            var icon = catalog.IconOf("cow");

            Assert.AreSame(cow, icon);
        }

        [TestCase("lion")]
        [TestCase(null)]
        [TestCase("")]
        public void IconOf_IdNotInTheCatalog_IsTheFirstEntrysSprite(string id)
        {
            var bear = NewSprite();
            SetEntries(("bear", bear), ("cow", NewSprite()));

            var icon = catalog.IconOf(id);

            Assert.AreSame(bear, icon);
        }

        [TestCase("cow", "cow")]
        [TestCase("lion", "bear")]
        public void ShownId_Id_NamesTheAvatarIconOfDraws(string id, string expected)
        {
            SetEntries(("bear", NewSprite()), ("cow", NewSprite()));

            var shown = catalog.ShownId(id);

            Assert.AreEqual(expected, shown);
            Assert.AreSame(catalog.SpriteAt(shown == "bear" ? 0 : 1), catalog.IconOf(id), "the icon is the shown avatar's");
        }

        [TestCase("cow", true)]
        [TestCase("Cow", false)]
        public void Contains_Id_IsAnOrdinalMatch(string id, bool expected)
        {
            SetEntries(("bear", NewSprite()), ("cow", NewSprite()));

            var contains = catalog.Contains(id);

            Assert.AreEqual(expected, contains);
        }

        [Test]
        public void IdAtAndSpriteAt_KeepTheEntriesOrder()
        {
            var bear = NewSprite();
            var cow = NewSprite();
            SetEntries(("bear", bear), ("cow", cow));

            Assert.AreEqual(2, catalog.Count);
            Assert.AreEqual("bear", catalog.IdAt(0));
            Assert.AreEqual("cow", catalog.IdAt(1));
            Assert.AreSame(bear, catalog.SpriteAt(0));
            Assert.AreSame(cow, catalog.SpriteAt(1));
        }

        [Test]
        public void Problems_MissingSprite_IsReportedByElement()
        {
            SetEntries(("bear", NewSprite()), ("cow", null));

            var problems = catalog.Problems();

            Assert.That(problems, Has.Some.Contains("element 1: Sprite is not assigned"), string.Join(" | ", problems));
        }

        [TestCase("")]
        [TestCase("  ")]
        public void Problems_EmptyId_IsReportedByElement(string id)
        {
            SetEntries(("bear", NewSprite()), (id, NewSprite()));

            var problems = catalog.Problems();

            Assert.That(problems, Has.Some.Contains("element 1: Id is empty"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_RepeatedId_IsReportedOnTheLaterElement()
        {
            SetEntries(("bear", NewSprite()), ("bear", NewSprite()));

            var problems = catalog.Problems();

            Assert.That(problems, Has.Some.Contains("element 1: Id 'bear' is used by an earlier element"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_EmptyCatalog_IsReported()
        {
            SetEntries();

            var problems = catalog.Problems();

            Assert.That(problems, Has.Some.Contains("Avatars is empty"), string.Join(" | ", problems));
            Assert.IsNull(catalog.IconOf("bear"), "an empty catalog shows nothing");
        }

        [Test]
        public void Problems_FullCatalog_ReportsNothing()
        {
            SetEntries(("bear", NewSprite()), ("cow", NewSprite()));

            var problems = catalog.Problems();

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
        }

        private Sprite NewSprite()
        {
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            sprites.Add(sprite);
            return sprite;
        }

        private void SetEntries(params (string id, Sprite sprite)[] entries)
        {
            using (var serialized = new SerializedObject(catalog))
            {
                var avatars = serialized.FindProperty(AvatarsField);
                Assert.IsNotNull(avatars, $"AvatarCatalog has no serialized field '{AvatarsField}'.");
                avatars.arraySize = entries.Length;
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = avatars.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative(IdField).stringValue = entries[i].id;
                    entry.FindPropertyRelative(SpriteField).objectReferenceValue = entries[i].sprite;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
