using System.Collections.Generic;
using GateRush.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="UiTint.AddMissing"/> since Module 45 removed the
    /// coin, heart and joker badge roles: a tint still holding one of their
    /// numbers is reported, naming its object, and a tint with a declared
    /// role and a target is not.
    /// </summary>
    public class UiTintTests
    {
        // The coin role's number before Module 45, now undeclared.
        private const int RemovedCoinRole = 3;

        private GameObject owner;
        private UiTint tint;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Coin", typeof(RectTransform));
            var image = owner.AddComponent<Image>();
            tint = owner.AddComponent<UiTint>();
            using (var serialized = new SerializedObject(tint))
            {
                serialized.FindProperty("target").objectReferenceValue = image;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void AddMissing_RemovedRole_IsReportedByItsObject()
        {
            SetRole(RemovedCoinRole);
            var missing = new List<string>();

            tint.AddMissing(missing);

            Assert.That(missing, Has.Some.Contains("'Coin'").And.Contains("no longer a colour role"), string.Join(" | ", missing));
        }

        [Test]
        public void AddMissing_DeclaredRoleWithTarget_ReportsNothing()
        {
            SetRole((int)UiColorRole.Icon);
            var missing = new List<string>();

            tint.AddMissing(missing);

            CollectionAssert.IsEmpty(missing);
        }

        private void SetRole(int role)
        {
            using (var serialized = new SerializedObject(tint))
            {
                serialized.FindProperty("role").intValue = role;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
