using System.Collections.Generic;
using System.IO;
using System.Linq;
using GateRush.UI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 45's <see cref="InterfaceArtImportRules"/>: which paths
    /// have a rule and what it is, that every slice border names a sprite in
    /// the project and leaves it a centre, and — once the three folders have
    /// been reimported — that every file there carries its settings, as
    /// <see cref="InterfaceArtImporter"/> applies them.
    /// </summary>
    /// <remarks>
    /// The last two tests read the project's real art, so they also fail
    /// when a file is missing or was imported before the importer existed
    /// and not reimported since.
    /// </remarks>
    public class InterfaceArtImportRulesTests
    {
        private const string PngPattern = "*.png";
        private const int PaintingCount = 3;
        private const int AvatarCount = 30;

        [Test]
        public void TryGetRule_SlicedSprite_IsTheSpriteSizeWithItsBorder()
        {
            var hasRule = InterfaceArtImportRules.TryGetRule("Assets/Art/Sprites/UI/panel.png", out var rule);

            Assert.IsTrue(hasRule);
            Assert.AreEqual(InterfaceArtImportRules.SpriteMaxSize, rule.MaxSize);
            Assert.AreEqual(new Vector4(54f, 51f, 54f, 58f), rule.Border, "left, bottom, right, top");
        }

        [Test]
        public void TryGetRule_IconSprite_IsTheSpriteSizeWithNoBorder()
        {
            var hasRule = InterfaceArtImportRules.TryGetRule("Assets/Art/Sprites/UI/coin.png", out var rule);

            Assert.IsTrue(hasRule);
            Assert.AreEqual(InterfaceArtImportRules.SpriteMaxSize, rule.MaxSize);
            Assert.AreEqual(Vector4.zero, rule.Border);
        }

        [Test]
        public void TryGetRule_Background_IsTheBackgroundSizeWithNoBorder()
        {
            var hasRule = InterfaceArtImportRules.TryGetRule("Assets/Art/Backgrounds/bg_town_day.png", out var rule);

            Assert.IsTrue(hasRule);
            Assert.AreEqual(InterfaceArtImportRules.BackgroundMaxSize, rule.MaxSize);
            Assert.AreEqual(Vector4.zero, rule.Border);
        }

        [Test]
        public void TryGetRule_Avatar_IsTheAvatarSizeWithNoBorder()
        {
            var hasRule = InterfaceArtImportRules.TryGetRule("Assets/Art/Sprites/Avatars/bear.png", out var rule);

            Assert.IsTrue(hasRule);
            Assert.AreEqual(InterfaceArtImportRules.AvatarMaxSize, rule.MaxSize);
            Assert.AreEqual(Vector4.zero, rule.Border);
        }

        [TestCase("Assets/Art/Generated/RoundedRect.png", TestName = "TryGetRule_OtherFolder_HasNone")]
        [TestCase("Assets/Art/Sprites/UI/Extra/panel.png", TestName = "TryGetRule_Subfolder_HasNone")]
        [TestCase("Assets/Art/Sprites/UI/panel.psd", TestName = "TryGetRule_OtherFileType_HasNone")]
        [TestCase("Assets/ThirdParty/Kenney/GameIcons/home.png", TestName = "TryGetRule_ThirdPartyIcon_HasNone")]
        public void TryGetRule_PathOutsideTheRules_HasNone(string path)
        {
            var hasRule = InterfaceArtImportRules.TryGetRule(path, out _);

            Assert.IsFalse(hasRule);
        }

        [Test]
        public void SliceBorders_EachNamesASpriteAndLeavesItACentre()
        {
            var wrong = new List<string>();

            foreach (var border in InterfaceArtImportRules.SliceBorders)
            {
                var path = $"{InterfaceArtImportRules.SpriteFolder}/{border.FileName}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    wrong.Add($"{path} is not an imported texture");
                    continue;
                }

                importer.GetSourceTextureWidthAndHeight(out var width, out var height);
                if (border.Left + border.Right >= width || border.Bottom + border.Top >= height)
                {
                    wrong.Add($"{border.FileName}: its borders leave no centre in {width} by {height}");
                }
            }

            Assert.That(InterfaceArtImportRules.SliceBorders.Count, Is.GreaterThan(0), "the table has entries");
            CollectionAssert.IsEmpty(wrong);
        }

        [Test]
        public void ImportedArt_EveryFileInTheFolders_CarriesItsRule()
        {
            var paths = Directory.GetFiles(InterfaceArtImportRules.SpriteFolder, PngPattern)
                .Concat(Directory.GetFiles(InterfaceArtImportRules.BackgroundFolder, PngPattern))
                .Concat(Directory.GetFiles(InterfaceArtImportRules.AvatarFolder, PngPattern))
                .Select(path => path.Replace('\\', '/'))
                .ToList();
            var wrong = new List<string>();

            foreach (var path in paths)
            {
                Assert.IsTrue(InterfaceArtImportRules.TryGetRule(path, out var rule), path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                if (importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || settings.spriteMeshType != SpriteMeshType.FullRect
                    || !importer.alphaIsTransparency
                    || importer.mipmapEnabled
                    || importer.maxTextureSize != rule.MaxSize
                    || !Mathf.Approximately(importer.spritePixelsPerUnit, InterfaceArtImportRules.PixelsPerUnit)
                    || importer.spriteBorder != rule.Border)
                {
                    wrong.Add(path);
                }
            }

            // At least the sliced sprites, the three paintings and the thirty avatars.
            Assert.That(
                paths.Count, Is.GreaterThanOrEqualTo(InterfaceArtImportRules.SliceBorders.Count + PaintingCount + AvatarCount),
                "the folders hold the art");
            CollectionAssert.IsEmpty(
                wrong, "files not imported by their rule; reimport Assets/Art/Sprites/UI, Assets/Art/Backgrounds and Assets/Art/Sprites/Avatars");
        }
    }
}
