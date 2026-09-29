using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Fills the camera's view behind the board with the background (Module
    /// 15): a vertical gradient from <see cref="RuntimeConfig.BackgroundBottom"/>
    /// — the camera's clear colour — up to
    /// <see cref="RuntimeConfig.BackgroundTop"/>, drawn as the generated ramp
    /// sprite, and a soft vignette over it. Two renderers, both children of
    /// this object, refitted whenever the camera is.
    /// </summary>
    public sealed class BackgroundView : MonoBehaviour
    {
        private RuntimeConfig config;
        private SpriteRenderer ramp;
        private SpriteRenderer vignette;

        /// <summary>
        /// Binds the view to <paramref name="config"/>, creating its two
        /// renderers the first time and recolouring them on every call. Call
        /// before <see cref="Fit"/>.
        /// </summary>
        public void Initialize(RuntimeConfig config)
        {
            this.config = config;
            ramp = Prepare(ramp, "Gradient", config.BackgroundRampSprite, config.BackgroundTop, config.BackgroundOrder);
            vignette = Prepare(vignette, "Vignette", config.VignetteSprite, config.VignetteColor, config.VignetteOrder);
        }

        /// <summary>
        /// Centres the background on <paramref name="camera"/> and stretches it
        /// over the camera's whole orthographic view. Call after every change
        /// to the camera's size, position or aspect.
        /// </summary>
        public void Fit(Camera camera)
        {
            if (config == null)
            {
                return;
            }

            var cameraPosition = camera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, transform.position.z);

            var height = camera.orthographicSize * 2f;
            var view = new Vector2(height * camera.aspect, height);
            Stretch(ramp, view);
            Stretch(vignette, view);
        }

        private SpriteRenderer Prepare(SpriteRenderer renderer, string childName, Sprite sprite, Color color, int order)
        {
            if (renderer == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                renderer = go.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.sharedMaterial = config.SpriteMaterial;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>Scales <paramref name="renderer"/> to cover <paramref name="size"/> world units, whatever the sprite's pixels per unit.</summary>
        private static void Stretch(SpriteRenderer renderer, Vector2 size)
        {
            var bounds = renderer.sprite.bounds.size;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
        }
    }
}
