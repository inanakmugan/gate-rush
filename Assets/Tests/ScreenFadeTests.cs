using System;
using GateRush.Runtime;
using GateRush.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ScreenFade.Create"/> (Module 47): the fade is built as
    /// the screen canvas's last child, stretched over it, on a canvas of its
    /// own sorting the configured offset above the screen's, with a
    /// raycaster, its image opaque in the given colour, disabled and no
    /// raycast target until a fade plays.
    /// </summary>
    /// <remarks>
    /// The settings come from a default <see cref="UiConfig"/>, the one way the
    /// bootstrap gets them. The fade's motion runs on DOTween, which these
    /// Edit Mode tests do not start; the order of its actions is
    /// <see cref="ScreenFadeSequence"/>'s, covered on its own.
    /// </remarks>
    public class ScreenFadeTests
    {
        private const int ScreenSortingOrder = 3;
        private static readonly Color Background = new Color(0.1f, 0.05f, 0.2f, 0.5f);

        private GameObject screen;
        private Canvas screenCanvas;
        private UiConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<UiConfig>();
            screen = new GameObject("Canvas", typeof(RectTransform));
            screenCanvas = screen.AddComponent<Canvas>();
            screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            screenCanvas.sortingOrder = ScreenSortingOrder;
            new GameObject("Front End", typeof(RectTransform)).transform.SetParent(screen.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(screen);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Create_UnderTheScreenCanvas_SortsItsOffsetAboveIt()
        {
            int offset;
            using (var serialized = new SerializedObject(config))
            {
                offset = serialized.FindProperty("screenFadeSortingOffset").intValue;
            }

            var fade = ScreenFade.Create(screenCanvas, Background, config.CreateScreenFadeSettings());

            Assert.AreEqual(screen.transform, fade.transform.parent);
            Assert.AreEqual(screen.transform.childCount - 1, fade.transform.GetSiblingIndex(), "last under the canvas");
            Assert.IsTrue(fade.Canvas.overrideSorting);
            Assert.AreEqual(ScreenSortingOrder + offset, fade.Canvas.sortingOrder);
            Assert.IsNotNull(fade.GetComponent<GraphicRaycaster>(), "it takes every press while it plays");
        }

        [Test]
        public void Create_Idle_DrawsNothingAndTakesNoPress()
        {
            var fade = ScreenFade.Create(screenCanvas, Background, config.CreateScreenFadeSettings());
            var image = fade.GetComponent<Image>();

            Assert.IsFalse(fade.IsBusy);
            Assert.IsFalse(image.enabled, "nothing is drawn while idle");
            Assert.IsFalse(image.raycastTarget);
            Assert.AreEqual(new Color(Background.r, Background.g, Background.b, 1f), image.color, "the background colour, opaque");
        }

        [Test]
        public void Create_StretchesOverTheScreen()
        {
            var rect = (RectTransform)ScreenFade.Create(screenCanvas, Background, config.CreateScreenFadeSettings()).transform;

            Assert.AreEqual(Vector2.zero, rect.anchorMin);
            Assert.AreEqual(Vector2.one, rect.anchorMax);
            Assert.AreEqual(Vector2.zero, rect.offsetMin);
            Assert.AreEqual(Vector2.zero, rect.offsetMax);
        }

        [Test]
        public void Create_NoCanvas_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ScreenFade.Create(null, Background, config.CreateScreenFadeSettings()));
        }
    }
}
