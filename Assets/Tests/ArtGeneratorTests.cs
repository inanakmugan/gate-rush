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
        public void RuntimeConfigField_EveryGeneratedSpriteButTheInterfaces_IsASpriteFieldOfTheConfig()
        {
            foreach (var sprite in ArtPainter.All)
            {
                var field = ArtGenerator.RuntimeConfigField(sprite);
                if (sprite == ArtSprite.Restart || sprite == ArtSprite.RoundGlow)
                {
                    Assert.IsNull(field, $"{sprite} is the interface's, not the board's");
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

        [Test]
        public void ProjectRuntimeConfig_HoldsTheProjectRecipesMeasuresAndTheLabelShadow()
        {
            // Generate Art writes the sizes the sprites were painted at, and
            // the digits' shadow material, with the sprites (Module 46, pass 2).
            var configGuids = AssetDatabase.FindAssets($"t:{nameof(RuntimeConfig)}");
            var recipeGuids = AssetDatabase.FindAssets($"t:{nameof(ArtRecipe)}");
            Assert.AreEqual(1, configGuids.Length, "the project has one Runtime Config");
            Assert.AreEqual(1, recipeGuids.Length, "the project has one Art Recipe");
            var config = AssetDatabase.LoadAssetAtPath<RuntimeConfig>(AssetDatabase.GUIDToAssetPath(configGuids[0]));
            var recipe = AssetDatabase.LoadAssetAtPath<ArtRecipe>(AssetDatabase.GUIDToAssetPath(recipeGuids[0]));
            var serialized = new SerializedObject(config);

            foreach (var (field, value) in recipe.BoardMeasures())
            {
                Assert.AreEqual(value, serialized.FindProperty(field).floatValue, 1e-6f, $"'{field}'");
            }

            Assert.IsNotNull(config.LabelShadowMaterial, "the label shadow material is assigned");
            Assert.IsTrue(config.HasLabelOutline, "the time-bonus seconds' outline font and material are assigned");
        }

        // An atlas padded by p has a gradient scale of p + 1; a bold weight
        // of 0.75 dilates by a quarter of it.
        private const float Weight = 0.75f / 4f;

        // Revision B's stroke: 3 pixels at the count digits' 34, in atlas
        // pixels of Lilita One sampled at 71; and the padding-17 atlas's
        // gradient scale. A field unit spans two gradient scales.
        private const float StrokeB = 3f / 34f * 71f;
        private const float GradientScale17 = 18f;

        [Test]
        public void OutlineFor_RevisionBStroke_IsLinearBelowTheRatiosKnee()
        {
            // 17 usable atlas pixels, two gradient scales a field unit: the
            // reach is W · 2 · 17 while weight + 2W stays at most 1.
            var outline = ArtGenerator.OutlineFor(StrokeB, GradientScale17, Weight);

            Assert.AreEqual(StrokeB / 34f, outline.Width, 1e-5f, "about 0.184");
            Assert.LessOrEqual(Weight + 2f * outline.Width, 1f, "below the knee");
            Assert.AreEqual(StrokeB, outline.ReachTexels, 1e-3f);
            Assert.IsTrue(outline.IsWholeStroke);
        }

        [Test]
        public void OutlineFor_StrokePastTheKnee_SolvesForTheWidthThatReachesIt()
        {
            // Padding 10: W = s · weight / (2 · 10 − 2s) for a stroke s = 9.
            const float stroke = 9f;

            var outline = ArtGenerator.OutlineFor(stroke, 11f, Weight);

            Assert.AreEqual(stroke * Weight / (20f - 2f * stroke), outline.Width, 1e-4f);
            Assert.Greater(Weight + 2f * outline.Width, 1f, "past the knee");
            Assert.AreEqual(stroke, outline.ReachTexels, 1e-3f);
            Assert.IsTrue(outline.IsWholeStroke);
        }

        [Test]
        public void OutlineFor_StrokeThePaddingCannotHold_IsTheWidestOutlineAndNamesThePaddingNeeded()
        {
            // Padding 10 holds at most 2 · 10 / (weight + 2) of a 12-pixel
            // stroke; 12 · (weight + 2) / 2 = 13.1, so 14 is needed.
            const float stroke = 12f;

            var outline = ArtGenerator.OutlineFor(stroke, 11f, Weight);

            Assert.AreEqual(1f, outline.Width, "the slider's most");
            Assert.AreEqual(20f / (Weight + 2f), outline.ReachTexels, 1e-4f, "short of the stroke");
            Assert.IsFalse(outline.IsWholeStroke);
            Assert.AreEqual(14, outline.PaddingNeeded);
            Assert.IsTrue(ArtGenerator.OutlineFor(stroke, outline.PaddingNeeded + 1f, Weight).IsWholeStroke, "the padding named is enough");
        }

        [Test]
        public void EdgesOf_OutlineForsSetting_KeepsTheFaceAtTheGlyphsSizeAndReachesTheStroke()
        {
            var outline = ArtGenerator.OutlineFor(StrokeB, GradientScale17, Weight);

            var edges = ArtGenerator.EdgesOf(outline.Width, outline.Width, GradientScale17, Weight);

            Assert.AreEqual(0f, edges.FaceTexels, 1e-5f, "the white face is the undilated glyph");
            Assert.AreEqual(StrokeB, edges.OuterTexels, 1e-3f, "only the outline reaches out, by the stroke");
        }

        [Test]
        public void EdgesOf_DilationUnequalToTheWidth_MovesTheFaceEdge()
        {
            var thin = ArtGenerator.EdgesOf(0.2f, 0f, GradientScale17, Weight);
            var fat = ArtGenerator.EdgesOf(0.2f, 0.4f, GradientScale17, Weight);

            Assert.Less(thin.FaceTexels, 0f, "an undilated outline eats into the face");
            Assert.Greater(fat.FaceTexels, 0f, "more dilation than width grows the face");
        }

        [Test]
        public void EdgesOf_RevisionASettings_ReachedTwiceTheStrokeAskedFor()
        {
            // Regression: revision A's LabelOutline.mat had width and dilation
            // 0.924 on the padding-17 atlas, worked out with a field unit of
            // one gradient scale. It asked for 5 pixels at 46, 7.72 atlas
            // pixels, and drew twice that, with the face still the glyph's.
            const float revisionAWidth = 0.92447877f;
            const float revisionAStroke = 5f / 46f * 71f;

            var edges = ArtGenerator.EdgesOf(revisionAWidth, revisionAWidth, GradientScale17, Weight);

            Assert.AreEqual(0f, edges.FaceTexels, 1e-5f, "the face was right");
            Assert.AreEqual(2f * revisionAStroke, edges.OuterTexels, 0.05f, "the outline was twice the stroke");
        }

        [Test]
        public void ProjectLabelOutlineMaterial_KeepsTheFaceAtTheGlyphsSizeAndReachesTheConfigsStroke()
        {
            // Reads the material Generate Art made, so it passes once Generate
            // Art has run on the current config.
            var guids = AssetDatabase.FindAssets($"t:{nameof(RuntimeConfig)}");
            Assert.AreEqual(1, guids.Length, "the project has one Runtime Config");
            var config = AssetDatabase.LoadAssetAtPath<RuntimeConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var material = config.LabelOutlineMaterial;
            Assert.IsNotNull(material, "Label Outline Material is assigned");
            Assert.IsNotNull(config.LabelOutlineFont, "Label Outline Font is assigned");

            var width = material.GetFloat("_OutlineWidth");
            var dilate = material.GetFloat("_FaceDilate");
            var gradientScale = material.GetFloat("_GradientScale");
            var weight = Mathf.Max(material.GetFloat("_WeightNormal"), material.GetFloat("_WeightBold")) / 4f;
            var edges = ArtGenerator.EdgesOf(width, dilate, gradientScale, weight);

            Assert.AreEqual(width, dilate, 1e-6f, "dilated by exactly the outline's width");
            Assert.AreEqual(0f, edges.FaceTexels, 1e-4f, "the white face is the undilated glyph");
            Assert.AreEqual(
                ArtGenerator.StrokeTexels(config, config.LabelOutlineFont.faceInfo.pointSize), edges.OuterTexels, 0.01f,
                "the outline stands the config's stroke outside it");
        }
    }
}
