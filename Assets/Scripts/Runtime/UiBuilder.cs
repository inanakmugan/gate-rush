using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// Builds the uGUI parts the HUD and the result panel are made of, one way
    /// for both: rects, rounded boxes cut from the generated rounded-box
    /// sprite, icons, labels in the label font, and buttons. Sizes are in
    /// canvas units.
    /// </summary>
    public static class UiBuilder
    {
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        /// <summary>An empty rect named <paramref name="name"/> under <paramref name="parent"/>, on its layer.</summary>
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Makes <paramref name="rect"/> fill its parent exactly.</summary>
        public static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }

        /// <summary>Anchors <paramref name="rect"/> between <paramref name="min"/> and <paramref name="max"/> of its parent, with no offsets.</summary>
        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Gives <paramref name="rect"/> a fixed <paramref name="size"/>, pinned
        /// by its pivot to the point <paramref name="anchor"/> of its parent
        /// and moved from there by <paramref name="position"/>. An anchor of
        /// (0, 0.5) pins its left edge to the parent's left edge.
        /// </summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Centres <paramref name="rect"/> in its parent at a fixed <paramref name="size"/>.</summary>
        public static void PlaceCentered(RectTransform rect, Vector2 size)
        {
            Place(rect, Center, Vector2.zero, size);
        }

        /// <summary>
        /// A 9-sliced rounded box of <paramref name="size"/> on
        /// <paramref name="rect"/>, whose slice border draws
        /// <paramref name="cornerUnits"/> wide — capped at half the shorter
        /// side, so a corner of half the height makes a pill. A raycast target,
        /// so a press on it is a UI press.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The corner or the size is not positive.</exception>
        public static Image AddRoundedBox(
            RectTransform rect, Vector2 size, Sprite sprite, Color color, float cornerUnits, float referencePixelsPerUnit)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.color = color;
            image.pixelsPerUnitMultiplier = SlicedPixelsPerUnitMultiplier(
                sprite.border.x, sprite.pixelsPerUnit, referencePixelsPerUnit, cornerUnits, size);
            return image;
        }

        /// <summary>
        /// The <see cref="Image.pixelsPerUnitMultiplier"/> at which a sliced
        /// sprite's border of <paramref name="borderPixels"/> draws
        /// <paramref name="cornerUnits"/> canvas units wide, capped at half the
        /// shorter side of <paramref name="size"/> so opposite corners never
        /// overlap. uGUI draws a border at
        /// <c>borderPixels × referencePixelsPerUnit / (spritePixelsPerUnit × multiplier)</c>
        /// units; this solves that for the multiplier. The same capping rule as
        /// the board's sliced sprites.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The corner, the size or a pixels-per-unit value is not positive.</exception>
        public static float SlicedPixelsPerUnitMultiplier(
            float borderPixels, float spritePixelsPerUnit, float referencePixelsPerUnit, float cornerUnits, Vector2 size)
        {
            if (!(spritePixelsPerUnit > 0f && referencePixelsPerUnit > 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spritePixelsPerUnit), spritePixelsPerUnit, "Pixels per unit must be positive.");
            }

            var drawn = Math.Min(cornerUnits, Math.Min(size.x, size.y) * 0.5f);
            if (!(drawn > 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cornerUnits), cornerUnits, "The corner and the box's size must be positive.");
            }

            return borderPixels * referencePixelsPerUnit / (spritePixelsPerUnit * drawn);
        }

        /// <summary>A white icon sprite on <paramref name="rect"/>, tinted <paramref name="color"/>, keeping its aspect. Not a raycast target.</summary>
        public static Image AddIcon(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A centred label on <paramref name="rect"/> in
        /// <paramref name="font"/>. It wraps within its rect only when
        /// <paramref name="wraps"/>; otherwise it stays on one line. Not a
        /// raycast target.
        /// </summary>
        public static TextMeshProUGUI AddLabel(RectTransform rect, TMP_FontAsset font, float fontSize, Color color, bool wraps)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = wraps ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// A button on <paramref name="rect"/> that tints
        /// <paramref name="target"/> as it is pressed. Its navigation is off, so
        /// a click never selects it: a selected button would be pressed again
        /// by a stray Space or Enter, restarting a level the player did not
        /// mean to.
        /// </summary>
        public static Button AddButton(RectTransform rect, Graphic target)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }
    }
}
