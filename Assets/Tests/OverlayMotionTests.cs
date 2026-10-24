using System.Collections.Generic;
using GateRush.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="OverlayMotion"/>'s states that start no tween
    /// (Module 47): shown at once open or closed, at its open look and taking
    /// input; a close of an overlay that is not open does nothing; the shared
    /// rule leaves an overlay already as it should be alone; and an unassigned
    /// part is reported.
    /// </summary>
    /// <remarks>
    /// The pop and the close themselves run on DOTween, which these Edit Mode
    /// tests do not start; they are checked by hand in Play Mode.
    /// </remarks>
    public class OverlayMotionTests
    {
        private const float ShownScale = 1f;

        private GameObject root;
        private CanvasGroup group;
        private RectTransform panel;
        private OverlayMotion motion;
        private UiConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<UiConfig>();
            root = new GameObject("Overlay", typeof(RectTransform));
            group = root.AddComponent<CanvasGroup>();
            panel = (RectTransform)new GameObject("Panel", typeof(RectTransform)).transform;
            panel.SetParent(root.transform, false);
            motion = root.AddComponent<OverlayMotion>();
            Wire(motion, group, panel);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ShowAt_False_DeactivatesAtTheOpenLook()
        {
            group.alpha = 0.3f;
            group.blocksRaycasts = false;
            panel.localScale = Vector3.one * 0.5f;

            motion.ShowAt(false);

            Assert.IsFalse(root.activeSelf);
            Assert.IsFalse(motion.IsOpen);
            Assert.IsFalse(motion.IsClosing);
            Assert.AreEqual(1f, group.alpha);
            Assert.IsTrue(group.blocksRaycasts, "it takes input again when it next opens");
            Assert.AreEqual(Vector3.one * ShownScale, panel.localScale);
        }

        [Test]
        public void ShowAt_True_IsOpenAndTakesInput()
        {
            motion.ShowAt(false);

            motion.ShowAt(true);

            Assert.IsTrue(root.activeSelf);
            Assert.IsTrue(motion.IsOpen);
            Assert.IsTrue(group.blocksRaycasts);
            Assert.AreEqual(1f, group.alpha);
        }

        [Test]
        public void Close_NotOpen_DoesNothing()
        {
            motion.ShowAt(false);

            motion.Close(config);

            Assert.IsFalse(root.activeSelf);
            Assert.IsFalse(motion.IsClosing);
        }

        [Test]
        public void Apply_AtOnce_ShowsItAsAsked()
        {
            OverlayMotion.Apply(motion, false, false, config);

            Assert.IsFalse(root.activeSelf);
        }

        [Test]
        public void Apply_AnimatedAndAlreadyClosed_LeavesItAlone()
        {
            motion.ShowAt(false);

            OverlayMotion.Apply(motion, false, true, config);

            Assert.IsFalse(root.activeSelf);
            Assert.IsFalse(motion.IsClosing);
        }

        [Test]
        public void AddMissing_PartsUnassigned_ReportsBoth()
        {
            Wire(motion, null, null);
            var missing = new List<string>();

            motion.AddMissing(missing);

            Assert.That(missing, Has.Some.Contains("group"));
            Assert.That(missing, Has.Some.Contains("panel"));
        }

        [Test]
        public void AddMissingOn_OverlayWithoutMotion_ReportsIt()
        {
            var bare = new GameObject("Bare Overlay", typeof(RectTransform));
            try
            {
                var missing = new List<string>();

                OverlayMotion.AddMissingOn(missing, bare.transform, "bare");

                Assert.That(missing, Has.Some.Contains("OverlayMotion").And.Contains("bare"));
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        private static void Wire(OverlayMotion target, CanvasGroup canvasGroup, RectTransform panelRect)
        {
            using (var serialized = new SerializedObject(target))
            {
                serialized.FindProperty("group").objectReferenceValue = canvasGroup;
                serialized.FindProperty("panel").objectReferenceValue = panelRect;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
