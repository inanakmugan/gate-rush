using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 37's <see cref="JokerIcons"/>: each icon left unassigned
    /// is reported by its field's name, a full set reports nothing, each kind
    /// reads its own field, and an undeclared kind throws.
    /// </summary>
    public class JokerIconsTests
    {
        private JokerIcons icons;
        private Texture2D texture;
        private readonly List<Sprite> sprites = new List<Sprite>();

        [SetUp]
        public void SetUp()
        {
            icons = ScriptableObject.CreateInstance<JokerIcons>();
            texture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(icons);
            foreach (var sprite in sprites)
            {
                Object.DestroyImmediate(sprite);
            }

            sprites.Clear();
            Object.DestroyImmediate(texture);
        }

        [TestCase("clockIcon", "Clock Icon")]
        [TestCase("rocketIcon", "Rocket Icon")]
        [TestCase("broomIcon", "Broom Icon")]
        public void Problems_MissingIcon_IsReportedByName(string field, string shownName)
        {
            AssignAll();
            SetSprite(field, null);

            var problems = icons.Problems();

            Assert.That(problems, Has.Some.Contains($"{shownName} is not assigned"), string.Join(" | ", problems));
        }

        [Test]
        public void Problems_EveryIconAssigned_ReportsNothing()
        {
            AssignAll();

            var problems = icons.Problems();

            Assert.IsEmpty(problems, string.Join(" | ", problems));
        }

        [TestCase(JokerKind.Clock, "clockIcon")]
        [TestCase(JokerKind.Rocket, "rocketIcon")]
        [TestCase(JokerKind.Broom, "broomIcon")]
        public void IconOf_Kind_IsItsOwnField(JokerKind kind, string field)
        {
            AssignAll();
            var marker = NewSprite();
            SetSprite(field, marker);

            var icon = icons.IconOf(kind);

            Assert.AreSame(marker, icon);
        }

        [Test]
        public void IconOf_UndeclaredKind_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => icons.IconOf((JokerKind)(-1)));
        }

        private void AssignAll()
        {
            SetSprite("clockIcon", NewSprite());
            SetSprite("rocketIcon", NewSprite());
            SetSprite("broomIcon", NewSprite());
        }

        private Sprite NewSprite()
        {
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
            sprites.Add(sprite);
            return sprite;
        }

        private void SetSprite(string field, Sprite sprite)
        {
            var serialized = new SerializedObject(icons);
            var property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"JokerIcons has no serialized field '{field}'.");
            property.objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
