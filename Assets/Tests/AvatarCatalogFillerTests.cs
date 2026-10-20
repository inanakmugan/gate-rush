using System;
using System.Collections.Generic;
using GateRush.Runtime;
using GateRush.UI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 38's <see cref="AvatarCatalogFiller"/> over the real
    /// Animal Pack Redux folder: thirty avatars in ordinal order of file name,
    /// the first the bear; each id is its file name and each sprite its file's
    /// one sprite; and the filled catalog reports no problem. Needs every
    /// file in the folder imported as one sprite (Sprite Mode: Single).
    /// </summary>
    public class AvatarCatalogFillerTests
    {
        private const int AvatarCount = 30;
        private const string FirstAvatarId = "bear";
        private const string PngExtension = ".png";

        private AvatarCatalog catalog;

        [OneTimeSetUp]
        public void FillOnce()
        {
            catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
            AvatarCatalogFiller.Fill(catalog, AvatarCatalogFiller.AvatarFolder);
        }

        [OneTimeTearDown]
        public void DestroyCatalog()
        {
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Fill_RoundFolder_ThirtyAvatarsInOrdinalOrderFromTheBear()
        {
            var ids = new List<string>();
            for (var i = 0; i < catalog.Count; i++)
            {
                ids.Add(catalog.IdAt(i));
            }

            var sorted = new List<string>(ids);
            sorted.Sort(StringComparer.Ordinal);

            Assert.AreEqual(AvatarCount, catalog.Count);
            CollectionAssert.AreEqual(sorted, ids, "ordinal order of file name");
            Assert.AreEqual(FirstAvatarId, catalog.IdAt(0), "the first avatar, and the Meta Config's default");
        }

        [Test]
        public void Fill_RoundFolder_EachSpriteIsItsIdsFile()
        {
            var wrong = new List<string>();

            for (var i = 0; i < catalog.Count; i++)
            {
                var expected = $"{AvatarCatalogFiller.AvatarFolder}/{catalog.IdAt(i)}{PngExtension}";
                var actual = AssetDatabase.GetAssetPath(catalog.SpriteAt(i));
                if (actual != expected)
                {
                    wrong.Add($"{catalog.IdAt(i)}: {actual}");
                }
            }

            CollectionAssert.IsEmpty(wrong, "avatars whose sprite is not their own file's");
        }

        [Test]
        public void Fill_RoundFolder_CatalogReportsNoProblem()
        {
            var problems = catalog.Problems();

            CollectionAssert.IsEmpty(problems, string.Join(" | ", problems));
        }
    }
}
