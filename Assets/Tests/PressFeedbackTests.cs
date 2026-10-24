using GateRush.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="PressFeedback"/> (Module 47): the hit padding keeps a
    /// shrunk face's hit area at its resting size; a press on a button that
    /// is not interactable, with another mouse button, or with no
    /// configuration does nothing; and a release with no press does nothing.
    /// </summary>
    /// <remarks>
    /// The shrink, the spring back and the rest on a disable run on DOTween
    /// and on the component's lifecycle, which these Edit Mode tests do not
    /// start; they are checked by hand in Play Mode.
    /// </remarks>
    public class PressFeedbackTests
    {
        private static readonly Vector2 FaceSize = new Vector2(320f, 140f);

        private GameObject root;
        private Button button;
        private Image face;
        private PressFeedback press;
        private UiConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<UiConfig>();
            root = new GameObject("Button", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = FaceSize;
            face = root.AddComponent<Image>();
            button = root.AddComponent<Button>();
            button.targetGraphic = face;
            press = root.AddComponent<PressFeedback>();
            using (var serialized = new SerializedObject(press))
            {
                serialized.FindProperty("config").objectReferenceValue = config;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        [TestCase(0.8f)]
        [TestCase(0.92f)]
        [TestCase(0.95f)]
        public void HitPadding_Shrunk_KeepsTheHitAreaAtItsRestingSize(float scale)
        {
            var padding = PressFeedback.HitPadding(FaceSize, scale);

            var paddedWidth = FaceSize.x - padding.x - padding.z;
            var paddedHeight = FaceSize.y - padding.y - padding.w;
            Assert.AreEqual(FaceSize.x, paddedWidth * scale, 1e-3f, "the hit width, shrunk, is the resting width");
            Assert.AreEqual(FaceSize.y, paddedHeight * scale, 1e-3f, "the hit height, shrunk, is the resting height");
            Assert.That(padding.x, Is.LessThan(0f), "negative padding widens the area");
        }

        [TestCase(1f)]
        [TestCase(1.1f)]
        [TestCase(0f)]
        public void HitPadding_NotShrunk_IsZero(float scale)
        {
            Assert.AreEqual(Vector4.zero, PressFeedback.HitPadding(FaceSize, scale));
        }

        [Test]
        public void OnPointerDown_NotInteractable_DoesNothing()
        {
            button.interactable = false;

            press.OnPointerDown(PointerAt(PointerEventData.InputButton.Left));

            Assert.IsFalse(press.IsPressed);
            Assert.AreEqual(Vector3.one, root.transform.localScale);
            Assert.AreEqual(Vector4.zero, face.raycastPadding);
        }

        [Test]
        public void OnPointerDown_RightButton_DoesNothing()
        {
            press.OnPointerDown(PointerAt(PointerEventData.InputButton.Right));

            Assert.IsFalse(press.IsPressed);
            Assert.AreEqual(Vector3.one, root.transform.localScale);
        }

        [Test]
        public void OnPointerDown_NoConfig_DoesNothing()
        {
            using (var serialized = new SerializedObject(press))
            {
                serialized.FindProperty("config").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            press.OnPointerDown(PointerAt(PointerEventData.InputButton.Left));

            Assert.IsFalse(press.IsPressed);
        }

        [Test]
        public void OnPointerUpAndExit_WithNoPress_DoNothing()
        {
            press.OnPointerUp(PointerAt(PointerEventData.InputButton.Left));
            press.OnPointerExit(PointerAt(PointerEventData.InputButton.Left));

            Assert.IsFalse(press.IsPressed);
            Assert.AreEqual(Vector3.one, root.transform.localScale);
        }

        private static PointerEventData PointerAt(PointerEventData.InputButton which) =>
            new PointerEventData(null) { button = which };
    }
}
