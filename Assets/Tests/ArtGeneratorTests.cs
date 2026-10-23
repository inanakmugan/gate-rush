using System.IO;
using System.Linq;
using System.Reflection;
using GateRush.Editor;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ArtGenerator"/>'s link to <see cref="RuntimeConfig"/>
    /// (Module 46): every generated sprite the board uses has a config field
    /// to be set into, every sprite field of the config but the hand-made
    /// ones has a generated PNG behind it, and the project's config holds
    /// exactly those PNGs, so the generator and the config cannot drift.
    /// </summary>
    /// <remarks>
    /// The last two read the PNGs and the config on disk, so they pass once
    /// <i>Gate Rush → Generate Art</i> has run on the current recipe.
    /// </remarks>
    public class ArtGeneratorTests
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        // The config's sprite fields drawn by hand or imported, never generated.
        private static readonly string[] HandMadeSpriteFields =
        {
            "cellSprite", "introPanelSprite", "introCloseSprite", "introCloseCrossSprite", "introTextBoxSprite"
        };

        [Test]
        public void RuntimeConfigField_EveryGeneratedSpriteButRestart_IsASpriteFieldOfTheConfig()
        {
            foreach (var sprite in ArtPainter.All)
            {
                var field = ArtGenerator.RuntimeConfigField(sprite);
                if (sprite == ArtSprite.Restart)
                {
                    Assert.IsNull(field, "the restart icon is the interface's, not the board's");
                    continue;
                }

                var info = typeof(RuntimeConfig).GetField(field, FieldFlags);

                Assert.IsNotNull(info, $"{sprite}: RuntimeConfig has a field '{field}'");
                Assert.AreEqual(typeof(Sprite), info.FieldType, $"{sprite}: '{field}' holds a sprite");
            }
        }

        [Test]
        public void EveryRuntimeConfigSpriteField_ButTheHandMadeOnes_HasAGeneratedPngBehindIt()
        {
            var generated = ArtPainter.All
                .Where(sprite => ArtGenerator.RuntimeConfigField(sprite) != null)
                .ToDictionary(ArtGenerator.RuntimeConfigField);
            var spriteFields = typeof(RuntimeConfig).GetFields(FieldFlags)
                .Where(field => field.FieldType == typeof(Sprite))
                .Select(field => field.Name)
                .ToList();

            foreach (var handMade in HandMadeSpriteFields)
            {
                Assert.Contains(handMade, spriteFields, $"the hand-made sprite field '{handMade}' still exists");
            }

            var generatedFields = spriteFields.Except(HandMadeSpriteFields).ToList();
            Assert.AreEqual(generated.Count, generatedFields.Count, "one generated sprite per remaining field");
            foreach (var field in generatedFields)
            {
                Assert.IsTrue(generated.TryGetValue(field, out var sprite), $"'{field}' is written by Generate Art");
                Assert.IsTrue(File.Exists(ArtGenerator.PathOf(sprite)), $"'{field}': {ArtGenerator.PathOf(sprite)} exists");
            }
        }

        [Test]
        public void ProjectRuntimeConfig_EveryGeneratedSpriteField_HoldsItsGeneratedPng()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(RuntimeConfig)}");
            Assert.AreEqual(1, guids.Length, "the project has one Runtime Config");
            var config = AssetDatabase.LoadAssetAtPath<RuntimeConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var serialized = new SerializedObject(config);

            foreach (var sprite in ArtPainter.All)
            {
                var field = ArtGenerator.RuntimeConfigField(sprite);
                if (field == null)
                {
                    continue;
                }

                var held = serialized.FindProperty(field).objectReferenceValue;

                Assert.AreEqual(ArtGenerator.PathOf(sprite), AssetDatabase.GetAssetPath(held), $"'{field}'");
            }
        }
    }
}
