using System;
using System.Collections.Generic;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BoardRecipes{TGroup, TPiece}"/>'s time-bonus mark
    /// (Module 46, pass 2, option D) through a surface that only records what
    /// it is asked to draw: the group holds the coin and the outlined seconds
    /// and nothing else — no plate, panel or backing behind them — centred on
    /// the anchor together for every value (revision B), so the coin moves
    /// with the text. The mark sorts above a layered block's window.
    /// </summary>
    public class BoardRecipesTests
    {
        private const int SpriteTexels = 4;
        private const float SpritePixelsPerUnit = 128f;
        private const float Tolerance = 1e-6f;

        private RuntimeConfig config;
        private Texture2D texture;
        private Sprite coin;
        private RecordingSurface surface;
        private BoardRecipes<RecordedGroup, RecordedPiece> recipes;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<RuntimeConfig>();
            texture = new Texture2D(SpriteTexels, SpriteTexels);
            coin = Sprite.Create(texture, new Rect(0f, 0f, SpriteTexels, SpriteTexels), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("bonusCoinSprite").objectReferenceValue = coin;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            surface = new RecordingSurface();
            recipes = new BoardRecipes<RecordedGroup, RecordedPiece>(config, surface);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(coin);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void TimeBonusMark_Drawn_IsOneGroupHoldingOnlyTheCoinAndTheSeconds()
        {
            var parent = new RecordedGroup("Body", null, Vector2.zero, 1f);
            var center = new Vector2(1f, 0.5f);
            const float scale = 0.6f;

            recipes.TimeBonusMark(parent, center, scale, 10);

            var group = surface.Groups.Find(g => g.Name == "Time bonus");
            Assert.IsNotNull(group, "the mark's group");
            Assert.AreSame(parent, group.Parent, "under the body");
            Assert.AreEqual(center, group.Center, "on its slot");
            Assert.AreEqual(scale, group.Scale, "the whole group scaled at once");
            CollectionAssert.AreEqual(new[] { "Sprite Coin", "OutlinedLabel Seconds" }, surface.Calls, "nothing else, so nothing behind them");
        }

        [Test]
        public void TimeBonusMark_Drawn_PutsTheCoinAndTheSecondsWhereTheCentredGroupHasThem()
        {
            recipes.TimeBonusMark(new RecordedGroup("Body", null, Vector2.zero, 1f), Vector2.zero, 1f, 10);

            var drawnCoin = surface.Pieces.Find(p => p.Name == "Coin");
            Assert.AreSame(coin, drawnCoin.Sprite, "the painted coin");
            Assert.AreEqual(config.TimeBonusCoinCenterCells("+10"), drawnCoin.Center.x, Tolerance, "the coin's centre, left of the anchor");
            Assert.Less(drawnCoin.Center.x, 0f, "left of the anchor");
            Assert.AreEqual(0f, drawnCoin.Center.y, Tolerance, "on the anchor's height");
            Assert.AreEqual(config.IconOrder, drawnCoin.Order, "with the icons");

            var seconds = surface.Labels.Find(l => l.Name == "Seconds");
            Assert.AreEqual("+10", seconds.Text);
            Assert.AreEqual(
                new Vector2(config.TimeBonusLabelStartCells("+10"), -config.TimeBonusLabelDropCells), seconds.Position, "where they start");
            Assert.AreEqual(config.CountFontSize, seconds.FontSize, "at the count digits' size");
            Assert.AreEqual(config.CountTextColor, seconds.Color, "white");
        }

        [TestCase(5)]
        [TestCase(RuntimeConfig.WidestBonusSecondsChecked)]
        public void TimeBonusMark_AnyValue_KeepsTheGapAndCentresTheGroup(int seconds)
        {
            var text = config.TimeBonusMarkText(seconds);

            recipes.TimeBonusMark(new RecordedGroup("Body", null, Vector2.zero, 1f), Vector2.zero, 1f, seconds);

            var coinCenter = surface.Pieces.Find(p => p.Name == "Coin").Center.x;
            var start = surface.Labels.Find(l => l.Name == "Seconds").Position.x;
            var coinRight = coinCenter + config.BonusCoinCells / 2f;
            Assert.AreEqual(config.TimeBonusLabelGapCells, start - coinRight, Tolerance, "the gap after the coin");
            Assert.AreEqual(
                -(coinCenter - config.BonusCoinCells / 2f), start + config.TimeBonusLabelAdvanceCells(text), Tolerance,
                "the coin's left edge and the advance's end either side of the anchor");
        }

        [Test]
        public void Orders_Defaults_DrawTheMarkAboveALayeredBlocksWindow()
        {
            // The coin sorts with the icons, above the window's gloss, and the
            // seconds with the labels, above the coin.
            Assert.Greater(config.IconOrder, config.LayerGlossOrder, "the coin over the window");
            Assert.Greater(config.LabelOrder, config.IconOrder, "the seconds over the coin");
        }

        /// <summary>A recorded group.</summary>
        private sealed class RecordedGroup
        {
            public RecordedGroup(string name, RecordedGroup parent, Vector2 center, float scale)
            {
                Name = name;
                Parent = parent;
                Center = center;
                Scale = scale;
            }

            public string Name { get; }

            public RecordedGroup Parent { get; }

            public Vector2 Center { get; }

            public float Scale { get; }
        }

        /// <summary>A recorded sprite.</summary>
        private sealed class RecordedPiece
        {
            public RecordedPiece(string name, Sprite sprite, Vector2 center, int order)
            {
                Name = name;
                Sprite = sprite;
                Center = center;
                Order = order;
            }

            public string Name { get; }

            public Sprite Sprite { get; }

            public Vector2 Center { get; }

            public int Order { get; }
        }

        /// <summary>A recorded label.</summary>
        private sealed class RecordedLabel
        {
            public RecordedLabel(string name, Vector2 position, string text, float fontSize, Color color)
            {
                Name = name;
                Position = position;
                Text = text;
                FontSize = fontSize;
                Color = color;
            }

            public string Name { get; }

            public Vector2 Position { get; }

            public string Text { get; }

            public float FontSize { get; }

            public Color Color { get; }
        }

        /// <summary>A surface that draws nothing and records each call, in order, as "Method Name".</summary>
        private sealed class RecordingSurface : IRecipeSurface<RecordedGroup, RecordedPiece>
        {
            public List<string> Calls { get; } = new List<string>();

            public List<RecordedGroup> Groups { get; } = new List<RecordedGroup>();

            public List<RecordedPiece> Pieces { get; } = new List<RecordedPiece>();

            public List<RecordedLabel> Labels { get; } = new List<RecordedLabel>();

            public RecordedGroup Group(RecordedGroup parent, string name, Vector2 center, float scale)
            {
                var group = new RecordedGroup(name, parent, center, scale);
                Groups.Add(group);
                return group;
            }

            public List<RecordedPiece> Quarters(
                RecordedGroup parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, QuarterLayer layer, Color color,
                int order)
            {
                Calls.Add($"Quarters {layer}");
                return new List<RecordedPiece>();
            }

            public RecordedPiece Sprite(
                RecordedGroup parent, string name, Sprite sprite, Vector2 center, Vector2 size, Color color, int order,
                float rotationDegrees, SpriteLook look) =>
                Record("Sprite", name, sprite, center, order);

            public RecordedPiece Sliced(
                RecordedGroup parent, string name, Sprite sprite, Vector2 center, Vector2 size, float border, Color color, int order,
                SpriteLook look) =>
                Record("Sliced", name, sprite, center, order);

            public RecordedPiece Tiled(
                RecordedGroup parent, string name, Sprite sprite, Vector2 center, Vector2 size, float tileCells,
                bool tileIsMeasuredByHeight, Color color, int order, SpriteLook look) =>
                Record("Tiled", name, sprite, center, order);

            public RecordedPiece Strip(
                RecordedGroup parent, string name, Sprite sprite, Vector2 center, float length, float thickness, bool isAlongY,
                Color color, int order, SpriteLook look) =>
                Record("Strip", name, sprite, center, order);

            public void Label(
                RecordedGroup parent, string name, Vector2 center, string text, float boardFontSize, Color color, bool isShadowed)
            {
                Calls.Add($"Label {name}");
                Labels.Add(new RecordedLabel(name, center, text, boardFontSize, color));
            }

            public void OutlinedLabel(RecordedGroup parent, string name, Vector2 start, string text, float boardFontSize, Color color)
            {
                Calls.Add($"OutlinedLabel {name}");
                Labels.Add(new RecordedLabel(name, start, text, boardFontSize, color));
            }

            private RecordedPiece Record(string method, string name, Sprite sprite, Vector2 center, int order)
            {
                Calls.Add($"{method} {name}");
                var piece = new RecordedPiece(name, sprite, center, order);
                Pieces.Add(piece);
                return piece;
            }
        }
    }
}
